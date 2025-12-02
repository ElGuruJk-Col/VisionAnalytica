using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk;

public partial class MainPage : ContentPage
{
	private readonly IAuthService _authService;
	private INavigationService? _navigationService;

	public MainPage(IAuthService authService, INavigationService? navigationService = null)
	{
		InitializeComponent();
		_authService = authService;
		_navigationService = navigationService;
	}
	
	private INavigationService GetNavigationService()
	{
		if (_navigationService != null)
			return _navigationService;

		var serviceProvider = Handler?.MauiContext?.Services;
		if (serviceProvider != null)
		{
			_navigationService = serviceProvider.GetRequiredService<INavigationService>();
			return _navigationService;
		}

		throw new InvalidOperationException("INavigationService no está disponible.");
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		
		// Verificar autenticación
		if (!_authService.IsAuthenticated)
		{
			// Si no está autenticado, redirigir a login
			await GetNavigationService().NavigateToLoginAsync();
		}
	}

	private async void OnCaptureClicked(object? sender, EventArgs e)
	{
		await GetNavigationService().NavigateToMultiCaptureAsync();
	}

	private async void OnHistoryClicked(object? sender, EventArgs e)
	{
		await GetNavigationService().NavigateToInspectionHistoryAsync();
	}

	private async void OnLogoutClicked(object? sender, EventArgs e)
	{
		var confirm = await DisplayAlertAsync("Cerrar Sesión", "¿Estás seguro de que deseas cerrar sesión?", "Sí", "No");
		if (confirm)
		{
			await _authService.LogoutAsync();
			await GetNavigationService().NavigateToLoginAsync();
		}
	}
}
