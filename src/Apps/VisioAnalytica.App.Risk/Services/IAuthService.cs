using VisioAnalytica.App.Risk.Models;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Interfaz para el servicio de autenticación.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registra un nuevo usuario y organización.
    /// </summary>
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Inicia sesión con email y contraseña.
    /// </summary>
    Task<AuthResponse?> LoginAsync(LoginRequest request);

    /// <summary>
    /// Cierra la sesión actual.
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Verifica si hay una sesión activa.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Obtiene el token JWT actual.
    /// </summary>
    string? CurrentToken { get; }

    /// <summary>
    /// Obtiene el email del usuario actual.
    /// </summary>
    string? CurrentUserEmail { get; }
}

