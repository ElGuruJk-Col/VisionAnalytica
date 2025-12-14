using Microsoft.Extensions.Logging;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Implementación de IReportService. Contiene la lógica de negocio 
    /// para consultar datos históricos, aplicando el filtro Multi-Tenant.
    /// </summary>
    public class ReportService : IReportService
    {
        private readonly IAnalysisRepository _analysisRepository;
        private readonly ILogger<ReportService> _logger;

        public ReportService(IAnalysisRepository analysisRepository, ILogger<ReportService> logger)
        {
            _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("ReportService inicializado.");
        }

        // --- MÉTODOS DE LECTURA DE DETALLE ---

        public async Task<InspectionDetailDto?> GetInspectionDetailsAsync(Guid inspectionId)
        {
            _logger.LogInformation("Buscando detalles de inspección {InspectionId}.", inspectionId);

            // 1. Obtener la entidad de la BBDD (incluye Findings gracias al Include del Repository)
            var inspection = await _analysisRepository.GetInspectionByIdAsync(inspectionId);

            if (inspection == null)
            {
                _logger.LogWarning("Inspección {InspectionId} no encontrada.", inspectionId);
                return null;
            }

            // 2. Mapear (transformar) los Hallazgos de todas las fotos al DTO (InspectionDetailDto)
            //    ✅ CORRECCIÓN: Los hallazgos están en las fotos, no en la inspección
            var findingDtos = inspection.Photos?
                .Where(p => p.IsAnalyzed && p.Findings != null)
                .SelectMany(p => p.Findings.Select(f => new FindingDetailDto
                (
                    Id: f.Id,
                    Description: f.Description,
                    RiskLevel: f.RiskLevel,
                    CorrectiveAction: f.CorrectiveAction,
                    PreventiveAction: f.PreventiveAction
                )))
                .ToList() ?? [];

            // 3. Devolver el DTO del detalle
            return new InspectionDetailDto
            (
                Id: inspection.Id,
                AnalysisDate: inspection.AnalysisDate,
                UserName: inspection.User.UserName!, // Asumimos que el usuario fue incluido por el Repository (o lo inyectaremos a futuro).
                Findings: findingDtos
            );
        }

        // --- MÉTODOS DE LECTURA DE HISTORIAL (Summary) ---

        public async Task<IReadOnlyList<InspectionSummaryDto>> GetInspectionHistoryAsync(Guid organizationId)
        {
            _logger.LogInformation("Consultando historial para Organización {OrganizationId}.", organizationId);

            // 1. Obtener el listado de Entidades (filtrado por Multi-Tenant en el Repository)
            var inspections = await _analysisRepository.GetInspectionsByOrganizationAsync(organizationId);

            if (!inspections.Any())
            {
                _logger.LogInformation("No se encontraron inspecciones para la Organización {OrganizationId}.", organizationId);
                return [];
            }

            // 2. Mapear al DTO de Resumen (SummaryDto)
            var summaryList = inspections.Select(i => new InspectionSummaryDto
            (
                Id: i.Id,
                AnalysisDate: i.AnalysisDate != default ? i.AnalysisDate : i.StartedAt, // Usar StartedAt si AnalysisDate no está establecido
                UserName: i.User?.UserName ?? i.User?.Email ?? $"Usuario {i.UserId}", // Manejar User null
                AffiliatedCompanyName: i.AffiliatedCompany?.Name ?? "N/A",
                Status: i.Status ?? "Draft",
                TotalFindings: i.Photos?.Sum(p => p.Findings?.Count ?? 0) ?? 0 // ✅ CORRECCIÓN: Sumar hallazgos de todas las fotos
            )).ToList();

            return summaryList;
        }
    }
}