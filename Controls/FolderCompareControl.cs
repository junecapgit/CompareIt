using CompareIt.Core;

namespace CompareIt.Controls;

/// <summary>
/// Folder comparison view.  Shows a filtered, sorted list of all files found in
/// either tree along with their diff status.  Double-clicking a Modified row
/// raises OpenFileCompare so the parent form can switch to the file view.
/// </summary>
public class FolderCompareControl : UserControl
{
    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------
    public event EventHandler<(string LeftFile, string RightFile)>? OpenFileCompare;

    // -----------------------------------------------------------------------
    // Controls
    // -----------------------------------------------------------------------
    private readonly ListView   _listView;
    private readonly Label      _statsLabel;
    private readonly CheckBox   _hideSameChk;

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------
    private IReadOnlyList<FolderDiffEntry> _allResults = [];
    private string _leftPath  = string.Empty;
    private string _rightPath = string.Empty;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public FolderCompareControl()
    {
        Dock = DockStyle.Fill;

        // Toolbar
        var toolbar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 28,
            BackColor = Color.FromArgb(240, 240, 240)
        };

        _hideSameChk = new CheckBox
        {
            Text    = "Hide identical files",
            Left    = 6, Top = 5,
            AutoSize = true,
            Font    = new Font("Segoe UI", 8.5f)
        };
        _hideSameChk.CheckedChanged += (_, _) => PopulateList();

        _statsLabel = new Label
        {
            Text    = "",
            Left    = 200, Top = 7,
            AutoSize = true,
            Font    = new Font("Segoe UI", 8.5f)
        };

        toolbar.Controls.AddRange(new Control[] { _hideSameChk, _statsLabel });

        // List view
        _listView = new ListView
        {
            Dock         = DockStyle.Fill,
            View         = View.Details,
            FullRowSelect = true,
            GridLines    = true,
            MultiSelect  = false,
            Font         = new Font("Consolas", 9.5f)
        };
        _listView.Columns.Add("Status",          90);
        _listView.Columns.Add("File / Path",     420);
        _listView.Columns.Add("Left Modified",   160);
        _listView.Columns.Add("Left Size",        80);
        _listView.Columns.Add("Right Modified",  160);
        _listView.Columns.Add("Right Size",       80);

        _listView.DoubleClick     += OnDoubleClick;
        _listView.ColumnClick     += OnColumnClick;

        // Context menu
        var ctxMenu  = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Open File Compare");
        openItem.Click += (_, _) => TryOpenFileCompare();
        ctxMenu.Items.Add(openItem);
        _listView.ContextMenuStrip = ctxMenu;

        Controls.Add(_listView);
        Controls.Add(toolbar);
    }

    // -----------------------------------------------------------------------
    // Public methods
    // -----------------------------------------------------------------------

    public void Compare(string leftPath, string rightPath)
    {
        _leftPath  = leftPath;
        _rightPath = rightPath;
        _allResults = FolderDiffer.Compare(leftPath, rightPath);
        PopulateList();
    }

    // -----------------------------------------------------------------------
    // List population
    // -----------------------------------------------------------------------

    private void PopulateList()
    {
        _listView.BeginUpdate();
        _listView.Items.Clear();

        bool hideSame = _hideSameChk.Checked;
        int  same = 0, modified = 0, leftOnly = 0, rightOnly = 0;

        foreach (var entry in _allResults)
        {
            switch (entry.Status)
            {
                case FileStatus.Same:      same++;      break;
                case FileStatus.Modified:  modified++;  break;
                case FileStatus.LeftOnly:  leftOnly++;  break;
                case FileStatus.RightOnly: rightOnly++; break;
            }

            if (hideSame && entry.Status == FileStatus.Same) continue;

            var item = new ListViewItem(StatusText(entry.Status))
            {
                BackColor = StatusColor(entry.Status),
                Tag       = entry
            };
            item.SubItems.Add(entry.RelativePath);
            item.SubItems.Add(entry.LeftModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
            item.SubItems.Add(entry.LeftSize.HasValue  ? FormatSize(entry.LeftSize.Value)  : "");
            item.SubItems.Add(entry.RightModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
            item.SubItems.Add(entry.RightSize.HasValue ? FormatSize(entry.RightSize.Value) : "");

            _listView.Items.Add(item);
        }

        _listView.EndUpdate();

        _statsLabel.Text =
            $"{_allResults.Count} total  |  " +
            $"{modified} modified  |  " +
            $"{leftOnly} left only  |  " +
            $"{rightOnly} right only  |  " +
            $"{same} identical";
    }

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    private void OnDoubleClick(object? sender, EventArgs e) => TryOpenFileCompare();

    private void TryOpenFileCompare()
    {
        if (_listView.SelectedItems.Count == 0) return;
        var entry = (FolderDiffEntry)_listView.SelectedItems[0].Tag!;

        if (entry.Status is FileStatus.LeftOnly or FileStatus.RightOnly)
        {
            MessageBox.Show(
                "This file only exists on one side — cannot open side-by-side compare.",
                "CompareIt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        OpenFileCompare?.Invoke(this, (
            Path.Combine(_leftPath,  entry.RelativePath),
            Path.Combine(_rightPath, entry.RelativePath)));
    }

    private int _sortColumn    = 0;
    private bool _sortAscending = true;

    private void OnColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (e.Column == _sortColumn)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn    = e.Column;
            _sortAscending = true;
        }
        _listView.ListViewItemSorter = new ListViewItemComparer(_sortColumn, _sortAscending);
        _listView.Sort();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string StatusText(FileStatus s) => s switch
    {
        FileStatus.Same      => "Same",
        FileStatus.Modified  => "Modified",
        FileStatus.LeftOnly  => "Left Only",
        FileStatus.RightOnly => "Right Only",
        _                    => ""
    };

    private static Color StatusColor(FileStatus s) => s switch
    {
        FileStatus.Same      => Color.White,
        FileStatus.Modified  => Color.FromArgb(255, 255, 180),
        FileStatus.LeftOnly  => Color.FromArgb(200, 220, 255),
        FileStatus.RightOnly => Color.FromArgb(200, 255, 200),
        _                    => Color.White
    };

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1_024                => $"{bytes} B",
        < 1_024 * 1_024        => $"{bytes / 1_024.0:F1} KB",
        < 1_024L * 1_024 * 1_024 => $"{bytes / (1_024.0 * 1_024):F1} MB",
        _                      => $"{bytes / (1_024.0 * 1_024 * 1_024):F2} GB"
    };
}

// ---------------------------------------------------------------------------
// Sort helper
// ---------------------------------------------------------------------------

file sealed class ListViewItemComparer(int column, bool ascending) : System.Collections.IComparer
{
    public int Compare(object? x, object? y)
    {
        var lx = ((ListViewItem)x!).SubItems[column].Text;
        var ly = ((ListViewItem)y!).SubItems[column].Text;
        int result = string.Compare(lx, ly, StringComparison.OrdinalIgnoreCase);
        return ascending ? result : -result;
    }
}
