using System.Collections.ObjectModel;
using System.Text.Json;
using VisioAnalytica.App.Risk.Models;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk.Pages;

public partial class ResultsPage : ContentPage
{
    private readonly IApiClient _apiClient;
    private readonly INavigationDataService _navigationDataService;
    private readonly IAuthService _authService;
    public ObservableCollection<FindingViewModel> Findings { get; } = new();
    
    // Caché local para mantener los resultados cuando se navega a otras páginas
    private AnalysisResult? _cachedResult;
    private byte[]? _cachedImageBytes;
    private ImageSource? _cachedImageSource;

    public ResultsPage(IApiClient apiClient, INavigationDataService navigationDataService, IAuthService authService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _navigationDataService = navigationDataService;
        _authService = authService;
        FindingsCollection.ItemsSource = Findings;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadResults();
    }

    private async void LoadResults()
    {
        Findings.Clear();
        
        // Primero verificar si hay resultados en caché local (de esta instancia de la página)
        AnalysisResult? result = _cachedResult;
        byte[]? capturedImageBytes = _cachedImageBytes;
        Guid? affiliatedCompanyId = null;
        
        // Si no hay resultados en caché local, obtenerlos del servicio de navegación
        // El servicio es Singleton, por lo que los datos persisten entre navegaciones
        if (result == null)
        {
            // Usar GetAnalysisResult() en lugar de GetAndClearAnalysisResult() para mantener los datos
            result = _navigationDataService.GetAnalysisResult();
            capturedImageBytes = _navigationDataService.GetCapturedImageBytes();
            affiliatedCompanyId = _navigationDataService.GetAffiliatedCompanyId();
            
            // Si se obtuvieron resultados del servicio, guardarlos en caché local también
            if (result != null)
            {
                _cachedResult = result;
                _cachedImageBytes = capturedImageBytes;
                _cachedImageSource = null; // Resetear la imagen en caché para recargarla
            }
        }
        else
        {
            // Si hay caché local, también obtener el AffiliatedCompanyId del servicio
            affiliatedCompanyId = _navigationDataService.GetAffiliatedCompanyId();
        }
        
        if (result != null)
        {
            try
            {
                // Primero, intentar mostrar la imagen desde caché si está disponible
                if (_cachedImageSource != null)
                {
                    AnalysisImage.Source = _cachedImageSource;
                    System.Diagnostics.Debug.WriteLine("Imagen mostrada desde caché");
                }
                // Si no hay imagen en caché, intentar mostrar desde bytes locales
                else if (capturedImageBytes != null && capturedImageBytes.Length > 0)
                {
                    try
                    {
                        var imageSource = ImageSource.FromStream(() => new MemoryStream(capturedImageBytes));
                        AnalysisImage.Source = imageSource;
                        _cachedImageSource = imageSource; // Guardar en caché para futuras navegaciones
                        System.Diagnostics.Debug.WriteLine("Imagen mostrada desde bytes locales y guardada en caché");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al mostrar imagen local: {ex.Message}");
                    }
                }
                
                // Luego, intentar cargar la imagen desde el servidor (para tener la versión final guardada)
                // La ImageUrl ahora apunta al endpoint seguro del FileController
                if (!string.IsNullOrWhiteSpace(result.ImageUrl))
                {
                    try
                    {
                        // Construir la URL completa usando la base URL de la API
                        // La ImageUrl ya viene en formato /api/v1/file/images/{orgId}/{filename}
                        // o en formato /uploads/{orgId}/{filename} que debemos convertir
                        var imageUrl = result.ImageUrl;
                        
                        // Si la URL viene en formato /uploads/{orgId}/{filename}, convertirla al endpoint seguro
                        if (imageUrl.StartsWith("/uploads/"))
                        {
                            // Extraer orgId y filename de /uploads/{orgId}/{filename}
                            var parts = imageUrl.TrimStart('/').Split('/', 3);
                            if (parts.Length >= 3 && Guid.TryParse(parts[1], out _))
                            {
                                var orgId = parts[1];
                                var filename = parts[2];
                                imageUrl = $"/api/v1/file/images/{orgId}/{filename}";
                            }
                        }
                        
                        var fullImageUrl = $"{_apiClient.BaseUrl}{imageUrl}";
                        
                        // Agregar AffiliatedCompanyId como query parameter si está disponible
                        if (affiliatedCompanyId.HasValue)
                        {
                            fullImageUrl += $"?affiliatedCompanyId={affiliatedCompanyId.Value}";
                        }
                        
                        // Cargar la imagen usando HttpClient con autenticación
                        // Esto reemplazará la imagen local si tiene éxito
                        await LoadImageSecurelyAsync(fullImageUrl);
                        
                        // Guardar la imagen cargada en caché si se cargó exitosamente
                        if (AnalysisImage.Source != null && _cachedImageSource == null)
                        {
                            _cachedImageSource = AnalysisImage.Source;
                        }
                    }
                    catch (UriFormatException ex)
                    {
                        // Si hay error al crear el Uri, registrar pero continuar
                        System.Diagnostics.Debug.WriteLine($"Error al crear Uri para imagen: {ex.Message}. ImageUrl: {result.ImageUrl}");
                        // La imagen local ya debería estar mostrándose
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al cargar imagen desde servidor: {ex.Message}. ImageUrl: {result.ImageUrl}");
                        // La imagen local ya debería estar mostrándose
                    }
                }

                if (result.Hallazgos != null && result.Hallazgos.Count > 0)
                {
                    foreach (var hallazgo in result.Hallazgos)
                    {
                        Findings.Add(new FindingViewModel
                        {
                            Descripcion = hallazgo.Descripcion,
                            NivelRiesgo = hallazgo.NivelRiesgo,
                            AccionCorrectiva = hallazgo.AccionCorrectiva,
                            AccionPreventiva = hallazgo.AccionPreventiva
                        });
                    }
                    NoFindingsLabel.IsVisible = false;
                }
                else
                {
                    NoFindingsLabel.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                // Error al procesar resultados
                NoFindingsLabel.Text = $"Error al cargar resultados: {ex.Message}";
                NoFindingsLabel.TextColor = Colors.Red;
                NoFindingsLabel.IsVisible = true;
            }
        }
        else
        {
            // No hay datos disponibles (puede ser que se accedió directamente a la página)
            NoFindingsLabel.Text = "No se encontraron resultados de análisis. Por favor, realiza un análisis primero.";
            NoFindingsLabel.TextColor = Colors.Red;
            NoFindingsLabel.IsVisible = true;
        }
    }

    private async void OnNewAnalysisClicked(object? sender, EventArgs e)
    {
        // Limpiar caché local y del servicio cuando se inicia un nuevo análisis
        ClearCache();
        _navigationDataService.Clear(); // Limpiar también el servicio Singleton
        await Shell.Current.GoToAsync("//CapturePage");
    }
    
    /// <summary>
    /// Limpia la caché de resultados. Se llama cuando se inicia un nuevo análisis.
    /// </summary>
    private void ClearCache()
    {
        _cachedResult = null;
        _cachedImageBytes = null;
        _cachedImageSource = null;
        Findings.Clear();
        AnalysisImage.Source = null;
        System.Diagnostics.Debug.WriteLine("Caché de resultados limpiada");
    }

    private async void OnHistoryClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//HistoryPage");
    }

    /// <summary>
    /// Carga una imagen de forma segura usando el endpoint protegido del FileController.
    /// Incluye el token de autenticación en la petición.
    /// </summary>
    private async Task LoadImageSecurelyAsync(string imageUrl)
    {
        try
        {
            // Verificar que el usuario esté autenticado
            if (!_authService.IsAuthenticated || string.IsNullOrWhiteSpace(_authService.CurrentToken))
            {
                System.Diagnostics.Debug.WriteLine("Usuario no autenticado, no se puede cargar la imagen desde el servidor");
                return;
            }

            // Usar HttpClient para descargar la imagen con autenticación
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.CurrentToken);
            
            // Descargar la imagen como bytes
            var response = await httpClient.GetAsync(imageUrl);
            if (response.IsSuccessStatusCode)
            {
                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                var imageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                AnalysisImage.Source = imageSource;
                _cachedImageSource = imageSource; // Guardar en caché para futuras navegaciones
                System.Diagnostics.Debug.WriteLine($"Imagen cargada exitosamente desde el servidor: {imageUrl}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Error al cargar imagen desde servidor: {response.StatusCode} - {response.ReasonPhrase}. URL: {imageUrl}. Error: {errorContent}");
                // No cambiar la imagen si ya hay una mostrándose (la local)
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar imagen de forma segura: {ex.Message}. URL: {imageUrl}. StackTrace: {ex.StackTrace}");
            // No lanzar la excepción, solo registrar el error
            // La imagen local debería seguir mostrándose si está disponible
        }
    }
}

/// <summary>
/// ViewModel para mostrar hallazgos en la UI.
/// </summary>
public class FindingViewModel
{
    public string Descripcion { get; set; } = string.Empty;
    public string NivelRiesgo { get; set; } = string.Empty;
    public string AccionCorrectiva { get; set; } = string.Empty;
    public string AccionPreventiva { get; set; } = string.Empty;

    public Color RiskColor => NivelRiesgo.ToUpper() switch
    {
        "ALTO" => Colors.Red,
        "MEDIO" => Colors.Orange,
        "BAJO" => Colors.Green,
        _ => Colors.Gray
    };

    public Color RiskBackgroundColor => NivelRiesgo.ToUpper() switch
    {
        "ALTO" => Color.FromRgba(255, 0, 0, 30),
        "MEDIO" => Color.FromRgba(255, 165, 0, 30),
        "BAJO" => Color.FromRgba(0, 255, 0, 30),
        _ => Colors.Transparent
    };
}

