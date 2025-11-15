using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk;

public partial class MainPage : ContentPage
{
	private readonly IAuthService _authService;

	public MainPage(IAuthService authService)
	{
		InitializeComponent();
		_authService = authService;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		
		// Verificar autenticación
		if (!_authService.IsAuthenticated)
		{
			// Si no está autenticado, redirigir a login
			Shell.Current.GoToAsync("//LoginPage");
		}
	}

	private async void OnCaptureClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//CapturePage");
	}

	private async void OnHistoryClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//HistoryPage");
	}

	private async void OnLogoutClicked(object? sender, EventArgs e)
	{
		var confirm = await DisplayAlert("Cerrar Sesión", "¿Estás seguro de que deseas cerrar sesión?", "Sí", "No");
		if (confirm)
		{
			await _authService.LogoutAsync();
			await Shell.Current.GoToAsync("//LoginPage");
		}
	}
}
