using AthloTrack.Core.Text;

namespace AthloTrack.Tests;

public sealed class GreekVocativeTests
{
    [Theory]
    // -ος: two syllables, or three stressed on the next-to-last → -ο
    [InlineData("Γιώργος", "Γιώργο")]
    [InlineData("Νίκος", "Νίκο")]
    [InlineData("Πέτρος", "Πέτρο")]
    [InlineData("Στέλιος", "Στέλιο")]
    [InlineData("Αλέκος", "Αλέκο")]
    [InlineData("Τάσος", "Τάσο")]
    // other -ος → -ε
    [InlineData("Παπαδόπουλος", "Παπαδόπουλε")]
    [InlineData("Θεόδωρος", "Θεόδωρε")]
    [InlineData("Νικόλαος", "Νικόλαε")]
    [InlineData("Δημήτριος", "Δημήτριε")]
    [InlineData("Αντώνιος", "Αντώνιε")]
    [InlineData("Κωνσταντίνος", "Κωνσταντίνε")]
    [InlineData("Αλέξανδρος", "Αλέξανδρε")]
    [InlineData("Δοκιμαστικός", "Δοκιμαστικέ")]
    // -ης, -ας
    [InlineData("Γιάννης", "Γιάννη")]
    [InlineData("Μιχάλης", "Μιχάλη")]
    [InlineData("Κώστας", "Κώστα")]
    [InlineData("Λουκάς", "Λουκά")]
    [InlineData("Ηλίας", "Ηλία")]
    [InlineData("Προπονητής", "Προπονητή")]
    // unchanged: women's names, Latin letters, initials
    [InlineData("Μαρία", "Μαρία")]
    [InlineData("Ελένη", "Ελένη")]
    [InlineData("Νεφέλη", "Νεφέλη")]
    [InlineData("John", "John")]
    public void A_name_takes_the_vocative(string name, string vocative)
    {
        Assert.Equal(vocative, GreekVocative.Of(name));
    }

    [Theory]
    [InlineData("Γιώργος Παπαδόπουλος", "Γιώργο Παπαδόπουλε")]
    [InlineData("Δοκιμαστικός Προπονητής", "Δοκιμαστικέ Προπονητή")]
    [InlineData("Μαρία Κωνσταντίνου", "Μαρία Κωνσταντίνου")]
    [InlineData("Γιάννης Κ.", "Γιάννη Κ.")]
    [InlineData("ΓΙΩΡΓΟΣ ΠΑΠΑΔΟΠΟΥΛΟΣ", "ΓΙΩΡΓΟ ΠΑΠΑΔΟΠΟΥΛΕ")]
    [InlineData("  ", "")]
    public void Every_word_of_the_full_name_changes(string name, string vocative)
    {
        Assert.Equal(vocative, GreekVocative.Of(name));
    }
}
