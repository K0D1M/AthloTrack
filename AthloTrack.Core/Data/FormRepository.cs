using AthloTrack.Core.Models;
using AthloTrack.Core.Supabase;
using AthloTrack.Core.Supabase.Rows;
using Newtonsoft.Json.Linq;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;

namespace AthloTrack.Core.Data;

public sealed class FormRepository : IFormRepository
{
    private readonly SupabaseClientFactory _factory;

    public FormRepository(SupabaseClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<FormDefinition>> GetFormsAsync()
    {
        var client = await _factory.GetClientAsync();
        var response = await Guard(() => client.From<FormRow>().Order(x => x.CreatedAt, Constants.Ordering.Descending).Get());
        return response.Models.Select(Map).ToList();
    }

    public async Task<FormDefinition?> GetFormAsync(Guid id)
    {
        var client = await _factory.GetClientAsync();
        var response = await Guard(() => client.From<FormRow>().Where(x => x.Id == id).Get());
        return response.Models.Select(Map).FirstOrDefault();
    }

    public async Task<FormDefinition> SaveFormAsync(FormDefinition form)
    {
        var client = await _factory.GetClientAsync();
        var row = MapBack(form);
        if (form.Id == Guid.Empty)
        {
            row.Id = Guid.NewGuid();
            var added = await Guard(() => client.From<FormRow>().Insert(row));
            return Map(added.Models.First());
        }

        var saved = await Guard(() => client.From<FormRow>()
            .Where(x => x.Id == form.Id)
            .Set(x => x.Title, row.Title)
            .Set(x => x.Intro, row.Intro)
            .Set(x => x.Questions, row.Questions)
            .Update());
        return saved.Models.Select(Map).FirstOrDefault() ?? form;
    }

    public async Task DeleteFormAsync(Guid id)
    {
        var client = await _factory.GetClientAsync();
        await Guard(() => client.From<FormRow>().Where(x => x.Id == id).Delete());
    }

    public async Task<int> AssignAsync(Guid formId, IEnumerable<Guid> athleteIds)
    {
        var client = await _factory.GetClientAsync();
        var existing = (await GetAssignmentsAsync(formId)).Select(a => a.AthleteId).ToHashSet();
        var rows = athleteIds.Distinct().Where(id => !existing.Contains(id))
            .Select(id => new FormAssignmentRow { Id = Guid.NewGuid(), FormId = formId, AthleteId = id })
            .ToList();
        if (rows.Count == 0) return 0;
        await Guard(() => client.From<FormAssignmentRow>().Insert(rows));
        return rows.Count;
    }

    public async Task<IReadOnlyList<FormAssignment>> GetAssignmentsAsync(Guid formId)
    {
        var client = await _factory.GetClientAsync();
        var response = await Guard(() => client.From<FormAssignmentRow>()
            .Where(x => x.FormId == formId)
            .Order(x => x.CreatedAt, Constants.Ordering.Ascending)
            .Get());
        return response.Models.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<FormAssignment>> GetAssignmentsForAthleteAsync(Guid athleteId)
    {
        var client = await _factory.GetClientAsync();
        var response = await Guard(() => client.From<FormAssignmentRow>()
            .Where(x => x.AthleteId == athleteId)
            .Order(x => x.CreatedAt, Constants.Ordering.Descending)
            .Get());
        return response.Models.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<FormAssignment>> GetAllAssignmentsAsync()
    {
        var client = await _factory.GetClientAsync();
        var response = await Guard(() => client.From<FormAssignmentRow>()
            .Order(x => x.CreatedAt, Constants.Ordering.Descending)
            .Get());
        return response.Models.Select(Map).ToList();
    }

    public async Task<FormAssignment?> GetAssignmentAsync(Guid id)
    {
        var client = await _factory.GetClientAsync();
        var response = await Guard(() => client.From<FormAssignmentRow>().Where(x => x.Id == id).Get());
        return response.Models.Select(Map).FirstOrDefault();
    }

    public async Task<DateTimeOffset> SubmitAsync(Guid assignmentId, IReadOnlyDictionary<string, string> answers)
    {
        var client = await _factory.GetClientAsync();
        var json = new JObject();
        foreach (var (key, value) in answers)
            if (!string.IsNullOrWhiteSpace(value)) json[key] = value.Trim();
        var response = await Guard(() => client.Rpc("submit_form",
            new Dictionary<string, object> { ["p_assignment"] = assignmentId, ["p_answers"] = json }));
        var text = response.Content?.Trim().Trim('"');
        return DateTimeOffset.TryParse(text, out var at) ? at : DateTimeOffset.UtcNow;
    }

    /// <summary>The database's refusals, in Greek.</summary>
    private static async Task<T> Guard<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (PostgrestException ex)
        {
            var message = ex.Message.Contains("PGRST202") || ex.Message.Contains("PGRST205") || ex.Message.Contains("42P01")
                    ? "Τα έντυπα δεν είναι ακόμα διαθέσιμα (λείπει το 018_forms.sql)."
                : ex.Message.Contains("already submitted") ? "Το έντυπο έχει ήδη υποβληθεί."
                : ex.Message.Contains("locked") ? "Κάποιος αθλητής το έχει ήδη υποβάλει: οι ερωτήσεις δεν αλλάζουν πια. Φτιάξε ένα αντίγραφο."
                : ex.Message.Contains("missing") ? "Συμπλήρωσε όλες τις υποχρεωτικές ερωτήσεις."
                : ex.Message.Contains("not allowed") || ex.Message.Contains("42501") ? "Δεν επιτρέπεται."
                : ex.Message;
            throw new FormException(message, ex);
        }
    }

    private static async Task Guard(Func<Task> call) => await Guard(async () => { await call(); return true; });

    internal static FormDefinition Map(FormRow r) => new()
    {
        Id = r.Id,
        CoachId = r.CoachId,
        CoachName = r.CoachName,
        Title = r.Title,
        Intro = r.Intro,
        Questions = r.Questions.OfType<JObject>().Select(MapQuestion).ToList(),
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };

    private static FormQuestion MapQuestion(JObject q) => new()
    {
        Id = q.Value<string>("id") ?? Guid.NewGuid().ToString("N")[..8],
        Kind = Enum.TryParse<FormQuestionKind>(q.Value<string>("kind"), ignoreCase: true, out var kind) ? kind : FormQuestionKind.Short,
        Label = q.Value<string>("label") ?? string.Empty,
        Required = q.Value<bool?>("required") ?? false,
        Options = q["options"] is JArray options ? options.Select(o => o.ToString()).ToList() : new(),
    };

    private static FormRow MapBack(FormDefinition f) => new()
    {
        Id = f.Id,
        CoachId = f.CoachId,
        Title = f.Title.Trim(),
        Intro = f.Intro,
        Questions = new JArray(f.Questions.Select(q => new JObject
        {
            ["id"] = q.Id,
            ["kind"] = q.Kind.ToString().ToLowerInvariant(), // short | long | yesno | choice | date
            ["label"] = q.Label.Trim(),
            ["required"] = q.Required,
            ["options"] = new JArray(q.Kind == FormQuestionKind.Choice
                ? q.Options.Select(o => o.Trim()).Where(o => o.Length > 0).ToArray<object>()
                : Array.Empty<object>()),
        })),
    };

    internal static FormAssignment Map(FormAssignmentRow r) => new()
    {
        Id = r.Id,
        FormId = r.FormId,
        AthleteId = r.AthleteId,
        Answers = r.Answers?.Properties().ToDictionary(p => p.Name, p => p.Value.ToString()) ?? new(),
        SubmittedAt = r.SubmittedAt,
        CreatedAt = r.CreatedAt,
    };
}
