using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration _configuration;

        public LocalFileStorage(
            IWebHostEnvironment environment, 
            ILogger<LocalFileStorage> logger,
            IConfiguration configuration)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            // Obtener ruta base configurada
            var configuredBasePath = _configuration["FileStorage:BasePath"];
            var useRelativePath = _configuration.GetValue<bool>("FileStorage:UseRelativePath", true);
            
            string basePath;
            
            if (!string.IsNullOrWhiteSpace(configuredBasePath))
            {
                // Usar ruta configurada (puede ser absoluta o relativa)
                if (Path.IsPathRooted(configuredBasePath))
                {
                    basePath = configuredBasePath;
                }
                else
                {
                    // Ruta relativa desde ContentRootPath
                    basePath = Path.Combine(_environment.ContentRootPath, configuredBasePath);
                }
            }
            else
            {
                // Ruta por defecto: fuera del proyecto en Storage/Images
                // Navegar desde ContentRootPath hasta la raíz del repositorio
                var contentRoot = _environment.ContentRootPath;
                
                // Si ContentRootPath está en bin/Debug, navegar hacia arriba
                if (contentRoot.Contains("bin" + Path.DirectorySeparatorChar + "Debug") || 
                    contentRoot.Contains("bin" + Path.DirectorySeparatorChar + "Release"))
                {
                    var directory = new DirectoryInfo(contentRoot);
                    while (directory != null && directory.Name != "Api" && directory.Parent != null)
                    {
                        directory = directory.Parent;
                    }
                    if (directory != null && directory.Name == "Api")
                    {
                        contentRoot = directory.Parent?.FullName ?? contentRoot;
                    }
                }
                else
                {
                    // Si no está en bin/Debug, asumir que ContentRootPath es la raíz del proyecto Api
                    // Navegar al directorio padre (raíz del repositorio)
                    contentRoot = Directory.GetParent(contentRoot)?.FullName ?? contentRoot;
                }
                
                // Crear ruta: {raiz_repositorio}/Storage/Images
                basePath = Path.Combine(contentRoot, "Storage", "Images");
            }
            
            _uploadsPath = basePath;
            
            // Aseguramos que la carpeta exista
            if (!Directory.Exists(_uploadsPath))
            {
                Directory.CreateDirectory(_uploadsPath);
                _logger.LogInformation("Carpeta de almacenamiento de imágenes creada en: {UploadsPath}", _uploadsPath);
            }
            else
            {
                _logger.LogInformation("Usando carpeta de almacenamiento existente: {UploadsPath}", _uploadsPath);
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

        public async Task<byte[]?> ReadImageAsync(string imageUrl)
        {
            try
            {
                // Convertir URL a ruta de archivo
                string? filePath = null;
                
                // Si la URL es del formato /api/v1/file/images/{orgId}/{fileName}
                if (imageUrl.StartsWith("/api/v1/file/images/"))
                {
                    var parts = imageUrl.Replace("/api/v1/file/images/", "").Split('/');
                    if (parts.Length >= 2 && Guid.TryParse(parts[0], out var orgId))
                    {
                        var fileName = parts[1];
                        var orgFolder = Path.Combine(_uploadsPath, orgId.ToString());
                        filePath = Path.Combine(orgFolder, fileName);
                    }
                }
                // Si es una ruta relativa que empieza con /uploads
                else if (imageUrl.StartsWith("/uploads/"))
                {
                    var relativePath = imageUrl.Replace("/uploads/", "");
                    filePath = Path.Combine(_uploadsPath, relativePath);
                }
                // Si es una ruta absoluta y está dentro de _uploadsPath
                else if (Path.IsPathRooted(imageUrl) && imageUrl.StartsWith(_uploadsPath))
                {
                    filePath = imageUrl;
                }
                
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    _logger.LogWarning("No se pudo encontrar la imagen en la ruta: {FilePath} (URL original: {ImageUrl})", filePath, imageUrl);
                    return null;
                }
                
                return await File.ReadAllBytesAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al leer imagen desde {ImageUrl}", imageUrl);
                return null;
            }
        }
        
        public async Task<string> SaveThumbnailAsync(byte[] thumbnailBytes, string originalFileName, Guid? organizationId = null)
        {
            try
            {
                // Generar nombre del thumbnail: thumb_{originalFileName}
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
                var extension = Path.GetExtension(originalFileName);
                var thumbnailFileName = $"thumb_{fileNameWithoutExt}{extension}";
                thumbnailFileName = SanitizeFileName(thumbnailFileName);

                // Construir la ruta completa
                string finalPath;
                if (organizationId.HasValue)
                {
                    // Organizar por organización: uploads/{orgId}/thumbnails/{filename}
                    var orgFolder = Path.Combine(_uploadsPath, organizationId.Value.ToString());
                    var thumbnailsFolder = Path.Combine(orgFolder, "thumbnails");
                    if (!Directory.Exists(thumbnailsFolder))
                    {
                        Directory.CreateDirectory(thumbnailsFolder);
                    }
                    finalPath = Path.Combine(thumbnailsFolder, thumbnailFileName);
                }
                else
                {
                    var thumbnailsFolder = Path.Combine(_uploadsPath, "thumbnails");
                    if (!Directory.Exists(thumbnailsFolder))
                    {
                        Directory.CreateDirectory(thumbnailsFolder);
                    }
                    finalPath = Path.Combine(thumbnailsFolder, thumbnailFileName);
                }

                // Guardar el thumbnail
                await File.WriteAllBytesAsync(finalPath, thumbnailBytes);
                _logger.LogInformation("Thumbnail guardado en: {FilePath}", finalPath);

                // Devolver la URL del endpoint seguro del FileController
                // Formato: /api/v1/file/images/{orgId}/thumbnails/{filename}
                if (organizationId.HasValue)
                {
                    return $"/api/v1/file/images/{organizationId.Value}/thumbnails/{thumbnailFileName}";
                }
                else
                {
                    var basePath = _environment.WebRootPath ?? _environment.ContentRootPath;
                    var relativePath = finalPath.Replace(basePath, "")
                                                .Replace("\\", "/")
                                                .TrimStart('/');
                    return $"/{relativePath}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar thumbnail. FileName: {FileName}, OrgId: {OrgId}", originalFileName, organizationId);
                throw new InvalidOperationException("No se pudo guardar el thumbnail en el almacenamiento local.", ex);
            }
        }

        public async Task<bool> DeleteImageAsync(string imageUrl)
        {
            try
            {
                // Si la URL es del formato /api/v1/file/images/{orgId}/{fileName}
                // Extraer organizationId y fileName
                if (imageUrl.StartsWith("/api/v1/file/images/"))
                {
                    var parts = imageUrl.Replace("/api/v1/file/images/", "").Split('/');
                    if (parts.Length >= 2 && Guid.TryParse(parts[0], out var orgId))
                    {
                        var fileName = parts[1];
                        var orgFolder = Path.Combine(_uploadsPath, orgId.ToString());
                        var filePath = Path.Combine(orgFolder, fileName);
                        
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            _logger.LogInformation("Imagen eliminada: {FilePath}", filePath);
                            return true;
                        }
                    }
                }
                else
                {
                    // Compatibilidad con rutas antiguas
                    var basePath = _environment.WebRootPath ?? _environment.ContentRootPath;
                    var physicalPath = imageUrl.StartsWith("/")
                        ? Path.Combine(basePath, imageUrl.TrimStart('/'))
                        : imageUrl;

                    physicalPath = physicalPath.Replace("/", Path.DirectorySeparatorChar.ToString());

                    if (File.Exists(physicalPath))
                    {
                        File.Delete(physicalPath);
                        _logger.LogInformation("Imagen eliminada: {FilePath}", physicalPath);
                        return true;
                    }
                }

                _logger.LogWarning("Imagen no encontrada para eliminar: {ImageUrl}", imageUrl);
                return false;
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

