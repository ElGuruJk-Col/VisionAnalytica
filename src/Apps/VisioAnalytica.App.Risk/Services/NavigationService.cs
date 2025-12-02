using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using VisioAnalytica.App.Risk.Pages;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Implementación del servicio de navegación.
/// Maneja toda la navegación de la aplicación usando NavigationPage en lugar de Shell.
/// </summary>
public class NavigationService : INavigationService
{
	private readonly IServiceProvider _serviceProvider;
	private NavigationPage? _currentNavigationPage;

	public NavigationService(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	}

	/// <summary>
	/// Obtiene la página inicial de la aplicación (LoginPage).
	/// </summary>
	public Page GetInitialPage()
	{
		var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
		_currentNavigationPage = new NavigationPage(loginPage);
		return _currentNavigationPage;
	}

	/// <summary>
	/// Obtiene la instancia actual de NavigationPage.
	/// </summary>
	private NavigationPage GetCurrentNavigation()
	{
		if (_currentNavigationPage != null)
			return _currentNavigationPage;

		// Intentar obtener desde la aplicación actual
		var window = Application.Current?.Windows?.FirstOrDefault();
		if (window?.Page is NavigationPage navPage)
		{
			_currentNavigationPage = navPage;
			return navPage;
		}

		// Si no hay NavigationPage, crear uno nuevo
		var initialPage = GetInitialPage();
		_currentNavigationPage = (NavigationPage)initialPage;
		return _currentNavigationPage;
	}

	/// <summary>
	/// Navega a la página de login, limpiando la pila de navegación.
	/// </summary>
	public async Task NavigateToLoginAsync()
	{
		var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
		var navPage = new NavigationPage(loginPage);
		
		// Reemplazar la página raíz de la ventana
		var window = Application.Current?.Windows?.FirstOrDefault();
		if (window != null)
		{
			window.Page = navPage;
			_currentNavigationPage = navPage;
		}
		else
		{
			await GetCurrentNavigation().Navigation.PushAsync(loginPage);
		}
	}

	/// <summary>
	/// Navega a la página principal después del login.
	/// Crea un TabbedPage con las pestañas principales.
	/// </summary>
	public async Task NavigateToMainAsync()
	{
		var mainPage = _serviceProvider.GetRequiredService<MainPage>();
		var multiCapturePage = _serviceProvider.GetRequiredService<MultiCapturePage>();
		var inspectionHistoryPage = _serviceProvider.GetRequiredService<InspectionHistoryPage>();

		// Crear TabbedPage con las pestañas principales
		var tabbedPage = new TabbedPage
		{
			Title = "VisioAnalytica Risk",
			Children =
			{
				new NavigationPage(mainPage) { Title = "Inicio", IconImageSource = "home.png" },
				new NavigationPage(multiCapturePage) { Title = "Capturar", IconImageSource = "camera.png" },
				new NavigationPage(inspectionHistoryPage) { Title = "Historial", IconImageSource = "history.png" }
			}
		};

		// Reemplazar la página raíz
		var window = Application.Current?.Windows?.FirstOrDefault();
		if (window != null)
		{
			var navPage = new NavigationPage(tabbedPage);
			window.Page = navPage;
			_currentNavigationPage = navPage;
		}
	}

	public async Task NavigateToRegisterAsync()
	{
		var page = _serviceProvider.GetRequiredService<RegisterPage>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateToForgotPasswordAsync()
	{
		var page = _serviceProvider.GetRequiredService<ForgotPasswordPage>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateToChangePasswordAsync()
	{
		var page = _serviceProvider.GetRequiredService<ChangePasswordPage>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateToResetPasswordAsync()
	{
		var page = _serviceProvider.GetRequiredService<ResetPasswordPage>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateToMultiCaptureAsync()
	{
		var page = _serviceProvider.GetRequiredService<MultiCapturePage>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateToCaptureAsync()
	{
		var page = _serviceProvider.GetRequiredService<CapturePage>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateToResultsAsync()
	{
		var page = _serviceProvider.GetRequiredService<ResultsPage>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateToInspectionHistoryAsync()
	{
		var page = _serviceProvider.GetRequiredService<InspectionHistoryPage>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateToInspectionDetailsAsync(Guid inspectionId)
	{
		var apiClient = _serviceProvider.GetRequiredService<IApiClient>();
		var authService = _serviceProvider.GetRequiredService<IAuthService>();
		var page = new InspectionDetailsPage(apiClient, authService, inspectionId);
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateToHistoryAsync()
	{
		var page = _serviceProvider.GetRequiredService<HistoryPage>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateToAdminDashboardAsync()
	{
		var page = _serviceProvider.GetRequiredService<AdminDashboardPage>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateToTeamInspectionsAsync()
	{
		var page = _serviceProvider.GetRequiredService<TeamInspectionsPage>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}

	public async Task NavigateBackAsync()
	{
		var nav = GetCurrentNavigation().Navigation;
		if (nav.NavigationStack.Count > 1)
		{
			await nav.PopAsync();
		}
	}

	public async Task NavigateToRootAsync()
	{
		var nav = GetCurrentNavigation().Navigation;
		while (nav.NavigationStack.Count > 1)
		{
			nav.RemovePage(nav.NavigationStack[nav.NavigationStack.Count - 1]);
		}
	}

	public async Task NavigateToAsync<T>(object? parameter = null) where T : Page
	{
		var page = _serviceProvider.GetRequiredService<T>();
		await GetCurrentNavigation().Navigation.PushAsync(page);
	}
}

