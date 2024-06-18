using Spectre.Console;
using Spectre.Console.Rendering;

namespace TakeoutFixer;

public class FileMetaData
{
    public required CreationTime creationTime { get; set; }
    public required CreationTime photoTakenTime { get; set; }
}

public class CreationTime
{
    public required string timestamp { get; set; }
    public required string formatted { get; set; }
}

/// <summary>
/// A column showing transfer speed.
/// </summary>
public sealed class ItemsProgessSpeedColumn : ProgressColumn
{
    /// <inheritdoc/>
    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
    {
        if (task.Speed == null)
        {
            return new Text("?/s");
        }
        
        return new Markup(string.Format("{0:F} f/s", task.Speed));
    }
}