namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Interfaz para el servicio de notificaciones locales.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Muestra una notificación local.
    /// </summary>
    Task ShowNotificationAsync(string title, string message);

    /// <summary>
    /// Solicita permisos para mostrar notificaciones.
    /// </summary>
    Task<bool> RequestPermissionAsync();
}

