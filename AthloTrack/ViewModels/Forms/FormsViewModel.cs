using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AthloTrack.Core.Data;
using AthloTrack.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AthloTrack.ViewModels;

/// <summary>«Έντυπα» (coach): the forms, with how many athletes have submitted each.</summary>
public partial class FormsViewModel : ViewModelBase
{
    private readonly IFormRepository _forms;

    public FormsViewModel(IFormRepository forms)
    {
        _forms = forms;
        _ = LoadAsync();
    }

    public event Action? NewFormRequested;
    public event Action<FormDefinition>? OpenFormRequested;

    public ObservableCollection<FormListItem> Forms { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool IsEmpty => !IsLoading && Forms.Count == 0 && ErrorMessage is null;

    [RelayCommand]
    private void NewForm() => NewFormRequested?.Invoke();

    [RelayCommand]
    private void OpenForm(FormListItem? item)
    {
        if (item is not null) OpenFormRequested?.Invoke(item.Form);
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var forms = await _forms.GetFormsAsync();
            var copies = await _forms.GetAllAssignmentsAsync();
            Forms.Clear();
            foreach (var f in forms)
            {
                var mine = copies.Where(a => a.FormId == f.Id).ToList();
                Forms.Add(new FormListItem(f, mine.Count, mine.Count(a => a.IsSubmitted)));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}

/// <summary>A form in the list: «Υποβλήθηκαν 3 από 5».</summary>
public sealed class FormListItem(FormDefinition form, int sent, int submitted)
{
    public FormDefinition Form { get; } = form;
    public string Title => Form.Title;
    public int Sent { get; } = sent;
    public int Submitted { get; } = submitted;
    public bool HasPending => Submitted < Sent;

    public string Summary => Sent == 0 ? "Δεν έχει σταλεί ακόμη" : $"Υποβλήθηκαν {Submitted} από {Sent}";

    public string QuestionsText => Form.Questions.Count == 1 ? "1 ερώτηση" : $"{Form.Questions.Count} ερωτήσεις";
}
