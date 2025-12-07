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

		// Registrar servicios base primero
		// Configurar HttpClient con handler específico para Android si es necesario
		builder.Services.AddSingleton<HttpClient>(sp =>
		{
			var httpClient = new HttpClient();
#if ANDROID
			// Configuración específica para Android
			httpClient.Timeout = TimeSpan.FromSeconds(30); // Timeout más corto para detectar problemas más rápido
#endif
			return httpClient;
		});
		builder.Services.AddSingleton<INavigationService, NavigationService>();
		builder.Services.AddSingleton<INavigationDataService, NavigationDataService>();
		builder.Services.AddSingleton<INotificationService, NotificationService>();
		
		// Registrar ApiClient primero usando factory para IAuthService (rompe dependencia circular)
		// Esto permite que ApiClient se cree sin esperar a que AuthService esté completamente inicializado
		builder.Services.AddSingleton<IApiClient>(sp => 
		{
			var httpClient = sp.GetRequiredService<HttpClient>();
			var navService = sp.GetRequiredService<INavigationService>();
			// Usar factory function para resolver IAuthService de forma diferida
			// Esto evita la dependencia circular durante la inicialización
			return new ApiClient(
				httpClient,
				() => sp.GetService<IAuthService>(), // Factory function para resolver IAuthService lazy
				navService);
		});
		
		// Registrar AuthService después de ApiClient (necesita IApiClient)
		builder.Services.AddSingleton<IAuthService>(sp => 
		{
			var apiClient = sp.GetRequiredService<IApiClient>();
			return new AuthService(apiClient);
		});
		
		builder.Services.AddSingleton<IAnalysisService, AnalysisService>();

		// Registrar páginas usando DI automático (más eficiente que factory functions explícitas)
		// DI resolverá automáticamente las dependencias desde los constructores
		builder.Services.AddTransient<Pages.LoginPage>();
		builder.Services.AddTransient<Pages.RegisterPage>();
		builder.Services.AddTransient<Pages.CapturePage>(); // Mantener para compatibilidad
		builder.Services.AddTransient<Pages.MultiCapturePage>();
		builder.Services.AddTransient<Pages.InspectionHistoryPage>();
		builder.Services.AddTransient<Pages.InspectionDetailsPage>();
		builder.Services.AddTransient<Pages.ResultsPage>();
		builder.Services.AddTransient<Pages.HistoryPage>(); // Mantener para compatibilidad
		builder.Services.AddTransient<Pages.ForgotPasswordPage>();
		builder.Services.AddTransient<Pages.ChangePasswordPage>();
		builder.Services.AddTransient<Pages.ResetPasswordPage>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<Pages.AdminDashboardPage>();
		builder.Services.AddTransient<Pages.TeamInspectionsPage>();

		var app = builder.Build();
		
		return app;
	}
}
