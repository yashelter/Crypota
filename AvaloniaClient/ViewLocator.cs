using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AvaloniaClient.ViewModels;
using Serilog;

namespace AvaloniaClient;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            try
            {
                return (Control)Activator.CreateInstance(type)!; 
            }
            catch
            {
                Log.Warning("Error creating view {Type} [In some cases it's normal]", type);
                return new TextBlock { Text = $"Error creating view for {type}" };
            }
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}