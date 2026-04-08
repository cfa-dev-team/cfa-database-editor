using SkiaSharp;

namespace CfaDatabaseEditor.Services;

/// <summary>
/// Perceptual image matching using difference hash (dHash) implemented with SkiaSharp.
/// No OpenCV dependency needed - works cross-platform.
/// </summary>
public class ImageMatcherService
{
    private const int HashSize = 16; // 16x16 = 256-bit hash for good accuracy
    private readonly Dictionary<int, ulong[]> _hashCache = new();

    /// <summary>
    /// Pre-computes perceptual hashes for all card images in the database.
    /// </summary>
    public async Task BuildIndexAsync(string cardSpritePath, IProgress<string>? progress = null)
    {
        _hashCache.Clear();

        var files = Directory.GetFiles(cardSpritePath, "n*.jpg");
        int count = 0;

        await Task.Run(() =>
        {
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith("n") && int.TryParse(fileName.Substring(1), out int cardStat))
                {
                    var hash = ComputeDHash(file);
                    if (hash != null)
                    {
                        lock (_hashCache)
                            _hashCache[cardStat] = hash;
                    }
                }
                count++;
                if (count % 500 == 0)
                    progress?.Report($"Indexed {count}/{files.Length} images...");
            }
        });

        progress?.Report($"Index complete: {_hashCache.Count} images.");
    }

    /// <summary>
    /// Finds the best matching card for the given image data.
    /// Returns (cardStat, confidence%) or null if no match.
    /// </summary>
    public (int CardStat, double Confidence)? FindBestMatch(byte[] imageData)
    {
        var queryHash = ComputeDHashFromBytes(imageData);
        if (queryHash == null || _hashCache.Count == 0)
            return null;

        int bestCardStat = -1;
        int bestDistance = int.MaxValue;

        foreach (var (cardStat, hash) in _hashCache)
        {
            int distance = HammingDistance(queryHash, hash);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCardStat = cardStat;
            }
        }

        if (bestCardStat < 0) return null;

        // Convert Hamming distance to confidence percentage
        // Total bits = HashSize * (HashSize - 1) for dHash (each row contributes HashSize-1 bits)
        int totalBits = HashSize * (HashSize - 1) * 2; // horizontal + vertical
        double confidence = (1.0 - (double)bestDistance / totalBits) * 100;

        return (bestCardStat, confidence);
    }

    /// <summary>
    /// Computes a difference hash (dHash) from a file path.
    /// Uses both horizontal and vertical gradients for better accuracy.
    /// </summary>
    private ulong[]? ComputeDHash(string filePath)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(filePath);
            return ComputeDHashFromBitmap(bitmap);
        }
        catch
        {
            return null;
        }
    }

    private ulong[]? ComputeDHashFromBytes(byte[] data)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(data);
            return ComputeDHashFromBitmap(bitmap);
        }
        catch
        {
            return null;
        }
    }

    private ulong[]? ComputeDHashFromBitmap(SKBitmap? bitmap)
    {
        if (bitmap == null) return null;

        // Resize to (HashSize+1) x (HashSize+1) for gradient computation
        int size = HashSize + 1;
        using var resized = bitmap.Resize(new SKImageInfo(size, size), SKFilterQuality.Low);
        if (resized == null) return null;

        // Convert to grayscale values
        var gray = new byte[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var pixel = resized.GetPixel(x, y);
                gray[y, x] = (byte)(0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue);
            }
        }

        // Compute horizontal dHash: compare pixel to right neighbor
        // HashSize rows * HashSize columns = HashSize^2 bits
        // We need ceil(bits/64) ulongs
        int hBits = HashSize * HashSize;
        int vBits = HashSize * HashSize;
        int totalUlongs = (hBits + vBits + 63) / 64;
        var hash = new ulong[totalUlongs];

        int bitIndex = 0;

        // Horizontal gradient
        for (int y = 0; y < HashSize; y++)
        {
            for (int x = 0; x < HashSize; x++)
            {
                if (gray[y, x] < gray[y, x + 1])
                    hash[bitIndex / 64] |= 1UL << (bitIndex % 64);
                bitIndex++;
            }
        }

        // Vertical gradient
        for (int y = 0; y < HashSize; y++)
        {
            for (int x = 0; x < HashSize; x++)
            {
                if (gray[y, x] < gray[y + 1, x])
                    hash[bitIndex / 64] |= 1UL << (bitIndex % 64);
                bitIndex++;
            }
        }

        return hash;
    }

    private static int HammingDistance(ulong[] a, ulong[] b)
    {
        int distance = 0;
        int len = Math.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            distance += BitCount(a[i] ^ b[i]);
        }
        return distance;
    }

    private static int BitCount(ulong value)
    {
        // Brian Kernighan's algorithm
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }
        return count;
    }
}
