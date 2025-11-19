using System.ComponentModel.DataAnnotations;

namespace VisioAnalytica.Core.Models.Dtos
{
    /// <summary>
    /// DTO para crear un nuevo usuario.
    /// </summary>
    public class CreateUserRequestDto
    {
        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres")]
        public string FirstName { get; set; } = null!;

        [Required(ErrorMessage = "El apellido es requerido")]
        [StringLength(100, ErrorMessage = "El apellido no puede exceder 100 caracteres")]
        public string LastName { get; set; } = null!;

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "El email no es válido")]
        public string Email { get; set; } = null!;

        /// <summary>
        /// Nombre de usuario. Si no se proporciona, se usará el email.
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// ID de la organización a la que pertenecerá el usuario.
        /// </summary>
        [Required(ErrorMessage = "La organización es requerida")]
        public Guid OrganizationId { get; set; }

        /// <summary>
        /// Rol inicial a asignar al usuario.
        /// Debe ser uno de: Admin, Inspector, Cliente
        /// </summary>
        [Required(ErrorMessage = "El rol es requerido")]
        public string Role { get; set; } = null!;

        /// <summary>
        /// Si se debe generar una contraseña provisional automáticamente.
        /// Si es false, se debe proporcionar Password.
        /// </summary>
        public bool GenerateTemporaryPassword { get; set; } = true;

        /// <summary>
        /// Contraseña personalizada. Solo se usa si GenerateTemporaryPassword es false.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Teléfono del usuario (opcional).
        /// </summary>
        public string? PhoneNumber { get; set; }
    }

    /// <summary>
    /// DTO de respuesta al crear un usuario.
    /// </summary>
    public class CreateUserResponseDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string TemporaryPassword { get; set; } = null!;
        public bool MustChangePassword { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO para actualizar información de un usuario.
    /// </summary>
    public class UpdateUserRequestDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// DTO para listar usuarios.
    /// </summary>
    public class UserListDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string FullName => $"{FirstName} {LastName}";
        public IList<string> Roles { get; set; } = new List<string>();
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

