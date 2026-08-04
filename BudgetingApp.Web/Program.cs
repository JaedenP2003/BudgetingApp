using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BudgetingApp.Web;
using BudgetingApp.Web.Import;
using BudgetingApp.Web.Storage;
using BudgetingApp.Web.Summary;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddBlazoredLocalStorage();
// Scoped, not Singleton: Blazored.LocalStorage's ILocalStorageService is registered Scoped,
// and a WASM app only ever has the one root scope anyway, so this behaves as a singleton.
builder.Services.AddScoped<WebBudgetStore>();
builder.Services.AddScoped<WebCsvImportService>();
builder.Services.AddScoped<WebMonthlySummaryService>();

var host = builder.Build();

// Data must be loaded from browser storage before any page renders against it.
await host.Services.GetRequiredService<WebBudgetStore>().InitializeAsync();

await host.RunAsync();
