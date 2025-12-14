using System.Runtime.Versioning;
using Microsoft.Maui.ApplicationModel;
using VisioAnalytica.App.Risk.Models;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk.Pages;

[SupportedOSPlatform("android")]
[SupportedOSPlatform("ios")]
[SupportedOSPlatform("maccatalyst")]
[SupportedOSPlatform("windows")]
public partial class LoginPage : ContentPage
{
    private IAuthService? _authService;
    private IApiClient? _apiClient;
    private INavigationService? _navigationService;

    // Constructor sin parámetros para ContentTemplate
    public LoginPage() : this(null, null, null)
    {
    }

    // Constructor con DI para navegación programática
    public LoginPage(IAuthService? authService, IApiClient? apiClient, INavigationService? navigationService = null)
    {
        InitializeComponent();
        _authService = authService;
        _apiClient = apiClient;
        _navigationService = navigationService;
    }
    
    private INavigationService GetNavigationService()
    {
        if (_navigationService != null)
            return _navigationService;

        var serviceProvider = Handler?.MauiContext?.Services;
        if (serviceProvider != null)
        {
            _navigationService = serviceProvider.GetRequiredService<INavigationService>();
            return _navigationService;
        }

        throw new InvalidOperationException("INavigationService no está disponible.");
    }

    // Obtener el servicio desde DI si no está disponible
    private IAuthService GetAuthService()
    {
        if (_authService != null)
            return _authService;

        // Intentar obtener desde el contenedor de DI
        var serviceProvider = Handler?.MauiContext?.Services;
        if (serviceProvider != null)
        {
            _authService = serviceProvider.GetRequiredService<IAuthService>();
            return _authService;
        }

        throw new InvalidOperationException("IAuthService no está disponible. La aplicación no se ha inicializado correctamente.");
    }

    // Obtener ApiClient desde DI si no está disponible
    private IApiClient GetApiClient()
    {
          if (_apiClient != null)
            return _apiClient;

        // Intentar obtener desde el contenedor de DI
        var serviceProvider = Handler?.MauiContext?.Services;
        if (serviceProvider != null)
        {
            _apiClient = serviceProvider.GetRequiredService<IApiClient>();
            return _apiClient;
        }

        throw new InvalidOperationException("IApiClient no está disponible. La aplicación no se ha inicializado correctamente.");
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("maccatalyst")]
    [SupportedOSPlatform("windows")]
    private async void OnLoginClicked(object? sender, EventArgs e)
     {
        try
        {
            System.Diagnostics.Debug.WriteLine("[Login] OnLoginClicked START");

            // Validación básica
            if (string.IsNullOrWhiteSpace(EmailEntry.Text) || 
                string.IsNullOrWhiteSpace(PasswordEntry.Text))
            {
                ShowError("Por favor completa todos los campos");
                return;
            }

            // Mostrar loading
            SetLoading(true);
            ErrorLabel.IsVisible = false;

            // Intentar login
            var request = new LoginRequest(EmailEntry.Text.Trim(), PasswordEntry.Text);
            System.Diagnostics.Debug.WriteLine($"[Login] Enviando login para: {request.Email}");
            var response = await GetAuthService().LoginAsync(request);
            System.Diagnostics.Debug.WriteLine($"[Login] LoginAsync returned. Response is null: {response == null}");

            if (response != null)
            {
                System.Diagnostics.Debug.WriteLine($"[Login] Token length: {(response.Token?.Length ?? 0)}; Email: {response.Email}; MustChangePassword: {response.MustChangePassword}");

                // Verificar si debe cambiar la contraseña
                if (GetAuthService().MustChangePassword)
                {
                    System.Diagnostics.Debug.WriteLine("[Login] MustChangePassword = true -> NavigateToChangePasswordAsync");
                    // Redirigir a la página de cambio de contraseña
                    await MainThread.InvokeOnMainThreadAsync(async () => await GetNavigationService().NavigateToChangePasswordAsync());
                    return;
                }
                
                // Verificar si es Inspector y tiene empresas asignadas
                var authService = GetAuthService();
                var roles = authService.CurrentUserRoles;
                System.Diagnostics.Debug.WriteLine($"[Login] Roles: {string.Join(',', roles)}");
                
                if (roles.Contains("Inspector"))
                {
                    try
                    {
                        var apiClient = GetApiClient();
                        var companies = await apiClient.GetMyCompaniesAsync();
                        System.Diagnostics.Debug.WriteLine($"[Login] GetMyCompaniesAsync returned {companies?.Count ?? 0}");
                        
                        if (companies == null || companies.Count == 0)
                        {
                            // Notificar al supervisor
                            await apiClient.NotifyInspectorWithoutCompaniesAsync();
                            
                            // Mostrar mensaje y bloquear acceso
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await DisplayAlertAsync("Acceso Deshabilitado",
                                    "Tu ingreso está deshabilitado, debes tener al menos una empresa asignada. Se ha notificado a tu superior.",
                                    "OK");
                            });
                            
                            // Cerrar sesión
                            await authService.LogoutAsync();
                            return;
                        }
                    }
                    catch (ApiException ex)
                    {
                        // Si hay error al verificar empresas, mostrar mensaje pero permitir acceso
                        System.Diagnostics.Debug.WriteLine($"Error al verificar empresas: {ex.Message}");
                        // Continuar con el flujo normal
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error inesperado al verificar empresas: {ex}");
                        // Continuar con el flujo normal si hay error
                    }
                }
                
                // Login exitoso - navegar a la página principal
                System.Diagnostics.Debug.WriteLine("[Login] Navegando a Main");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await GetNavigationService().NavigateToMainAsync();
                    // Debug alert to confirm navigation (temporary)
                    try
                    {
                        await DisplayAlertAsync("Debug", "Login exitoso. Navegando a Main.", "OK");
                    }
                    catch { }
                });
            }
            else
            {
                ShowError("Error al iniciar sesión. Verifica tus credenciales.");
            }
        }
        catch (ApiException ex)
        {
            // El ApiException ya contiene un mensaje amigable
            System.Diagnostics.Debug.WriteLine($"❌ ApiException en login: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"   StatusCode: {ex.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"   InnerException: {ex.InnerException.Message}");
            }
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            // Para errores inesperados, mostrar un mensaje genérico
            System.Diagnostics.Debug.WriteLine($"❌ Error inesperado en login: {ex}");
            System.Diagnostics.Debug.WriteLine($"   Tipo: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"   Mensaje: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"   InnerException: {ex.InnerException.Message}");
            }
            ShowError($"Ocurrió un error inesperado: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
            System.Diagnostics.Debug.WriteLine("[Login] OnLoginClicked END");
        }
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("maccatalyst")]
    [SupportedOSPlatform("windows")]
    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        // Navegar a la página de registro
        await GetNavigationService().NavigateToRegisterAsync();
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("maccatalyst")]
    [SupportedOSPlatform("windows")]
    private async void OnForgotPasswordClicked(object? sender, EventArgs e)
    {
        // Navegar a la página de recuperación de contraseña
        await GetNavigationService().NavigateToForgotPasswordAsync();
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        LoginButton.IsEnabled = !isLoading;
        RegisterButton.IsEnabled = !isLoading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void OnTogglePasswordClicked(object? sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        // Iconos monocromáticos modernos: ● (ojo cerrado) y ○ (ojo abierto)
        TogglePasswordButton.Text = PasswordEntry.IsPassword ? "●" : "○";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Limpiar campos al aparecer la página
        ClearFields();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Limpiar campos al salir de la página
        ClearFields();
    }

    private void ClearFields()
    {
        EmailEntry.Text = string.Empty;
        PasswordEntry.Text = string.Empty;
        ErrorLabel.IsVisible = false;
    }
}

