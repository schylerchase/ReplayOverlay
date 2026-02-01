using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OBSReplay.Host.Models;

namespace OBSReplay.Host.Services;

public class TrayIconService : IDisposable
{
    private NotifyIcon? _icon;
    private ContextMenuStrip? _menu;

    public event Action? ShowOverlayClicked;
    public event Action? SaveReplayClicked;
    public event Action? StartBufferClicked;
    public event Action? StopBufferClicked;
    public event Action? SettingsClicked;
    public event Action<string>? OpenLibraryClicked;
    public event Action? RestartClicked;
    public event Action? ExitClicked;

    public void Initialize()
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Show Overlay", null, (_, _) => ShowOverlayClicked?.Invoke());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Save Replay", null, (_, _) => SaveReplayClicked?.Invoke());
        _menu.Items.Add("Start Buffer", null, (_, _) => StartBufferClicked?.Invoke());
        _menu.Items.Add("Stop Buffer", null, (_, _) => StopBufferClicked?.Invoke());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Settings", null, (_, _) => SettingsClicked?.Invoke());
        _menu.Items.Add("Open Library", null, (_, _) => OpenLibraryClicked?.Invoke(""));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Restart", null, (_, _) => RestartClicked?.Invoke());
        _menu.Items.Add("Exit", null, (_, _) => ExitClicked?.Invoke());

        _icon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Visible = true,
            Text = "Replay Overlay",
            ContextMenuStrip = _menu
        };

        _icon.Click += (_, e) =>
        {
            if (e is MouseEventArgs me && me.Button == MouseButtons.Left)
                ShowOverlayClicked?.Invoke();
        };
    }

    /// <summary>
    /// Paints a 32x32 tray icon with a red dot and "REC" text,
    /// matching the Python overlay's tray icon design.
    /// </summary>
    private static Icon CreateTrayIcon()
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        // Dark background with rounded border
        using var bgBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
        using var borderPen = new Pen(Color.FromArgb(60, 60, 60), 1);
        g.FillRectangle(bgBrush, 0, 0, size, size);
        g.DrawRectangle(borderPen, 0, 0, size - 1, size - 1);

        // Calculate centered layout
        int dotSize = 8;
        using var font = new Font("Segoe UI", 8, FontStyle.Bold);
        var textSize = g.MeasureString("REC", font);
        int gap = 2;
        float totalWidth = dotSize + gap + textSize.Width;
        float startX = (size - totalWidth) / 2.0f;
        float centerY = size / 2.0f;

        // Red dot
        using var dotBrush = new SolidBrush(Color.FromArgb(233, 69, 96)); // #e94560
        g.FillEllipse(dotBrush, startX, centerY - dotSize / 2.0f, dotSize, dotSize);

        // "REC" text
        using var textBrush = new SolidBrush(Color.FromArgb(233, 69, 96));
        float textX = startX + dotSize + gap;
        float textY = centerY - textSize.Height / 2.0f;
        g.DrawString("REC", font, textBrush, textX, textY);

        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        if (_icon != null)
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
        _menu?.Dispose();
        GC.SuppressFinalize(this);
    }
}
