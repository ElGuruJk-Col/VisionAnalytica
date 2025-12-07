using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.App.Risk.Models;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Implementación del cliente HTTP para comunicación con la API backend.
/// </summary>
public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Lazy<IAuthService?>? _authServiceLazy;
    private readonly INavigationService? _navigation_service;
    private string? _authToken;

    public ApiClient(HttpClient httpClient, IAuthService? authService = null, INavigationService? navigationService = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authServiceLazy = authService != null ? new Lazy<IAuthService?>(() => authService) : null;
        _navigation_service = navigationService;

        // Configuración de JSON con opciones modernas
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        BaseUrl = GetBaseUrl();
        System.Diagnostics.Debug.WriteLine($"🔧 ApiClient inicializado con BaseUrl: {BaseUrl}");
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Constructor alternativo que acepta un factory para resolver IAuthService de forma diferida.
    /// Esto rompe la dependencia circular durante la inicialización.
    /// </summary>
    public ApiClient(HttpClient httpClient, Func<IAuthService?>? authServiceFactory, INavigationService? navigationService = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authServiceLazy = authServiceFactory != null ? new Lazy<IAuthService?>(authServiceFactory) : null;
        _navigation_service = navigationService;
        
        // Configuración de JSON con opciones modernas
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // URL base - Detecta automáticamente según la plataforma
        BaseUrl = GetBaseUrl();
        System.Diagnostics.Debug.WriteLine($"🔧 ApiClient inicializado con BaseUrl: {BaseUrl}");
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(60); // Timeout de 60 segundos
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Obtiene IAuthService de forma lazy para evitar dependencia circular durante la inicialización.
    /// </summary>
    private IAuthService? GetAuthService() => _authServiceLazy?.Value;

    public string BaseUrl { get; }

    /// <summary>
    /// Obtiene la URL base de la API según la plataforma.
    /// </summary>
    private static string GetBaseUrl()
    {
#if ANDROID
        // Para Android: 
        // - Emulador: usa 10.0.2.2 que apunta al localhost de tu máquina
        // - Dispositivo físico: usa la IP de tu máquina (ej: 192.168.1.83)
        // 
        // INSTRUCCIONES:
        // 1. Si usas EMULADOR: descomenta la línea de 10.0.2.2 y comenta la de IP física
        // 2. Si usas DISPOSITIVO FÍSICO: 
        //    - Ejecuta: ipconfig (Windows) o ifconfig (Linux/Mac)
        //    - Busca tu "Dirección IPv4" (ej: 192.168.1.83)
        //    - Reemplaza la IP en la línea correspondiente
        
        // OPCIÓN 1: Para EMULADOR Android (descomenta esta línea y comenta la siguiente)
        // return "http://10.0.2.2:5170";
        
        // OPCIÓN 2: Para DISPOSITIVO FÍSICO Android (cambia la IP por la de tu máquina)
        return "http://192.168.1.83:5170"; // ⚠️ CAMBIA ESTA IP por la de tu máquina
        
        // Para encontrar tu IP en Windows:
        //   ipconfig | findstr /i "IPv4"
        // En Linux/Mac:
        //   ifconfig | grep "inet " | grep -v 127.0.0.1
#elif IOS
        // Para iOS:
        // - Simulador: puede usar localhost
        // - Dispositivo físico: usa la IP de tu máquina
        // 
        // INSTRUCCIONES:
        // 1. Si usas SIMULADOR: descomenta la línea de localhost y comenta la de IP física
        // 2. Si usas DISPOSITIVO FÍSICO: cambia la IP por la de tu máquina
        
        // OPCIÓN 1: Para SIMULADOR iOS (descomenta esta línea y comenta la siguiente)
        // return "http://localhost:5170";
        
        // OPCIÓN 2: Para DISPOSITIVO FÍSICO iOS (cambia la IP por la de tu máquina)
        return "http://192.168.1.83:5170"; // ⚠️ CAMBIA ESTA IP por la de tu máquina
#else
        // Windows, Mac Catalyst, etc: usa localhost
        return "http://localhost:5170";
#endif
    }

    public void SetAuthToken(string? token)
    {
        _authToken = token;
        
        if (string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<T?> GetAsync<T>(string endpoint) where T : class
    {
        try
        {
            // Verificar proactivamente si el token expiró antes de hacer la request
            var authService = GetAuthService();
            if (authService != null && authService.IsTokenExpired())
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Token expirado detectado antes de la request. Cerrando sesión...");
                await HandleUnauthorizedAsync();
                throw new ApiException("Tu sesión ha expirado. Por favor, inicia sesión nuevamente.", 401);
            }
            
            var fullUrl = $"{BaseUrl}{endpoint}";
            var response = await _httpClient.GetAsync(endpoint);
            
            if (!response.IsSuccessStatusCode)
            {
                // Manejar 401 (Unauthorized) - Token expirado o inválido
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Intentar renovar con refresh token antes de cerrar sesión
                    if (authService != null)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ 401 recibido. Intentando renovar token...");
                        var refreshed = await authService.RefreshTokenAsync();
                        if (refreshed)
                        {
                            // Si se renovó, reintentar la request con el nuevo token
                            System.Diagnostics.Debug.WriteLine("✅ Token renovado. Reintentando request...");
                            var retryResponse = await _httpClient.GetAsync(endpoint);
                            if (retryResponse.IsSuccessStatusCode)
                            {
                                var retryJson = await retryResponse.Content.ReadAsStringAsync();
                                System.Diagnostics.Debug.WriteLine($"📥 Respuesta JSON recibida (primeros 500 chars): {(retryJson.Length > 500 ? retryJson[..500] : retryJson)}");
                                if (string.IsNullOrWhiteSpace(retryJson))
                                {
                                    return null;
                                }
                                return JsonSerializer.Deserialize<T>(retryJson, _jsonOptions);
                            }
                        }
                    }

                    // Si no se pudo renovar o no hay authService, cerrar sesión
                    await HandleUnauthorizedAsync();
                    throw new ApiException("Tu sesión ha expirado. Por favor, inicia sesión nuevamente.", (int)response.StatusCode);
                }

                var friendlyMessage = await ExtractFriendlyErrorMessageAsync(response);
                throw new ApiException(friendlyMessage, (int)response.StatusCode);
            }

            var json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"📥 Respuesta JSON recibida (primeros 500 chars): {(json.Length > 500 ? json[..500] : json)}");
            
            if (string.IsNullOrWhiteSpace(json))
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Respuesta JSON vacía");
                return null;
            }
            
            var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            System.Diagnostics.Debug.WriteLine($"✅ Deserialización exitosa: {result is not null}");
            return result;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new ApiException($"La solicitud tardó demasiado. Verifica tu conexión a internet y que la API esté disponible.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(
                $"No se pudo conectar con el servidor. Verifica tu conexión a internet y que la API esté disponible.", ex);
        }
        catch (JsonException ex)
        {
            throw new ApiException($"Error al procesar la respuesta del servidor.", ex);
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest request) where TResponse : class
    {
        try
        {
            // Lista de endpoints públicos que no requieren verificación de token
            var publicEndpoints = new[]
            {
                "/api/auth/login",
                "/api/auth/register",
                "/api/auth/forgot-password",
                "/api/auth/reset-password",
                "/api/auth/refresh"
            };
            
            var isPublicEndpoint = publicEndpoints.Any(e => endpoint.Contains(e, StringComparison.OrdinalIgnoreCase));
            
            // Solo verificar token si NO es un endpoint público
            if (!isPublicEndpoint)
            {
                var authService = GetAuthService();
                if (authService != null)
                {
                    if (authService.IsTokenExpired())
                    {
                        // Intentar renovar con refresh token
                        System.Diagnostics.Debug.WriteLine("⚠️ Token expirado detectado en POST. Intentando renovar con refresh token...");
                        var refreshed = await authService.RefreshTokenAsync();
                        if (!refreshed)
                        {
                            System.Diagnostics.Debug.WriteLine("❌ No se pudo renovar el token. Cerrando sesión...");
                            await HandleUnauthorizedAsync();
                            throw new ApiException("Tu sesión ha expirado. Por favor, inicia sesión nuevamente.", 401);
                        }
                        System.Diagnostics.Debug.WriteLine("✅ Token renovado exitosamente en POST.");
                    }
                    else if (authService.IsTokenExpiringSoon(TimeSpan.FromMinutes(15)))
                    {
                        // Renovar proactivamente si el token expirará en menos de 15 minutos
                        System.Diagnostics.Debug.WriteLine("⚠️ Token expirará pronto en POST. Renovando proactivamente...");
                        _ = Task.Run(async () => await authService.RefreshTokenAsync());
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"🔓 Endpoint público detectado: {endpoint}. Saltando verificación de token.");
            }
            
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var fullUrl = $"{BaseUrl}{endpoint}";
            System.Diagnostics.Debug.WriteLine($"📤 POST a: {fullUrl}");
            System.Diagnostics.Debug.WriteLine($"📦 Payload (primeros 200 chars): {(json.Length > 200 ? string.Concat(json.AsSpan(0, 200), "...") : json)}");
            System.Diagnostics.Debug.WriteLine($"🔗 HttpClient BaseAddress: {_httpClient.BaseAddress}");
            System.Diagnostics.Debug.WriteLine($"⏱️ HttpClient Timeout: {_httpClient.Timeout.TotalSeconds} segundos");
            
            var response = await _httpClient.PostAsync(endpoint, content);
        
            System.Diagnostics.Debug.WriteLine($"📥 Respuesta recibida: StatusCode={response.StatusCode}, IsSuccess={response.IsSuccessStatusCode}");
        
            if (!response.IsSuccessStatusCode)
            {
                // Manejar 401 (Unauthorized) - Token expirado o inválido
                // NO manejar 401 para endpoints públicos (login, register, etc.)
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && !isPublicEndpoint)
                {
                    // Intentar renovar con refresh token antes de cerrar sesión
                    var authService = GetAuthService();
                    if (authService != null)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ 401 recibido en POST. Intentando renovar token...");
                        var refreshed = await authService.RefreshTokenAsync();
                        if (refreshed)
                        {
                            // Si se renovó, reintentar la request con el nuevo token
                            System.Diagnostics.Debug.WriteLine("✅ Token renovado. Reintentando POST...");
                            var retryResponse = await _httpClient.PostAsync(endpoint, content);
                            if (retryResponse.IsSuccessStatusCode)
                            {
                                var retryResponseJson = await retryResponse.Content.ReadAsStringAsync();
                                System.Diagnostics.Debug.WriteLine($"✅ POST exitoso después de renovar token. Respuesta (primeros 200 chars): {(retryResponseJson.Length > 200 ? string.Concat(retryResponseJson.AsSpan(0, 200), "...") : retryResponseJson)}");
                                return JsonSerializer.Deserialize<TResponse>(retryResponseJson, _jsonOptions);
                            }
                        }
                    }
                    
                    // Si no se pudo renovar o no hay authService, cerrar sesión
                    await HandleUnauthorizedAsync();
                    System.Diagnostics.Debug.WriteLine($"❌ Token expirado. Sesión cerrada automáticamente.");
                    throw new ApiException("Tu sesión ha expirado. Por favor, inicia sesión nuevamente.", (int)response.StatusCode);
                }
                
                var friendlyMessage = await ExtractFriendlyErrorMessageAsync(response);
                System.Diagnostics.Debug.WriteLine($"❌ Error en POST: {friendlyMessage} (StatusCode: {response.StatusCode})");
                throw new ApiException(friendlyMessage, (int)response.StatusCode);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"✅ POST exitoso. Respuesta (primeros 200 chars): {(responseJson.Length > 200 ? string.Concat(responseJson.AsSpan(0, 200), "...") : responseJson)}");
            
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Respuesta JSON vacía en POST");
                return null;
            }
            
            try
            {
                var result = JsonSerializer.Deserialize<TResponse>(responseJson, _jsonOptions);
                System.Diagnostics.Debug.WriteLine($"✅ Deserialización POST exitosa: {result is not null}");
                if (result == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Deserialización retornó null. JSON completo: {responseJson}");
                }
                return result;
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error de deserialización JSON: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   JSON recibido: {responseJson}");
                throw new ApiException($"Error al procesar la respuesta del servidor: {ex.Message}", ex);
            }
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            var fullUrl = $"{BaseUrl}{endpoint}";
            System.Diagnostics.Debug.WriteLine($"⏱️ Timeout al conectar a: {fullUrl}");
            System.Diagnostics.Debug.WriteLine($"   HttpClient BaseAddress: {_httpClient.BaseAddress}");
            System.Diagnostics.Debug.WriteLine($"   Endpoint: {endpoint}");
            System.Diagnostics.Debug.WriteLine($"   Timeout configurado: {_httpClient.Timeout.TotalSeconds} segundos");
            System.Diagnostics.Debug.WriteLine($"   InnerException: {ex.InnerException?.Message}");
            throw new ApiException($"La solicitud tardó demasiado. Verifica que:\n1. El dispositivo esté en la misma red que el servidor (192.168.1.83)\n2. La API esté ejecutándose en el puerto 5170\n3. No haya firewall bloqueando la conexión", ex);
        }
        catch (HttpRequestException ex)
        {
            var fullUrl = $"{BaseUrl}{endpoint}";
            System.Diagnostics.Debug.WriteLine($"❌ HttpRequestException al conectar a {fullUrl}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"   HttpClient BaseAddress: {_httpClient.BaseAddress}");
            System.Diagnostics.Debug.WriteLine($"   InnerException: {ex.InnerException?.Message}");
            System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            throw new ApiException(
                $"No se pudo conectar con el servidor en {BaseUrl}.\n\nVerifica que:\n1. El dispositivo esté en la misma red WiFi que el servidor\n2. La IP del servidor sea correcta (192.168.1.83)\n3. La API esté ejecutándose\n4. No haya firewall bloqueando la conexión", ex);
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ JsonException en POST: {ex.Message}");
            throw new ApiException($"Error al procesar la respuesta del servidor.", ex);
        }
    }
    
    /// <summary>
    /// Extrae un mensaje de error amigable de la respuesta HTTP.
    /// Intenta parsear JSON con mensaje, si no, devuelve un mensaje genérico según el código de estado.
    /// </summary>
    private static async Task<string> ExtractFriendlyErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            
            // Intentar parsear como JSON para extraer el mensaje
            if (!string.IsNullOrWhiteSpace(errorContent))
            {
                try
                {
                    using var doc = JsonDocument.Parse(errorContent);
                    if (doc.RootElement.TryGetProperty("message", out var messageElement))
                    {
                        var message = messageElement.GetString();
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            return message;
                        }
                    }
                    // Si el JSON es un string directo
                    if (doc.RootElement.ValueKind == JsonValueKind.String)
                    {
                        return doc.RootElement.GetString() ?? GetDefaultErrorMessage(response.StatusCode);
                    }
                }
                catch
                {
                    // Si no es JSON válido, usar el contenido como está si es corto y legible
                    if (errorContent.Length < 200 && !errorContent.Contains("Error HTTP"))
                    {
                        return errorContent;
                    }
                }
            }
        }
        catch
        {
            // Si hay error al leer el contenido, usar mensaje por defecto
        }
        
        // Mensaje por defecto según el código de estado
        return GetDefaultErrorMessage(response.StatusCode);
    }
    
    /// <summary>
    /// Obtiene un mensaje de error amigable según el código de estado HTTP.
    /// </summary>
    private static string GetDefaultErrorMessage(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Email o contraseña inválidos.",
            System.Net.HttpStatusCode.Forbidden => "No tienes permiso para realizar esta acción.",
            System.Net.HttpStatusCode.NotFound => "El recurso solicitado no fue encontrado.",
            System.Net.HttpStatusCode.BadRequest => "La solicitud no es válida. Verifica los datos ingresados.",
            System.Net.HttpStatusCode.InternalServerError => "Error interno del servidor. Por favor, intenta más tarde.",
            System.Net.HttpStatusCode.ServiceUnavailable => "El servicio no está disponible. Por favor, intenta más tarde.",
            _ => "Ocurrió un error al procesar tu solicitud. Por favor, intenta nuevamente."
        };
    }

    /// <summary>
    /// Obtiene las empresas cliente asignadas al inspector autenticado.
    /// </summary>
    public async Task<IList<AffiliatedCompanyDto>> GetMyCompaniesAsync(bool includeInactive = false)
    {
        try
        {
            var endpoint = $"/api/AffiliatedCompany/my-companies?includeInactive={includeInactive}";
            System.Diagnostics.Debug.WriteLine($"📡 Llamando a endpoint: {BaseUrl}{endpoint}");
            
            var result = await GetAsync<IList<AffiliatedCompanyDto>>(endpoint);
            
            if (result is null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ GetAsync retornó null");
                return new List<AffiliatedCompanyDto>();
            }
            
            System.Diagnostics.Debug.WriteLine($"✅ GetAsync retornó {result.Count} empresas");
            foreach (var company in result)
            {
                System.Diagnostics.Debug.WriteLine($"   - {company?.Name ?? "NULL"} (ID: {company?.Id})");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error en GetMyCompaniesAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Obtiene todas las empresas afiliadas de la organización del usuario autenticado (para SuperAdmin/Admin).
    /// </summary>
    public async Task<IList<AffiliatedCompanyDto>> GetAllCompaniesAsync(bool includeInactive = false)
    {
        try
        {
            var endpoint = $"/api/AffiliatedCompany?includeInactive={includeInactive}";
            System.Diagnostics.Debug.WriteLine($"📡 Llamando a endpoint: {BaseUrl}{endpoint}");
            
            var result = await GetAsync<IList<AffiliatedCompanyDto>>(endpoint);
            
            if (result is null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ GetAsync retornó null");
                return new List<AffiliatedCompanyDto>();
            }
            
            System.Diagnostics.Debug.WriteLine($"✅ GetAsync retornó {result.Count} empresas");
            foreach (var company in result)
            {
                System.Diagnostics.Debug.WriteLine($"   - {company?.Name ?? "NULL"} (ID: {company?.Id})");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error en GetAllCompaniesAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Notifica al supervisor que el inspector no tiene empresas asignadas.
    /// </summary>
    public async Task<bool> NotifyInspectorWithoutCompaniesAsync()
    {
        try
        {
            var endpoint = "/api/UserManagement/notify-inspector-without-companies";
            var response = await PostAsync<object, object>(endpoint, new { });
            return response != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Crea una nueva inspección con múltiples fotos.
    /// </summary>
    public async Task<InspectionDto?> CreateInspectionAsync(CreateInspectionDto request)
    {
        var requestId = Guid.NewGuid();
        System.Diagnostics.Debug.WriteLine($"🌐 [ApiClient] CreateInspectionAsync llamado - RequestId: {requestId}, Thread: {Thread.CurrentThread.ManagedThreadId}, Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
        System.Diagnostics.Debug.WriteLine($"🌐 [ApiClient] EmpresaId: {request.AffiliatedCompanyId}, Fotos: {request.Photos?.Count ?? 0}");
        
        var endpoint = "/api/v1/Inspection/create";
        var result = await PostAsync<CreateInspectionDto, InspectionDto>(endpoint, request);
        
        System.Diagnostics.Debug.WriteLine($"🌐 [ApiClient] CreateInspectionAsync completado - RequestId: {requestId}, InspectionId: {result?.Id}");
        return result;
    }

    /// <summary>
    /// Obtiene las inspecciones del usuario autenticado.
    /// </summary>
    public async Task<List<InspectionDto>> GetMyInspectionsAsync(Guid? affiliatedCompanyId = null)
    {
        var endpoint = "/api/v1/Inspection/my-inspections";
        if (affiliatedCompanyId.HasValue)
        {
            endpoint += $"?affiliatedCompanyId={affiliatedCompanyId.Value}";
        }
        var result = await GetAsync<List<InspectionDto>>(endpoint);
        return result ?? new List<InspectionDto>();
    }
    
    /// <summary>
    /// Obtiene las inspecciones del usuario autenticado con paginación.
    /// </summary>
    public async Task<PagedResult<InspectionDto>> GetMyInspectionsPagedAsync(int pageNumber = 1, int pageSize = 20, Guid? affiliatedCompanyId = null)
    {
        var endpoint = $"/api/v1/Inspection/my-inspections/paged?pageNumber={pageNumber}&pageSize={pageSize}";
        if (affiliatedCompanyId.HasValue)
        {
            endpoint += $"&affiliatedCompanyId={affiliatedCompanyId.Value}";
        }
        var result = await GetAsync<PagedResult<InspectionDto>>(endpoint);
        return result ?? new PagedResult<InspectionDto>(new List<InspectionDto>(), pageNumber, pageSize, 0, 0, false, false);
    }

    /// <summary>
    /// Obtiene los detalles de una inspección específica.
    /// </summary>
    public async Task<InspectionDto?> GetInspectionByIdAsync(Guid inspectionId)
    {
        var endpoint = $"/api/v1/Inspection/{inspectionId}";
        return await GetAsync<InspectionDto>(endpoint);
    }

    /// <summary>
    /// Inicia el análisis en segundo plano de las fotos seleccionadas.
    /// </summary>
    public async Task<string> StartAnalysisAsync(AnalyzeInspectionDto request)
    {
        var endpoint = $"/api/v1/Inspection/{request.InspectionId}/analyze";
        var response = await PostAsync<AnalyzeInspectionDto, Dictionary<string, object>>(endpoint, request);
        
        if (response != null && response.TryGetValue("jobId", out var jobIdObj) && jobIdObj is JsonElement jobIdElement)
        {
            return jobIdElement.GetString() ?? string.Empty;
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Obtiene el estado del análisis de una inspección.
    /// </summary>
    public async Task<InspectionAnalysisStatusDto> GetAnalysisStatusAsync(Guid inspectionId)
    {
        var endpoint = $"/api/v1/Inspection/{inspectionId}/status";
        var result = await GetAsync<InspectionAnalysisStatusDto>(endpoint);

        return result is null ? throw new ApiException("No se pudo obtener el estado del análisis.") : result;
    }

    /// <summary>
    /// Obtiene los hallazgos de una inspección de análisis (generada por una foto).
    /// </summary>
    public async Task<List<FindingDetailDto>> GetInspectionFindingsAsync(Guid analysisInspectionId)
    {
        var endpoint = $"/api/v1/Inspection/{analysisInspectionId}/findings";
        var result = await GetAsync<List<FindingDetailDto>>(endpoint);
        return result ?? new List<FindingDetailDto>();
    }
    /// <summary>
    /// Obtiene el historial de inspecciones de la organización (para Admin/Supervisor).
    /// </summary>
    public async Task<List<InspectionSummaryDto>> GetOrganizationHistoryAsync()
    {
        var endpoint = "/api/v1/Analysis/history";
        var result = await GetAsync<List<InspectionSummaryDto>>(endpoint);
        return result ?? new List<InspectionSummaryDto>();
    }

    /// <summary>
    /// Renueva el access token usando un refresh token.
    /// </summary>
    public async Task<RefreshTokenResponse?> RefreshTokenAsync(string refreshToken)
    {
        var endpoint = "/api/auth/refresh";
        var request = new RefreshTokenRequest(refreshToken);
        return await PostAsync<RefreshTokenRequest, RefreshTokenResponse>(endpoint, request);
    }

    /// <summary>
    /// Maneja respuestas 401 (Unauthorized) cerrando la sesión automáticamente y redirigiendo al login.
    /// </summary>
    private async Task HandleUnauthorizedAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("🔒 Detectado 401 (Unauthorized). Cerrando sesión automáticamente...");
            
            // Limpiar token del cliente
            SetAuthToken(null);
            
            // Cerrar sesión en AuthService si está disponible
            var authService = GetAuthService();
            if (authService != null)
            {
                await authService.LogoutAsync();
                System.Diagnostics.Debug.WriteLine("✅ Sesión cerrada automáticamente por token expirado.");
            }
            
            // Redirigir al login en el hilo principal
            if (_navigation_service != null)
            {
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        await _navigation_service.NavigateToLoginAsync();
                        System.Diagnostics.Debug.WriteLine("✅ Redirigido al login automáticamente.");
                    }
                    catch (Exception navEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Error al redirigir al login: {navEx.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Error al cerrar sesión automáticamente: {ex.Message}");
        }
    }
}

/// <summary>
/// Excepción personalizada para errores de API.
/// </summary>
public class ApiException : Exception
{
    public int? StatusCode { get; }
    
    public ApiException(string message) : base(message) { }
    
    public ApiException(string message, Exception innerException) : base(message, innerException) { }
    
    public ApiException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
    
    public ApiException(string message, int statusCode, Exception innerException) : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}

