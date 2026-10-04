using AthloTrack.Core.Workouts;

namespace AthloTrack.Tests;

public class WorkoutMarkupTests
{
    [Fact]
    public void Old_plain_text_stays_as_typed()
    {
        var lines = WorkoutMarkup.Parse("2x10 καμψεις\nμαραθωνιο κατερινη- αθηνα");

        Assert.All(lines, l => Assert.Equal(MarkupKind.Text, l.Kind));
        Assert.Equal("μαραθωνιο κατερινη- αθηνα", Assert.Single(lines[1].Spans).Text);
    }

    [Fact]
    public void Headings_lists_and_bold_are_recognised()
    {
        var lines = WorkoutMarkup.Parse("## Ζέσταμα\r\n- 10' τρέξιμο\n• διατάσεις\n\n2. **Καθίσματα** 3×10\nτέλος **ανοιχτό");

        Assert.Equal(
            [MarkupKind.Heading, MarkupKind.Bullet, MarkupKind.Bullet, MarkupKind.Blank, MarkupKind.Numbered, MarkupKind.Text],
            lines.Select(l => l.Kind));
        Assert.Equal("Ζέσταμα", lines[0].Spans[0].Text);
        Assert.Equal(2, lines[4].Number);
        Assert.Equal([("Καθίσματα", true), (" 3×10", false)], lines[4].Spans.Select(s => (s.Text, s.Bold)));
        // An unclosed ** is just text.
        Assert.Equal("τέλος **ανοιχτό", Assert.Single(lines[5].Spans).Text);
    }

    [Fact]
    public void Plain_preview_strips_the_markup()
    {
        Assert.Equal("Ζέσταμα\n• τρέξιμο\n1. Καθίσματα", WorkoutMarkup.PlainPreview("## Ζέσταμα\n- τρέξιμο\n\n1. **Καθίσματα**"));
    }

    [Fact]
    public void Bold_wraps_and_unwraps_the_selection()
    {
        var wrapped = WorkoutMarkup.ToggleBold("3x10 καθίσματα", 5, 14);
        Assert.Equal(new TextEdit("3x10 **καθίσματα**", 7, 16), wrapped);

        Assert.Equal("3x10 καθίσματα", WorkoutMarkup.ToggleBold(wrapped.Text, wrapped.SelectionStart, wrapped.SelectionEnd).Text);
        Assert.Equal("3x10 καθίσματα", WorkoutMarkup.ToggleBold(wrapped.Text, 5, 18).Text);
    }

    [Fact]
    public void Bold_without_a_selection_puts_the_caret_between_the_markers()
    {
        Assert.Equal(new TextEdit("ab****", 4, 4), WorkoutMarkup.ToggleBold("ab", 2, 2));
    }

    [Fact]
    public void Numbering_selected_lines_counts_them_and_toggles_back()
    {
        const string text = "τρέξιμο\nκαθίσματα\n- πους απς";
        var numbered = WorkoutMarkup.ToggleLinePrefix(text, 0, text.Length, MarkupKind.Numbered);
        Assert.Equal("1. τρέξιμο\n2. καθίσματα\n3. πους απς", numbered.Text);

        var back = WorkoutMarkup.ToggleLinePrefix(numbered.Text, 0, numbered.Text.Length, MarkupKind.Numbered);
        Assert.Equal("τρέξιμο\nκαθίσματα\nπους απς", back.Text);
    }

    [Fact]
    public void A_prefix_applies_to_the_caret_line_only()
    {
        var edit = WorkoutMarkup.ToggleLinePrefix("α\nβ\nγ", 2, 2, MarkupKind.Heading);
        Assert.Equal("α\n## β\nγ", edit.Text);
        Assert.Equal(6, edit.SelectionStart);
    }

    [Fact]
    public void Enter_after_a_list_item_starts_the_next_one()
    {
        Assert.Equal(new TextEdit("- α\n- ", 6, 6), WorkoutMarkup.ContinueList("- α", "- α\n", 4));
        Assert.Equal("3. α\n4. ", WorkoutMarkup.ContinueList("3. α", "3. α\n", 5)!.Value.Text);
        // Windows line breaks
        Assert.Equal("- α\r\n- ", WorkoutMarkup.ContinueList("- α", "- α\r\n", 5)!.Value.Text);
    }

    [Fact]
    public void Enter_on_an_empty_item_ends_the_list()
    {
        Assert.Equal(new TextEdit("- α\n", 4, 4), WorkoutMarkup.ContinueList("- α\n- ", "- α\n- \n", 7));
    }

    [Fact]
    public void Other_edits_are_left_alone()
    {
        Assert.Null(WorkoutMarkup.ContinueList("απλό", "απλό\n", 5));
        Assert.Null(WorkoutMarkup.ContinueList("- α", "- αβ", 4));
        Assert.Null(WorkoutMarkup.ContinueList("", "- α\n", 4)); // pasted
    }

    [Fact]
    public void Exercises_are_formatted_with_only_the_given_fields()
    {
        Assert.Equal("- **Καθίσματα** — 3×10 @ 62,5kg, διάλ. 90\"",
            WorkoutMarkup.FormatExercise(" Καθίσματα ", 3, 10, 62.5m, null, null, 90));
        Assert.Equal("- **Τρέξιμο** — 20' / 5km", WorkoutMarkup.FormatExercise("Τρέξιμο", null, null, null, 20, "5km", null));
        Assert.Equal("- **Σανίδα**", WorkoutMarkup.FormatExercise("Σανίδα", null, null, null, null, " ", null));
        Assert.Equal("- **Σανίδα** — 4 σετ, διάλ. 30\"", WorkoutMarkup.FormatExercise("Σανίδα", 4, null, null, null, null, 30));
    }
}
