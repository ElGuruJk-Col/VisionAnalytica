using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Implementación (la "Fábrica") del contrato ITokenService.
    /// Esta clase sabe cómo construir un Token JWT.
    /// </summary>
    public class TokenService : ITokenService
    {
        // Guardamos la "llave secreta" (leída de appsettings.json)
        private readonly SymmetricSecurityKey _key;

        // Guardamos la configuración (para leer Issuer y Audience)
        private readonly IConfiguration _config;

        /// <summary>
        /// Constructor: Aquí es donde recibimos nuestras dependencias
        /// (Inyección de Dependencias).
        /// </summary>
        /// <param name="config">El servicio que sabe leer appsettings.json</param>
        public TokenService(IConfiguration config)
        {
            _config = config;

            // 1. Leemos la clave secreta desde appsettings.json
            var keyString = _config["Jwt:Key"];
            if (string.IsNullOrEmpty(keyString))
            {
                // FIX CA2208: Usamos la forma más simple de ArgumentNullException,
                // que es la más aceptada, ya que la dependencia proviene de 'config'.
                throw new ArgumentNullException(nameof(config), "La clave 'Jwt:Key' no está configurada en appsettings.json o es nula/vacía.");
            }

            // 2. Convertimos la clave (string) en un objeto de seguridad (bytes)
            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        }

        /// <summary>
        /// Genera un string de token JWT para un usuario específico.
        /// (Este es el método exigido por la interfaz ITokenService).
        /// </summary>
        public string CreateToken(User user)
        {
            // 1. Definir los "Claims" (Datos dentro del Token)
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
                new("org_id", user.OrganizationId.ToString())
            };

            // 2. Definir las "Credenciales de Firma"
            var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

            // 3. Crear el "Descriptor" del Token (El "Molde")
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims), // Los datos del usuario
                Expires = DateTime.Now.AddDays(7),    // Cuándo expira (ej. 7 días)
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
    }
}
