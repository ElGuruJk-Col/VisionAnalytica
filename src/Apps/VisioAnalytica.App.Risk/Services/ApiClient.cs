using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VisioAnalytica.Core.Models.Dtos;

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
        
        // Configuración de JSON con opciones modernas
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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
                var friendlyMessage = await ExtractFriendlyErrorMessageAsync(response);
                throw new ApiException(friendlyMessage, (int)response.StatusCode);
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
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
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var fullUrl = $"{BaseUrl}{endpoint}";
            var response = await _httpClient.PostAsync(endpoint, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var friendlyMessage = await ExtractFriendlyErrorMessageAsync(response);
                throw new ApiException(friendlyMessage, (int)response.StatusCode);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(responseJson, _jsonOptions);
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
    
    /// <summary>
    /// Extrae un mensaje de error amigable de la respuesta HTTP.
    /// Intenta parsear JSON con mensaje, si no, devuelve un mensaje genérico según el código de estado.
    /// </summary>
    private async Task<string> ExtractFriendlyErrorMessageAsync(HttpResponseMessage response)
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
        var endpoint = $"/api/AffiliatedCompany/my-companies?includeInactive={includeInactive}";
        var result = await GetAsync<IList<AffiliatedCompanyDto>>(endpoint);
        return result ?? []; // Collection expression
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

