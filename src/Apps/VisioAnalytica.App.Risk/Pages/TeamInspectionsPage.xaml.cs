using System.Collections.ObjectModel;
using VisioAnalytica.App.Risk.Services;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Pages;

public partial class TeamInspectionsPage : ContentPage
{
    private readonly IApiClient _apiClient;
    public ObservableCollection<InspectionGroup> InspectionGroups { get; } = new();

    public TeamInspectionsPage(IApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        HistoryCollection.ItemsSource = InspectionGroups;
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
            var inspections = await _apiClient.GetOrganizationHistoryAsync();

            InspectionGroups.Clear();

            if (inspections.Count == 0)
            {
                EmptyLabel.IsVisible = true;
                return;
            }

            EmptyLabel.IsVisible = false;

            var viewModels = inspections.Select(i => new TeamInspectionViewModel
            {
                Id = i.Id,
                Date = i.AnalysisDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                RawDate = i.AnalysisDate.ToLocalTime(),
                FindingsCount = i.TotalFindings,
                CompanyName = i.AffiliatedCompanyName,
                InspectorName = i.UserName,
                Status = i.Status,
                StatusColor = GetStatusColor(i.Status)
            }).ToList();

            var grouped = viewModels
                .GroupBy(i => GetDateGroup(i.RawDate))
                .OrderBy(g => GetGroupOrder(g.Key));

            foreach (var group in grouped)
            {
                InspectionGroups.Add(new InspectionGroup(group.Key, group.Select(x => (InspectionHistoryViewModel)x).ToList()));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"No se pudo cargar el historial: {ex.Message}", "OK");
        }
    }

    private string GetDateGroup(DateTime date)
    {
        var today = DateTime.Today;
        if (date.Date == today) return "Hoy";
        if (date.Date == today.AddDays(-1)) return "Ayer";
        if (date.Date > today.AddDays(-7)) return "Esta Semana";
        if (date.Date > today.AddDays(-30)) return "Este Mes";
        return "Anteriores";
    }

    private int GetGroupOrder(string group)
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

    private Color GetStatusColor(string status)
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
        if (e.CurrentSelection.FirstOrDefault() is InspectionHistoryViewModel selected)
        {
            // Navegar a detalles
            await Shell.Current.GoToAsync($"InspectionDetailsPage?inspectionId={selected.Id}");
            
            // Deseleccionar
            ((CollectionView)sender).SelectedItem = null;
        }
    }
}

public class TeamInspectionViewModel : InspectionHistoryViewModel
{
    public string InspectorName { get; set; } = string.Empty;
}
