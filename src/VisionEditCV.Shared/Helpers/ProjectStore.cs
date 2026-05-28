using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VisionEditCV.Shared.Models;

namespace VisionEditCV.Shared.Helpers;

/// <summary>JSON-backed project store at LocalAppData/VisionEditCV/projects.json.</summary>
public class ProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _path;

    public ProjectStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VisionEditCV");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "projects.json");
    }

    public IReadOnlyList<ProjectItem> Load()
    {
        if (!File.Exists(_path)) return Array.Empty<ProjectItem>();
        try
        {
            using var stream = File.OpenRead(_path);
            var items = JsonSerializer.Deserialize<List<ProjectEntry>>(stream, JsonOptions);
            if (items is null) return Array.Empty<ProjectItem>();
            return items.Select(ToItem).Where(p => !string.IsNullOrEmpty(p.FilePath)).ToList();
        }
        catch
        {
            return Array.Empty<ProjectItem>();
        }
    }

    public void Save(IEnumerable<ProjectItem> projects)
    {
        try
        {
            var entries = projects
                .Where(p => !p.IsNewProject && !string.IsNullOrEmpty(p.FilePath))
                .Select(ToEntry)
                .ToList();
            using var stream = File.Create(_path);
            JsonSerializer.Serialize(stream, entries, JsonOptions);
        }
        catch
        {
            // Best-effort. Loss of recents is recoverable; don't crash on disk errors.
        }
    }

    private static ProjectItem ToItem(ProjectEntry e) => new()
    {
        Name = e.Name,
        FilePath = e.FilePath,
        LastEditedUtc = e.LastEditedUtc,
        IsPinned = e.IsPinned,
        IsExported = e.IsExported,
        MaskCount = e.MaskCount,
        FileSizeBytes = e.FileSizeBytes,
        WidthPx = e.WidthPx,
        HeightPx = e.HeightPx,
    };

    private static ProjectEntry ToEntry(ProjectItem p) => new()
    {
        Name = p.Name,
        FilePath = p.FilePath,
        LastEditedUtc = p.LastEditedUtc,
        IsPinned = p.IsPinned,
        IsExported = p.IsExported,
        MaskCount = p.MaskCount,
        FileSizeBytes = p.FileSizeBytes,
        WidthPx = p.WidthPx,
        HeightPx = p.HeightPx,
    };

    private sealed class ProjectEntry
    {
        public string Name { get; set; } = "";
        public string FilePath { get; set; } = "";
        public DateTimeOffset LastEditedUtc { get; set; }
        public bool IsPinned { get; set; }
        public bool IsExported { get; set; }
        public int MaskCount { get; set; }
        public long FileSizeBytes { get; set; }
        public int WidthPx { get; set; }
        public int HeightPx { get; set; }
    }
}
