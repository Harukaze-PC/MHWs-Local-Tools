using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class DropdownPicker : ComboBox
{
    const int WmMouseWheel = 0x020A;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr DefWindowProc(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    internal DropdownPicker()
    {
        AutoSize = true;
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmMouseWheel && !DroppedDown)
        {
            // Bypass the combo box's selection change and let Windows route scrolling to its parent.
            message.Result = DefWindowProc(message.HWnd, message.Msg, message.WParam, message.LParam);
            return;
        }
        base.WndProc(ref message);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize);
        // With visual styles, Windows may give the closed selection field a taller
        // native height than WinForms reports. Reserve the full field in auto-size rows.
        size.Height = Math.Max(size.Height, Math.Max(PreferredHeight, Height));
        return size;
    }
}
