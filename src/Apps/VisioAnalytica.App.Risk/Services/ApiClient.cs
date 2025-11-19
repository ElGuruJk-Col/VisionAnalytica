using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Implementación del cliente HTTP para comunicación con la API backend.
/// </summary>
public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _authToken;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        // Configuración de JSON
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // URL base - Detecta automáticamente según la plataforma
        // En dispositivo físico Android/iOS: usa la IP de tu máquina
        // En emulador Android: usa 10.0.2.2 (apunta al localhost de tu máquina)
        // En Windows/iOS Simulador: usa localhost
        BaseUrl = GetBaseUrl();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(60); // Timeout de 60 segundos
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

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
        // TODO: Cambiar a tu IP si usas dispositivo físico
        // Para encontrar tu IP: ipconfig (Windows) o ifconfig (Linux/Mac)
        return "http://192.168.1.83:5170"; // Cambia esta IP por la de tu máquina
#elif IOS
        // Para iOS:
        // - Simulador: puede usar localhost
        // - Dispositivo físico: usa la IP de tu máquina
        return "http://192.168.1.83:5170"; // Cambia esta IP por la de tu máquina
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
            var fullUrl = $"{BaseUrl}{endpoint}";
            var response = await _httpClient.GetAsync(endpoint);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new ApiException(
                    $"Error HTTP {(int)response.StatusCode} al realizar petición GET a {endpoint}. " +
                    $"URL: {fullUrl}. Respuesta: {errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new ApiException($"Timeout al realizar petición GET a {endpoint}. Verifica que la API esté corriendo en {BaseUrl}", ex);
        }
        catch (HttpRequestException ex)
        {
            var fullUrl = $"{BaseUrl}{endpoint}";
            throw new ApiException(
                $"Error de conexión al realizar petición GET a {endpoint}. " +
                $"URL: {fullUrl}. " +
                $"Verifica que la API esté corriendo y accesible. Error: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new ApiException($"Error al deserializar respuesta de {endpoint}: {ex.Message}", ex);
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest request) where TResponse : class
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var fullUrl = $"{BaseUrl}{endpoint}";
            var response = await _httpClient.PostAsync(endpoint, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new ApiException(
                    $"Error HTTP {(int)response.StatusCode} al realizar petición POST a {endpoint}. " +
                    $"URL: {fullUrl}. Respuesta: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(responseJson, _jsonOptions);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new ApiException($"Timeout al realizar petición POST a {endpoint}. Verifica que la API esté corriendo en {BaseUrl}", ex);
        }
        catch (HttpRequestException ex)
        {
            var fullUrl = $"{BaseUrl}{endpoint}";
            throw new ApiException(
                $"Error de conexión al realizar petición POST a {endpoint}. " +
                $"URL: {fullUrl}. " +
                $"Verifica que la API esté corriendo y accesible. Error: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new ApiException($"Error al deserializar respuesta de {endpoint}: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Excepción personalizada para errores de API.
/// </summary>
public class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
    public ApiException(string message, Exception innerException) : base(message, innerException) { }
}

