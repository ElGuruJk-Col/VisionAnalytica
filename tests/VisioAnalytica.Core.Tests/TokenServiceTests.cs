// En: tests/VisioAnalytica.Core.Tests/TokenServiceTests.cs
// (¡MEJORADO con FluentAssertions y mejores prácticas!)

using FluentAssertions;
using Moq;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Infrastructure.Services;
using Xunit;

namespace VisioAnalytica.Core.Tests;

/// <summary>
/// Tests unitarios para TokenService.
/// Verifica la generación correcta de tokens JWT con claims personalizados.
/// </summary>
public class TokenServiceTests
{
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly TokenService _sut; // System Under Test

    public TokenServiceTests()
    {
        _mockConfig = new Mock<IConfiguration>();

        // Configurar los "Secretos" del token usando collection expressions
        // La clave debe ser larga para cumplir con el requisito de seguridad SHA512.
        _mockConfig.SetupGet(x => x[It.Is<string>(s => s == "Jwt:Key")])
            .Returns("ESTA-ES-UNA-CLAVE-SECRETA-MUY-LARGA-Y-COMPLEJA-QUE-DEBES-CAMBIAR-EN-PRODUCCION-Y-DEBE-TENER-AL-MENOS-256-BITS");
        _mockConfig.SetupGet(x => x[It.Is<string>(s => s == "Jwt:Issuer")])
            .Returns("test-issuer");
        _mockConfig.SetupGet(x => x[It.Is<string>(s => s == "Jwt:Audience")])
            .Returns("test-audience");

        // Crear la instancia real del servicio con el Mock (SUT)
        _sut = new TokenService(_mockConfig.Object);
    }

    [Fact]
    public void CreateToken_ShouldReturnValidJwtToken_WithCorrectClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@org.com",
            UserName = "test@org.com",
            FirstName = "Test",
            OrganizationId = orgId,
            MustChangePassword = false
        };
        var roles = new List<string> { "Inspector", "Admin" };

        // Act
        var tokenString = _sut.CreateToken(user, roles);
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(tokenString) as JwtSecurityToken;

        // Assert usando FluentAssertions
        jsonToken.Should().NotBeNull();
        jsonToken!.Issuer.Should().Be("test-issuer");
        jsonToken.Audiences.Should().Contain("test-audience");

        // Verificar claims estándar
        jsonToken.Claims.Should().Contain(c => 
            c.Type == JwtRegisteredClaimNames.Email && 
            c.Value == "test@org.com");
        
        // Verificar claims personalizados
        jsonToken.Claims.Should().Contain(c => 
            c.Type == "uid" && 
            c.Value == userId.ToString());
        
        jsonToken.Claims.Should().Contain(c => 
            c.Type == "org_id" && 
            c.Value == orgId.ToString());
        
        jsonToken.Claims.Should().Contain(c => 
            c.Type == "must_change_password" && 
            c.Value == "false");

        // Verificar roles - usar "role" o ClaimTypes.Role
        var roleClaims = jsonToken.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .ToList();
        
        roleClaims.Should().BeEquivalentTo(roles);
    }

    [Fact]
    public void CreateToken_ShouldIncludeMustChangePassword_WhenTrue()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@org.com",
            UserName = "test@org.com",
            OrganizationId = Guid.NewGuid(),
            MustChangePassword = true
        };

        // Act
        var tokenString = _sut.CreateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(tokenString) as JwtSecurityToken;

        // Assert
        jsonToken.Should().NotBeNull();
        jsonToken!.Claims.Should().Contain(c => 
            c.Type == "must_change_password" && 
            c.Value == "true");
    }

    [Fact]
    public void CreateToken_ShouldWork_WithoutRoles()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@org.com",
            UserName = "test@org.com",
            OrganizationId = Guid.NewGuid()
        };

        // Act
        var tokenString = _sut.CreateToken(user, null);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(tokenString) as JwtSecurityToken;
        jsonToken.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenJwtKeyIsMissing()
    {
        // Arrange
        _mockConfig.SetupGet(x => x[It.Is<string>(s => s == "Jwt:Key")])
            .Returns((string?)null);

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            new TokenService(_mockConfig.Object));
        
        exception.ParamName.Should().Be("config");
        exception.Message.Should().Contain("Jwt:Key");
    }

    [Fact]
    public void CreateToken_ShouldSetExpiration_To7Days()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@org.com",
            UserName = "test@org.com",
            OrganizationId = Guid.NewGuid()
        };

        // Act
        var tokenString = _sut.CreateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(tokenString) as JwtSecurityToken;

        // Assert
        jsonToken.Should().NotBeNull();
        jsonToken!.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(5));
    }
}
