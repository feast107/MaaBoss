using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using MaaBoss.Desktop.ViewModels;

namespace MaaBoss.Desktop.Infrastructure;

/// <summary>
/// 导航服务，统一管理页面跳转和 ViewModel 生命周期。
/// </summary>
public partial class NavigationService : ObservableObject
{
    [ObservableProperty]
    public partial ViewModelBase? CurrentViewModel { get; set; }

    private readonly Dictionary<string, Func<ViewModelBase>> _viewModelFactories = new();

    public void RegisterViewModel(string key, Func<ViewModelBase> factory)
    {
        _viewModelFactories[key] = factory;
    }

    public void NavigateTo(string key)
    {
        if (_viewModelFactories.TryGetValue(key, out var factory))
        {
            CurrentViewModel = factory();
        }
        else
        {
            throw new InvalidOperationException($"未注册的视图: {key}");
        }
    }

    public void NavigateTo(ViewModelBase viewModel)
    {
        CurrentViewModel = viewModel;
    }
}
