using System.Collections.ObjectModel;
using System.Net.Http;
using VisioAnalytica.App.Risk.Services;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Pages;

/// <summary>
/// Página para mostrar los detalles de una inspección, incluyendo fotos y hallazgos.
/// </summary>
[QueryProperty(nameof(InspectionId), "inspectionId")]
public partial class InspectionDetailsPage : ContentPage
{
    private readonly IApiClient _apiClient;
    private readonly IAuthService _authService;
    private readonly ObservableCollection<PhotoFindingViewModel> _photoFindings = [];
    private InspectionDto? _inspection;
    
    private static readonly string[] UploadsSeparator = ["/uploads/"];
    
    // HttpClient compartido para cargar imágenes (evita agotamiento de sockets)
    private static readonly HttpClient _imageHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    
    // Propiedad para recibir el ID desde la navegación
    private string _inspectionId = string.Empty;
    public string InspectionId
    {
        get => _inspectionId;
        set
        {
            _inspectionId = value;
            System.Diagnostics.Debug.WriteLine($"🔍 InspectionId recibido: {value}");
            
            // Cuando se establece el ID, cargar los detalles
            if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out var guid))
            {
                _ = LoadInspectionDetails(guid);
            }
        }
    }

    public InspectionDetailsPage(IApiClient apiClient, IAuthService authService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _authService = authService;
        PhotosCollection.ItemsSource = _photoFindings;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Si ya tenemos la inspección cargada, no recargar
        if (_inspection != null)
        {
            return;
        }
        
        // Si tenemos el ID pero aún no hemos cargado, intentar cargar
        if (!string.IsNullOrEmpty(_inspectionId) && Guid.TryParse(_inspectionId, out var inspectionId))
        {
            await LoadInspectionDetails(inspectionId);
        }
        else
        {
            // Intentar obtener desde la query string como fallback
            try
            {
                var currentState = Shell.Current.CurrentState;
                if (currentState != null)
                {
                    var location = currentState.Location;
                    var fullPath = location.OriginalString ?? location.ToString();
                    
                    System.Diagnostics.Debug.WriteLine($"🔍 Location completo (fallback): {fullPath}");
                    
                    if (fullPath.Contains("inspectionId="))
                    {
                        var startIndex = fullPath.IndexOf("inspectionId=", StringComparison.Ordinal) + "inspectionId=".Length;
                        var endIndex = fullPath.IndexOf('&', startIndex);
                        if (endIndex == -1)
                        {
                            endIndex = fullPath.IndexOf('?', startIndex);
                            if (endIndex == -1)
                            {
                                endIndex = fullPath.Length;
                            }
                        }
                        
                        var idString = fullPath[startIndex..endIndex].Trim();
                        System.Diagnostics.Debug.WriteLine($"🔍 ID extraído (fallback): {idString}");
                        
                        if (Guid.TryParse(idString, out var parsedId))
                        {
                            await LoadInspectionDetails(parsedId);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al obtener inspectionId (fallback): {ex.Message}");
            }
            
            System.Diagnostics.Debug.WriteLine("❌ No se pudo obtener el inspectionId");
            await DisplayAlertAsync("Error", "No se proporcionó ID de inspección.", "OK");
            await GoBackAsync();
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await GoBackAsync();
    }

    private static async Task GoBackAsync()
    {
        // Intentar regresar a la página anterior
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            // Si no hay página anterior, ir al historial
            await Shell.Current.GoToAsync("//InspectionHistoryPage");
        }
    }

    public async Task LoadInspectionDetails(Guid inspectionId)
    {
        _inspectionId = inspectionId.ToString();
        
        try
        {
            SetLoading(true);
            _inspection = await _apiClient.GetInspectionByIdAsync(inspectionId);
            
            if (_inspection != null)
            {
                // Actualizar información de la inspección en el hilo principal
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CompanyNameLabel.Text = _inspection.AffiliatedCompanyName;
                    StatusLabel.Text = $"Estado: {GetStatusDisplay(_inspection.Status)}";
                    DateRangeLabel.Text = $"Fecha: {_inspection.StartedAt:dd/MM/yyyy HH:mm} - {(_inspection.CompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "En proceso")}";
                    _photoFindings.Clear();
                });
                
                // Construir URL base una sola vez
                var baseUrl = _apiClient.BaseUrl.TrimEnd('/');
                
                // Preparar todas las fotos primero (sin cargar imágenes aún)
                var photoTasks = new List<Task<PhotoFindingViewModel>>();
                
                foreach (var photo in _inspection.Photos.OrderBy(p => p.CapturedAt))
                {
                    var photoTask = ProcessPhotoAsync(photo, baseUrl);
                    photoTasks.Add(photoTask);
                }
                
                // Cargar todas las fotos en paralelo (pero limitado para no sobrecargar)
                var photos = await Task.WhenAll(photoTasks);
                
                // Actualizar UI en el hilo principal
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var photoFinding in photos)
                    {
                        _photoFindings.Add(photoFinding);
                    }
                });
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo cargar la inspección.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar detalles: {ex}");
            await DisplayAlertAsync("Error", $"Error al cargar detalles: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private static string GetStatusDisplay(string status)
    {
        return status switch
        {
            "Draft" => "Borrador",
            "PhotosCaptured" => "Fotos Capturadas",
            "Analyzing" => "Analizando",
            "Completed" => "Completada",
            "Failed" => "Fallida",
            _ => status
        };
    }

    private void SetLoading(bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsRunning = isLoading;
            LoadingIndicator.IsVisible = isLoading;
        });
    }

    /// <summary>
    /// Procesa una foto individual: carga hallazgos e imagen.
    /// </summary>
    private async Task<PhotoFindingViewModel> ProcessPhotoAsync(PhotoInfoDto photo, string baseUrl)
    {
        try
        {
            List<FindingDetailDto> findings = [];
            
            // Si la foto está analizada, obtener sus hallazgos
            if (photo.IsAnalyzed && photo.AnalysisId.HasValue)
            {
                try
                {
                    findings = await _apiClient.GetInspectionFindingsAsync(photo.AnalysisId.Value);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al cargar hallazgos para foto {photo.Id}: {ex.Message}");
                    findings = [];
                }
            }
            
            // Construir URL completa de la imagen
            var imageUrl = photo.ImageUrl.StartsWith("http") 
                ? photo.ImageUrl 
                : $"{baseUrl}{photo.ImageUrl}";
            
            // Convertir /uploads/{orgId}/{filename} a /api/v1/file/images/{orgId}/{filename} si es necesario
            if (imageUrl.Contains("/uploads/", StringComparison.Ordinal))
            {
                var parts = imageUrl.Split(UploadsSeparator, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    var orgAndFile = parts[1];
                    imageUrl = $"{baseUrl}/api/v1/file/images/{orgAndFile}";
                }
            }
            
            // Cargar la imagen de forma segura (en background thread)
            var imageSource = await LoadImageSecurelyAsync(imageUrl);
            
            return new PhotoFindingViewModel
            {
                PhotoId = photo.Id,
                ImageUrl = imageUrl,
                ImageSource = imageSource,
                CapturedAt = photo.CapturedAt,
                Description = photo.Description,
                IsAnalyzed = photo.IsAnalyzed,
                Findings = [.. findings]
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al procesar foto {photo.Id}: {ex.Message}");
            
            // Construir URL en caso de error también
            var imageUrl = photo.ImageUrl.StartsWith("http") 
                ? photo.ImageUrl 
                : $"{baseUrl}{photo.ImageUrl}";
            
            if (imageUrl.Contains("/uploads/", StringComparison.Ordinal))
            {
                var parts = imageUrl.Split(UploadsSeparator, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    var orgAndFile = parts[1];
                    imageUrl = $"{baseUrl}/api/v1/file/images/{orgAndFile}";
                }
            }
            
            return new PhotoFindingViewModel
            {
                PhotoId = photo.Id,
                ImageUrl = imageUrl,
                ImageSource = null,
                CapturedAt = photo.CapturedAt,
                Description = photo.Description,
                IsAnalyzed = photo.IsAnalyzed,
                Findings = []
            };
        }
    }

    /// <summary>
    /// Carga una imagen de forma segura usando el endpoint protegido del FileController.
    /// Incluye el token de autenticación en la petición.
    /// Usa un HttpClient compartido para evitar agotamiento de sockets.
    /// </summary>
    private async Task<ImageSource?> LoadImageSecurelyAsync(string imageUrl)
    {
        try
        {
            // Verificar que el usuario esté autenticado
            if (!_authService.IsAuthenticated || string.IsNullOrWhiteSpace(_authService.CurrentToken))
            {
                System.Diagnostics.Debug.WriteLine("Usuario no autenticado, no se puede cargar la imagen desde el servidor");
                return null;
            }

            // Usar HttpClient compartido para descargar la imagen con autenticación
            // Limpiar headers anteriores y establecer el nuevo token
            _imageHttpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.CurrentToken);
            
            // Descargar la imagen como bytes
            var response = await _imageHttpClient.GetAsync(imageUrl);
            if (response.IsSuccessStatusCode)
            {
                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                
                // Crear ImageSource desde bytes (más eficiente que desde stream)
                var imageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                
                System.Diagnostics.Debug.WriteLine($"✅ Imagen cargada exitosamente desde el servidor: {imageUrl}");
                return imageSource;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al cargar imagen desde servidor: {response.StatusCode} - {response.ReasonPhrase}. URL: {imageUrl}");
                return null;
            }
        }
        catch (TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"⏱️ Timeout al cargar imagen: {imageUrl}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ HttpRequestException al cargar imagen: {ex.Message}. URL: {imageUrl}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error al cargar imagen de forma segura: {ex.Message}. URL: {imageUrl}");
            return null;
        }
    }
}

/// <summary>
/// ViewModel para una foto con sus hallazgos.
/// </summary>
public class PhotoFindingViewModel
{
    public Guid PhotoId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public ImageSource? ImageSource { get; set; }
    public DateTime CapturedAt { get; set; }
    public string? Description { get; set; }
    public bool IsAnalyzed { get; set; }
    public List<FindingDetailDto> Findings { get; set; } = [];
}

