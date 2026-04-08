using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowTaskSwitcher.Models;
using WindowTaskSwitcher.Services;

namespace WindowTaskSwitcher.ViewModels;

public partial class SwitcherViewModel : ObservableObject
{
    private readonly WindowEnumerationService _enumerationService;
    private readonly FuzzySearchService _searchService;
    private readonly SearchLearningService _learningService;
    private readonly WindowSwitchService _switchService;

    private List<WindowInfo> _allWindows = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private bool _isVisible;

    public ObservableCollection<SearchResult> FilteredWindows { get; } = [];

    public SwitcherViewModel(
        WindowEnumerationService enumerationService,
        FuzzySearchService searchService,
        SearchLearningService learningService,
        WindowSwitchService switchService)
    {
        _enumerationService = enumerationService;
        _searchService = searchService;
        _learningService = learningService;
        _switchService = switchService;
    }

    partial void OnSearchTextChanged(string value)
    {
        UpdateFilteredWindows();
    }

    public void Show()
    {
        _allWindows = _enumerationService.GetWindows();
        SearchText = string.Empty;
        UpdateFilteredWindows();
        SelectedIndex = 0;
        IsVisible = true;
    }

    public void Hide()
    {
        IsVisible = false;
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void SwitchToSelected()
    {
        if (SelectedIndex >= 0 && SelectedIndex < FilteredWindows.Count)
        {
            var selected = FilteredWindows[SelectedIndex];
            _learningService.RecordSelection(SearchText, selected.Window.ProcessName);
            Hide();
            _switchService.SwitchTo(selected.Window.Handle);
        }
    }

    [RelayCommand]
    private void CloseSelected()
    {
        if (SelectedIndex >= 0 && SelectedIndex < FilteredWindows.Count)
        {
            var selected = FilteredWindows[SelectedIndex];
            _switchService.CloseWindow(selected.Window.Handle);
            // Refresh the list
            _allWindows.RemoveAll(w => w.Handle == selected.Window.Handle);
            UpdateFilteredWindows();
        }
    }

    [RelayCommand]
    private void MoveSelectionUp()
    {
        if (FilteredWindows.Count > 0 && SelectedIndex > 0)
            SelectedIndex--;
    }

    [RelayCommand]
    private void MoveSelectionDown()
    {
        if (FilteredWindows.Count > 0 && SelectedIndex < FilteredWindows.Count - 1)
            SelectedIndex++;
    }

    private void UpdateFilteredWindows()
    {
        FilteredWindows.Clear();

        if (string.IsNullOrEmpty(SearchText))
        {
            // Show all windows, sorted by process name then title
            foreach (var window in _allWindows)
            {
                FilteredWindows.Add(new SearchResult(window, 0, []));
            }
        }
        else
        {
            var results = new List<(WindowInfo Window, int Score, List<int> MatchedIndices)>();

            foreach (var window in _allWindows)
            {
                var (score, matchedIndices) = _searchService.Match(SearchText, window.SearchText);
                if (score > 0)
                {
                    double boost = _learningService.GetBoost(SearchText, window.ProcessName);
                    int boostedScore = (int)(score * boost);
                    results.Add((window, boostedScore, matchedIndices));
                }
            }

            // Sort by score descending
            results.Sort((a, b) => b.Score.CompareTo(a.Score));

            foreach (var (window, score, matchedIndices) in results)
            {
                FilteredWindows.Add(new SearchResult(window, score, matchedIndices));
            }
        }

        // Clamp selection
        if (FilteredWindows.Count > 0)
            SelectedIndex = Math.Clamp(SelectedIndex, 0, FilteredWindows.Count - 1);
    }
}

public sealed record SearchResult(WindowInfo Window, int Score, List<int> MatchedIndices);
