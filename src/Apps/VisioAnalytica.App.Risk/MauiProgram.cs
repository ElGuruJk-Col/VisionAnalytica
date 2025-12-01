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
		builder.Services.AddSingleton<INotificationService, NotificationService>();

		// Registrar páginas (LoginPage necesita IApiClient también)
		builder.Services.AddTransient<Pages.LoginPage>(sp => 
			new Pages.LoginPage(
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<IApiClient>()));
		builder.Services.AddTransient<Pages.RegisterPage>();
		// Mantener CapturePage para compatibilidad (puede ser eliminada después)
		builder.Services.AddTransient<Pages.CapturePage>(sp => 
			new Pages.CapturePage(
				sp.GetRequiredService<IAnalysisService>(),
				sp.GetRequiredService<INavigationDataService>(),
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<IAuthService>()));
		
		// Nuevas páginas con diseño moderno
		builder.Services.AddTransient<Pages.MultiCapturePage>(sp => 
			new Pages.MultiCapturePage(
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<INotificationService>()));
		builder.Services.AddTransient<Pages.InspectionHistoryPage>(sp => 
			new Pages.InspectionHistoryPage(
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<INotificationService>()));
		builder.Services.AddTransient<Pages.InspectionDetailsPage>(sp => 
			new Pages.InspectionDetailsPage(
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<IAuthService>()));
		
		builder.Services.AddTransient<Pages.ResultsPage>();
		builder.Services.AddTransient<Pages.HistoryPage>(); // Mantener para compatibilidad
		builder.Services.AddTransient<Pages.ForgotPasswordPage>();
		builder.Services.AddTransient<Pages.ChangePasswordPage>();
		builder.Services.AddTransient<Pages.ResetPasswordPage>();
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
		Routing.RegisterRoute("ResetPasswordPage", new DependencyInjectionRouteFactory<Pages.ResetPasswordPage>(serviceProvider));
		Routing.RegisterRoute("CapturePage", new DependencyInjectionRouteFactory<Pages.CapturePage>(serviceProvider));
		Routing.RegisterRoute("MultiCapturePage", new DependencyInjectionRouteFactory<Pages.MultiCapturePage>(serviceProvider));
		Routing.RegisterRoute("ResultsPage", new DependencyInjectionRouteFactory<Pages.ResultsPage>(serviceProvider));
		Routing.RegisterRoute("HistoryPage", new DependencyInjectionRouteFactory<Pages.HistoryPage>(serviceProvider));
		Routing.RegisterRoute("InspectionHistoryPage", new DependencyInjectionRouteFactory<Pages.InspectionHistoryPage>(serviceProvider));
		Routing.RegisterRoute("InspectionDetailsPage", new DependencyInjectionRouteFactory<Pages.InspectionDetailsPage>(serviceProvider));
		Routing.RegisterRoute("MainPage", new DependencyInjectionRouteFactory<MainPage>(serviceProvider));
		
		return app;
	}
	
	// Factory personalizado para resolver páginas desde DI
	private class DependencyInjectionRouteFactory<T>(IServiceProvider serviceProvider) : RouteFactory where T : Page
	{
		private readonly IServiceProvider _serviceProvider = serviceProvider;
		
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
