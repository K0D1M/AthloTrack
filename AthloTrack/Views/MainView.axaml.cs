
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AthloTrack.Core.Models;
using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace AthloTrack.Views;

/// <summary>
/// The signed-in shell: top bar (Up arrow, title, own photo), the current page, and the bottom bar
/// with the five sections. Sub-pages remember how to go back; the Up arrow and Android's back
/// button/gesture use that.
/// </summary>
public partial class MainView : ContentPage
{
    // Bottom bar order; NotificationNavigation.WorkoutsSection relies on Προπονήσεις being 2.
    private const int RecentTab = 0, AthletesTab = 1, WorkoutsTab = 2, CalendarTab = 3, SettingsTab = 4;

    private static readonly Type[] SectionRootTypes =
    {
        typeof(RecentViewModel), typeof(AthletesViewModel), typeof(WorkoutsViewModel),
        typeof(CalendarViewModel), typeof(SettingsViewModel),
    };

    private static readonly string[] SectionTitles =
    {
        "Πρόσφατα", "Αθλητές", "Προπονήσεις", "Ημερολόγιο", "Ρυθμίσεις",
    };

    /// <summary>Where back goes from the page shown now; null on a section's root page.</summary>
    private Action? _back;

    private TopLevel? _topLevel;

    public MainView()
    {
        InitializeComponent();
        TopAvatar.DataContext = Resolve<CurrentUserViewModel>();
    }

    /// <summary>Whether back stays inside the app (a sub-page, or a section other than Πρόσφατα).</summary>
    public bool CanGoBack => _back is not null || TabBar.SelectedIndex != RecentTab;

    // Ρυθμίσεις: the signed-in user (coach or athlete) changes their own photo.
    private async void OnChangeOwnPhoto(object? sender, RoutedEventArgs e)
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        UpdatePage(TabBar.SelectedIndex);

        // Android's back button and back gesture (Avalonia raises this from the activity's
        // OnBackPressedDispatcher). Unhandled, Android does its default: the app goes to the background.
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null) _topLevel.BackRequested += OnBackRequested;

        // A tapped phone notification opens its section (see MainActivity).
        AthloTrack.Services.NotificationNavigation.Requested += ApplyPendingSection;
        ApplyPendingSection();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        AthloTrack.Services.NotificationNavigation.Requested -= ApplyPendingSection;
        if (_topLevel is not null) _topLevel.BackRequested -= OnBackRequested;
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (CurrentPageHandledBack())
        {
            e.Handled = true;
        }
        else if (_back is not null)
        {
            GoBack();
            e.Handled = true;
        }
        else if (TabBar.SelectedIndex != RecentTab)
        {
            TabBar.SelectedIndex = RecentTab; // like native apps with a bottom bar: back leads home first
            e.Handled = true;
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (!CurrentPageHandledBack()) GoBack();
    }

    /// <summary>Lets the page on screen use the back first (e.g. the profile closes its "+" menu).</summary>
    private bool CurrentPageHandledBack() =>
        ContentPage.GetVisualDescendants().OfType<IHandlesBack>().FirstOrDefault()?.TryHandleBack() == true;

    /// <summary>Leaves the current sub-page. Editors are left as they are (a workout draft stays saved).</summary>
    private void GoBack() => _back?.Invoke();

    private void OnAvatarClick(object? sender, RoutedEventArgs e)
    {
        if (TabBar.SelectedIndex != SettingsTab) TabBar.SelectedIndex = SettingsTab;
        else if (_back is not null) UpdatePage(SettingsTab);
    }

    private void OnOpenAbout(object? sender, RoutedEventArgs e) =>
        Show(Resolve<AboutViewModel>(), "Σχετικά", back: () => UpdatePage(SettingsTab));

    private void ApplyPendingSection()
    {
        if (AthloTrack.Services.NotificationNavigation.TakePending() is not { } request) return;
        foreach (var notificationId in request.NotificationIds) _ = MarkNotificationReadAsync(notificationId);

        var index = request.Section;
        if (TabBar.SelectedIndex != index)
        {
            TabBar.SelectedIndex = index; // SelectionChanged shows the page
        }
        else
        {
            UpdatePage(index); // reload it, and leave any sub-page
        }
    }

    private void TabBar_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ContentPage != null && sender is ListBox listbox)
        {
            UpdatePage(listbox.SelectedIndex);
        }
    }

    // Re-tapping the selected section doesn't raise SelectionChanged, so from a sub-page
    // (e.g. an athlete profile) tapping "Αθλητές" would otherwise do nothing.
    private void TabBar_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var index = TabBar.SelectedIndex;
        if (index >= 0 && index < SectionRootTypes.Length &&
            (_back is not null || ContentPage.Content?.GetType() != SectionRootTypes[index]))
        {
            UpdatePage(index);
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

    /// <summary>Shows a page, names it in the top bar, and remembers where back goes (null: a section root).</summary>
    private void Show(object page, string title, Action? back = null)
    {
        _back = back;
        BackButton.IsVisible = back is not null;
        ContentPage.Content = page;
        TitleText.Text = title;
        Motion.FadeInPage(ContentPage);
    }

    private void UpdatePage(int index)
    {
        ViewModelBase page = index switch
        {
            RecentTab => Resolve<RecentViewModel>(),
            AthletesTab => CreateAthletesPage(),
            WorkoutsTab => Resolve<WorkoutsViewModel>(),
            CalendarTab => CreateCalendarPage(),
            SettingsTab => CreateSettingsPage(),
            _ => throw new NotImplementedException()
        };

        Show(page, SectionTitles[index]);
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
        vm.OpenAthleteRequested += id => ShowAthleteProfile(id, back: () => UpdatePage(CalendarTab));
        return vm;
    }

    private AthletesViewModel CreateAthletesPage()
    {
        var vm = Resolve<AthletesViewModel>();
        vm.OpenAthleteRequested += id => ShowAthleteProfile(id, back: ShowAthletesList);
        vm.AddAthleteRequested += OnAddAthlete;
        return vm;
    }

    private void ShowAthletesList()
    {
        // Re-create the athletes page so its list reloads after add/edit/delete.
        Show(CreateAthletesPage(), SectionTitles[AthletesTab]);
    }

    /// <param name="back">Back to the page the profile was opened from (Αθλητές or Ημερολόγιο).</param>
    private void ShowAthleteProfile(Guid athleteId, Action back)
    {
        var profile = Resolve<AthleteProfileViewModelFactory>().Create(athleteId);
        void Return() => ShowAthleteProfile(athleteId, back);
        profile.AddMeasurementRequested += id => OnAddMeasurement(id, Return);
        profile.AddWorkoutRequested += (id, name) => OnAddWorkout(id, name, Return);
        profile.EditMeasurementRequested += m => OnEditMeasurement(m, Return);
        profile.EditAthleteRequested += a => OnEditAthlete(a, Return);
        profile.EditWorkoutRequested += (w, name) => OnEditWorkout(w, name, Return);
        profile.AthleteDeleted += ShowAthletesList;
        Show(profile, "Προφίλ αθλητή", back);
    }

    // Editors: Αποθήκευση, Άκυρο and back all return to the athlete's profile.

    private void OnAddMeasurement(Guid athleteId, Action toProfile)
    {
        var vm = Resolve<AddMeasurementViewModelFactory>().Create(athleteId);
        vm.Saved += toProfile;
        vm.Cancelled += toProfile;
        Show(vm, vm.Title, toProfile);
    }

    private void OnEditMeasurement(Measurement measurement, Action toProfile)
    {
        var vm = Resolve<AddMeasurementViewModelFactory>().CreateForEdit(measurement);
        vm.Saved += toProfile;
        vm.Cancelled += toProfile;
        Show(vm, vm.Title, toProfile);
    }

    private void OnAddWorkout(Guid athleteId, string athleteName, Action toProfile)
    {
        var vm = Resolve<AddWorkoutViewModelFactory>().Create(athleteId, athleteName);
        vm.Saved += toProfile;
        vm.Cancelled += toProfile;
        Show(vm, vm.PageTitle, toProfile);
    }

    private void OnEditWorkout(WorkoutProgram workout, string athleteName, Action toProfile)
    {
        var vm = Resolve<AddWorkoutViewModelFactory>().CreateForEdit(workout, athleteName);
        vm.Saved += toProfile;
        vm.Cancelled += toProfile;
        Show(vm, vm.PageTitle, toProfile);
    }

    private void OnAddAthlete()
    {
        var vm = Resolve<AddAthleteViewModel>();
        vm.Saved += ShowAthletesList;
        vm.Cancelled += ShowAthletesList;
        Show(vm, vm.Title, ShowAthletesList);
    }

    private void OnEditAthlete(Athlete athlete, Action toProfile)
    {
        var vm = Resolve<AddAthleteViewModel>();
        vm.BeginEdit(athlete);
        vm.Saved += toProfile;
        vm.Cancelled += toProfile;
        Show(vm, vm.Title, toProfile);
    }
}
