using System;
using System.Text.RegularExpressions;

namespace Klemmbrett.Models;

/// <summary>
/// Erkennt „offensichtliche" Passwörter/Secrets im Klartext, damit sie in der
/// Liste maskiert angezeigt werden (Schulter-Surf-Schutz). Bewusst konservativ:
/// lieber ein Passwort nicht erkennen als harmlosen Text fälschlich verbergen.
/// Der Volltext bleibt für das Zurückkopieren immer erhalten — die Maskierung
/// ist reine Anzeige, keine Verschlüsselung.
/// </summary>
public static partial class SecretDetector
{
    // Beschriftete Secrets: password=…, Passwort: …, token=…, api_key: …, Bearer …
    [GeneratedRegex(
        @"(password|passwort|passwd|pwd|kennwort|token|api[_-]?key|secret|bearer|authorization)\s*[=:]\s*\S{3,}",
        RegexOptions.IgnoreCase)]
    private static partial Regex LabeledSecret();

    /// <summary>True, wenn der Text wie ein Passwort/Secret aussieht.</summary>
    public static bool LooksLikeSecret(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();

        // 1) Beschriftete Secrets (auch mit umgebendem Kontext / mehrzeilig)
        if (LabeledSecret().IsMatch(trimmed)) return true;

        // 2) Alleinstehendes, passwortartiges Token
        return IsPasswordLikeToken(trimmed);
    }

    private static bool IsPasswordLikeToken(string s)
    {
        // Ein Passwort ist ein einzelnes Token ohne Leerraum in vernünftiger Länge
        if (s.Length < 8 || s.Length > 256) return false;
        foreach (var ch in s)
            if (char.IsWhiteSpace(ch)) return false;

        // URLs, E-Mails und www-Adressen sind harmlos und häufig — nicht verbergen
        if (s.Contains("://", StringComparison.Ordinal)) return false;
        if (s.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) return false;
        if (LooksLikeEmail(s)) return false;

        // Zeichenklassen zählen
        bool lower = false, upper = false, digit = false, symbol = false;
        foreach (var ch in s)
        {
            if (char.IsLower(ch)) lower = true;
            else if (char.IsUpper(ch)) upper = true;
            else if (char.IsDigit(ch)) digit = true;
            else if (!char.IsLetterOrDigit(ch)) symbol = true;
        }

        var classes = (lower ? 1 : 0) + (upper ? 1 : 0) + (digit ? 1 : 0) + (symbol ? 1 : 0);

        // Heuristik: mind. drei Zeichenklassen UND mindestens eine Ziffer.
        // Die Ziffer-Pflicht hält CamelCase-Namen, Wörter-mit-Bindestrich und
        // reine Hex-/Base64-Blöcke draußen (zu viele Fehlalarme sonst).
        return classes >= 3 && digit;
    }

    private static bool LooksLikeEmail(string s)
    {
        var at = s.IndexOf('@');
        return at > 0 && s.IndexOf('.', at) > at;
    }
}
