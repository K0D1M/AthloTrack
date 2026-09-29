using System;
using AthloTrack.Core.Auth;
using AthloTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace AthloTrack.WinUI.Views;

public sealed partial class ShellPage : Page
{
    public event Action? LogoutRequested;

    public ShellPage()
    {
        InitializeComponent();

        var session = App.Services.GetRequiredService<SessionState>();
        UserLabel.Text = session.DisplayName ?? string.Empty;

        Navigate("recent");
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
            Navigate(tag);
    }

    private void Navigate(string tag)
    {
        object page = tag switch
        {
            "recent" => new RecentPage(),
            "athletes" => BuildAthletesPage(),
            "workouts" => new WorkoutsPage(),
            "calendar" => new CalendarPage(),
            "about" => new AboutPage(),
            "settings" => BuildSettingsPage(),
            _ => new RecentPage(),
        };

        ContentFrame.Content = page;
        // modern page transition
        ContentFrame.ContentTransitions = new TransitionCollection
        {
            new EntranceThemeTransition { FromVerticalOffset = 24 },
        };
    }

    private AthletesPage BuildAthletesPage()
    {
        var page = new AthletesPage();
        page.OpenAthleteRequested += ShowAthleteProfile;
        page.AddAthleteRequested += ShowAddAthlete;
        return page;
    }

    private SettingsPage BuildSettingsPage()
    {
        var page = new SettingsPage();
        page.LoggedOut += () => LogoutRequested?.Invoke();
        return page;
    }

    private void ShowAthletesList()
    {
        ContentFrame.Content = BuildAthletesPage();
    }

    private void ShowAthleteProfile(Guid athleteId)
    {
        var page = new AthleteProfilePage(athleteId);
        page.BackRequested += ShowAthletesList;
        page.AddMeasurementRequested += id => ShowAddMeasurement(id, athleteId);
        page.AddWorkoutRequested += (id, name) => ShowAddWorkout(id, name, athleteId);
        ContentFrame.Content = page;
    }

    private void ShowAddAthlete()
    {
        var page = new AddAthletePage();
        page.Completed += ShowAthletesList;
        ContentFrame.Content = page;
    }

    private void ShowAddMeasurement(Guid athleteId, Guid backTo)
    {
        var page = new AddMeasurementPage(athleteId);
        page.Completed += () => ShowAthleteProfile(backTo);
        ContentFrame.Content = page;
    }

    private void ShowAddWorkout(Guid athleteId, string athleteName, Guid backTo)
    {
        var page = new AddWorkoutPage(athleteId, athleteName);
        page.Completed += () => ShowAthleteProfile(backTo);
        ContentFrame.Content = page;
    }
}
