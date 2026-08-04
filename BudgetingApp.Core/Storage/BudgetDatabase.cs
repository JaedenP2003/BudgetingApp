using Microsoft.Data.Sqlite;

namespace BudgetingApp.Core.Storage;

/// <summary>
/// Owns the SQLite connection string and schema creation. Every repository opens its
/// own short-lived connection against this same file, matching the pattern used
/// elsewhere in this environment's WPF apps (see CalendarApp.Core.Storage.EventStore).
/// </summary>
public class BudgetDatabase
{
    public string ConnectionString { get; }

    public BudgetDatabase(string dbPath)
    {
        ConnectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS categories (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                is_income INTEGER NOT NULL CHECK (is_income IN (0, 1)),
                is_transfer INTEGER NOT NULL DEFAULT 0 CHECK (is_transfer IN (0, 1))
            );

            CREATE TABLE IF NOT EXISTS accounts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                type TEXT NOT NULL CHECK (type IN ('checking', 'savings', 'credit_card'))
            );

            -- One row per match pattern; a rule points at exactly one category_id,
            -- so a matching transaction can never be summed into two buckets.
            CREATE TABLE IF NOT EXISTS category_rules (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                category_id INTEGER NOT NULL REFERENCES categories (id),
                match_type TEXT NOT NULL CHECK (match_type IN ('contains', 'exact', 'regex')),
                pattern TEXT NOT NULL,
                priority INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_category_rules_priority ON category_rules (priority);

            CREATE TABLE IF NOT EXISTS recurring_expenses (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                category_id INTEGER NOT NULL REFERENCES categories (id),
                expected_amount_cents INTEGER NOT NULL,
                cadence TEXT NOT NULL CHECK (cadence IN ('weekly', 'biweekly', 'monthly', 'yearly')),
                start_date TEXT NOT NULL,
                end_date TEXT,
                note TEXT
            );

            CREATE TABLE IF NOT EXISTS transactions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                posting_date TEXT NOT NULL,
                description TEXT NOT NULL,
                amount_cents INTEGER NOT NULL,
                category_id INTEGER REFERENCES categories (id),
                account_id INTEGER NOT NULL REFERENCES accounts (id),
                source_file TEXT NOT NULL,
                raw_row TEXT NOT NULL,
                imported_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS budgets (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                category_id INTEGER NOT NULL REFERENCES categories (id),
                month TEXT NOT NULL,
                expected_amount_cents INTEGER NOT NULL,
                UNIQUE (category_id, month)
            );
            """;
        command.ExecuteNonQuery();

        SeedCategories(connection);
        MigrateLegacyTransactionsTable(connection);
        MigrateCategoriesIsTransferColumn(connection);

        // Safe to run unconditionally now: both the fresh-create and migration paths
        // above guarantee transactions.account_id exists by this point.
        var indexes = connection.CreateCommand();
        indexes.CommandText =
            """
            CREATE INDEX IF NOT EXISTS idx_transactions_posting_date ON transactions (posting_date);
            CREATE INDEX IF NOT EXISTS idx_transactions_category ON transactions (category_id);
            CREATE INDEX IF NOT EXISTS idx_transactions_account ON transactions (account_id);
            """;
        indexes.ExecuteNonQuery();
    }

    /// <summary>
    /// A database created before multi-account support won't have transactions.account_id
    /// (CREATE TABLE IF NOT EXISTS above is a no-op against an existing table). Rebuild the
    /// table with the new NOT NULL column, backfilling every existing row to a default
    /// "Checking" account so no previously-imported data is lost or orphaned.
    /// </summary>
    private static void MigrateLegacyTransactionsTable(SqliteConnection connection)
    {
        var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(transactions);";
        using (var reader = pragma.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "account_id", StringComparison.OrdinalIgnoreCase))
                {
                    return; // already migrated
                }
            }
        }

        using var migration = connection.BeginTransaction();

        var ensureChecking = connection.CreateCommand();
        ensureChecking.Transaction = migration;
        ensureChecking.CommandText =
            """
            INSERT INTO accounts (name, type)
            SELECT 'Checking', 'checking'
            WHERE NOT EXISTS (SELECT 1 FROM accounts WHERE name = 'Checking');
            SELECT id FROM accounts WHERE name = 'Checking';
            """;
        var defaultAccountId = (long)ensureChecking.ExecuteScalar()!;

        var rebuild = connection.CreateCommand();
        rebuild.Transaction = migration;
        rebuild.CommandText =
            $"""
            CREATE TABLE transactions_new (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                posting_date TEXT NOT NULL,
                description TEXT NOT NULL,
                amount_cents INTEGER NOT NULL,
                category_id INTEGER REFERENCES categories (id),
                account_id INTEGER NOT NULL REFERENCES accounts (id),
                source_file TEXT NOT NULL,
                raw_row TEXT NOT NULL,
                imported_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            INSERT INTO transactions_new (id, posting_date, description, amount_cents, category_id, account_id, source_file, raw_row, imported_at)
            SELECT id, posting_date, description, amount_cents, category_id, {defaultAccountId}, source_file, raw_row, imported_at
            FROM transactions;
            DROP TABLE transactions;
            ALTER TABLE transactions_new RENAME TO transactions;
            CREATE INDEX IF NOT EXISTS idx_transactions_posting_date ON transactions (posting_date);
            CREATE INDEX IF NOT EXISTS idx_transactions_category ON transactions (category_id);
            CREATE INDEX IF NOT EXISTS idx_transactions_account ON transactions (account_id);
            """;
        rebuild.ExecuteNonQuery();

        migration.Commit();
    }

    private static void SeedCategories(SqliteConnection connection)
    {
        var existing = connection.CreateCommand();
        existing.CommandText = "SELECT COUNT(*) FROM categories;";
        var count = (long)existing.ExecuteScalar()!;
        if (count > 0) return;

        (string Name, bool IsIncome, bool IsTransfer)[] seed =
        [
            ("Paycheck", true, false),
            ("Other Income", true, false),
            ("Tithing", false, false),
            ("Rent", false, false),
            ("Utilities", false, false),
            ("Groceries", false, false),
            ("Dining Out", false, false),
            ("Gas", false, false),
            ("Entertainment", false, false),
            ("Subscriptions", false, false),
            ("Shopping", false, false),
            ("Misc", false, false),
            ("Investments", false, false),
            // Money moving between the household's own tracked accounts — excluded from
            // Total Income/Expenses (see MonthlySummaryService) so a transfer isn't counted
            // as both an expense on the sending account and income on the receiving one.
            ("Transfer", false, true)
        ];

        using var transaction = connection.BeginTransaction();
        var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO categories (name, is_income, is_transfer) VALUES ($name, $isIncome, $isTransfer);";
        var nameParam = insert.CreateParameter(); nameParam.ParameterName = "$name";
        var isIncomeParam = insert.CreateParameter(); isIncomeParam.ParameterName = "$isIncome";
        var isTransferParam = insert.CreateParameter(); isTransferParam.ParameterName = "$isTransfer";
        insert.Parameters.Add(nameParam);
        insert.Parameters.Add(isIncomeParam);
        insert.Parameters.Add(isTransferParam);

        foreach (var (name, isIncome, isTransfer) in seed)
        {
            nameParam.Value = name;
            isIncomeParam.Value = isIncome ? 1 : 0;
            isTransferParam.Value = isTransfer ? 1 : 0;
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    /// <summary>
    /// A database created before the Transfer category existed won't have is_transfer on
    /// categories, and won't have a "Transfer" row (SeedCategories only runs once, against
    /// an empty table). Add the column if missing, then ensure the row exists either way —
    /// safe to run on every startup.
    /// </summary>
    private static void MigrateCategoriesIsTransferColumn(SqliteConnection connection)
    {
        var hasColumn = false;
        var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(categories);";
        using (var reader = pragma.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "is_transfer", StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (!hasColumn)
        {
            var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE categories ADD COLUMN is_transfer INTEGER NOT NULL DEFAULT 0 CHECK (is_transfer IN (0, 1));";
            alter.ExecuteNonQuery();
        }

        var ensureTransfer = connection.CreateCommand();
        ensureTransfer.CommandText =
            """
            INSERT INTO categories (name, is_income, is_transfer)
            SELECT 'Transfer', 0, 1
            WHERE NOT EXISTS (SELECT 1 FROM categories WHERE name = 'Transfer');
            """;
        ensureTransfer.ExecuteNonQuery();
    }
}
