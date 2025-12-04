using Microsoft.Maui.Controls;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Servicio centralizado para manejar la navegación en la aplicación.
/// Reemplaza el uso de Shell.Current.GoToAsync() con métodos tipados y más control.
/// </summary>
public interface INavigationService
{
	/// <summary>
	/// Obtiene la página inicial de la aplicación (LoginPage).
	/// </summary>
	Page GetInitialPage();

	/// <summary>
	/// Navega a la página de login.
	/// </summary>
	Task NavigateToLoginAsync();

	/// <summary>
	/// Navega a la página principal (MainPage) después del login.
	/// </summary>
	Task NavigateToMainAsync();

	/// <summary>
	/// Navega a la página de registro.
	/// </summary>
	Task NavigateToRegisterAsync();

	/// <summary>
	/// Navega a la página de recuperación de contraseña.
	/// </summary>
	Task NavigateToForgotPasswordAsync();

	/// <summary>
	/// Navega a la página de cambio de contraseña.
	/// </summary>
	Task NavigateToChangePasswordAsync();

	/// <summary>
	/// Navega a la página de restablecimiento de contraseña.
	/// </summary>
	Task NavigateToResetPasswordAsync();

	/// <summary>
	/// Navega a la página de captura múltiple.
	/// </summary>
	Task NavigateToMultiCaptureAsync();

	/// <summary>
	/// Navega a la página de captura simple (dev).
	/// </summary>
	Task NavigateToCaptureAsync();

	/// <summary>
	/// Navega a la página de resultados.
	/// </summary>
	Task NavigateToResultsAsync();

	/// <summary>
	/// Navega a la página de historial de inspecciones.
	/// </summary>
	Task NavigateToInspectionHistoryAsync();

	/// <summary>
	/// Navega a la página de detalles de inspección.
	/// </summary>
	Task NavigateToInspectionDetailsAsync(Guid inspectionId);

	/// <summary>
	/// Navega a la página de historial (legacy).
	/// </summary>
	Task NavigateToHistoryAsync();

	/// <summary>
	/// Navega a la página de dashboard de administración.
	/// </summary>
	Task NavigateToAdminDashboardAsync();

	/// <summary>
	/// Navega a la página de inspecciones del equipo.
	/// </summary>
	Task NavigateToTeamInspectionsAsync();

	/// <summary>
	/// Cambia a la tab de Historial en el TabbedPage y refresca los datos.
	/// </summary>
	Task NavigateToHistoryTabAsync();

	/// <summary>
	/// Navega hacia atrás en la pila de navegación.
	/// </summary>
	Task NavigateBackAsync();

	/// <summary>
	/// Navega a la raíz de la pila de navegación.
	/// </summary>
	Task NavigateToRootAsync();

	/// <summary>
	/// Navega a una página genérica.
	/// </summary>
	Task NavigateToAsync<T>(object? parameter = null) where T : Page;
}

