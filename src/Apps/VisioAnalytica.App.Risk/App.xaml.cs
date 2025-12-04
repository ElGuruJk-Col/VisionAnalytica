using Microsoft.Extensions.DependencyInjection;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Obtener NavigationService desde el contenedor de DI
		IServiceProvider? serviceProvider = null;
		
		// Método 1: Desde Handler.MauiContext (disponible después de que se construye la app)
		if (Handler?.MauiContext?.Services != null)
		{
			serviceProvider = Handler.MauiContext.Services;
		}
		
		// Método 2: Desde activationState si está disponible
		if (serviceProvider == null && activationState?.Context?.Services != null)
		{
			serviceProvider = activationState.Context.Services;
		}
		
		// Si no tenemos serviceProvider, lanzar excepción
		if (serviceProvider == null)
		{
			throw new InvalidOperationException("No se pudo obtener el ServiceProvider. La aplicación no se ha inicializado correctamente.");
		}
		
		// Obtener NavigationService y crear la página inicial
		var navigationService = serviceProvider.GetRequiredService<INavigationService>();
		var initialPage = navigationService.GetInitialPage();
		
		var window = new Window(initialPage);
		
#if WINDOWS
		window.Title = "VisioAnalytica Risk";
		// Asegurar que la ventana sea visible y tenga un tamaño adecuado
		window.MinimumWidth = 800;
		window.MinimumHeight = 600;
		window.Width = 1000;
		window.Height = 700;
#endif
		
		return window;
	}
}