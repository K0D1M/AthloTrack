using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AthloTrack.ViewModels;

namespace AthloTrack.Views;

public partial class FormEditorView : UserControl
{
    public FormEditorView()
    {
        InitializeComponent();
    }

    /// <summary>«Εισαγωγή από Word»: pick a .docx (phone, browser or PC) and hand its text to the form.</summary>
    private async void OnImportWord(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FormEditorViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Αρχείο Word",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Word (.docx)")
                    {
                        Patterns = ["*.docx"],
                        MimeTypes = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
                    },
                ],
            });
            if (files.Count == 0) return;
            await using var stream = await files[0].OpenReadAsync();
            // ZipArchive needs to seek: copy phone and browser streams first.
            using var copy = new System.IO.MemoryStream();
            await stream.CopyToAsync(copy);
            copy.Position = 0;
            vm.ImportDocx(copy);
        }
        catch (Exception ex)
        {
            // async void: never let a picker error take the app down.
            vm.FormError = $"Το αρχείο Word δεν άνοιξε: {ex.Message}";
        }
    }
}
