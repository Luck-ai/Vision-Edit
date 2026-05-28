using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionEditCV.Shared.Helpers;
using VisionEditCV.Shared.Models;

namespace VisionEditCV.Shared.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _editor;
    private readonly ProjectStore _store = new();
    private readonly ObservableCollection<ProjectItem> _allProjects = new();

    /// <summary>Projects visible after sidebar/filter/search are applied. Always includes the "Open new image" tile first.</summary>
    public ObservableCollection<ProjectItem> Projects { get; } = new();

    /// <summary>4 most-recently-edited real projects (no "new" tile).</summary>
    public ObservableCollection<ProjectItem> RecentProjects { get; } = new();

    [ObservableProperty] private string _searchQuery = "";

    /// <summary>"All" | "InProgress" | "Exported" | "Pinned"</summary>
    [ObservableProperty] private string _activeFilter = "All";

    public bool IsAllFilter => ActiveFilter == "All";
    public bool IsInProgressFilter => ActiveFilter == "InProgress";
    public bool IsExportedFilter => ActiveFilter == "Exported";
    public bool IsPinnedFilter => ActiveFilter == "Pinned";

    public int AllCount => _allProjects.Count;
    public int InProgressCount => _allProjects.Count(p => p.TagKind == ProjectTagKind.InProgress);
    public int ExportedCount => _allProjects.Count(p => p.IsExported);
    public int PinnedCount => _allProjects.Count(p => p.IsPinned);

    public int RealProjectCount => _allProjects.Count;
    public string ProjectCountText => RealProjectCount == 0
        ? "Local · 0 projects"
        : $"Local · {RealProjectCount} project{(RealProjectCount == 1 ? "" : "s")}";
    public string ProjectCountSummary => $"{Projects.Count - 1} item{(Projects.Count - 1 == 1 ? "" : "s")}";
    public bool HasRecentProjects => RecentProjects.Count > 0;

    public event EventHandler? ProjectOpened;

    public LibraryViewModel(MainWindowViewModel editor)
    {
        _editor = editor;
        foreach (var p in _store.Load())
            _allProjects.Add(p);

        // When a new image is loaded in the editor, record it as a project.
        _editor.PropertyChanged += OnEditorPropertyChanged;
        _editor.ProjectExported += OnEditorProjectExported;
        _editor.MaskItems.CollectionChanged += (_, _) => UpdateMaskCount();

        RebuildProjects();
        RebuildRecent();
    }

    private void OnEditorProjectExported(object? sender, EventArgs e)
    {
        var path = _editor.CurrentImagePath;
        if (string.IsNullOrEmpty(path)) return;
        var item = _allProjects.FirstOrDefault(p =>
            string.Equals(p.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;
        item.IsExported = true;
        item.LastEditedUtc = DateTimeOffset.UtcNow;
        _store.Save(_allProjects);
        NotifyCounts();
        RebuildProjects();
        RebuildRecent();
    }

    private void UpdateMaskCount()
    {
        var path = _editor.CurrentImagePath;
        if (string.IsNullOrEmpty(path)) return;
        var item = _allProjects.FirstOrDefault(p =>
            string.Equals(p.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;
        item.MaskCount = _editor.MaskItems.Count;
        _store.Save(_allProjects);
        NotifyCounts();
    }

    partial void OnSearchQueryChanged(string value) => RebuildProjects();

    partial void OnActiveFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsAllFilter));
        OnPropertyChanged(nameof(IsInProgressFilter));
        OnPropertyChanged(nameof(IsExportedFilter));
        OnPropertyChanged(nameof(IsPinnedFilter));
        RebuildProjects();
    }

    [RelayCommand]
    private void SelectFilter(string? filter) => ActiveFilter = filter ?? "All";

    [RelayCommand]
    private async Task NewProject(object? window)
    {
        await _editor.OpenImageCommand.ExecuteAsync(window);
        // The PropertyChanged hook on CurrentImagePath records the project; just flip view.
        if (_editor.HasImage)
            ProjectOpened?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task OpenProject(object? param)
    {
        // Cards pass the Button (a Visual) as parameter; the project record is on its DataContext.
        var item = (param as Avalonia.Controls.Control)?.DataContext as ProjectItem
                   ?? param as ProjectItem;

        if (item is not null && !item.IsNewProject)
        {
            if (!File.Exists(item.FilePath))
            {
                _allProjects.Remove(item);
                _store.Save(_allProjects);
                RebuildProjects();
                RebuildRecent();
                return;
            }
            _editor.OpenImageFromPathCommand.Execute(item.FilePath);
            if (_editor.HasImage)
                ProjectOpened?.Invoke(this, EventArgs.Empty);
            return;
        }

        // "Open new image" tile or a raw visual — invoke the picker with a Visual.
        await _editor.OpenImageCommand.ExecuteAsync(param);
        if (_editor.HasImage)
            ProjectOpened?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void TogglePin(ProjectItem? item)
    {
        if (item is null || item.IsNewProject) return;
        item.IsPinned = !item.IsPinned;
        _store.Save(_allProjects);
        NotifyCounts();
        RebuildProjects();
    }

    [RelayCommand]
    private void DeleteProject(ProjectItem? item)
    {
        if (item is null || item.IsNewProject) return;
        _allProjects.Remove(item);
        _store.Save(_allProjects);
        NotifyCounts();
        RebuildProjects();
        RebuildRecent();
    }

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.CurrentImagePath)) return;
        var path = _editor.CurrentImagePath;
        if (string.IsNullOrEmpty(path)) return;
        RecordProjectOpen(path);
    }

    private void RecordProjectOpen(string path)
    {
        var existing = _allProjects.FirstOrDefault(p => string.Equals(p.FilePath, path, StringComparison.OrdinalIgnoreCase));
        var fileInfo = new FileInfo(path);
        var img = _editor.CurrentImage;
        int width = (int)(img?.PixelSize.Width ?? 0);
        int height = (int)(img?.PixelSize.Height ?? 0);

        if (existing is not null)
        {
            existing.LastEditedUtc = DateTimeOffset.UtcNow;
            existing.FileSizeBytes = fileInfo.Length;
            existing.WidthPx = width;
            existing.HeightPx = height;
        }
        else
        {
            _allProjects.Insert(0, new ProjectItem
            {
                Name = Path.GetFileNameWithoutExtension(path),
                FilePath = path,
                LastEditedUtc = DateTimeOffset.UtcNow,
                FileSizeBytes = fileInfo.Length,
                WidthPx = width,
                HeightPx = height,
            });
        }
        _store.Save(_allProjects);
        NotifyCounts();
        RebuildProjects();
        RebuildRecent();
    }

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(AllCount));
        OnPropertyChanged(nameof(InProgressCount));
        OnPropertyChanged(nameof(ExportedCount));
        OnPropertyChanged(nameof(PinnedCount));
        OnPropertyChanged(nameof(RealProjectCount));
        OnPropertyChanged(nameof(ProjectCountText));
        OnPropertyChanged(nameof(ProjectCountSummary));
    }

    private void RebuildProjects()
    {
        Projects.Clear();

        // Always-first "Open new image" tile.
        Projects.Add(new ProjectItem { Name = "Open new image", IsNewProject = true });

        IEnumerable<ProjectItem> q = _allProjects;
        q = ActiveFilter switch
        {
            "InProgress" => q.Where(p => p.TagKind == ProjectTagKind.InProgress),
            "Exported" => q.Where(p => p.IsExported),
            "Pinned" => q.Where(p => p.IsPinned),
            _ => q,
        };
        if (!string.IsNullOrWhiteSpace(SearchQuery))
            q = q.Where(p => p.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

        foreach (var p in q.OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.LastEditedUtc))
            Projects.Add(p);

        OnPropertyChanged(nameof(ProjectCountSummary));
    }

    private void RebuildRecent()
    {
        RecentProjects.Clear();
        foreach (var p in _allProjects.OrderByDescending(p => p.LastEditedUtc).Take(4))
            RecentProjects.Add(p);
        OnPropertyChanged(nameof(HasRecentProjects));
    }
}
