using System.IO;
using System.Windows;

namespace BudgetingApp.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BudgetingApp");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "budget.db");

        var services = new AppServices(dbPath);
        new MainWindow(services).Show();
    }
}

