using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClipDrop.Core;

namespace ClipDrop.Windows.Views;

// Read-only, bounded viewport. No archive paths or live ShelfItem objects are
// retained here; a later restore must revalidate its own archive and live state.
public partial class BackupComparisonView : UserControl
{
    private IReadOnlyList<BackupClipComparison> _clips = Array.Empty<BackupClipComparison>();
    private sealed record Row(string Title, string Description, string Identity);
    public BackupComparisonView() { InitializeComponent(); Visibility = Visibility.Collapsed; }
    public void Show(BackupPreview preview) { _clips = preview.Clips; Visibility = Visibility.Visible; ApplyFilter(); }
    public void Clear() { _clips = Array.Empty<BackupClipComparison>(); Comparisons.ItemsSource = null; Visibility = Visibility.Collapsed; }
    private void OnFilter(object sender, RoutedEventArgs e) => ApplyFilter();
    private void ApplyFilter()
    {
        var rows = _clips.Where(c => IncludeIdentical.IsChecked == true || c.Status != BackupClipStatus.Identical).Select(c => new Row(
            Title(c.Title), Describe(c), "Clip ID · " + c.Id)).ToArray();
        Comparisons.ItemsSource = rows;
        Comparisons.Visibility = rows.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        Empty.Text = _clips.Count == 0 ? "No active clips in this backup." : "All active clips match the captured library.";
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
