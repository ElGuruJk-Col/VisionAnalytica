using System.Runtime.Versioning;
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

    // Constructor sin parámetros para ContentTemplate
    public LoginPage() : this(null)
    {
    }

    // Constructor con DI para navegación programática
    public LoginPage(IAuthService? authService)
    {
        InitializeComponent();
        _authService = authService;
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

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("maccatalyst")]
    [SupportedOSPlatform("windows")]
    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        try
        {
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
            var response = await GetAuthService().LoginAsync(request);

            if (response != null)
            {
                // Actualizar el menú del Flyout después del login
                if (Shell.Current is AppShell appShell)
                {
                    appShell.UpdateFlyoutMenu();
                }
                
                // Verificar si debe cambiar la contraseña
                if (GetAuthService().MustChangePassword)
                {
                    // Redirigir a la página de cambio de contraseña
                    await Shell.Current.GoToAsync("//ChangePasswordPage");
                }
                else
                {
                    // Login exitoso - navegar a la página principal
                    await Shell.Current.GoToAsync("//MainPage");
                }
            }
            else
            {
                ShowError("Error al iniciar sesión. Verifica tus credenciales.");
            }
        }
        catch (ApiException ex)
        {
            // El ApiException ya contiene un mensaje amigable
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            // Para errores inesperados, mostrar un mensaje genérico
            ShowError("Ocurrió un error inesperado. Por favor, intenta nuevamente.");
            System.Diagnostics.Debug.WriteLine($"Error inesperado en login: {ex}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("maccatalyst")]
    [SupportedOSPlatform("windows")]
    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        // Navegar a la página de registro
        await Shell.Current.GoToAsync("RegisterPage");
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("maccatalyst")]
    [SupportedOSPlatform("windows")]
    private async void OnForgotPasswordClicked(object? sender, EventArgs e)
    {
        // Navegar a la página de recuperación de contraseña
        await Shell.Current.GoToAsync("ForgotPasswordPage");
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
}

