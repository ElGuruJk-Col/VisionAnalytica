using Microsoft.Extensions.DependencyInjection;

namespace VisioAnalytica.App.Risk;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Obtener AppShell desde el contenedor de DI
		// Intentar obtener desde diferentes fuentes
		AppShell? shell = null;
		IServiceProvider? serviceProvider = null;
		
		// Método 1: Desde Handler.MauiContext (disponible después de que se construye la app)
		if (Handler?.MauiContext?.Services != null)
		{
			serviceProvider = Handler.MauiContext.Services;
			shell = serviceProvider.GetService<AppShell>();
		}
		
		// Método 2: Desde activationState si está disponible
		if (shell == null && activationState?.Context?.Services != null)
		{
			serviceProvider = activationState.Context.Services;
			shell = serviceProvider.GetService<AppShell>();
		}
		
		// Método 3: Crear nueva instancia (fallback)
		// Si no podemos obtener desde DI, crear sin serviceProvider
		// Las páginas fallarán al crearse desde ContentTemplate, pero las rutas registradas funcionarán
		shell ??= new AppShell(serviceProvider);
		
		var window = new Window(shell);
		
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