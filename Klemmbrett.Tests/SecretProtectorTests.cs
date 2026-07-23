using System;
using System.IO;
using FluentAssertions;
using Klemmbrett.Services;
using Xunit;

namespace Klemmbrett.Tests;

public class SecretProtectorTests : IDisposable
{
    private readonly string _keyFile = Path.Combine(Path.GetTempPath(), "kb-protect-" + Guid.NewGuid() + ".key");

    public void Dispose()
    {
        try { File.Delete(_keyFile); } catch { /* best effort */ }
    }

    [Fact]
    public void ProtectAndUnprotect_Roundtrip()
    {
        var sut = new SecretProtector(_keyFile);
        var token = sut.Protect("supergeheim!");
        token.Should().StartWith("ENC1:");
        sut.IsProtected(token).Should().BeTrue();
        sut.Unprotect(token).Should().Be("supergeheim!");
    }

    [Fact]
    public void Protect_ProducesDifferentCiphertextsForSamePlaintext()
    {
        // Nonce muss pro Aufruf frisch sein → identischer Klartext ergibt unterschiedliche Chiffrate.
        // Nur unter der AES-GCM-Variante relevant, unter DPAPI durch die interne Randomisierung ebenso.
        var sut = new SecretProtector(_keyFile);
        var a = sut.Protect("abc");
        var b = sut.Protect("abc");
        a.Should().NotBe(b);
    }

    [Fact]
    public void IsProtected_False_ForPlainStrings()
    {
        var sut = new SecretProtector(_keyFile);
        sut.IsProtected(null).Should().BeFalse();
        sut.IsProtected("").Should().BeFalse();
        sut.IsProtected("normaler Text").Should().BeFalse();
    }

    [Fact]
    public void Unprotect_ThrowsOnMalformedToken()
    {
        var sut = new SecretProtector(_keyFile);
        var act = () => sut.Unprotect("kein-encoding");
        act.Should().Throw<FormatException>();
    }
}
