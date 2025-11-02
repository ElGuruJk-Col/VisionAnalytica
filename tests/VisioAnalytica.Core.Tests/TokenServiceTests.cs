// En: tests/VisioAnalytica.Core.Tests/TokenServiceTests.cs
// (¡CORREGIDO! Fix para CS8600 y CS1061)

using Moq;
using System;
using System.Security.Claims;
using Xunit;
using Microsoft.Extensions.Configuration;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Infrastructure.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace VisioAnalytica.Core.Tests
{
    public class TokenServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly TokenService _sut; // System Under Test

        public TokenServiceTests()
        {
            _mockConfig = new Mock<IConfiguration>();

            // 1. Configurar los "Secretos" del token. 
            //    La clave debe ser larga para cumplir con el requisito de seguridad SHA512.
            _mockConfig.SetupGet(x => x[It.Is<string>(s => s == "Jwt:Key")]).Returns("ESTA-ES-UNA-CLAVE-SECRETA-MUY-LARGA-Y-COMPLEJA-QUE-DEBES-CAMBIAR-EN-PRODUCCION-Y-DEBE-TENER-AL-MENOS-256-BITS");
            _mockConfig.SetupGet(x => x[It.Is<string>(s => s == "Jwt:Issuer")]).Returns("test-issuer");
            _mockConfig.SetupGet(x => x[It.Is<string>(s => s == "Jwt:Audience")]).Returns("test-audience");

            // 2. Crear la instancia real del servicio con el Mock (SUT)
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
                OrganizationId = orgId
            };

            // Act
            var tokenString = _sut.CreateToken(user);
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(tokenString) as JwtSecurityToken;

            // Assert
            Assert.NotNull(jsonToken);

            // Verificación del Issuer (emisor)
            Assert.Equal("test-issuer", jsonToken.Issuer);

            // Verificación de Claims (estándar y personalizados)
            Assert.Contains(jsonToken.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "test@org.com");
            Assert.Contains(jsonToken.Claims, c => c.Type == "uid" && c.Value == userId.ToString());
            Assert.Contains(jsonToken.Claims, c => c.Type == "org_id" && c.Value == orgId.ToString());
        }

        [Fact]
        public void Constructor_ShouldThrowException_WhenJwtKeyIsMissing()
        {
            // Arrange
            // FIX CS8600: Indicamos que el valor de retorno del mock es un string nullable.
            _mockConfig.SetupGet(x => x[It.Is<string>(s => s == "Jwt:Key")]).Returns((string?)null);

            // Act & Assert
            // El servicio debe lanzar ArgumentNullException si la clave no existe.
            Assert.Throws<ArgumentNullException>(() => new TokenService(_mockConfig.Object));
        }
    }
}
