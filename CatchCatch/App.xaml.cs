using System.Windows;
using CatchCatch.Services;
using CatchCatch.Views;

namespace CatchCatch;

public partial class App : Application
{
    private AppCoordinator? _coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _coordinator = new AppCoordinator();
        _coordinator.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _coordinator?.Dispose();
        base.OnExit(e);
    }
}
