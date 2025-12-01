using System.Collections.ObjectModel;
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
    private readonly ObservableCollection<InspectionViewModel> _inspections = [];
    private IList<AffiliatedCompanyDto>? _companies;
    private Guid? _selectedCompanyFilter;
    private System.Timers.Timer? _statusCheckTimer;

    public InspectionHistoryPage(IApiClient apiClient, IAuthService authService, INotificationService notificationService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _authService = authService;
        _notificationService = notificationService;
        InspectionsCollection.ItemsSource = _inspections;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadData();
        StartStatusCheckTimer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopStatusCheckTimer();
    }

    private async Task LoadData()
    {
        try
        {
            SetLoading(true);

            // Cargar empresas para el filtro
            var roles = _authService.CurrentUserRoles;
            if (roles.Contains("Inspector"))
            {
                _companies = await _apiClient.GetMyCompaniesAsync();
                
                var companyList = new List<string> { "Todas las empresas" };
                if (_companies != null)
                {
                    companyList.AddRange(_companies.Select(c => c.Name));
                }
                CompanyFilterPicker.ItemsSource = companyList;
            }
            else
            {
                CompanyFilterPicker.IsVisible = false;
            }

            // Cargar inspecciones
            await LoadInspections();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar datos: {ex}");
            await DisplayAlertAsync("Error", "No se pudieron cargar las inspecciones.", "OK");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task LoadInspections()
    {
        try
        {
            var inspections = await _apiClient.GetMyInspectionsAsync(_selectedCompanyFilter);
            
            _inspections.Clear();
            
            foreach (var inspection in inspections)
            {
                var viewModel = new InspectionViewModel
                {
                    Id = inspection.Id,
                    CompanyName = inspection.AffiliatedCompanyName,
                    Status = GetStatusDisplay(inspection.Status),
                    StatusColor = GetStatusColor(inspection.Status),
                    StatusBackgroundColor = GetStatusBackgroundColor(inspection.Status),
                    DateRange = $"{inspection.StartedAt:dd/MM/yyyy HH:mm} - {(inspection.CompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "En proceso")}",
                    PhotosInfo = $"{inspection.AnalyzedPhotosCount} de {inspection.PhotosCount} fotos analizadas"
                };
                
                _inspections.Add(viewModel);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar inspecciones: {ex}");
            await DisplayAlertAsync("Error", "No se pudieron cargar las inspecciones.", "OK");
        }
    }

    private void OnCompanyFilterChanged(object? sender, EventArgs e)
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
            
            _ = LoadInspections();
        }
    }

    private async void OnViewDetailsClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is Guid inspectionId)
        {
            // Navegar a página de detalles con el ID
            await Shell.Current.GoToAsync($"//InspectionDetailsPage?inspectionId={inspectionId}");
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
            // Verificar inspecciones que están en proceso
            var analyzingInspections = _inspections.Where(i => i.Status == "Analizando").ToList();
            
            foreach (var inspection in analyzingInspections)
            {
                try
                {
                    var status = await _apiClient.GetAnalysisStatusAsync(inspection.Id);
                    
                    if (status.Status == "Completed")
                    {
                        // Notificar al usuario
                        await _notificationService.ShowNotificationAsync(
                            "Análisis Completado",
                            $"El análisis de la inspección para {inspection.CompanyName} ha sido completado.");
                        
                        // Recargar la lista
                        await LoadInspections();
                    }
                    else if (status.Status == "Failed")
                    {
                        await _notificationService.ShowNotificationAsync(
                            "Análisis Fallido",
                            $"El análisis de la inspección para {inspection.CompanyName} ha fallado.");
                        
                        await LoadInspections();
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

