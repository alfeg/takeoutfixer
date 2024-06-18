// See https://aka.ms/new-console-template for more information

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Spectre.Console;
using Spectre.Console.Cli;
using TakeoutFixer;
using Directory = System.IO.Directory;
using ValidationResult = Spectre.Console.ValidationResult;

var app = new CommandApp<FixupCommand>()
    .WithDescription("Fix files timestamps according to EXIF or Google json metadata");
app.Configure(c =>
{
    c.Settings.ApplicationName = "takoutfixer";
});
await app.RunAsync(args);

internal sealed class FixupCommand : Command<FixupCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Directory with Google Takeout Data")]
        [CommandOption("--dir")]
        public required string TakeOutDirectory { get; init; }

        [Description("Dry run - output affected files list, don't change anything")]
        [CommandOption("--dry")]
        [DefaultValue(false)]
        public bool DryRun { get; init; }

        [Description("Skip EXIF data, only use json metadata.")]
        [CommandOption("--skip-exif|-s")]
        [DefaultValue(false)]
        public bool SkipExif { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrEmpty(TakeOutDirectory))
            {
                return ValidationResult.Error("Please specify directory: --dir <directory>");
            }

            return Directory.Exists(TakeOutDirectory)
                ? ValidationResult.Success()
                : ValidationResult.Error($"Directory not exists: {TakeOutDirectory}");
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var totalFiles = CountFiles(settings);
        UpdateFileTimestamps(settings, totalFiles);
        return 0;
    }

    private long CountFiles(Settings settings)
    {
        long count = 0;
        AnsiConsole.Status()
            .Start("Counting files", _ =>
            {
                foreach (var file in Directory.EnumerateFiles(settings.TakeOutDirectory, "*.*",
                             SearchOption.AllDirectories))
                {
                    if (Path.GetExtension(file) == ".json")
                    {
                        continue;
                    }

                    count++;
                }
            });
        return count;
    }

    private void UpdateFileTimestamps(Settings settings, long totalFiles) =>
        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns([
                new ElapsedTimeColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new ItemsProgessSpeedColumn(),
                new SpinnerColumn(),
                new TaskDescriptionColumn(),
            ])
            .Start(ctx =>
            {
                var task = ctx.AddTask("Writing:", maxValue: totalFiles);

                foreach (var file in Directory.EnumerateFiles(settings.TakeOutDirectory, "*.*",
                             SearchOption.AllDirectories))
                {
                    if (Path.GetExtension(file) == ".json")
                    {
                        continue;
                    }

                    if ((!settings.SkipExif && TryUsingExif(file, out var dateTime)) ||
                        TryUsingJsonMetadata(file, out dateTime))
                    {
                        try
                        {
                            if (settings.DryRun)
                            {
                                AnsiConsole.MarkupLineInterpolated(
                                    $"[green][[DRY]][/] - [yellow]{dateTime.ToString("g")}[/] {file.Substring(settings.TakeOutDirectory.Length + 1)}");
                            }
                            else
                            {
                                File.SetLastWriteTimeUtc(file, dateTime.ToUniversalTime());
                            }
                        }
                        catch (Exception e)
                        {
                            AnsiConsole.MarkupInterpolated($"[red][[SKIP]][/] {file} - {e.Message}");
                        }
                    }

                    task.Description("Writing: " + file.Substring(settings.TakeOutDirectory.Length + 1));
                    task.Increment(1);
                }
            });

    bool TryUsingExif(string file, out DateTime dateTime)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(file);
            var info = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (info != null)
            {
                if (info.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out dateTime))
                {
                    return true;
                }
            }
        }
        catch
        {
            dateTime = default;
            return false;
        }

        dateTime = default;
        return false;
    }

    bool TryUsingJsonMetadata(string file, out DateTime dateTime)
    {
        var json = file + ".json";
        if (File.Exists(json))
        {
            var meta = ReadJsonMetadata(json);
            if (meta != null)
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(meta.photoTakenTime.timestamp));
                dateTime = date.UtcDateTime;
                return true;
            }
        }

        dateTime = default;
        return false;
    }

    static FileMetaData? ReadJsonMetadata(string file)
    {
        using var fs = File.OpenRead(file);
        return JsonSerializer.Deserialize<FileMetaData>(fs);
    }
}