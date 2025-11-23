namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Interfaz para el cliente HTTP que se comunica con la API backend.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Realiza una petición GET a la API.
    /// </summary>
    Task<T?> GetAsync<T>(string endpoint) where T : class;

    /// <summary>
    /// Realiza una petición POST a la API.
    /// </summary>
    Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest request) where TResponse : class;

    /// <summary>
    /// Establece el token JWT para las peticiones autenticadas.
    /// </summary>
    void SetAuthToken(string? token);

    /// <summary>
    /// Obtiene la URL base de la API.
    /// </summary>
    string BaseUrl { get; }

    /// <summary>
    /// Obtiene las empresas cliente asignadas al inspector autenticado.
    /// </summary>
    Task<IList<VisioAnalytica.Core.Models.Dtos.AffiliatedCompanyDto>> GetMyCompaniesAsync(bool includeInactive = false);

    /// <summary>
    /// Notifica al supervisor que el inspector no tiene empresas asignadas.
    /// </summary>
    Task<bool> NotifyInspectorWithoutCompaniesAsync();
}

