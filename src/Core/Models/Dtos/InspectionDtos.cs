namespace VisioAnalytica.Core.Models.Dtos;

/// <summary>
/// DTO para crear una nueva inspección con múltiples fotos.
/// </summary>
public record CreateInspectionDto(
    Guid AffiliatedCompanyId,
    List<PhotoDto> Photos
);

/// <summary>
/// DTO para una foto capturada.
/// </summary>
public record PhotoDto(
    string ImageBase64,
    DateTime CapturedAt,
    string? Description = null
);

/// <summary>
/// DTO para solicitar análisis de múltiples fotos de una inspección.
/// </summary>
public record AnalyzeInspectionDto(
    Guid InspectionId,
    List<Guid> PhotoIds
);

/// <summary>
/// DTO para respuesta de inspección.
/// </summary>
public record InspectionDto(
    Guid Id,
    Guid AffiliatedCompanyId,
    string AffiliatedCompanyName,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int PhotosCount,
    int AnalyzedPhotosCount,
    int FindingsCount,
    List<PhotoInfoDto> Photos
);

/// <summary>
/// DTO para información de una foto.
/// </summary>
public record PhotoInfoDto(
    Guid Id,
    string ImageUrl,
    DateTime CapturedAt,
    string? Description,
    bool IsAnalyzed,
    Guid? AnalysisId
);

/// <summary>
/// DTO para el estado de análisis de una inspección.
/// </summary>
public record InspectionAnalysisStatusDto(
    Guid InspectionId,
    string Status,
    int TotalPhotos,
    int AnalyzedPhotos,
    int PendingPhotos,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage
);

