using VisioAnalytica.App.Risk.Models;
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

    public AuthService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        
        // Cargar token guardado al inicializar
        LoadStoredAuth();
    }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(CurrentToken);

    public string? CurrentToken { get; private set; }

    public string? CurrentUserEmail { get; private set; }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _apiClient.PostAsync<RegisterRequest, AuthResponse>(
                "/api/auth/register", request);

            if (response != null)
            {
                await SaveAuthAsync(response.Token, response.Email);
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
                await SaveAuthAsync(response.Token, response.Email);
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

    public async Task LogoutAsync()
    {
        CurrentToken = null;
        CurrentUserEmail = null;
        _apiClient.SetAuthToken(null);

        // Limpiar almacenamiento
        await SecureStorage.SetAsync(TokenKey, string.Empty);
        await SecureStorage.SetAsync(UserEmailKey, string.Empty);
    }

    private async Task SaveAuthAsync(string token, string email)
    {
        CurrentToken = token;
        CurrentUserEmail = email;
        _apiClient.SetAuthToken(token);

        // Guardar en SecureStorage (almacenamiento seguro)
        await SecureStorage.SetAsync(TokenKey, token);
        await SecureStorage.SetAsync(UserEmailKey, email);
    }

    private void LoadStoredAuth()
    {
        try
        {
            var token = SecureStorage.GetAsync(TokenKey).Result;
            var email = SecureStorage.GetAsync(UserEmailKey).Result;

            if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(email))
            {
                CurrentToken = token;
                CurrentUserEmail = email;
                _apiClient.SetAuthToken(token);
            }
        }
        catch
        {
            // Si hay error al cargar, simplemente no hay sesión guardada
            CurrentToken = null;
            CurrentUserEmail = null;
        }
    }
}

