using AthloTrack.Core.Auth;
using AthloTrack.Core.Models;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>The workout editor's builder, templates/earlier workouts and draft autosave.</summary>
public class WorkoutEditorTests
{
    private static readonly Guid AthleteId = Guid.NewGuid();
    private static readonly Guid CoachId = Guid.NewGuid();

    private static AddWorkoutViewModel Create(IAppPreferences? prefs = null, FakeWorkouts? workouts = null,
        FakeTemplates? templates = null, FakeAthletes? athletes = null) =>
        new(AthleteId, "Γιάννης", workouts ?? new FakeWorkouts(), Sessions.Coach(CoachId),
            prefs ?? new InMemoryAppPreferences(), templates ?? new FakeTemplates(), athletes ?? new FakeAthletes());

    // ---- builder ----

    [Fact]
    public void The_builder_needs_a_name_and_valid_numbers()
    {
        var vm = Create();
        vm.AddExerciseCommand.Execute(null);
        Assert.NotNull(vm.BuilderError);

        vm.ExerciseName = "Καθίσματα";
        vm.ExerciseSets = "τρία";
        vm.AddExerciseCommand.Execute(null);
        Assert.Equal("Μη έγκυρος αριθμός στο «Σετ».", vm.BuilderError);
        Assert.Equal(string.Empty, vm.Content);
    }

    [Fact]
    public void The_builder_hands_the_line_to_the_view_and_clears_the_name()
    {
        var vm = Create();
        string? inserted = null;
        vm.InsertRequested += line => inserted = line;

        vm.ExerciseName = "Πιέσεις";
        vm.ExerciseSets = "4";
        vm.ExerciseReps = "8";
        vm.ExerciseKg = "62,5";
        vm.ExerciseRest = "120";
        vm.AddExerciseCommand.Execute(null);

        Assert.Equal("- **Πιέσεις** — 4×8 @ 62,5kg, διάλ. 120\"", inserted);
        Assert.Equal(string.Empty, vm.ExerciseName);
        Assert.Equal("4", vm.ExerciseSets); // kept for the next exercise
    }

    [Fact]
    public void Without_a_view_the_builder_appends_the_line()
    {
        var vm = Create();
        vm.Content = "## Κυρίως";
        vm.ExerciseName = "Τρέξιμο";
        vm.ExerciseMinutes = "20";
        vm.AddExerciseCommand.Execute(null);

        Assert.Equal("## Κυρίως\n- **Τρέξιμο** — 20'", vm.Content);
    }

    // ---- templates and earlier workouts ----

    [Fact]
    public async Task A_saved_template_can_start_a_new_workout_and_be_deleted()
    {
        var templates = new FakeTemplates();
        var vm = Create(templates: templates);
        await vm.ToggleReuseCommand.ExecuteAsync(null);
        Assert.True(vm.HasNoTemplates);

        vm.Content = "## Δύναμη\n- **Καθίσματα** — 3×10";
        vm.TemplateName = "Δύναμη Α";
        await vm.SaveTemplateCommand.ExecuteAsync(null);
        Assert.Equal("Δύναμη Α", Assert.Single(templates.Items).Name);
        Assert.Equal("Δύναμη", Assert.Single(vm.Templates).Subtitle);

        var next = Create(templates: templates);
        await next.LoadReuseAsync();
        next.UseItemCommand.Execute(next.Templates[0]);
        Assert.Equal("## Δύναμη\n- **Καθίσματα** — 3×10", next.Content);
        Assert.False(next.IsReuseOpen);

        next.DeleteTemplateCommand.Execute(next.Templates[0]);
        Assert.True(next.IsConfirming);
        await next.ConfirmCommand.ExecuteAsync(null);
        Assert.Empty(templates.Items);
        Assert.True(next.HasNoTemplates);
    }

    [Fact]
    public async Task Earlier_workouts_are_listed_newest_first_without_repeats()
    {
        var workouts = new FakeWorkouts();
        var athletes = new FakeAthletes();
        athletes.Items.Add(new Athlete { Id = AthleteId, FullName = "Γιάννης" });
        workouts.Items.Add(new WorkoutProgram { AthleteId = AthleteId, Content = "παλιό", TargetDate = new DateOnly(2026, 9, 1), CreatedAt = DateTimeOffset.Now.AddDays(-30) });
        workouts.Items.Add(new WorkoutProgram { AthleteId = AthleteId, Content = "- νέο", TargetDate = new DateOnly(2026, 10, 1), CreatedAt = DateTimeOffset.Now.AddDays(-2) });
        workouts.Items.Add(new WorkoutProgram { AthleteId = AthleteId, Content = "παλιό ", TargetDate = new DateOnly(2026, 9, 8), CreatedAt = DateTimeOffset.Now.AddDays(-20) });

        var vm = Create(workouts: workouts, athletes: athletes);
        await vm.LoadReuseAsync();

        Assert.Equal(["• νέο", "παλιό"], vm.Previous.Select(p => p.Title));
        Assert.Equal("Γιάννης · 01/10/2026", vm.Previous[0].Subtitle);
    }

    [Fact]
    public async Task Using_an_item_over_existing_text_asks_first()
    {
        var vm = Create();
        vm.Content = "ό,τι έγραψα";
        vm.UseItemCommand.Execute(new ReuseItem("x", "y", "νέο κείμενο"));

        Assert.True(vm.IsConfirming);
        Assert.Equal("ό,τι έγραψα", vm.Content);
        vm.CancelConfirmCommand.Execute(null);
        Assert.Equal("ό,τι έγραψα", vm.Content);

        vm.UseItemCommand.Execute(new ReuseItem("x", "y", "νέο κείμενο"));
        await vm.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal("νέο κείμενο", vm.Content);
    }

    // ---- drafts ----

    [Fact]
    public void A_new_workout_draft_comes_back_until_saved_or_cancelled()
    {
        var prefs = new InMemoryAppPreferences();
        var first = Create(prefs);
        first.Content = "μισογραμμένο";
        first.IsAbsentChosen = true;

        var reopened = Create(prefs);
        Assert.True(reopened.IsDraftRestored);
        Assert.Equal("μισογραμμένο", reopened.Content);
        Assert.False(reopened.CoachPresent);

        reopened.CancelCommand.Execute(null);
        var afterCancel = Create(prefs);
        Assert.False(afterCancel.IsDraftRestored);
        Assert.Equal(string.Empty, afterCancel.Content);
    }

    [Fact]
    public async Task Saving_clears_the_draft()
    {
        var prefs = new InMemoryAppPreferences();
        var vm = Create(prefs);
        vm.Content = "3x10 καθίσματα";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(Create(prefs).IsDraftRestored);
    }

    [Fact]
    public void Discarding_goes_back_to_the_saved_workout()
    {
        var prefs = new InMemoryAppPreferences();
        var workout = new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = AthleteId, Content = "αρχικό", TargetDate = new DateOnly(2026, 10, 6), CoachPresent = true };
        var vm = Create(prefs);
        vm.BeginEdit(workout, "Γιάννης");
        vm.Content = "αλλαγμένο";

        var reopened = Create(prefs);
        reopened.BeginEdit(workout, "Γιάννης");
        Assert.True(reopened.IsDraftRestored);
        Assert.Equal("αλλαγμένο", reopened.Content);

        reopened.DiscardDraftCommand.Execute(null);
        Assert.Equal("αρχικό", reopened.Content);
        Assert.True(reopened.CoachPresent);
        Assert.False(reopened.IsDraftRestored);
        Assert.Null(prefs.Get($"workout.draft.edit.{workout.Id}"));
    }

    [Fact]
    public void An_edit_draft_is_dropped_when_the_workout_changed_since()
    {
        var prefs = new InMemoryAppPreferences();
        var workout = new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = AthleteId, Content = "v1", TargetDate = new DateOnly(2026, 10, 6) };
        var vm = Create(prefs);
        vm.BeginEdit(workout, "Γιάννης");
        vm.Content = "πρόχειρο πάνω στο v1";

        workout.Content = "v2 από άλλη συσκευή";
        var reopened = Create(prefs);
        reopened.BeginEdit(workout, "Γιάννης");

        Assert.False(reopened.IsDraftRestored);
        Assert.Equal("v2 από άλλη συσκευή", reopened.Content);
    }

    [Fact]
    public void Undoing_every_change_leaves_no_draft()
    {
        var prefs = new InMemoryAppPreferences();
        var vm = Create(prefs);
        vm.Content = "κάτι";
        vm.Content = string.Empty;

        Assert.False(Create(prefs).IsDraftRestored);
    }

    [Fact]
    public void A_new_workout_draft_does_not_leak_into_an_edit()
    {
        var prefs = new InMemoryAppPreferences();
        Create(prefs).Content = "νέο πρόχειρο";

        var edit = Create(prefs);
        edit.BeginEdit(new WorkoutProgram { Id = Guid.NewGuid(), AthleteId = AthleteId, Content = "υπάρχον", TargetDate = new DateOnly(2026, 10, 6) }, "Γιάννης");

        Assert.False(edit.IsDraftRestored);
        Assert.Equal("υπάρχον", edit.Content);
        Assert.True(Create(prefs).IsDraftRestored); // the new-workout draft is still there
    }
}
