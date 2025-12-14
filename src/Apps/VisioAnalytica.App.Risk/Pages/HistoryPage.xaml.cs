using System.Collections.ObjectModel;
using VisioAnalytica.App.Risk.Services;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Pages;

public partial class HistoryPage : ContentPage
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService? _navigationService;
    public ObservableCollection<InspectionGroup> InspectionGroups { get; } = [];

    public HistoryPage(IApiClient apiClient, INavigationService? navigationService = null)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _navigationService = navigationService;
        HistoryCollection.ItemsSource = InspectionGroups;
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
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var inspections = await _apiClient.GetMyInspectionsAsync();

            InspectionGroups.Clear();

            if (inspections.Count == 0)
            {
                EmptyLabel.IsVisible = true;
                return;
            }

            EmptyLabel.IsVisible = false;

            var viewModels = inspections.Select(i => new InspectionHistoryViewModel
            {
                Id = i.Id,
                Date = i.StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                RawDate = i.StartedAt.ToLocalTime(),
                FindingsCount = i.FindingsCount,
                CompanyName = i.AffiliatedCompanyName,
                Status = i.Status,
                StatusColor = GetStatusColor(i.Status)
            }).ToList();

            var grouped = viewModels
                .GroupBy(i => GetDateGroup(i.RawDate))
                .OrderBy(g => GetGroupOrder(g.Key));

            foreach (var group in grouped)
            {
                InspectionGroups.Add(new InspectionGroup(group.Key, [.. group]));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudo cargar el historial: {ex.Message}", "OK");
        }
    }

    private static string GetDateGroup(DateTime date)
    {
        var today = DateTime.Today;
        if (date.Date == today) return "Hoy";
        if (date.Date == today.AddDays(-1)) return "Ayer";
        if (date.Date > today.AddDays(-7)) return "Esta Semana";
        if (date.Date > today.AddDays(-30)) return "Este Mes";
        return "Anteriores";
    }

    private static int GetGroupOrder(string group)
    {
        return group switch
        {
            "Hoy" => 0,
            "Ayer" => 1,
            "Esta Semana" => 2,
            "Este Mes" => 3,
            _ => 4
        };
    }

    private static Color GetStatusColor(string status)
    {
        return status switch
        {
            "Completed" => Colors.Green,
            "Failed" => Colors.Red,
            "Analyzing" => Colors.Orange,
            _ => Colors.Gray
        };
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is InspectionHistoryViewModel selected)
        {
            // Navegar a detalles
            await GetNavigationService().NavigateToInspectionDetailsAsync(selected.Id);
            
            // Deseleccionar
            ((CollectionView)sender).SelectedItem = null;
        }
    }
}

public class InspectionHistoryViewModel
{
    public Guid Id { get; set; }
    public string Date { get; set; } = string.Empty;
    public DateTime RawDate { get; set; }
    public int FindingsCount { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
}

public partial class InspectionGroup(string name, List<InspectionHistoryViewModel> inspections) : List<InspectionHistoryViewModel>(inspections)
{
    public string Name { get; private set; } = name;
}

