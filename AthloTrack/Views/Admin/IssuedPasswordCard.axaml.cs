using System;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using AthloTrack.ViewModels;

namespace AthloTrack.Views;

public partial class IssuedPasswordCard : UserControl
{
    public IssuedPasswordCard()
    {
        InitializeComponent();
    }

    private async void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IssuedPassword issued || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        try
        {
            await clipboard.SetTextAsync(issued.Password);
            CopyText.Text = "Αντιγράφηκε";
        }
        catch (Exception ex)
        {
            // async void: never let it escape (some browsers refuse clipboard access).
            Console.WriteLine($"[AthloTrack] Copy failed: {ex.Message}");
            CopyText.Text = "Επίλεξε και αντίγραψε";
        }
    }
}
