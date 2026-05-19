using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Chat.Shared.Constants;

namespace ChatClient.Services;

/// <summary>
/// Service for compressing images before sending and creating thumbnails for display.
/// Uses WPF's built-in imaging pipeline (no external dependencies needed).
/// </summary>
public static class ImageService
{
    /// <summary>
    /// Compress and resize an image file for sending over the network.
    /// Returns the compressed JPEG bytes.
    /// </summary>
    public static async Task<byte[]> CompressImageAsync(string sourcePath)
    {
        return await Task.Run(() =>
        {
            BitmapImage original = new();
            original.BeginInit();
            original.UriSource = new Uri(sourcePath, UriKind.Absolute);
            original.CacheOption = BitmapCacheOption.OnLoad;
            original.EndInit();
            original.Freeze();

            // Determine if resize is needed
            double scale = 1.0;
            int maxDim = Math.Max(original.PixelWidth, original.PixelHeight);
            if (maxDim > AppConstants.MaxImageDimension)
            {
                scale = (double)AppConstants.MaxImageDimension / maxDim;
            }

            BitmapSource source;
            if (scale < 1.0)
            {
                source = new TransformedBitmap(original, new ScaleTransform(scale, scale));
            }
            else
            {
                source = original;
            }

            // Encode as JPEG
            JpegBitmapEncoder encoder = new()
            {
                QualityLevel = AppConstants.ImageQuality
            };
            encoder.Frames.Add(BitmapFrame.Create(source));

            using MemoryStream ms = new();
            encoder.Save(ms);
            return ms.ToArray();
        });
    }

    /// <summary>
    /// Create a BitmapImage thumbnail from raw image bytes for display in the chat.
    /// </summary>
    public static BitmapImage CreateThumbnail(byte[] imageData, int maxSize = 300)
    {
        BitmapImage thumbnail = new();
        thumbnail.BeginInit();
        thumbnail.StreamSource = new MemoryStream(imageData);
        thumbnail.CacheOption = BitmapCacheOption.OnLoad;
        thumbnail.DecodePixelWidth = maxSize;
        thumbnail.EndInit();
        thumbnail.Freeze();
        return thumbnail;
    }

    /// <summary>
    /// Save raw image bytes to a file.
    /// </summary>
    public static async Task<string> SaveImageAsync(byte[] imageData, string fileName, string saveDir)
    {
        Directory.CreateDirectory(saveDir);
        string filePath = Path.Combine(saveDir, fileName);

        // Avoid overwriting
        int counter = 1;
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        while (File.Exists(filePath))
        {
            filePath = Path.Combine(saveDir, $"{baseName}_{counter}{ext}");
            counter++;
        }

        await File.WriteAllBytesAsync(filePath, imageData);
        return filePath;
    }
}
