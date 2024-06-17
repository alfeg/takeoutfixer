using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Spectre.Console;
using Spectre.Console.Cli;
using Directory = System.IO.Directory;
using ValidationResult = Spectre.Console.ValidationResult;

namespace TakeoutFixer;

internal sealed class FixupCommand : Command<FixupCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Directory with Google Takout Data")]
        [CommandOption("--dir")]
        [Required]
        public string TakeOutDirectory { get; init; }

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
            .Start(ctx =>
            {
                var task = ctx.AddTask("Reading files metadata", maxValue: totalFiles);

                foreach (var file in Directory.EnumerateFiles(settings.TakeOutDirectory, "*.*",
                             SearchOption.AllDirectories))
                {
                    if (Path.GetExtension(file) == ".json")
                    {
                        continue;
                    }

                    if (TryUsingExif(file, out var dateTime) || TryUsingJsonMetadata(file, out dateTime))
                    {
                        File.SetLastWriteTimeUtc(file, dateTime.ToUniversalTime());
                    }

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