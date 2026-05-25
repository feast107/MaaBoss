using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaaBoss.Desktop.Services;

namespace MaaBoss.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string WindowTitle { get; set; } = "MaaBoss - 招聘端自动化助手";

    [ObservableProperty]
    public partial ViewModelBase? CurrentViewModel { get; set; }

    public ObservableCollection<NavItem> NavigationItems { get; } = new();

    public MainWindowViewModel()
    {
        var navService = new NavigationService();

        // 注册所有页面
        navService.RegisterViewModel("Dashboard", () => ServiceLocator.Get<DashboardViewModel>());
        navService.RegisterViewModel("Candidates", () => new CandidateBrowserViewModel());
        navService.RegisterViewModel("Chat", () => new ChatViewModel());
        navService.RegisterViewModel("Debug", () => new DebugViewModel());
        navService.RegisterViewModel("FlowEditor", () => new FlowEditorViewModel());
        navService.RegisterViewModel("Settings", () => ServiceLocator.Get<SettingsViewModel>());

        // 导航项列表
        NavigationItems.Add(new NavItem("仪表盘", "Dashboard"));
        NavigationItems.Add(new NavItem("候选人", "Candidates"));
        NavigationItems.Add(new NavItem("消息", "Chat"));
        NavigationItems.Add(new NavItem("调试", "Debug"));
        NavigationItems.Add(new NavItem("流程", "FlowEditor"));
        NavigationItems.Add(new NavItem("设置", "Settings"));

        // 默认首页
        navService.NavigateTo("Dashboard");
        CurrentViewModel = navService.CurrentViewModel;

        // 监听导航服务属性变化同步到当前 ViewModel
        navService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NavigationService.CurrentViewModel))
                CurrentViewModel = navService.CurrentViewModel;
        };

        _navigateCommand = new RelayCommand<NavItem?>(item =>
        {
            if (item != null)
                navService.NavigateTo(item.Route);
        });
    }

    private readonly RelayCommand<NavItem?> _navigateCommand;

    public IRelayCommand<NavItem?> NavigateCommand => _navigateCommand;
}

public class NavItem
{
    public string Title { get; }
    public string Route { get; }

    public NavItem(string title, string route)
    {
        Title = title;
        Route = route;
    }
}
