using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaaBoss.Core.Messaging;
using MaaBoss.Core.Models;
using MaaBoss.Core.Services;
using MaaBoss.Desktop.Infrastructure;

namespace MaaBoss.Desktop.ViewModels;

public partial class CandidateBrowserViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Keyword { get; set; } = "";

    [ObservableProperty]
    public partial string SelectedListType { get; set; } = "recommend";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial Candidate? SelectedCandidate { get; set; }

    public ObservableCollection<Candidate> Candidates { get; } = new();
    public ObservableCollection<string> ListTypes { get; } = new() { "recommend", "new", "nearby", "active" };

    private readonly ControllerService _controller;
    private readonly TaskService _tasks;

    public CandidateBrowserViewModel()
    {
        _controller = ServiceLocator.Get<ControllerService>();
        _tasks = ServiceLocator.Get<TaskService>();

        WeakReferenceMessenger.Default.Register<ConnectionStateChangedMessage>(this, (_, msg) =>
        {
            IsConnected = msg.Value?.Success ?? false;
        });
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        IsBusy = true;
        Candidates.Clear();
        var result = await _tasks.BrowseCandidatesAsync(Keyword, null, null, null, 3, SelectedListType, default);
        if (result.Success && result.Extra != null &&
            result.Extra.TryGetValue("candidates", out var list) &&
            list is System.Collections.IEnumerable candidates)
        {
            foreach (var c in candidates)
            {
                if (c is Candidate cand) Candidates.Add(cand);
            }
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task SwipeDownAsync()
    {
        IsBusy = true;
        await _tasks.SwipeCandidatesAsync("down", 3, 1500, default);
        IsBusy = false;
    }

    [RelayCommand]
    private async Task ViewDetailAsync(Candidate? candidate)
    {
        if (candidate == null) return;
        IsBusy = true;
        SelectedCandidate = candidate;
        await _tasks.ViewCandidateDetailAsync(candidate.Name, true, default);
        IsBusy = false;
    }

    [RelayCommand]
    private async Task GreetAsync(Candidate? candidate)
    {
        if (candidate == null) return;
        IsBusy = true;
        await _tasks.GreetCandidateAsync(candidate.Name, null, true, default);
        IsBusy = false;
    }

    [RelayCommand]
    private async Task BatchGreetAsync()
    {
        IsBusy = true;
        await _tasks.BatchGreetAsync(10, true, default);
        IsBusy = false;
    }
}
