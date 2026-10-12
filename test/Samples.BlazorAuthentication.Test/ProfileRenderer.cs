// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;

using Samples.BlazorAuthentication.Components.Pages;

namespace Samples.BlazorAuthentication.Test;

// The render-tree APIs let these tests invoke real component callbacks without a browser or another test dependency.
#pragma warning disable BL0006
internal sealed class ProfileRenderer(IServiceProvider services, ILoggerFactory loggerFactory) : Renderer(services, loggerFactory)
{
    private int _root;
    private Manage? _editor;

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    internal ProfileInput Input => (ProfileInput)Form.Model!;
    internal IEnumerable<string> ValidationErrors => Form.EditContext!.GetValidationMessages();

    private EditForm Form => GetCurrentRenderTreeFrames(_root).Array
        .Take(GetCurrentRenderTreeFrames(_root).Count)
        .Where(frame => frame.FrameType == RenderTreeFrameType.Component)
        .Select(frame => frame.Component)
        .OfType<EditForm>()
        .Single();

    internal Task RenderAsync() => Dispatcher.InvokeAsync(async () =>
    {
        _editor = (Manage)InstantiateComponent(typeof(Manage));
        _root = AssignRootComponentId(_editor);
        await RenderRootComponentAsync(_root);
    });

    internal Task RestoreAsync(string cid, string name, string description, string pronouns) =>
        Dispatcher.InvokeAsync(() => _editor!.RestoreDraft(cid, name, description, pronouns));

    internal Task SubmitAsync() => Dispatcher.InvokeAsync(async () =>
    {
        if (Form.EditContext!.Validate())
        {
            await Form.OnValidSubmit.InvokeAsync(Form.EditContext);
        }

        await RenderRootComponentAsync(_root);
    });

    internal string Text => ReadText(_root);

    private string ReadText(int componentId)
    {
        StringBuilder text = new();
        var frames = GetCurrentRenderTreeFrames(componentId);
        for (int index = 0; index < frames.Count; index++)
        {
            var frame = frames.Array[index];
            switch (frame.FrameType)
            {
                case RenderTreeFrameType.Text:
                    text.Append(frame.TextContent);
                    break;
                case RenderTreeFrameType.Markup:
                    text.Append(frame.MarkupContent);
                    break;
                case RenderTreeFrameType.Component:
                    text.Append(ReadText(frame.ComponentId));
                    break;
            }
        }

        return text.ToString();
    }

    protected override void HandleException(Exception exception) => throw new InvalidOperationException("Component rendering failed.", exception);

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
}
#pragma warning restore BL0006
