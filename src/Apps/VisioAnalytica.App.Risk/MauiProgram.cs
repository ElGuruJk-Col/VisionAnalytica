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
		builder.Services.AddSingleton<INavigationService, NavigationService>();

		// Registrar páginas (LoginPage necesita IApiClient también)
		builder.Services.AddTransient<Pages.LoginPage>(sp => 
			new Pages.LoginPage(
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<INavigationService>()));
		builder.Services.AddTransient<Pages.RegisterPage>(sp =>
			new Pages.RegisterPage(
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<INavigationService>()));
		// Mantener CapturePage para compatibilidad (puede ser eliminada después)
		builder.Services.AddTransient<Pages.CapturePage>(sp => 
			new Pages.CapturePage(
				sp.GetRequiredService<IAnalysisService>(),
				sp.GetRequiredService<INavigationDataService>(),
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<INavigationService>()));
		
		// Nuevas páginas con diseño moderno
		builder.Services.AddTransient<Pages.MultiCapturePage>(sp => 
			new Pages.MultiCapturePage(
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<INotificationService>(),
				sp.GetRequiredService<INavigationService>()));
		builder.Services.AddTransient<Pages.InspectionHistoryPage>(sp => 
			new Pages.InspectionHistoryPage(
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<INotificationService>(),
				sp.GetRequiredService<INavigationService>()));
		builder.Services.AddTransient<Pages.InspectionDetailsPage>(sp => 
			new Pages.InspectionDetailsPage(
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<IAuthService>()));
		
		builder.Services.AddTransient<Pages.ResultsPage>(sp =>
			new Pages.ResultsPage(
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<INavigationDataService>(),
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<INavigationService>()));
		builder.Services.AddTransient<Pages.HistoryPage>(sp =>
			new Pages.HistoryPage(
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<INavigationService>())); // Mantener para compatibilidad
		builder.Services.AddTransient<Pages.ForgotPasswordPage>(sp =>
			new Pages.ForgotPasswordPage(
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<INavigationService>()));
		builder.Services.AddTransient<Pages.ChangePasswordPage>(sp =>
			new Pages.ChangePasswordPage(
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<INavigationService>()));
		builder.Services.AddTransient<Pages.ResetPasswordPage>(sp =>
			new Pages.ResetPasswordPage(
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<INavigationService>()));
		builder.Services.AddTransient<MainPage>(sp =>
			new MainPage(
				sp.GetRequiredService<IAuthService>(),
				sp.GetRequiredService<INavigationService>()));
		builder.Services.AddTransient<Pages.AdminDashboardPage>(sp =>
			new Pages.AdminDashboardPage(
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<INavigationService>()));
		builder.Services.AddTransient<Pages.TeamInspectionsPage>(sp =>
			new Pages.TeamInspectionsPage(
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<INavigationService>()));

		var app = builder.Build();
		
		return app;
	}
}
