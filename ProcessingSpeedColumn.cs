using Spectre.Console;
using Spectre.Console.Rendering;

/// <summary>
/// A column showing processing speed.
/// </summary>
public sealed class ProcessingSpeedColumn : ProgressColumn
{
    /// <inheritdoc/>
    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
    {
        if (task.Speed == null)
        {
            return new Text("?/s");
        }
        return new Markup($"{task.Speed:0} guesses/s");
    }
}
