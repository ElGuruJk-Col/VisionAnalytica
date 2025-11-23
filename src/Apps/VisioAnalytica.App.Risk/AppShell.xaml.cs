using VisioAnalytica.App.Risk.Pages;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
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

	private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		// Ocultar TabBar y Flyout en páginas de autenticación
		var currentLocation = e.Current?.Location?.ToString() ?? "";
		var isAuthPage = currentLocation.Contains("LoginPage") || 
		                 currentLocation.Contains("RegisterPage") || 
		                 currentLocation.Contains("ForgotPasswordPage") || 
		                 currentLocation.Contains("ChangePasswordPage");
		
		FlyoutBehavior = isAuthPage ? FlyoutBehavior.Disabled : FlyoutBehavior.Flyout;
		
		// Actualizar el menú cuando navegamos
		UpdateFlyoutMenu();
	}
	
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
		// Lista de rutas de páginas de autenticación que queremos ocultar/mostrar
		var authRoutes = new[] { "LoginPage", "RegisterPage", "ForgotPasswordPage" };
		
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
}
