// En: tests/VisioAnalytica.Core.Tests/ReportServiceTests.cs
// (¡FINAL! Testeo del ReportService)

using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace VisioAnalytica.Core.Tests
{
    public class ReportServiceTests
    {
        private readonly Mock<IAnalysisRepository> _mockRepository;
        private readonly Mock<ILogger<ReportService>> _mockLogger;
        private readonly ReportService _sut;

        // Datos de prueba para simular el retorno de la BBDD
        private readonly Guid _testOrganizationId = Guid.NewGuid();
        private readonly Guid _testUserId = Guid.NewGuid();
        private readonly Guid _inspectionId = Guid.NewGuid();
        private readonly Inspection _testInspection;

        public ReportServiceTests()
        {
            // --- 1. Crear el Mock de la dependencia ---
            _mockRepository = new Mock<IAnalysisRepository>();
            _mockLogger = new Mock<ILogger<ReportService>>();

            // --- 2. Preparar los datos de prueba de la BBDD (Entidades) ---
            _testInspection = new Inspection
            {
                Id = _inspectionId,
                AnalysisDate = new DateTime(2025, 10, 26, 10, 0, 0, DateTimeKind.Utc),
                UserId = _testUserId,
                OrganizationId = _testOrganizationId,
                // Simulamos la relación con el usuario para el mapeo
                User = new User { Id = _testUserId, UserName = "InspectorTest", FirstName = "Test" }
            };

            // Crear una foto con hallazgos (los hallazgos ahora están en las fotos, no en la inspección)
            var testPhoto = new Photo
            {
                Id = Guid.NewGuid(),
                InspectionId = _inspectionId,
                ImageUrl = "http://blob.storage/image.jpg",
                CapturedAt = DateTime.UtcNow,
                IsAnalyzed = true
            };

            // Añadir Hallazgos a la foto
            var finding1 = new Finding { Id = Guid.NewGuid(), PhotoId = testPhoto.Id, Description = "Falla de EPP", RiskLevel = "ALTO", CorrectiveAction = "Corregir A", PreventiveAction = "Prevenir A" };
            var finding2 = new Finding { Id = Guid.NewGuid(), PhotoId = testPhoto.Id, Description = "Piso mojado", RiskLevel = "MEDIO", CorrectiveAction = "Corregir B", PreventiveAction = "Prevenir B" };
            
            testPhoto.Findings.Add(finding1);
            testPhoto.Findings.Add(finding2);
            
            _testInspection.Photos.Add(testPhoto);

            // --- 3. Configurar el Mock del Repositorio (Simulamos lo que haría la BBDD) ---

            // Setup para GetInspectionHistoryAsync
            var summaryList = new List<Inspection> { _testInspection };
            _mockRepository.Setup(r => r.GetInspectionsByOrganizationAsync(_testOrganizationId))
                           .ReturnsAsync(summaryList.AsReadOnly());

            // Setup para GetInspectionByIdAsync
            _mockRepository.Setup(r => r.GetInspectionByIdAsync(_inspectionId))
                           .ReturnsAsync(_testInspection);

            // --- 4. Inicializar el servicio a testear (SUT) ---
            _sut = new ReportService(_mockRepository.Object, _mockLogger.Object);
        }

        // ===============================================
        // --- TESTS: HISTORIAL (GetInspectionHistoryAsync) ---
        // ===============================================

        [Fact]
        public async Task GetInspectionHistoryAsync_ShouldReturnSummaryDtoList_WhenInspectionsExist()
        {
            // Act
            var result = await _sut.GetInspectionHistoryAsync(_testOrganizationId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);

            var summary = result[0];

            // 1. Verificar el mapeo de la cabecera (DTO de resumen)
            Assert.Equal(_inspectionId, summary.Id);
            Assert.Equal(2, summary.TotalFindings); // Contiene 2 hallazgos (sumados de todas las fotos)

            // 2. Verificar que se usó el repositorio
            _mockRepository.Verify(r => r.GetInspectionsByOrganizationAsync(_testOrganizationId), Times.Once);
        }

        [Fact]
        public async Task GetInspectionHistoryAsync_ShouldReturnEmptyList_WhenNoInspectionsFound()
        {
            // Arrange: Configurar el mock para devolver una lista vacía
            _mockRepository.Setup(r => r.GetInspectionsByOrganizationAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new List<Inspection>().AsReadOnly());

            // Act
            var result = await _sut.GetInspectionHistoryAsync(Guid.NewGuid());

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // ===============================================
        // --- TESTS: DETALLE (GetInspectionDetailsAsync) ---
        // ===============================================

        [Fact]
        public async Task GetInspectionDetailsAsync_ShouldReturnDetailDto_AndMapAllFields()
        {
            // Act
            var result = await _sut.GetInspectionDetailsAsync(_inspectionId);

            // Assert
            Assert.NotNull(result);

            // 1. Verificar la cabecera del DTO de detalle
            Assert.Equal(_inspectionId, result.Id);
            Assert.Equal("InspectorTest", result.UserName); // Debe mapear el UserName

            // 2. Verificar la colección de detalles (Findings)
            Assert.Equal(2, result.Findings.Count);

            var finding = result.Findings[0];
            Assert.Equal("Falla de EPP", finding.Description);
            Assert.Equal("ALTO", finding.RiskLevel);
            Assert.Equal("Corregir A", finding.CorrectiveAction);
            Assert.Equal("Prevenir A", finding.PreventiveAction);

            // 3. Verificar que se usó el repositorio
            _mockRepository.Verify(r => r.GetInspectionByIdAsync(_inspectionId), Times.Once);
        }

        [Fact]
        public async Task GetInspectionDetailsAsync_ShouldReturnNull_WhenInspectionNotFound()
        {
            // Arrange: Configurar el mock para devolver null
            _mockRepository.Setup(r => r.GetInspectionByIdAsync(It.IsAny<Guid>()))
                           .ReturnsAsync((Inspection?)null);

            // Act
            var result = await _sut.GetInspectionDetailsAsync(Guid.NewGuid());

            // Assert
            Assert.Null(result);
        }
    }
}