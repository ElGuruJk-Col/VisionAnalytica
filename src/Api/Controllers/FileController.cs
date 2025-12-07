using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using VisioAnalytica.Core.Constants;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Infrastructure.Data;

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
        private readonly VisioAnalyticaDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _configuration;

        public FileController(
            IFileStorage fileStorage,
            IWebHostEnvironment environment,
            ILogger<FileController> logger,
            VisioAnalyticaDbContext context,
            UserManager<User> userManager,
            IConfiguration configuration)
        {
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Obtiene el ID del usuario autenticado desde el token JWT.
        /// El token usa el claim "uid" (según TokenService).
        /// </summary>
        private Guid? GetCurrentUserId()
        {
            // El TokenService genera el token con "uid" como claim personalizado
            var userIdClaim = User.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return null;
            }
            return userId;
        }

        /// <summary>
        /// Obtiene el GUID de la organización del token JWT (claim "org_id").
        /// El token usa el claim "org_id" (según TokenService).
        /// </summary>
        private Guid? GetOrganizationIdFromClaims()
        {
            // El TokenService genera el token con "org_id" como claim personalizado
            var orgIdString = User.FindFirst("org_id")?.Value;
            if (string.IsNullOrWhiteSpace(orgIdString) || !Guid.TryParse(orgIdString, out var organizationId))
            {
                return null;
            }
            return organizationId;
        }

        /// <summary>
        /// Verifica si el usuario tiene uno de los roles especificados.
        /// </summary>
        private bool HasAnyRole(params string[] roles)
        {
            return User.Claims.Any(c => c.Type == ClaimTypes.Role && roles.Contains(c.Value));
        }

        /// <summary>
        /// Verifica si el usuario tiene acceso a una imagen basado en su rol y empresa afiliada.
        /// </summary>
        private async Task<bool> HasAccessToImageAsync(Guid organizationId, string fileName, Guid? affiliatedCompanyId = null)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return false;
            }

            var userOrgId = GetOrganizationIdFromClaims();
            if (!userOrgId.HasValue)
            {
                return false;
            }

            // SuperAdmin puede acceder a todo
            if (HasAnyRole(Roles.SuperAdmin))
            {
                return true;
            }

            // Admin puede acceder a imágenes de su organización
            if (HasAnyRole(Roles.Admin))
            {
                return userOrgId.Value == organizationId;
            }

            // Inspector: puede acceder a imágenes de empresas asignadas
            if (HasAnyRole(Roles.Inspector))
            {
                if (userOrgId.Value != organizationId)
                {
                    return false;
                }

                // Si se proporciona el ID de empresa afiliada, verificar asignación
                if (affiliatedCompanyId.HasValue)
                {
                    var user = await _userManager.FindByIdAsync(currentUserId.Value.ToString());
                    if (user == null)
                    {
                        return false;
                    }

                    var hasAccess = await _context.AffiliatedCompanies
                        .AnyAsync(ac => ac.Id == affiliatedCompanyId.Value &&
                                       ac.OrganizationId == organizationId &&
                                       ac.AssignedInspectors.Any(i => i.Id == currentUserId.Value));

                    return hasAccess;
                }

                // Si no se proporciona, verificar que la imagen pertenezca a una inspección del inspector
                // ⚠️ CORRECCIÓN: Buscar en la tabla Photo, no en Inspection.ImageUrl
                var photo = await _context.Photos
                    .Include(p => p.Inspection)
                    .FirstOrDefaultAsync(p => p.ImageUrl.Contains(fileName) && 
                                             p.Inspection.OrganizationId == organizationId &&
                                             p.Inspection.UserId == currentUserId.Value);

                return photo != null;
            }

            // Cliente: solo puede acceder a imágenes de su empresa
            if (HasAnyRole(Roles.Cliente))
            {
                if (userOrgId.Value != organizationId)
                {
                    return false;
                }

                // Buscar la foto y verificar que pertenezca a una inspección de la organización
                // ⚠️ CORRECCIÓN: Buscar en la tabla Photo, no en Inspection.ImageUrl
                var photo = await _context.Photos
                    .Include(p => p.Inspection)
                    .ThenInclude(i => i.AffiliatedCompany)
                    .FirstOrDefaultAsync(p => p.ImageUrl.Contains(fileName) && 
                                             p.Inspection.OrganizationId == organizationId);

                // Por ahora, si la foto existe en la organización, el cliente puede verla
                // Esto puede necesitar ajuste según la lógica de negocio específica
                return photo != null;
            }

            return false;
        }

        /// <summary>
        /// Sirve una imagen de forma segura.
        /// Verifica permisos basados en roles y empresas afiliadas.
        /// Soporta parámetros de optimización: width (ancho máximo) y quality (calidad 0-100).
        /// </summary>
        /// <param name="organizationId">ID de la organización (parte de la ruta)</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <param name="affiliatedCompanyId">ID de la empresa afiliada (opcional, para validación adicional)</param>
        /// <param name="width">Ancho máximo de la imagen en píxeles (opcional, para redimensionamiento)</param>
        /// <param name="quality">Calidad de compresión JPEG (0-100, opcional, por defecto 85)</param>
        /// <returns>El archivo de imagen si el usuario tiene permisos, o 403/404 si no</returns>
        [HttpGet("images/{organizationId:guid}/{fileName}")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetImage(
            Guid organizationId, 
            string fileName, 
            [FromQuery] Guid? affiliatedCompanyId = null,
            [FromQuery] int? width = null,
            [FromQuery] int? quality = null)
        {
            // 1. Verificar permisos basados en roles y empresas afiliadas
            var hasAccess = await HasAccessToImageAsync(organizationId, fileName, affiliatedCompanyId);
            if (!hasAccess)
            {
                var currentUserId = GetCurrentUserId();
                _logger.LogWarning(
                    "Intento de acceso no autorizado a imagen. " +
                    "UserId: {UserId}, OrgId: {OrgId}, FileName: {FileName}, AffiliatedCompanyId: {CompanyId}", 
                    currentUserId, organizationId, fileName, affiliatedCompanyId);
                return StatusCode(StatusCodes.Status403Forbidden, 
                    "No tienes permiso para acceder a esta imagen.");
            }

            // 3. Construir la ruta física del archivo usando la misma lógica que LocalFileStorage
            var basePath = GetStorageBasePath();
            var orgFolder = Path.Combine(basePath, organizationId.ToString());
            var filePath = Path.Combine(orgFolder, fileName);

            // Sanitizar el nombre del archivo para prevenir path traversal attacks
            var sanitizedPath = Path.GetFullPath(filePath);
            var sanitizedBasePath = Path.GetFullPath(orgFolder);
            
            var userId = GetCurrentUserId();
            
            // Logging para diagnóstico
            _logger.LogInformation(
                "Buscando imagen. BasePath: {BasePath}, OrgFolder: {OrgFolder}, FilePath: {FilePath}, SanitizedPath: {SanitizedPath}",
                basePath, orgFolder, filePath, sanitizedPath);
            
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
                
                _logger.LogWarning(
                    "Intento de acceso a imagen inexistente. " +
                    "UserId: {UserId}, OrgId: {OrgId}, FileName: {FileName}, Path: {Path}, " +
                    "FolderExists: {FolderExists}, BasePath: {BasePath}", 
                    userId, organizationId, fileName, sanitizedPath, folderExists, basePath);
                return NotFound($"La imagen solicitada no existe. Ruta buscada: {sanitizedPath}");
            }

            // 5. Determinar el content type basado en la extensión
            var contentType = GetContentType(fileName);

            // 6. Procesar imagen si se solicitan optimizaciones (width o quality)
            if (width.HasValue || quality.HasValue)
            {
                try
                {
                    var processedImage = await ProcessImageAsync(sanitizedPath, width, quality ?? 85);
                    if (processedImage != null)
                    {
                        _logger.LogInformation(
                            "Imagen procesada y servida. UserId: {UserId}, OrgId: {OrgId}, FileName: {FileName}, Width: {Width}, Quality: {Quality}", 
                            userId, organizationId, fileName, width, quality ?? 85);
                        return File(processedImage, contentType, fileName);
                    }
                    // Si falla el procesamiento, continuar con la imagen original
                    _logger.LogWarning("Error al procesar imagen, sirviendo original. FileName: {FileName}", fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al procesar imagen, sirviendo original. FileName: {FileName}", fileName);
                    // Continuar con la imagen original si falla el procesamiento
                }
            }

            // 7. Servir el archivo (original o si no se solicitaron optimizaciones)
            _logger.LogInformation(
                "Imagen servida correctamente. UserId: {UserId}, OrgId: {OrgId}, FileName: {FileName}", 
                userId, organizationId, fileName);

            return PhysicalFile(sanitizedPath, contentType, fileName);
        }

        /// <summary>
        /// Elimina una imagen de forma segura.
        /// Solo Admin y SuperAdmin pueden eliminar imágenes.
        /// </summary>
        /// <param name="organizationId">ID de la organización</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <returns>200 si se eliminó correctamente, 403/404 si no</returns>
        [HttpDelete("images/{organizationId:guid}/{fileName}")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteImage(Guid organizationId, string fileName)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized("Usuario no autenticado.");
            }

            var userOrgId = GetOrganizationIdFromClaims();
            if (!userOrgId.HasValue)
            {
                return StatusCode(StatusCodes.Status403Forbidden, 
                    "No se pudo verificar la organización del usuario.");
            }

            // SuperAdmin puede eliminar de cualquier organización
            // Admin solo puede eliminar de su organización
            if (!HasAnyRole(Roles.SuperAdmin) && userOrgId.Value != organizationId)
            {
                _logger.LogWarning(
                    "Intento de eliminación no autorizada. " +
                    "UserId: {UserId}, UserOrgId: {UserOrgId}, RequestedOrgId: {OrgId}, FileName: {FileName}", 
                    currentUserId, userOrgId.Value, organizationId, fileName);
                return StatusCode(StatusCodes.Status403Forbidden, 
                    "No tienes permiso para eliminar imágenes de esta organización.");
            }

            // Construir la ruta del archivo usando la misma lógica que LocalFileStorage
            var basePath = GetStorageBasePath();
            var orgFolder = Path.Combine(basePath, organizationId.ToString());
            var filePath = Path.Combine(orgFolder, fileName);

            var sanitizedPath = Path.GetFullPath(filePath);
            var sanitizedBasePath = Path.GetFullPath(orgFolder);
            
            if (!sanitizedPath.StartsWith(sanitizedBasePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Intento de path traversal en eliminación. FileName: {FileName}", fileName);
                return BadRequest("Nombre de archivo inválido.");
            }

            if (!System.IO.File.Exists(sanitizedPath))
            {
                return NotFound("La imagen no existe.");
            }

            try
            {
                System.IO.File.Delete(sanitizedPath);
                _logger.LogInformation(
                    "Imagen eliminada correctamente. UserId: {UserId}, OrgId: {OrgId}, FileName: {FileName}", 
                    currentUserId, organizationId, fileName);
                
                return Ok(new { message = "Imagen eliminada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar imagen. FileName: {FileName}", fileName);
                return StatusCode(500, "Error al eliminar la imagen.");
            }
        }

        /// <summary>
        /// Obtiene la ruta base de almacenamiento usando la misma lógica que LocalFileStorage.
        /// </summary>
        private string GetStorageBasePath()
        {
            // Obtener ruta base configurada
            var configuredBasePath = _configuration["FileStorage:BasePath"];
            
            if (!string.IsNullOrWhiteSpace(configuredBasePath))
            {
                // Usar ruta configurada (puede ser absoluta o relativa)
                if (Path.IsPathRooted(configuredBasePath))
                {
                    return configuredBasePath;
                }
                else
                {
                    // Ruta relativa desde ContentRootPath
                    return Path.Combine(_environment.ContentRootPath, configuredBasePath);
                }
            }
            else
            {
                // Ruta por defecto: fuera del proyecto en Storage/Images
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
                    // Si no está en bin/Debug, navegar al directorio padre (raíz del repositorio)
                    contentRoot = Directory.GetParent(contentRoot)?.FullName ?? contentRoot;
                }
                
                // Crear ruta: {raiz_repositorio}/Storage/Images
                return Path.Combine(contentRoot, "Storage", "Images");
            }
        }

        /// <summary>
        /// Procesa una imagen aplicando redimensionamiento y/o compresión según los parámetros.
        /// </summary>
        /// <param name="imagePath">Ruta completa del archivo de imagen</param>
        /// <param name="maxWidth">Ancho máximo en píxeles (opcional)</param>
        /// <param name="quality">Calidad de compresión JPEG (0-100, por defecto 85)</param>
        /// <returns>Bytes de la imagen procesada, o null si falla</returns>
        private async Task<byte[]?> ProcessImageAsync(string imagePath, int? maxWidth, int quality)
        {
            try
            {
                // Validar parámetros
                if (maxWidth.HasValue && maxWidth.Value <= 0)
                {
                    _logger.LogWarning("Ancho máximo inválido: {Width}", maxWidth);
                    return null;
                }

                if (quality < 0 || quality > 100)
                {
                    _logger.LogWarning("Calidad inválida: {Quality}, usando 85 por defecto", quality);
                    quality = 85;
                }

                // Cargar imagen
                using var image = await Image.LoadAsync(imagePath);

                // Redimensionar si se especifica ancho máximo
                var originalWidth = image.Width;
                var originalHeight = image.Height;
                
                if (maxWidth.HasValue && image.Width > maxWidth.Value)
                {
                    var aspectRatio = (float)image.Height / image.Width;
                    var newHeight = (int)(maxWidth.Value * aspectRatio);

                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(maxWidth.Value, newHeight),
                        Mode = ResizeMode.Max // Mantiene aspect ratio
                    }));

                    _logger.LogDebug("Imagen redimensionada: {OriginalWidth}x{OriginalHeight} -> {NewWidth}x{NewHeight}",
                        originalWidth, originalHeight, maxWidth.Value, newHeight);
                }

                // Comprimir y convertir a JPEG con calidad especificada
                using var memoryStream = new MemoryStream();
                var encoder = new JpegEncoder
                {
                    Quality = quality
                };

                await image.SaveAsync(memoryStream, encoder);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar imagen: {ImagePath}", imagePath);
                return null;
            }
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

