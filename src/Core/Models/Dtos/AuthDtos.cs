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
    public record UserDto(string Email, string FirstName, string Token);

    /// <summary>
    /// DTO para cambiar la contraseña.
    /// </summary>
    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }

    /// <summary>
    /// DTO para solicitar recuperación de contraseña.
    /// </summary>
    public class ForgotPasswordDto
    {
        public string Email { get; set; } = null!;
    }

    /// <summary>
    /// DTO para restablecer la contraseña con token.
    /// </summary>
    public class ResetPasswordDto
    {
        public string Email { get; set; } = null!;
        public string Token { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }
}