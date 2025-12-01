using VisioAnalytica.App.Risk.Pages;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk;

public partial class AppShell : Shell
{
	private readonly IServiceProvider? _serviceProvider;
	private IAuthService? _authService;

	public AppShell() : this(null)
	{
	}

	public AppShell(IServiceProvider? serviceProvider)
	{
		_serviceProvider = serviceProvider;
		InitializeComponent();
		
		// Si tenemos serviceProvider, configurar las páginas para usar DI
		if (_serviceProvider != null)
		{
			ConfigurePagesWithDI();
			_authService = _serviceProvider.GetService<IAuthService>();
		}
		
		// Ocultar TabBar y Flyout cuando estemos en páginas de autenticación
		Navigating += OnNavigating;
		Navigated += OnNavigated;
		
		// Asegurar que la página de Login sea la primera en mostrarse
		Loaded += AppShell_Loaded;
		
		// Actualizar el menú cuando cambie el estado de autenticación
		UpdateFlyoutMenu();
	}
	
	private void ConfigurePagesWithDI()
	{
		// Configurar LoginPage para usar DI
		// Navegar por la jerarquía: Shell -> ShellItem -> ShellSection -> ShellContent
		foreach (var item in Items)
		{
			if (item is ShellItem shellItem)
			{
				foreach (var section in shellItem.Items)
				{
					if (section is ShellSection shellSection)
					{
						foreach (var content in shellSection.Items)
						{
							if (content is ShellContent shellContent && shellContent.Route == "LoginPage")
							{
								shellContent.ContentTemplate = new DataTemplate(() => _serviceProvider!.GetRequiredService<Pages.LoginPage>());
								return; // Solo necesitamos configurar LoginPage aquí
							}
						}
					}
				}
			}
		}
		
		// Las otras páginas se resuelven cuando se navega a ellas usando las rutas registradas en MauiProgram
	}

	private async void AppShell_Loaded(object? sender, EventArgs e)
	{
		// Navegar a la página de Login al inicio
		// Usar /// para ruta absoluta en Shell
		try
		{
			await Shell.Current.GoToAsync("//LoginPage");
		}
		catch (Exception)
		{
			// Si falla la navegación, la primera ShellContent (LoginPage) se mostrará automáticamente
		}
	}

	private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
	{
		// Capturar la ruta anterior ANTES de navegar a ChangePasswordPage
		var targetLocation = e.Target?.Location?.ToString() ?? "";
		if (targetLocation.Contains("ChangePasswordPage"))
		{
			// Obtener la ruta actual (antes de navegar)
			var currentLocation = Shell.Current?.CurrentState?.Location?.ToString() ?? "";
			if (!string.IsNullOrEmpty(currentLocation) && !currentLocation.Contains("ChangePasswordPage"))
			{
				// Normalizar la ruta: si es MainPage (primera pestaña del TabBar), usar //MainPage
				// El problema es que cuando estás en MainPage, la ruta puede ser "//MainPage" o el TabBar completo
				// Verificamos si la ruta actual contiene MainPage específicamente
				if (currentLocation.Contains("MainPage") && !currentLocation.Contains("HistoryPage") && !currentLocation.Contains("CapturePage"))
				{
					PreviousRoute = "//MainPage";
				}
				else if (currentLocation.Contains("InspectionHistoryPage"))
				{
					PreviousRoute = "//InspectionHistoryPage";
				}
				else if (currentLocation.Contains("HistoryPage"))
				{
					PreviousRoute = "//HistoryPage";
				}
				else if (currentLocation.Contains("MultiCapturePage"))
				{
					PreviousRoute = "//MultiCapturePage";
				}
				else if (currentLocation.Contains("CapturePage"))
				{
					PreviousRoute = "//CapturePage";
				}
				else if (currentLocation.Contains("MainPage"))
				{
					// Si contiene MainPage pero también otras páginas, verificar si MainPage es la primera
					// En MAUI Shell TabBar, la primera pestaña (MainPage) puede no tener ruta explícita
					// Si la ruta termina con MainPage o empieza con //MainPage, usar MainPage
					if (currentLocation.EndsWith("MainPage") || currentLocation.StartsWith("//MainPage"))
					{
						PreviousRoute = "//MainPage";
					}
					else
					{
						PreviousRoute = currentLocation;
					}
				}
				else
				{
					PreviousRoute = currentLocation;
				}
			}
		}
	}

	private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		// Ocultar TabBar y Flyout en páginas de autenticación
		var currentLocation = e.Current?.Location?.ToString() ?? "";
		var isAuthPage = currentLocation.Contains("LoginPage") || 
		                 currentLocation.Contains("RegisterPage") || 
		                 currentLocation.Contains("ForgotPasswordPage") || 
		                 currentLocation.Contains("ChangePasswordPage");
		
		FlyoutBehavior = isAuthPage ? FlyoutBehavior.Disabled : FlyoutBehavior.Flyout;
		
		// Actualizar el menú cuando navegamos (con un pequeño delay para asegurar que los servicios estén disponibles)
		_ = Task.Run(async () =>
		{
			await Task.Delay(50);
			MainThread.BeginInvokeOnMainThread(() => UpdateFlyoutMenu());
		});
	}
	
	/// <summary>
	/// Ruta anterior guardada para navegación de regreso.
	/// </summary>
	public static string? PreviousRoute { get; private set; }
	
	/// <summary>
	/// Actualiza el menú del Flyout según el estado de autenticación y roles.
	/// </summary>
	public void UpdateFlyoutMenu()
	{
		if (_authService == null)
		{
			// Si no tenemos acceso al servicio, intentar obtenerlo
			_authService = _serviceProvider?.GetService<IAuthService>();
		}
		
		var isAuthenticated = _authService?.IsAuthenticated ?? false;
		
		// Mostrar/ocultar opciones según el estado de autenticación
		if (UnauthenticatedMenu != null && AuthenticatedMenu != null)
		{
			UnauthenticatedMenu.IsVisible = !isAuthenticated;
			AuthenticatedMenu.IsVisible = isAuthenticated;
			
			// Actualizar el email del usuario si está autenticado
			if (isAuthenticated && UserEmailLabel != null && _authService != null)
			{
				UserEmailLabel.Text = _authService.CurrentUserEmail ?? "Usuario";
			}
			
			// Mostrar/ocultar opciones según roles
			if (isAuthenticated && _authService != null)
			{
				var roles = _authService.CurrentUserRoles;
				
				// Cambiar contraseña está disponible para todos los usuarios autenticados
				if (ChangePasswordMenuButton != null)
				{
					ChangePasswordMenuButton.IsVisible = true;
				}
				
				// Opciones de administración solo para Admin y SuperAdmin
				if (AdminMenu != null)
				{
					var isAdmin = roles.Contains("Admin") || roles.Contains("SuperAdmin");
					AdminMenu.IsVisible = isAdmin;
                    
                    // Mostrar/Ocultar páginas de Admin/Supervisor
                    var isSupervisor = roles.Contains("Supervisor") || isAdmin;
                    
                    UpdateShellContentVisibility("AdminDashboardPage", isAdmin);
                    UpdateShellContentVisibility("TeamInspectionsPage", isSupervisor);
				}
				
				// Opciones de desarrollo solo para SuperAdmin
				if (DevMenu != null)
				{
					var isSuperAdmin = roles.Contains("SuperAdmin");
					DevMenu.IsVisible = isSuperAdmin;
				}
			}
		}
		
		// Ocultar páginas de autenticación del Flyout cuando el usuario esté logueado
		// Usamos búsqueda por ruta para evitar problemas de conversión de tipos
		UpdateAuthPagesVisibility(!isAuthenticated);
	}
	
	/// <summary>
	/// Oculta o muestra las páginas de autenticación del Flyout según el estado.
	/// Usa búsqueda por ruta para evitar problemas de conversión de tipos.
	/// </summary>
	private void UpdateAuthPagesVisibility(bool show)
	{
		// Lista de rutas de páginas de autenticación que queremos ocultar/mostrar según estado
		var authRoutes = new[] { "LoginPage", "ForgotPasswordPage" };
		
		// RegisterPage siempre oculta (solo para uso futuro desde web app)
		var alwaysHiddenRoutes = new[] { "RegisterPage" };
		
		// Buscar y actualizar cada página de autenticación
		foreach (var route in authRoutes)
		{
			try
			{
				// Buscar el ShellContent por su ruta usando el método Items
				// Iteramos sobre todos los elementos del Shell
				foreach (var element in Items)
				{
					// Buscar en ShellItems (como TabBar)
					if (element is ShellItem shellItem)
					{
						UpdateShellContentInItem(shellItem, route, show);
					}
					// Buscar ShellContent directos usando búsqueda por tipo base
					else
					{
						// Usar reflexión o búsqueda recursiva para encontrar ShellContent
						UpdateShellContentRecursive(element, route, show);
					}
				}
			}
			catch
			{
				// Si hay algún error al buscar la página, simplemente continuar
				// Esto evita que un error en una página afecte a las demás
			}
		}
		
		// Ocultar RegisterPage siempre
		foreach (var route in alwaysHiddenRoutes)
		{
			try
			{
				foreach (var element in Items)
				{
					if (element is ShellItem shellItem)
					{
						UpdateShellContentInItem(shellItem, route, false);
					}
					else
					{
						UpdateShellContentRecursive(element, route, false);
					}
				}
			}
			catch
			{
				// Continuar si hay error
			}
		}
	}
	
	/// <summary>
	/// Actualiza la visibilidad de un ShellContent dentro de un ShellItem.
	/// </summary>
	private void UpdateShellContentInItem(ShellItem shellItem, string route, bool show)
	{
		foreach (var section in shellItem.Items)
		{
			if (section is ShellSection shellSection)
			{
				foreach (var content in shellSection.Items)
				{
					if (content is ShellContent shellContent && shellContent.Route == route)
					{
						shellContent.FlyoutItemIsVisible = show;
					}
				}
			}
		}
	}
	
	/// <summary>
	/// Busca recursivamente un ShellContent por su ruta en cualquier elemento del Shell.
	/// </summary>
	private void UpdateShellContentRecursive(Element element, string route, bool show)
	{
		// Si el elemento es un ShellContent y tiene la ruta que buscamos
		if (element is ShellContent shellContent && shellContent.Route == route)
		{
			shellContent.FlyoutItemIsVisible = show;
			return;
		}
		
		// Si el elemento tiene hijos, buscar recursivamente
		if (element is ShellSection shellSection)
		{
			foreach (var child in shellSection.Items)
			{
				UpdateShellContentRecursive(child, route, show);
			}
		}
		else if (element is ShellItem shellItem)
		{
			foreach (var child in shellItem.Items)
			{
				UpdateShellContentRecursive(child, route, show);
			}
		}
	}

    /// <summary>
    /// Actualiza la visibilidad de un ShellContent por su ruta.
    /// </summary>
    private void UpdateShellContentVisibility(string route, bool isVisible)
    {
        try
        {
            foreach (var element in Items)
            {
                if (element is ShellItem shellItem)
                {
                    UpdateShellContentInItem(shellItem, route, isVisible);
                }
                else
                {
                    UpdateShellContentRecursive(element, route, isVisible);
                }
            }
        }
        catch
        {
            // Ignorar errores
        }
    }
	
	// Handlers para los botones del menú
	private async void OnLoginMenuClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//LoginPage");
		FlyoutIsPresented = false;
	}
	
	private async void OnRegisterMenuClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//RegisterPage");
		FlyoutIsPresented = false;
	}
	
	private async void OnForgotPasswordMenuClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//ForgotPasswordPage");
		FlyoutIsPresented = false;
	}
	
	private async void OnChangePasswordMenuClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync("//ChangePasswordPage");
		FlyoutIsPresented = false;
	}
	
	private async void OnLogoutMenuClicked(object? sender, EventArgs e)
	{
		var confirm = await DisplayAlertAsync("Cerrar Sesión", "¿Estás seguro de que deseas cerrar sesión?", "Sí", "No");
		if (confirm && _authService != null)
		{
			await _authService.LogoutAsync();
			UpdateFlyoutMenu();
			await Shell.Current.GoToAsync("//LoginPage");
			FlyoutIsPresented = false;
		}
	}
	
	/// <summary>
	/// Handler para el botón de análisis simple (desarrollo) - Solo visible para SuperAdmin.
	/// Navega entre CapturePage (análisis simple) y MultiCapturePage (análisis completo).
	/// </summary>
	private async void OnDevCaptureMenuClicked(object? sender, EventArgs e)
	{
		try
		{
			FlyoutIsPresented = false;
			
			// Obtener la ruta actual
			var currentLocation = Shell.Current.CurrentState?.Location?.ToString() ?? "";
			System.Diagnostics.Debug.WriteLine($"📍 Ubicación actual: {currentLocation}");
			
			// Si estamos en CapturePage, navegar a MultiCapturePage
			// Si estamos en cualquier otra página, navegar a CapturePage
			if (currentLocation.Contains("CapturePage") && !currentLocation.Contains("MultiCapturePage"))
			{
				System.Diagnostics.Debug.WriteLine("🔄 Navegando de CapturePage a MultiCapturePage");
				await Shell.Current.GoToAsync("//MultiCapturePage");
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("🔄 Navegando a CapturePage");
				await Shell.Current.GoToAsync("//CapturePage");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"❌ Error al navegar: {ex}");
			System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
			await DisplayAlertAsync("Error", "No se pudo navegar a la página solicitada.", "OK");
		}
	}
}
