using System.Globalization;
using System.IO;

namespace SecondSonLauncher.Core;

public readonly record struct RenderSize(int Width, int Height)
{
    public override string ToString() => $"{Width} × {Height}";
}

/// The size the game renders at, worked out the way the emulator does it at boot
/// (src/common/memory_patcher.cpp: ApplyAspectRatio, SizeAt, BaseHeightForPercent, Reference).
/// The window size does not change it: without a pick the game keeps its own 1080 lines and the
/// picture is scaled to the window.
public static class RenderMath
{
    const int MinLines = 96, MaxLines = 4320;
    const long MaxPixels = 3840L * 2160;
    const double Base = 16.0 / 9.0;

    /// screen: the game window's size at boot (the whole monitor in fullscreen); display: the monitor.
    /// renderHeight / percent: SHADPS4_RENDER_HEIGHT, then SHADPS4_SCREEN_PERCENT or the in-game
    /// slider's saved value; 0 = not set.
    public static RenderSize Compute(string? aspectMode, RenderSize screen, RenderSize display,
                                     double renderHeight, double percent)
    {
        var aspect = Aspect(aspectMode, screen);
        int h0;
        if (renderHeight > 0)
            h0 = CapPixels(Math.Clamp(Round8(renderHeight), MinLines, MaxLines), aspect);
        else if (percent > 0)
            h0 = BaseHeightForPercent(Math.Clamp(percent, 10, 200), aspect, display, screen);
        else
            h0 = 1080;
        return SizeAt(h0, aspect);
    }

    public static double Aspect(string? mode, RenderSize screen)
    {
        mode = string.IsNullOrWhiteSpace(mode) ? "auto" : mode.Trim();
        double aspect = 0;
        var colon = mode.IndexOf(':');
        if (mode is "off" or "0")
            aspect = Base;
        else if (colon >= 0)
        {
            var w = Atof(mode[..colon]);
            var h = Atof(mode[(colon + 1)..]);
            aspect = h > 0 ? w / h : 0;
        }
        else if (screen.Width > 0 && screen.Height > 0)
            aspect = (double)screen.Width / screen.Height;
        // 3%: a "1280x720" window reports a client area like 1279x724.
        if (aspect <= 0 || Math.Abs(aspect - Base) < 0.03 * Base)
            aspect = Base;
        return aspect;
    }

    /// The value the DLSS panel's slider saved (user\render-size.ini), 0 when none.
    public static double SavedScreenPercent()
    {
        var path = Path.Combine(AppPaths.UserDir, "render-size.ini");
        try
        {
            if (!File.Exists(path))
                return 0;
            foreach (var line in File.ReadLines(path))
                if (line.StartsWith("screen_percent=", StringComparison.Ordinal))
                {
                    var v = Atof(line[15..]);
                    return v > 0 ? Math.Clamp(v, 10, 200) : 0;
                }
        }
        catch (IOException)
        {
        }
        return 0;
    }

    public static double Atof(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0;
        // atof: the leading number only.
        var t = s.Trim();
        var end = 0;
        while (end < t.Length && (char.IsDigit(t[end]) || t[end] is '.' or '-' or '+'))
            end++;
        return double.TryParse(t[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    static int Round8(double v) => (int)Math.Round(v / 8.0, MidpointRounding.AwayFromZero) * 8;

    static RenderSize SizeAt(int h0, double aspect)
    {
        var narrow = aspect < Base - 1e-6;
        var w0 = Round8(h0 * 16.0 / 9.0);
        return narrow
            ? new RenderSize(w0, Math.Min(w0, Round8(w0 / aspect)))
            : new RenderSize(Math.Min(Round8(h0 * 32.0 / 9.0), Round8(h0 * aspect)), h0);
    }

    static int CapPixels(int h0, double aspect)
    {
        while (h0 > MinLines)
        {
            var s = SizeAt(h0, aspect);
            if ((long)s.Width * s.Height <= MaxPixels)
                break;
            h0 -= 8;
        }
        return h0;
    }

    static RenderSize Reference(double aspect, RenderSize display, RenderSize screen)
    {
        double dw = display.Width > 0 ? display.Width : screen.Width;
        double dh = display.Width > 0 ? display.Height : screen.Height;
        if (dw <= 0 || dh <= 0)
        {
            dw = 1920;
            dh = 1080;
        }
        return aspect >= dw / dh
            ? new RenderSize((int)dw, (int)Math.Round(dw / aspect, MidpointRounding.AwayFromZero))
            : new RenderSize((int)Math.Round(dh * aspect, MidpointRounding.AwayFromZero), (int)dh);
    }

    static int BaseHeightForPercent(double percent, double aspect, RenderSize display, RenderSize screen)
    {
        // 33 and 67 = DLSS's exact 1/3 and 2/3.
        var whole = (int)Math.Round(percent, MidpointRounding.AwayFromZero);
        var p = whole == 33 ? 1.0 / 3.0 : whole == 67 ? 2.0 / 3.0 : Math.Clamp(percent, 10, 200) / 100.0;
        var reference = Reference(aspect, display, screen);
        var full = Math.Min(reference.Height, reference.Width * 9.0 / 16.0);
        return CapPixels(Math.Clamp(Round8(full * p), MinLines, MaxLines), aspect);
    }
}
