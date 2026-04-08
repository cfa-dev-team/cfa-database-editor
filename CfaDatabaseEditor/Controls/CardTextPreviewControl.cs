using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SkiaSharp;
using CfaDatabaseEditor.Services;

namespace CfaDatabaseEditor.Controls;

/// <summary>
/// Custom Avalonia control that renders card text on the Zatemnenie background
/// using the CFA text rendering pipeline.
/// </summary>
public class CardTextPreviewControl : Control
{
    private readonly CardTextRenderer _renderer = new();
    private bool _resourcesLoaded;

    public static readonly StyledProperty<string?> CardTextProperty =
        AvaloniaProperty.Register<CardTextPreviewControl, string?>(nameof(CardText));

    public static readonly StyledProperty<string?> CardNameProperty =
        AvaloniaProperty.Register<CardTextPreviewControl, string?>(nameof(CardName));

    public static readonly StyledProperty<bool> ExtendedTextBoxProperty =
        AvaloniaProperty.Register<CardTextPreviewControl, bool>(nameof(ExtendedTextBox));

    public string? CardText
    {
        get => GetValue(CardTextProperty);
        set => SetValue(CardTextProperty, value);
    }

    public string? CardName
    {
        get => GetValue(CardNameProperty);
        set => SetValue(CardNameProperty, value);
    }

    public bool ExtendedTextBox
    {
        get => GetValue(ExtendedTextBoxProperty);
        set => SetValue(ExtendedTextBoxProperty, value);
    }

    static CardTextPreviewControl()
    {
        AffectsRender<CardTextPreviewControl>(CardTextProperty, CardNameProperty, ExtendedTextBoxProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Draw background
        context.DrawRectangle(Brushes.White, null, new Rect(0, 0, bounds.Width, bounds.Height));

        if (string.IsNullOrEmpty(CardText) && string.IsNullOrEmpty(CardName)) return;

        // Load resources on first render
        if (!_resourcesLoaded)
        {
            TryLoadResources();
            _resourcesLoaded = true;
        }

        if (!_renderer.IsLoaded)
        {
            // Fallback: draw plain text
            var typeface = new Typeface("Georgia, serif");
            var text = (CardName ?? "") + "\n" + (CardText ?? "");
            var formattedText = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, 12, Brushes.Black);
            context.DrawText(formattedText, new Point(4, 4));
            return;
        }

        // Render at 300px wide (native card width), then scale to fit control
        int renderWidth = 300;
        float yScale = ExtendedTextBox ? 1.2f : 1f;
        int renderHeight = (int)(428 * yScale);

        using var surface = SKSurface.Create(new SKImageInfo(renderWidth, renderHeight));
        if (surface == null) return;

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        _renderer.RenderFull(canvas, CardName ?? "", CardText ?? "", ExtendedTextBox);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        if (data != null)
        {
            using var stream = new MemoryStream(data.ToArray());
            var avBitmap = new Avalonia.Media.Imaging.Bitmap(stream);

            // Scale to fit the control width while maintaining aspect ratio
            double scale = bounds.Width / renderWidth;
            double drawHeight = renderHeight * scale;
            context.DrawImage(avBitmap, new Rect(0, 0, bounds.Width, drawHeight));
        }
    }

    private void TryLoadResources()
    {
        var exePath = AppDomain.CurrentDomain.BaseDirectory;
        var assetsPath = Path.Combine(exePath, "Assets");

        if (!Directory.Exists(Path.Combine(assetsPath, "Icons")))
        {
            var dir = new DirectoryInfo(exePath);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Assets");
                if (Directory.Exists(Path.Combine(candidate, "Icons")))
                {
                    assetsPath = candidate;
                    break;
                }
                dir = dir.Parent;
            }
        }

        if (Directory.Exists(assetsPath))
            _renderer.LoadResources(assetsPath);
    }
}
