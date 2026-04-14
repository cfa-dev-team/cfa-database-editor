using SkiaSharp;

namespace CfaDatabaseEditor.Services;

public class ImageService
{
    private readonly DatabaseService _db;

    public ImageService(DatabaseService db)
    {
        _db = db;
    }

    /// <summary>
    /// Imports a card image, resizes it for both CardSprite (300px) and CardSpriteMini2 (75px),
    /// and saves as JPEG.
    /// </summary>
    public void ImportCardImage(int cardStat, string sourcePath)
    {
        if (_db.CardSpritePath == null || _db.CardSpriteMini2Path == null)
            throw new InvalidOperationException("Database not loaded");

        using var original = SKBitmap.Decode(sourcePath)
            ?? throw new InvalidOperationException($"Could not decode image: {sourcePath}");

        // CardSprite - 300px wide
        SaveResized(original, 300,
            Path.Combine(_db.CardSpritePath, $"n{cardStat}.jpg"));

        // CardSpriteMini2 - 75px wide
        SaveResized(original, 75,
            Path.Combine(_db.CardSpriteMini2Path, $"n{cardStat}.jpg"));
    }

    /// <summary>
    /// Imports a card image from a byte array (for EN sync downloads).
    /// </summary>
    public void ImportCardImageFromBytes(int cardStat, byte[] imageData)
    {
        if (_db.CardSpritePath == null || _db.CardSpriteMini2Path == null)
            throw new InvalidOperationException("Database not loaded");

        using var original = SKBitmap.Decode(imageData)
            ?? throw new InvalidOperationException("Could not decode image data");

        SaveResized(original, 300,
            Path.Combine(_db.CardSpritePath, $"n{cardStat}.jpg"));
        SaveResized(original, 75,
            Path.Combine(_db.CardSpriteMini2Path, $"n{cardStat}.jpg"));
    }

    /// <summary>
    /// If the image has a horizontal aspect ratio (width > height), rotates it 90 degrees
    /// counter-clockwise and returns the re-encoded bytes. Otherwise returns the input unchanged.
    /// </summary>
    public static byte[] FixRotationIfNeeded(byte[] imageData)
    {
        using var bitmap = SKBitmap.Decode(imageData);
        if (bitmap == null || bitmap.Width <= bitmap.Height)
            return imageData;

        // Rotate 90 degrees counter-clockwise
        var rotated = new SKBitmap(bitmap.Height, bitmap.Width);
        using (var canvas = new SKCanvas(rotated))
        {
            canvas.Translate(0, bitmap.Width);
            canvas.RotateDegrees(-90);
            canvas.DrawBitmap(bitmap, 0, 0);
        }

        using var image = SKImage.FromBitmap(rotated);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        rotated.Dispose();
        return data.ToArray();
    }

    private static void SaveResized(SKBitmap original, int targetWidth, string outputPath)
    {
        float ratio = (float)targetWidth / original.Width;
        int targetHeight = (int)(original.Height * ratio);

        using var resized = original.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.High);
        if (resized == null) return;

        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }
}
