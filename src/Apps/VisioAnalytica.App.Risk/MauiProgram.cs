using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Registrar servicios
		builder.Services.AddSingleton<HttpClient>();
		builder.Services.AddSingleton<IApiClient, ApiClient>();
		builder.Services.AddSingleton<IAuthService, AuthService>();
		builder.Services.AddSingleton<IAnalysisService, AnalysisService>();
		builder.Services.AddSingleton<INavigationDataService, NavigationDataService>();

		// Registrar páginas
		builder.Services.AddTransient<Pages.LoginPage>();
		builder.Services.AddTransient<Pages.RegisterPage>();
		builder.Services.AddTransient<Pages.CapturePage>();
		builder.Services.AddTransient<Pages.ResultsPage>();
		builder.Services.AddTransient<Pages.HistoryPage>();
		builder.Services.AddTransient<Pages.ForgotPasswordPage>();
		builder.Services.AddTransient<Pages.ChangePasswordPage>();
		builder.Services.AddTransient<MainPage>();

		// Registrar AppShell
		builder.Services.AddSingleton<AppShell>();

		var app = builder.Build();
		
		// Configurar el factory de rutas para usar DI
		// Esto permite que las páginas se resuelvan desde el contenedor de DI cuando se navega
		var serviceProvider = app.Services;
		
		// Crear un RouteFactory personalizado que use DI
		Routing.RegisterRoute("LoginPage", new DependencyInjectionRouteFactory<Pages.LoginPage>(serviceProvider));
		Routing.RegisterRoute("RegisterPage", new DependencyInjectionRouteFactory<Pages.RegisterPage>(serviceProvider));
		Routing.RegisterRoute("ForgotPasswordPage", new DependencyInjectionRouteFactory<Pages.ForgotPasswordPage>(serviceProvider));
		Routing.RegisterRoute("ChangePasswordPage", new DependencyInjectionRouteFactory<Pages.ChangePasswordPage>(serviceProvider));
		Routing.RegisterRoute("CapturePage", new DependencyInjectionRouteFactory<Pages.CapturePage>(serviceProvider));
		Routing.RegisterRoute("ResultsPage", new DependencyInjectionRouteFactory<Pages.ResultsPage>(serviceProvider));
		Routing.RegisterRoute("HistoryPage", new DependencyInjectionRouteFactory<Pages.HistoryPage>(serviceProvider));
		Routing.RegisterRoute("MainPage", new DependencyInjectionRouteFactory<MainPage>(serviceProvider));
		
		return app;
	}
	
	// Factory personalizado para resolver páginas desde DI
	private class DependencyInjectionRouteFactory<T> : RouteFactory where T : Page
	{
		private readonly IServiceProvider _serviceProvider;
		
		public DependencyInjectionRouteFactory(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}
		
		public override Element GetOrCreate()
		{
			return _serviceProvider.GetRequiredService<T>();
		}
		
		public override Element GetOrCreate(IServiceProvider services)
		{
			return _serviceProvider.GetRequiredService<T>();
		}
	}
}
