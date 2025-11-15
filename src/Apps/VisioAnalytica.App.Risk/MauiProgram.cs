using Microsoft.Extensions.Logging;
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
		builder.Services.AddTransient<MainPage>();

		return builder.Build();
	}
}
