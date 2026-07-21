using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Klemmbrett.Views;

/// <summary>
/// Custom-Chrome nach Avalonia-12-Konvention (Kroste-Standard, Referenz: Amtsschimmel):
/// BorderOnly (NICHT None — sonst fehlen die nativen Resize-Griffe) und Client-Area
/// bis in die Dekoration ausgedehnt. Ohne ExtendClientArea liegt die OS-Caption-
/// Hit-Test-Zone über der eigenen Titelleiste und schluckt Klicks und Drag!
/// </summary>
public class ChromeWindow : Window
{
    protected ChromeWindow()
    {
        WindowDecorations = WindowDecorations.BorderOnly;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = -1;
        CanResize = true;
        try
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Klemmbrett/Assets/Klemmbrett.png")));
        }
        catch
        {
            // Ohne Icon lauffaehig bleiben (z.B. falls Asset fehlt)
        }
    }
}
