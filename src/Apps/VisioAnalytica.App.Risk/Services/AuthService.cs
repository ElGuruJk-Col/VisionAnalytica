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
    private const string RefreshTokenKey = "refresh_token";
    private const string UserEmailKey = "user_email";
    private bool _mustChangePassword;
    private IList<string> _currentUserRoles = new List<string>();

    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1, 1);

    public AuthService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        // NO cargar nada en el constructor - se hará de forma diferida cuando se necesite
    }

    /// <summary>
    /// Inicializa la autenticación de forma diferida. Se llama automáticamente cuando se necesita.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
            return;

        await _initSemaphore.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            await LoadStoredAuthAsync();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] EnsureInitializedAsync error: {ex}");
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            // Inicializar de forma síncrona si es necesario (solo lectura, no bloquea)
            if (!_isInitialized)
            {
                // Inicializar en background sin bloquear
                _ = Task.Run(async () => await EnsureInitializedAsync());
            }
            return !string.IsNullOrWhiteSpace(CurrentToken);
        }
    }

    public string? CurrentToken
    {
        get
        {
            // Inicializar si es necesario (sin bloquear)
            if (!_isInitialized)
            {
                _ = Task.Run(async () => await EnsureInitializedAsync());
            }
            return _currentToken;
        }
        private set => _currentToken = value;
    }

    private string? _currentToken;
    private string? _currentUserEmail;

    public string? CurrentUserEmail
    {
        get
        {
            // Inicializar si es necesario (sin bloquear)
            if (!_isInitialized)
            {
                _ = Task.Run(async () => await EnsureInitializedAsync());
            }
            return _currentUserEmail;
        }
        private set => _currentUserEmail = value;
    }

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
                await SaveAuthAsync(response.Token, response.Email, response.RefreshToken, response.MustChangePassword, response.Roles);
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
                await SaveAuthAsync(response.Token, response.Email, response.RefreshToken, response.MustChangePassword, response.Roles);
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
        _currentToken = null;
        _currentUserEmail = null;
        _mustChangePassword = false;
        _currentUserRoles = new List<string>();
        _apiClient.SetAuthToken(null);

        System.Diagnostics.Debug.WriteLine("[AuthService] LogoutAsync: token cleared and SetAuthToken(null) called");

        // Limpiar almacenamiento
        await SecureStorage.SetAsync(TokenKey, string.Empty);
        await SecureStorage.SetAsync(RefreshTokenKey, string.Empty);
        await SecureStorage.SetAsync(UserEmailKey, string.Empty);
    }

    /// <summary>
    /// Renueva el access token usando el refresh token guardado.
    /// </summary>
    public async Task<bool> RefreshTokenAsync()
    {
        try
        {
            var refreshToken = await SecureStorage.GetAsync(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return false; // No hay refresh token guardado
            }

            var response = await _apiClient.RefreshTokenAsync(refreshToken);
            if (response != null)
            {
                // Guardar los nuevos tokens
                await SaveAuthAsync(response.AccessToken, CurrentUserEmail ?? string.Empty, response.RefreshToken, _mustChangePassword, _currentUserRoles);
                return true;
            }

            return false; // Refresh token inválido o expirado
        }
        catch (ApiException)
        {
            // Si falla, limpiar sesión
            await LogoutAsync();
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task SaveAuthAsync(string token, string email, string? refreshToken = null, bool mustChangePassword = false, IList<string>? roles = null)
    {
        _currentToken = token;
        _currentUserEmail = email;
        _apiClient.SetAuthToken(token);

        System.Diagnostics.Debug.WriteLine($"[AuthService] SaveAuthAsync: token length={(token?.Length ?? 0)}, email={email}");

        // Decodificar el token para extraer información adicional
        DecodeToken(token);

        System.Diagnostics.Debug.WriteLine($"[AuthService] SaveAuthAsync: roles={string.Join(',', _currentUserRoles)}; mustChangePassword={_mustChangePassword}");

        // Guardar en SecureStorage (almacenamiento seguro)
        await SecureStorage.SetAsync(TokenKey, token);
        await SecureStorage.SetAsync(UserEmailKey, email);
        
        // Guardar refresh token si está presente
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await SecureStorage.SetAsync(RefreshTokenKey, refreshToken);
        }
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
            _currentUserRoles = new List<string>();
        }
    }

    private async Task LoadStoredAuthAsync()
    {
        try
        {
            // Cargar token y email de forma asíncrona
            var token = await SecureStorage.GetAsync(TokenKey);
            var email = await SecureStorage.GetAsync(UserEmailKey);

            if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(email))
            {
                _currentToken = token;
                _currentUserEmail = email;
                _apiClient.SetAuthToken(token);
                DecodeToken(token);
                
                System.Diagnostics.Debug.WriteLine($"[AuthService] LoadStoredAuthAsync: loaded token length={(token?.Length ?? 0)}, email={email}");
                
                // Verificar si el token ya expiró al cargarlo
                if (IsTokenExpired())
                {
                    // Intentar renovar con refresh token
                    var refreshToken = await SecureStorage.GetAsync(RefreshTokenKey);
                    if (!string.IsNullOrWhiteSpace(refreshToken))
                    {
                        // Intentar renovar en background (no bloquear)
                        try
                        {
                            var refreshed = await RefreshTokenAsync();
                            if (!refreshed)
                            {
                                // Si falla la renovación, limpiar sesión
                                await LogoutAsync();
                            }
                        }
                        catch
                        {
                            // Si hay error, limpiar sesión
                            await LogoutAsync();
                        }
                    }
                    else
                    {
                        // No hay refresh token, limpiar sesión
                        _currentToken = null;
                        _currentUserEmail = null;
                        _mustChangePassword = false;
                        _currentUserRoles = new List<string>();
                        _apiClient.SetAuthToken(null);
                    }
                }
            }
        }
        catch
        {
            // Si hay error al cargar, simplemente no hay sesión guardada
            _currentToken = null;
            _currentUserEmail = null;
            _mustChangePassword = false;
            _currentUserRoles = new List<string>();
        }
    }

    /// <summary>
    /// Verifica si el token actual ha expirado.
    /// NO bloquea el hilo principal - inicia inicialización en background si es necesario.
    /// </summary>
    public bool IsTokenExpired()
    {
        // Si no está inicializado, iniciar inicialización en background pero NO esperar
        if (!_isInitialized)
        {
            // Iniciar inicialización en background (fire and forget)
            _ = Task.Run(async () => await EnsureInitializedAsync());
            // Si no está inicializado, asumir que no hay token (no expirado, simplemente no existe)
            return string.IsNullOrWhiteSpace(_currentToken);
        }

        if (string.IsNullOrWhiteSpace(_currentToken))
            return true;
        
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(_currentToken);
            return token.ValidTo < DateTime.UtcNow;
        }
        catch
        {
            // Si hay error al decodificar, considerar como expirado
            return true;
        }
    }

    /// <summary>
    /// Verifica si el token expirará pronto (dentro del umbral especificado).
    /// NO bloquea el hilo principal.
    /// </summary>
    public bool IsTokenExpiringSoon(TimeSpan? threshold = null)
    {
        // Si no está inicializado, iniciar inicialización en background pero NO esperar
        if (!_isInitialized)
        {
            _ = Task.Run(async () => await EnsureInitializedAsync());
            // Si no está inicializado, asumir que expira pronto (conservador)
            return true;
        }

        threshold ??= TimeSpan.FromHours(1); // Por defecto, 1 hora antes
        
        if (string.IsNullOrWhiteSpace(_currentToken))
            return true;
        
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(_currentToken);
            var expirationTime = token.ValidTo;
            var warningTime = DateTime.UtcNow.Add(threshold.Value);
            return expirationTime < warningTime;
        }
        catch
        {
            // Si hay error al decodificar, considerar como expirando pronto
            return true;
        }
    }

    /// <summary>
    /// Obtiene el tiempo restante hasta que el token expire.
    /// NO bloquea el hilo principal.
    /// </summary>
    public TimeSpan? GetTokenTimeRemaining()
    {
        // Si no está inicializado, iniciar inicialización en background pero NO esperar
        if (!_isInitialized)
        {
            _ = Task.Run(async () => await EnsureInitializedAsync());
            // Si no está inicializado, no podemos saber el tiempo restante
            return null;
        }

        if (string.IsNullOrWhiteSpace(_currentToken))
            return null;
        
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(_currentToken);
            var expirationTime = token.ValidTo;
            var now = DateTime.UtcNow;
            
            if (expirationTime <= now)
                return TimeSpan.Zero; // Ya expiró
            
            return expirationTime - now;
        }
        catch
        {
            return null;
        }
    }
}

