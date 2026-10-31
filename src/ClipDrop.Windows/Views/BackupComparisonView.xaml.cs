using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClipDrop.Core;

namespace ClipDrop.Windows.Views;

// Read-only, bounded viewport. No archive paths or live ShelfItem objects are
// retained here; a later restore must revalidate its own archive and live state.
public partial class BackupComparisonView : UserControl
{
    public sealed record ChoiceOption(string Label, BackupChoice Choice)
    {
        public override string ToString() => Label;
    }
    public sealed class Row : INotifyPropertyChanged
    {
        private readonly BackupRestoreDraft _draft;
        private readonly Action _changed;
        private ChoiceOption _selection;
        public string Title { get; }
        public string Description { get; }
        public string Identity { get; }
        public BackupClipStatus Status { get; }
        public string Id { get; }
        public ChoiceOption[] Options { get; }
        public Visibility ChoiceVisibility => Status == BackupClipStatus.Identical ? Visibility.Collapsed : Visibility.Visible;
        public ChoiceOption Selection {
            get => _selection;
            set { if (value is null || value == _selection) return; _draft.Choose(Id, value.Choice); _selection = value; PropertyChanged?.Invoke(this, new(nameof(Selection))); _changed(); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public Row(BackupClipComparison clip, BackupRestoreDraft draft, Action changed)
        {
            _draft = draft; _changed = changed; Id = clip.Id; Status = clip.Status;
            Title = BackupComparisonView.Title(clip.Title); Description = Describe(clip); Identity = "Clip ID · " + clip.Id;
            Options = clip.Status switch {
                BackupClipStatus.New => new[] { new ChoiceOption("Add from backup", BackupChoice.UseBackup), new ChoiceOption("Skip clip", BackupChoice.KeepCurrent) },
                BackupClipStatus.InTrash => new[] { new ChoiceOption("Leave in Trash", BackupChoice.KeepCurrent), new ChoiceOption("Restore from backup", BackupChoice.UseBackup) },
                BackupClipStatus.ChangedMetadata => new[] { new ChoiceOption("Keep current", BackupChoice.KeepCurrent), new ChoiceOption("Use backup", BackupChoice.UseBackup) },
                _ => new[] { new ChoiceOption("Keep identical clip", BackupChoice.KeepCurrent) }
            };
            _selection = Options.Single(o => o.Choice == draft.ChoiceFor(Id));
        }
    }
    private BackupRestoreDraft? _draft;
    private Row[] _rows = Array.Empty<Row>();
    public BackupComparisonView() { InitializeComponent(); Visibility = Visibility.Collapsed; }
    public void Show(BackupPreview preview)
    {
        _draft = new(preview); _rows = preview.Clips.Select(c => new Row(c, _draft, UpdatePlan)).ToArray();
        Visibility = Visibility.Visible; ApplyFilter(); UpdatePlan();
    }
    public void Clear() { _draft = null; _rows = Array.Empty<Row>(); Comparisons.ItemsSource = null; PlanSummary.Text = ""; Visibility = Visibility.Collapsed; }
    private void OnFilter(object sender, RoutedEventArgs e) => ApplyFilter();
    private void UpdatePlan()
    {
        if (_draft is null) return;
        var plan = _draft.Counts;
        PlanSummary.Text = $"Preview · {plan.Add} add · {plan.Replace} replace · {plan.Restore} restore · {plan.Keep} keep/skip";
    }
    private void ApplyFilter()
    {
        var rows = IncludeIdentical.IsChecked == true ? _rows : _rows.Where(c => c.Status != BackupClipStatus.Identical).ToArray();
        Comparisons.ItemsSource = rows;
        Comparisons.Visibility = rows.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        Empty.Text = _rows.Length == 0 ? "No active clips in this backup." : "All active clips match the captured library.";
        Empty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private static string Title(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Untitled clip";
        return title[..Math.Min(240, title.Length)].Replace('\r', ' ').Replace('\n', ' ');
    }
    private static string Describe(BackupClipComparison clip) => clip.Status switch {
        BackupClipStatus.New => "New clip · absent from the captured library",
        BackupClipStatus.Identical => "Identical recorded metadata",
        BackupClipStatus.InTrash => "Already in Trash · restoration needs a separate choice",
        _ => "Changed metadata · " + string.Join(", ", clip.ChangedFields.Select(FieldName))
    };
    private static string FieldName(string name) => name switch {
        "Title" => "Name", "Content" => "Text", "FilePath" => "File reference", "Kind" => "Type", "OwnedAsset" => "Managed asset",
        "IsPinned" => "In panel", "KeepAfterUse" => "Keep after use", "PinOrder" => "Panel order", "Group" => "Collection",
        "Clipboard" => "Clipboard formats", "SourceUrl" => "Source URL", "CreatedUtc" => "Created time", _ => name
    };
}
