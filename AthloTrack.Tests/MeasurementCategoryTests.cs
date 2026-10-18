using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using AthloTrack.ViewModels;

namespace AthloTrack.Tests;

/// <summary>«Νέα μέτρηση»: the «Λιπομέτρηση» and «Άλλες μετρήσεις» categories.</summary>
public sealed class MeasurementCategoryTests
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

    [Fact]
    public async Task A_new_measurement_starts_with_only_date_and_weight()
    {
        Field("Περιφέρεια μέσης", "cm");
        var vm = await Form();

        Assert.False(vm.ShowFat);
        Assert.False(vm.ShowCustom);
        Assert.Empty(vm.CustomRows);
        Assert.Single(vm.AvailableFields);
    }

    [Fact]
    public async Task Weight_only_saves_no_fat_and_no_extra()
    {
        var vm = await Form();
        vm.WeightKg = "72.5";

        await vm.SaveCommand.ExecuteAsync(null);

        var m = Assert.Single(_measurements.Items);
        Assert.Equal(72.5m, m.WeightKg);
        Assert.Null(m.FatMassWt);
        Assert.Empty(m.Extra);
    }

    [Fact]
    public async Task Closing_fat_clears_its_values()
    {
        var vm = await Form();
        vm.WeightKg = "70";
        vm.AddFatSectionCommand.Execute(null);
        vm.FatMassWt = "18,5";
        vm.RemoveFatSectionCommand.Execute(null);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.ShowFat);
        Assert.Null(Assert.Single(_measurements.Items).FatMassWt);
    }

    [Fact]
    public async Task Fat_and_custom_values_are_saved_empty_rows_are_not()
    {
        var waist = Field("Περιφέρεια μέσης", "cm");
        var hips = Field("Γοφοί", "cm");
        var vm = await Form();
        vm.WeightKg = "70";
        vm.AddFatSectionCommand.Execute(null);
        vm.FatMassWt = "18,5";
        vm.AddCustomSectionCommand.Execute(null);
        vm.AddFieldCommand.Execute(waist);
        vm.AddFieldCommand.Execute(hips);
        Assert.Empty(vm.AvailableFields);
        vm.CustomRows[0].Value = "82";

        await vm.SaveCommand.ExecuteAsync(null);

        var m = Assert.Single(_measurements.Items);
        Assert.Equal(18.5m, m.FatMassWt);
        Assert.Equal(new Dictionary<Guid, decimal> { [waist.Id] = 82m }, m.Extra);
    }

    [Fact]
    public async Task An_invalid_custom_value_is_refused()
    {
        var waist = Field("Περιφέρεια μέσης");
        var vm = await Form();
        vm.WeightKg = "70";
        vm.AddCustomSectionCommand.Execute(null);
        vm.AddFieldCommand.Execute(waist);
        vm.CustomRows[0].Value = "ογδόντα";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_measurements.Items);
        Assert.NotNull(vm.CustomRows[0].Error);
    }

    [Fact]
    public async Task A_new_field_is_saved_and_added_and_a_duplicate_is_refused()
    {
        Field("Γοφοί");
        var vm = await Form();
        vm.AddCustomSectionCommand.Execute(null);
        Assert.True(vm.CanCreateFields);

        vm.StartNewFieldCommand.Execute(null);
        vm.NewFieldName = "  Περιφέρεια μέσης ";
        vm.NewFieldUnit = "cm";
        await vm.CreateFieldCommand.ExecuteAsync(null);

        Assert.False(vm.IsAddingField);
        var row = Assert.Single(vm.CustomRows);
        Assert.Equal("Περιφέρεια μέσης (cm)", row.Field.Label);
        Assert.Equal(2, _fields.Items.Count);

        vm.StartNewFieldCommand.Execute(null);
        vm.NewFieldName = "γοφοί";
        await vm.CreateFieldCommand.ExecuteAsync(null);
        Assert.Equal("Υπάρχει ήδη πεδίο με αυτό το όνομα.", vm.NewFieldError);
        Assert.Equal(2, _fields.Items.Count);
    }

    [Fact]
    public async Task Editing_reopens_the_sections_and_keeps_unknown_values()
    {
        var waist = Field("Περιφέρεια μέσης", "cm");
        var gone = Guid.NewGuid(); // a field that's no longer in the list
        var existing = new Measurement
        {
            Id = Guid.NewGuid(), AthleteId = _athleteId, MeasuredAt = new DateOnly(2026, 10, 1), WeightKg = 70,
            FatHgt = 3.2m, Extra = new() { [waist.Id] = 80m, [gone] = 5m },
        };
        var vm = await Form();
        vm.BeginEdit(existing);

        Assert.True(vm.ShowFat);
        Assert.True(vm.ShowCustom);
        var row = Assert.Single(vm.CustomRows);
        Assert.Equal(waist.Id, row.Field.Id);
        row.Value = "79,5";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new Dictionary<Guid, decimal> { [waist.Id] = 79.5m, [gone] = 5m }, existing.Extra);
    }

    [Fact]
    public void Extra_goes_to_json_and_back_and_skips_non_numbers()
    {
        var id = Guid.NewGuid();
        var json = MeasurementRepository.ExtraToJson(new() { [id] = 82.5m })!;
        json["not-a-guid"] = 1;
        json[Guid.NewGuid().ToString()] = "text";

        var back = MeasurementRepository.ExtraFromJson(json);

        Assert.Equal(new Dictionary<Guid, decimal> { [id] = 82.5m }, back);
        Assert.Null(MeasurementRepository.ExtraToJson(new()));
    }

    [Fact]
    public async Task The_profile_shows_only_what_was_measured_and_charts_custom_fields()
    {
        var waist = Field("Περιφέρεια μέσης", "cm");
        var athletes = new FakeAthletes();
        athletes.Items.Add(new Athlete { Id = _athleteId, CoachId = _coachId, FullName = "Γ" });
        _measurements.Items.Add(new Measurement { Id = Guid.NewGuid(), AthleteId = _athleteId, MeasuredAt = new DateOnly(2026, 9, 1), WeightKg = 71, Extra = new() { [waist.Id] = 84m } });
        _measurements.Items.Add(new Measurement { Id = Guid.NewGuid(), AthleteId = _athleteId, MeasuredAt = new DateOnly(2026, 10, 1), WeightKg = 70 });

        var vm = new AthleteProfileViewModel(_athleteId, athletes, _measurements, new FakeWorkouts(), Sessions.Coach(_coachId),
            new FakeAvatars(), fields: _fields);
        await vm.LoadAsync();

        var september = vm.Measurements.Single(m => m.MeasuredAt.Month == 9);
        Assert.Equal(new[] { "Περιφέρεια μέσης: 84 cm" }, september.ExtraLines);
        Assert.Empty(vm.Measurements.Single(m => m.MeasuredAt.Month == 10).ExtraLines);
        Assert.Contains(vm.Chart!.Lines, l => l.Name == "Περιφέρεια μέσης (cm)");
        Assert.DoesNotContain(vm.Chart.Lines, l => l.Name == "Fat Mass/WT");
    }
}
