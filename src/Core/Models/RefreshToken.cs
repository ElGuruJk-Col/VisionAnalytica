using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VisioAnalytica.Core.Models;

/// <summary>
/// Representa un refresh token para renovar tokens JWT de acceso.
/// Permite mantener sesiones activas sin requerir login frecuente.
/// </summary>
public class RefreshToken
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Token de refresh (string aleatorio único).
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Token { get; set; } = null!;

    /// <summary>
    /// ID del usuario propietario del refresh token.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Propiedad de navegación al usuario.
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// Fecha y hora de expiración del refresh token.
    /// </summary>
    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Fecha y hora de creación del refresh token.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha y hora en que el token fue revocado (si fue revocado).
    /// Null si el token sigue activo.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Indica si el token ha sido revocado.
    /// </summary>
    [NotMapped]
    public bool IsRevoked => RevokedAt.HasValue;

    /// <summary>
    /// Indica si el token ha expirado.
    /// </summary>
    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Indica si el token está activo (no revocado y no expirado).
    /// </summary>
    [NotMapped]
    public bool IsActive => !IsRevoked && !IsExpired;

    /// <summary>
    /// Dirección IP desde la cual se creó el token (para auditoría).
    /// </summary>
    [MaxLength(50)]
    public string? CreatedByIp { get; set; }

    /// <summary>
    /// Dirección IP desde la cual se revocó el token (para auditoría).
    /// </summary>
    [MaxLength(50)]
    public string? RevokedByIp { get; set; }

    /// <summary>
    /// Token de reemplazo si este token fue reemplazado por uno nuevo.
    /// Permite rastrear la cadena de refresh tokens.
    /// </summary>
    [MaxLength(500)]
    public string? ReplacedByToken { get; set; }

    /// <summary>
    /// Razón de revocación (opcional, para auditoría).
    /// </summary>
    [MaxLength(200)]
    public string? ReasonRevoked { get; set; }
}

