using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using MaaBoss.Core.Messaging;
using MaaBoss.Core.Services;
using MaaBoss.Desktop.Infrastructure;
using MaaBoss.Desktop.ViewModels;
using MaaBoss.Desktop.Views;

namespace MaaBoss.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 注册共享服务
        var controllerService = new ControllerService();
        var taskService = new TaskService(controllerService);
        var logService = new LogService();
        var settingsVm = new SettingsViewModel();

        ServiceLocator.Register(controllerService);
        ServiceLocator.Register(taskService);
        ServiceLocator.Register(logService);
        ServiceLocator.Register(settingsVm);

        var dashboardVm = new DashboardViewModel();
        ServiceLocator.Register(dashboardVm);

        // 日志服务通过 Messenger 广播
        logService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LogService.LogText))
            {
                WeakReferenceMessenger.Default.Send(new LogMessage(logService.LogText));
            }
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
