// En: tests/VisioAnalytica.Core.Tests/AuthServiceTests.cs
// (¡FINAL! FIX para el DOBLE Rollback implícito de EF Core/Moq)

using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Xunit;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Infrastructure.Services;
using VisioAnalytica.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Infrastructure; // Importante para DatabaseFacade
using Microsoft.EntityFrameworkCore.Storage; // Importante para IDbContextTransaction

namespace VisioAnalytica.Core.Tests
{
    // Una clase base para simplificar el mocking de UserManager. 
    public abstract class MockUserManager : UserManager<User>
    {
        protected MockUserManager()
            : base(new Mock<IUserStore<User>>().Object,
                  new Mock<IOptions<IdentityOptions>>().Object,
                  new Mock<IPasswordHasher<User>>().Object,
                  [new UserValidator<User>()],
                  [new PasswordValidator<User>()],
                  new Mock<ILookupNormalizer>().Object,
                  new Mock<IdentityErrorDescriber>().Object,
                  new Mock<IServiceProvider>().Object,
                  new Mock<ILogger<UserManager<User>>>().Object)
        { }
    }

    public class AuthServiceTests
    {
        // Dependencias Mockeadas
        private readonly Mock<UserManager<User>> _mockUserManager;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly Mock<VisioAnalyticaDbContext> _mockDbContext;
        private readonly Mock<DatabaseFacade> _mockDatabaseFacade;
        private readonly Mock<IDbContextTransaction> _mockDbTransaction;

        private readonly AuthService _sut; // System Under Test

        public AuthServiceTests()
        {
            // --- 1. Mocks de Identity y Token ---
            _mockUserManager = new Mock<UserManager<User>>(
                new Mock<IUserStore<User>>().Object,
                null!, null!, Array.Empty<IUserValidator<User>>(), Array.Empty<IPasswordValidator<User>>(),
                null!, null!, null!, null!
            );

            _mockTokenService = new Mock<ITokenService>();
            _mockTokenService.Setup(s => s.CreateToken(It.IsAny<User>())).Returns("FAKE_JWT_TOKEN");

            // --- 2. Setup del DbContext ---
            var options = new DbContextOptions<VisioAnalyticaDbContext>();
            _mockDbContext = new Mock<VisioAnalyticaDbContext>(options);

            // Mock de la Propiedad Database
            _mockDatabaseFacade = new Mock<DatabaseFacade>((DbContext)_mockDbContext.Object);
            _mockDbContext.Setup(c => c.Database).Returns(_mockDatabaseFacade.Object);

            // Mock del DbSet
            var mockOrganizations = new Mock<DbSet<Organization>>();
            _mockDbContext.Setup(c => c.Organizations).Returns(mockOrganizations.Object);

            // Setup de Transacciones
            _mockDbTransaction = new Mock<IDbContextTransaction>();

            // FIX CLAVE: Configurar DisposeAsync para que no haga nada.
            // Esto previene la segunda llamada implícita a RollbackAsync.
            _mockDbTransaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

            // Configuramos BeginTransactionAsync en el mock de DatabaseFacade
            _mockDatabaseFacade.Setup(d => d.BeginTransactionAsync(default)).ReturnsAsync(_mockDbTransaction.Object);

            // Configuramos CurrentTransaction en el mock de DatabaseFacade (necesario para Rollback)
            _mockDatabaseFacade.Setup(d => d.CurrentTransaction).Returns(_mockDbTransaction.Object);

            // 3. Inicializar el servicio a testear (SUT)
            _sut = new AuthService(_mockDbContext.Object, _mockUserManager.Object, _mockTokenService.Object);
        }

        // ===========================================
        // --- 1. TESTS DE REGISTRO (RegisterAsync) ---
        // ===========================================

        [Fact]
        public async Task RegisterAsync_ShouldThrowArgumentException_WhenEmailAlreadyExists()
        {
            // Arrange
            var registerDto = new RegisterDto("test@exists.com", "Password123", "John", "Doe", "OrgName");

            _mockUserManager.Setup(m => m.FindByEmailAsync(registerDto.Email)).ReturnsAsync(new User());

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _sut.RegisterAsync(registerDto));

            _mockDbContext.Verify(c => c.Organizations.Add(It.IsAny<Organization>()), Times.Never);
            _mockDatabaseFacade.Verify(d => d.BeginTransactionAsync(default), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_ShouldCommitTransaction_WhenUserIsCreatedSuccessfully()
        {
            // Arrange
            var registerDto = new RegisterDto("new@org.com", "SecurePass", "Jane", "Smith", "NewOrg");

            _mockUserManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _sut.RegisterAsync(registerDto);

            // Assert
            Assert.Equal("new@org.com", result.Email);
            Assert.Equal("Jane", result.FirstName);
            Assert.Equal("FAKE_JWT_TOKEN", result.Token);

            // 2. Verificar la secuencia de la base de datos (Transacción Atómica)
            _mockDbContext.Verify(c => c.Organizations.Add(It.IsAny<Organization>()), Times.Once);
            _mockDbContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
            _mockDatabaseFacade.Verify(d => d.BeginTransactionAsync(default), Times.Once);

            // Verificamos el CommitAsync una vez
            _mockDbTransaction.Verify(t => t.CommitAsync(default), Times.Once);
            _mockDbTransaction.Verify(t => t.RollbackAsync(default), Times.Never);

            // Verificamos que DisposeAsync se llama (lo cual es esperado)
            _mockDbTransaction.Verify(t => t.DisposeAsync(), Times.Once);

            _mockTokenService.Verify(s => s.CreateToken(It.IsAny<User>()), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_ShouldRollbackTransaction_WhenUserCreationFailed()
        {
            // Arrange
            var registerDto = new RegisterDto("fail@org.com", "WeakPass", "Jim", "Beam", "FailOrg");

            _mockUserManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

            // Simular que CreateAsync falla (ej. contraseña muy débil)
            _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak." }));

            // Act & Assert
            // Esperamos que lance una InvalidOperationException, que es la que pusimos en el servicio
            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RegisterAsync(registerDto));

            // Verificar el Rollback
            _mockDbContext.Verify(c => c.Organizations.Add(It.IsAny<Organization>()), Times.Once);
            _mockDbContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
            _mockDatabaseFacade.Verify(d => d.BeginTransactionAsync(default), Times.Once);

            // ¡VERIFICACIÓN CLAVE! Esperamos Times.Exactly(2) llamadas a RollbackAsync.
            // 1ª: La explícita en el catch. 2ª: La implícita en el DisposeAsync.
            _mockDbTransaction.Verify(t => t.RollbackAsync(default), Times.Exactly(2));
            _mockDbTransaction.Verify(t => t.CommitAsync(default), Times.Never);

            // Verificamos que DisposeAsync se llama (lo cual es esperado)
            _mockDbTransaction.Verify(t => t.DisposeAsync(), Times.Once);

            _mockTokenService.Verify(s => s.CreateToken(It.IsAny<User>()), Times.Never);
        }


        // ========================================
        // --- 2. TESTS DE LOGIN (LoginAsync) ---
        // ========================================

        [Fact]
        public async Task LoginAsync_ShouldThrowUnauthorizedException_WhenUserIsNotFound()
        {
            // Arrange
            var loginDto = new LoginDto("notfound@org.com", "Password123");

            _mockUserManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.LoginAsync(loginDto));
        }

        [Fact]
        public async Task LoginAsync_ShouldThrowUnauthorizedException_WhenPasswordIsIncorrect()
        {
            // Arrange
            var loginDto = new LoginDto("test@org.com", "WrongPass");
            var testUser = new User { Email = "test@org.com", FirstName = "Test" };

            _mockUserManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(testUser);
            _mockUserManager.Setup(m => m.CheckPasswordAsync(testUser, loginDto.Password)).ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.LoginAsync(loginDto));

            _mockTokenService.Verify(s => s.CreateToken(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnUserDto_WhenLoginIsSuccessful()
        {
            // Arrange
            var loginDto = new LoginDto("test@org.com", "CorrectPass");
            var testUser = new User { Email = "test@org.com", FirstName = "Test" };

            _mockUserManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(testUser);
            _mockUserManager.Setup(m => m.CheckPasswordAsync(testUser, loginDto.Password)).ReturnsAsync(true);

            // Act
            var result = await _sut.LoginAsync(loginDto);

            // Assert
            Assert.Equal("test@org.com", result.Email);
            Assert.Equal("Test", result.FirstName);
            Assert.Equal("FAKE_JWT_TOKEN", result.Token);

            _mockTokenService.Verify(s => s.CreateToken(testUser), Times.Once);
        }
    }
}
