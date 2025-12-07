using System.Collections.ObjectModel;
using System.Net.Http;
using VisioAnalytica.App.Risk.Services;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Pages;

/// <summary>
/// Página para mostrar los detalles de una inspección, incluyendo fotos y hallazgos.
/// </summary>
public partial class InspectionDetailsPage : ContentPage
{
    private readonly IApiClient _apiClient;
    private readonly IAuthService _authService;
    private readonly ObservableCollection<PhotoFindingViewModel> _photoFindings = [];
    private InspectionDto? _inspection;
    private Guid? _inspectionId;
    
    private static readonly string[] UploadsSeparator = ["/uploads/"];
    
    // HttpClient compartido para cargar imágenes (evita agotamiento de sockets)
    private static readonly HttpClient _imageHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    
    // Control de lazy loading
    private const int InitialLoadCount = 5; // Cargar primeras 5 imágenes inmediatamente
    private int _loadedCount = 0;
    private bool _isLoadingMore = false;

    public InspectionDetailsPage(IApiClient apiClient, IAuthService authService, Guid? inspectionId = null)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _authService = authService;
        _inspectionId = inspectionId;
        PhotosCollection.ItemsSource = _photoFindings;
        
        // Configurar lazy loading
        PhotosCollection.RemainingItemsThreshold = 2; // Cargar más cuando queden 2 items por mostrar
        PhotosCollection.RemainingItemsThresholdReached += OnRemainingItemsThresholdReached;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Si ya tenemos la inspección cargada, no recargar
        if (_inspection != null)
        {
            return;
        }
        
        // Si tenemos el ID, cargar los detalles
        if (_inspectionId.HasValue)
        {
            await LoadInspectionDetails(_inspectionId.Value);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("❌ No se proporcionó ID de inspección.");
            await DisplayAlertAsync("Error", "No se proporcionó ID de inspección.", "OK");
            await GoBackAsync();
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await GoBackAsync();
    }

    private async Task GoBackAsync()
    {
        // Intentar regresar a la página anterior usando Navigation
        var navigation = Navigation;
        if (navigation != null && navigation.NavigationStack.Count > 1)
        {
            await navigation.PopAsync();
        }
        else
        {
            // Si no hay página anterior, intentar obtener NavigationService
            var serviceProvider = Handler?.MauiContext?.Services;
            if (serviceProvider != null)
            {
                var navService = serviceProvider.GetService<INavigationService>();
                if (navService != null)
                {
                    await navService.NavigateToInspectionHistoryAsync();
                }
            }
        }
    }

    public async Task LoadInspectionDetails(Guid inspectionId)
    {
        _inspectionId = inspectionId;
        
        try
        {
            SetLoading(true);
            _inspection = await _apiClient.GetInspectionByIdAsync(inspectionId);
            
            if (_inspection != null)
            {
                // ⚠️ CORRECCIÓN: Obtener hallazgos directamente de la inspección (no de AnalysisId)
                List<FindingDetailDto> allFindings = [];
                try
                {
                    // Los hallazgos ahora están directamente en la inspección, no en inspecciones de análisis separadas
                    allFindings = await _apiClient.GetInspectionFindingsAsync(_inspection.Id);
                    System.Diagnostics.Debug.WriteLine($"✅ Hallazgos cargados para inspección {_inspection.Id}: {allFindings.Count} hallazgos");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Error al cargar hallazgos para inspección {_inspection.Id}: {ex.Message}");
                    allFindings = [];
                }
                
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
                
                // ⚠️ CORRECCIÓN: Obtener AffiliatedCompanyId de la inspección para validación de acceso
                var affiliatedCompanyId = _inspection.AffiliatedCompanyId;
                
                // Preparar todas las fotos primero
                var photoTasks = new List<Task<PhotoFindingViewModel>>();
                var photosList = _inspection.Photos.OrderBy(p => p.CapturedAt).ToList();
                
                foreach (var photo in photosList)
                {
                    // ⚠️ CORRECCIÓN: Pasar todos los hallazgos de la inspección y el AffiliatedCompanyId a cada foto
                    var photoTask = ProcessPhotoAsync(photo, baseUrl, allFindings, affiliatedCompanyId);
                    photoTasks.Add(photoTask);
                }
                
                // OPTIMIZACIÓN: Cargar todas las fotos en paralelo (paralelismo real, sin límites)
                // Cargar primero las primeras N imágenes para mostrar algo rápido
                var initialPhotos = photoTasks.Take(InitialLoadCount).ToList();
                var remainingPhotos = photoTasks.Skip(InitialLoadCount).ToList();
                
                // Cargar primeras imágenes inmediatamente
                var initialResults = await Task.WhenAll(initialPhotos);
                
                // Actualizar UI con primeras imágenes
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var photoFinding in initialResults)
                    {
                        _photoFindings.Add(photoFinding);
                    }
                    _loadedCount = initialResults.Length;
                });
                
                // Cargar el resto en background (lazy loading)
                if (remainingPhotos.Count > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        var remainingResults = await Task.WhenAll(remainingPhotos);
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            foreach (var photoFinding in remainingResults)
                            {
                                _photoFindings.Add(photoFinding);
                            }
                            _loadedCount = _photoFindings.Count;
                        });
                    });
                }
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo cargar la inspección.", "OK");
                await GoBackAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar detalles: {ex}");
            await DisplayAlertAsync("Error", $"Error al cargar detalles: {ex.Message}", "OK");
            await GoBackAsync();
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
    private async Task<PhotoFindingViewModel> ProcessPhotoAsync(PhotoInfoDto photo, string baseUrl, List<FindingDetailDto> inspectionFindings, Guid affiliatedCompanyId)
    {
        try
        {
            // ⚠️ CORRECCIÓN: Usar los hallazgos de la inspección directamente
            // Ya no usamos photo.AnalysisId porque los hallazgos están en la inspección original
            List<FindingDetailDto> findings = [];
            
            // Si la foto está analizada, usar los hallazgos de la inspección
            if (photo.IsAnalyzed)
            {
                findings = inspectionFindings; // Usar los hallazgos de la inspección
                System.Diagnostics.Debug.WriteLine($"Foto {photo.Id} analizada: {findings.Count} hallazgos asignados");
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
                    // ⚠️ CORRECCIÓN: Agregar affiliatedCompanyId como query parameter para validación de acceso
                    imageUrl = $"{baseUrl}/api/v1/file/images/{orgAndFile}?affiliatedCompanyId={affiliatedCompanyId}";
                }
            }
            else if (imageUrl.Contains("/api/v1/file/images/", StringComparison.Ordinal))
            {
                // Si ya es una URL del endpoint, agregar el query parameter si no existe
                if (!imageUrl.Contains("affiliatedCompanyId=", StringComparison.Ordinal))
                {
                    var separator = imageUrl.Contains('?') ? "&" : "?";
                    imageUrl = $"{imageUrl}{separator}affiliatedCompanyId={affiliatedCompanyId}";
                }
            }
            
            // Cargar la imagen de forma segura (con optimización automática)
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
                    // ⚠️ CORRECCIÓN: Agregar affiliatedCompanyId como query parameter para validación de acceso
                    imageUrl = $"{baseUrl}/api/v1/file/images/{orgAndFile}?affiliatedCompanyId={affiliatedCompanyId}";
                }
            }
            else if (imageUrl.Contains("/api/v1/file/images/", StringComparison.Ordinal))
            {
                // Si ya es una URL del endpoint, agregar el query parameter si no existe
                if (!imageUrl.Contains("affiliatedCompanyId=", StringComparison.Ordinal))
                {
                    var separator = imageUrl.Contains('?') ? "&" : "?";
                    imageUrl = $"{imageUrl}{separator}affiliatedCompanyId={affiliatedCompanyId}";
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
    /// Soporta parámetros de optimización del servidor (width, quality).
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

            // OPTIMIZACIÓN: Agregar parámetros de compresión/redimensionamiento si el servidor los soporta
            // Formato: ?width=800&quality=80 (si el servidor implementa estos parámetros)
            var optimizedUrl = AddImageOptimizationParams(imageUrl);

            _imageHttpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.CurrentToken);
            
            var response = await _imageHttpClient.GetAsync(optimizedUrl);
            if (response.IsSuccessStatusCode)
            {
                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                
                // Crear ImageSource desde bytes
                var imageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                
                System.Diagnostics.Debug.WriteLine($"✅ Imagen cargada exitosamente desde el servidor: {optimizedUrl}");
                return imageSource;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al cargar imagen desde servidor: {response.StatusCode} - {response.ReasonPhrase}. URL: {optimizedUrl}");
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
    
    /// <summary>
    /// Agrega parámetros de optimización a la URL de la imagen si el servidor los soporta.
    /// Parámetros: width (ancho máximo), quality (calidad de compresión 0-100).
    /// </summary>
    private static string AddImageOptimizationParams(string imageUrl)
    {
        // Si la URL ya tiene parámetros de optimización, no agregar más
        if (imageUrl.Contains("width=", StringComparison.OrdinalIgnoreCase) || 
            imageUrl.Contains("quality=", StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }
        
        // Agregar parámetros de optimización para reducir tamaño de descarga
        // Estos parámetros deben ser implementados en el FileController del servidor
        var separator = imageUrl.Contains('?') ? "&" : "?";
        return $"{imageUrl}{separator}width=1200&quality=85";
    }
    
    /// <summary>
    /// Maneja el evento de lazy loading cuando el usuario se acerca al final de la lista.
    /// </summary>
    private async void OnRemainingItemsThresholdReached(object? sender, EventArgs e)
    {
        if (_isLoadingMore || _inspection == null)
            return;
            
        _isLoadingMore = true;
        
        try
        {
            // Si ya cargamos todas las imágenes, no hacer nada
            if (_loadedCount >= _photoFindings.Count)
            {
                _isLoadingMore = false;
                return;
            }
            
            // Las imágenes restantes ya se están cargando en background desde LoadInspectionDetails
            // Este método es principalmente para logging/debugging
            System.Diagnostics.Debug.WriteLine($"📸 Lazy loading: Usuario cerca del final, {_photoFindings.Count - _loadedCount} imágenes pendientes");
        }
        finally
        {
            _isLoadingMore = false;
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

