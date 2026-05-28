using System;
using System.IO;
using Avalonia.Media;

namespace VisionEditCV.Shared.Models;

public enum ProjectTagKind
{
    None,
    Start,
    InProgress,
    Exported,
    Draft
}

public class ProjectItem
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
    public bool IsNewProject { get; set; }

    public ProjectTagKind TagKind => IsNewProject
        ? ProjectTagKind.Start
        : IsExported
            ? ProjectTagKind.Exported
            : MaskCount > 0
                ? ProjectTagKind.InProgress
                : ProjectTagKind.Draft;

    public string TagText => TagKind switch
    {
        ProjectTagKind.Start => "Start",
        ProjectTagKind.InProgress => "In progress",
        ProjectTagKind.Exported => "Exported",
        ProjectTagKind.Draft => "Draft",
        _ => ""
    };

    public bool ShowStartTag => TagKind == ProjectTagKind.Start;
    public bool ShowInProgressTag => TagKind == ProjectTagKind.InProgress;
    public bool ShowExportedTag => TagKind == ProjectTagKind.Exported;
    public bool ShowDraftTag => TagKind == ProjectTagKind.Draft;

    public string DimensionsText => WidthPx > 0 && HeightPx > 0 ? $"{WidthPx}×{HeightPx}" : "";

    public string MetaText
    {
        get
        {
            if (IsNewProject) return "Photos · Files · Drag & drop";
            var parts = new System.Collections.Generic.List<string>();
            if (FileSizeBytes > 0) parts.Add(FormatBytes(FileSizeBytes));
            if (MaskCount > 0) parts.Add(MaskCount == 1 ? "1 mask" : $"{MaskCount} masks");
            parts.Add(FormatRelativeTime(LastEditedUtc));
            return string.Join(" · ", parts);
        }
    }

    public string LastEditedText => IsNewProject ? "" : FormatRelativeTime(LastEditedUtc).ToUpperInvariant();

    public IBrush ThumbBrush
    {
        get
        {
            var (a, b) = GradientFromKey(IsNewProject ? "__new__" : FilePath);
            return new LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(a, 0),
                    new GradientStop(b, 1)
                }
            };
        }
    }

    private static (Color start, Color end) GradientFromKey(string key)
    {
        if (key == "__new__")
            return (Color.FromArgb(0x24, 0x22, 0xD3, 0xEE), Color.FromArgb(0x14, 0x22, 0xD3, 0xEE));

        // Deterministic palette pick from name hash.
        var palettes = new[]
        {
            (0x6F8AB8u, 0x2A3553u),
            (0xB8946Fu, 0x533C2Au),
            (0x6FB89Cu, 0x2A5345u),
            (0x9C6FB8u, 0x452A53u),
            (0xB86F8Au, 0x532A3Du),
            (0x4D6FB8u, 0x1F2D53u),
            (0xB8B86Fu, 0x535345u),
            (0x6FB8B8u, 0x2A5353u),
            (0x8A6FB8u, 0x3D2A53u),
            (0x6FB87Au, 0x2A5333u),
            (0xB86F6Fu, 0x532A2Au),
        };
        var idx = Math.Abs(StringHash(key)) % palettes.Length;
        var (s, e) = palettes[idx];
        return (Color.FromRgb((byte)(s >> 16), (byte)(s >> 8), (byte)s),
                Color.FromRgb((byte)(e >> 16), (byte)(e >> 8), (byte)e));
    }

    private static int StringHash(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (var c in s) h = h * 31 + c;
            return h;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000d:0.#} GB";
        if (bytes >= 1_000_000) return $"{bytes / 1_000_000d:0.#} MB";
        if (bytes >= 1_000) return $"{bytes / 1_000d:0.#} KB";
        return $"{bytes} B";
    }

    private static string FormatRelativeTime(DateTimeOffset when)
    {
        var now = DateTimeOffset.UtcNow;
        var delta = now - when;
        if (delta.TotalSeconds < 60) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} min ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} hr ago";
        if (delta.TotalDays < 2) return "yesterday";
        if (delta.TotalDays < 7) return $"{(int)delta.TotalDays} days ago";
        if (delta.TotalDays < 14) return "last week";
        if (delta.TotalDays < 60) return $"{(int)(delta.TotalDays / 7)} weeks ago";
        return when.ToString("MMM d, yyyy");
    }
}
