using System.IO;
using System.Windows;
using System.Windows.Media;

namespace FreeHarbor.Tray.Ui;

/// <summary>
/// Writes a window to a PNG by rendering it, not by photographing the screen (DD22).
/// </summary>
/// <remarks>
/// Its own file since DD301: rendering a window is what <c>--capture-window</c> does with the shell,
/// not part of the shell's chrome, and the shell's size is a guard on what the chrome costs.
/// </remarks>
internal static class WindowSnapshot
{
    /// <summary>Render a window's content to a PNG.</summary>
    /// <param name="window">The window, shown, at whatever position.</param>
    /// <param name="path">Where to write.</param>
    /// <param name="scale">Device-independent pixels per pixel.</param>
    /// <returns>The size written, in pixels.</returns>
    /// <remarks>
    /// A <c>RenderTargetBitmap</c> over this window's own content has nothing else in the frame, which
    /// is the whole point: the screen copy it replaces read whatever was actually inside the window's
    /// rectangle, and twice that was somebody else's editor and messaging app. Rendering cannot
    /// photograph a thing that is not in this visual tree.
    ///
    /// The opaque background is not decoration. A Fluent window's backdrop is drawn by the system
    /// behind the window and is not part of the visual tree, so a render of the content alone comes
    /// back as light text on transparency — borrowed from claude-tray, which found the same thing.
    /// </remarks>
    internal static (int Width, int Height) Save(Window window, string path, double scale = 1.5)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(scale, 0);

        // Let layout finish before measuring it. Without this the first capture of a window is its
        // pre-arrange size, which is a picture of nothing at 0x0.
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

        var target = (FrameworkElement)window.Content;

        // The content's own margin is part of its offset inside the window, and rendering the element
        // directly draws it at that offset — so the bitmap loses a strip off the right and bottom equal
        // to the margin, and the About button came back as "Abou". Measured by looking at the first
        // capture this verb produced. Drawing it through a VisualBrush into a rectangle at the origin
        // places it by the rectangle instead of by its layout, which is what makes the frame the whole
        // content and nothing less.
        var margin = target.Margin;
        var wide = target.ActualWidth + margin.Left + margin.Right;
        var tall = target.ActualHeight + margin.Top + margin.Bottom;
        if (wide <= 0 || tall <= 0)
        {
            throw new InvalidOperationException(
                $"the window measured {target.ActualWidth}x{target.ActualHeight}, so there is "
                + "nothing to render: it was captured before it was laid out");
        }

        // Both names below are ambiguous unqualified: enabling WinForms puts System.Drawing in the
        // implicit usings, so Brush and Color resolve to the GDI+ pair as readily as the WPF one.
        var backdrop =
            window.TryFindResource("SolidBackgroundFillColorBaseBrush") as System.Windows.Media.Brush
            ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x20, 0x20, 0x20));

        var whole = new Rect(0, 0, wide, tall);
        var visual = new DrawingVisual();
        using (var canvas = visual.RenderOpen())
        {
            // A Fluent window's backdrop is drawn by the system behind the window and is not in the
            // visual tree, so without this the render is light text on transparency.
            canvas.DrawRectangle(backdrop, null, whole);

            // DD328: the viewbox is the window's client rectangle, stated and not left to the brush.
            // The brush draws the content at its own offset, which is its margin, so the client
            // rectangle starts at the origin. By default a VisualBrush frames its visual's descendant
            // bounds instead, and the water's negative margin reaches 20 px past the grid on both
            // sides: those bounds started at the client's left edge, the rectangle they were drawn
            // into started a margin further in, and every capture came out shifted right by the margin
            // with its right edge cut off. Read as the page lacking a right inset, which it never did.
            canvas.DrawRectangle(
                new VisualBrush(target)
                {
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = whole,
                },
                null,
                whole);
        }

        var width = (int)(wide * scale);
        var height = (int)(tall * scale);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var file = File.Create(path);
        encoder.Save(file);
        return (width, height);
    }
}
