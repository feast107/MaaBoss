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

public partial class ChatViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string ReplyMessage { get; set; } = "";

    [ObservableProperty]
    public partial ChatMessage? SelectedMessage { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<string> FilterTypes { get; } = new() { "all", "replied", "interview", "system" };

    [ObservableProperty]
    public partial string SelectedFilter { get; set; } = "all";

    private readonly ControllerService _controller;
    private readonly TaskService _tasks;

    public ChatViewModel()
    {
        _controller = ServiceLocator.Get<ControllerService>();
        _tasks = ServiceLocator.Get<TaskService>();

        WeakReferenceMessenger.Default.Register<ConnectionStateChangedMessage>(this, (_, msg) =>
        {
            IsConnected = msg.Value?.Success ?? false;
        });
    }

    [RelayCommand]
    private async Task LoadMessagesAsync()
    {
        IsBusy = true;
        Messages.Clear();
        var result = await _tasks.GetUnreadMessagesAsync(SelectedFilter, default);
        if (result.Success && result.Extra != null &&
            result.Extra.TryGetValue("messages", out var list) &&
            list is System.Collections.IEnumerable msgs)
        {
            foreach (var m in msgs)
            {
                if (m is ChatMessage msg) Messages.Add(msg);
            }
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task SendReplyAsync()
    {
        if (SelectedMessage == null || string.IsNullOrWhiteSpace(ReplyMessage)) return;
        IsBusy = true;
        await _tasks.SendMessageAsync(SelectedMessage.ContactName, ReplyMessage, false, 30, default);
        ReplyMessage = "";
        await LoadMessagesAsync();
        IsBusy = false;
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        IsBusy = true;
        await _tasks.MarkAllReadAsync(default);
        await LoadMessagesAsync();
        IsBusy = false;
    }
}
