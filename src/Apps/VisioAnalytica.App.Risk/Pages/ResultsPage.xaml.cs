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
        
        // Obtener el resultado del servicio de navegación (almacenado en memoria)
        var result = _navigationDataService.GetAndClearAnalysisResult();
        
        if (result != null)
        {
            try
            {
                        // Cargar imagen usando la ImageUrl del resultado (viene del servidor)
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
                                
                                // Cargar la imagen usando HttpClient con autenticación
                                await LoadImageSecurelyAsync(fullImageUrl);
                            }
                            catch (UriFormatException ex)
                            {
                                // Si hay error al crear el Uri, registrar pero continuar
                                System.Diagnostics.Debug.WriteLine($"Error al crear Uri para imagen: {ex.Message}. ImageUrl: {result.ImageUrl}");
                                // No mostrar la imagen si hay error, pero continuar mostrando los resultados
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error al cargar imagen: {ex.Message}. ImageUrl: {result.ImageUrl}");
                                // No mostrar la imagen si hay error, pero continuar mostrando los resultados
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
        await Shell.Current.GoToAsync("//CapturePage");
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
                System.Diagnostics.Debug.WriteLine("Usuario no autenticado, no se puede cargar la imagen");
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
                AnalysisImage.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar imagen: {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar imagen de forma segura: {ex.Message}");
            // No lanzar la excepción, solo registrar el error
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

