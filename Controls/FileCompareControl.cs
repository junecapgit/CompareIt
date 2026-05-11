using System.Text;
using System.Text.RegularExpressions;
using CompareIt.Core;
using DiffPlex.DiffBuilder.Model;

namespace CompareIt.Controls;

/// <summary>
/// Side-by-side file comparison view.
/// Left and right panes scroll in sync.
/// Press F3 / Shift+F3 to jump between diff blocks.
/// Press Ctrl+Right / Ctrl+Left to copy the current block.
/// </summary>
public class FileCompareControl : UserControl
{
    private static readonly Color ColModified = Color.FromArgb(255, 246, 163);
    private static readonly Color ColDeleted = Color.FromArgb(255, 220, 220);
    private static readonly Color ColInserted = Color.FromArgb(220, 255, 220);
    private static readonly Color ColImaginary = Color.FromArgb(224, 224, 224);
    private static readonly Color ColSame = Color.White;
    private static readonly Color ColLineNumber = Color.FromArgb(102, 102, 102);

    private readonly Label _leftPathLabel;
    private readonly Label _rightPathLabel;
    private readonly Label _statsLabel;
    private readonly Button _prevDiffBtn;
    private readonly Button _nextDiffBtn;
    private readonly Button _copyLeftToRightBtn;
    private readonly Button _copyRightToLeftBtn;
    private readonly Button _saveBtn;
    private readonly Button _undoBtn;
    private readonly SyncedRichTextBox _leftBox;
    private readonly SyncedRichTextBox _rightBox;

    private bool _syncing;
    private readonly List<DiffBlock> _diffBlocks = new();
    private int _currentDiffIndex = -1;
    private string _leftPath = string.Empty;
    private string _rightPath = string.Empty;
    private TextDocument _savedLeftDocument = TextDocument.Empty;
    private TextDocument _savedRightDocument = TextDocument.Empty;
    private TextDocument _workingLeftDocument = TextDocument.Empty;
    private TextDocument _workingRightDocument = TextDocument.Empty;
    private readonly List<UndoState> _undoHistory = new();

    public FileCompareControl()
    {
        Dock = DockStyle.Fill;

        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Color.FromArgb(240, 240, 240)
        };

        _prevDiffBtn = new Button
        {
            Text = "◄ Prev Diff",
            Width = 90,
            Height = 24,
            Left = 4,
            Top = 2,
            FlatStyle = FlatStyle.Flat
        };
        _nextDiffBtn = new Button
        {
            Text = "Next Diff ►",
            Width = 90,
            Height = 24,
            Left = 98,
            Top = 2,
            FlatStyle = FlatStyle.Flat
        };
        _copyLeftToRightBtn = new Button
        {
            Text = "Copy Left -> Right",
            Width = 120,
            Height = 24,
            Left = 196,
            Top = 2,
            FlatStyle = FlatStyle.Flat
        };
        _copyRightToLeftBtn = new Button
        {
            Text = "<- Copy Right",
            Width = 110,
            Height = 24,
            Left = 320,
            Top = 2,
            FlatStyle = FlatStyle.Flat
        };
        _saveBtn = new Button
        {
            Text = "Save",
            Width = 70,
            Height = 24,
            Left = 434,
            Top = 2,
            FlatStyle = FlatStyle.Flat
        };
        _undoBtn = new Button
        {
            Text = "Undo Last",
            Width = 85,
            Height = 24,
            Left = 508,
            Top = 2,
            FlatStyle = FlatStyle.Flat
        };
        _statsLabel = new Label
        {
            Text = string.Empty,
            Left = 602,
            Top = 5,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f)
        };

        toolbar.Controls.AddRange([
            _prevDiffBtn,
            _nextDiffBtn,
            _copyLeftToRightBtn,
            _copyRightToLeftBtn,
            _saveBtn,
            _undoBtn,
            _statsLabel
        ]);

        var legend = BuildLegend();

        var headerTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 22,
            ColumnCount = 2,
            RowCount = 1
        };
        headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        _leftPathLabel = MakePathLabel("Left");
        _rightPathLabel = MakePathLabel("Right");
        headerTable.Controls.Add(_leftPathLabel, 0, 0);
        headerTable.Controls.Add(_rightPathLabel, 1, 0);

        var monoFont = new Font("Consolas", 10f);
        _leftBox = new SyncedRichTextBox
        {
            Dock = DockStyle.Fill,
            Font = monoFont,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            BackColor = Color.White
        };
        _rightBox = new SyncedRichTextBox
        {
            Dock = DockStyle.Fill,
            Font = monoFont,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            BackColor = Color.White
        };

        _leftBox.ScrollChanged += OnLeftScrollChanged;
        _rightBox.ScrollChanged += OnRightScrollChanged;

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 50,
            Panel1MinSize = 100,
            Panel2MinSize = 100
        };
        split.Panel1.Controls.Add(_leftBox);
        split.Panel2.Controls.Add(_rightBox);

        Controls.Add(split);
        Controls.Add(headerTable);
        Controls.Add(legend);
        Controls.Add(toolbar);

        _nextDiffBtn.Click += (_, _) => NavigateDiff(+1);
        _prevDiffBtn.Click += (_, _) => NavigateDiff(-1);
        _copyLeftToRightBtn.Click += (_, _) => CopyCurrentBlock(copyLeftToRight: true);
        _copyRightToLeftBtn.Click += (_, _) => CopyCurrentBlock(copyLeftToRight: false);
        _saveBtn.Click += (_, _) => SaveWorkingCopies();
        _undoBtn.Click += (_, _) => UndoLastChange();
        KeyDown += OnKeyDown;
    }

    public void Compare(string leftPath, string rightPath)
    {
        _leftPath = leftPath;
        _rightPath = rightPath;
        _diffBlocks.Clear();
        _currentDiffIndex = -1;
        _undoHistory.Clear();

        if (FileDiffer.IsBinary(leftPath) || FileDiffer.IsBinary(rightPath))
        {
            ShowBinaryMessage(leftPath, rightPath);
            return;
        }

        try { _savedLeftDocument = TextDocument.Load(leftPath); }
        catch { _savedLeftDocument = TextDocument.FromError(leftPath); }

        try { _savedRightDocument = TextDocument.Load(rightPath); }
        catch { _savedRightDocument = TextDocument.FromError(rightPath); }

        _workingLeftDocument = _savedLeftDocument;
        _workingRightDocument = _savedRightDocument;
        RenderWorkingDiff();
    }

    private void RenderWorkingDiff()
    {
        _leftPathLabel.Text = BuildPathLabel(_leftPath, IsLeftDirty);
        _rightPathLabel.Text = BuildPathLabel(_rightPath, IsRightDirty);
        var diff = FileDiffer.Compare(_workingLeftDocument.GetText(), _workingRightDocument.GetText());
        RenderDiff(diff);
    }

    private void RenderDiff(SideBySideDiffModel diff)
    {
        var leftLines = diff.OldText.Lines;
        var rightLines = diff.NewText.Lines;
        int total = Math.Max(leftLines.Count, rightLines.Count);
        int lineNumberWidth = Math.Max(_workingLeftDocument.Lines.Count, _workingRightDocument.Lines.Count).ToString().Length;

        int modified = 0;
        int deleted = 0;
        int inserted = 0;
        int leftLineNumber = 1;
        int rightLineNumber = 1;
        DiffBlockBuilder? currentBlock = null;

        _leftBox.BeginUpdate();
        _rightBox.BeginUpdate();
        _leftBox.Clear();
        _rightBox.Clear();

        for (int row = 0; row < total; row++)
        {
            var leftPiece = row < leftLines.Count ? leftLines[row] : null;
            var rightPiece = row < rightLines.Count ? rightLines[row] : null;

            bool leftHasLine = HasActualLine(leftPiece);
            bool rightHasLine = HasActualLine(rightPiece);

            AppendLine(_leftBox, leftHasLine ? leftLineNumber : null, leftPiece, rightPiece, lineNumberWidth);
            AppendLine(_rightBox, rightHasLine ? rightLineNumber : null, rightPiece, leftPiece, lineNumberWidth);

            bool rowChanged = IsChanged(leftPiece, rightPiece);
            if (rowChanged)
            {
                currentBlock ??= new DiffBlockBuilder(row, leftLineNumber - 1, rightLineNumber - 1);
                currentBlock.EndRow = row;

                if (leftPiece?.Type == ChangeType.Modified || rightPiece?.Type == ChangeType.Modified)
                {
                    modified++;
                }
                else if (leftPiece?.Type == ChangeType.Deleted || rightPiece?.Type == ChangeType.Deleted)
                {
                    deleted++;
                }
                else if (leftPiece?.Type == ChangeType.Inserted || rightPiece?.Type == ChangeType.Inserted)
                {
                    inserted++;
                }
            }
            else if (currentBlock is not null)
            {
                currentBlock.LeftLineCount = (leftLineNumber - 1) - currentBlock.LeftStartLineIndex;
                currentBlock.RightLineCount = (rightLineNumber - 1) - currentBlock.RightStartLineIndex;
                _diffBlocks.Add(currentBlock.Build());
                currentBlock = null;
            }

            if (leftHasLine)
            {
                leftLineNumber++;
            }

            if (rightHasLine)
            {
                rightLineNumber++;
            }
        }

        if (currentBlock is not null)
        {
            currentBlock.LeftLineCount = (leftLineNumber - 1) - currentBlock.LeftStartLineIndex;
            currentBlock.RightLineCount = (rightLineNumber - 1) - currentBlock.RightStartLineIndex;
            _diffBlocks.Add(currentBlock.Build());
        }

        _leftBox.EndUpdate();
        _rightBox.EndUpdate();

        var parts = new List<string>();
        if (modified > 0) parts.Add($"{modified} modified");
        if (deleted > 0) parts.Add($"{deleted} deleted");
        if (inserted > 0) parts.Add($"{inserted} inserted");
        _statsLabel.Text = parts.Count > 0
            ? string.Join("  |  ", parts)
            : "Files are identical";

        bool hasBlocks = _diffBlocks.Count > 0;
        _prevDiffBtn.Enabled = _nextDiffBtn.Enabled = hasBlocks;
        _copyLeftToRightBtn.Enabled = _copyRightToLeftBtn.Enabled = hasBlocks;
        _saveBtn.Enabled = IsLeftDirty || IsRightDirty;
        _undoBtn.Enabled = _undoHistory.Count > 0;
        if (hasBlocks)
        {
            NavigateDiff(+1);
        }
    }

    private static void AppendLine(
        SyncedRichTextBox box,
        int? lineNumber,
        DiffPiece? piece,
        DiffPiece? oppositePiece,
        int lineNumberWidth)
    {
        int lineStart = box.TextLength;
        string numberText = lineNumber.HasValue
            ? $"{lineNumber.Value.ToString().PadLeft(lineNumberWidth)} | "
            : new string(' ', lineNumberWidth) + " | ";

        box.AppendText(numberText);
        box.Select(lineStart, numberText.Length);
        box.SelectionColor = ColLineNumber;
        box.SelectionBackColor = Color.White;

        string bodyText = piece?.Text ?? string.Empty;
        var spans = GetHighlightSpans(piece, oppositePiece, bodyText.Length);

        if (bodyText.Length > 0)
        {
            int bodyStart = box.TextLength;
            box.AppendText(bodyText);

            foreach (var span in spans)
            {
                if (span.Length <= 0)
                {
                    continue;
                }

                box.Select(bodyStart + span.Start, span.Length);
                box.SelectionBackColor = span.Background;
            }
        }

        box.AppendText("\n");
        box.Select(box.TextLength, 0);
        box.SelectionColor = box.ForeColor;
        box.SelectionBackColor = Color.White;
    }

    private static IReadOnlyList<HighlightSpan> GetHighlightSpans(DiffPiece? piece, DiffPiece? oppositePiece, int textLength)
    {
        if (piece is null || textLength == 0)
        {
            return [];
        }

        if (piece.Type == ChangeType.Modified && oppositePiece is not null)
        {
            return BuildModifiedSpans(piece.Text ?? string.Empty, oppositePiece.Text ?? string.Empty);
        }

        Color background = piece.Type switch
        {
            ChangeType.Deleted => ColDeleted,
            ChangeType.Inserted => ColInserted,
            ChangeType.Imaginary => ColImaginary,
            _ => ColSame
        };

        return background == ColSame ? [] : [new HighlightSpan(0, textLength, background)];
    }

    private static IReadOnlyList<HighlightSpan> BuildModifiedSpans(string source, string opposite)
    {
        if (source.Length == 0)
        {
            return [];
        }

        int[,] lcs = new int[source.Length + 1, opposite.Length + 1];
        for (int sourceIndex = source.Length - 1; sourceIndex >= 0; sourceIndex--)
        {
            for (int oppositeIndex = opposite.Length - 1; oppositeIndex >= 0; oppositeIndex--)
            {
                lcs[sourceIndex, oppositeIndex] = source[sourceIndex] == opposite[oppositeIndex]
                    ? 1 + lcs[sourceIndex + 1, oppositeIndex + 1]
                    : Math.Max(lcs[sourceIndex + 1, oppositeIndex], lcs[sourceIndex, oppositeIndex + 1]);
            }
        }

        bool[] matched = new bool[source.Length];
        int sourceCursor = 0;
        int oppositeCursor = 0;
        while (sourceCursor < source.Length && oppositeCursor < opposite.Length)
        {
            if (source[sourceCursor] == opposite[oppositeCursor])
            {
                matched[sourceCursor] = true;
                sourceCursor++;
                oppositeCursor++;
            }
            else if (lcs[sourceCursor + 1, oppositeCursor] >= lcs[sourceCursor, oppositeCursor + 1])
            {
                sourceCursor++;
            }
            else
            {
                oppositeCursor++;
            }
        }

        var spans = new List<HighlightSpan>();
        int spanStart = -1;
        for (int i = 0; i < matched.Length; i++)
        {
            if (!matched[i])
            {
                if (spanStart < 0)
                {
                    spanStart = i;
                }
            }
            else if (spanStart >= 0)
            {
                spans.Add(new HighlightSpan(spanStart, i - spanStart, ColModified));
                spanStart = -1;
            }
        }

        if (spanStart >= 0)
        {
            spans.Add(new HighlightSpan(spanStart, matched.Length - spanStart, ColModified));
        }

        return spans.Count > 0 ? spans : [new HighlightSpan(0, source.Length, ColModified)];
    }

    private void ShowBinaryMessage(string leftPath, string rightPath)
    {
        _leftBox.Clear();
        _rightBox.Clear();
        _leftBox.AppendText($"[Binary file]{Environment.NewLine}{leftPath}");
        _rightBox.AppendText($"[Binary file]{Environment.NewLine}{rightPath}");
        _statsLabel.Text = "Binary files - cannot show text diff";
        _prevDiffBtn.Enabled = _nextDiffBtn.Enabled = false;
        _copyLeftToRightBtn.Enabled = _copyRightToLeftBtn.Enabled = false;
        _saveBtn.Enabled = false;
        _undoBtn.Enabled = false;
    }

    private void NavigateDiff(int direction)
    {
        if (_diffBlocks.Count == 0)
        {
            return;
        }

        _currentDiffIndex = (_currentDiffIndex + direction + _diffBlocks.Count) % _diffBlocks.Count;
        int targetLine = _diffBlocks[_currentDiffIndex].StartRow;
        ScrollBoxToLine(_leftBox, targetLine);
        ScrollBoxToLine(_rightBox, targetLine);
    }

    private static void ScrollBoxToLine(RichTextBox box, int lineIndex)
    {
        if (lineIndex < 0)
        {
            return;
        }

        int charIndex = box.GetFirstCharIndexFromLine(lineIndex);
        if (charIndex < 0)
        {
            return;
        }

        box.Select(charIndex, 0);
        box.ScrollToCaret();
    }

    private void CopyCurrentBlock(bool copyLeftToRight)
    {
        if (_currentDiffIndex < 0 || _currentDiffIndex >= _diffBlocks.Count)
        {
            return;
        }

        var block = _diffBlocks[_currentDiffIndex];
        var sourceDocument = copyLeftToRight ? _workingLeftDocument : _workingRightDocument;
        var targetDocument = copyLeftToRight ? _workingRightDocument : _workingLeftDocument;

        int sourceStart = copyLeftToRight ? block.LeftStartLineIndex : block.RightStartLineIndex;
        int sourceCount = copyLeftToRight ? block.LeftLineCount : block.RightLineCount;
        int targetStart = copyLeftToRight ? block.RightStartLineIndex : block.LeftStartLineIndex;
        int targetCount = copyLeftToRight ? block.RightLineCount : block.LeftLineCount;

        PushUndoState();

        var replacementLines = sourceDocument.Lines
            .Skip(sourceStart)
            .Take(sourceCount)
            .ToList();

        var updatedTarget = targetDocument.ReplaceRange(targetStart, targetCount, replacementLines);

        if (copyLeftToRight)
        {
            _workingRightDocument = updatedTarget;
        }
        else
        {
            _workingLeftDocument = updatedTarget;
        }

        RenderWorkingDiff();
    }

    private void SaveWorkingCopies()
    {
        if (IsLeftDirty)
        {
            File.WriteAllText(_leftPath, _workingLeftDocument.GetText(), new UTF8Encoding(false));
            _savedLeftDocument = _workingLeftDocument;
        }

        if (IsRightDirty)
        {
            File.WriteAllText(_rightPath, _workingRightDocument.GetText(), new UTF8Encoding(false));
            _savedRightDocument = _workingRightDocument;
        }

        RenderWorkingDiff();
    }

    private void UndoLastChange()
    {
        if (_undoHistory.Count == 0)
        {
            return;
        }

        var previousState = _undoHistory[^1];
        _undoHistory.RemoveAt(_undoHistory.Count - 1);
        _workingLeftDocument = previousState.LeftDocument;
        _workingRightDocument = previousState.RightDocument;
        RenderWorkingDiff();
    }

    private void PushUndoState()
    {
        _undoHistory.Add(new UndoState(_workingLeftDocument, _workingRightDocument));
        if (_undoHistory.Count > 5)
        {
            _undoHistory.RemoveAt(0);
        }
    }

    private void OnLeftScrollChanged(object? sender, Point pos)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        _rightBox.SetScrollPosition(pos);
        _syncing = false;
    }

    private void OnRightScrollChanged(object? sender, Point pos)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        _leftBox.SetScrollPosition(pos);
        _syncing = false;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F3)
        {
            NavigateDiff(e.Shift ? -1 : +1);
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.Right)
        {
            CopyCurrentBlock(copyLeftToRight: true);
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.Left)
        {
            CopyCurrentBlock(copyLeftToRight: false);
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.S)
        {
            SaveWorkingCopies();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.Z)
        {
            UndoLastChange();
            e.Handled = true;
        }
    }

    private static Label MakePathLabel(string placeholder) => new()
    {
        Text = placeholder,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        BackColor = Color.SteelBlue,
        ForeColor = Color.White,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(4, 0, 0, 0)
    };

    private static Panel BuildLegend()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 22,
            BackColor = Color.FromArgb(250, 250, 250)
        };

        int x = 8;
        foreach (var (label, color) in new[]
        {
            ("Same", ColSame),
            ("Changed Chars", ColModified),
            ("Deleted", ColDeleted),
            ("Inserted", ColInserted),
            ("Spacer", ColImaginary)
        })
        {
            var swatch = new Panel
            {
                Left = x,
                Top = 4,
                Width = 14,
                Height = 14,
                BackColor = color,
                BorderStyle = BorderStyle.FixedSingle
            };
            var labelControl = new Label
            {
                Text = label,
                Left = x + 16,
                Top = 4,
                AutoSize = true,
                Font = new Font("Segoe UI", 8f)
            };
            panel.Controls.Add(swatch);
            panel.Controls.Add(labelControl);
            x += 96;
        }

        return panel;
    }

    private static bool HasActualLine(DiffPiece? piece)
        => piece is not null && piece.Type != ChangeType.Imaginary;

    private bool IsLeftDirty => !_workingLeftDocument.ContentEquals(_savedLeftDocument);

    private bool IsRightDirty => !_workingRightDocument.ContentEquals(_savedRightDocument);

    private static string BuildPathLabel(string path, bool isDirty)
        => isDirty ? $"* {path}" : path;

    private static bool IsChanged(DiffPiece? leftPiece, DiffPiece? rightPiece)
        => (leftPiece?.Type ?? ChangeType.Unchanged) != ChangeType.Unchanged
            || (rightPiece?.Type ?? ChangeType.Unchanged) != ChangeType.Unchanged;

    private readonly record struct HighlightSpan(int Start, int Length, Color Background);

    private readonly record struct DiffBlock(
        int StartRow,
        int EndRow,
        int LeftStartLineIndex,
        int LeftLineCount,
        int RightStartLineIndex,
        int RightLineCount);

    private readonly record struct UndoState(TextDocument LeftDocument, TextDocument RightDocument);

    private sealed class DiffBlockBuilder(int startRow, int leftStartLineIndex, int rightStartLineIndex)
    {
        public int EndRow { get; set; } = startRow;
        public int LeftLineCount { get; set; }
        public int RightLineCount { get; set; }
        public int StartRow { get; } = startRow;
        public int LeftStartLineIndex { get; } = leftStartLineIndex;
        public int RightStartLineIndex { get; } = rightStartLineIndex;

        public DiffBlock Build() => new(StartRow, EndRow, LeftStartLineIndex, LeftLineCount, RightStartLineIndex, RightLineCount);
    }

    private sealed record TextDocument(IReadOnlyList<string> Lines, string NewLine, bool EndsWithNewLine)
    {
        public static TextDocument Empty { get; } = new([], Environment.NewLine, false);

        public static TextDocument Load(string path)
        {
            string text = File.ReadAllText(path);
            return FromText(text);
        }

        public static TextDocument FromError(string path)
            => FromText($"[Error reading file: {path}]");

        public static TextDocument FromText(string text)
        {
            string newLine = DetectNewLine(text);
            bool endsWithNewLine = text.EndsWith("\r\n", StringComparison.Ordinal)
                || text.EndsWith("\n", StringComparison.Ordinal)
                || text.EndsWith("\r", StringComparison.Ordinal);
            string[] splitLines = Regex.Split(text, "\r\n|\n|\r");
            if (splitLines.Length > 0 && endsWithNewLine)
            {
                splitLines = splitLines[..^1];
            }

            return new TextDocument(splitLines, newLine, endsWithNewLine);
        }

        public string GetText()
        {
            var builder = new StringBuilder();
            for (int i = 0; i < Lines.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(NewLine);
                }

                builder.Append(Lines[i]);
            }

            if (EndsWithNewLine && Lines.Count > 0)
            {
                builder.Append(NewLine);
            }

            return builder.ToString();
        }

        public TextDocument ReplaceRange(int start, int count, IReadOnlyCollection<string> replacement)
        {
            var updatedLines = Lines.ToList();
            updatedLines.RemoveRange(start, count);
            updatedLines.InsertRange(start, replacement);
            return this with { Lines = updatedLines };
        }

        public bool ContentEquals(TextDocument other)
        {
            if (EndsWithNewLine != other.EndsWithNewLine || NewLine != other.NewLine || Lines.Count != other.Lines.Count)
            {
                return false;
            }

            for (int i = 0; i < Lines.Count; i++)
            {
                if (!string.Equals(Lines[i], other.Lines[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string DetectNewLine(string text)
        {
            if (text.Contains("\r\n", StringComparison.Ordinal)) return "\r\n";
            if (text.Contains('\n')) return "\n";
            if (text.Contains('\r')) return "\r";
            return Environment.NewLine;
        }
    }
}
