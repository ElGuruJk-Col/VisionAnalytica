using System.Runtime.Versioning;
using Microsoft.Maui.ApplicationModel;
using VisioAnalytica.App.Risk.Services;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Pages;

[SupportedOSPlatform("android")]
[SupportedOSPlatform("ios")]
[SupportedOSPlatform("maccatalyst")]
[SupportedOSPlatform("windows")]
public partial class CapturePage : ContentPage
{
    private readonly IAnalysisService _analysisService;
    private readonly INavigationDataService _navigationDataService;
    private readonly IApiClient? _apiClient;
    private readonly IAuthService? _authService;
    private byte[]? _capturedImageBytes;
    private IList<AffiliatedCompanyDto>? _assignedCompanies;
    private AffiliatedCompanyDto? _selectedCompany;

    public CapturePage(IAnalysisService analysisService, INavigationDataService navigationDataService, IApiClient? apiClient = null, IAuthService? authService = null)
    {
        InitializeComponent();
        _analysisService = analysisService;
        _navigationDataService = navigationDataService;
        _apiClient = apiClient;
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Verificar si es Inspector y cargar empresas asignadas
        if (_authService != null && _apiClient != null)
        {
            var roles = _authService.CurrentUserRoles;
            if (roles.Contains("Inspector"))
            {
                await LoadAssignedCompanies();
            }
            else
            {
                CompanyLabel.IsVisible = false;
                CompanyPicker.IsVisible = false;
            }
        }
    }

    private async Task LoadAssignedCompanies()
    {
        if (_apiClient == null) return;

        try
        {
            _assignedCompanies = await _apiClient.GetMyCompaniesAsync();
            
            if (_assignedCompanies != null && _assignedCompanies.Count > 0)
            {
                CompanyPicker.ItemsSource = _assignedCompanies.ToList();
                CompanyLabel.IsVisible = true;
                CompanyPicker.IsVisible = true;
                
                // Seleccionar primera empresa por defecto
                if (_assignedCompanies.Count == 1)
                {
                    CompanyPicker.SelectedItem = _assignedCompanies[0];
                    _selectedCompany = _assignedCompanies[0];
                }
            }
            else
            {
                // No debería llegar aquí si la validación de login funciona
                await DisplayAlertAsync("Error", "No tienes empresas asignadas.", "OK");
                await Shell.Current.GoToAsync("//MainPage");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar empresas: {ex}");
            await DisplayAlertAsync("Error", "No se pudieron cargar las empresas asignadas.", "OK");
        }
    }

    private void OnCompanySelected(object? sender, EventArgs e)
    {
        if (CompanyPicker.SelectedItem is AffiliatedCompanyDto company)
        {
            _selectedCompany = company;
        }
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("maccatalyst")]
    [SupportedOSPlatform("windows")]
    private async void OnCaptureClicked(object? sender, EventArgs e)
    {
        // Validar que haya seleccionado una empresa (si es Inspector)
        if (_authService != null)
        {
            var roles = _authService.CurrentUserRoles;
            if (roles.Contains("Inspector") && _selectedCompany == null)
            {
                await DisplayAlertAsync(
                    "Empresa Requerida",
                    "Debes seleccionar una empresa cliente antes de capturar una foto.",
                    "OK");
                return;
            }
        }

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

        // Validar empresa seleccionada (si es Inspector)
        if (_authService != null)
        {
            var roles = _authService.CurrentUserRoles;
            if (roles.Contains("Inspector") && _selectedCompany == null)
            {
                await DisplayAlertAsync(
                    "Empresa Requerida",
                    "Debes seleccionar una empresa cliente antes de analizar.",
                    "OK");
                return;
            }
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
                    // Guardar también el AffiliatedCompanyId si está seleccionado (para Inspectores)
                    var companyId = _selectedCompany?.Id;
                    _navigationDataService.SetAnalysisResult(result, _capturedImageBytes, companyId);
                    
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
            // El ApiException ya contiene un mensaje amigable
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        catch (Exception ex)
        {
            // Para errores inesperados, mostrar un mensaje genérico
            await DisplayAlertAsync("Error", "Ocurrió un error inesperado al analizar la imagen. Por favor, intenta nuevamente.", "OK");
            System.Diagnostics.Debug.WriteLine($"Error inesperado al analizar imagen: {ex}");
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

