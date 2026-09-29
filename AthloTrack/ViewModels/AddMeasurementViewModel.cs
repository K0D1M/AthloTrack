using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

public partial class AddMeasurementViewModel : ObservableValidator
{
    private readonly Guid _athleteId;
    private readonly IMeasurementRepository _measurements;
    private readonly SessionState _session;

    public AddMeasurementViewModel(Guid athleteId, IMeasurementRepository measurements, SessionState session)
    {
        _athleteId = athleteId;
        _measurements = measurements;
        _session = session;
    }

    public event Action? Saved;
    public event Action? Cancelled;

    private Measurement? _editing;

    /// <summary>Page title: add vs. edit.</summary>
    public string Title => _editing is null ? "Νέα μέτρηση" : "Επεξεργασία μέτρησης";

    /// <summary>Switches the form to editing an existing measurement, pre-filling its fields.</summary>
    public void BeginEdit(Measurement measurement)
    {
        _editing = measurement;
        var culture = CultureInfo.CurrentCulture;
        MeasuredAt = new DateTimeOffset(measurement.MeasuredAt.ToDateTime(TimeOnly.MinValue));
        WeightKg = measurement.WeightKg.ToString(culture);
        FatMassWt = measurement.FatMassWt?.ToString(culture) ?? string.Empty;
        FatHgt = measurement.FatHgt?.ToString(culture) ?? string.Empty;
        OnPropertyChanged(nameof(Title));
    }

    [ObservableProperty]
    public partial DateTimeOffset MeasuredAt { get; set; } = DateTimeOffset.Now;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Το βάρος είναι υποχρεωτικό.")]
    [RegularExpression(@"^\d{1,3}([.,]\d{1,2})?$", ErrorMessage = "Μη έγκυρο βάρος (π.χ. 72.5).")]
    public partial string WeightKg { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression(@"^$|^\d{1,3}([.,]\d{1,2})?$", ErrorMessage = "Μη έγκυρη τιμή.")]
    public partial string FatMassWt { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression(@"^$|^\d{1,3}([.,]\d{1,2})?$", ErrorMessage = "Μη έγκυρη τιμή.")]
    public partial string FatHgt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        ValidateAllProperties();
        if (HasErrors)
        {
            ErrorMessage = "Παρακαλώ διορθώστε τα πεδία.";
            return;
        }

        IsBusy = true;
        try
        {
            var measuredAt = DateOnly.FromDateTime(MeasuredAt.DateTime);
            var weight = ParseDecimal(WeightKg)!.Value;

            if (_editing is { } existing)
            {
                existing.MeasuredAt = measuredAt;
                existing.WeightKg = weight;
                existing.FatMassWt = ParseNullable(FatMassWt);
                existing.FatHgt = ParseNullable(FatHgt);
                await _measurements.UpdateAsync(existing);
            }
            else
            {
                await _measurements.AddAsync(new Measurement
                {
                    AthleteId = _athleteId,
                    MeasuredAt = measuredAt,
                    WeightKg = weight,
                    FatMassWt = ParseNullable(FatMassWt),
                    FatHgt = ParseNullable(FatHgt),
                    CreatedBy = _session.ProfileId,
                });
            }

            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static decimal? ParseDecimal(string s) =>
        decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static decimal? ParseNullable(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : ParseDecimal(s);
}
