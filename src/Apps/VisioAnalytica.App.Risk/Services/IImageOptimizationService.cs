namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Servicio para optimizar imágenes antes de enviarlas al servidor.
/// Redimensiona y comprime imágenes usando APIs nativas de cada plataforma.
/// </summary>
public interface IImageOptimizationService
{
    /// <summary>
    /// Optimiza una imagen redimensionándola y comprimiéndola según los parámetros.
    /// </summary>
    /// <param name="originalBytes">Bytes de la imagen original</param>
    /// <param name="maxWidth">Ancho máximo en píxeles (mantiene aspect ratio)</param>
    /// <param name="quality">Calidad de compresión JPEG (0-100)</param>
    /// <returns>Bytes de la imagen optimizada, o null si falla</returns>
    Task<byte[]?> OptimizeImageAsync(byte[] originalBytes, int maxWidth, int quality);

    /// <summary>
    /// Genera un thumbnail de una imagen.
    /// </summary>
    /// <param name="originalBytes">Bytes de la imagen original</param>
    /// <param name="thumbnailWidth">Ancho máximo del thumbnail en píxeles</param>
    /// <param name="quality">Calidad de compresión JPEG (0-100)</param>
    /// <returns>Bytes del thumbnail, o null si falla</returns>
    Task<byte[]?> GenerateThumbnailAsync(byte[] originalBytes, int thumbnailWidth, int quality);
}

