// En: src/Core/Models/Dtos/AuthDtos.cs
// (¡NUEVO ARCHIVO!)

namespace VisioAnalytica.Core.Models.Dtos
{
    /// <summary>
    /// DTO (Data Transfer Object) para el registro de un nuevo usuario/organización.
    /// </summary>
    public record RegisterDto(string Email, string Password, string FirstName, string LastName, string OrganizationName);

    /// <summary>
    /// DTO para el inicio de sesión.
    /// </summary>
    public record LoginDto(string Email, string Password);

    /// <summary>
    /// DTO que se devuelve al cliente tras un login/registro exitoso.
    /// </summary>
    public record UserDto(string Email, string FirstName, string Token, string? RefreshToken = null);

    /// <summary>
    /// DTO para solicitar renovación de token usando refresh token.
    /// </summary>
    public record RefreshTokenDto(string RefreshToken);

    /// <summary>
    /// DTO que se devuelve al renovar un token.
    /// </summary>
    public record RefreshTokenResponseDto(string AccessToken, string RefreshToken);

    /// <summary>
    /// DTO para revocar un refresh token.
    /// </summary>
    public record RevokeTokenDto(string RefreshToken);

    /// <summary>
    /// DTO que representa un refresh token en la lista de tokens del usuario.
    /// </summary>
    public record RefreshTokenInfoDto(
        Guid Id,
        DateTime CreatedAt,
        DateTime ExpiresAt,
        DateTime? RevokedAt,
        string? CreatedByIp,
        bool IsActive
    );

    /// <summary>
    /// DTO para cambiar la contraseña.
    /// </summary>
    public record ChangePasswordDto(string? CurrentPassword, string NewPassword);

    /// <summary>
    /// DTO para solicitar recuperación de contraseña.
    /// </summary>
    public record ForgotPasswordDto(string Email);

    /// <summary>
    /// DTO para restablecer la contraseña con token.
    /// </summary>
    public record ResetPasswordDto(string Email, string Token, string NewPassword);
}