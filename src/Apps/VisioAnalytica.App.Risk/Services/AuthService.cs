using VisioAnalytica.App.Risk.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Implementación del servicio de autenticación.
/// Maneja registro, login y almacenamiento de tokens.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IApiClient _apiClient;
    private const string TokenKey = "auth_token";
    private const string UserEmailKey = "user_email";
    private bool _mustChangePassword;
    private IList<string> _currentUserRoles = []; // Collection expression

    public AuthService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        
        // Cargar token guardado al inicializar
        LoadStoredAuth();
    }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(CurrentToken);

    public string? CurrentToken { get; private set; }

    public string? CurrentUserEmail { get; private set; }

    public bool MustChangePassword => _mustChangePassword;

    public IList<string> CurrentUserRoles => _currentUserRoles;

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _apiClient.PostAsync<RegisterRequest, AuthResponse>(
                "/api/auth/register", request);

            if (response != null)
            {
                await SaveAuthAsync(response.Token, response.Email, response.MustChangePassword, response.Roles);
            }

            return response;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException($"Error al registrar usuario: {ex.Message}", ex);
        }
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _apiClient.PostAsync<LoginRequest, AuthResponse>(
                "/api/auth/login", request);

            if (response != null)
            {
                await SaveAuthAsync(response.Token, response.Email, response.MustChangePassword, response.Roles);
            }

            return response;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException($"Error al iniciar sesión: {ex.Message}", ex);
        }
    }

    public async Task<bool> ChangePasswordAsync(ChangePasswordRequest request)
    {
        try
        {
            var response = await _apiClient.PostAsync<ChangePasswordRequest, object>(
                "/api/auth/change-password", request);
            return response != null;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException($"Error al cambiar contraseña: {ex.Message}", ex);
        }
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        try
        {
            var response = await _apiClient.PostAsync<ForgotPasswordRequest, object>(
                "/api/auth/forgot-password", request);
            return response != null;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException($"Error al solicitar recuperación de contraseña: {ex.Message}", ex);
        }
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try
        {
            var response = await _apiClient.PostAsync<ResetPasswordRequest, object>(
                "/api/auth/reset-password", request);
            return response != null;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException($"Error al restablecer contraseña: {ex.Message}", ex);
        }
    }

    public async Task LogoutAsync()
    {
        CurrentToken = null;
        CurrentUserEmail = null;
        _mustChangePassword = false;
        _currentUserRoles = []; // Collection expression
        _apiClient.SetAuthToken(null);

        // Limpiar almacenamiento
        await SecureStorage.SetAsync(TokenKey, string.Empty);
        await SecureStorage.SetAsync(UserEmailKey, string.Empty);
    }

    private async Task SaveAuthAsync(string token, string email, bool mustChangePassword = false, IList<string>? roles = null)
    {
        CurrentToken = token;
        CurrentUserEmail = email;
        _apiClient.SetAuthToken(token);

        // Decodificar el token para extraer información adicional
        DecodeToken(token);

        // Guardar en SecureStorage (almacenamiento seguro)
        await SecureStorage.SetAsync(TokenKey, token);
        await SecureStorage.SetAsync(UserEmailKey, email);
    }

    private void DecodeToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            
            // Extraer must_change_password
            var mustChangePasswordClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "must_change_password");
            _mustChangePassword = mustChangePasswordClaim != null && 
                                 bool.TryParse(mustChangePasswordClaim.Value, out var mustChange) && mustChange;

            // Extraer roles usando collection expression
            _currentUserRoles = jsonToken.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();
        }
        catch
        {
            // Si hay error al decodificar, usar valores por defecto
            _mustChangePassword = false;
            _currentUserRoles = []; // Collection expression
        }
    }

    private void LoadStoredAuth()
    {
        try
        {
            // Nota: SecureStorage.GetAsync es asíncrono, pero en el constructor no podemos usar await
            // Esta es una limitación conocida. En producción, considerar inicialización asíncrona diferida
            var tokenTask = SecureStorage.GetAsync(TokenKey);
            var emailTask = SecureStorage.GetAsync(UserEmailKey);
            
            // Usar Task.WaitAll para esperar ambas tareas de forma más eficiente
            Task.WaitAll([tokenTask, emailTask]);
            
            var token = tokenTask.Result;
            var email = emailTask.Result;

            if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(email))
            {
                CurrentToken = token;
                CurrentUserEmail = email;
                _apiClient.SetAuthToken(token);
                DecodeToken(token);
            }
        }
        catch
        {
            // Si hay error al cargar, simplemente no hay sesión guardada
            CurrentToken = null;
            CurrentUserEmail = null;
            _mustChangePassword = false;
            _currentUserRoles = []; // Collection expression
        }
    }
}

