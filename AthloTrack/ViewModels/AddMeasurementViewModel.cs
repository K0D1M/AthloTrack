using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>One of the coach's own fields in «Άλλες μετρήσεις», with the value typed for it.</summary>
public sealed partial class CustomValueRow : ObservableObject
{
    private readonly Action<CustomValueRow> _remove;

    public CustomValueRow(MeasurementField field, string value, Action<CustomValueRow> remove)
    {
        Field = field;
        Value = value;
        _remove = remove;
    }

    public MeasurementField Field { get; }

    [ObservableProperty]
    public partial string Value { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    partial void OnValueChanged(string value) => Error = null;

    [RelayCommand]
    private void Remove() => _remove(this);
}

/// <summary>«Διαχείριση πεδίων»: one saved field, with rename (✎) and delete (🗑, confirmed inline).</summary>
public sealed partial class FieldManageRow : ObservableObject
{
    private readonly Func<FieldManageRow, Task> _save;
    private readonly Func<FieldManageRow, Task> _askDelete;
    private readonly Func<FieldManageRow, Task> _delete;

    public FieldManageRow(MeasurementField field, Func<FieldManageRow, Task> save,
        Func<FieldManageRow, Task> askDelete, Func<FieldManageRow, Task> delete)
    {
        Field = field;
        _save = save;
        _askDelete = askDelete;
        _delete = delete;
    }

    public MeasurementField Field { get; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string EditName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditUnit { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsConfirmingDelete { get; set; }

    [ObservableProperty]
    public partial string? DeleteText { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Neither renaming nor confirming: the label with ✎ and 🗑.</summary>
    public bool IsIdle => !IsEditing && !IsConfirmingDelete;

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(IsIdle));

    [RelayCommand]
    private void Edit()
    {
        EditName = Field.Name;
        EditUnit = Field.Unit ?? string.Empty;
        Error = null;
        IsConfirmingDelete = false;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        Error = null;
    }

    [RelayCommand]
    private Task SaveEditAsync() => _save(this);

    [RelayCommand]
    private Task AskDeleteAsync() => _askDelete(this);

    [RelayCommand]
    private void CancelDelete()
    {
        IsConfirmingDelete = false;
        Error = null;
    }

    [RelayCommand]
    private Task ConfirmDeleteAsync() => _delete(this);
}

/// <summary>
/// «Νέα μέτρηση»: date and weight, plus the categories the coach adds — «Λιπομέτρηση»
/// (Fat Mass/WT, fat/hgt) and «Άλλες μετρήσεις» (the coach's own saved fields).
/// </summary>
public partial class AddMeasurementViewModel : ObservableValidator
{
    private const string NumberPattern = @"^$|^\d{1,3}([.,]\d{1,2})?$";

    private readonly Guid _athleteId;
    private readonly IMeasurementRepository _measurements;
    private readonly SessionState _session;
    private readonly IMeasurementFieldRepository? _fields;

    /// <summary>The coach's saved fields (loaded when the form opens).</summary>
    private readonly List<MeasurementField> _saved = new();

    /// <summary>Editing: values whose field isn't known here; saved back unchanged.</summary>
    private readonly Dictionary<Guid, decimal> _hiddenExtra = new();

    /// <summary>Editing: the measurement's values, until the saved fields are known.</summary>
    private Dictionary<Guid, decimal>? _pendingExtra;

    public AddMeasurementViewModel(Guid athleteId, IMeasurementRepository measurements, SessionState session,
        IMeasurementFieldRepository? fields = null)
    {
        _athleteId = athleteId;
        _measurements = measurements;
        _session = session;
        _fields = fields;
        FieldsLoaded = LoadFieldsAsync();
    }

    public event Action? Saved;
    public event Action? Cancelled;

    private Measurement? _editing;

    /// <summary>Page title: add vs. edit.</summary>
    public string Title => _editing is null ? "Νέα μέτρηση" : "Επεξεργασία μέτρησης";

    /// <summary>Completes once the saved fields are loaded (or failed to load).</summary>
    public Task FieldsLoaded { get; }

    /// <summary>Switches the form to editing an existing measurement, pre-filling its fields.</summary>
    public void BeginEdit(Measurement measurement)
    {
        _editing = measurement;
        var culture = CultureInfo.CurrentCulture;
        MeasuredAt = new DateTimeOffset(measurement.MeasuredAt.ToDateTime(TimeOnly.MinValue));
        WeightKg = measurement.WeightKg?.ToString(culture) ?? string.Empty;
        Notes = measurement.Notes ?? string.Empty;
        if (measurement.HasNotes) ShowCustom = true;
        FatMassWt = measurement.FatMassWt?.ToString(culture) ?? string.Empty;
        FatHgt = measurement.FatHgt?.ToString(culture) ?? string.Empty;
        ShowFat = measurement.HasFat;
        _pendingExtra = new Dictionary<Guid, decimal>(measurement.Extra);
        if (FieldsLoaded.IsCompleted) ApplyPendingExtra();
        OnPropertyChanged(nameof(Title));
    }

    [ObservableProperty]
    public partial DateTimeOffset MeasuredAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Optional: a measurement may be only «Λιπομέτρηση», «Άλλες μετρήσεις» or notes.</summary>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression(NumberPattern, ErrorMessage = "Μη έγκυρο βάρος (π.χ. 72.5).")]
    public partial string WeightKg { get; set; } = string.Empty;

    /// <summary>«Σημειώσεις» in «Άλλες μετρήσεις»: free text with the Ασκησιολόγιο markup.</summary>
    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    public const string NothingText = "Συμπλήρωσε τουλάχιστον μία τιμή.";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression(NumberPattern, ErrorMessage = "Μη έγκυρη τιμή.")]
    public partial string FatMassWt { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression(NumberPattern, ErrorMessage = "Μη έγκυρη τιμή.")]
    public partial string FatHgt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    // ---- «Λιπομέτρηση» ----

    [ObservableProperty]
    public partial bool ShowFat { get; set; }

    [RelayCommand]
    private void AddFatSection() => ShowFat = true;

    /// <summary>× on the section: its values go too.</summary>
    [RelayCommand]
    private void RemoveFatSection()
    {
        FatMassWt = string.Empty;
        FatHgt = string.Empty;
        ClearErrors(nameof(FatMassWt));
        ClearErrors(nameof(FatHgt));
        ShowFat = false;
    }

    // ---- «Άλλες μετρήσεις» ----

    [ObservableProperty]
    public partial bool ShowCustom { get; set; }

    public ObservableCollection<CustomValueRow> CustomRows { get; } = new();

    /// <summary>Saved fields not in the form yet: one tap adds them.</summary>
    public ObservableCollection<MeasurementField> AvailableFields { get; } = new();

    public bool HasAvailableFields => AvailableFields.Count > 0;

    [RelayCommand]
    private void AddCustomSection() => ShowCustom = true;

    [RelayCommand]
    private void RemoveCustomSection()
    {
        CustomRows.Clear();
        Notes = string.Empty;
        CancelNewField();
        RefreshAvailable();
        ShowCustom = false;
    }

    [RelayCommand]
    private void AddField(MeasurementField? field)
    {
        if (field is null || CustomRows.Any(r => r.Field.Id == field.Id)) return;
        CustomRows.Add(new CustomValueRow(field, string.Empty, RemoveRow));
        RefreshAvailable();
    }

    private void RemoveRow(CustomValueRow row)
    {
        CustomRows.Remove(row);
        RefreshAvailable();
    }

    // «+ Νέο πεδίο»: name and optional unit, saved for every measurement after this one.

    [ObservableProperty]
    public partial bool IsAddingField { get; set; }

    [ObservableProperty]
    public partial string NewFieldName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewFieldUnit { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? NewFieldError { get; set; }

    /// <summary>Only a coach creates fields (and only the coach sees this form).</summary>
    public bool CanCreateFields => _fields is not null && _session.IsCoach && _session.ProfileId is not null;

    [RelayCommand]
    private void StartNewField()
    {
        NewFieldName = string.Empty;
        NewFieldUnit = string.Empty;
        NewFieldError = null;
        IsAddingField = true;
    }

    [RelayCommand]
    private void CancelNewField()
    {
        IsAddingField = false;
        NewFieldError = null;
    }

    [RelayCommand]
    private async Task CreateFieldAsync()
    {
        var name = (NewFieldName ?? string.Empty).Trim();
        var unit = (NewFieldUnit ?? string.Empty).Trim();
        if (name.Length == 0) { NewFieldError = "Γράψε όνομα πεδίου."; return; }
        if (name.Length > 40) { NewFieldError = "Έως 40 χαρακτήρες."; return; }
        if (unit.Length > 12) { NewFieldError = "Η μονάδα έως 12 χαρακτήρες."; return; }
        if (_saved.Any(f => string.Equals(f.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase)))
        {
            NewFieldError = "Υπάρχει ήδη πεδίο με αυτό το όνομα.";
            return;
        }
        if (!CanCreateFields) return;

        NewFieldError = null;
        try
        {
            var field = await _fields!.AddAsync(_session.ProfileId!.Value, name, unit.Length == 0 ? null : unit);
            _saved.Add(field);
            _saved.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            IsAddingField = false;
            AddField(field);
            if (IsManaging) RefreshManaged();
        }
        catch (DuplicateMeasurementFieldException ex)
        {
            NewFieldError = ex.Message;
        }
        catch (Exception ex)
        {
            NewFieldError = ex.Message;
        }
    }

    // ---- «Διαχείριση πεδίων» ----

    [ObservableProperty]
    public partial bool IsManaging { get; set; }

    public ObservableCollection<FieldManageRow> ManagedFields { get; } = new();

    public bool HasSavedFields => _saved.Count > 0;

    [RelayCommand]
    private void ToggleManage()
    {
        IsManaging = !IsManaging;
        if (IsManaging) RefreshManaged();
    }

    private void RefreshManaged()
    {
        ManagedFields.Clear();
        foreach (var f in _saved)
            ManagedFields.Add(new FieldManageRow(f, SaveRenameAsync, AskDeleteAsync, DeleteFieldAsync));
        OnPropertyChanged(nameof(HasSavedFields));
    }

    /// <summary>Same checks as a new field; a name clash with another field is refused.</summary>
    private static string? CheckName(string name, string unit) =>
        name.Length == 0 ? "Γράψε όνομα πεδίου."
        : name.Length > 40 ? "Έως 40 χαρακτήρες."
        : unit.Length > 12 ? "Η μονάδα έως 12 χαρακτήρες."
        : null;

    private async Task SaveRenameAsync(FieldManageRow row)
    {
        if (_fields is null) return;
        var name = (row.EditName ?? string.Empty).Trim();
        var unit = (row.EditUnit ?? string.Empty).Trim();
        if (CheckName(name, unit) is { } problem) { row.Error = problem; return; }
        if (_saved.Any(f => f.Id != row.Field.Id && string.Equals(f.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase)))
        {
            row.Error = "Υπάρχει ήδη πεδίο με αυτό το όνομα.";
            return;
        }

        row.IsBusy = true;
        try
        {
            var renamed = await _fields.UpdateAsync(row.Field.Id, name, unit.Length == 0 ? null : unit);
            var at = _saved.FindIndex(f => f.Id == renamed.Id);
            if (at >= 0) _saved[at] = renamed;
            _saved.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            // A row of it already in the form keeps its value under the new name.
            for (var i = 0; i < CustomRows.Count; i++)
            {
                if (CustomRows[i].Field.Id == renamed.Id)
                    CustomRows[i] = new CustomValueRow(renamed, CustomRows[i].Value, RemoveRow);
            }
            RefreshAvailable();
            RefreshManaged();
        }
        catch (Exception ex)
        {
            row.Error = ex.Message;
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    private async Task AskDeleteAsync(FieldManageRow row)
    {
        if (_fields is null) return;
        row.Error = null;
        row.IsEditing = false;
        row.IsBusy = true;
        try
        {
            var count = await _fields.UsageAsync(row.Field.Id);
            row.DeleteText = DeleteQuestion(row.Field.Name, count);
            row.IsConfirmingDelete = true;
        }
        catch (Exception ex)
        {
            row.Error = ex.Message;
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    internal static string DeleteQuestion(string name, int count) => count switch
    {
        0 => $"Διαγραφή του «{name}»; Δεν έχει τιμές ακόμα.",
        1 => $"Διαγραφή του «{name}»; Θα σβηστεί και 1 τιμή από μετρήσεις.",
        _ => $"Διαγραφή του «{name}»; Θα σβηστούν και {count} τιμές από μετρήσεις.",
    };

    private async Task DeleteFieldAsync(FieldManageRow row)
    {
        if (_fields is null) return;
        row.IsBusy = true;
        try
        {
            await _fields.DeleteAsync(row.Field.Id);
            _saved.RemoveAll(f => f.Id == row.Field.Id);
            foreach (var r in CustomRows.Where(r => r.Field.Id == row.Field.Id).ToList()) CustomRows.Remove(r);
            _hiddenExtra.Remove(row.Field.Id);
            RefreshAvailable();
            RefreshManaged();
            if (_saved.Count == 0) IsManaging = false;
        }
        catch (Exception ex)
        {
            row.Error = ex.Message;
            row.IsBusy = false;
        }
    }

    private async Task LoadFieldsAsync()
    {
        if (_fields is not null)
        {
            try
            {
                _saved.AddRange(await _fields.GetAllAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AthloTrack] measurement fields failed to load: {ex.Message}");
            }
        }
        ApplyPendingExtra();
        RefreshAvailable();
    }

    private void ApplyPendingExtra()
    {
        if (_pendingExtra is not { } extra) return;
        _pendingExtra = null;
        var culture = CultureInfo.CurrentCulture;
        foreach (var (id, value) in extra)
        {
            if (_saved.FirstOrDefault(f => f.Id == id) is { } field)
                CustomRows.Add(new CustomValueRow(field, value.ToString(culture), RemoveRow));
            else
                _hiddenExtra[id] = value;
        }
        if (CustomRows.Count > 0) ShowCustom = true;
        RefreshAvailable();
    }

    private void RefreshAvailable()
    {
        AvailableFields.Clear();
        foreach (var f in _saved.Where(f => CustomRows.All(r => r.Field.Id != f.Id)))
            AvailableFields.Add(f);
        OnPropertyChanged(nameof(HasAvailableFields));
        OnPropertyChanged(nameof(HasSavedFields));
    }

    // ---- save ----

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        ValidateAllProperties();
        var customOk = true;
        foreach (var row in CustomRows)
        {
            var ok = Regex.IsMatch(row.Value.Trim(), NumberPattern);
            row.Error = ok ? null : "Μη έγκυρη τιμή.";
            customOk &= ok;
        }
        if (HasErrors || !customOk)
        {
            ErrorMessage = "Παρακαλώ διορθώστε τα πεδία.";
            return;
        }

        var notes = ShowCustom && !string.IsNullOrWhiteSpace(Notes) ? Notes.Trim() : null;
        var extraNow = BuildExtra();
        if (ParseNullable(WeightKg) is null
            && !(ShowFat && (ParseNullable(FatMassWt) is not null || ParseNullable(FatHgt) is not null))
            && extraNow.Count == 0 && notes is null)
        {
            ErrorMessage = NothingText;
            return;
        }

        IsBusy = true;
        try
        {
            var measuredAt = DateOnly.FromDateTime(MeasuredAt.DateTime);
            var weight = ParseNullable(WeightKg);
            var fatMass = ShowFat ? ParseNullable(FatMassWt) : null;
            var fatHgt = ShowFat ? ParseNullable(FatHgt) : null;
            var extra = extraNow;

            if (_editing is { } existing)
            {
                existing.MeasuredAt = measuredAt;
                existing.WeightKg = weight;
                existing.FatMassWt = fatMass;
                existing.FatHgt = fatHgt;
                existing.Extra = extra;
                existing.Notes = notes;
                await _measurements.UpdateAsync(existing);
            }
            else
            {
                await _measurements.AddAsync(new Measurement
                {
                    AthleteId = _athleteId,
                    MeasuredAt = measuredAt,
                    WeightKg = weight,
                    FatMassWt = fatMass,
                    FatHgt = fatHgt,
                    Extra = extra,
                    Notes = notes,
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

    /// <summary>The filled custom values, plus (editing) values of fields not known here.</summary>
    private Dictionary<Guid, decimal> BuildExtra()
    {
        var extra = new Dictionary<Guid, decimal>(_hiddenExtra);
        foreach (var row in CustomRows)
        {
            if (ParseNullable(row.Value) is { } value) extra[row.Field.Id] = value;
        }
        return extra;
    }

    private static decimal? ParseDecimal(string s) =>
        decimal.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static decimal? ParseNullable(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : ParseDecimal(s);
}
