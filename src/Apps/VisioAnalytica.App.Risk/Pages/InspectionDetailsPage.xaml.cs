using System.Collections.ObjectModel;
using System.ComponentModel;
using VisioAnalytica.App.Risk.Services;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Pages;

/// <summary>
/// Página para mostrar los detalles de una inspección, incluyendo fotos y hallazgos.
/// Refactorizado para corregir problemas de rendimiento, memory leaks y asignación incorrecta de hallazgos.
/// </summary>
public partial class InspectionDetailsPage : ContentPage
{
    private readonly IApiClient _apiClient;
    private readonly IAuthService _authService;
    private readonly ObservableCollection<PhotoFindingViewModel> _photoFindings = [];
    private InspectionDto? _inspection;
    private Guid? _inspectionId;
    
    private static readonly string[] UploadsSeparator = ["/uploads/"];
    
    // Control de lazy loading
    private const int InitialLoadCount = 10; // Cargar primeras 10 imágenes inmediatamente
    private int _loadedCount = 0;
    private bool _isLoadingMore = false;
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1); // Protección contra carga concurrente

    // Cache de hallazgos por PhotoId para evitar llamadas duplicadas
    private readonly Dictionary<Guid, List<FindingDetailDto>> _findingsCache = [];

    public InspectionDetailsPage(IApiClient apiClient, IAuthService authService, Guid? inspectionId = null)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _authService = authService;
        _inspectionId = inspectionId;
        PhotosCollection.ItemsSource = _photoFindings;
        
        // Configurar lazy loading
        PhotosCollection.RemainingItemsThreshold = 3; // Cargar más cuando queden 3 items por mostrar
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
        try
        {
            // Limpiar recursos antes de navegar
            await CleanupResourcesAsync();
            
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al navegar hacia atrás: {ex.Message}");
        }
    }

    private CancellationTokenSource? _cts;

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Cancelar tareas pendientes al salir para evitar crash
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Limpia recursos (ImageSource, cache, etc.) para evitar memory leaks.
    /// </summary>
    private async Task CleanupResourcesAsync()
    {
        try
        {
            await _loadSemaphore.WaitAsync();
            
            // Liberar ImageSource de cada ViewModel
            foreach (var photoFinding in _photoFindings)
            {
                // UriImageSource no implementa IDisposable, solo asignar null
                photoFinding.ImageSource = null;
            }
            
            // Limpiar cache
            _findingsCache.Clear();
            
            // Limpiar colección
            _photoFindings.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al limpiar recursos: {ex.Message}");
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    public async Task LoadInspectionDetails(Guid inspectionId)
    {
        _inspectionId = inspectionId;
        
        // Reiniciar token
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            SetLoading(true);
            _inspection = await _apiClient.GetInspectionByIdAsync(inspectionId);
            
            if (_inspection == null)
            {
                await DisplayAlertAsync("Error", "No se pudo cargar la inspección.", "OK");
                await GoBackAsync();
                return;
            }

            if (token.IsCancellationRequested) return;

            // Actualizar información de la inspección en el hilo principal
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CompanyNameLabel.Text = _inspection.AffiliatedCompanyName;
                StatusLabel.Text = $"Estado: {GetStatusDisplay(_inspection.Status)}";
                DateRangeLabel.Text = $"Fecha: {_inspection.StartedAt:dd/MM/yyyy HH:mm} - {(_inspection.CompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "En proceso")}";
                _photoFindings.Clear();
                _findingsCache.Clear(); // Limpiar cache anterior
            });
            
            // Construir URL base una sola vez
            var baseUrl = _apiClient.BaseUrl.TrimEnd('/');
            var affiliatedCompanyId = _inspection.AffiliatedCompanyId;
            
            // Cargar fotos en background de forma optimizada
            _ = Task.Run(async () =>
            {
                try 
                {
                    if (token.IsCancellationRequested) return;

                    // Ordenar fotos por fecha
                    var photosList = _inspection.Photos.OrderBy(p => p.CapturedAt).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"📸 Total fotos en inspección: {photosList.Count}");
                    foreach (var p in photosList)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - Foto {p.Id}: IsAnalyzed={p.IsAnalyzed}");
                    }
                    
                    if (photosList.Count == 0)
                    {
                        return;
                    }

                    // Cargar primeras fotos inmediatamente (lazy loading real)
                    var initialPhotos = photosList.Take(InitialLoadCount).ToList();
                    var remainingPhotos = photosList.Skip(InitialLoadCount).ToList();
                    
                    // Procesar lote inicial
                    var initialViewModels = new List<PhotoFindingViewModel>();
                    foreach (var photo in initialPhotos)
                    {
                        if (token.IsCancellationRequested) return;
                        
                        var viewModel = await ProcessPhotoAsync(photo, baseUrl, affiliatedCompanyId, token);
                        if (viewModel != null)
                        {
                            initialViewModels.Add(viewModel);
                        }
                    }
                    
                    if (token.IsCancellationRequested) return;

                    // Actualizar UI con lote inicial
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        foreach (var viewModel in initialViewModels)
                        {
                            _photoFindings.Add(viewModel);
                            System.Diagnostics.Debug.WriteLine($"✅ Foto agregada a UI: {viewModel.PhotoId}, Hallazgos: {viewModel.Findings.Count}");
                        }
                        _loadedCount = _photoFindings.Count;
                        System.Diagnostics.Debug.WriteLine($"✅ Total fotos en UI: {_photoFindings.Count}");
                    });
                    
                    // Cargar resto de fotos en background (lazy loading progresivo)
                    if (remainingPhotos.Count > 0)
                    {
                        // Procesar en chunks pequeños para no saturar memoria
                        const int chunkSize = 5;
                        var chunks = remainingPhotos.Chunk(chunkSize);
                        
                        foreach (var chunk in chunks)
                        {
                            if (token.IsCancellationRequested) return;
                            
                            var chunkViewModels = new List<PhotoFindingViewModel>();
                            foreach (var photo in chunk)
                            {
                                if (token.IsCancellationRequested) return;
                                
                                var viewModel = await ProcessPhotoAsync(photo, baseUrl, affiliatedCompanyId, token);
                                if (viewModel != null)
                                {
                                    chunkViewModels.Add(viewModel);
                                }
                            }
                            
                            if (token.IsCancellationRequested) return;
                            
                            // Actualizar UI con chunk
                            await MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                if (token.IsCancellationRequested) return;
                                foreach (var viewModel in chunkViewModels)
                                {
                                    _photoFindings.Add(viewModel);
                                    System.Diagnostics.Debug.WriteLine($"✅ Foto agregada a UI (chunk): {viewModel.PhotoId}, Hallazgos: {viewModel.Findings.Count}");
                                }
                                _loadedCount = _photoFindings.Count;
                            });
                            
                            // Pequeña pausa para dejar respirar al UI thread
                            await Task.Delay(50, token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancelación esperada, no loguear
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error en carga background: {ex}");
                    }
                }
            }, token);
        }
        catch (OperationCanceledException)
        {
            // Cancelación esperada
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar detalles: {ex}");
                await DisplayAlertAsync("Error", $"Error al cargar detalles: {ex.Message}", "OK");
                await GoBackAsync();
            }
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
    /// Procesa una foto individual: carga hallazgos específicos de esa foto e imagen.
    /// CORRECCIÓN CRÍTICA: Ahora carga los hallazgos correctos por PhotoId de cada foto.
    /// </summary>
    private async Task<PhotoFindingViewModel?> ProcessPhotoAsync(
        PhotoInfoDto photo, 
        string baseUrl, 
        Guid affiliatedCompanyId,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // ✅ CORRECCIÓN: Cargar hallazgos específicos de esta foto usando su PhotoId
            List<FindingDetailDto> findings = [];
            
            System.Diagnostics.Debug.WriteLine($"🔍 Procesando foto {photo.Id}: IsAnalyzed={photo.IsAnalyzed}");
            
            if (photo.IsAnalyzed)
            {
                System.Diagnostics.Debug.WriteLine($"📡 Intentando cargar hallazgos para PhotoId: {photo.Id}");
                
                // Usar cache para evitar llamadas duplicadas
                if (!_findingsCache.TryGetValue(photo.Id, out List<FindingDetailDto>? cachedFindings))
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"🌐 Llamando API: GET /api/v1/Inspection/photos/{photo.Id}/findings");
                        var apiFindings = await _apiClient.GetPhotoFindingsAsync(photo.Id);
                        
                        System.Diagnostics.Debug.WriteLine($"📥 Respuesta del API: {apiFindings?.Count ?? 0} hallazgos recibidos");
                        
                        findings = apiFindings?.ToList() ?? [];
                        
                        if (findings.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"📋 Primer hallazgo: {findings[0].Description}");
                        }
                        
                        _findingsCache[photo.Id] = findings;
                        System.Diagnostics.Debug.WriteLine($"✅ Hallazgos cargados y cacheados para foto {photo.Id}: {findings.Count} hallazgos");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ ERROR al cargar hallazgos para foto {photo.Id}: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"❌ Tipo de excepción: {ex.GetType().Name}");
                        System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                        if (ex.InnerException != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ InnerException: {ex.InnerException.Message}");
                        }
                        findings = []; // Continuar sin hallazgos
                    }
                }
                else
                {
                    // IMPORTANTE: Crear una copia de la lista del cache para evitar referencias compartidas
                    findings = [..cachedFindings];
                    System.Diagnostics.Debug.WriteLine($"✅ Hallazgos obtenidos del cache para foto {photo.Id}: {findings.Count} hallazgos");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Foto {photo.Id} no está analizada. IsAnalyzed: {photo.IsAnalyzed}");
            }
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // Construir URL completa de la imagen
            var imageUrl = BuildImageUrl(photo.ImageUrl, baseUrl, affiliatedCompanyId);
            
            // ✅ OPTIMIZACIÓN: Crear ImageSource solo para thumbnails (más liviano)
            // La imagen completa se cargará cuando el usuario la toque
            var thumbnailUrl = GetThumbnailUrl(imageUrl);
            var imageSource = thumbnailUrl != null 
                ? LoadImageSecurely(thumbnailUrl, preferThumbnail: false) 
                : LoadImageSecurely(imageUrl, preferThumbnail: false);
            
            // Crear ObservableCollection para que el binding funcione correctamente
            var findingsCollection = new ObservableCollection<FindingDetailDto>(findings);
            
            var viewModel = new PhotoFindingViewModel
            {
                PhotoId = photo.Id,
                ImageUrl = imageUrl,
                ImageSource = imageSource,
                ThumbnailUrl = thumbnailUrl,
                CapturedAt = photo.CapturedAt,
                IsAnalyzed = photo.IsAnalyzed,
                Findings = findingsCollection // Usar ObservableCollection para binding
            };
            
            System.Diagnostics.Debug.WriteLine($"📸 ViewModel creado para foto {photo.Id}: {viewModel.Findings.Count} hallazgos asignados");
            
            // Verificar que los hallazgos se asignaron correctamente
            if (viewModel.Findings.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"✅ Primer hallazgo: {viewModel.Findings[0].Description}");
            }
            
            return viewModel;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al procesar foto {photo.Id}: {ex.Message}");
            
            // Construir URL en caso de error también
            var imageUrl = BuildImageUrl(photo.ImageUrl, baseUrl, affiliatedCompanyId);
            
            return new PhotoFindingViewModel
            {
                PhotoId = photo.Id,
                ImageUrl = imageUrl,
                ImageSource = null,
                ThumbnailUrl = GetThumbnailUrl(imageUrl),
                CapturedAt = photo.CapturedAt,
                IsAnalyzed = photo.IsAnalyzed,
                Findings = [] // ObservableCollection vacía
            };
        }
    }

    /// <summary>
    /// Construye la URL completa de la imagen con validación de acceso.
    /// </summary>
    private static string BuildImageUrl(string photoImageUrl, string baseUrl, Guid affiliatedCompanyId)
    {
        var imageUrl = photoImageUrl.StartsWith("http") 
            ? photoImageUrl 
            : $"{baseUrl}{photoImageUrl}";
        
        // Convertir /uploads/{orgId}/{filename} a /api/v1/file/images/{orgId}/{filename} si es necesario
        if (imageUrl.Contains("/uploads/", StringComparison.Ordinal))
        {
            var parts = imageUrl.Split(UploadsSeparator, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                var orgAndFile = parts[1];
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
        
        return imageUrl;
    }

    /// <summary>
    /// Construye la URL del thumbnail basándose en la URL de la imagen original.
    /// </summary>
    private static string? GetThumbnailUrl(string originalImageUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(originalImageUrl)) return null;

            Uri? uri = null;
            string path;

            if (originalImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(originalImageUrl, UriKind.Absolute, out uri))
                {
                    return null;
                }
                path = uri.AbsolutePath;
            }
            else
            {
                // Es relativa, asumimos que empieza con /
                path = originalImageUrl.Split('?')[0];
            }

            // Buscamos el segmento /api/v1/file/images/
            // La ruta debería ser .../api/v1/file/images/{orgId}/{fileName}
            var keyword = "/api/v1/file/images/";
            var index = path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            
            if (index == -1) return null;

            var afterKeyword = path[(index + keyword.Length)..];
            var parts = afterKeyword.Split('/');
            
            if (parts.Length < 2) return null;

            var orgId = parts[0];
            var fileName = parts[1];

            // Construir nombre del thumbnail: thumb_{filename}
            var thumbnailFileName = $"thumb_{fileName}";

            // Preservar query string si existe
            var queryIndex = originalImageUrl.IndexOf('?');
            var queryString = queryIndex >= 0 
                ? originalImageUrl[queryIndex..] 
                : "";

            // Construir URL del thumbnail (relativa o absoluta según venga)
            if (uri != null)
            {
                var builder = new UriBuilder(uri)
                {
                    Path = $"{keyword}{orgId}/thumbnails/{thumbnailFileName}",
                    Query = queryString.TrimStart('?')
                };
                return builder.Uri.ToString();
            }
            else
            {
                return $"{keyword}{orgId}/thumbnails/{thumbnailFileName}{queryString}";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error GetThumbnailUrl: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Crea un ImageSource optimizado usando cache nativo y headers de autorización.
    /// Implementa lazy loading real: solo crea el ImageSource cuando es necesario.
    /// </summary>
    private UriImageSource? LoadImageSecurely(string imageUrl, bool preferThumbnail = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            var token = _authService.CurrentToken;

            // Determinar URL final (Thumbnail o Full)
            string targetUrl = imageUrl;
            
            if (preferThumbnail)
            {
                var thumbUrl = GetThumbnailUrl(imageUrl);
                if (!string.IsNullOrEmpty(thumbUrl))
                {
                    var baseUrl = imageUrl.Contains("http") 
                        ? new Uri(imageUrl).GetLeftPart(UriPartial.Authority)
                        : "";
                    targetUrl = thumbUrl.StartsWith("http") 
                        ? thumbUrl 
                        : $"{baseUrl}{thumbUrl}";
                }
            }
            
            // Asegurar que sea absoluta si no lo es
            if (!targetUrl.StartsWith("http") && !string.IsNullOrEmpty(_apiClient.BaseUrl))
            {
                var baseUrl = _apiClient.BaseUrl.TrimEnd('/');
                targetUrl = $"{baseUrl}{targetUrl}";
            }

            // Inyectar Token en URL (soportado por la API)
            if (!string.IsNullOrWhiteSpace(token))
            {
                var separator = targetUrl.Contains('?') ? "&" : "?";
                targetUrl = $"{targetUrl}{separator}access_token={token}";
            }

            var source = new UriImageSource
            {
                Uri = new Uri(targetUrl),
                CachingEnabled = true,
                CacheValidity = TimeSpan.FromDays(2) // Cache agresivo para performance
            };

            return source;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creando UriImageSource para {imageUrl}: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Maneja el evento de lazy loading cuando el usuario se acerca al final de la lista.
    /// </summary>
    private async void OnRemainingItemsThresholdReached(object? sender, EventArgs e)
    {
        if (_isLoadingMore || _inspection == null)
            return;
            
        await _loadSemaphore.WaitAsync();
        _isLoadingMore = true;
        
        try
        {
            // Si ya cargamos todas las imágenes, no hacer nada
            if (_loadedCount >= _photoFindings.Count)
            {
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"📸 Lazy loading: Usuario cerca del final, {_photoFindings.Count - _loadedCount} imágenes pendientes");
            
            // Las imágenes restantes ya se están cargando en background desde LoadInspectionDetails
            // Este método es principalmente para logging/debugging
        }
        finally
        {
            _isLoadingMore = false;
            _loadSemaphore.Release();
        }
    }

    /// <summary>
    /// Maneja el evento cuando el usuario presiona sobre una imagen.
    /// Abre la imagen en pantalla completa.
    /// </summary>
    private async void OnImageTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string imageUrl || string.IsNullOrWhiteSpace(imageUrl))
            return;
            
        try
        {
            // Obtener la URL de la imagen completa
            var fullImageUrl = imageUrl;
            
            // Si es un thumbnail, obtener la URL de la imagen completa
            if (imageUrl.Contains("/thumbnails/", StringComparison.Ordinal))
            {
                // Convertir thumbnail URL a imagen completa
                // Formato: /api/v1/file/images/{orgId}/thumbnails/thumb_{filename}
                // A: /api/v1/file/images/{orgId}/{filename}
                fullImageUrl = imageUrl.Replace("/thumbnails/thumb_", "/");
            }
            
            // Construir URL completa si es necesario
            var baseUrl = _apiClient.BaseUrl.TrimEnd('/');
            if (!fullImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                fullImageUrl = $"{baseUrl}{fullImageUrl}";
            }
            
            // Cargar la imagen completa
            var fullImageSource = LoadImageSecurely(fullImageUrl, preferThumbnail: false);
            
            if (fullImageSource == null)
            {
                await DisplayAlertAsync("Error", "No se pudo cargar la imagen completa.", "OK");
                return;
            }
            
            // Crear una página modal para mostrar la imagen en pantalla completa
            var fullImagePage = new ContentPage
            {
                BackgroundColor = Colors.Black,
                Title = "Imagen Completa"
            };
            
            // Crear ScrollView para permitir zoom y desplazamiento
            var scrollView = new ScrollView
            {
                Content = new Image
                {
                    Source = fullImageSource,
                    Aspect = Aspect.AspectFit,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            
            // Agregar botón de cerrar
            var closeButton = new Button
            {
                Text = "✕ Cerrar",
                BackgroundColor = Color.FromRgba(0, 0, 0, 128), // Semi-transparente
                TextColor = Colors.White,
                FontSize = 16,
                Margin = new Thickness(10, 10, 0, 0),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                Padding = new Thickness(12, 8)
            };
            closeButton.Clicked += async (s, args) => 
            {
                // UriImageSource no implementa IDisposable, solo asignar null
                fullImageSource = null;
                await Navigation.PopModalAsync();
            };
            
            // Crear Grid para superponer el botón sobre la imagen
            var grid = new Grid
            {
                Children = 
                {
                    scrollView,
                    closeButton
                }
            };
            
            fullImagePage.Content = grid;
            
            await Navigation.PushModalAsync(fullImagePage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al abrir imagen en pantalla completa: {ex.Message}");
            await DisplayAlertAsync("Error", "No se pudo abrir la imagen en pantalla completa.", "OK");
        }
    }
}

/// <summary>
/// ViewModel para una foto con sus hallazgos.
/// Refactorizado para optimizar memoria y rendimiento.
/// Implementa INotifyPropertyChanged para que el binding funcione correctamente.
/// </summary>
public partial class PhotoFindingViewModel : INotifyPropertyChanged
{
    private Guid _photoId;
    private string _imageUrl = string.Empty;
    private string? _thumbnailUrl;
    private ImageSource? _imageSource;
    private DateTime _capturedAt;
    private string? _description;
    private bool _isAnalyzed;
    private ObservableCollection<FindingDetailDto> _findings = [];

    public Guid PhotoId
    {
        get => _photoId;
        set { _photoId = value; OnPropertyChanged(); }
    }

    public string ImageUrl
    {
        get => _imageUrl;
        set { _imageUrl = value; OnPropertyChanged(); }
    }

    public string? ThumbnailUrl
    {
        get => _thumbnailUrl;
        set { _thumbnailUrl = value; OnPropertyChanged(); }
    }

    public ImageSource? ImageSource
    {
        get => _imageSource;
        set { _imageSource = value; OnPropertyChanged(); }
    }

    public DateTime CapturedAt
    {
        get => _capturedAt;
        set { _capturedAt = value; OnPropertyChanged(); }
    }

    public string? Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public bool IsAnalyzed
    {
        get => _isAnalyzed;
        set { _isAnalyzed = value; OnPropertyChanged(); }
    }

    public ObservableCollection<FindingDetailDto> Findings
    {
        get => _findings;
        set 
        { 
            if (_findings != value)
            {
                _findings = value ?? [];
                OnPropertyChanged();
                // Notificar también cambios en la colección para que el CollectionView se actualice
                OnPropertyChanged(nameof(HasFindings));
            }
        }
    }

    /// <summary>
    /// Propiedad calculada para facilitar el binding en XAML.
    /// </summary>
    public bool HasFindings => Findings != null && Findings.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
