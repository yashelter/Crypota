using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AvaloniaClient.Models;
using StainsGate;

namespace AvaloniaClient.Repositories;

public class MessageTemplateSelector : IDataTemplate
{
    public IDataTemplate TextTemplate { get; set; } = null!;
    public IDataTemplate FileTemplate { get; set; } = null!;
    public IDataTemplate ImageTemplate { get; set; } = null!;
    

    public bool Match(object? data) => data is ChatMessageModel;

    public Control? Build(object? data)
    {
        if (data is not ChatMessageModel msg)
            return null;

        var template = msg.MessageType switch
        {
            MessageType.Message => TextTemplate,
            MessageType.File    => FileTemplate,
            MessageType.Image   => ImageTemplate,
            _                    => TextTemplate
        };
        return template.Build(data);
    }
}
