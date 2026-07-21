using System.Text;
using System.Text.RegularExpressions;
using NLog;
using NLog.LayoutRenderers.Wrappers;

namespace Klemmbrett.Logging;

/// <summary>
/// Kroste-Standard: maskiert Secrets (Passwörter, Tokens, API-Keys, Connection-
/// String-Credentials) in allen Log-Ausgaben. Wird in nlog.config als
/// ${masked:inner=${message}} verwendet und muss vor der NLog-Konfiguration
/// registriert werden.
/// </summary>
[NLog.LayoutRenderers.LayoutRenderer("masked")]
public sealed partial class MaskedLayoutRenderer : WrapperLayoutRendererBase
{
    [GeneratedRegex(
        @"(?<key>password|passwort|pwd|pass|token|api[_-]?key|secret|authorization|bearer)(?<sep>\s*[=:]\s*)(?<value>[^\s;,""']+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex SecretPattern();

    public static void Register() =>
        LogManager.Setup().SetupExtensions(ext =>
            ext.RegisterLayoutRenderer<MaskedLayoutRenderer>("masked"));

    protected override string Transform(string text) => Mask(text);

    public static string Mask(string text) =>
        string.IsNullOrEmpty(text)
            ? text
            : SecretPattern().Replace(text, m => m.Groups["key"].Value + m.Groups["sep"].Value + "***");
}
