using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Models;

/// <summary>
/// Modelos para inspecciones en la aplicación MAUI.
/// </summary>
public record CreateInspectionRequest(
    Guid AffiliatedCompanyId,
    List<PhotoRequest> Photos
);

public record PhotoRequest(
    string ImageBase64,
    DateTime CapturedAt,
    string? Description = null
);

public record AnalyzeInspectionRequest(
    Guid InspectionId,
    List<Guid> PhotoIds
);

