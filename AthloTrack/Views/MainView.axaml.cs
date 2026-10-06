
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
/// with the sections of the user's role (coaches and athletes: Πρόσφατα … Ρυθμίσεις; admins: the
/// «Διαχείριση» dashboard). Sub-pages remember how to go back; the Up arrow and Android's back
/// button/gesture use that.
/// </summary>
public partial class MainView : ContentPage
{
    // Back from another tab leads to the first one; Ρυθμίσεις is the last.
    private const int HomeTab = 0;

    /// <summary>A bottom-bar section: label, icons (filled when selected), and its root page.</summary>
    private sealed record Tab(string Title, string OutlineIcon, string FilledIcon, Type RootType,
        Func<ViewModelBase> Create, bool StrokedOutline = false);

    private readonly Tab[] _tabs;
    private readonly bool _isAdmin;

    private int SettingsTab => _tabs.Length - 1;

    /// <summary>Where back goes from the page shown now; null on a section's root page.</summary>
    private Action? _back;

    private TopLevel? _topLevel;

    public MainView()
    {
        InitializeComponent();
        TopAvatar.DataContext = Resolve<CurrentUserViewModel>();

        _isAdmin = Resolve<AthloTrack.Core.Auth.SessionState>().IsAdmin;
        _tabs = _isAdmin ? AdminTabs() : MemberTabs();
        foreach (var tab in _tabs) TabBar.Items.Add(BuildTabItem(tab));
        TabBar.SelectedIndex = HomeTab;
    }

    // Coaches and athletes. NotificationNavigation.WorkoutsSection relies on Προπονήσεις being 2.
    private Tab[] MemberTabs() =>
    [
        new("Πρόσφατα", "TabRecentOutline", "TabRecentFilled", typeof(RecentViewModel), Resolve<RecentViewModel>),
        new("Αθλητές", "TabAthletesOutline", "TabAthletesFilled", typeof(AthletesViewModel), CreateAthletesPage),
        // No outline dumbbell in MDI: the same shape, stroked instead of filled.
        new("Προπονήσεις", "DumbbellIcon", "DumbbellIcon", typeof(WorkoutsViewModel), Resolve<WorkoutsViewModel>,
            StrokedOutline: true),
        new("Ημερολόγιο", "TabCalendarOutline", "TabCalendarFilled", typeof(CalendarViewModel), CreateCalendarPage),
        new("Ρυθμίσεις", "TabSettingsOutline", "TabSettingsFilled", typeof(SettingsViewModel), CreateSettingsPage),
    ];

    // Administrators: the «Διαχείριση» dashboard.
    private Tab[] AdminTabs() =>
    [
        new("Επισκόπηση", "TabOverviewOutline", "TabOverviewFilled", typeof(AdminOverviewViewModel), CreateAdminOverview),
        new("Προπονητές", "TabCoachesOutline", "TabCoachesFilled", typeof(AdminCoachesViewModel), Resolve<AdminCoachesViewModel>),
        new("Αθλητές", "TabPeopleOutline", "TabPeopleFilled", typeof(AdminAthletesViewModel), Resolve<AdminAthletesViewModel>),
        new("Ειδοποιήσεις", "TabPushOutline", "TabPushFilled", typeof(AdminPushViewModel), Resolve<AdminPushViewModel>),
        new("Ρυθμίσεις", "TabSettingsOutline", "TabSettingsFilled", typeof(SettingsViewModel), CreateSettingsPage),
    ];

    /// <summary>A bottom-bar button: the outline and filled icons (styles show one), and the label.</summary>
    private static ListBoxItem BuildTabItem(Tab tab)
    {
        static Avalonia.Media.Geometry Icon(string key) =>
            (Avalonia.Media.Geometry)Application.Current!.FindResource(key)!;

        Control outline = tab.StrokedOutline
            ? new Avalonia.Controls.Shapes.Path
            {
                Data = Icon(tab.OutlineIcon),
                Stretch = Avalonia.Media.Stretch.Uniform,
                StrokeThickness = 1.5,
                StrokeJoin = Avalonia.Media.PenLineJoin.Round,
                Margin = new Thickness(1),
                [!Avalonia.Controls.Shapes.Shape.StrokeProperty] = new Avalonia.Data.Binding("Foreground")
                {
                    RelativeSource = new Avalonia.Data.RelativeSource(Avalonia.Data.RelativeSourceMode.FindAncestor)
                    {
                        AncestorType = typeof(ListBoxItem),
                    },
                },
            }
            : new PathIcon { Data = Icon(tab.OutlineIcon) };
        outline.Classes.Add("outline");
        var filled = new PathIcon { Data = Icon(tab.FilledIcon) };
        filled.Classes.Add("filled");

        var pill = new Border
        {
            Child = new Panel
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Children = { outline, filled },
            },
        };
        pill.Classes.Add("pill");

        return new ListBoxItem
        {
            Content = new StackPanel { Children = { pill, new TextBlock { Text = tab.Title } } },
        };
    }

    private int TabOf<T>() => Array.FindIndex(_tabs, t => t.RootType == typeof(T));

    /// <summary>Whether back stays inside the app (a sub-page, or a section other than the first).</summary>
    public bool CanGoBack => _back is not null || TabBar.SelectedIndex != HomeTab;

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
        else if (TabBar.SelectedIndex != HomeTab)
        {
            TabBar.SelectedIndex = HomeTab; // like native apps with a bottom bar: back leads home first
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
        if (_isAdmin) return; // an admin's pushes (tests, announcements) have no section to open
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
        if (index >= 0 && index < _tabs.Length &&
            (_back is not null || ContentPage.Content?.GetType() != _tabs[index].RootType))
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
        if (index < 0 || index >= _tabs.Length) return;
        Show(_tabs[index].Create(), _tabs[index].Title);
    }

    private AdminOverviewViewModel CreateAdminOverview()
    {
        var vm = Resolve<AdminOverviewViewModel>();
        vm.OpenCheckRequested += check =>
            Show(new AdminHealthListViewModel(check), check.Title, back: () => UpdatePage(HomeTab));
        vm.OpenAuditRequested += () =>
            Show(Resolve<AdminAuditViewModel>(), "Ιστορικό ενεργειών", back: () => UpdatePage(HomeTab));
        return vm;
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
        vm.OpenAthleteRequested += id => ShowAthleteProfile(id, back: () => UpdatePage(TabOf<CalendarViewModel>()));
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
        Show(CreateAthletesPage(), "Αθλητές");
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
