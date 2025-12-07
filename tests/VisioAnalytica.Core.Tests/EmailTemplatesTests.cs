using FluentAssertions;
using VisioAnalytica.Infrastructure.Services;
using Xunit;

namespace VisioAnalytica.Core.Tests;

/// <summary>
/// Tests unitarios para EmailTemplates.
/// Verifica que las plantillas HTML se generen correctamente.
/// </summary>
public class EmailTemplatesTests
{
    [Fact]
    public void GetPasswordResetTemplate_ShouldContainTemporaryPassword_AndInstructions()
    {
        // Arrange
        var userName = "Juan Pérez";
        var temporaryPassword = "TempPass123!";

        // Act
        var template = EmailTemplates.GetPasswordResetTemplate(userName, temporaryPassword);

        // Assert
        template.Should().NotBeNullOrWhiteSpace();
        template.Should().Contain(userName);
        template.Should().Contain(temporaryPassword);
        template.Should().Contain("contraseña temporal");
        template.Should().Contain("<!DOCTYPE html>");
        template.Should().Contain("</html>");
    }

    [Fact]
    public void GetAccountLockedTemplate_ShouldContainUserName_AndInstructions()
    {
        // Arrange
        var userName = "María García";

        // Act
        var template = EmailTemplates.GetAccountLockedTemplate(userName);

        // Assert
        template.Should().NotBeNullOrWhiteSpace();
        template.Should().Contain(userName);
        template.Should().Contain("bloqueada");
        template.Should().Contain("Recuperar Contraseña");
        template.Should().Contain("<!DOCTYPE html>");
    }

    [Fact]
    public void GetInspectorWithoutCompaniesTemplate_ShouldContainAllNames_AndInstructions()
    {
        // Arrange
        var supervisorName = "Supervisor Test";
        var inspectorEmail = "inspector@test.com";
        var inspectorName = "Inspector Test";

        // Act
        var template = EmailTemplates.GetInspectorWithoutCompaniesTemplate(
            supervisorName, inspectorEmail, inspectorName);

        // Assert
        template.Should().NotBeNullOrWhiteSpace();
        template.Should().Contain(supervisorName);
        template.Should().Contain(inspectorEmail);
        template.Should().Contain(inspectorName);
        template.Should().Contain("sin Empresas Asignadas", AtLeast.Once());
        template.Should().Contain("<!DOCTYPE html>");
    }

    [Fact]
    public void GetWelcomeTemplate_ShouldContainUserName_AndTemporaryPassword()
    {
        // Arrange
        var userName = "Nuevo Usuario";
        var temporaryPassword = "Welcome123!";

        // Act
        var template = EmailTemplates.GetWelcomeTemplate(userName, temporaryPassword);

        // Assert
        template.Should().NotBeNullOrWhiteSpace();
        template.Should().Contain(userName);
        template.Should().Contain(temporaryPassword);
        template.Should().Contain("Bienvenido");
        template.Should().Contain("<!DOCTYPE html>");
    }

    [Fact]
    public void GetAnalysisCompleteTemplate_ShouldContainCompanyName_AndInspectionId()
    {
        // Arrange
        var companyName = "Empresa Test S.A.";
        var inspectionId = Guid.NewGuid();

        // Act
        var template = EmailTemplates.GetAnalysisCompleteTemplate(companyName, inspectionId);

        // Assert
        template.Should().NotBeNullOrWhiteSpace();
        template.Should().Contain(companyName);
        template.Should().Contain(inspectionId.ToString());
        template.Should().Contain("Análisis Completado");
        template.Should().Contain("<!DOCTYPE html>");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void GetPasswordResetTemplate_ShouldHandleEmptyUserName(string? userName)
    {
        // Arrange
        var temporaryPassword = "TempPass123!";

        // Act
        var template = EmailTemplates.GetPasswordResetTemplate(userName ?? string.Empty, temporaryPassword);

        // Assert
        template.Should().NotBeNullOrWhiteSpace();
        template.Should().Contain(temporaryPassword);
    }
}

