using System.Runtime.Versioning;
#if ANDROID
using Android.Graphics;
#elif IOS || MACCATALYST
using UIKit;
using CoreGraphics;
using Foundation;
#elif WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using SD = System.Drawing;
using SDI = System.Drawing.Imaging;
#endif

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Implementación del servicio de optimización de imágenes usando APIs nativas de cada plataforma.
/// Sin paquetes externos - usa código específico de plataforma.
/// </summary>
public class ImageOptimizationService : IImageOptimizationService
{
    public async Task<byte[]?> OptimizeImageAsync(byte[] originalBytes, int maxWidth, int quality)
    {
        try
        {
            if (originalBytes == null || originalBytes.Length == 0)
            {
                return null;
            }

#if ANDROID
            return await OptimizeImageAndroidAsync(originalBytes, maxWidth, quality);
#elif IOS || MACCATALYST
            return await OptimizeImageIOSAsync(originalBytes, maxWidth, quality);
#elif WINDOWS
            return await OptimizeImageWindowsAsync(originalBytes, maxWidth, quality);
#else
            // Para otras plataformas, devolver original
            System.Diagnostics.Debug.WriteLine("⚠️ Optimización de imágenes no disponible en esta plataforma");
            return originalBytes;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error al optimizar imagen: {ex.Message}");
            return originalBytes;
        }
    }

    public async Task<byte[]?> GenerateThumbnailAsync(byte[] originalBytes, int thumbnailWidth, int quality)
    {
        try
        {
            if (originalBytes == null || originalBytes.Length == 0)
            {
                return null;
            }

#if ANDROID
            return await OptimizeImageAndroidAsync(originalBytes, thumbnailWidth, quality);
#elif IOS || MACCATALYST
            return await OptimizeImageIOSAsync(originalBytes, thumbnailWidth, quality);
#elif WINDOWS
            return await OptimizeImageWindowsAsync(originalBytes, thumbnailWidth, quality);
#else
            return null;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error al generar thumbnail: {ex.Message}");
            return null;
        }
    }

#if ANDROID
    [SupportedOSPlatform("android")]
    private Task<byte[]?> OptimizeImageAndroidAsync(byte[] originalBytes, int maxWidth, int quality)
    {
        return Task.Run<byte[]?>(() =>
        {
            try
            {
                // Usar Android.Graphics.Bitmap (API nativa de Android, sin paquetes externos)
                using var originalBitmap = BitmapFactory.DecodeByteArray(originalBytes, 0, originalBytes.Length);
                
                if (originalBitmap == null)
                {
                    return originalBytes;
                }

                var originalWidth = originalBitmap.Width;
                var originalHeight = originalBitmap.Height;

                // Si la imagen es más pequeña que el máximo, no redimensionar
                if (originalWidth <= maxWidth)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Imagen ya es pequeña ({originalWidth}x{originalHeight}), no se redimensiona");
                    return originalBytes;
                }

                // Calcular nuevo tamaño manteniendo aspect ratio
                var aspectRatio = (float)originalHeight / originalWidth;
                var newWidth = maxWidth;
                var newHeight = (int)(maxWidth * aspectRatio);

                System.Diagnostics.Debug.WriteLine($"📐 Redimensionando: {originalWidth}x{originalHeight} -> {newWidth}x{newHeight}");

                // Redimensionar usando Android Graphics
                using var resizedBitmap = Bitmap.CreateScaledBitmap(
                    originalBitmap, 
                    newWidth, 
                    newHeight, 
                    filter: true);

                // Comprimir a JPEG con calidad especificada
                using var outputStream = new MemoryStream();
                if (resizedBitmap != null)
                {
                    resizedBitmap.Compress(Bitmap.CompressFormat.Jpeg!, quality, outputStream);
                }

                var optimizedBytes = outputStream.ToArray();
                var reductionPercent = 100 - (optimizedBytes.Length * 100 / originalBytes.Length);
                
                System.Diagnostics.Debug.WriteLine($"✅ Imagen optimizada: {originalBytes.Length / 1024}KB -> {optimizedBytes.Length / 1024}KB ({reductionPercent}% reducción)");
                
                return optimizedBytes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al optimizar imagen en Android: {ex.Message}");
                return originalBytes;
            }
        });
    }
#endif

#if IOS || MACCATALYST
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("maccatalyst")]
    private Task<byte[]?> OptimizeImageIOSAsync(byte[] originalBytes, int maxWidth, int quality)
    {
        return Task.Run<byte[]?>(() =>
        {
            try
            {
                // Usar UIKit.UIImage (API nativa de iOS, sin paquetes externos)
                using var nsData = NSData.FromArray(originalBytes);
                using var originalImage = UIImage.LoadFromData(nsData);
                
                if (originalImage == null)
                {
                    return originalBytes;
                }

                var originalWidth = (int)originalImage.Size.Width;
                var originalHeight = (int)originalImage.Size.Height;

                // Si la imagen es más pequeña que el máximo, no redimensionar
                if (originalWidth <= maxWidth)
                {
                    return originalBytes;
                }

                // Calcular nuevo tamaño manteniendo aspect ratio
                var aspectRatio = (float)originalHeight / originalWidth;
                var newWidth = maxWidth;
                var newHeight = (int)(maxWidth * aspectRatio);

                // Redimensionar usando UIGraphicsImageRenderer (iOS 10+)
                var size = new CGSize(newWidth, newHeight);
                var renderer = new UIGraphicsImageRenderer(size);
                var resizedImage = renderer.CreateImage((context) =>
                {
                    originalImage.Draw(new CGRect(0, 0, newWidth, newHeight));
                });

                if (resizedImage == null)
                {
                    return originalBytes;
                }

                // Convertir a JPEG con calidad especificada
                using var jpegData = resizedImage.AsJPEG(quality / 100f);
                var optimizedBytes = jpegData.ToArray();

                return optimizedBytes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al optimizar imagen en iOS: {ex.Message}");
                return originalBytes;
            }
        });
    }
#endif

#if WINDOWS
    [SupportedOSPlatform("windows")]
    private Task<byte[]?> OptimizeImageWindowsAsync(byte[] originalBytes, int maxWidth, int quality)
    {
        return Task.Run<byte[]?>(() =>
        {
            try
            {
                // Usar System.Drawing.Common (disponible en Windows, parte del framework .NET)
                using var originalImage = SD.Image.FromStream(new MemoryStream(originalBytes));
                
                var originalWidth = originalImage.Width;
                var originalHeight = originalImage.Height;

                // Si la imagen es más pequeña que el máximo, no redimensionar
                if (originalWidth <= maxWidth)
                {
                    return originalBytes;
                }

                // Calcular nuevo tamaño manteniendo aspect ratio
                var aspectRatio = (float)originalHeight / originalWidth;
                var newWidth = maxWidth;
                var newHeight = (int)(maxWidth * aspectRatio);

                // Redimensionar
                using var resizedImage = new Bitmap(newWidth, newHeight);
                using var graphics = Graphics.FromImage(resizedImage);
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);

                // Comprimir a JPEG con calidad especificada
                var jpegCodec = ImageCodecInfo.GetImageDecoders()
                    .FirstOrDefault(c => c.FormatID == SDI.ImageFormat.Jpeg.Guid);
                
                if (jpegCodec == null)
                {
                    return originalBytes;
                }

                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(
                    Encoder.Quality, quality);

                using var outputStream = new MemoryStream();
                resizedImage.Save(outputStream, jpegCodec, encoderParams);
                
                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al optimizar imagen en Windows: {ex.Message}");
                return originalBytes;
            }
        });
    }
#endif
}

