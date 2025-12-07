using System.Collections.ObjectModel;
using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using VisioAnalytica.App.Risk.Services;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Pages;

/// <summary>
/// Página para visualizar el historial de inspecciones.
/// Diseño moderno y minimalista con filtros por empresa.
/// Optimizada con caché persistente, paginación y pull-to-refresh.
/// </summary>
public partial class InspectionHistoryPage : ContentPage
{
    private readonly IApiClient _apiClient;
    private readonly IAuthService _authService;
    private readonly INotificationService _notificationService;
    private readonly INavigationService? _navigationService;
    private readonly ObservableCollection<InspectionViewModel> _inspections = [];
    private IList<AffiliatedCompanyDto>? _companies;
    private Guid? _selectedCompanyFilter;
    private System.Timers.Timer? _statusCheckTimer;
    private bool _isDataLoaded = false;
    private DateTime? _lastLoadTime;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5); // Cache por 5 minutos (aumentado)
    private static readonly TimeSpan PersistentCacheExpiration = TimeSpan.FromHours(1); // Cache persistente por 1 hora
    private bool _isLoading = false;
    private bool _isLoadingMore = false;
    private CancellationTokenSource? _loadCancellationToken;
    
    // Paginación del servidor
    private const int PageSize = 20;
    private int _currentPageNumber = 1;
    private int _totalPages = 0;
    private int _totalCount = 0;
    private bool _hasMorePages = true;
    
    // Sincronización en background
    private System.Timers.Timer? _backgroundSyncTimer;
    private static readonly TimeSpan BackgroundSyncInterval = TimeSpan.FromMinutes(5); // Sincronizar cada 5 minutos
    
    // Caché
    private const string CacheKeyPrefix = "inspections_cache_";
    private const string CacheTimeKeyPrefix = "inspections_cache_time_";
    private const string CacheCompressedKeyPrefix = "inspections_cache_compressed_";

    public InspectionHistoryPage(IApiClient apiClient, IAuthService authService, INotificationService notificationService, INavigationService? navigationService = null)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _authService = authService;
        _notificationService = notificationService;
        _navigationService = navigationService;
        InspectionsCollection.ItemsSource = _inspections;
        
        // Configurar Pull-to-Refresh
        RefreshContainer.Refreshing += OnRefreshViewRefreshing;
    }
    
    private INavigationService GetNavigationService()
    {
        if (_navigationService != null)
            return _navigationService;

        var serviceProvider = Handler?.MauiContext?.Services;
        if (serviceProvider != null)
        {
            return serviceProvider.GetRequiredService<INavigationService>();
        }

        throw new InvalidOperationException("INavigationService no está disponible.");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Cargar desde caché en background para no bloquear UI
        if (IsVisible && !_isDataLoaded && !_isLoading)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    LoadFromPersistentCacheCompressed();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al cargar caché: {ex}");
                }
            });
        }
        
        // Solo cargar datos si no se han cargado o si el cache expiró
        // Y solo si la página está realmente visible (evita cargas cuando se cambia de tab)
        if (IsVisible &&
            (!_isDataLoaded || 
            (_lastLoadTime.HasValue && DateTime.Now - _lastLoadTime.Value > CacheExpiration)) &&
            !_isLoading)
        {
            // Lanzar la carga sin esperarla (fire and forget)
            // Esto permite que el usuario cambie de tab inmediatamente
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadDataAsync().ConfigureAwait(false);
                    _isDataLoaded = true;
                    _lastLoadTime = DateTime.Now;
                }
                catch (OperationCanceledException)
                {
                    // Carga cancelada, no hacer nada
                    System.Diagnostics.Debug.WriteLine("Carga de datos cancelada");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al cargar datos en background: {ex}");
                }
            });
        }
        
        // Solo iniciar los timers si la página está visible
        if (IsVisible)
        {
            StartStatusCheckTimer();
            StartBackgroundSync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopStatusCheckTimer();
        StopBackgroundSync();
        
        // Cancelar carga si está en progreso cuando el usuario cambia de tab
        _loadCancellationToken?.Cancel();
        _loadCancellationToken = null;
    }

    private async Task LoadDataAsync()
    {
        // Cancelar carga anterior si existe
        _loadCancellationToken?.Cancel();
        _loadCancellationToken = new CancellationTokenSource();
        var ct = _loadCancellationToken.Token;
        
        try
        {
            _isLoading = true;
            
            // Actualizar UI en el hilo principal
            MainThread.BeginInvokeOnMainThread(SetLoadingTrue);

            // Cargar empresas para el filtro (en background)
            var roles = _authService.CurrentUserRoles;
            if (roles.Contains("Inspector"))
            {
                ct.ThrowIfCancellationRequested();
                _companies = await _apiClient.GetMyCompaniesAsync().ConfigureAwait(false);
                
                // Actualizar UI en el hilo principal
                MainThread.BeginInvokeOnMainThread(UpdateCompanyFilter);
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(HideCompanyFilter);
            }

            // Cargar primera página de inspecciones (en background)
            ct.ThrowIfCancellationRequested();
            await LoadInspectionsAsync(1, append: false, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Carga cancelada, no hacer nada
            System.Diagnostics.Debug.WriteLine("Carga de datos cancelada por el usuario");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar datos: {ex}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync("Error", "No se pudieron cargar las inspecciones.", "OK");
            }).ConfigureAwait(false);
        }
        finally
        {
            _isLoading = false;
            MainThread.BeginInvokeOnMainThread(SetLoadingFalse);
        }
    }
    
    /// <summary>
    /// Método legacy para compatibilidad. Usa LoadDataAsync internamente.
    /// </summary>
    private async Task LoadData()
    {
        await LoadDataAsync();
    }

    private async Task LoadInspectionsAsync(int pageNumber = 1, bool append = false, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Cargar página desde el servidor
            var pagedResult = await _apiClient.GetMyInspectionsPagedAsync(
                pageNumber: pageNumber, 
                pageSize: PageSize, 
                affiliatedCompanyId: _selectedCompanyFilter).ConfigureAwait(false);
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // Actualizar información de paginación
            _currentPageNumber = pagedResult.PageNumber;
            _totalPages = pagedResult.TotalPages;
            _totalCount = pagedResult.TotalCount;
            _hasMorePages = pagedResult.HasNextPage;
            
            // Procesar datos
            var viewModels = pagedResult.Items.Select(inspection => new InspectionViewModel
            {
                Id = inspection.Id,
                CompanyName = inspection.AffiliatedCompanyName,
                Status = GetStatusDisplay(inspection.Status),
                StatusColor = GetStatusColor(inspection.Status),
                StatusBackgroundColor = GetStatusBackgroundColor(inspection.Status),
                DateRange = $"{inspection.StartedAt:dd/MM/yyyy HH:mm} - {(inspection.CompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "En proceso")}",
                PhotosInfo = $"{inspection.AnalyzedPhotosCount} de {inspection.PhotosCount} fotos analizadas"
            }).ToList();
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // Guardar en caché persistente comprimido (solo primera página)
            if (pageNumber == 1)
            {
                SaveToPersistentCacheCompressed(viewModels);
            }
            
            // Actualizar UI de forma optimizada (batch update)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (!append)
                    {
                        _inspections.Clear();
                    }
                    
                    // Agregar todos los items de una vez para mejor rendimiento
                    foreach (var vm in viewModels)
                    {
                        _inspections.Add(vm);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al actualizar UI: {ex}");
                }
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar inspecciones: {ex}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync("Error", "No se pudieron cargar las inspecciones.", "OK");
            }).ConfigureAwait(false);
        }
    }
    
    /// <summary>
    /// Se ejecuta cuando el usuario hace scroll cerca del final (infinite scroll con paginación del servidor).
    /// </summary>
    private void OnRemainingItemsThresholdReached(object? sender, EventArgs e)
    {
        if (_isLoadingMore || !_hasMorePages)
            return;
        
        _isLoadingMore = true;
        LoadingMoreIndicator.IsRunning = true;
        LoadingMoreIndicator.IsVisible = true;
        
        _ = Task.Run(async () =>
        {
            try
            {
                var nextPage = _currentPageNumber + 1;
                await LoadInspectionsAsync(nextPage, append: true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar más items: {ex}");
            }
            finally
            {
                _isLoadingMore = false;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadingMoreIndicator.IsRunning = false;
                    LoadingMoreIndicator.IsVisible = false;
                });
            }
        });
    }
    
    /// <summary>
    /// Maneja el Pull-to-Refresh.
    /// </summary>
    private async void OnRefreshViewRefreshing(object? sender, EventArgs e)
    {
        try
        {
            // Forzar recarga desde el servidor
            _isDataLoaded = false;
            _lastLoadTime = null;
            _currentPageNumber = 1;
            _hasMorePages = true;
            
            // Cancelar carga anterior si existe
            _loadCancellationToken?.Cancel();
            
            await LoadInspectionsAsync(1, append: false).ConfigureAwait(false);
            _isDataLoaded = true;
            _lastLoadTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al refrescar: {ex}");
        }
        finally
        {
            // Detener el indicador de refresh
            RefreshContainer.IsRefreshing = false;
        }
    }
    
    /// <summary>
    /// Guarda los datos en caché persistente comprimido.
    /// </summary>
    private void SaveToPersistentCacheCompressed(List<InspectionViewModel> viewModels)
    {
        try
        {
            var timeKey = GetCacheTimeKey();
            var compressedKey = GetCompressedCacheKey();
            
            var json = JsonSerializer.Serialize(viewModels);
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            
            // Comprimir usando GZip
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress))
                {
                    gzip.Write(jsonBytes, 0, jsonBytes.Length);
                }
                var compressed = output.ToArray();
                
                // Guardar comprimido
                var compressedBase64 = Convert.ToBase64String(compressed);
                Preferences.Set(compressedKey, compressedBase64);
                Preferences.Set(timeKey, DateTime.Now.ToBinary());
                
                System.Diagnostics.Debug.WriteLine($"Caché comprimido guardado: tamaño original: {jsonBytes.Length}, comprimido: {compressed.Length} ({100.0 * compressed.Length / jsonBytes.Length:F1}%)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al guardar caché comprimido: {ex}");
        }
    }
    
    /// <summary>
    /// Carga los datos desde caché persistente comprimido si están disponibles y no han expirado.
    /// Este método debe ejecutarse en un hilo en background para no bloquear la UI.
    /// </summary>
    private void LoadFromPersistentCacheCompressed()
    {
        try
        {
            var compressedKey = GetCompressedCacheKey();
            var timeKey = GetCacheTimeKey();
            
            if (!Preferences.ContainsKey(compressedKey) || !Preferences.ContainsKey(timeKey))
                return;
            
            var cacheTime = DateTime.FromBinary(Preferences.Get(timeKey, 0L));
            if (DateTime.Now - cacheTime > PersistentCacheExpiration)
                return;
            
            var compressedBase64 = Preferences.Get(compressedKey, string.Empty);
            if (string.IsNullOrEmpty(compressedBase64))
                return;
            
            // Descomprimir en background
            var compressed = Convert.FromBase64String(compressedBase64);
            List<InspectionViewModel> viewModels;
            
            using (var input = new MemoryStream(compressed))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                var jsonBytes = output.ToArray();
                var json = Encoding.UTF8.GetString(jsonBytes);
                
                viewModels = JsonSerializer.Deserialize<List<InspectionViewModel>>(json) ?? [];
            }
            
            // Actualizar UI en el hilo principal de forma optimizada
            if (viewModels.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        _inspections.Clear();
                        // Agregar todos los items de una vez para mejor rendimiento
                        foreach (var vm in viewModels)
                        {
                            _inspections.Add(vm);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al actualizar UI desde caché: {ex}");
                    }
                });
                
                _isDataLoaded = true;
                _lastLoadTime = cacheTime;
                _currentPageNumber = 1;
                
                System.Diagnostics.Debug.WriteLine($"Cargados {viewModels.Count} items desde caché comprimido");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar caché comprimido: {ex}");
        }
    }
    
    /// <summary>
    /// Obtiene el ID del usuario actual desde el token JWT.
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        try
        {
            var token = _authService.CurrentToken;
            if (string.IsNullOrWhiteSpace(token))
                return null;
            
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            
            var userIdClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "uid");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }
        }
        catch
        {
            // Si hay error al decodificar, retornar null
        }
        
        return null;
    }
    
    private string GetCacheKey()
    {
        var userId = GetCurrentUserId()?.ToString() ?? "unknown";
        var companyId = _selectedCompanyFilter?.ToString() ?? "all";
        return $"{CacheKeyPrefix}{userId}_{companyId}";
    }
    
    private string GetCacheTimeKey()
    {
        var userId = GetCurrentUserId()?.ToString() ?? "unknown";
        var companyId = _selectedCompanyFilter?.ToString() ?? "all";
        return $"{CacheTimeKeyPrefix}{userId}_{companyId}";
    }
    
    private string GetCompressedCacheKey()
    {
        var userId = GetCurrentUserId()?.ToString() ?? "unknown";
        var companyId = _selectedCompanyFilter?.ToString() ?? "all";
        return $"{CacheCompressedKeyPrefix}{userId}_{companyId}";
    }
    
    /// <summary>
    /// Inicia la sincronización en background.
    /// </summary>
    private void StartBackgroundSync()
    {
        _backgroundSyncTimer?.Stop();
        _backgroundSyncTimer?.Dispose();
        
        _backgroundSyncTimer = new System.Timers.Timer(BackgroundSyncInterval.TotalMilliseconds);
        _backgroundSyncTimer.Elapsed += async (sender, e) => await SyncInBackgroundAsync();
        _backgroundSyncTimer.AutoReset = true;
        _backgroundSyncTimer.Start();
    }
    
    /// <summary>
    /// Detiene la sincronización en background.
    /// </summary>
    private void StopBackgroundSync()
    {
        _backgroundSyncTimer?.Stop();
        _backgroundSyncTimer?.Dispose();
        _backgroundSyncTimer = null;
    }
    
    /// <summary>
    /// Sincroniza los datos en background cuando la app está en segundo plano.
    /// </summary>
    private async Task SyncInBackgroundAsync()
    {
        try
        {
            // Solo sincronizar si la app está en segundo plano o la página no está visible
            if (IsVisible && Application.Current?.Windows?.FirstOrDefault()?.IsActivated == true)
                return;
            
            System.Diagnostics.Debug.WriteLine("Sincronizando datos en background...");
            
            // Cargar primera página para actualizar caché
            var pagedResult = await _apiClient.GetMyInspectionsPagedAsync(
                pageNumber: 1, 
                pageSize: PageSize, 
                affiliatedCompanyId: _selectedCompanyFilter).ConfigureAwait(false);
            
            if (pagedResult.Items.Count > 0)
            {
                var viewModels = pagedResult.Items.Select(inspection => new InspectionViewModel
                {
                    Id = inspection.Id,
                    CompanyName = inspection.AffiliatedCompanyName,
                    Status = GetStatusDisplay(inspection.Status),
                    StatusColor = GetStatusColor(inspection.Status),
                    StatusBackgroundColor = GetStatusBackgroundColor(inspection.Status),
                    DateRange = $"{inspection.StartedAt:dd/MM/yyyy HH:mm} - {(inspection.CompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "En proceso")}",
                    PhotosInfo = $"{inspection.AnalyzedPhotosCount} de {inspection.PhotosCount} fotos analizadas"
                }).ToList();
                
                SaveToPersistentCacheCompressed(viewModels);
                _lastLoadTime = DateTime.Now;
                
                System.Diagnostics.Debug.WriteLine($"Sincronización en background completada: {viewModels.Count} items");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en sincronización background: {ex}");
        }
    }

    private async void OnCompanyFilterChanged(object? sender, EventArgs e)
    {
        if (CompanyFilterPicker.SelectedItem is string selectedItem)
        {
            if (selectedItem == "Todas las empresas")
            {
                _selectedCompanyFilter = null;
            }
            else if (_companies != null)
            {
                var company = _companies.FirstOrDefault(c => c.Name == selectedItem);
                _selectedCompanyFilter = company?.Id;
            }
            
            // Al cambiar el filtro, limpiar caché y forzar recarga
            ClearPersistentCache();
            _isDataLoaded = false;
            _lastLoadTime = null;
            _currentPageNumber = 1;
            _hasMorePages = true;
            _inspections.Clear();
            
            // Cancelar carga anterior si existe
            _loadCancellationToken?.Cancel();
            
            // Mostrar indicador de carga
            MainThread.BeginInvokeOnMainThread(SetLoadingTrue);
            
            // Cargar en background para no bloquear la UI
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadInspectionsAsync(1, append: false, _loadCancellationToken?.Token ?? CancellationToken.None).ConfigureAwait(false);
                    _isDataLoaded = true;
                    _lastLoadTime = DateTime.Now;
                }
                catch (OperationCanceledException)
                {
                    // Ignorar cancelación
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al cargar inspecciones con filtro: {ex}");
                }
                finally
                {
                    MainThread.BeginInvokeOnMainThread(SetLoadingFalse);
                }
            });
        }
    }
    
    /// <summary>
    /// Limpia el caché persistente para el filtro actual.
    /// </summary>
    private void ClearPersistentCache()
    {
        try
        {
            var cacheKey = GetCacheKey();
            var timeKey = GetCacheTimeKey();
            var compressedKey = GetCompressedCacheKey();
            
            if (Preferences.ContainsKey(cacheKey))
                Preferences.Remove(cacheKey);
            
            if (Preferences.ContainsKey(timeKey))
                Preferences.Remove(timeKey);
            
            if (Preferences.ContainsKey(compressedKey))
                Preferences.Remove(compressedKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al limpiar caché persistente: {ex}");
        }
    }
    
    /// <summary>
    /// Fuerza la recarga de datos (útil después de crear una nueva inspección).
    /// </summary>
    public async Task RefreshDataAsync()
    {
        _isDataLoaded = false;
        _lastLoadTime = null;
        _currentPageNumber = 1;
        _hasMorePages = true;
        
        // Limpiar caché persistente para forzar recarga desde servidor
        ClearPersistentCache();
        
        // Cancelar carga anterior si existe
        _loadCancellationToken?.Cancel();
        
        // Cargar en background sin bloquear
        _ = Task.Run(async () =>
        {
            try
            {
                await LoadInspectionsAsync(1, append: false).ConfigureAwait(false);
                _isDataLoaded = true;
                _lastLoadTime = DateTime.Now;
            }
            catch (OperationCanceledException)
            {
                // Carga cancelada, no hacer nada
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al refrescar datos: {ex}");
            }
        });
    }

    private async void OnViewDetailsClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is Guid inspectionId)
        {
            // Navegar a página de detalles con el ID
            await GetNavigationService().NavigateToInspectionDetailsAsync(inspectionId);
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

    private static Color GetStatusColor(string status)
    {
        return status switch
        {
            "Completed" => Color.FromArgb("#4CAF50"),
            "Analyzing" => Color.FromArgb("#2196F3"),
            "Failed" => Color.FromArgb("#F44336"),
            "PhotosCaptured" => Color.FromArgb("#FF9800"),
            _ => Color.FromArgb("#757575")
        };
    }

    private static Color GetStatusBackgroundColor(string status)
    {
        return status switch
        {
            "Completed" => Color.FromArgb("#E8F5E9"),
            "Analyzing" => Color.FromArgb("#E3F2FD"),
            "Failed" => Color.FromArgb("#FFEBEE"),
            "PhotosCaptured" => Color.FromArgb("#FFF3E0"),
            _ => Color.FromArgb("#F5F5F5")
        };
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
    }

    // Métodos auxiliares para MainThread.BeginInvokeOnMainThread
    private void SetLoadingTrue()
    {
        SetLoading(true);
    }

    private void SetLoadingFalse()
    {
        SetLoading(false);
    }

    private void UpdateCompanyFilter()
    {
        var companyList = new List<string> { "Todas las empresas" };
        if (_companies != null)
        {
            companyList.AddRange(_companies.Select(c => c.Name));
        }
        CompanyFilterPicker.ItemsSource = companyList;
    }

    private void HideCompanyFilter()
    {
        CompanyFilterPicker.IsVisible = false;
    }

    private void UpdateInspectionsCollection(List<InspectionViewModel> viewModels)
    {
        // Este método ya no se usa directamente, pero se mantiene por compatibilidad
        // La actualización ahora se hace directamente en LoadInspectionsAsync
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _inspections.Clear();
            foreach (var vm in viewModels)
            {
                _inspections.Add(vm);
            }
        });
    }

    private void StartStatusCheckTimer()
    {
        // Verificar estado de inspecciones cada 30 segundos
        _statusCheckTimer = new System.Timers.Timer(30000); // 30 segundos
        _statusCheckTimer.Elapsed += async (sender, e) => await CheckInspectionStatusesAsync();
        _statusCheckTimer.AutoReset = true;
        _statusCheckTimer.Start();
    }

    private void StopStatusCheckTimer()
    {
        _statusCheckTimer?.Stop();
        _statusCheckTimer?.Dispose();
        _statusCheckTimer = null;
    }

    private async Task CheckInspectionStatusesAsync()
    {
        try
        {
            // Solo verificar si la página está visible, hay datos cargados y no hay carga en progreso
            if (!IsVisible || !_isDataLoaded || _isLoading)
                return;
            
            // Verificar inspecciones que están en proceso
            var analyzingInspections = _inspections.Where(i => i.Status == "Analizando").ToList();
            
            // Si no hay inspecciones analizando, no hacer nada
            if (analyzingInspections.Count == 0)
                return;
            
            // Procesar en background sin bloquear
            foreach (var inspection in analyzingInspections)
            {
                try
                {
                    // Verificar si la página sigue visible antes de cada llamada
                    // Esto evita hacer llamadas innecesarias si el usuario cambió de tab
                    if (!IsVisible)
                        break;
                    
                    var status = await _apiClient.GetAnalysisStatusAsync(inspection.Id).ConfigureAwait(false);
                    
                    // Verificar nuevamente después de la llamada
                    if (!IsVisible)
                        break;
                    
                    if (status.Status == "Completed")
                    {
                        // Notificar al usuario
                        await _notificationService.ShowNotificationAsync(
                            "Análisis Completado",
                            $"El análisis de la inspección para {inspection.CompanyName} ha sido completado.").ConfigureAwait(false);
                        
                        // Recargar la lista (en background) solo si la página sigue visible
                        if (IsVisible)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await LoadInspectionsAsync(1, append: false, _loadCancellationToken?.Token ?? CancellationToken.None).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException)
                                {
                                    // Ignorar cancelación
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error al recargar inspecciones: {ex}");
                                }
                            });
                        }
                    }
                    else if (status.Status == "Failed")
                    {
                        await _notificationService.ShowNotificationAsync(
                            "Análisis Fallido",
                            $"El análisis de la inspección para {inspection.CompanyName} ha fallado.").ConfigureAwait(false);
                        
                        if (IsVisible)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await LoadInspectionsAsync(1, append: false, _loadCancellationToken?.Token ?? CancellationToken.None).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException)
                                {
                                    // Ignorar cancelación
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error al recargar inspecciones: {ex}");
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al verificar estado de inspección {inspection.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al verificar estados de inspecciones: {ex.Message}");
        }
    }
}

/// <summary>
/// ViewModel para una inspección en el historial.
/// </summary>
public class InspectionViewModel
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public Color StatusBackgroundColor { get; set; } = Colors.Transparent;
    public string DateRange { get; set; } = string.Empty;
    public string PhotosInfo { get; set; } = string.Empty;
}


