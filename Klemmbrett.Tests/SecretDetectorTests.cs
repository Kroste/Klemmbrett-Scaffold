using FluentAssertions;
using Klemmbrett.Models;
using Xunit;

namespace Klemmbrett.Tests;

public class SecretDetectorTests
{
    [Theory]
    // Beschriftete Secrets
    [InlineData("password=geheim123")]
    [InlineData("Passwort: Sommer2024!")]
    [InlineData("token = abc.def.ghi")]
    [InlineData("api_key: sk-12345")]
    [InlineData("Server=db1;User Id=app;Password=topsecret;")]
    // Alleinstehende passwortartige Tokens (mind. 3 Zeichenklassen inkl. Ziffer)
    [InlineData("xK9#mQ2$vL8p")]
    [InlineData("Sommer2024!")]
    [InlineData("P4ssw0rd-2025")]
    public void LooksLikeSecret_DetectsSecrets(string input)
        => SecretDetector.LooksLikeSecret(input).Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Hallo Welt, das ist ein Satz")]        // enthält Leerraum → kein Token
    [InlineData("https://example.com/pfad?x=1")]         // URL
    [InlineData("www.beispiel.de")]                      // Web-Adresse
    [InlineData("lars.kruegel@gmx.de")]                  // E-Mail
    [InlineData("Zwischenablage")]                       // ein Wort ohne Ziffer/Symbol
    [InlineData("CamelCaseName")]                        // zwei Klassen, keine Ziffer
    [InlineData("Kunden-Nummer")]                        // Buchstaben + Bindestrich, keine Ziffer
    [InlineData("abc123")]                               // zu kurz (< 8)
    public void LooksLikeSecret_LeavesHarmlessTextAlone(string input)
        => SecretDetector.LooksLikeSecret(input).Should().BeFalse();

    [Fact]
    public void TextEntry_MarksSecret_AndMasksDisplayUntilRevealed()
    {
        var secret = new TextClipboardEntry("xK9#mQ2$vL8p");
        secret.IsSecret.Should().BeTrue();
        secret.DisplayPreview.Should().NotContain("xK9");   // maskiert
        secret.Text.Should().Be("xK9#mQ2$vL8p");            // Volltext bleibt erhalten

        secret.IsRevealed = true;
        secret.DisplayPreview.Should().Be("xK9#mQ2$vL8p");  // aufgedeckt
    }

    [Fact]
    public void TextEntry_NormalText_IsNotMasked()
    {
        var entry = new TextClipboardEntry("ganz normaler Text");
        entry.IsSecret.Should().BeFalse();
        entry.DisplayPreview.Should().Be(entry.Preview);
    }
}
