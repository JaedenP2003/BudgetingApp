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

### Phase 4 — Deploy & Access from iPad — DONE (GitHub Pages, real HTTPS)
- LAN self-hosting (bind `0.0.0.0`) was the first pass, but the user separately stood up `https://jaedenp2003.github.io/BudgetingApp/` — a proper public HTTPS static host, which resolves the LAN plan's service-worker/secure-context caveat entirely. That's now the primary way to access the app; the LAN option (`launchSettings.json`'s `http` profile bound to `0.0.0.0:5297`) still works for local dev/testing.
- The repo (`JaedenP2003/BudgetingApp`) had been pushed but Pages 404'd — nothing had ever built the Blazor app or told Pages what to serve. Added `.github/workflows/deploy-pages.yml`: `dotnet publish` on every push to `main`, then three GitHub-Pages-specific fixups the Blazor SDK doesn't do on its own:
  - Rewrite `<base href>` (index.html) and the service worker's own base-path constant for the `/BudgetingApp/` subpath — both default to `/` and there's no way to make the SDK subpath-aware at publish time.
  - `.nojekyll` so GitHub doesn't run the output through Jekyll and silently drop the `_framework` folder (Jekyll ignores any directory starting with `_`).
  - Serve `index.html` as `404.html` too, so a direct link or refresh on a client route (e.g. `/transactions`) doesn't hit a real 404 — GitHub Pages has no server-side rewrites, so this is the standard SPA-on-Pages trick.
- Hit one non-obvious bug along the way: the base-href rewrite edits `index.html`'s bytes *after* `dotnet publish` already recorded its SHA-256 into `service-worker-assets.js` for the service worker's Subresource-Integrity check. The stale hash made that one fetch fail, which aborted the *entire* install step (one bad hash fails the whole `cache.addAll`) — so the service worker registered but silently never reached "activated," and nothing was actually cached offline. Fixed by recomputing and patching the hash in the same workflow step. Verified live: service worker reaches `activated`, 76 assets cached, second load has `serviceWorker.controller` set (page is actually being served from cache), and a direct deep link to `/transactions` resolves correctly.
- Also cleaned up the repo while in there: 1,428 `bin`/`obj` files had been committed by mistake (`.gitignore` only excluded `/.claude`), and the unused Bootstrap lib (replaced by `theme.css` earlier) was still sitting in `wwwroot`.
- Bonus: **desktop → web data migration**, since the user has ~2 years of real desktop data (2,644 transactions, 62 category rules) they didn't want to re-enter. `BudgetingApp.ExportTool` (new console project, references Core) reads the desktop SQLite DB through the existing repositories and writes one JSON file shaped to match `WebBudgetStore`'s six localStorage keys exactly. `WebBudgetStore.RestoreAsync` + a new `Pages/RestoreBackup.razor` page load that file and replace everything in browser storage. Verified end-to-end against the real database — all counts round-tripped exactly, totals came out right, all 2,644 transaction rows rendered fine.

### Phase 5 — Polish
- Tighten up mobile-friendly layout/touch targets for iPad screen size.
- Add any quality-of-life features that only matter on iPad (e.g., quicker CSV import flow via Files app integration).
- Not yet tested on an actual iPad — everything so far has been verified via automated browser tooling on desktop viewports.

## Tech Stack Summary

| Layer | Technology |
|---|---|
| Shared logic | .NET class library (`net8.0`) |
| Desktop UI | WPF/WinForms (existing) |
| iPad UI | Blazor WebAssembly + PWA |
| Hosting | GitHub Pages (`jaedenp2003.github.io/BudgetingApp/`), auto-deployed via GitHub Actions on push to `main` |
| Data migration | `BudgetingApp.ExportTool` (desktop DB → JSON) + web app's Restore Backup page |

## Open Questions

- ~~Should desktop and iPad share live data, or are two independent budgets acceptable for now?~~ Resolved for now: independent (see Phase 2) — but a one-time desktop→web migration path exists now (see Phase 4) for carrying existing data over.
- ~~Any existing local database (SQLite, LiteDB, flat file) that needs a WASM-compatible equivalent?~~ Resolved: browser local storage via `Blazored.LocalStorage`, not SQLite-in-WASM.
- ~~Actual hosting choice for Phase 4?~~ Resolved: GitHub Pages, real HTTPS, auto-deploys on push.
- Still open: budget-setting UI (absent from both frontends, not just web); real-device testing on an actual iPad (everything verified via automated browser tooling so far, not a physical device); Windows Firewall may still block LAN-mode access from other devices if that path is used instead of the Pages URL.
