using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VisioAnalytica.Core.Interfaces;

namespace VisioAnalytica.Api.Controllers
{
    /// <summary>
    /// Controlador para servir archivos (imágenes) de forma segura.
    /// Requiere autenticación y verifica que el usuario pertenezca a la organización propietaria del archivo.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly IFileStorage _fileStorage;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileController> _logger;

        public FileController(
            IFileStorage fileStorage,
            IWebHostEnvironment environment,
            ILogger<FileController> logger)
        {
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Obtiene el GUID de la organización del token JWT (claim "org_id").
        /// </summary>
        private Guid? GetOrganizationIdFromClaims()
        {
            var orgIdString = User.FindFirstValue("org_id");
            if (string.IsNullOrWhiteSpace(orgIdString) || !Guid.TryParse(orgIdString, out var organizationId))
            {
                return null;
            }
            return organizationId;
        }

        /// <summary>
        /// Sirve una imagen de forma segura.
        /// Verifica que el usuario autenticado pertenezca a la organización propietaria del archivo.
        /// </summary>
        /// <param name="organizationId">ID de la organización (parte de la ruta)</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <returns>El archivo de imagen si el usuario tiene permisos, o 403/404 si no</returns>
        [HttpGet("images/{organizationId:guid}/{fileName}")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetImage(Guid organizationId, string fileName)
        {
            // 1. Verificar que el usuario esté autenticado (ya está garantizado por [Authorize])
            var userId = User.FindFirstValue("uid");
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Intento de acceso a imagen sin userId en el token. OrganizationId: {OrgId}, FileName: {FileName}", 
                    organizationId, fileName);
                return Unauthorized("El token no contiene información de usuario válida.");
            }

            // 2. Verificar que el usuario pertenezca a la organización propietaria del archivo
            var userOrgId = GetOrganizationIdFromClaims();
            if (!userOrgId.HasValue)
            {
                _logger.LogWarning("Intento de acceso a imagen sin org_id en el token. UserId: {UserId}, RequestedOrgId: {OrgId}, FileName: {FileName}", 
                    userId, organizationId, fileName);
                return StatusCode(StatusCodes.Status403Forbidden, 
                    "No se pudo verificar la organización del usuario. Acceso denegado.");
            }

            if (userOrgId.Value != organizationId)
            {
                _logger.LogWarning(
                    "Intento de acceso no autorizado a imagen de otra organización. " +
                    "UserId: {UserId}, UserOrgId: {UserOrgId}, RequestedOrgId: {OrgId}, FileName: {FileName}", 
                    userId, userOrgId.Value, organizationId, fileName);
                return StatusCode(StatusCodes.Status403Forbidden, 
                    "No tienes permiso para acceder a archivos de esta organización.");
            }

            // 3. Construir la ruta física del archivo
            // Usar la misma lógica que LocalFileStorage para construir la ruta
            // Si WebRootPath es null, usar ContentRootPath, pero si ContentRootPath está en bin/Debug,
            // navegar hacia arriba hasta encontrar la carpeta del proyecto
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
            
            var uploadsPath = Path.Combine(basePath, "uploads");
            var orgFolder = Path.Combine(uploadsPath, organizationId.ToString());
            var filePath = Path.Combine(orgFolder, fileName);

            // Sanitizar el nombre del archivo para prevenir path traversal attacks
            var sanitizedPath = Path.GetFullPath(filePath);
            var sanitizedBasePath = Path.GetFullPath(orgFolder);
            
            // Logging para diagnóstico
            _logger.LogInformation(
                "Buscando imagen. BasePath: {BasePath}, UploadsPath: {UploadsPath}, OrgFolder: {OrgFolder}, FilePath: {FilePath}, SanitizedPath: {SanitizedPath}",
                basePath, uploadsPath, orgFolder, filePath, sanitizedPath);
            
            if (!sanitizedPath.StartsWith(sanitizedBasePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Intento de path traversal attack detectado. " +
                    "UserId: {UserId}, OrgId: {OrgId}, FileName: {FileName}, SanitizedPath: {SanitizedPath}, SanitizedBasePath: {SanitizedBasePath}", 
                    userId, organizationId, fileName, sanitizedPath, sanitizedBasePath);
                return StatusCode(StatusCodes.Status400BadRequest, "Nombre de archivo inválido.");
            }

            // 4. Verificar que el archivo exista
            if (!System.IO.File.Exists(sanitizedPath))
            {
                // Verificar si la carpeta existe
                var folderExists = Directory.Exists(orgFolder);
                var uploadsExists = Directory.Exists(uploadsPath);
                
                _logger.LogWarning(
                    "Intento de acceso a imagen inexistente. " +
                    "UserId: {UserId}, OrgId: {OrgId}, FileName: {FileName}, Path: {Path}, " +
                    "FolderExists: {FolderExists}, UploadsExists: {UploadsExists}, BasePath: {BasePath}", 
                    userId, organizationId, fileName, sanitizedPath, folderExists, uploadsExists, basePath);
                return NotFound($"La imagen solicitada no existe. Ruta buscada: {sanitizedPath}");
            }

            // 5. Determinar el content type basado en la extensión
            var contentType = GetContentType(fileName);

            // 6. Servir el archivo
            _logger.LogInformation(
                "Imagen servida correctamente. UserId: {UserId}, OrgId: {OrgId}, FileName: {FileName}", 
                userId, organizationId, fileName);

            return PhysicalFile(sanitizedPath, contentType, fileName);
        }

        /// <summary>
        /// Determina el Content-Type basado en la extensión del archivo.
        /// </summary>
        private static string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }
    }
}

