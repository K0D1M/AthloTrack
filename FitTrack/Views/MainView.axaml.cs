
using Avalonia;
using Avalonia.Controls;
using FitTrack.ViewModels;
using System;

namespace FitTrack.Views;

public partial class MainView : DrawerPage
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        UpdatePage(DrawerList.SelectedIndex);
    }

    private void DrawerList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ContentPage != null && sender is ListBox listbox)
        {
            var index = listbox.SelectedIndex;
            UpdatePage(index);
        }
    }

    private void UpdatePage(int index)
    {
        ViewModelBase page = index switch
        {
            0 => new RecentViewModel(),
            1 => new AthletesViewModel(),
            2 => new WorkoutsViewModel(),
            3 => new CalendarViewModel(),
            4 => new AboutViewModel(),
            5 => new SettingsViewModel(),
            _ => throw new NotImplementedException()
        };

        ContentPage.Content = page;

        IsOpen = false;
    }
}
