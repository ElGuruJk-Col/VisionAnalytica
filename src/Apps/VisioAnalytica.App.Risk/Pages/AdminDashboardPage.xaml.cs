using VisioAnalytica.App.Risk.Services;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Pages;

public partial class AdminDashboardPage : ContentPage
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService? _navigationService;

    public AdminDashboardPage(IApiClient apiClient, INavigationService? navigationService = null)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _navigationService = navigationService;
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDashboardData();
    }

    private async Task LoadDashboardData()
    {
        try
        {
            // Obtener historial de inspecciones para el dashboard
            var history = await _apiClient.GetOrganizationHistoryAsync();

            if (history != null)
            {
                TotalInspectionsLabel.Text = history.Count.ToString();
                // Simulación de hallazgos críticos (no viene en summary dto por defecto, habría que agregarlo)
                CriticalFindingsLabel.Text = history.Sum(h => h.TotalFindings).ToString(); 

                RecentActivityCollection.ItemsSource = history.Take(5).Select(h => new 
                {
                    CompanyName = h.AffiliatedCompanyName,
                    InspectorName = h.UserName,
                    Date = h.AnalysisDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    Status = h.Status
                }).ToList();
            }
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Error", "No se pudo cargar el dashboard.", "OK");
        }
    }

    private async void OnViewHistoryClicked(object sender, EventArgs e)
    {
        await GetNavigationService().NavigateToTeamInspectionsAsync();
    }
}
