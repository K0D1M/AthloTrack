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
        Assert.True(coach.IsPendingForReader); // «Εκκρεμεί: δεν έχει υποβληθεί ακόμη»
        Assert.True(coach.CanDownload); // the blank form, for printing

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
    public async Task Completed_lists_every_submitted_copy_newest_first_and_remembers_the_choice()
    {
        var form = await _forms.SaveFormAsync(Form());
        var other = await _forms.SaveFormAsync(new FormDefinition { CoachId = _coachId, Title = "Συγκατάθεση" });
        await _forms.AssignAsync(form.Id, [_maria.Id, _nikos.Id]);
        await _forms.AssignAsync(other.Id, [_maria.Id]);
        var now = DateTimeOffset.Now;
        _forms.Copies[0].SubmittedAt = now.AddDays(-1); // Μαρία, Ιστορικό, yesterday
        _forms.Copies[2].SubmittedAt = now;             // Μαρία, Συγκατάθεση, today
        // Νίκος hasn't submitted.

        var prefs = new InMemoryAppPreferences();
        var vm = new FormsViewModel(_forms, _athletes, prefs);
        await vm.LoadAsync();
        Assert.True(vm.ShowAll);
        Assert.Equal(2, vm.CompletedCount);
        Assert.Equal(["Σήμερα", "Χθες"], vm.Completed.Select(d => d.Title));
        Assert.Equal(("Μαρία", "Συγκατάθεση"), (vm.Completed[0].Items[0].AthleteName, vm.Completed[0].Items[0].FormTitle));
        Assert.Equal("Ιστορικό υγείας", vm.Completed[1].Items[0].FormTitle);

        Guid? opened = null;
        vm.OpenCopyRequested += id => opened = id;
        vm.OpenCopyCommand.Execute(vm.Completed[0].Items[0]);
        Assert.Equal(_forms.Copies[2].Id, opened);

        vm.ShowCompletedFormsCommand.Execute(null);
        var reopened = new FormsViewModel(_forms, _athletes, prefs);
        Assert.True(reopened.ShowCompleted); // remembered
        Assert.False(new FormsViewModel(_forms, _athletes, prefs, showCompleted: false).ShowCompleted);
    }

    [Fact]
    public async Task The_coach_sends_a_form_from_the_profile_once()
    {
        var form = await _forms.SaveFormAsync(Form());
        var other = await _forms.SaveFormAsync(new FormDefinition { CoachId = _coachId, Title = "Συγκατάθεση" });
        await _forms.AssignAsync(form.Id, [_maria.Id]);
        var vm = new AthleteProfileViewModel(_maria.Id, _athletes, new FakeMeasurements(), new FakeWorkouts(),
            Sessions.Coach(_coachId), new FakeAvatars(), forms: _forms);
        await Task.Delay(100);
        Assert.True(vm.ShowFormsSection);

        await vm.OpenSendSheetCommand.ExecuteAsync(null);
        Assert.True(vm.IsSendSheetOpen);
        Assert.True(vm.SendChoices.Single(c => c.Form.Id == form.Id).AlreadySent);
        var choice = vm.SendChoices.Single(c => c.Form.Id == other.Id);
        Assert.False(choice.AlreadySent);

        await vm.SendFormCommand.ExecuteAsync(choice);
        Assert.False(vm.IsSendSheetOpen);
        Assert.Contains(_forms.Copies, c => c.FormId == other.Id && c.AthleteId == _maria.Id);
        Assert.Equal(2, vm.Forms.Count);
        await vm.SendFormCommand.ExecuteAsync(choice); // the sheet's old row: not sent twice
        Assert.Single(_forms.Copies, c => c.FormId == other.Id);
    }

    [Fact]
    public void The_blank_pdf_has_room_to_write_and_paginates()
    {
        var form = Form();
        for (var i = 0; i < 25; i++) form.Questions.Add(new() { Id = $"l{i}", Kind = FormQuestionKind.Long, Label = $"Ερώτηση {i}" });
        var pdf = new FormPdf(SKTypeface.Default, SKTypeface.Default).Create(form, new FormAssignment(), "Μαρία");

        var text = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-", text);
        Assert.True(System.Text.RegularExpressions.Regex.Matches(text, @"/Type /Page\b").Count >= 3);
        Assert.Equal("Ιστορικό υγείας - κενό.pdf", FormPdf.FileName(form, "Μαρία", blank: true));
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
    public void A_docx_keeps_its_blanks_word_fields_and_tick_boxes()
    {
        const string w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        static string P(string inner) => "<w:p>" + inner + "</w:p>";
        static string R(string text) => $"<w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r>";
        static string Tc(string inner) => "<w:tc>" + inner + "</w:tc>";
        const string Placeholder = "<w:sdtPr><w:showingPlcHdr/></w:sdtPr>";

        var document = $"<w:document xmlns:w=\"{w}\"><w:body>"
            // a label with an empty answer cell, and one whose answer cell is an empty form control
            + "<w:tbl><w:tr>" + Tc(P(R("Ύψος (cm)"))) + Tc("<w:p/>") + "</w:tr>"
            + "<w:tr>" + Tc(P(R("Ομάδα αίματος"))) + Tc($"<w:sdt>{Placeholder}<w:sdtContent>" + P(R("Επιλέξτε")) + "</w:sdtContent></w:sdt>") + "</w:tr>"
            + "<w:tr>" + Tc("<w:p/>") + Tc("<w:p/>") + "</w:tr></w:tbl>"
            // text inside a form control is kept; an empty control is a blank
            + "<w:sdt><w:sdtPr/><w:sdtContent>" + P(R("Μέσα σε πεδίο.")) + "</w:sdtContent></w:sdt>"
            + P(R("Σχόλια: ") + $"<w:sdt>{Placeholder}<w:sdtContent>" + R("Κάντε κλικ εδώ.") + "</w:sdtContent></w:sdt>")
            // old-style text field and tick box
            + P(R("Τηλέφωνο: ") + "<w:r><w:fldChar w:fldCharType=\"begin\"><w:ffData><w:textInput/></w:ffData></w:fldChar></w:r>"
                + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>" + R("     ") + "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>")
            + P("<w:r><w:fldChar w:fldCharType=\"begin\"><w:ffData><w:checkBox><w:default w:val=\"1\"/></w:checkBox></w:ffData></w:fldChar></w:r>"
                + "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>" + R("Συμφωνώ"))
            // a Wingdings box
            + P("<w:r><w:sym w:font=\"Wingdings\" w:char=\"F0A8\"/></w:r>" + R(" Ναι"))
            + "</w:body></w:document>";

        using var docx = new MemoryStream();
        using (var zip = new ZipArchive(docx, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var s = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            s.Write(document);
        }
        docx.Position = 0;

        Assert.Equal(
            "Ύψος (cm): ______\nΟμάδα αίματος: ______\n\nΜέσα σε πεδίο.\nΣχόλια: ______\nΤηλέφωνο: ______\n☒ Συμφωνώ\n☐ Ναι",
            DocxImport.ToMarkup(docx));
    }

    [Fact]
    public async Task The_athlete_edits_the_document_and_the_coach_reads_their_copy()
    {
        var form = Form();
        form.Intro = "## Συγκατάθεση\nΟνοματεπώνυμο: ______";
        form.IntroEditable = true;
        form.Questions.Clear();
        await _forms.SaveFormAsync(form);
        await _forms.AssignAsync(form.Id, [_maria.Id]);
        var copy = _forms.Copies.Single();
        var prefs = new InMemoryAppPreferences();
        var athlete = Sessions.Athlete(_maria.Id, _coachId);

        var vm = await OpenAsync(athlete, copy.Id, prefs);
        Assert.True(vm.CanEditDocument);
        Assert.False(vm.HasIntro); // the editor shows it, not the read-only text
        Assert.Equal(form.Intro, vm.Document);
        vm.Document = "## Συγκατάθεση\nΟνοματεπώνυμο: Μαρία Κ.";

        vm = await OpenAsync(athlete, copy.Id, prefs); // the draft keeps the edited text
        Assert.Equal("## Συγκατάθεση\nΟνοματεπώνυμο: Μαρία Κ.", vm.Document);

        vm.SubmitCommand.Execute(null);
        await vm.Confirm.ConfirmCommand.ExecuteAsync(null);
        var (_, answers) = Assert.Single(_forms.Submitted);
        Assert.Equal("## Συγκατάθεση\nΟνοματεπώνυμο: Μαρία Κ.", answers[FormAnswers.DocumentKey]);
        Assert.False(vm.CanEditDocument);
        Assert.Equal("## Συγκατάθεση\nΟνοματεπώνυμο: Μαρία Κ.", vm.Intro); // read-only, the athlete's copy

        var coach = await OpenAsync(Sessions.Coach(_coachId), copy.Id, new InMemoryAppPreferences());
        Assert.False(coach.CanEditDocument);
        Assert.Contains("Μαρία Κ.", coach.Intro);
        Assert.Contains("Μαρία Κ.", FormAnswers.Document(form, copy.Answers)); // what the PDF prints
    }

    private static MemoryStream Docx(params string[] paragraphs)
    {
        var docx = new MemoryStream();
        using (var zip = new ZipArchive(docx, ZipArchiveMode.Create, leaveOpen: true))
        using (var s = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
            s.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>"
                    + string.Concat(paragraphs.Select(p => $"<w:p><w:r><w:t xml:space=\"preserve\">{p}</w:t></w:r></w:p>"))
                    + "</w:body></w:document>");
        docx.Position = 0;
        return docx;
    }

    [Fact]
    public void Importing_word_without_blanks_lets_the_athlete_edit_the_text()
    {
        using var docx = Docx("Γράψε λίγα λόγια για σένα.");
        var vm = new FormEditorViewModel(_forms, Sessions.Coach(_coachId));
        vm.ImportDocx(docx);

        Assert.Equal("Γράψε λίγα λόγια για σένα.", vm.Intro);
        Assert.True(vm.IntroEditable);
        Assert.False(vm.HasFields);
    }

    [Fact]
    public void Importing_word_turns_its_blanks_into_fields()
    {
        using var docx = Docx("Όνομα: ____", "Γέννηση: __/__/____", "Φάρμακα; ☐ Ναι ☐ Όχι");
        var vm = new FormEditorViewModel(_forms, Sessions.Coach(_coachId));
        vm.ImportDocx(docx);

        Assert.Equal("Όνομα: [[Κείμενο]]\nΓέννηση: [[Ημερομηνία]]\nΦάρμακα; [[Κουτάκι (προαιρετικό)]] Ναι [[Κουτάκι (προαιρετικό)]] Όχι", vm.Intro);
        Assert.False(vm.IntroEditable); // the fields, not the whole text
        Assert.True(vm.HasFields);
        Assert.Equal("4 πεδία", vm.FieldsText);
        Assert.Contains("Βρέθηκαν 4 πεδία", vm.InfoMessage);
    }

    [Fact]
    public void The_coach_changes_a_fields_kind_and_removes_one()
    {
        var vm = new FormEditorViewModel(_forms, Sessions.Coach(_coachId)) { Intro = "Α: [[Κείμενο]] Β: [[Κείμενο]]" };

        vm.ChangeField(FormFields.Parse(vm.Intro)[1], kind: FormFieldKind.Date, required: false);
        Assert.Equal("Α: [[Κείμενο]] Β: [[Ημερομηνία (προαιρετικό)]]", vm.Intro);

        vm.ChangeField(FormFields.Parse(vm.Intro)[0], remove: true);
        Assert.Equal("Α: Β: [[Ημερομηνία (προαιρετικό)]]", vm.Intro);
    }

    [Fact]
    public async Task Once_submitted_the_fields_stay_as_they_are()
    {
        var form = Form();
        form.Intro = "Όνομα: [[Κείμενο]]";
        var vm = new FormEditorViewModel(_forms, Sessions.Coach(_coachId), form, locked: true) { Intro = "Όνομα: [[Ημερομηνία]]" };

        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("τα πεδία δεν αλλάζουν", vm.FormError);

        vm.Intro = "Ονοματεπώνυμο αθλητή: [[Κείμενο]]"; // the words may change
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Null(vm.FormError);
    }

    [Fact]
    public async Task The_athlete_fills_only_the_fields_and_submits()
    {
        var form = Form();
        form.Intro = "Όνομα: [[Κείμενο]]\nΓέννηση: [[Ημερομηνία (προαιρετικό)]]\n[[Κουτάκι]] Συμφωνώ\nΣχόλια: [[Πολλές γραμμές (προαιρετικό)]]";
        form.Questions.Clear();
        await _forms.SaveFormAsync(form);
        await _forms.AssignAsync(form.Id, [_maria.Id]);
        var copy = _forms.Copies.Single();
        var prefs = new InMemoryAppPreferences();
        var athlete = Sessions.Athlete(_maria.Id, _coachId);

        var vm = await OpenAsync(athlete, copy.Id, prefs);
        Assert.True(vm.HasFields && vm.IsFillingFields);
        Assert.False(vm.CanEditDocument); // the text itself isn't theirs to change
        Assert.False(vm.HasIntro);
        Assert.Equal("0 από 4", vm.FieldsProgress);

        vm.Fields.Set("f1", "Μαρία Κ.");
        Assert.Equal("1 από 4", vm.FieldsProgress);

        vm = await OpenAsync(athlete, copy.Id, prefs); // the draft keeps it
        Assert.Equal("Μαρία Κ.", vm.Fields.Get("f1"));

        vm.SubmitCommand.Execute(null); // «Συμφωνώ» is required
        Assert.Equal("Συμπλήρωσε το υποχρεωτικό πεδίο με κόκκινο.", vm.FormError);
        Assert.True(vm.Fields.IsMissing("f3"));
        Assert.Empty(_forms.Submitted);

        vm.Fields.Set("f3", FormFields.Ticked);
        Assert.False(vm.Fields.IsMissing("f3"));
        vm.SubmitCommand.Execute(null);
        await vm.Confirm.ConfirmCommand.ExecuteAsync(null);
        var (_, answers) = Assert.Single(_forms.Submitted);
        Assert.Equal("Μαρία Κ.", answers["f1"]);
        Assert.Equal(FormFields.Ticked, answers["f3"]);
        Assert.False(answers.ContainsKey("f2"));
        Assert.False(answers.ContainsKey(FormAnswers.DocumentKey));

        // The coach reads the answers in place
        var coach = await OpenAsync(Sessions.Coach(_coachId), copy.Id, new InMemoryAppPreferences());
        Assert.True(coach.IsReadingFields);
        Assert.Equal("Μαρία Κ.", coach.Fields.Get("f1"));
    }

    [Fact]
    public void The_pdf_puts_the_answers_in_the_text_or_blanks_before_submitting()
    {
        var form = Form();
        form.Intro = "## Συγκατάθεση\nΌνομα: [[Κείμενο]] Γέννηση: [[Ημερομηνία]]\n- [[Κουτάκι]] Συμφωνώ\nΣχόλια: [[Πολλές γραμμές]]";
        form.Questions.Clear();
        var copy = new FormAssignment { Id = Guid.NewGuid(), FormId = form.Id, AthleteId = _maria.Id };
        // Real Greek glyphs (the app's own font files), so the rendered pages can be looked at.
        var fonts = new DirectoryInfo(AppContext.BaseDirectory);
        while (fonts is not null && !File.Exists(Path.Combine(fonts.FullName, "AthloTrack.slnx"))) fonts = fonts.Parent;
        var fontDir = Path.Combine(fonts!.FullName, "AthloTrack", "Assets", "Fonts");
        var pdf = new FormPdf(SKTypeface.FromFile(Path.Combine(fontDir, "Manrope-Regular.ttf")), SKTypeface.FromFile(Path.Combine(fontDir, "Manrope-Bold.ttf")));

        var blank = pdf.Create(form, copy, "Μαρία");
        copy.SubmittedAt = DateTimeOffset.UtcNow;
        copy.Answers = new() { ["f1"] = "Μαρία Κωνσταντίνου", ["f2"] = "2004-03-14", ["f3"] = FormFields.Ticked, ["f4"] = "Διάστρεμμα αστραγάλου.\nΚανένα χειρουργείο." };
        var filled = pdf.Create(form, copy, "Μαρία");

        foreach (var bytes in new[] { blank, filled })
        {
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Contains("/Type /Page", Encoding.ASCII.GetString(bytes));
        }
        Assert.NotEqual(blank.Length, filled.Length);
        if (Environment.GetEnvironmentVariable("ATHLOTRACK_PDF_OUT") is { Length: > 0 } dir)
        {
            File.WriteAllBytes(Path.Combine(dir, "fields-blank.pdf"), blank);
            File.WriteAllBytes(Path.Combine(dir, "fields-filled.pdf"), filled);
        }
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
