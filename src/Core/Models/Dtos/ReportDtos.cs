// En: src/Core/Models/Dtos/ReportDtos.cs
// (¡NUEVO ARCHIVO: DTOs para la capa de Reportes!)

using System;
using System.Collections.Generic;

namespace VisioAnalytica.Core.Models.Dtos
{
    /// <summary>
    /// DTO para la lista de hallazgos de una inspección (vista de detalle).
    /// </summary>
    public record FindingDetailDto
    (
        Guid Id,
        string Description,
        string RiskLevel,
        string CorrectiveAction,
        string PreventiveAction
    );

    // ===============================================

    /// <summary>
    /// DTO para la vista de listado/grilla (solo datos de cabecera).
    /// </summary>
    public record InspectionSummaryDto
    (
        Guid Id,
        DateTime AnalysisDate,
        string ImageUrl,
        string UserName,
        string AffiliatedCompanyName,
        string Status,
        int TotalFindings
    );

    /// <summary>
    /// DTO para la vista de detalle de una sola inspección.
    /// </summary>
    public record InspectionDetailDto
    (
        Guid Id,
        DateTime AnalysisDate,
        string ImageUrl,
        string UserName,
        IReadOnlyList<FindingDetailDto> Findings
    );
}