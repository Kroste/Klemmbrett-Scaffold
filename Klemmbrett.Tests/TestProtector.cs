using System;
using System.Text;
using Klemmbrett.Services;

namespace Klemmbrett.Tests;

/// <summary>
/// Deterministischer Fake-Protector für Storage-Tests: reversibel (Base64) und ohne
/// echte Krypto — Tests laufen so auf allen Plattformen. Reicht, um zu prüfen, dass
/// der Klartext NICHT mehr im JSON steht und dass die Roundtrip-Semantik stimmt.
/// </summary>
internal sealed class TestProtector : ISecretProtector
{
    private const string Prefix = "ENC1:";

    public bool IsProtected(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

    public string Protect(string plaintext) =>
        Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));

    public string Unprotect(string token) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(token[Prefix.Length..]));
}
