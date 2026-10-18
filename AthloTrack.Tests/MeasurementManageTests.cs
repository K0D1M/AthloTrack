using AthloTrack.Core.Models;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>Optional weight, measurement notes, and renaming / deleting saved fields.</summary>
public sealed class MeasurementManageTests
{
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _athleteId = Guid.NewGuid();
    private readonly FakeMeasurements _measurements = new();
    private readonly FakeMeasurementFields _fields = new();

    private MeasurementField Field(string name, string? unit = null)
    {
        var f = new MeasurementField { Id = Guid.NewGuid(), CoachId = _coachId, Name = name, Unit = unit };
        _fields.Items.Add(f);
        return f;
    }

    private async Task<AddMeasurementViewModel> Form()
    {
        var vm = new AddMeasurementViewModel(_athleteId, _measurements, Sessions.Coach(_coachId), _fields);
        await vm.FieldsLoaded;
        return vm;
    }

    // ---- weight optional ----

    [Fact]
    public async Task An_empty_measurement_is_refused()
    {
        var vm = await Form();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_measurements.Items);
        Assert.Equal(AddMeasurementViewModel.NothingText, vm.ErrorMessage);
    }

    [Fact]
    public async Task Fat_alone_is_a_measurement_without_weight()
    {
        var vm = await Form();
        vm.AddFatSectionCommand.Execute(null);
        vm.FatHgt = "3,1";

        await vm.SaveCommand.ExecuteAsync(null);

        var m = Assert.Single(_measurements.Items);
        Assert.Null(m.WeightKg);
        Assert.False(m.HasWeight);
        Assert.Equal(3.1m, m.FatHgt);
    }

    // ---- notes ----

    [Fact]
    public async Task Notes_alone_are_saved_trimmed_and_reopen_the_section()
    {
        var vm = await Form();
        vm.AddCustomSectionCommand.Execute(null);
        vm.Notes = "  ## Παρατηρήσεις\n- κούραση  ";

        await vm.SaveCommand.ExecuteAsync(null);

        var m = Assert.Single(_measurements.Items);
        Assert.Equal("## Παρατηρήσεις\n- κούραση", m.Notes);
        Assert.True(m.HasNotes);

        var edit = await Form();
        edit.BeginEdit(m);
        Assert.True(edit.ShowCustom);
        Assert.Equal(m.Notes, edit.Notes);
    }

    [Fact]
    public async Task Closing_the_section_drops_the_notes()
    {
        var vm = await Form();
        vm.WeightKg = "70";
        vm.AddCustomSectionCommand.Execute(null);
        vm.Notes = "κάτι";
        vm.RemoveCustomSectionCommand.Execute(null);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(_measurements.Items).Notes);
    }

    // ---- rename ----

    [Fact]
    public async Task Renaming_updates_the_chips_and_a_row_in_the_form_keeps_its_value()
    {
        var waist = Field("Μέση", "cm");
        Field("Γοφοί", "cm");
        var vm = await Form();
        vm.AddCustomSectionCommand.Execute(null);
        vm.AddFieldCommand.Execute(waist);
        vm.CustomRows[0].Value = "82";
        vm.ToggleManageCommand.Execute(null);

        var row = vm.ManagedFields.Single(r => r.Field.Id == waist.Id);
        row.EditCommand.Execute(null);
        row.EditName = "Περιφέρεια μέσης";
        await row.SaveEditCommand.ExecuteAsync(null);

        Assert.Equal("Περιφέρεια μέσης (cm)", vm.CustomRows[0].Field.Label);
        Assert.Equal("82", vm.CustomRows[0].Value);
        Assert.Contains(vm.ManagedFields, r => r.Field.Name == "Περιφέρεια μέσης");
        Assert.Contains(_fields.Items, f => f.Name == "Περιφέρεια μέσης");
    }

    [Fact]
    public async Task A_rename_to_another_fields_name_is_refused()
    {
        var waist = Field("Μέση");
        Field("Γοφοί");
        var vm = await Form();
        vm.ToggleManageCommand.Execute(null);

        var row = vm.ManagedFields.Single(r => r.Field.Id == waist.Id);
        row.EditCommand.Execute(null);
        row.EditName = "γοφοί";
        await row.SaveEditCommand.ExecuteAsync(null);

        Assert.Equal("Υπάρχει ήδη πεδίο με αυτό το όνομα.", row.Error);
        Assert.Contains(_fields.Items, f => f.Name == "Μέση");
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_asks_with_the_count_and_then_removes_the_field_everywhere()
    {
        var waist = Field("Μέση", "cm");
        var hips = Field("Γοφοί", "cm");
        _fields.Usage[waist.Id] = 3;
        var vm = await Form();
        vm.AddCustomSectionCommand.Execute(null);
        vm.AddFieldCommand.Execute(waist);
        vm.ToggleManageCommand.Execute(null);

        var row = vm.ManagedFields.Single(r => r.Field.Id == waist.Id);
        await row.AskDeleteCommand.ExecuteAsync(null);
        Assert.True(row.IsConfirmingDelete);
        Assert.Equal("Διαγραφή του «Μέση»; Θα σβηστούν και 3 τιμές από μετρήσεις.", row.DeleteText);

        await row.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Equal(new[] { waist.Id }, _fields.Deleted);
        Assert.Empty(vm.CustomRows);
        Assert.Equal(new[] { hips.Id }, vm.AvailableFields.Select(f => f.Id));
        Assert.Equal(new[] { hips.Id }, vm.ManagedFields.Select(r => r.Field.Id));
    }

    [Fact]
    public async Task Cancelling_a_delete_changes_nothing()
    {
        var waist = Field("Μέση");
        var vm = await Form();
        vm.ToggleManageCommand.Execute(null);
        var row = vm.ManagedFields.Single();

        await row.AskDeleteCommand.ExecuteAsync(null);
        Assert.Equal("Διαγραφή του «Μέση»; Δεν έχει τιμές ακόμα.", row.DeleteText);
        row.CancelDeleteCommand.Execute(null);

        Assert.False(row.IsConfirmingDelete);
        Assert.Empty(_fields.Deleted);
        Assert.Single(_fields.Items);
    }

    // ---- profile ----

    [Fact]
    public async Task The_chart_has_no_weight_line_when_nothing_was_weighed()
    {
        var athletes = new FakeAthletes();
        athletes.Items.Add(new Athlete { Id = _athleteId, CoachId = _coachId, FullName = "Γ" });
        _measurements.Items.Add(new Measurement { Id = Guid.NewGuid(), AthleteId = _athleteId, MeasuredAt = new DateOnly(2026, 9, 1), FatHgt = 3 });
        _measurements.Items.Add(new Measurement { Id = Guid.NewGuid(), AthleteId = _athleteId, MeasuredAt = new DateOnly(2026, 10, 1), FatHgt = 2.8m });

        var vm = new AthleteProfileViewModel(_athleteId, athletes, _measurements, new FakeWorkouts(), Sessions.Coach(_coachId),
            new FakeAvatars(), fields: _fields);
        await vm.LoadAsync();

        Assert.DoesNotContain(vm.Chart!.Lines, l => l.Name == "Βάρος (kg)");
        Assert.Contains(vm.Chart.Lines, l => l.Name == "fat/hgt");
    }
}
