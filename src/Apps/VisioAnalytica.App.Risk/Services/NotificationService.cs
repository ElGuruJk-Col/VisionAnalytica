using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Servicio de notificaciones locales para MAUI.
/// </summary>
public class NotificationService : INotificationService
{
    public async Task ShowNotificationAsync(string title, string message)
    {
        try
        {
            // Mostrar notificación en el hilo principal
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // Usar la nueva API de MAUI para obtener la página actual
                    var window = Application.Current?.Windows?.FirstOrDefault();
                    var page = window?.Page;
                    
                    if (page != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ NOTIFICACIÓN: {title} - {message}");
                        
                        // Para notificaciones importantes, mostrar un alert breve
                        // (solo para desarrollo, en producción se puede usar un Snackbar real)
                        if (title.Contains("Completado") || title.Contains("Fallido"))
                        {
                            await page.DisplayAlertAsync(title, message, "OK");
                        }
                    }
                    else
                    {
                        // Fallback: mostrar en consola si no hay página disponible
                        System.Diagnostics.Debug.WriteLine($"NOTIFICACIÓN: {title} - {message}");
                    }
                }
                catch (Exception ex)
                {
                    // Si hay error, usar Debug como fallback
                    System.Diagnostics.Debug.WriteLine($"NOTIFICACIÓN (fallback): {title} - {message}");
                    System.Diagnostics.Debug.WriteLine($"Error al mostrar notificación: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al mostrar notificación: {ex.Message}");
        }
    }

    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
            // Solicitar permisos de notificaciones según la plataforma
            // Por ahora, retornamos true ya que las alertas no requieren permisos especiales
            // En producción, esto debería solicitar permisos reales de notificaciones
            
            return await Task.FromResult(true);
        }
        catch
        {
            return false;
        }
    }
}

