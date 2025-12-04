using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Microsoft.Maui.ApplicationModel;
using VisioAnalytica.App.Risk.Services;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Pages;

/// <summary>
/// Página para visualizar el historial de inspecciones.
/// Diseño moderno y minimalista con filtros por empresa.
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
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(2); // Cache por 2 minutos
    private bool _isLoading = false;
    private CancellationTokenSource? _loadCancellationToken;

    public InspectionHistoryPage(IApiClient apiClient, IAuthService authService, INotificationService notificationService, INavigationService? navigationService = null)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _authService = authService;
        _notificationService = notificationService;
        _navigationService = navigationService;
        InspectionsCollection.ItemsSource = _inspections;
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
        
        // Solo cargar datos si no se han cargado o si el cache expiró
        // Esto evita recargas innecesarias cuando se cambia de tab en el TabbedPage
        if ((!_isDataLoaded || 
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
        
        StartStatusCheckTimer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopStatusCheckTimer();
        
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

            // Cargar inspecciones (en background)
            ct.ThrowIfCancellationRequested();
            await LoadInspectionsAsync(ct).ConfigureAwait(false);
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

    private async Task LoadInspectionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Cargar datos en background
            var inspections = await _apiClient.GetMyInspectionsAsync(_selectedCompanyFilter).ConfigureAwait(false);
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // Procesar todos los datos en background primero (más eficiente)
            var viewModels = inspections.Select(inspection => new InspectionViewModel
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
            
            // Actualizar UI una sola vez en el hilo principal (más eficiente)
            MainThread.BeginInvokeOnMainThread(() => UpdateInspectionsCollection(viewModels));
        }
        catch (OperationCanceledException)
        {
            // Carga cancelada, no hacer nada
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
    /// Método legacy para compatibilidad. Usa LoadInspectionsAsync internamente.
    /// </summary>
    private async Task LoadInspections()
    {
        await LoadInspectionsAsync(_loadCancellationToken?.Token ?? CancellationToken.None);
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
            
            // Al cambiar el filtro, forzar recarga
            // Cancelar carga anterior si existe
            _loadCancellationToken?.Cancel();
            await LoadInspectionsAsync(_loadCancellationToken?.Token ?? CancellationToken.None);
            _lastLoadTime = DateTime.Now; // Actualizar tiempo de carga
        }
    }
    
    /// <summary>
    /// Fuerza la recarga de datos (útil después de crear una nueva inspección).
    /// </summary>
    public async Task RefreshDataAsync()
    {
        _isDataLoaded = false;
        _lastLoadTime = null;
        
        // Cancelar carga anterior si existe
        _loadCancellationToken?.Cancel();
        
        // Cargar en background sin bloquear
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
        _inspections.Clear();
        // Agregar todos de una vez (más eficiente que uno por uno)
        foreach (var vm in viewModels)
        {
            _inspections.Add(vm);
        }
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
            // Solo verificar si la página está visible y hay datos cargados
            if (!IsVisible || !_isDataLoaded)
                return;
            
            // Verificar inspecciones que están en proceso
            var analyzingInspections = _inspections.Where(i => i.Status == "Analizando").ToList();
            
            // Si no hay inspecciones analizando, no hacer nada
            if (analyzingInspections.Count == 0)
                return;
            
            foreach (var inspection in analyzingInspections)
            {
                try
                {
                    var status = await _apiClient.GetAnalysisStatusAsync(inspection.Id).ConfigureAwait(false);
                    
                    if (status.Status == "Completed")
                    {
                        // Notificar al usuario
                        await _notificationService.ShowNotificationAsync(
                            "Análisis Completado",
                            $"El análisis de la inspección para {inspection.CompanyName} ha sido completado.").ConfigureAwait(false);
                        
                        // Recargar la lista (en background)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await LoadInspectionsAsync(_loadCancellationToken?.Token ?? CancellationToken.None).ConfigureAwait(false);
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
                    else if (status.Status == "Failed")
                    {
                        await _notificationService.ShowNotificationAsync(
                            "Análisis Fallido",
                            $"El análisis de la inspección para {inspection.CompanyName} ha fallado.").ConfigureAwait(false);
                        
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await LoadInspectionsAsync(_loadCancellationToken?.Token ?? CancellationToken.None).ConfigureAwait(false);
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


