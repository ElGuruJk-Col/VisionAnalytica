using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Infrastructure.Services;
using Xunit;

namespace VisioAnalytica.Core.Tests;

/// <summary>
/// Tests unitarios para AnalysisService.
/// Verifica la orquestación del análisis de imágenes SST.
/// </summary>
public class AnalysisServiceTests
{
    private readonly Mock<IAiSstAnalyzer> _mockAiAnalyzer;
    private readonly Mock<IAnalysisRepository> _mockRepository;
    private readonly Mock<IFileStorage> _mockFileStorage;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<ILogger<AnalysisService>> _mockLogger;
    private readonly AnalysisService _sut;

    public AnalysisServiceTests()
    {
        _mockAiAnalyzer = new Mock<IAiSstAnalyzer>();
        _mockRepository = new Mock<IAnalysisRepository>();
        _mockFileStorage = new Mock<IFileStorage>();
        _mockConfig = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<AnalysisService>>();

        // Configurar el prompt maestro
        _mockConfig.SetupGet(x => x["AiPrompts:MasterSst"])
            .Returns("Analiza esta imagen de seguridad y salud en el trabajo...");

        _sut = new AnalysisService(
            _mockAiAnalyzer.Object,
            _mockRepository.Object,
            _mockFileStorage.Object,
            _mockLogger.Object,
            _mockConfig.Object);
    }

    [Fact]
    public async Task PerformSstAnalysisAsync_ShouldReturnResult_WhenAnalysisSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var organizationId = Guid.NewGuid();
        var request = new AnalysisRequestDto(
            ImageBase64: Convert.ToBase64String([1, 2, 3, 4, 5]),
            PromptTemplateId: null,
            CustomPrompt: null);

        var analysisResult = new SstAnalysisResult
        {
            Hallazgos = [
                new HallazgoItem(
                    Descripcion: "Test finding",
                    NivelRiesgo: "ALTO",
                    AccionCorrectiva: "Fix it",
                    AccionPreventiva: "Prevent it")
            ]
        };

        var affiliatedCompanyId = Guid.NewGuid();

        _mockAiAnalyzer
            .Setup(x => x.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
            .ReturnsAsync(analysisResult);

        _mockFileStorage
            .Setup(x => x.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), organizationId))
            .ReturnsAsync("/uploads/test-org/test.jpg");

        _mockRepository
            .Setup(x => x.GetFirstActiveAffiliatedCompanyIdAsync(organizationId))
            .ReturnsAsync(affiliatedCompanyId);

        var inspection = new Inspection
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse(userId),
            OrganizationId = organizationId,
            AffiliatedCompanyId = affiliatedCompanyId
        };

        _mockRepository
            .Setup(x => x.SaveInspectionAsync(It.IsAny<Inspection>()))
            .ReturnsAsync(inspection);

        // Act
        var result = await _sut.PerformSstAnalysisAsync(request, userId, organizationId);

        // Assert
        result.Should().NotBeNull();
        result!.Hallazgos.Should().HaveCount(1);
        result.Hallazgos[0].Descripcion.Should().Be("Test finding");

        _mockAiAnalyzer.Verify(
            x => x.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>()),
            Times.Once);

        _mockFileStorage.Verify(
            x => x.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), organizationId),
            Times.Once);
    }

    [Fact]
    public async Task PerformSstAnalysisAsync_ShouldReturnNull_WhenBase64IsInvalid()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var organizationId = Guid.NewGuid();
        var request = new AnalysisRequestDto(
            ImageBase64: "invalid-base64!!!",
            PromptTemplateId: null,
            CustomPrompt: null);

        // Act
        var result = await _sut.PerformSstAnalysisAsync(request, userId, organizationId);

        // Assert
        result.Should().BeNull();
        _mockAiAnalyzer.Verify(
            x => x.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task PerformSstAnalysisAsync_ShouldUseCustomPrompt_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var organizationId = Guid.NewGuid();
        var customPrompt = "Custom analysis prompt";
        var request = new AnalysisRequestDto(
            ImageBase64: Convert.ToBase64String([1, 2, 3]),
            PromptTemplateId: null,
            CustomPrompt: customPrompt);

        var analysisResult = new SstAnalysisResult { Hallazgos = [] };
        var affiliatedCompanyId = Guid.NewGuid();

        _mockAiAnalyzer
            .Setup(x => x.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
            .ReturnsAsync(analysisResult);

        _mockFileStorage
            .Setup(x => x.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), organizationId))
            .ReturnsAsync("/uploads/test.jpg");

        _mockRepository
            .Setup(x => x.GetFirstActiveAffiliatedCompanyIdAsync(organizationId))
            .ReturnsAsync(affiliatedCompanyId);

        var inspection = new Inspection
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse(userId),
            OrganizationId = organizationId,
            AffiliatedCompanyId = affiliatedCompanyId
        };

        _mockRepository
            .Setup(x => x.SaveInspectionAsync(It.IsAny<Inspection>()))
            .ReturnsAsync(inspection);

        // Act
        await _sut.PerformSstAnalysisAsync(request, userId, organizationId);

        // Assert
        _mockAiAnalyzer.Verify(
            x => x.AnalyzeImageAsync(
                It.IsAny<byte[]>(),
                It.Is<string>(p => p == customPrompt)),
            Times.Once);
    }

    [Fact]
    public async Task PerformSstAnalysisAsync_ShouldThrowException_WhenNoAffiliatedCompany()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var organizationId = Guid.NewGuid();
        var request = new AnalysisRequestDto(
            ImageBase64: Convert.ToBase64String([1, 2, 3]),
            PromptTemplateId: null,
            CustomPrompt: null);

        var analysisResult = new SstAnalysisResult { Hallazgos = [] };

        _mockAiAnalyzer
            .Setup(x => x.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
            .ReturnsAsync(analysisResult);

        _mockFileStorage
            .Setup(x => x.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), organizationId))
            .ReturnsAsync("/uploads/test.jpg");

        _mockRepository
            .Setup(x => x.GetFirstActiveAffiliatedCompanyIdAsync(organizationId))
            .ReturnsAsync((Guid?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.PerformSstAnalysisAsync(request, userId, organizationId));
    }
}

