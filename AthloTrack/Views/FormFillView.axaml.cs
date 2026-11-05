using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AthloTrack.ViewModels;

namespace AthloTrack.Views;

public partial class FormFillView : UserControl
{
    public FormFillView()
    {
        InitializeComponent();
    }

    /// <summary>«Λήψη PDF»: the save dialog of the platform (Android's file chooser, the browser, Windows).</summary>
    private async void OnDownloadPdf(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FormFillViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            if (vm.CreatePdf() is not { } pdf) return;
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
        PdfMessage.Text = message;
        PdfMessage.Foreground = (Avalonia.Media.IBrush?)this.FindResource(ok ? "SuccessBrush" : "DangerBrush");
        PdfMessage.IsVisible = true;
    }
}
