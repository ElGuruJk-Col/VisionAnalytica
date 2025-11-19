namespace VisioAnalytica.App.Risk.Models;

/// <summary>
/// Modelo para registro de usuario.
/// </summary>
public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string OrganizationName
);

/// <summary>
/// Modelo para login de usuario.
/// </summary>
public record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// Modelo de respuesta de autenticación.
/// </summary>
public record AuthResponse(
    string Email,
    string FirstName,
    string Token,
    bool MustChangePassword = false,
    IList<string>? Roles = null
);

/// <summary>
/// Modelo para cambio de contraseña.
/// </summary>
public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

/// <summary>
/// Modelo para solicitar recuperación de contraseña.
/// </summary>
public record ForgotPasswordRequest(
    string Email
);

/// <summary>
/// Modelo para restablecer contraseña con token.
/// </summary>
public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword
);

