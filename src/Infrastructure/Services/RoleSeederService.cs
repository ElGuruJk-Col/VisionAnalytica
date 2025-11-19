using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using VisioAnalytica.Core.Constants;
using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Servicio para inicializar los roles del sistema en la base de datos.
    /// Debe ejecutarse al iniciar la aplicación si los roles no existen.
    /// </summary>
    public class RoleSeederService
    {
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly ILogger<RoleSeederService> _logger;

        public RoleSeederService(
            RoleManager<IdentityRole<Guid>> roleManager,
            ILogger<RoleSeederService> logger)
        {
            _roleManager = roleManager;
            _logger = logger;
        }

        /// <summary>
        /// Inicializa todos los roles del sistema si no existen.
        /// </summary>
        public async Task SeedRolesAsync()
        {
            var roles = Roles.GetAll();

            foreach (var roleName in roles)
            {
                var roleExists = await _roleManager.RoleExistsAsync(roleName);
                if (!roleExists)
                {
                    var role = new IdentityRole<Guid>
                    {
                        Name = roleName,
                        NormalizedName = roleName.ToUpperInvariant()
                    };

                    var result = await _roleManager.CreateAsync(role);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("Rol '{RoleName}' creado exitosamente.", roleName);
                    }
                    else
                    {
                        _logger.LogError("Error al crear el rol '{RoleName}': {Errors}",
                            roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    _logger.LogDebug("El rol '{RoleName}' ya existe.", roleName);
                }
            }
        }
    }
}

