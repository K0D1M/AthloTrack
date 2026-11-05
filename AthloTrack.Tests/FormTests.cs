using System.IO.Compression;
using System.Text;
using AthloTrack.Core.Auth;
using AthloTrack.Core.Forms;
using AthloTrack.Core.Models;
using AthloTrack.Core.Workouts;
using AthloTrack.Services;
using AthloTrack.ViewModels;
using SkiaSharp;

namespace AthloTrack.Tests;

/// <summary>«Έντυπα»: editing, sending, filling in once, the PDF, and Word.</summary>
public sealed class FormTests
{
    private readonly FakeForms _forms = new();
    private readonly FakeAthletes _athletes = new();
    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Athlete _maria = new() { Id = Guid.NewGuid(), FullName = "Μαρία" };
    private readonly Athlete _nikos = new() { Id = Guid.NewGuid(), FullName = "Νίκος" };

    public FormTests()
    {
        _athletes.Items.Add(_maria);
        _athletes.Items.Add(_nikos);
    }

    private FormDefinition Form() => new()
    {
        Id = Guid.NewGuid(), CoachId = _coachId, CoachName = "Δοκιμαστικός Προπονητής", Title = "Ιστορικό υγείας",
        Intro = "## Οδηγίες\n- Συμπλήρωσε τα πάντα.",
        Questions =
        [
            new() { Id = "name", Kind = FormQuestionKind.Short, Label = "Ονοματεπώνυμο", Required = true },
            new() { Id = "ok", Kind = FormQuestionKind.YesNo, Label = "Είσαι καλά;", Required = true },
            new() { Id = "freq", Kind = FormQuestionKind.Choice, Label = "Συχνότητα", Options = ["1–2", "3+"] },
            new() { Id = "born", Kind = FormQuestionKind.Date, Label = "Γέννηση" },
        ],
    };

    [Fact]
    public async Task The_editor_checks_the_title_the_questions_and_the_choices()
    {
        var vm = new FormEditorViewModel(_forms, Sessions.Coach(_coachId));
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Δώσε τίτλο στο έντυπο.", vm.FormError);

        vm.Title = "Ερωτηματολόγιο";
        vm.AddQuestionCommand.Execute(null);
        vm.AddQuestionCommand.Execute(null);
        vm.Questions[1].KindIndex = (int)FormQuestionKind.Choice;
        vm.Questions[1].Label = "Επιλογή";
        vm.Questions[1].OptionsText = "μόνο μία";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.NotNull(vm.FormError);
        Assert.NotNull(vm.Questions[0].Error); // no label
        Assert.NotNull(vm.Questions[1].Error); // one option

        FormDefinition? saved = null;
        vm.Saved += f => saved = f;
        vm.Questions[0].Label = "Όνομα";
        vm.Questions[1].OptionsText = "Α\n\nΒ\n";
        vm.MoveUpCommand.Execute(vm.Questions[1]);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(vm.FormError);
        Assert.NotNull(saved);
        Assert.Equal(_coachId, saved!.CoachId);
        Assert.Equal(["Επιλογή", "Όνομα"], saved.Questions.Select(q => q.Label));
        Assert.Equal(["Α", "Β"], saved.Questions[0].Options);
        Assert.Empty(saved.Questions[1].Options); // only a choice keeps options
    }

    [Fact]
    public async Task Sending_skips_athletes_who_already_have_the_form()
    {
        var form = await _forms.SaveFormAsync(Form());
        await _forms.AssignAsync(form.Id, [_maria.Id]);
        var vm = new FormSendViewModel(_forms, _athletes, form);
        await Task.Delay(50);
        var sent = -1;
        vm.Sent += n => sent = n;

        Assert.True(vm.Athletes.Single(a => a.Id == _maria.Id).AlreadySent);
        vm.AllChecked = true;
        Assert.True(vm.CanSend);
        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal(1, sent);
        Assert.Equal(2, _forms.Copies.Count);
    }

    private async Task<(FormDefinition Form, FormAssignment Copy)> SentToMariaAsync()
    {
        var form = await _forms.SaveFormAsync(Form());
        await _forms.AssignAsync(form.Id, [_maria.Id]);
        return (form, _forms.Copies.Single());
    }

    private async Task<FormFillViewModel> OpenAsync(SessionState session, Guid copyId, IAppPreferences prefs)
    {
        var vm = new FormFillViewModel(_forms, _athletes, session, copyId, prefs);
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task The_athlete_keeps_a_draft_and_submits_once()
    {
        var (_, copy) = await SentToMariaAsync();
        var prefs = new InMemoryAppPreferences();
        var athlete = Sessions.Athlete(_maria.Id, _coachId);

        var vm = await OpenAsync(athlete, copy.Id, prefs);
        Assert.False(vm.IsReadOnly);
        vm.Items[0].Text = "Μαρία Κ.";
        vm.Items[2].SelectedOption = "3+";

        // Closed and opened again: the draft is still there, and the coach has seen nothing.
        vm = await OpenAsync(athlete, copy.Id, prefs);
        Assert.True(vm.HasDraft);
        Assert.Equal("Μαρία Κ.", vm.Items[0].Text);
        Assert.Empty(_forms.Submitted);

        vm.SubmitCommand.Execute(null);
        Assert.NotNull(vm.FormError); // «Είσαι καλά;» is required
        Assert.True(vm.Items[1].IsMissing);
        Assert.False(vm.Confirm.IsOpen);

        vm.Items[1].IsYes = true;
        vm.Items[3].Date = new DateTimeOffset(new DateTime(1998, 3, 12));
        vm.SubmitCommand.Execute(null);
        Assert.True(vm.Confirm.IsOpen); // «Σίγουρα;»
        await vm.Confirm.ConfirmCommand.ExecuteAsync(null);

        var (_, answers) = Assert.Single(_forms.Submitted);
        Assert.Equal("yes", answers["ok"]);
        Assert.Equal("1998-03-12", answers["born"]);
        Assert.True(vm.IsReadOnly);
        Assert.True(vm.IsSubmitted);
        Assert.Null(prefs.Get($"form.draft.{copy.Id}")); // the draft is gone
        Assert.Equal("Ναι", vm.Items[1].Display);
        Assert.Equal("12/03/1998", vm.Items[3].Display);
    }

    [Fact]
    public async Task The_coach_only_reads_and_the_pdf_needs_a_submitted_copy()
    {
        var (_, copy) = await SentToMariaAsync();
        var coach = await OpenAsync(Sessions.Coach(_coachId), copy.Id, new InMemoryAppPreferences());
        Assert.True(coach.IsReadOnly);
        Assert.False(coach.CanSubmit);
        Assert.Null(coach.CreatePdf()); // nothing submitted yet

        var detail = new FormDetailViewModel(_forms, _athletes, _forms.Forms[0]);
        await detail.LoadAsync();
        Assert.False(detail.IsLocked);
        Assert.Equal("Υποβλήθηκαν 0 από 1", detail.Summary);

        await _forms.SubmitAsync(copy.Id, new Dictionary<string, string> { ["name"] = "Μ", ["ok"] = "no" });
        await detail.LoadAsync();
        Assert.True(detail.IsLocked); // the questions can't change now
        FormAssignment? opened = null;
        detail.OpenAnswersRequested += (c, _, _) => opened = c;
        detail.OpenAnswersCommand.Execute(detail.Recipients[0]);
        Assert.Equal(copy.Id, opened?.Id);
    }

    [Fact]
    public async Task The_pdf_is_a_real_multi_page_pdf()
    {
        var form = Form();
        for (var i = 0; i < 30; i++) form.Questions.Add(new() { Id = $"x{i}", Kind = FormQuestionKind.Long, Label = $"Ερώτηση {i}" });
        var copy = new FormAssignment
        {
            SubmittedAt = DateTimeOffset.Now,
            Answers = form.Questions.ToDictionary(q => q.Id, _ => "Μια απάντηση που πιάνει αρκετό χώρο στη σελίδα για να χρειαστεί δεύτερη."),
        };
        var pdf = new FormPdf(SKTypeface.Default, SKTypeface.Default).Create(form, copy, "Μαρία");

        var text = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-", text);
        Assert.True(System.Text.RegularExpressions.Regex.Matches(text, @"/Type /Page\b").Count >= 2);
        Assert.Equal("Ιστορικό υγείας - Μαρία.pdf", FormPdf.FileName(form, "Μαρία"));
        await Task.CompletedTask;
    }

    [Fact]
    public void Text_pasted_from_word_becomes_markup()
    {
        var pasted = "Οδηγίες\r\n•\tΠρώτο\r\no\tΔεύτερο\r\n1)\tΑρχή\r\n2.\tΜετά\r\n\r\n\r\n\r\nΤέλος\tκειμένου\r\n";
        Assert.Equal("Οδηγίες\n- Πρώτο\n- Δεύτερο\n1. Αρχή\n2. Μετά\n\nΤέλος κειμένου", WorkoutMarkup.NormalizePasted(pasted));
        Assert.Equal("**έντονο** κείμενο", WorkoutMarkup.NormalizePasted("**έντονο** κείμενο"));
    }

    [Fact]
    public void A_docx_keeps_headings_lists_bold_and_tables()
    {
        const string w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        static string P(string inner, string? style = null, int? numId = null) =>
            "<w:p><w:pPr>" + (style is null ? "" : $"<w:pStyle w:val=\"{style}\"/>")
            + (numId is null ? "" : $"<w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"{numId}\"/></w:numPr>") + "</w:pPr>" + inner + "</w:p>";
        static string R(string text, bool bold = false) =>
            "<w:r>" + (bold ? "<w:rPr><w:b/></w:rPr>" : "") + $"<w:t xml:space=\"preserve\">{text}</w:t></w:r>";

        var document = $"<w:document xmlns:w=\"{w}\"><w:body>"
            + P(R("Ιστορικό"), style: "Heading1")
            + P(R("Συμπλήρωσε ") + R("όλα", bold: true) + R(" τα πεδία."))
            + P(R("Πρώτο"), numId: 1) + P(R("Δεύτερο"), numId: 1)
            + P(R("Βήμα"), numId: 2) + P(R("Επόμενο"), numId: 2)
            + "<w:tbl><w:tr><w:tc>" + P(R("Όνομα")) + "</w:tc><w:tc>" + P(R("Τηλέφωνο")) + "</w:tc></w:tr></w:tbl>"
            + "</w:body></w:document>";
        var numbering = $"<w:numbering xmlns:w=\"{w}\">"
            + "<w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"bullet\"/></w:lvl></w:abstractNum>"
            + "<w:abstractNum w:abstractNumId=\"1\"><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"decimal\"/></w:lvl></w:abstractNum>"
            + "<w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num><w:num w:numId=\"2\"><w:abstractNumId w:val=\"1\"/></w:num>"
            + "</w:numbering>";

        using var docx = new MemoryStream();
        using (var zip = new ZipArchive(docx, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, xml) in new[] { ("word/document.xml", document), ("word/numbering.xml", numbering) })
            {
                using var s = new StreamWriter(zip.CreateEntry(path).Open());
                s.Write(xml);
            }
        }
        docx.Position = 0;

        Assert.Equal(
            "## Ιστορικό\nΣυμπλήρωσε **όλα** τα πεδία.\n- Πρώτο\n- Δεύτερο\n1. Βήμα\n2. Επόμενο\nΌνομα · Τηλέφωνο",
            DocxImport.ToMarkup(docx));
    }

    [Fact]
    public void A_form_notification_opens_its_own_page()
    {
        AthloTrack.Services.NotificationNavigation.RequestFor("new_form", Guid.NewGuid().ToString());
        var request = AthloTrack.Services.NotificationNavigation.TakePending();
        Assert.NotNull(request);
        Assert.Equal(AthloTrack.Services.NotificationNavigation.HomeSection, request!.Section);
        Assert.Single(request.NotificationIds);
    }
}
