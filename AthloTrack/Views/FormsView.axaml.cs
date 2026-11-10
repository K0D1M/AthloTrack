using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AthloTrack.ViewModels;

namespace AthloTrack.Views;

public partial class FormsView : UserControl
{
    public FormsView()
    {
        InitializeComponent();
    }

    /// <summary>«Κατέβασμα» on a submitted copy: its PDF, with the platform's save dialog.</summary>
    private async void OnDownload(object? sender, RoutedEventArgs e)
    {
        e.Handled = true; // not the row: that opens the answers
        if ((sender as Control)?.DataContext is not CompletedFormItem item || TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            if (item.CreatePdf() is not { } pdf) return;
            if (await AthloTrack.Services.FileSaver.SaveAsync(top, pdf.FileName, "application/pdf", pdf.Bytes) is { } where)
                Show($"Αποθηκεύτηκε: {where}", ok: true);
        }
        catch (Exception ex)
        {
            // async void: never let a save error take the app down.
            Show($"Το PDF δεν αποθηκεύτηκε: {ex.Message}", ok: false);
        }
    }

    private void Show(string message, bool ok)
    {
        SavedMessage.Text = message;
        // The theme's brush (light/dark); a plain cast of FindResource crashed on Android.
        if (this.TryFindResource(ok ? "SuccessBrush" : "DangerBrush", ActualThemeVariant, out var brush)
            && brush is Avalonia.Media.IBrush b)
            SavedMessage.Foreground = b;
        SavedMessage.IsVisible = true;
    }
}
