using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VisioAnalytica.Core.Constants;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Infrastructure.Data;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Implementación del servicio de gestión de usuarios.
    /// </summary>
    public class UserManagementService : IUserManagementService
    {
        private readonly VisioAnalyticaDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IEmailService? _emailService;
        private readonly Random _random = new();

        public UserManagementService(
            VisioAnalyticaDbContext context,
            UserManager<User> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IEmailService? emailService = null)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailService = emailService;
        }

        public async Task<CreateUserResponseDto> CreateUserAsync(CreateUserRequestDto request, Guid createdByUserId)
        {
            // Validar que el rol sea válido (no puede ser SuperAdmin)
            var validRoles = new[] { Roles.Admin, Roles.Inspector, Roles.Cliente };
            if (!validRoles.Contains(request.Role))
            {
                throw new ArgumentException($"El rol '{request.Role}' no es válido para creación de usuarios. Roles válidos: {string.Join(", ", validRoles)}");
            }

            // Verificar que el rol existe en el sistema
            var roleExists = await _roleManager.RoleExistsAsync(request.Role);
            if (!roleExists)
            {
                throw new InvalidOperationException($"El rol '{request.Role}' no existe en el sistema. Debe crearse primero.");
            }

            // Verificar que el email no esté en uso
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new ArgumentException("El correo electrónico ya está en uso.");
            }

            // Verificar que la organización existe
            var organization = await _context.Organizations.FindAsync(request.OrganizationId);
            if (organization == null)
            {
                throw new ArgumentException("La organización especificada no existe.");
            }

            // Generar contraseña
            string password;
            if (request.GenerateTemporaryPassword)
            {
                password = GenerateTemporaryPassword();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    throw new ArgumentException("Se debe proporcionar una contraseña cuando GenerateTemporaryPassword es false.");
                }
                password = request.Password;
            }

            // Crear el usuario
            var userName = request.UserName ?? request.Email.ToLower();
            var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email.ToLower(),
                UserName = userName,
                OrganizationId = request.OrganizationId,
                MustChangePassword = request.GenerateTemporaryPassword, // Debe cambiar si es provisional
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdByUserId,
                PhoneNumber = request.PhoneNumber
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Error al crear el usuario: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            // Asignar el rol
            var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
            if (!roleResult.Succeeded)
            {
                // Si falla la asignación de rol, eliminar el usuario creado
                await _userManager.DeleteAsync(user);
                throw new InvalidOperationException($"Error al asignar el rol: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }

            // Enviar email de bienvenida si se generó contraseña temporal
            if (request.GenerateTemporaryPassword && _emailService != null)
            {
                var fullName = $"{user.FirstName} {user.LastName}";
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendWelcomeEmailAsync(user.Email!, fullName, password);
                    }
                    catch (Exception ex)
                    {
                        // Log el error pero no fallar la creación del usuario
                        Console.WriteLine($"[WARNING] No se pudo enviar el email de bienvenida a {user.Email}: {ex.Message}");
                    }
                });
            }

            return new CreateUserResponseDto
            {
                UserId = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName!,
                Role = request.Role,
                TemporaryPassword = request.GenerateTemporaryPassword ? password : "***", // No exponer contraseña personalizada
                MustChangePassword = user.MustChangePassword,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<bool> AssignRoleAsync(Guid userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return false;
            }

            // Verificar que el rol existe
            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                throw new InvalidOperationException($"El rol '{roleName}' no existe en el sistema.");
            }

            // Verificar que no tenga ya el rol
            var isInRole = await _userManager.IsInRoleAsync(user, roleName);
            if (isInRole)
            {
                return true; // Ya tiene el rol, consideramos éxito
            }

            var result = await _userManager.AddToRoleAsync(user, roleName);
            return result.Succeeded;
        }

        public async Task<bool> RemoveRoleAsync(Guid userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return false;
            }

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            return result.Succeeded;
        }

        public async Task<IList<string>> GetUserRolesAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return new List<string>();
            }

            return await _userManager.GetRolesAsync(user);
        }

        public async Task<bool> SetUserActiveStatusAsync(Guid userId, bool isActive)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return false;
            }

            user.IsActive = isActive;
            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded;
        }

        public string GenerateTemporaryPassword(int length = 12)
        {
            const string chars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@#$%&*";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        public async Task<User?> GetUserByIdAsync(Guid userId)
        {
            return await _userManager.FindByIdAsync(userId.ToString());
        }

        public async Task<IList<User>> GetUsersByOrganizationAsync(Guid organizationId, bool includeInactive = false)
        {
            var query = _userManager.Users
                .Where(u => u.OrganizationId == organizationId);

            if (!includeInactive)
            {
                query = query.Where(u => u.IsActive);
            }

            return await query.ToListAsync();
        }

        public async Task<IList<User>> GetUsersByRoleAsync(Guid organizationId, string roleName)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
            return usersInRole
                .Where(u => u.OrganizationId == organizationId && u.IsActive)
                .ToList();
        }
    }
}

