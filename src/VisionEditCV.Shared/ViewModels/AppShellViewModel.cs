using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisionEditCV.Shared.ViewModels;

public partial class AppShellViewModel : ViewModelBase
{
    public MainWindowViewModel Editor { get; }
    public LibraryViewModel Library { get; }

    [ObservableProperty] private string _activeView = "Library";
    [ObservableProperty] private string _mobileTab = "Home";

    public bool IsLibraryView => ActiveView == "Library";
    public bool IsEditorView => ActiveView == "Editor";

    public bool IsHomeTab => MobileTab == "Home";
    public bool IsHistoryTab => MobileTab == "History";
    public bool IsSettingsTab => MobileTab == "Settings";

    /// <summary>Bound to the TransitioningContentControl on the shell — DataTemplates select the matching View.</summary>
    public object CurrentViewModel => IsEditorView ? (object)Editor : Library;
    public object CurrentMobileTabViewModel => IsSettingsTab || IsHistoryTab ? (object)Editor : Library;

    public AppShellViewModel()
    {
        Editor = new MainWindowViewModel();
        Library = new LibraryViewModel(Editor);
        Library.ProjectOpened += (_, _) => ActiveView = "Editor";
        Editor.BackRequested += (_, _) => ActiveView = "Library";
    }

    partial void OnActiveViewChanged(string value)
    {
        OnPropertyChanged(nameof(IsLibraryView));
        OnPropertyChanged(nameof(IsEditorView));
        OnPropertyChanged(nameof(CurrentViewModel));
    }

    partial void OnMobileTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsHomeTab));
        OnPropertyChanged(nameof(IsHistoryTab));
        OnPropertyChanged(nameof(IsSettingsTab));
        OnPropertyChanged(nameof(CurrentMobileTabViewModel));
    }

    [RelayCommand]
    private void NavigateToLibrary() => ActiveView = "Library";

    [RelayCommand]
    private void NavigateToEditor() => ActiveView = "Editor";

    [RelayCommand]
    private void SelectMobileTab(string? tab) => MobileTab = tab ?? "Home";
}
