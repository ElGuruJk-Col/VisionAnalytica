namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Contrato para el almacenamiento de archivos (imágenes).
    /// Permite abstraer el proveedor de almacenamiento (Azure Blob, Local, etc.)
    /// </summary>
    public interface IFileStorage
    {
        /// <summary>
        /// Guarda una imagen y devuelve su URL o ruta de acceso.
        /// </summary>
        /// <param name="imageBytes">Los bytes de la imagen</param>
        /// <param name="fileName">Nombre sugerido del archivo (opcional)</param>
        /// <param name="organizationId">ID de la organización (para multi-tenancy)</param>
        /// <returns>La URL o ruta donde se guardó la imagen</returns>
        Task<string> SaveImageAsync(byte[] imageBytes, string? fileName = null, Guid? organizationId = null);
        
        /// <summary>
        /// Lee una imagen del almacenamiento y devuelve sus bytes.
        /// </summary>
        /// <param name="imageUrl">La URL o ruta de la imagen a leer</param>
        /// <returns>Los bytes de la imagen, o null si no se pudo leer</returns>
        Task<byte[]?> ReadImageAsync(string imageUrl);
        
        /// <summary>
        /// Guarda un thumbnail de una imagen y devuelve su URL o ruta de acceso.
        /// </summary>
        /// <param name="thumbnailBytes">Los bytes del thumbnail</param>
        /// <param name="originalFileName">Nombre del archivo original (para generar nombre del thumbnail)</param>
        /// <param name="organizationId">ID de la organización (para multi-tenancy)</param>
        /// <returns>La URL o ruta donde se guardó el thumbnail</returns>
        Task<string> SaveThumbnailAsync(byte[] thumbnailBytes, string originalFileName, Guid? organizationId = null);
        
        /// <summary>
        /// Elimina una imagen del almacenamiento.
        /// </summary>
        /// <param name="imageUrl">La URL o ruta de la imagen a eliminar</param>
        /// <returns>True si se eliminó correctamente, False en caso contrario</returns>
        Task<bool> DeleteImageAsync(string imageUrl);
    }
}

