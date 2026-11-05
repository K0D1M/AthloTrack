using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>«Αποστολή σε…»: the coach ticks athletes (or «Όλοι»); ones that already have it are shown, not ticked again.</summary>
public partial class FormSendViewModel : ViewModelBase
{
    private readonly IFormRepository _forms;
    private readonly IAthleteRepository _athletes;
    private bool _settingAll;

    public FormSendViewModel(IFormRepository forms, IAthleteRepository athletes, FormDefinition form)
    {
        _forms = forms;
        _athletes = athletes;
        Form = form;
        _ = LoadAsync();
    }

    public FormDefinition Form { get; }
    public string Title => $"Αποστολή: {Form.Title}";

    /// <summary>Sent to this many athletes; the page goes back to the form.</summary>
    public event Action<int>? Sent;
    public event Action? Cancelled;

    public ObservableCollection<FormSendAthlete> Athletes { get; } = new();

    [ObservableProperty]
    public partial bool AllChecked { get; set; }

    partial void OnAllCheckedChanged(bool value)
    {
        if (_settingAll) return;
        _settingAll = true;
        foreach (var a in Athletes.Where(a => !a.AlreadySent)) a.IsChecked = value;
        _settingAll = false;
        OnPropertyChanged(nameof(CanSend));
    }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool CanSend => !IsBusy && Athletes.Any(a => a.IsChecked && !a.AlreadySent);
    public bool HasNobody => Athletes.Count == 0 && !IsBusy;

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var sent = (await _forms.GetAssignmentsAsync(Form.Id)).Select(a => a.AthleteId).ToHashSet();
            var list = await _athletes.GetAllAsync();
            Athletes.Clear();
            foreach (var a in list.OrderBy(a => a.FullName, StringComparer.Create(new System.Globalization.CultureInfo("el-GR"), true)))
            {
                var item = new FormSendAthlete(a.Id, a.FullName, sent.Contains(a.Id));
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(FormSendAthlete.IsChecked) || _settingAll) return;
                    OnPropertyChanged(nameof(CanSend));
                    _settingAll = true;
                    AllChecked = Athletes.Where(x => !x.AlreadySent).All(x => x.IsChecked);
                    _settingAll = false;
                };
                Athletes.Add(item);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSend));
            OnPropertyChanged(nameof(HasNobody));
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    [RelayCommand]
    private async Task SendAsync()
    {
        if (!CanSend) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var count = await _forms.AssignAsync(Form.Id, Athletes.Where(a => a.IsChecked && !a.AlreadySent).Select(a => a.Id));
            Sent?.Invoke(count);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSend));
        }
    }
}

public sealed partial class FormSendAthlete(Guid id, string name, bool alreadySent) : ObservableObject
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";

    /// <summary>Has the form already: shown ticked and greyed, «Έχει σταλεί».</summary>
    public bool AlreadySent { get; } = alreadySent;
    public bool CanChoose => !AlreadySent;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = alreadySent;
}
