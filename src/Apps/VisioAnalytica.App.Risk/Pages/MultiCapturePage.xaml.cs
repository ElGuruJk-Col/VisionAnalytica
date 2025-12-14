using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using VisioAnalytica.App.Risk.Models;
using VisioAnalytica.App.Risk.Services;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Pages;

/// <summary>
/// Página para capturar múltiples fotos y crear una inspección.
/// Diseño moderno y minimalista siguiendo mejores prácticas de .NET 10.0.
/// </summary>
[SupportedOSPlatform("android")]
[SupportedOSPlatform("ios")]
[SupportedOSPlatform("maccatalyst")]
[SupportedOSPlatform("windows")]
public partial class MultiCapturePage : ContentPage
{
    private readonly IApiClient _apiClient;
    private readonly IAuthService _authService;
    private readonly INotificationService _notificationService;
    private readonly INavigationService? _navigationService;
    private readonly IImageOptimizationService? _imageOptimizationService;
    private readonly ObservableCollection<CapturedPhotoViewModel> _capturedPhotos = [];
    private IList<AffiliatedCompanyDto>? _assignedCompanies;
    private AffiliatedCompanyDto? _selectedCompany;
    private bool _isAnalyzing;
    private readonly SemaphoreSlim _analyzeSemaphore = new SemaphoreSlim(1, 1); // Protección contra ejecución concurrente

    // Valores por defecto para optimización (se obtienen de la configuración de organización)
    private int _maxWidth = 1920;
    private int _quality = 85;

    public MultiCapturePage(IApiClient apiClient, IAuthService authService, INotificationService notificationService, INavigationService? navigationService = null, IImageOptimizationService? imageOptimizationService = null)
    {
        var instanceId = Guid.NewGuid();
        System.Diagnostics.Debug.WriteLine($"🏗️ [MultiCapturePage] Nueva instancia creada - InstanceId: {instanceId}, Thread: {Thread.CurrentThread.ManagedThreadId}, Time: {DateTime.Now:HH:mm:ss.fff}");
        
        InitializeComponent();
        _apiClient = apiClient;
        _authService = authService;
        _notificationService = notificationService;
        _navigationService = navigationService;
        _imageOptimizationService = imageOptimizationService;
        
        // ═══════════════════════════════════════════════════════════════
        // PROTECCIÓN: Desregistrar y registrar evento para evitar duplicados
        // ═══════════════════════════════════════════════════════════════
        AnalyzeButton.Clicked -= OnAnalyzeClicked; // Desregistrar primero (por si acaso)
        AnalyzeButton.Clicked += OnAnalyzeClicked; // Registrar el evento
        System.Diagnostics.Debug.WriteLine($"🔗 [MultiCapturePage] Evento OnAnalyzeClicked registrado - InstanceId: {instanceId}");
        
        // Establecer ItemsSource directamente (no usar binding)
        PhotosCollection.ItemsSource = _capturedPhotos;
        
        // Inicializar estado de botones (deshabilitados hasta que se carguen las empresas)
        var roles = _authService.CurrentUserRoles;
        if (roles.Contains("Inspector"))
        {
            CaptureButton.IsEnabled = false;
        }
        AnalyzeButton.IsEnabled = false;
        
        // Inicializar checkbox "Seleccionar todas"
        if (SelectAllCheckBox != null)
        {
            SelectAllCheckBox.IsEnabled = false;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Cargar configuración de organización para optimización
        await LoadOrganizationSettings();
        
        // Cargar empresas asignadas si es Inspector
        var roles = _authService.CurrentUserRoles;
        if (roles.Contains("Inspector"))
        {
            await LoadAssignedCompanies();
        }
        else
        {
            CompanyPicker.IsVisible = false;
            CompanyWarningLabel.IsVisible = false;
            // Para roles que no son Inspector, habilitar botón de captura
            UpdateButtonsState();
        }
    }

    private async Task LoadOrganizationSettings()
    {
        try
        {
            var settings = await _apiClient.GetOrganizationSettingsAsync();
            if (settings != null && settings.EnableImageOptimization)
            {
                _maxWidth = settings.MaxImageWidth;
                _quality = settings.ImageQuality;
                System.Diagnostics.Debug.WriteLine($"✅ Configuración de organización cargada: MaxWidth={_maxWidth}, Quality={_quality}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Optimización de imágenes deshabilitada o configuración no disponible, usando valores por defecto");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Error al cargar configuración de organización: {ex.Message}. Usando valores por defecto");
            // Usar valores por defecto si falla
        }
    }

    private async Task LoadAssignedCompanies()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("🔄 Iniciando carga de empresas asignadas...");
            SetLoading(true);
            
            // Verificar autenticación
            if (!_authService.IsAuthenticated)
            {
                System.Diagnostics.Debug.WriteLine("❌ Usuario no autenticado");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlertAsync("Error", "No estás autenticado. Por favor, inicia sesión.", "OK");
                    var navService = Handler?.MauiContext?.Services?.GetRequiredService<INavigationService>();
                    if (navService != null)
                        await navService.NavigateToLoginAsync();
                });
                return;
            }
            
            // Verificar token
            var token = _authService.CurrentToken;
            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("❌ Token no disponible");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlertAsync("Error", "Token de autenticación no disponible.", "OK");
                });
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"✅ Token disponible, llamando a GetMyCompaniesAsync...");
            _assignedCompanies = await _apiClient.GetMyCompaniesAsync();
            System.Diagnostics.Debug.WriteLine($"📦 Respuesta recibida: {(_assignedCompanies?.Count ?? 0)} empresas");
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_assignedCompanies != null && _assignedCompanies.Count > 0)
                {
                    // DEBUG: Verificar que las empresas tienen Name
                    System.Diagnostics.Debug.WriteLine($"✅ Cargadas {_assignedCompanies.Count} empresas:");
                    foreach (var company in _assignedCompanies)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {company?.Name ?? "NULL"} (ID: {company?.Id}, Activa: {company?.IsActive})");
                    }
                    
                    // Limpiar ItemsSource primero
                    CompanyPicker.ItemsSource = null;
                    
                    // Asignar nueva lista
                    var companyList = _assignedCompanies.Where(c => c != null && !string.IsNullOrEmpty(c.Name)).ToList();
                    CompanyPicker.ItemsSource = companyList;
                    
                    System.Diagnostics.Debug.WriteLine($"📋 ItemsSource asignado con {companyList.Count} empresas");
                    
                    CompanyPicker.IsVisible = true;
                    CompanyPicker.IsEnabled = true;
                    
                    // Si solo hay una empresa, seleccionarla automáticamente
                    if (companyList.Count == 1)
                    {
                        CompanyPicker.SelectedItem = companyList[0];
                        _selectedCompany = companyList[0];
                        CompanyWarningLabel.IsVisible = false;
                        System.Diagnostics.Debug.WriteLine($"✅ Empresa única seleccionada: {_selectedCompany.Name}");
                    }
                    else
                    {
                        // Si hay múltiples empresas, mostrar advertencia hasta que se seleccione una
                        CompanyWarningLabel.IsVisible = true;
                        _selectedCompany = null;
                        System.Diagnostics.Debug.WriteLine("⚠️ Múltiples empresas disponibles, esperando selección");
                    }
                    
                    // Actualizar estado de botones después de cargar empresas
                    UpdateButtonsState();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No se encontraron empresas asignadas o lista vacía");
                    CompanyPicker.ItemsSource = null; // Limpiar ItemsSource
                    CompanyPicker.IsVisible = true; // Mantener visible para mostrar el problema
                    CompanyPicker.IsEnabled = false;
                    CompanyWarningLabel.Text = "⚠️ No tienes empresas asignadas. Contacta a tu supervisor.";
                    CompanyWarningLabel.IsVisible = true;
                    CaptureButton.IsEnabled = false;
                }
            });
        }
        catch (ApiException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error API al cargar empresas: {ex.Message} (Status: {ex.StatusCode})");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                CompanyPicker.ItemsSource = null;
                CompanyPicker.IsEnabled = false;
                CompanyWarningLabel.Text = $"⚠️ Error: {ex.Message}";
                CompanyWarningLabel.IsVisible = true;
                await DisplayAlertAsync("Error", $"No se pudieron cargar las empresas: {ex.Message}", "OK");
                UpdateButtonsState();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error al cargar empresas: {ex}");
            System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                CompanyPicker.ItemsSource = null;
                CompanyPicker.IsEnabled = false;
                CompanyWarningLabel.Text = "⚠️ Error de conexión. Verifica tu internet.";
                CompanyWarningLabel.IsVisible = true;
                await DisplayAlertAsync("Error", $"Error de conexión: {ex.Message}", "OK");
                UpdateButtonsState();
            });
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void OnCompanySelected(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"🔄 OnCompanySelected llamado. SelectedItem: {CompanyPicker.SelectedItem}");
        
        if (CompanyPicker.SelectedItem is AffiliatedCompanyDto company && company != null)
        {
            // Si cambió la empresa, limpiar todas las fotos capturadas
            if (_selectedCompany != null && _selectedCompany.Id != company.Id)
            {
                System.Diagnostics.Debug.WriteLine($"🔄 Empresa cambió de {_selectedCompany.Name} a {company.Name}. Limpiando fotos...");
                _capturedPhotos.Clear();
                UpdateButtonsState();
            }
            
            _selectedCompany = company;
            CompanyWarningLabel.IsVisible = false;
            System.Diagnostics.Debug.WriteLine($"✅ Empresa seleccionada: {company.Name} (ID: {company.Id})");
            UpdateButtonsState();
        }
        else
        {
            // Si se deseleccionó la empresa, limpiar fotos
            if (_selectedCompany != null)
            {
                System.Diagnostics.Debug.WriteLine("🔄 Empresa deseleccionada. Limpiando fotos...");
                _capturedPhotos.Clear();
            }
            
            _selectedCompany = null;
            var roles = _authService.CurrentUserRoles;
            if (roles.Contains("Inspector"))
            {
                CompanyWarningLabel.IsVisible = true;
            }
            System.Diagnostics.Debug.WriteLine("⚠️ Empresa deseleccionada");
            UpdateButtonsState();
        }
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("maccatalyst")]
    [SupportedOSPlatform("windows")]
    private async void OnCaptureClicked(object? sender, EventArgs e)
    {
        // Validar empresa seleccionada (si es Inspector)
        var roles = _authService.CurrentUserRoles;
        if (roles.Contains("Inspector") && _selectedCompany == null)
        {
            await DisplayAlertAsync(
                "Empresa Requerida",
                "Debes seleccionar una empresa cliente antes de capturar una foto.",
                "OK");
            return;
        }

        try
        {
            // Solicitar permisos de cámara
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlertAsync(
                        "Permisos Requeridos",
                        "Se necesitan permisos de cámara para capturar fotos.",
                        "OK");
                    return;
                }
            }

            // Capturar foto
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo == null)
            {
                return; // Usuario canceló
            }

            // Leer bytes de la foto
            using var stream = await photo.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();

            // Crear thumbnail
            var imageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));

            // Agregar a la colección
            var photoViewModel = new CapturedPhotoViewModel
            {
                Id = Guid.NewGuid(),
                Thumbnail = imageSource,
                ImageBytes = imageBytes,
                CapturedAt = DateTime.Now,
                IsSelected = false
            };

            _capturedPhotos.Add(photoViewModel);
            UpdateButtonsState();
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlertAsync(
                "No Soportado",
                "La captura de fotos no está soportada en este dispositivo.",
                "OK");
        }
        catch (PermissionException)
        {
            await DisplayAlertAsync(
                "Permisos Denegados",
                "Se necesitan permisos de cámara para capturar fotos.",
                "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al capturar foto: {ex}");
            await DisplayAlertAsync("Error", "Ocurrió un error al capturar la foto.", "OK");
        }
    }

    private void OnPhotoTapped(object? sender, TappedEventArgs e)
    {
        if (_isAnalyzing) return; // No permitir selección durante análisis
        
        if (e.Parameter is CapturedPhotoViewModel photo)
        {
            photo.IsSelected = !photo.IsSelected;
            UpdateButtonsState();
        }
    }

    private void OnSelectAllChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_isAnalyzing) return; // No permitir durante análisis
        
        var isChecked = e.Value;
        foreach (var photo in _capturedPhotos)
        {
            photo.IsSelected = isChecked;
        }
        UpdateButtonsState();
    }

    private void OnSelectAllLabelTapped(object? sender, TappedEventArgs e)
    {
        if (_isAnalyzing || SelectAllCheckBox == null) return;
        SelectAllCheckBox.IsChecked = !SelectAllCheckBox.IsChecked;
    }

    /// <summary>
    /// Actualiza el estado de todos los botones según las condiciones actuales.
    /// </summary>
    private void UpdateButtonsState()
    {
        var roles = _authService.CurrentUserRoles;
        var hasCompanySelected = _selectedCompany != null;
        var hasPhotos = _capturedPhotos.Count > 0;
        var selectedCount = _capturedPhotos.Count(p => p.IsSelected);
        
        System.Diagnostics.Debug.WriteLine($"🔄 UpdateButtonsState: Company={hasCompanySelected}, Photos={hasPhotos}, Selected={selectedCount}, Analyzing={_isAnalyzing}");
        
        // Botón "Tomar Foto": Deshabilitado si está analizando O si no hay empresa (Inspector)
        if (roles.Contains("Inspector"))
        {
            CaptureButton.IsEnabled = hasCompanySelected && !_isAnalyzing;
            System.Diagnostics.Debug.WriteLine($"  📸 CaptureButton: {CaptureButton.IsEnabled} (Inspector, Company={hasCompanySelected}, Analyzing={_isAnalyzing})");
        }
        else
        {
            // Para otros roles, siempre habilitado excepto cuando está analizando
            CaptureButton.IsEnabled = !_isAnalyzing;
            System.Diagnostics.Debug.WriteLine($"  📸 CaptureButton: {CaptureButton.IsEnabled} (Otro rol, Analyzing={_isAnalyzing})");
        }
        
        // Botón "Analizar Seleccionadas": Solo habilitado si hay fotos seleccionadas, empresa seleccionada y no está analizando
        AnalyzeButton.Text = $"Analizar Seleccionadas ({selectedCount})";
        var analyzeEnabled = selectedCount > 0 && !_isAnalyzing && hasCompanySelected;
        AnalyzeButton.IsEnabled = analyzeEnabled;
        System.Diagnostics.Debug.WriteLine($"  🔍 AnalyzeButton: {analyzeEnabled} (Selected={selectedCount}, Company={hasCompanySelected}, Analyzing={_isAnalyzing})");
        
        // Deshabilitar selección de fotos si está analizando
        PhotosCollection.IsEnabled = !_isAnalyzing;
        
        // Actualizar checkbox "Seleccionar todas" si existe
        if (SelectAllCheckBox != null)
        {
            SelectAllCheckBox.IsEnabled = !_isAnalyzing && hasPhotos;
            // Actualizar estado del checkbox según si todas están seleccionadas
            if (hasPhotos && !_isAnalyzing)
            {
                var allSelected = _capturedPhotos.All(p => p.IsSelected);
                // Evitar actualización circular
                if (SelectAllCheckBox.IsChecked != allSelected)
                {
                    SelectAllCheckBox.IsChecked = allSelected;
                }
            }
        }
    }

    private async void OnAnalyzeClicked(object? sender, EventArgs e)
    {
        var clickId = Guid.NewGuid();
        System.Diagnostics.Debug.WriteLine($"🖱️ [OnAnalyzeClicked] CLIC DETECTADO - ClickId: {clickId}, Thread: {Thread.CurrentThread.ManagedThreadId}, Time: {DateTime.Now:HH:mm:ss.fff}");
        System.Diagnostics.Debug.WriteLine($"🖱️ [OnAnalyzeClicked] Sender: {sender?.GetType().Name}, Button IsEnabled: {(sender as Button)?.IsEnabled}");
        
        // ═══════════════════════════════════════════════════════════════
        // PROTECCIÓN CONTRA DOBLE CLIC Y EJECUCIÓN CONCURRENTE
        // ═══════════════════════════════════════════════════════════════
        
        // Intentar adquirir el semáforo (retorna false si ya está en uso)
        if (!await _analyzeSemaphore.WaitAsync(0))
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ [OnAnalyzeClicked] ClickId: {clickId} - Semáforo bloqueado, ignorando clic duplicado/concurrente");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"🔒 [OnAnalyzeClicked] ClickId: {clickId} - Semáforo adquirido exitosamente");
        
        try
        {
            // Verificación adicional con flag
            if (_isAnalyzing)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ [OnAnalyzeClicked] ClickId: {clickId} - Flag _isAnalyzing ya está en true, ignorando");
                return;
            }
            
            // Deshabilitar botón INMEDIATAMENTE (antes de cualquier operación asíncrona)
            AnalyzeButton.IsEnabled = false;
            System.Diagnostics.Debug.WriteLine($"🔒 [OnAnalyzeClicked] ClickId: {clickId} - Botón deshabilitado");
            
            // Establecer flag ANTES de cualquier operación asíncrona
            _isAnalyzing = true;
            
            System.Diagnostics.Debug.WriteLine($"🔍 [OnAnalyzeClicked] ClickId: {clickId} - Iniciado - Thread: {Thread.CurrentThread.ManagedThreadId}, Time: {DateTime.Now:HH:mm:ss.fff}");
            
            var selectedPhotos = _capturedPhotos.Where(p => p.IsSelected).ToList();
            System.Diagnostics.Debug.WriteLine($"📸 Fotos seleccionadas: {selectedPhotos.Count} de {_capturedPhotos.Count}");
            
            if (selectedPhotos.Count == 0)
            {
                _isAnalyzing = false;
                UpdateButtonsState();
                await DisplayAlertAsync("Sin Selección", "Debes seleccionar al menos una foto para analizar.", "OK");
                return;
            }

            if (_selectedCompany == null)
            {
                System.Diagnostics.Debug.WriteLine("❌ No hay empresa seleccionada");
                _isAnalyzing = false;
                UpdateButtonsState();
                await DisplayAlertAsync("Empresa Requerida", "Debes seleccionar una empresa cliente.", "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"✅ Empresa seleccionada: {_selectedCompany.Name} (ID: {_selectedCompany.Id})");

            InspectionDto? inspection = null;
            
            try
            {
                SetLoading(true);
                UpdateButtonsState(); // Deshabilitar botones inmediatamente
            
            StatusLabel.Text = "Creando inspección...";
            StatusSubLabel.Text = "Por favor espera...";
            StatusBorder.IsVisible = true;
            StatusBorder.Stroke = (Color)Application.Current!.Resources["Primary"]!;
            StatusBorder.BackgroundColor = (Color)Application.Current!.Resources["Gray50"]!;

            // OPTIMIZACIÓN: Optimizar imágenes antes de enviar (reduce tamaño y mejora rendimiento)
            System.Diagnostics.Debug.WriteLine($"🖼️ Optimizando {selectedPhotos.Count} imágenes antes de enviar...");
            var optimizedPhotos = new List<(byte[] optimizedBytes, DateTime capturedAt)>();
            
            foreach (var photo in selectedPhotos)
            {
                byte[] bytesToSend = photo.ImageBytes;
                
                // Optimizar imagen si el servicio está disponible
                if (_imageOptimizationService != null)
                {
                    try
                    {
                        var optimized = await _imageOptimizationService.OptimizeImageAsync(
                            photo.ImageBytes, 
                            _maxWidth, 
                            _quality);
                        
                        if (optimized != null && optimized.Length < photo.ImageBytes.Length)
                        {
                            bytesToSend = optimized;
                            var reductionPercent = 100 - (optimized.Length * 100 / photo.ImageBytes.Length);
                            System.Diagnostics.Debug.WriteLine($"✅ Imagen optimizada: {photo.ImageBytes.Length / 1024}KB -> {optimized.Length / 1024}KB ({reductionPercent}% reducción)");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Error al optimizar imagen, usando original: {ex.Message}");
                        // Si falla la optimización, usar imagen original
                    }
                }
                
                optimizedPhotos.Add((bytesToSend, photo.CapturedAt));
            }
            
            System.Diagnostics.Debug.WriteLine($"✅ {optimizedPhotos.Count} imágenes optimizadas listas para enviar");

            // Convertir fotos optimizadas a DTOs
            var photoDtos = optimizedPhotos.Select(p => new PhotoDto(
                Convert.ToBase64String(p.optimizedBytes),
                p.capturedAt
            )).ToList();

            System.Diagnostics.Debug.WriteLine($"📤 [OnAnalyzeClicked] Enviando {photoDtos.Count} fotos para crear inspección...");
            System.Diagnostics.Debug.WriteLine($"📤 [OnAnalyzeClicked] Request ID único: {Guid.NewGuid()}");
            System.Diagnostics.Debug.WriteLine($"📤 [OnAnalyzeClicked] Empresa ID: {_selectedCompany.Id}");
            System.Diagnostics.Debug.WriteLine($"📤 [OnAnalyzeClicked] Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");

            // Crear inspección
            var createRequest = new CreateInspectionDto(
                _selectedCompany.Id,
                photoDtos
            );

            System.Diagnostics.Debug.WriteLine($"📤 [OnAnalyzeClicked] Llamando a CreateInspectionAsync - Thread: {Thread.CurrentThread.ManagedThreadId}");
            inspection = await _apiClient.CreateInspectionAsync(createRequest);
            System.Diagnostics.Debug.WriteLine($"✅ [OnAnalyzeClicked] CreateInspectionAsync completado - Inspection ID: {inspection?.Id}");

            if (inspection != null)
            {
                System.Diagnostics.Debug.WriteLine($"✅ Inspección creada: {inspection.Id} con {inspection.Photos.Count} fotos");
                StatusLabel.Text = "Iniciando análisis en segundo plano...";
                
                // Iniciar análisis - usar los IDs de las fotos de la inspección creada
                var photoIds = inspection.Photos
                    .Where(p => selectedPhotos.Any(sp => Math.Abs((sp.CapturedAt - p.CapturedAt).TotalSeconds) < 5))
                    .Select(p => p.Id)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"🔍 Iniciando análisis de {photoIds.Count} fotos...");

                var analyzeRequest = new AnalyzeInspectionDto(
                    inspection.Id,
                    photoIds
                );

                var jobId = await _apiClient.StartAnalysisAsync(analyzeRequest);
                System.Diagnostics.Debug.WriteLine($"✅ Análisis iniciado con JobId: {jobId}");

                // Mostrar notificación local
                await _notificationService.ShowNotificationAsync(
                    "Análisis Iniciado",
                    $"Se está analizando {selectedPhotos.Count} foto(s) para {_selectedCompany.Name}. Recibirás una notificación cuando termine.");

                StatusLabel.Text = $"✅ Análisis Iniciado";
                StatusSubLabel.Text = $"{selectedPhotos.Count} foto(s) en proceso. Recibirás una notificación cuando termine.";
                StatusSubLabel.IsVisible = true;
                StatusBorder.IsVisible = true;
                StatusBorder.Stroke = (Color)Application.Current!.Resources["Success"]!;
                StatusBorder.BackgroundColor = Color.FromArgb("#E8F5E9"); // Light green

                // Limpiar TODAS las fotos después de analizar (no solo las seleccionadas)
                _capturedPhotos.Clear();
                System.Diagnostics.Debug.WriteLine("🧹 Lista de fotos limpiada después del análisis");

                // Actualizar estado de botones
                UpdateButtonsState();
                
                // Resetear estado de análisis para permitir nuevas capturas
                _isAnalyzing = false;
                SetLoading(false);
                
                // Esperar un momento para que el usuario vea el mensaje
                await Task.Delay(2000);
                
                // Navegar a la tab de Historial y refrescar datos
                // Esto evita crear una nueva instancia de la página
                var navService = Handler?.MauiContext?.Services?.GetRequiredService<INavigationService>();
                if (navService != null)
                    await navService.NavigateToHistoryTabAsync();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("❌ La inspección no se creó correctamente");
                throw new Exception("No se pudo crear la inspección.");
            }
            }
            catch (ApiException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ApiException al analizar: {ex.Message} (Status: {ex.StatusCode})");
                StatusLabel.Text = "❌ Error al Iniciar Análisis";
                StatusSubLabel.Text = ex.Message;
                StatusSubLabel.IsVisible = true;
                StatusBorder.IsVisible = true;
                StatusBorder.Stroke = (Color)Application.Current!.Resources["Error"]!;
                StatusBorder.BackgroundColor = Color.FromArgb("#FFEBEE"); // Light red
                _isAnalyzing = false; // Resetear solo en caso de error
                UpdateButtonsState();
                await DisplayAlertAsync("Error", $"Error al iniciar el análisis: {ex.Message}", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al analizar: {ex}");
                System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
                StatusLabel.Text = "❌ Error Inesperado";
                StatusSubLabel.Text = "Ocurrió un error al iniciar el análisis. Por favor, intenta nuevamente.";
                StatusSubLabel.IsVisible = true;
                StatusBorder.IsVisible = true;
                StatusBorder.Stroke = (Color)Application.Current!.Resources["Error"]!;
                StatusBorder.BackgroundColor = Color.FromArgb("#FFEBEE"); // Light red
                _isAnalyzing = false; // Resetear solo en caso de error
                UpdateButtonsState();
                await DisplayAlertAsync("Error", $"Error inesperado: {ex.Message}", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error en protección OnAnalyzeClicked: {ex}");
            _isAnalyzing = false;
            UpdateButtonsState();
        }
        finally
        {
            // Solo resetear loading, pero mantener _isAnalyzing si el análisis se inició correctamente
            // Nota: inspection no está disponible aquí porque está dentro del try interno
            SetLoading(false);
            
            // Liberar el semáforo
            _analyzeSemaphore.Release();
            System.Diagnostics.Debug.WriteLine($"🔓 Semáforo liberado - Thread: {Thread.CurrentThread.ManagedThreadId}");
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        // Actualizar estado de botones cuando cambia el estado de carga
        UpdateButtonsState();
    }
}

/// <summary>
/// ViewModel para una foto capturada.
/// </summary>
public class CapturedPhotoViewModel : BindableObject
{
    public Guid Id { get; set; }
    public ImageSource? Thumbnail { get; set; }
    public byte[] ImageBytes { get; set; } = [];
    public DateTime CapturedAt { get; set; }
    
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }
}

