using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk.Pages;

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
                    await DisplayAlert(
                        "Permisos Requeridos", 
                        "Se requiere permiso de cámara para tomar fotos. Por favor, habilita el permiso en Configuración > Apps > VisioAnalytica Risk > Permisos.", 
                        "OK");
                    return;
                }
            }

            // Verificar permisos de almacenamiento
#if ANDROID
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
                    await DisplayAlert(
                        "Permisos Requeridos", 
                        "Se requieren permisos de almacenamiento para guardar fotos. Por favor, habilítalos en Configuración > Apps > VisioAnalytica Risk > Permisos.", 
                        "OK");
                    return;
                }
            }
            // Para Android 13+: solo READ_MEDIA_IMAGES (se maneja automáticamente con MediaPicker)
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
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error al capturar foto: {ex.Message}", "OK");
        }
    }

    private async void OnAnalyzeClicked(object? sender, EventArgs e)
    {
        if (_capturedImageBytes == null || _capturedImageBytes.Length == 0)
        {
            await DisplayAlert("Error", "No hay imagen para analizar", "OK");
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
                    _navigationDataService.SetAnalysisResult(result);
                    
                    // Navegar a la página de resultados sin parámetros
                    // La página de resultados recuperará los datos del servicio
                    // Usar /// para ruta absoluta en Shell
                    await Shell.Current.GoToAsync("///ResultsPage");
                }
                catch (Exception ex)
                {
                    await DisplayAlert(
                        "Error", 
                        $"Error al navegar a la página de resultados: {ex.Message}", 
                        "OK");
                }
            }
            else
            {
                await DisplayAlert("Error", "No se pudo analizar la imagen", "OK");
            }
        }
        catch (ApiException ex)
        {
            await DisplayAlert("Error", $"Error al analizar: {ex.Message}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error inesperado: {ex.Message}", "OK");
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
}

