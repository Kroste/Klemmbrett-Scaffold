using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Klemmbrett.ViewModels;

namespace Klemmbrett;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null) return null;
        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);
        return type is null
            ? new TextBlock { Text = "View nicht gefunden: " + name }
            : (Control)Activator.CreateInstance(type)!;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
