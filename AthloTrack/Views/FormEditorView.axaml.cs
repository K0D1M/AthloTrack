using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AthloTrack.Core.Forms;
using AthloTrack.ViewModels;

namespace AthloTrack.Views;

public partial class FormEditorView : UserControl
{
    public FormEditorView()
    {
        InitializeComponent();
        FieldsPreview.FieldTapped += OnFieldTapped;
    }

    /// <summary>«+ Κείμενο», «+ Κουτάκι», …: a field at the cursor (tick boxes optional, like Word's).</summary>
    private void OnInsertField(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not string tag || !Enum.TryParse<FormFieldKind>(tag, out var kind)) return;
        if (DataContext is FormEditorViewModel { IsLocked: true } vm)
        {
            vm.FormError = "Κάποιος αθλητής έχει ήδη υποβάλει το έντυπο, οπότε τα πεδία δεν αλλάζουν. Για άλλα πεδία, φτιάξε «Αντίγραφο».";
            return;
        }
        IntroEditor.Insert(FormFields.MarkerFor(kind, required: kind != FormFieldKind.Tick));
    }

    /// <summary>A field's chip: its kind, required or not, or remove it.</summary>
    private void OnFieldTapped(FormField field, Control chip)
    {
        if (DataContext is not FormEditorViewModel vm) return;
        var menu = new MenuFlyout();
        foreach (var kind in Enum.GetValues<FormFieldKind>())
        {
            var item = new MenuItem
            {
                Header = FormFields.Name(kind),
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = kind == field.Kind,
            };
            item.Click += (_, _) => vm.ChangeField(field, kind: kind);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var required = new MenuItem { Header = "Υποχρεωτικό", ToggleType = MenuItemToggleType.CheckBox, IsChecked = field.Required };
        required.Click += (_, _) => vm.ChangeField(field, required: !field.Required);
        menu.Items.Add(required);
        var remove = new MenuItem { Header = "Διαγραφή πεδίου" };
        remove.Click += (_, _) => vm.ChangeField(field, remove: true);
        menu.Items.Add(remove);
        menu.ShowAt(chip);
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
