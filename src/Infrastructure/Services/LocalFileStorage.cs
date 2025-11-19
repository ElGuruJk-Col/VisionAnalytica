using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using VisioAnalytica.Core.Interfaces;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Implementación de IFileStorage usando el sistema de archivos local.
    /// Ideal para desarrollo y demos. En producción, usar AzureBlobStorage.
    /// </summary>
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _uploadsPath;
        private readonly ILogger<LocalFileStorage> _logger;
        private readonly IWebHostEnvironment _environment;

        public LocalFileStorage(IWebHostEnvironment environment, ILogger<LocalFileStorage> logger)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Definimos la ruta base: wwwroot/uploads o ContentRootPath/uploads
            // Si ContentRootPath está en bin/Debug, navegar hacia arriba hasta encontrar la carpeta del proyecto
            var basePath = _environment.WebRootPath ?? _environment.ContentRootPath;
            
            // Si ContentRootPath está en bin/Debug/net9.0, navegar hacia arriba
            if (basePath.Contains("bin" + Path.DirectorySeparatorChar + "Debug") || 
                basePath.Contains("bin" + Path.DirectorySeparatorChar + "Release"))
            {
                // Navegar hacia arriba desde bin/Debug/net9.0 hasta la raíz del proyecto Api
                var directory = new DirectoryInfo(basePath);
                while (directory != null && directory.Name != "Api" && directory.Parent != null)
                {
                    directory = directory.Parent;
                }
                if (directory != null && directory.Name == "Api")
                {
                    basePath = directory.FullName;
                }
            }
            
            _uploadsPath = Path.Combine(basePath, "uploads");
            
            // Aseguramos que la carpeta exista
            if (!Directory.Exists(_uploadsPath))
            {
                Directory.CreateDirectory(_uploadsPath);
                _logger.LogInformation("Carpeta de uploads creada en: {UploadsPath}", _uploadsPath);
            }
        }

        public async Task<string> SaveImageAsync(byte[] imageBytes, string? fileName = null, Guid? organizationId = null)
        {
            try
            {
                // 1. Generar nombre único si no se proporciona
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    var hash = ComputeHash(imageBytes);
                    fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{hash[..8]}.jpg";
                }
                else
                {
                    // Sanitizar el nombre del archivo
                    fileName = SanitizeFileName(fileName);
                }

                // 2. Construir la ruta completa
                string finalPath;
                if (organizationId.HasValue)
                {
                    // Organizar por organización: uploads/{orgId}/{filename}
                    var orgFolder = Path.Combine(_uploadsPath, organizationId.Value.ToString());
                    if (!Directory.Exists(orgFolder))
                    {
                        Directory.CreateDirectory(orgFolder);
                    }
                    finalPath = Path.Combine(orgFolder, fileName);
                }
                else
                {
                    finalPath = Path.Combine(_uploadsPath, fileName);
                }

                // 3. Guardar el archivo
                await File.WriteAllBytesAsync(finalPath, imageBytes);
                _logger.LogInformation("Imagen guardada en: {FilePath}", finalPath);

                // 4. Devolver la URL del endpoint seguro del FileController
                // Formato: /api/v1/file/images/{orgId}/{filename}
                if (organizationId.HasValue)
                {
                    return $"/api/v1/file/images/{organizationId.Value}/{fileName}";
                }
                else
                {
                    // Si no hay organizationId, usar la ruta antigua (compatibilidad)
                    // Pero esto no debería pasar en producción
                    var basePath = _environment.WebRootPath ?? _environment.ContentRootPath;
                    var relativePath = finalPath.Replace(basePath, "")
                                                .Replace("\\", "/")
                                                .TrimStart('/');
                    return $"/{relativePath}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar imagen. FileName: {FileName}, OrgId: {OrgId}", fileName, organizationId);
                throw new InvalidOperationException("No se pudo guardar la imagen en el almacenamiento local.", ex);
            }
        }

        public async Task<bool> DeleteImageAsync(string imageUrl)
        {
            try
            {
                // Convertir URL relativa a ruta física
                var basePath = _environment.WebRootPath ?? _environment.ContentRootPath;
                var physicalPath = imageUrl.StartsWith("/")
                    ? Path.Combine(basePath, imageUrl.TrimStart('/'))
                    : imageUrl;

                // Reemplazar '/' por separador de directorio del sistema
                physicalPath = physicalPath.Replace("/", Path.DirectorySeparatorChar.ToString());

                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                    _logger.LogInformation("Imagen eliminada: {FilePath}", physicalPath);
                    return await Task.FromResult(true);
                }

                _logger.LogWarning("Imagen no encontrada para eliminar: {ImageUrl}", imageUrl);
                return await Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar imagen: {ImageUrl}", imageUrl);
                return false;
            }
        }

        private static string ComputeHash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(data);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private static string SanitizeFileName(string fileName)
        {
            // Eliminar caracteres peligrosos y espacios
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Trim();
        }
    }
}

