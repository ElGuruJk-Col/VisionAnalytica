using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace VisioAnalytica.Infrastructure.Services;

/// <summary>
/// Servicio para optimizar imágenes en el servidor usando System.Drawing.Common.
/// Sin paquetes externos - usa APIs nativas de .NET.
/// Nota: System.Drawing.Common solo está disponible en Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public class ServerImageOptimizationService
{
    private readonly ILogger<ServerImageOptimizationService> _logger;

    public ServerImageOptimizationService(ILogger<ServerImageOptimizationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Optimiza una imagen redimensionándola y comprimiéndola según los parámetros.
    /// </summary>
    /// <param name="originalBytes">Bytes de la imagen original</param>
    /// <param name="maxWidth">Ancho máximo en píxeles (mantiene aspect ratio)</param>
    /// <param name="quality">Calidad de compresión JPEG (0-100)</param>
    /// <returns>Bytes de la imagen optimizada, o null si falla</returns>
    [SupportedOSPlatform("windows")]
    public byte[]? OptimizeImage(byte[] originalBytes, int maxWidth, int quality)
    {
        try
        {
            if (originalBytes == null || originalBytes.Length == 0)
            {
                return null;
            }

            using var originalStream = new MemoryStream(originalBytes);
            using var originalImage = Image.FromStream(originalStream);

            var originalWidth = originalImage.Width;
            var originalHeight = originalImage.Height;

            // Si la imagen es más pequeña que el máximo, no redimensionar
            if (originalWidth <= maxWidth)
            {
                _logger.LogDebug("Imagen ya es pequeña ({Width}x{Height}), no se redimensiona", originalWidth, originalHeight);
                return originalBytes;
            }

            // Calcular nuevo tamaño manteniendo aspect ratio
            var aspectRatio = (float)originalHeight / originalWidth;
            var newWidth = maxWidth;
            var newHeight = (int)(maxWidth * aspectRatio);

            _logger.LogDebug("Redimensionando: {OriginalWidth}x{OriginalHeight} -> {NewWidth}x{NewHeight}", 
                originalWidth, originalHeight, newWidth, newHeight);

            // Redimensionar
            using var resizedImage = new Bitmap(newWidth, newHeight);
            using var graphics = Graphics.FromImage(resizedImage);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);

            // Comprimir a JPEG con calidad especificada
            var jpegCodec = ImageCodecInfo.GetImageDecoders()
                .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

            if (jpegCodec == null)
            {
                _logger.LogWarning("No se encontró codec JPEG, devolviendo imagen original");
                return originalBytes;
            }

            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            using var outputStream = new MemoryStream();
            resizedImage.Save(outputStream, jpegCodec, encoderParams);

            var optimizedBytes = outputStream.ToArray();
            var reductionPercent = 100 - (optimizedBytes.Length * 100 / originalBytes.Length);

            _logger.LogInformation(
                "Imagen optimizada: {OriginalSize}KB -> {OptimizedSize}KB ({ReductionPercent}% reducción)",
                originalBytes.Length / 1024, optimizedBytes.Length / 1024, reductionPercent);

            return optimizedBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al optimizar imagen");
            return originalBytes; // Devolver original si falla
        }
    }

    /// <summary>
    /// Genera un thumbnail de una imagen.
    /// </summary>
    /// <param name="originalBytes">Bytes de la imagen original</param>
    /// <param name="thumbnailWidth">Ancho máximo del thumbnail en píxeles</param>
    /// <param name="quality">Calidad de compresión JPEG (0-100)</param>
    /// <returns>Bytes del thumbnail, o null si falla</returns>
    [SupportedOSPlatform("windows")]
    public byte[]? GenerateThumbnail(byte[] originalBytes, int thumbnailWidth, int quality)
    {
        try
        {
            if (originalBytes == null || originalBytes.Length == 0)
            {
                return null;
            }

            using var originalStream = new MemoryStream(originalBytes);
            using var originalImage = Image.FromStream(originalStream);

            var originalWidth = originalImage.Width;
            var originalHeight = originalImage.Height;

            // Calcular nuevo tamaño manteniendo aspect ratio
            var aspectRatio = (float)originalHeight / originalWidth;
            var newWidth = Math.Min(thumbnailWidth, originalWidth);
            var newHeight = (int)(newWidth * aspectRatio);

            _logger.LogDebug("Generando thumbnail: {OriginalWidth}x{OriginalHeight} -> {NewWidth}x{NewHeight}",
                originalWidth, originalHeight, newWidth, newHeight);

            // Redimensionar
            using var thumbnail = new Bitmap(newWidth, newHeight);
            using var graphics = Graphics.FromImage(thumbnail);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);

            // Comprimir a JPEG con calidad especificada
            var jpegCodec = ImageCodecInfo.GetImageDecoders()
                .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

            if (jpegCodec == null)
            {
                _logger.LogWarning("No se encontró codec JPEG para thumbnail");
                return null;
            }

            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            using var outputStream = new MemoryStream();
            thumbnail.Save(outputStream, jpegCodec, encoderParams);

            var thumbnailBytes = outputStream.ToArray();
            _logger.LogDebug("Thumbnail generado: {Size}KB", thumbnailBytes.Length / 1024);

            return thumbnailBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar thumbnail");
            return null;
        }
    }
}

