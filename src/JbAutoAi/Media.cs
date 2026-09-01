using System.Globalization;
using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using UglyToad.PdfPig;

namespace JbAutoAi;

/// Local pre-processing that must not depend on any AI service: content hashing,
/// perceptual hashing for the recycled-photo signal, EXIF forensics, PDF text.
/// Every method here is failure-tolerant — a corrupt upload degrades a signal,
/// it never breaks intake.
public static class Media
{
    public static readonly HashSet<string> PhotoExts = [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"];
    public static readonly HashSet<string> PdfExts = [".pdf"];
    public static readonly HashSet<string> EmailExts = [".eml", ".txt", ".msg"];

    public static string ClassifyByExtension(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        if (PhotoExts.Contains(ext)) return "photo";
        if (PdfExts.Contains(ext)) return "pdf";
        if (EmailExts.Contains(ext)) return "email";
        return "other";
    }

    /// Allow-list at the trust boundary: intake takes claim artifacts, not arbitrary
    /// files. Anything outside the list is rejected rather than stored and served.
    public static bool IsAcceptedUpload(string filename) =>
        ClassifyByExtension(filename) != "other";

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    const int HashSize = 8;              // 64-bit hash
    const int ImageSize = HashSize * 4;  // 32×32 DCT input, same as the imagehash default

    static readonly double[,] Cos = BuildCosTable();

    static double[,] BuildCosTable()
    {
        var t = new double[ImageSize, ImageSize];
        for (var k = 0; k < ImageSize; k++)
            for (var n = 0; n < ImageSize; n++)
                t[k, n] = Math.Cos(Math.PI * k * (2 * n + 1) / (2.0 * ImageSize));
        return t;
    }

    /// Perceptual hash (DCT-based, the standard pHash construction): 32×32 grey,
    /// 2-D DCT-II, keep the top-left 8×8 low-frequency block, threshold at its
    /// median. Resistant to re-compression and rescaling, which is exactly what a
    /// recycled claim photo has been through.
    public static string? PerceptualHash(byte[] imageBytes)
    {
        try
        {
            using var img = Image.Load<L8>(imageBytes);
            img.Mutate(x => x.Resize(ImageSize, ImageSize));

            var pixels = new double[ImageSize, ImageSize];
            for (var y = 0; y < ImageSize; y++)
                for (var x = 0; x < ImageSize; x++)
                    pixels[y, x] = img[x, y].PackedValue;

            // Separable DCT-II: rows, then columns.
            var rows = new double[ImageSize, ImageSize];
            for (var y = 0; y < ImageSize; y++)
                for (var u = 0; u < ImageSize; u++)
                {
                    var s = 0.0;
                    for (var x = 0; x < ImageSize; x++) s += pixels[y, x] * Cos[u, x];
                    rows[y, u] = s;
                }

            var dct = new double[HashSize, HashSize];
            var low = new List<double>(HashSize * HashSize);
            for (var v = 0; v < HashSize; v++)
                for (var u = 0; u < HashSize; u++)
                {
                    var s = 0.0;
                    for (var y = 0; y < ImageSize; y++) s += rows[y, u] * Cos[v, y];
                    dct[v, u] = s;
                    low.Add(s);
                }

            low.Sort();
            var median = (low[low.Count / 2 - 1] + low[low.Count / 2]) / 2.0;

            // Row-major, MSB first — 16 hex chars.
            var bits = 0UL;
            for (var v = 0; v < HashSize; v++)
                for (var u = 0; u < HashSize; u++)
                    bits = (bits << 1) | (dct[v, u] > median ? 1UL : 0UL);

            return bits.ToString("x16");
        }
        catch
        {
            return null;
        }
    }

    /// EXIF forensics (FR-7.1). Missing EXIF means a screenshot or re-save; a
    /// capture date far from the reported loss date is a stronger signal.
    public static List<Rules.Signal> PhotoExifSignals(byte[] imageBytes, DateOnly? lossDate)
    {
        var signals = new List<Rules.Signal>();
        try
        {
            var info = Image.Identify(imageBytes);
            var exif = info.Metadata.ExifProfile;

            string? raw = null;
            if (exif is not null)
            {
                if (exif.TryGetValue(ExifTag.DateTimeOriginal, out var original)) raw = original?.Value;
                if (string.IsNullOrWhiteSpace(raw) && exif.TryGetValue(ExifTag.DateTimeDigitized, out var digitized))
                    raw = digitized?.Value;
                if (string.IsNullOrWhiteSpace(raw) && exif.TryGetValue(ExifTag.DateTime, out var dt))
                    raw = dt?.Value;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                signals.Add(new("PHOTO_NO_EXIF", "low",
                    "Photo has no EXIF timestamp — screenshot or re-saved image."));
                return signals;
            }

            // EXIF DateTime is "YYYY:MM:DD HH:MM:SS".
            if (!DateOnly.TryParseExact(raw[..Math.Min(10, raw.Length)], "yyyy:MM:dd",
                                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var taken))
                return signals;

            if (lossDate is not { } loss) return signals;

            var delta = Math.Abs(taken.DayNumber - loss.DayNumber);
            if (delta > 3)
                signals.Add(new("PHOTO_EXIF_DATE_MISMATCH", delta > 30 ? "high" : "medium",
                    $"Photo taken {taken:yyyy-MM-dd} vs loss date {loss:yyyy-MM-dd} (Δ {delta} days)."));
        }
        catch
        {
            // Never let EXIF checks break the upload flow.
        }
        return signals;
    }

    public static string PdfText(byte[] pdfBytes)
    {
        try
        {
            using var doc = PdfDocument.Open(pdfBytes);
            return string.Join("\n", doc.GetPages().Select(p => p.Text));
        }
        catch (Exception e)
        {
            // Some intake channels send text files with a .pdf extension. Fall back
            // to a plain read before admitting defeat.
            try
            {
                var text = System.Text.Encoding.UTF8.GetString(pdfBytes);
                if (text.Length > 0 && !text.Contains('\0')) return text;
            }
            catch { /* fall through */ }
            return $"(pdf parse error: {e.Message})";
        }
    }
}
