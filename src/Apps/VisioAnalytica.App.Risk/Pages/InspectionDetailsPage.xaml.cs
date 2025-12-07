using System.Collections.ObjectModel;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Storage;
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
    
    // Semáforo para limitar descargas concurrentes (máximo 3 imágenes a la vez)
    private static readonly SemaphoreSlim _downloadSemaphore = new(3, 3);
    
    // Caché de rutas de imágenes (URL -> ruta local)
    private static readonly Dictionary<string, string> _imageCache = new();
    private static readonly object _cacheLock = new();

    public InspectionDetailsPage(IApiClient apiClient, IAuthService authService, Guid? inspectionId = null)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _authService = authService;
        _inspectionId = inspectionId;
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
                
                // OPTIMIZACIÓN: Cargar imágenes de forma progresiva (no esperar a todas)
                // Primero agregar todas las fotos sin imágenes (placeholder)
                var photoViewModels = new List<PhotoFindingViewModel>();
                foreach (var photo in _inspection.Photos.OrderBy(p => p.CapturedAt))
                {
                    var imageUrl = photo.ImageUrl.StartsWith("http") 
                        ? photo.ImageUrl 
                        : $"{baseUrl}{photo.ImageUrl}";
                    
                    // Convertir URL si es necesario
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
                        if (!imageUrl.Contains("affiliatedCompanyId=", StringComparison.Ordinal))
                        {
                            var separator = imageUrl.Contains('?') ? "&" : "?";
                            imageUrl = $"{imageUrl}{separator}affiliatedCompanyId={affiliatedCompanyId}";
                        }
                    }
                    
                    // Hallazgos para esta foto
                    List<FindingDetailDto> findings = [];
                    if (photo.IsAnalyzed)
                    {
                        findings = allFindings;
                    }
                    
                    var viewModel = new PhotoFindingViewModel
                    {
                        PhotoId = photo.Id,
                        ImageUrl = imageUrl,
                        ImageSource = null, // Se cargará después
                        CapturedAt = photo.CapturedAt,
                        Description = photo.Description,
                        IsAnalyzed = photo.IsAnalyzed,
                        Findings = [.. findings]
                    };
                    
                    photoViewModels.Add(viewModel);
                }
                
                // Agregar todas las fotos a la UI inmediatamente (sin imágenes)
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var viewModel in photoViewModels)
                    {
                        _photoFindings.Add(viewModel);
                    }
                });
                
                // OPTIMIZACIÓN: Cargar imágenes en paralelo con límite de concurrencia
                // Usar Task.Run para no bloquear el hilo principal
                _ = Task.Run(async () =>
                {
                    var loadTasks = photoViewModels.Select(async (viewModel, index) =>
                    {
                        var semaphoreAcquired = false;
                        try
                        {
                            // Esperar turno para descargar (máximo 3 simultáneas)
                            await _downloadSemaphore.WaitAsync();
                            semaphoreAcquired = true; // Marcar como adquirido solo si WaitAsync() fue exitoso
                            
                            // Pequeño delay para no sobrecargar (opcional)
                            if (index > 0)
                            {
                                await Task.Delay(index * 100); // 100ms entre cada inicio de descarga
                            }
                            
                            var imageSource = await LoadImageSecurelyAsync(viewModel.ImageUrl);
                            
                            // Actualizar UI en el hilo principal
                            await MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                viewModel.ImageSource = imageSource;
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ Error al cargar imagen {viewModel.ImageUrl}: {ex.Message}");
                        }
                        finally
                        {
                            // Solo liberar el semáforo si fue adquirido exitosamente
                            if (semaphoreAcquired)
                            {
                                _downloadSemaphore.Release();
                            }
                        }
                    });
                    
                    await Task.WhenAll(loadTasks);
                });
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
    /// Incluye caché local para evitar descargas repetidas.
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

            // OPTIMIZACIÓN: Verificar caché local primero
            string? cachedPath = null;
            lock (_cacheLock)
            {
                if (_imageCache.TryGetValue(imageUrl, out var path) && File.Exists(path))
                {
                    cachedPath = path;
                }
            }
            
            if (cachedPath != null)
            {
                System.Diagnostics.Debug.WriteLine($"✅ Imagen cargada desde caché: {imageUrl}");
                return ImageSource.FromFile(cachedPath);
            }

            // Si no está en caché, descargar
            _imageHttpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.CurrentToken);
            
            var response = await _imageHttpClient.GetAsync(imageUrl);
            if (response.IsSuccessStatusCode)
            {
                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                
                // OPTIMIZACIÓN: Guardar en caché local
                var cachePath = await SaveImageToCacheAsync(imageUrl, imageBytes);
                
                if (cachePath != null)
                {
                    lock (_cacheLock)
                    {
                        _imageCache[imageUrl] = cachePath;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"✅ Imagen descargada y guardada en caché: {imageUrl}");
                    return ImageSource.FromFile(cachePath);
                }
                else
                {
                    // Si falla el guardado en caché, usar memoria
                    var imageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                    System.Diagnostics.Debug.WriteLine($"✅ Imagen cargada desde servidor (sin caché): {imageUrl}");
                    return imageSource;
                }
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
    
    /// <summary>
    /// Guarda una imagen en el caché local del dispositivo.
    /// </summary>
    private async Task<string?> SaveImageToCacheAsync(string imageUrl, byte[] imageBytes)
    {
        try
        {
            // Generar nombre de archivo único basado en la URL
            var hash = ComputeHash(imageUrl);
            var fileName = $"{hash}.jpg";
            
            // Obtener directorio de caché
            var cacheDir = FileSystem.CacheDirectory;
            var imageCacheDir = Path.Combine(cacheDir, "inspection_images");
            
            if (!Directory.Exists(imageCacheDir))
            {
                Directory.CreateDirectory(imageCacheDir);
            }
            
            var filePath = Path.Combine(imageCacheDir, fileName);
            
            // Guardar archivo
            await File.WriteAllBytesAsync(filePath, imageBytes);
            
            // Limpiar caché antiguo si es necesario (mantener máximo 100MB)
            _ = Task.Run(() => CleanOldCacheFiles(imageCacheDir));
            
            return filePath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Error al guardar imagen en caché: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Limpia archivos antiguos del caché si excede el tamaño máximo.
    /// </summary>
    private void CleanOldCacheFiles(string cacheDir)
    {
        try
        {
            const long maxCacheSize = 100 * 1024 * 1024; // 100MB
            
            var files = Directory.GetFiles(cacheDir)
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.LastWriteTime)
                .ToList();
            
            long totalSize = files.Sum(f => f.Length);
            
            // Si excede el tamaño máximo, eliminar los más antiguos
            if (totalSize > maxCacheSize)
            {
                foreach (var file in files)
                {
                    if (totalSize <= maxCacheSize)
                        break;
                    
                    try
                    {
                        totalSize -= file.Length;
                        file.Delete();
                        
                        // Limpiar del diccionario de caché
                        lock (_cacheLock)
                        {
                            var keyToRemove = _imageCache.FirstOrDefault(kvp => kvp.Value == file.FullName).Key;
                            if (keyToRemove != null)
                            {
                                _imageCache.Remove(keyToRemove);
                            }
                        }
                    }
                    catch
                    {
                        // Ignorar errores al eliminar
                    }
                }
            }
        }
        catch
        {
            // Ignorar errores en limpieza
        }
    }
    
    /// <summary>
    /// Calcula un hash MD5 de una cadena para usar como nombre de archivo.
    /// </summary>
    private static string ComputeHash(string input)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
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

