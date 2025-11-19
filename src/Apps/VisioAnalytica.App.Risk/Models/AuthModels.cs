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
    string Token
);

