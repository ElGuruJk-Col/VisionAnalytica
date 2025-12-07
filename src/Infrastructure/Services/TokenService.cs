using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Infrastructure.Data;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Implementación (la "Fábrica") del contrato ITokenService.
    /// Esta clase sabe cómo construir un Token JWT y manejar refresh tokens.
    /// Usa características modernas de .NET 10.0 y C# 14.
    /// </summary>
    public class TokenService(IConfiguration config, VisioAnalyticaDbContext context) : ITokenService
    {
        // Guardamos la "llave secreta" (leída de appsettings.json)
        private readonly SymmetricSecurityKey _key = InitializeKey(config);

        // Guardamos la configuración (para leer Issuer y Audience)
        private readonly IConfiguration _config = config;
        
        // DbContext para manejar refresh tokens
        private readonly VisioAnalyticaDbContext _context = context;

        /// <summary>
        /// Inicializa la clave de seguridad desde la configuración.
        /// </summary>
        private static SymmetricSecurityKey InitializeKey(IConfiguration config)
        {
            // 1. Leemos la clave secreta desde appsettings.json
            var keyString = config["Jwt:Key"];
            if (string.IsNullOrEmpty(keyString))
            {
                // FIX CA2208: Usamos la forma más simple de ArgumentNullException,
                // que es la más aceptada, ya que la dependencia proviene de 'config'.
                throw new ArgumentNullException(nameof(config), "La clave 'Jwt:Key' no está configurada en appsettings.json o es nula/vacía.");
            }

            // 2. Convertimos la clave (string) en un objeto de seguridad (bytes)
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        }

        /// <summary>
        /// Genera un string de token JWT para un usuario específico.
        /// (Este es el método exigido por la interfaz ITokenService).
        /// </summary>
        public string CreateToken(User user, IList<string>? roles = null)
        {
            // 1. Definir los "Claims" (Datos dentro del Token) usando collection expressions
            var claims = new List<Claim>
            {
                // Claim estándar para el "nombre de usuario" (único)
                new(JwtRegisteredClaimNames.NameId, user.UserName!),
                
                // Claim estándar para el "email"
                new(JwtRegisteredClaimNames.Email, user.Email!),
                
                // --- Claims Personalizados (¡Nuestra lógica de negocio!) ---
                
                // Guardamos el ID del usuario (Guid)
                new("uid", user.Id.ToString()),
                
                // ¡VITAL! Guardamos el ID de la organización (Multi-Tenant)
                new("org_id", user.OrganizationId.ToString()),
                
                // Guardamos si el usuario debe cambiar su contraseña
                new("must_change_password", user.MustChangePassword.ToString().ToLowerInvariant())
            };

            // Agregar roles como claims (para autorización) usando LINQ moderno
            if (roles is not null && roles.Count > 0)
            {
                var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
                claims.AddRange(roleClaims);
            }

            // 2. Definir las "Credenciales de Firma"
            var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

            // 3. Crear el "Descriptor" del Token (El "Molde")
            // Leer tiempo de expiración desde configuración (por defecto: 120 minutos = 2 horas)
            var expirationMinutes = _config.GetValue<int>("Jwt:AccessTokenExpirationMinutes", 120);
            var expirationTime = DateTime.UtcNow.AddMinutes(expirationMinutes);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims), // Los datos del usuario
                Expires = expirationTime,             // Cuándo expira (configurable desde appsettings)
                Issuer = _config["Jwt:Issuer"],       // Quién lo emitió (leído de appsettings)
                Audience = _config["Jwt:Audience"],   // Para quién es (leído de appsettings)
                SigningCredentials = creds            // La firma de seguridad
            };

            // 4. Crear el "Manejador" (El "Operario")
            var tokenHandler = new JwtSecurityTokenHandler();

            // 5. Crear el Token (El Producto Final)
            var token = tokenHandler.CreateToken(tokenDescriptor);

            // 6. Escribir el Token (La "Impresión")
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Genera un refresh token único y seguro usando criptografía.
        /// </summary>
        public string GenerateRefreshToken()
        {
            // Generar 32 bytes aleatorios usando RandomNumberGenerator (más seguro que Random)
            var randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            
            // Convertir a Base64 para obtener un string seguro y único
            return Convert.ToBase64String(randomBytes);
        }

        /// <summary>
        /// Valida un refresh token y devuelve el ID del usuario si es válido.
        /// </summary>
        public async Task<Guid?> ValidateRefreshTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            try
            {
                // Buscar el refresh token en la base de datos
                var refreshToken = await _context.RefreshTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.Token == token);

                if (refreshToken == null)
                    return null;

                // Verificar que el token esté activo (no revocado y no expirado)
                if (!refreshToken.IsActive)
                {
                    // Si el token fue revocado o expiró, no devolver el usuario
                    return null;
                }

                // Verificar que el usuario esté activo
                if (refreshToken.User == null || !refreshToken.User.IsActive)
                {
                    return null;
                }

                return refreshToken.UserId;
            }
            catch
            {
                // Si hay error, considerar el token como inválido
                return null;
            }
        }
    }
}
