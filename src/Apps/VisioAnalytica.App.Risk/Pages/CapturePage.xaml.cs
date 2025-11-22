using System.Runtime.Versioning;
using Microsoft.Maui.ApplicationModel;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk.Pages;

[SupportedOSPlatform("android")]
[SupportedOSPlatform("ios")]
[SupportedOSPlatform("maccatalyst")]
[SupportedOSPlatform("windows")]
public partial class CapturePage : ContentPage
{
    private readonly IAnalysisService _analysisService;
    private readonly INavigationDataService _navigationDataService;
    private byte[]? _capturedImageBytes;

    public CapturePage(IAnalysisService analysisService, INavigationDataService navigationDataService)
    {
        InitializeComponent();
        _analysisService = analysisService;
        _navigationDataService = navigationDataService;
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("maccatalyst")]
    [SupportedOSPlatform("windows")]
    private async void OnCaptureClicked(object? sender, EventArgs e)
    {
        try
        {
            // Verificar permisos de cámara
            var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (cameraStatus != PermissionStatus.Granted)
            {
                cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    await DisplayAlertAsync(
                        "Permisos Requeridos", 
                        "Se requiere permiso de cámara para tomar fotos. Por favor, habilita el permiso en Configuración > Apps > VisioAnalytica Risk > Permisos.", 
                        "OK");
                    return;
                }
            }

            // Verificar permisos de almacenamiento específicos de Android
#if ANDROID
#pragma warning disable CA1416 // Call site is reachable on all platforms. 'CapturePage.RequestAndroidStoragePermissionsAsync()' is only supported on 'android'.
            var storagePermissionGranted = await RequestAndroidStoragePermissionsAsync();
#pragma warning restore CA1416
            if (!storagePermissionGranted)
            {
                return;
            }
#endif

            // Tomar foto
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                // Leer bytes de la imagen
                using var stream = await photo.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                _capturedImageBytes = memoryStream.ToArray();

                // Mostrar preview - Usar el FullPath si está disponible, sino crear un stream
                if (!string.IsNullOrEmpty(photo.FullPath))
                {
                    CapturedImage.Source = ImageSource.FromFile(photo.FullPath);
                }
                else
                {
                    // Si no hay FullPath, crear un stream desde los bytes
                    var imageBytes = _capturedImageBytes.ToArray(); // Crear copia para el stream
                    CapturedImage.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                }
                
                CapturedImage.IsVisible = true;
                PlaceholderLabel.IsVisible = false;
                AnalyzeButton.IsEnabled = true;

                StatusLabel.Text = "Foto capturada. Presiona 'Analizar Imagen' para continuar.";
                StatusLabel.TextColor = Colors.Green;
                StatusLabel.IsVisible = true;
            }
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlertAsync(
                "Cámara no disponible", 
                "La cámara no está disponible en este dispositivo o no está configurada correctamente. " +
                "Por favor, verifica que tengas una cámara conectada y que los permisos estén habilitados.", 
                "OK");
        }
        catch (PermissionException)
        {
            await DisplayAlertAsync(
                "Permisos de cámara", 
                "No se han otorgado los permisos de cámara. Por favor, habilita el permiso de cámara en la configuración de Windows.", 
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Error al capturar foto: {ex.Message}", "OK");
        }
    }

    private async void OnAnalyzeClicked(object? sender, EventArgs e)
    {
        if (_capturedImageBytes == null || _capturedImageBytes.Length == 0)
        {
            await DisplayAlertAsync("Error", "No hay imagen para analizar", "OK");
            return;
        }

        try
        {
            SetLoading(true);
            StatusLabel.IsVisible = false;

            // Realizar análisis
            var result = await _analysisService.AnalyzeImageAsync(_capturedImageBytes);

            if (result != null)
            {
                try
                {
                    // Almacenar el resultado en el servicio de navegación (en memoria)
                    // Esto evita pasar datos grandes por URL
                    // También guardamos los bytes de la imagen para mostrarla localmente como fallback
                    _navigationDataService.SetAnalysisResult(result, _capturedImageBytes);
                    
                    // Navegar a la página de resultados sin parámetros
                    // La página de resultados recuperará los datos del servicio
                    // Usar /// para ruta absoluta en Shell
                    await Shell.Current.GoToAsync("///ResultsPage");
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync(
                        "Error", 
                        $"Error al navegar a la página de resultados: {ex.Message}", 
                        "OK");
                }
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo analizar la imagen", "OK");
            }
        }
        catch (ApiException ex)
        {
            await DisplayAlertAsync("Error", $"Error al analizar: {ex.Message}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Error inesperado: {ex.Message}", "OK");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        CaptureButton.IsEnabled = !isLoading;
        AnalyzeButton.IsEnabled = !isLoading && _capturedImageBytes != null;
    }

#if ANDROID
    /// <summary>
    /// Solicita permisos de almacenamiento específicos para Android 12 y anteriores.
    /// Para Android 13+ (Tiramisu), los permisos de medios se manejan automáticamente con MediaPicker.
    /// </summary>
    [SupportedOSPlatform("android")]
    private async Task<bool> RequestAndroidStoragePermissionsAsync()
    {
        // Para Android 12 y anteriores: solicitar READ_EXTERNAL_STORAGE y WRITE_EXTERNAL_STORAGE
        if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.Tiramisu) // Android 12 y anteriores
        {
            var readStorageStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            if (readStorageStatus != PermissionStatus.Granted)
            {
                readStorageStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
            }

            var writeStorageStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            if (writeStorageStatus != PermissionStatus.Granted)
            {
                writeStorageStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
            }

            if (readStorageStatus != PermissionStatus.Granted || writeStorageStatus != PermissionStatus.Granted)
            {
                await DisplayAlertAsync(
                    "Permisos Requeridos", 
                    "Se requieren permisos de almacenamiento para guardar fotos. Por favor, habilítalos en Configuración > Apps > VisioAnalytica Risk > Permisos.", 
                    "OK");
                return false;
            }
        }
        // Para Android 13+: solo READ_MEDIA_IMAGES (se maneja automáticamente con MediaPicker)
        return true;
    }
#endif
}

