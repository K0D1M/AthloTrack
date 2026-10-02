
using Avalonia;
using Avalonia.Controls;
using AthloTrack.Core.Models;
using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AthloTrack.Views;

public partial class MainView : DrawerPage
{
    public MainView()
    {
        InitializeComponent();
        DrawerHeaderRoot.DataContext = Resolve<CurrentUserViewModel>();
    }

    // Ρυθμίσεις: the signed-in user (coach or athlete) changes their own photo.
    private async void OnChangeOwnPhoto(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var user = Resolve<CurrentUserViewModel>();
        try
        {
            var picked = await AthloTrack.Services.PhotoPicker.PickAvatarAsync(this);
            if (picked is null) return;
            if (picked.Jpeg is null)
            {
                user.ErrorMessage = picked.Error;
                return;
            }
            await user.UploadPhotoAsync(picked.Jpeg, AthloTrack.Services.PhotoProcessor.ContentType);
        }
        catch (Exception ex)
        {
            // async void: an escaped exception would take the app down.
            user.ErrorMessage = $"Η φωτογραφία δεν αποθηκεύτηκε: {ex.Message}";
        }
    }

    // The top bar's background is set inside DrawerPage's template, which outranks styles
    // and ignores resource overrides; a local value on the template part wins.
    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Border>("PART_TopBar") is { } bar &&
            this.TryFindResource("BrandBarGradient", out var brush) && brush is Avalonia.Media.IBrush gradient)
        {
            bar.Background = gradient;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        UpdatePage(DrawerList.SelectedIndex);

        // A tapped phone notification opens its section (see MainActivity).
        AthloTrack.Services.NotificationNavigation.Requested += ApplyPendingSection;
        ApplyPendingSection();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        AthloTrack.Services.NotificationNavigation.Requested -= ApplyPendingSection;
        base.OnDetachedFromVisualTree(e);
    }

    private void ApplyPendingSection()
    {
        if (AthloTrack.Services.NotificationNavigation.TakePending() is not { } request) return;
        foreach (var notificationId in request.NotificationIds) _ = MarkNotificationReadAsync(notificationId);

        var index = request.Section;
        if (DrawerList.SelectedIndex != index)
        {
            DrawerList.SelectedIndex = index; // SelectionChanged shows the page
        }
        else
        {
            UpdatePage(index); // reload it, and leave any sub-page
        }
    }

    private void DrawerList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ContentPage != null && sender is ListBox listbox)
        {
            var index = listbox.SelectedIndex;
            UpdatePage(index);
        }
    }

    // Root page type and top-bar title of each drawer section, in drawer order.
    private static readonly Type[] SectionRootTypes =
    {
        typeof(RecentViewModel), typeof(AthletesViewModel), typeof(WorkoutsViewModel),
        typeof(CalendarViewModel), typeof(AboutViewModel), typeof(SettingsViewModel),
    };

    private static readonly string[] SectionTitles =
    {
        "Πρόσφατα", "Αθλητές", "Προπονήσεις", "Ημερολόγιο", "Σχετικά", "Ρυθμίσεις",
    };

    // Re-tapping the selected section doesn't raise SelectionChanged, so from a sub-page
    // (e.g. an athlete profile) tapping "Αθλητές" would otherwise do nothing.
    private void DrawerList_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var index = DrawerList.SelectedIndex;
        if (index >= 0 && index < SectionRootTypes.Length &&
            ContentPage.Content?.GetType() != SectionRootTypes[index])
        {
            UpdatePage(index);
        }
        else
        {
            IsOpen = false;
        }
    }

    /// <summary>The push was tapped, so its in-app copy on Πρόσφατα has been seen too.</summary>
    private async System.Threading.Tasks.Task MarkNotificationReadAsync(Guid notificationId)
    {
        try
        {
            await Resolve<AthloTrack.Core.Data.INotificationRepository>().MarkReadAsync(notificationId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AthloTrack] Marking the tapped notification read failed: {ex.Message}");
        }
    }

    private T Resolve<T>() where T : notnull => App.Services.GetRequiredService<T>();

    /// <summary>Shows a page and names it in the top bar.</summary>
    private void Show(object page, string title)
    {
        ContentPage.Content = page;
        Header = title;
    }

    private void UpdatePage(int index)
    {
        ViewModelBase page = index switch
        {
            0 => Resolve<RecentViewModel>(),
            1 => CreateAthletesPage(),
            2 => Resolve<WorkoutsViewModel>(),
            3 => CreateCalendarPage(),
            4 => Resolve<AboutViewModel>(),
            5 => CreateSettingsPage(),
            _ => throw new NotImplementedException()
        };

        Show(page, SectionTitles[index]);

        IsOpen = false;
    }

    private SettingsViewModel CreateSettingsPage()
    {
        var vm = Resolve<SettingsViewModel>();
        vm.LoggedOut += () => App.Instance?.NavigateToLogin();
        return vm;
    }

    private CalendarViewModel CreateCalendarPage()
    {
        var vm = Resolve<CalendarViewModel>();
        vm.OpenAthleteRequested += ShowAthleteProfile;
        return vm;
    }

    private AthletesViewModel CreateAthletesPage()
    {
        var vm = Resolve<AthletesViewModel>();
        vm.OpenAthleteRequested += ShowAthleteProfile;
        vm.AddAthleteRequested += OnAddAthlete;
        return vm;
    }

    private void ShowAthletesList()
    {
        // Re-create the athletes page so its list reloads after add/edit/delete.
        Show(CreateAthletesPage(), SectionTitles[1]);
    }

    private void ShowAthleteProfile(Guid athleteId)
    {
        var profile = Resolve<AthleteProfileViewModelFactory>().Create(athleteId);
        profile.AddMeasurementRequested += OnAddMeasurement;
        profile.AddWorkoutRequested += OnAddWorkout;
        profile.EditMeasurementRequested += OnEditMeasurement;
        profile.EditAthleteRequested += OnEditAthlete;
        profile.EditWorkoutRequested += OnEditWorkout;
        profile.AthleteDeleted += ShowAthletesList;
        Show(profile, "Προφίλ αθλητή");
    }

    private void OnAddMeasurement(Guid athleteId)
    {
        var vm = Resolve<AddMeasurementViewModelFactory>().Create(athleteId);
        vm.Saved += () => ShowAthleteProfile(athleteId);
        vm.Cancelled += () => ShowAthleteProfile(athleteId);
        Show(vm, vm.Title);
    }

    private void OnEditMeasurement(Measurement measurement)
    {
        var vm = Resolve<AddMeasurementViewModelFactory>().CreateForEdit(measurement);
        vm.Saved += () => ShowAthleteProfile(measurement.AthleteId);
        vm.Cancelled += () => ShowAthleteProfile(measurement.AthleteId);
        Show(vm, vm.Title);
    }

    private void OnAddWorkout(Guid athleteId, string athleteName)
    {
        var vm = Resolve<AddWorkoutViewModelFactory>().Create(athleteId, athleteName);
        vm.Saved += () => ShowAthleteProfile(athleteId);
        vm.Cancelled += () => ShowAthleteProfile(athleteId);
        Show(vm, vm.PageTitle);
    }

    private void OnEditWorkout(WorkoutProgram workout, string athleteName)
    {
        var vm = Resolve<AddWorkoutViewModelFactory>().CreateForEdit(workout, athleteName);
        vm.Saved += () => ShowAthleteProfile(workout.AthleteId);
        vm.Cancelled += () => ShowAthleteProfile(workout.AthleteId);
        Show(vm, vm.PageTitle);
    }

    private void OnAddAthlete()
    {
        var vm = Resolve<AddAthleteViewModel>();
        vm.Saved += ShowAthletesList;
        vm.Cancelled += ShowAthletesList;
        Show(vm, vm.Title);
    }

    private void OnEditAthlete(Athlete athlete)
    {
        var vm = Resolve<AddAthleteViewModel>();
        vm.BeginEdit(athlete);
        vm.Saved += () => ShowAthleteProfile(athlete.Id);
        vm.Cancelled += () => ShowAthleteProfile(athlete.Id);
        Show(vm, vm.Title);
    }
}
