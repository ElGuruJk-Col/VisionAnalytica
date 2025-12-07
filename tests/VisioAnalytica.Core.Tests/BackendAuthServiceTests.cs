using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Infrastructure.Data;
using VisioAnalytica.Infrastructure.Services;
using Xunit;

namespace VisioAnalytica.Core.Tests;

/// <summary>
/// Tests unitarios para AuthService (Backend API).
/// Verifica registro, login, cambio de contraseña y recuperación.
/// Usa características modernas de .NET 10.0 y C# 14.
/// </summary>
public class BackendAuthServiceTests
{
    private readonly Mock<UserManager<User>> _mockUserManager;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly VisioAnalyticaDbContext _dbContext;
    private readonly AuthService _sut;

    public BackendAuthServiceTests()
    {
        // Configurar DbContext en memoria para tests
        // Ignorar advertencias de transacciones (InMemory no las soporta)
        var options = new DbContextOptionsBuilder<VisioAnalyticaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new VisioAnalyticaDbContext(options);

        // Configurar mocks
        var userStore = new Mock<IUserStore<User>>();
        _mockUserManager = new Mock<UserManager<User>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        
        _mockTokenService = new Mock<ITokenService>();
        _mockEmailService = new Mock<IEmailService>();

        // Crear instancia del servicio usando primary constructor
        _sut = new AuthService(
            _dbContext,
            _mockUserManager.Object,
            _mockTokenService.Object,
            _mockEmailService.Object,
            null); // Configuration opcional
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateUser_AndReturnUserDto_WhenSuccessful()
    {
        // Arrange
        var registerDto = new RegisterDto(
            "test@example.com",
            "Password123!",
            "Test",
            "User",
            "Test Organization");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = registerDto.Email,
            UserName = registerDto.Email,
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName
        };

        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager
            .Setup(x => x.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockTokenService
            .Setup(x => x.CreateToken(It.IsAny<User>(), It.IsAny<IList<string>>()))
            .Returns("test-token");

        // Act
        var result = await _sut.RegisterAsync(registerDto);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(registerDto.Email);
        result.FirstName.Should().Be(registerDto.FirstName);
        result.Token.Should().Be("test-token");
        
        _mockUserManager.Verify(
            x => x.CreateAsync(
                It.Is<User>(u => u.Email == registerDto.Email),
                registerDto.Password),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnUserDto_WhenCredentialsAreValid()
    {
        // Arrange
        var loginDto = new LoginDto("test@example.com", "Password123!");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = loginDto.Email,
            UserName = loginDto.Email,
            FirstName = "Test",
            IsActive = true
        };
        var roles = new[] { "Inspector" };

        _mockUserManager
            .Setup(x => x.FindByEmailAsync(loginDto.Email))
            .ReturnsAsync(user);

        _mockUserManager
            .Setup(x => x.CheckPasswordAsync(user, loginDto.Password))
            .ReturnsAsync(true);

        _mockUserManager
            .Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(roles);

        _mockUserManager
            .Setup(x => x.ResetAccessFailedCountAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockTokenService
            .Setup(x => x.CreateToken(user, roles))
            .Returns("test-token");

        // Act
        var result = await _sut.LoginAsync(loginDto);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(loginDto.Email);
        result.FirstName.Should().Be("Test");
        result.Token.Should().Be("test-token");
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowUnauthorizedAccessException_WhenPasswordIsIncorrect()
    {
        // Arrange
        var loginDto = new LoginDto("test@example.com", "WrongPassword");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = loginDto.Email,
            IsActive = true
        };

        _mockUserManager
            .Setup(x => x.FindByEmailAsync(loginDto.Email))
            .ReturnsAsync(user);

        _mockUserManager
            .Setup(x => x.CheckPasswordAsync(user, loginDto.Password))
            .ReturnsAsync(false);

        _mockUserManager
            .Setup(x => x.AccessFailedAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager
            .Setup(x => x.IsLockedOutAsync(user))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.LoginAsync(loginDto));
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldSucceed_WhenCurrentPasswordIsCorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var changePasswordDto = new ChangePasswordDto("OldPass123!", "NewPass123!");
        var user = new User
        {
            Id = userId,
            IsActive = true,
            MustChangePassword = false
        };

        _mockUserManager
            .Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        _mockUserManager
            .Setup(x => x.ChangePasswordAsync(user, changePasswordDto.CurrentPassword!, changePasswordDto.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager
            .Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.ChangePasswordAsync(userId, changePasswordDto);

        // Assert
        result.Should().BeTrue();
        user.MustChangePassword.Should().BeFalse();
        user.PasswordChangedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldNotRequireCurrentPassword_WhenMustChangePasswordIsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var changePasswordDto = new ChangePasswordDto(null, "NewPass123!");
        var user = new User
        {
            Id = userId,
            IsActive = true,
            MustChangePassword = true
        };

        _mockUserManager
            .Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        _mockUserManager
            .Setup(x => x.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("reset-token");

        _mockUserManager
            .Setup(x => x.ResetPasswordAsync(user, "reset-token", changePasswordDto.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager
            .Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.ChangePasswordAsync(userId, changePasswordDto);

        // Assert
        result.Should().BeTrue();
        user.MustChangePassword.Should().BeFalse();
        _mockUserManager.Verify(
            x => x.ResetPasswordAsync(user, It.IsAny<string>(), changePasswordDto.NewPassword),
            Times.Once);
    }
}

