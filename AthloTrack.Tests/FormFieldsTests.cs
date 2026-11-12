using AthloTrack.Core.Forms;

namespace AthloTrack.Tests;

public sealed class FormFieldsTests
{
    [Fact]
    public void Markers_read_in_order_with_kind_and_required()
    {
        var fields = FormFields.Parse("Όνομα: [[Κείμενο]]\nΓέννηση: [[Ημερομηνία]]\n[[Κουτάκι (προαιρετικό)]] Ναι\n[[Πολλές γραμμές]]");

        Assert.Equal([FormFieldKind.Text, FormFieldKind.Date, FormFieldKind.Tick, FormFieldKind.Long], fields.Select(f => f.Kind));
        Assert.Equal([true, true, false, true], fields.Select(f => f.Required));
        Assert.Equal(["f1", "f2", "f3", "f4"], fields.Select(f => f.Key));
    }

    [Fact]
    public void Word_blanks_become_fields()
    {
        var text = FormFields.FromBlanks("Ονοματεπώνυμο: ______\nΓέννηση: ___/___/______\nΦάρμακα; ☐ Ναι   ☐ Όχι\n**Υπογραφή: ____________**");

        Assert.Equal(
            "Ονοματεπώνυμο: [[Κείμενο]]\nΓέννηση: [[Ημερομηνία]]\nΦάρμακα; [[Κουτάκι (προαιρετικό)]] Ναι   [[Κουτάκι (προαιρετικό)]] Όχι\n**Υπογραφή: **[[Κείμενο]]",
            text);
        Assert.Equal(5, FormFields.Parse(text).Count);
    }

    [Fact]
    public void Text_without_blanks_is_unchanged()
    {
        Assert.Equal("Απλό κείμενο, χωρίς κενά. __ δύο παύλες δεν είναι κενό.", FormFields.FromBlanks("Απλό κείμενο, χωρίς κενά. __ δύο παύλες δεν είναι κενό."));
        Assert.False(FormFields.HasFields("[[κάτι άλλο]] και [Κείμενο]"));
    }

    [Fact]
    public void A_field_is_changed_or_removed_by_its_number()
    {
        const string text = "Α: [[Κείμενο]] Β: [[Κείμενο]] τέλος";

        Assert.Equal("Α: [[Κείμενο]] Β: [[Ημερομηνία (προαιρετικό)]] τέλος",
            FormFields.Replace(text, 1, FormFields.MarkerFor(FormFieldKind.Date, false)));
        Assert.Equal("Α: Β: [[Κείμενο]] τέλος", FormFields.Replace(text, 0, string.Empty));
        Assert.Equal(text, FormFields.Replace(text, 5, string.Empty));
    }

    [Fact]
    public void Split_numbers_fields_across_lines_like_parse()
    {
        var next = 0;
        var first = FormFields.Split("Α: [[Κείμενο]] και [[Κουτάκι]]", ref next);
        var second = FormFields.Split("[[Ημερομηνία]] τέλος", ref next);

        Assert.Equal(["Α: ", "", " και ", ""], first.Select(p => p.Text));
        Assert.Equal([0, 1], first.Where(p => p.Field is not null).Select(p => p.Field!.Index));
        Assert.Equal(2, second[0].Field!.Index);
        Assert.Equal(" τέλος", second[1].Text);
    }

    [Fact]
    public void Missing_counts_only_required_empty_fields()
    {
        const string text = "[[Κείμενο]] [[Κείμενο (προαιρετικό)]] [[Κουτάκι]] [[Ημερομηνία]]";
        var missing = FormFields.Missing(text, new Dictionary<string, string> { ["f1"] = "Μαρία", ["f3"] = "  " });

        Assert.Equal(["f3", "f4"], missing.Select(f => f.Key));
    }

    [Fact]
    public void Answers_read_as_in_the_document()
    {
        Assert.Equal("14/03/2004", FormFields.Display(new FormField(0, FormFieldKind.Date, true), "2004-03-14"));
        Assert.Equal("☒", FormFields.Display(new FormField(0, FormFieldKind.Tick, false), FormFields.Ticked));
        Assert.Equal("", FormFields.Display(new FormField(0, FormFieldKind.Text, true), "  "));
    }
}
