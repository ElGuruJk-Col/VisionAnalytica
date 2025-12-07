using Microsoft.Extensions.DependencyInjection;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk;

public partial class App : Application
{
	private TokenVerificationService? _tokenVerificationService;

	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		try
		{
			// Obtener ServiceProvider - priorizar activationState (más confiable)
			IServiceProvider? serviceProvider = activationState?.Context?.Services;
			
			// Fallback: intentar desde Handler solo si activationState falla
			if (serviceProvider == null && Handler?.MauiContext?.Services != null)
			{
				serviceProvider = Handler.MauiContext.Services;
			}
			
			// Si no tenemos serviceProvider, crear página de error simple
			if (serviceProvider == null)
			{
				System.Diagnostics.Debug.WriteLine("⚠️ No se pudo obtener ServiceProvider. Creando página de error.");
				var errorLabel = new Label 
				{ 
					Text = "Error al inicializar la aplicación.\nPor favor, reinicia la app.",
					HorizontalOptions = LayoutOptions.Center,
					VerticalOptions = LayoutOptions.Center,
					HorizontalTextAlignment = TextAlignment.Center
				};
				var errorPage = new ContentPage { Content = errorLabel };
				return new Window(new NavigationPage(errorPage));
			}
			
			// Obtener NavigationService y crear página INMEDIATAMENTE (sin esperar nada)
			var navigationService = serviceProvider.GetRequiredService<INavigationService>();
			var initialPage = navigationService.GetInitialPage();
			var window = new Window(initialPage);
			
#if WINDOWS
			window.Title = "VisioAnalytica Risk";
			window.MinimumWidth = 800;
			window.MinimumHeight = 600;
			window.Width = 1000;
			window.Height = 700;
#endif
			
			// Inicializar servicios en background DESPUÉS de crear la ventana
			// Esto asegura que la UI se muestre primero
			_ = Task.Run(async () =>
			{
				try
				{
					// Esperar 1 segundo para que la UI se muestre completamente
					await Task.Delay(1000);
					
					var authService = serviceProvider.GetRequiredService<IAuthService>();
					_tokenVerificationService = new TokenVerificationService(authService, navigationService);
					_tokenVerificationService.StartVerification();
					
					System.Diagnostics.Debug.WriteLine("✅ TokenVerificationService iniciado correctamente.");
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"⚠️ Error al inicializar servicios en background: {ex}");
				}
			});
			
			return window;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"❌ ERROR CRÍTICO en CreateWindow: {ex}");
			// Crear página de error simple para que la app al menos se muestre
			var errorLabel = new Label 
			{ 
				Text = $"Error: {ex.Message}\n\nPor favor, reinicia la aplicación.",
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center,
				HorizontalTextAlignment = TextAlignment.Center,
				Margin = new Thickness(20)
			};
			var errorPage = new ContentPage { Content = errorLabel };
			return new Window(new NavigationPage(errorPage));
		}
	}

	protected override void OnSleep()
	{
		base.OnSleep();
		// Pausar verificación cuando la app está en segundo plano
		_tokenVerificationService?.StopVerification();
	}

	protected override void OnResume()
	{
		base.OnResume();
		// Reanudar verificación cuando la app vuelve al primer plano
		_tokenVerificationService?.StartVerification();
	}
}