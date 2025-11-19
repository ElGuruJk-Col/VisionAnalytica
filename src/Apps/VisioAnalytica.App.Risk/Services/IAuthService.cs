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

    /// <summary>
    /// Verifica si el usuario debe cambiar su contraseña.
    /// </summary>
    bool MustChangePassword { get; }

    /// <summary>
    /// Obtiene los roles del usuario actual.
    /// </summary>
    IList<string> CurrentUserRoles { get; }

    /// <summary>
    /// Cambia la contraseña del usuario actual.
    /// </summary>
    Task<bool> ChangePasswordAsync(ChangePasswordRequest request);

    /// <summary>
    /// Solicita recuperación de contraseña.
    /// </summary>
    Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request);

    /// <summary>
    /// Restablece la contraseña con un token.
    /// </summary>
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
}

