using Microsoft.Maui.ApplicationModel;
using VisioAnalytica.App.Risk.Services;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Pages;

public partial class CapturePage : ContentPage
{
    private readonly IAnalysisService _analysisService;
    private readonly INavigationDataService _navigationDataService;
    private readonly IApiClient _apiClient;
    private readonly IAuthService _authService;
    private readonly INavigationService? _navigationService;
    private byte[]? _capturedImageBytes;
    private IList<AffiliatedCompanyDto>? _assignedCompanies;
    private AffiliatedCompanyDto? _selectedCompany;
    private bool _isAnalyzing;

    // Constructor con DI - Los servicios son requeridos y siempre se inyectan desde MauiProgram
    public CapturePage(IAnalysisService analysisService, INavigationDataService navigationDataService, IApiClient apiClient, IAuthService authService, INavigationService? navigationService = null)
    {
        try
        {
            InitializeComponent();
            _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
            _navigationDataService = navigationDataService ?? throw new ArgumentNullException(nameof(navigationDataService));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _navigationService = navigationService;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en constructor de CapturePage: {ex}");
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        try
        {
            // Esperar un momento para que los controles estén completamente inicializados
            await Task.Delay(100);
            
            // VALIDACIÓN: Solo SuperAdmin puede acceder a esta página (uso interno/desarrollo)
            var roles = _authService.CurrentUserRoles;
            if (!roles.Contains("SuperAdmin"))
            {
                // Si no es SuperAdmin, redirigir a MainPage
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlertAsync(
                        "Acceso Restringido", 
                        "Esta página es solo para uso interno de desarrollo.", 
                        "OK");
                    var navService = _navigationService ?? Handler?.MauiContext?.Services?.GetRequiredService<INavigationService>();
                    if (navService != null)
                        await navService.NavigateToMainAsync();
                });
                return;
            }
            
            // Cargar empresas según el rol
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (roles.Contains("Inspector"))
                {
                    await LoadAssignedCompanies();
                }
                else if (roles.Contains("SuperAdmin"))
                {
                    // Para SuperAdmin: modo de prueba sin persistencia, no necesita empresa
                    if (CompanyLabel != null)
                        CompanyLabel.IsVisible = false;
                    if (CompanyPicker != null)
                        CompanyPicker.IsVisible = false;
                    
                    // Habilitar botón de captura directamente
                    if (CaptureButton != null)
                        CaptureButton.IsEnabled = true;
                }
                else if (roles.Contains("Admin"))
                {
                    // Para Admin, cargar todas las empresas de la organización
                    await LoadAllCompanies();
                }
                else
                {
                    // Para otros roles, ocultar controles de empresa
                    if (CompanyLabel != null)
                        CompanyLabel.IsVisible = false;
                    if (CompanyPicker != null)
                        CompanyPicker.IsVisible = false;
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnAppearing de CapturePage: {ex}");
        }
    }

    private async Task LoadAssignedCompanies()
    {
        if (_apiClient == null) return;

        try
        {
            _assignedCompanies = await _apiClient.GetMyCompaniesAsync();
            
            // Actualizar UI en el hilo principal
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_assignedCompanies != null && _assignedCompanies.Count > 0)
                {
                    if (CompanyPicker != null)
                    {
                        CompanyPicker.ItemsSource = _assignedCompanies.ToList();
                        CompanyPicker.IsVisible = true;
                        
                        // Seleccionar primera empresa por defecto
                        if (_assignedCompanies.Count == 1)
                        {
                            CompanyPicker.SelectedItem = _assignedCompanies[0];
                            _selectedCompany = _assignedCompanies[0];
                        }
                    }
                    
                    if (CompanyLabel != null)
                        CompanyLabel.IsVisible = true;
                    
                    // Habilitar botón de captura cuando hay empresa seleccionada
                    if (CaptureButton != null && _selectedCompany != null)
                        CaptureButton.IsEnabled = true;
                }
                else
                {
                    // No debería llegar aquí si la validación de login funciona
                    // Mostrar alerta en el hilo principal
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlertAsync("Error", "No tienes empresas asignadas.", "OK");
                        var navService = _navigationService ?? Handler?.MauiContext?.Services?.GetRequiredService<INavigationService>();
                    if (navService != null)
                        await navService.NavigateToMainAsync();
                    });
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar empresas: {ex}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync("Error", "No se pudieron cargar las empresas asignadas.", "OK");
            });
        }
    }

    private async Task LoadAllCompanies()
    {
        if (_apiClient == null) return;

        try
        {
            _assignedCompanies = await _apiClient.GetAllCompaniesAsync();
            
            // Actualizar UI en el hilo principal
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_assignedCompanies != null && _assignedCompanies.Count > 0)
                {
                    if (CompanyPicker != null)
                    {
                        CompanyPicker.ItemsSource = _assignedCompanies.ToList();
                        CompanyPicker.IsVisible = true;
                        
                        // Seleccionar primera empresa por defecto
                        if (_assignedCompanies.Count == 1)
                        {
                            CompanyPicker.SelectedItem = _assignedCompanies[0];
                            _selectedCompany = _assignedCompanies[0];
                        }
                    }
                    
                    if (CompanyLabel != null)
                        CompanyLabel.IsVisible = true;
                    
                    // Habilitar botón de captura cuando hay empresa seleccionada
                    if (CaptureButton != null && _selectedCompany != null)
                        CaptureButton.IsEnabled = true;
                }
                else
                {
                    // Mostrar alerta si no hay empresas
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlertAsync("Error", "No hay empresas disponibles en la organización. Debes crear al menos una empresa antes de realizar análisis.", "OK");
                    });
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar empresas: {ex}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync("Error", "No se pudieron cargar las empresas.", "OK");
            });
        }
    }

    private void OnCompanySelected(object? sender, EventArgs e)
    {
        try
        {
            if (CompanyPicker != null && CompanyPicker.SelectedItem is AffiliatedCompanyDto company)
            {
                _selectedCompany = company;
                
                // Habilitar botones cuando se selecciona una empresa
                if (CaptureButton != null)
                    CaptureButton.IsEnabled = true;
                
                // Habilitar botón de análisis si hay imagen capturada
                if (AnalyzeButton != null && _capturedImageBytes != null && _capturedImageBytes.Length > 0)
                    AnalyzeButton.IsEnabled = true;
            }
            else
            {
                _selectedCompany = null;
                // Deshabilitar botones si no hay empresa seleccionada
                var roles = _authService.CurrentUserRoles;
                if (roles.Contains("Inspector") || roles.Contains("SuperAdmin") || roles.Contains("Admin"))
                {
                    if (CaptureButton != null)
                        CaptureButton.IsEnabled = false;
                    if (AnalyzeButton != null)
                        AnalyzeButton.IsEnabled = false;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en OnCompanySelected: {ex}");
        }
    }

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
        // ═══════════════════════════════════════════════════════════════
        // PROTECCIÓN CONTRA DOBLE CLIC - DEBE SER LO PRIMERO
        // ═══════════════════════════════════════════════════════════════
        if (_isAnalyzing)
        {
            System.Diagnostics.Debug.WriteLine("⚠️ OnAnalyzeClicked: Ya está analizando, ignorando clic duplicado");
            return;
        }
        
        // Deshabilitar botón INMEDIATAMENTE (antes de cualquier operación asíncrona)
        if (AnalyzeButton != null)
        {
            AnalyzeButton.IsEnabled = false;
        }
        
        // Establecer flag ANTES de cualquier operación asíncrona
        _isAnalyzing = true;
        
        if (_capturedImageBytes == null || _capturedImageBytes.Length == 0)
        {
            _isAnalyzing = false;
            if (AnalyzeButton != null)
            {
                AnalyzeButton.IsEnabled = true;
            }
            await DisplayAlertAsync("Error", "No hay imagen para analizar", "OK");
            return;
        }

        var roles = _authService?.CurrentUserRoles ?? new List<string>();
        var isSuperAdmin = roles.Contains("SuperAdmin");

        // Para SuperAdmin: análisis directo sin persistencia (modo de prueba)
        if (isSuperAdmin)
        {
            try
            {
                SetLoading(true);
                StatusLabel.IsVisible = true;
                StatusLabel.Text = "Analizando imagen...";
                StatusLabel.TextColor = Colors.Blue;

                // Realizar análisis directo sin crear inspección
                System.Diagnostics.Debug.WriteLine("🔍 SuperAdmin: Iniciando análisis directo (sin persistencia)...");
                
                var result = await _analysisService.AnalyzeImageAsync(_capturedImageBytes);

                if (result != null)
                {
                    System.Diagnostics.Debug.WriteLine("✅ Análisis completado exitosamente");
                    
                    try
                    {
                        // Almacenar el resultado en el servicio de navegación (en memoria)
                        // No guardamos AffiliatedCompanyId porque no hay empresa en modo de prueba
                        _navigationDataService.SetAnalysisResult(result, _capturedImageBytes, null);
                        
                        StatusLabel.Text = "✅ Análisis Completado";
                        StatusLabel.TextColor = Colors.Green;
                        StatusLabel.IsVisible = true;

                        // Limpiar la imagen capturada
                        _capturedImageBytes = null;
                        CapturedImage.IsVisible = false;
                        PlaceholderLabel.IsVisible = true;
                        AnalyzeButton.IsEnabled = false;

                        // Navegar a la página de resultados
                        var navService = _navigationService ?? Handler?.MauiContext?.Services?.GetRequiredService<INavigationService>();
                        if (navService != null)
                            await navService.NavigateToResultsAsync();
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
                System.Diagnostics.Debug.WriteLine($"❌ ApiException al analizar: {ex.Message} (Status: {ex.StatusCode})");
                await DisplayAlertAsync("Error", ex.Message, "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error inesperado al analizar: {ex}");
                System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
                await DisplayAlertAsync("Error", "Ocurrió un error inesperado al analizar la imagen. Por favor, intenta nuevamente.", "OK");
            }
            finally
            {
                _isAnalyzing = false;
                SetLoading(false);
                if (AnalyzeButton != null)
                {
                    AnalyzeButton.IsEnabled = true;
                }
            }
            
            return; // Salir temprano para SuperAdmin
        }

        // Para otros roles (Inspector, Admin): usar el flujo con persistencia
        // Validar empresa seleccionada
        if (_selectedCompany == null)
        {
            _isAnalyzing = false;
            if (AnalyzeButton != null)
            {
                AnalyzeButton.IsEnabled = true;
            }
            await DisplayAlertAsync(
                "Empresa Requerida",
                "Debes seleccionar una empresa cliente antes de analizar.",
                "OK");
            return;
        }

        try
        {
            SetLoading(true);
            StatusLabel.IsVisible = true;
            StatusLabel.Text = "Creando inspección...";
            StatusLabel.TextColor = Colors.Blue;

            // Crear inspección con la foto usando el nuevo flujo
            var photoDto = new PhotoDto(
                Convert.ToBase64String(_capturedImageBytes),
                DateTime.UtcNow,
                null
            );

            var createRequest = new CreateInspectionDto(
                _selectedCompany.Id,
                new List<PhotoDto> { photoDto }
            );

            System.Diagnostics.Debug.WriteLine($"📤 Creando inspección para empresa {_selectedCompany.Name} (ID: {_selectedCompany.Id})");

            var inspection = await _apiClient.CreateInspectionAsync(createRequest);

            if (inspection == null)
            {
                _isAnalyzing = false;
                SetLoading(false);
                if (AnalyzeButton != null)
                {
                    AnalyzeButton.IsEnabled = true;
                }
                await DisplayAlertAsync("Error", "No se pudo crear la inspección.", "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"✅ Inspección creada: {inspection.Id} con {inspection.Photos.Count} foto(s)");

            StatusLabel.Text = "Iniciando análisis en segundo plano...";
            StatusLabel.TextColor = Colors.Blue;

            // Iniciar análisis de la foto
            var photoId = inspection.Photos.FirstOrDefault()?.Id;
            if (!photoId.HasValue)
            {
                _isAnalyzing = false;
                SetLoading(false);
                if (AnalyzeButton != null)
                {
                    AnalyzeButton.IsEnabled = true;
                }
                await DisplayAlertAsync("Error", "No se pudo obtener el ID de la foto.", "OK");
                return;
            }

            var analyzeRequest = new AnalyzeInspectionDto(
                inspection.Id,
                new List<Guid> { photoId.Value }
            );

            System.Diagnostics.Debug.WriteLine($"🔍 Iniciando análisis de foto {photoId.Value}...");

            var jobId = await _apiClient.StartAnalysisAsync(analyzeRequest);
            
            System.Diagnostics.Debug.WriteLine($"✅ Análisis iniciado con JobId: {jobId}");

            StatusLabel.Text = "✅ Análisis Iniciado";
            StatusLabel.TextColor = Colors.Green;
            StatusLabel.IsVisible = true;

            // Limpiar la imagen capturada
            _capturedImageBytes = null;
            CapturedImage.IsVisible = false;
            PlaceholderLabel.IsVisible = true;
            AnalyzeButton.IsEnabled = false;

            // Resetear estado de análisis después de éxito
            _isAnalyzing = false;

            await DisplayAlertAsync(
                "Análisis Iniciado",
                "El análisis se está procesando en segundo plano. Puedes ver el progreso en el historial de inspecciones.",
                "OK");

            // Navegar al historial de inspecciones
            var navService = _navigationService ?? Handler?.MauiContext?.Services?.GetRequiredService<INavigationService>();
            if (navService != null)
                await navService.NavigateToInspectionHistoryAsync();
        }
        catch (ApiException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ ApiException al analizar: {ex.Message} (Status: {ex.StatusCode})");
            _isAnalyzing = false;
            SetLoading(false);
            if (AnalyzeButton != null)
            {
                AnalyzeButton.IsEnabled = true;
            }
            await DisplayAlertAsync("Error", $"Error al iniciar el análisis: {ex.Message}", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error inesperado al analizar: {ex}");
            System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            _isAnalyzing = false;
            SetLoading(false);
            if (AnalyzeButton != null)
            {
                AnalyzeButton.IsEnabled = true;
            }
            await DisplayAlertAsync("Error", "Ocurrió un error inesperado al iniciar el análisis. Por favor, intenta nuevamente.", "OK");
        }
        finally
        {
            // Asegurar que el estado se resetee incluso si hay errores no capturados
            if (_isAnalyzing)
            {
                _isAnalyzing = false;
            }
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

