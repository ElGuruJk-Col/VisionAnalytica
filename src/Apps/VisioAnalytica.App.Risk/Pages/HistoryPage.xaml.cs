using System.Collections.ObjectModel;

namespace VisioAnalytica.App.Risk.Pages;

public partial class HistoryPage : ContentPage
{
    public ObservableCollection<InspectionViewModel> Inspections { get; } = new();

    public HistoryPage()
    {
        InitializeComponent();
        HistoryCollection.ItemsSource = Inspections;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadHistory();
    }

    private void LoadHistory()
    {
        // TODO: Cargar historial real desde la API
        // Por ahora, mostramos vacío
        if (Inspections.Count == 0)
        {
            EmptyLabel.IsVisible = true;
        }
    }
}

public class InspectionViewModel
{
    public string Date { get; set; } = string.Empty;
    public int FindingsCount { get; set; }
}

