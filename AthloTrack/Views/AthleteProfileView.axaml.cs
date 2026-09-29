using System;
using AthloTrack.Services;
using AthloTrack.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AthloTrack.Views;

public partial class AthleteProfileView : UserControl
{
    public AthleteProfileView()
    {
        InitializeComponent();
    }

    // File picking and image decoding are UI concerns, so they live here; the view model only
    // receives the finished bytes.
    private async void OnChangePhoto(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AthleteProfileViewModel vm) return;
        try
        {
            var picked = await PhotoPicker.PickAvatarAsync(this);
            if (picked is null) return;
            if (picked.Jpeg is null)
            {
                vm.ErrorMessage = picked.Error;
                return;
            }
            await vm.UploadPhotoAsync(picked.Jpeg, PhotoProcessor.ContentType);
        }
        catch (Exception ex)
        {
            // async void: an escaped exception would take the app down.
            vm.ErrorMessage = $"Η φωτογραφία δεν αποθηκεύτηκε: {ex.Message}";
        }
    }
}
