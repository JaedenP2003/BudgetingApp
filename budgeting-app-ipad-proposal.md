# Budgeting App: iPad Access Proposal

## Problem

The budgeting app currently exists as a WPF/WinForms desktop app. WPF/WinForms is Windows-only — there's no path to run it on iPadOS. To use the app from the iPad (10th gen, 128GB), the app needs a UI layer that can run in a browser or as an installed web app, without giving up the existing C# business logic.

## Goals

- Reuse existing C# budgeting logic (CSV import, categorization, expected-vs-actual calculations, models) with minimal rewrite.
- Access the app from Safari on iPad, installable to the home screen (PWA) for an app-like feel.
- Work offline where possible — no dependency on a host machine being powered on and reachable.
- Keep the existing WPF desktop app functional on the PC (not a replacement, an additional frontend).

## Non-Goals (for this phase)

- A native App Store iPad app (would require MAUI + Mac tooling + Apple Developer account — revisit later if needed).
- Multi-user support / cloud sync (single-user, single-device scope for now).

## Proposed Architecture

```
BudgetApp.Core        <-- shared class library (no UI dependencies)
  ├── Models           (Transaction, Category, BudgetPeriod, etc.)
  ├── Services          (CSV import/parsing, categorization rules, calculations)
  └── Persistence       (data storage/access layer)

BudgetApp.Desktop      <-- existing WPF/WinForms app, references Core
BudgetApp.Web           <-- new Blazor WebAssembly PWA, references Core
```

Both frontends call into the same `BudgetApp.Core` library, so business logic is written once and used by both the desktop app and the iPad-accessible web app.

## Migration Phases

### Phase 1 — Extract Core Library — DONE
- `BudgetingApp.Core` already exists as a clean class library (`net10.0`, no UI deps): Models, Storage, Import, Categorization, Summary.
- `BudgetingApp.App` (WPF) references it via `AppServices`. Nothing further needed here.

### Phase 2 — Data Access Decision — DONE (independent store, as planned)
- Went with the doc's own "simplest option": the web app is its own independent data store, not synced with desktop SQLite.
- Reason it's not SQLite-in-WASM: `Microsoft.Data.Sqlite` has no durable persistence story in Blazor WebAssembly without extra OPFS/IndexedDB virtual-filesystem plumbing. Revisit only if syncing becomes a real pain point.
- `BudgetingApp.Core/AssemblyInfo.cs` grants `InternalsVisibleTo("BudgetingApp.Web")` so the web project can reuse Core's internal CSV/money-parsing helpers (`ColumnMapping`, `MoneyParsing`, `RecurringExpenseOccurrences`) instead of re-deriving that logic.

### Phase 3 — Blazor WebAssembly Frontend — DONE (MVP)
- `BudgetingApp.Web` created (`dotnet new blazorwasm --pwa`), references `BudgetingApp.Core`, added to `BudgetingApp.slnx`.
- `BudgetingApp.Web/Storage/WebBudgetStore.cs` — in-memory store mirrored to browser local storage (via `Blazored.LocalStorage`) as JSON. Seeds the same default categories/Checking account as a fresh desktop install.
- `BudgetingApp.Web/Import/WebCsvImportService.cs` — browser-side CSV import, reuses Core's `ColumnMapping`/`MoneyParsing` and `CategorizationEngine.MatchCategory` directly.
- `BudgetingApp.Web/Summary/WebMonthlySummaryService.cs` — port of Core's `MonthlySummaryService` against the browser store.
- Pages: `Pages/Home.razor` (monthly summary), `Pages/Transactions.razor` (list + manual categorization), `Pages/Import.razor` (CSV upload), `Pages/CategoryRules.razor`, `Pages/RecurringExpenses.razor` (inline-editable grid, writes through on every field change like the desktop grid).
- Verified: full solution builds; DI wiring resolves (`ILocalStorageService` is Scoped, so `WebBudgetStore` etc. must be Scoped too, not Singleton); import→categorize→summary pipeline verified end-to-end against `sample-data/CheckingTransactions.csv` (118/118 rows parsed, correct signs, correct totals); add/edit/delete flows verified for category rules and recurring expenses.
- Visual theme: `wwwroot/css/theme.css` ports the desktop app's "Sheikah Slate" palette/typography (`BudgetingApp.App/Theme/AppTheme.xaml`) — void/surface colors, cyan accent, Consolas display font, sharp-cornered stat/category cards with left accent bars, dark data tables. Sidebar nav and PWA icons (glowing cyan diamond mark) match the desktop app's look.
- `Pages/Accounts.razor` and `Pages/Trends.razor` added since. Trends uses a hand-rolled SVG chart component (`Shared/TrendChart.razor`) — grouped income/expense bars + a savings line for the overview, a per-category trend line with a category picker — instead of a JS charting library, keeping the same "Sheikah Slate" theme colors (`ChartTheme.cs`) with zero extra dependencies. Verified against seeded multi-month data (bar heights and axis scaling match the underlying numbers).
- Not built (also absent from the desktop app, so not a web-specific gap): a budget-setting UI. `BudgetRepository`/`WebBudgetStore.UpsertBudgetAsync` exist but neither frontend has a screen for it.

### Phase 4 — Deploy & Access from iPad — DONE (LAN self-host; see caveat)
- Went with self-hosting on the home network rather than a public static host: `Properties/launchSettings.json`'s `http` profile now binds Kestrel to `0.0.0.0:5297` instead of `localhost:5297`, so any device on the same Wi-Fi can reach it. No GitHub/Azure account needed.
- Verified reachable at `http://<PC's LAN IP>:5297` (confirmed via curl and a real page load from a non-localhost origin — title/routing rendered correctly).
- **Important caveat**: Service workers (what makes "Add to Home Screen" install a true offline-caching PWA) only register in a "secure context" — HTTPS, or `localhost` specifically. A plain-HTTP LAN IP does **not** qualify, so the offline-install piece of the original Goals section won't work over `http://192.168.x.x:...`. Everything else works fine over LAN HTTP (import, categorize, edit, browse — `localStorage` has no secure-context requirement), it just needs the PC's server running and both devices on the same network each time, rather than being a true installed-and-cached PWA.
- To get genuine offline PWA install, the original plan's public static host (GitHub Pages / Azure Static Web Apps) is still the way — both issue real trusted HTTPS certs for free. That needs the user to have (or create) an account there; not something doable unilaterally.
- Possible firewall snag: no inbound rule for `dotnet.exe`/port 5297 currently exists. If the iPad can't connect despite the server running, Windows Firewall may be blocking it — ask before adding a rule, since that's a system-settings change.

### Phase 5 — Polish
- Tighten up mobile-friendly layout/touch targets for iPad screen size.
- Add any quality-of-life features that only matter on iPad (e.g., quicker CSV import flow via Files app integration).

## Tech Stack Summary

| Layer | Technology |
|---|---|
| Shared logic | .NET class library (`net8.0`) |
| Desktop UI | WPF/WinForms (existing) |
| iPad UI | Blazor WebAssembly + PWA |
| Hosting | Static file host (GitHub Pages / Azure Static Web Apps) |

## Open Questions

- ~~Should desktop and iPad share live data, or are two independent budgets acceptable for now?~~ Resolved for now: independent (see Phase 2).
- ~~Any existing local database (SQLite, LiteDB, flat file) that needs a WASM-compatible equivalent?~~ Resolved: browser local storage via `Blazored.LocalStorage`, not SQLite-in-WASM.
- Still open: category-rule/recurring-expense/budget management screens on web (exist on desktop, not yet ported); actual hosting choice for Phase 4; real-device testing on the iPad itself.
