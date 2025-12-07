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

    /// <summary>
    /// Verifica si el token actual ha expirado.
    /// </summary>
    bool IsTokenExpired();

    /// <summary>
    /// Verifica si el token expirará pronto (dentro del umbral especificado).
    /// </summary>
    /// <param name="threshold">Umbral de tiempo antes de la expiración. Por defecto: 1 hora.</param>
    bool IsTokenExpiringSoon(TimeSpan? threshold = null);

    /// <summary>
    /// Obtiene el tiempo restante hasta que el token expire.
    /// </summary>
    /// <returns>TimeSpan con el tiempo restante, o null si el token no existe o ya expiró.</returns>
    TimeSpan? GetTokenTimeRemaining();

    /// <summary>
    /// Renueva el access token usando el refresh token guardado.
    /// </summary>
    /// <returns>True si la renovación fue exitosa, false en caso contrario.</returns>
    Task<bool> RefreshTokenAsync();
}

