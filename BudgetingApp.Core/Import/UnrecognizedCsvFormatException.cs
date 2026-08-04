namespace BudgetingApp.Core.Import;

/// <summary>
/// Thrown when the CSV header row doesn't contain columns the importer can map to a
/// date, description, and amount. v1 only recognizes common bank export layouts;
/// a column-mapping UI to handle arbitrary layouts is P1 (see project spec).
/// </summary>
public class UnrecognizedCsvFormatException(string message) : Exception(message);
