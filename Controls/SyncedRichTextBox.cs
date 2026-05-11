using System.Runtime.InteropServices;

namespace CompareIt.Controls;

/// <summary>
/// A RichTextBox that fires ScrollChanged whenever the vertical scroll position changes,
/// and exposes pixel-accurate Get/Set scroll position via the RichEdit EM_GETSCROLLPOS /
/// EM_SETSCROLLPOS messages.  Two instances can be wired together for synchronized scrolling.
/// </summary>
internal class SyncedRichTextBox : RichTextBox
{
    // -----------------------------------------------------------------------
    // Win32
    // -----------------------------------------------------------------------
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref Point lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int EM_GETSCROLLPOS = 0x04DD;
    private const int EM_SETSCROLLPOS = 0x04DE;
    private const int WM_VSCROLL      = 0x0115;
    private const int WM_MOUSEWHEEL   = 0x020A;
    private const int WM_SETREDRAW    = 0x000B;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Fired after any operation that may change the vertical scroll position.</summary>
    public event EventHandler<Point>? ScrollChanged;

    public Point GetScrollPosition()
    {
        var pt = new Point();
        SendMessage(Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref pt);
        return pt;
    }

    public void SetScrollPosition(Point pt)
    {
        SendMessage(Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref pt);
    }

    /// <summary>
    /// Suppresses redraws for the duration of an update batch, then repaints.
    /// Drastically speeds up coloured-line rendering for large files.
    /// </summary>
    public void BeginUpdate() =>
        SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);

    public void EndUpdate()
    {
        SendMessage(Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
        Invalidate();
    }

    // -----------------------------------------------------------------------
    // Internals
    // -----------------------------------------------------------------------

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL)
            ScrollChanged?.Invoke(this, GetScrollPosition());
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown
                      or Keys.Home or Keys.End)
            ScrollChanged?.Invoke(this, GetScrollPosition());
    }
}
