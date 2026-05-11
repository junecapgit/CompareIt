using CompareIt.Controls;

namespace CompareIt;

public class MainForm : Form
{
    // -----------------------------------------------------------------------
    // Controls
    // -----------------------------------------------------------------------
    private readonly TextBox              _leftPathBox;
    private readonly TextBox              _rightPathBox;
    private readonly RadioButton          _fileModeBtn;
    private readonly RadioButton          _folderModeBtn;
    private readonly Button               _compareBtn;
    private readonly Label                _statusLabel;
    private readonly FileCompareControl   _fileView;
    private readonly FolderCompareControl _folderView;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public MainForm()
    {
        Text            = "CompareIt";
        Size            = new Size(1280, 860);
        MinimumSize     = new Size(800, 600);
        StartPosition   = FormStartPosition.CenterScreen;
        KeyPreview      = true;

        // ---- Path inputs ----
        _leftPathBox  = new TextBox { Dock = DockStyle.Fill };
        _rightPathBox = new TextBox { Dock = DockStyle.Fill };

        var leftBrowseBtn  = MakeBrowseButton();
        var rightBrowseBtn = MakeBrowseButton();
        var swapBtn        = new Button
        {
            Text      = "⇄",
            Width     = 30, Height = 24,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 10f)
        };
        swapBtn.Click += (_, _) =>
            (_leftPathBox.Text, _rightPathBox.Text) = (_rightPathBox.Text, _leftPathBox.Text);

        // ---- Mode radios ----
        _fileModeBtn = new RadioButton
        {
            Text    = "File",
            Checked = true,
            AutoSize = true,
            Anchor  = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom
        };
        _folderModeBtn = new RadioButton
        {
            Text    = "Folder",
            AutoSize = true,
            Anchor  = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom
        };

        // ---- Compare button ----
        _compareBtn = new Button
        {
            Text      = "Compare  [F5]",
            Width     = 120, Height = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.SteelBlue,
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _compareBtn.Click += (_, _) => RunCompare();

        // ---- Top panel (two-row table layout) ----
        var topPanel = new TableLayoutPanel
        {
            Dock       = DockStyle.Top,
            Height     = 74,
            ColumnCount = 8,
            RowCount   = 2,
            Padding    = new Padding(6, 6, 6, 4),
            BackColor  = SystemColors.Control
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // "Left:"
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));      // left path
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // browse left
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // swap
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // "Right:"
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));      // right path
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // browse right
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));          // compare

        topPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        topPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

        // Row 0 — paths
        topPanel.Controls.Add(MakeLabel("Left:"),  0, 0);
        topPanel.Controls.Add(_leftPathBox,        1, 0);
        topPanel.Controls.Add(leftBrowseBtn,       2, 0);
        topPanel.Controls.Add(swapBtn,             3, 0);
        topPanel.Controls.Add(MakeLabel("Right:"), 4, 0);
        topPanel.Controls.Add(_rightPathBox,       5, 0);
        topPanel.Controls.Add(rightBrowseBtn,      6, 0);

        // Row 1 — mode radios + compare button
        var modePanel = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize      = false
        };
        modePanel.Controls.Add(MakeLabel("Mode:"));
        modePanel.Controls.Add(_fileModeBtn);
        modePanel.Controls.Add(_folderModeBtn);
        topPanel.Controls.Add(modePanel, 0, 1);
        topPanel.SetColumnSpan(modePanel, 7);
        topPanel.Controls.Add(_compareBtn, 7, 0);
        topPanel.SetRowSpan(_compareBtn, 2);

        // ---- Status bar ----
        var statusBar  = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = Color.SteelBlue };
        _statusLabel   = new Label
        {
            Dock      = DockStyle.Fill,
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0)
        };
        statusBar.Controls.Add(_statusLabel);

        // ---- Content views ----
        _fileView   = new FileCompareControl   { Visible = true  };
        _folderView = new FolderCompareControl { Visible = false };

        _folderView.OpenFileCompare += (_, args) =>
        {
            _leftPathBox.Text  = args.LeftFile;
            _rightPathBox.Text = args.RightFile;
            _fileModeBtn.Checked = true;
            RunCompare();
        };

        var contentPanel = new Panel { Dock = DockStyle.Fill };
        contentPanel.Controls.Add(_fileView);
        contentPanel.Controls.Add(_folderView);

        // ---- Assemble form ----
        Controls.Add(contentPanel);
        Controls.Add(topPanel);
        Controls.Add(statusBar);

        // ---- Wire events ----
        leftBrowseBtn.Click  += (_, _) => Browse(_leftPathBox);
        rightBrowseBtn.Click += (_, _) => Browse(_rightPathBox);
        _fileModeBtn.CheckedChanged   += (_, _) => SyncViewMode();
        _folderModeBtn.CheckedChanged += (_, _) => SyncViewMode();
        KeyDown += OnFormKeyDown;

        // Apply initial DPI-aware splitter after form is shown
        Shown += (_, _) => SetStatus("Ready  —  F5 to compare  |  Drag files or folders onto either path box");
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop  += OnDragDrop;
    }

    // -----------------------------------------------------------------------
    // Browse helper
    // -----------------------------------------------------------------------

    private void Browse(TextBox target)
    {
        bool fileMode = _fileModeBtn.Checked;
        if (fileMode)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Select file",
                Filter = "All Files (*.*)|*.*"
            };
            if (!string.IsNullOrEmpty(target.Text))
            {
                var dir = Path.GetDirectoryName(target.Text);
                if (Directory.Exists(dir)) dlg.InitialDirectory = dir;
            }
            if (dlg.ShowDialog() == DialogResult.OK)
                target.Text = dlg.FileName;
        }
        else
        {
            using var dlg = new FolderBrowserDialog
            {
                Description        = "Select folder",
                UseDescriptionForTitle = true
            };
            if (Directory.Exists(target.Text))
                dlg.InitialDirectory = target.Text;
            if (dlg.ShowDialog() == DialogResult.OK)
                target.Text = dlg.SelectedPath;
        }
    }

    // -----------------------------------------------------------------------
    // Drag-and-drop
    // -----------------------------------------------------------------------

    private static void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
        if (files == null || files.Length == 0) return;

        if (files.Length >= 2)
        {
            _leftPathBox.Text  = files[0];
            _rightPathBox.Text = files[1];
            // Auto-detect mode from first drop
            bool isFolder = Directory.Exists(files[0]);
            _fileModeBtn.Checked   = !isFolder;
            _folderModeBtn.Checked = isFolder;
        }
        else
        {
            if (string.IsNullOrEmpty(_leftPathBox.Text))
                _leftPathBox.Text = files[0];
            else
                _rightPathBox.Text = files[0];
        }
    }

    // -----------------------------------------------------------------------
    // Compare
    // -----------------------------------------------------------------------

    private void RunCompare()
    {
        string left  = _leftPathBox.Text.Trim();
        string right = _rightPathBox.Text.Trim();

        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            MessageBox.Show("Please enter both left and right paths.", "CompareIt",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;

            if (_fileModeBtn.Checked)
            {
                if (!File.Exists(left))  { ShowMissingError(left);  return; }
                if (!File.Exists(right)) { ShowMissingError(right); return; }
                _fileView.Compare(left, right);
                SetStatus($"File compare:  {left}  ↔  {right}");
            }
            else
            {
                if (!Directory.Exists(left))  { ShowMissingError(left);  return; }
                if (!Directory.Exists(right)) { ShowMissingError(right); return; }
                _folderView.Compare(left, right);
                SetStatus($"Folder compare:  {left}  ↔  {right}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during comparison:\n\n{ex.Message}", "CompareIt",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void SyncViewMode()
    {
        _fileView.Visible   = _fileModeBtn.Checked;
        _folderView.Visible = _folderModeBtn.Checked;
    }

    private void SetStatus(string text) => _statusLabel.Text = "  " + text;

    private void ShowMissingError(string path) =>
        MessageBox.Show($"Path not found:\n{path}", "CompareIt",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5) { RunCompare(); e.Handled = true; }
    }

    private static Label MakeLabel(string text) => new()
    {
        Text      = text,
        AutoSize  = true,
        Anchor    = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom,
        TextAlign = ContentAlignment.MiddleRight
    };

    private static Button MakeBrowseButton() => new()
    {
        Text      = "Browse…",
        Width     = 80, Height = 24,
        FlatStyle = FlatStyle.Flat
    };
}
