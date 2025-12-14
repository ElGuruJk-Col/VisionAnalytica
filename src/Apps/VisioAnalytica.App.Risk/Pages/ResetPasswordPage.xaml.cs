using VisioAnalytica.App.Risk.Models;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk.Pages;

public partial class ResetPasswordPage : ContentPage
{
    private readonly IAuthService _authService;
    private readonly INavigationService? _navigationService;
    private string? _token;

    public ResetPasswordPage(IAuthService authService, INavigationService? navigationService = null)
    {
        InitializeComponent();
        _authService = authService;
        _navigationService = navigationService;
        
        // Intentar obtener el token de los query parameters si viene de un deep link
        LoadTokenFromQuery();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Intentar obtener el token de los query parameters si viene de un deep link
        LoadTokenFromQuery();
    }

    private void LoadTokenFromQuery()
    {
        // Si la página se carga con query parameters (desde un deep link o URL)
        // Por ahora, el usuario puede ingresar el token manualmente en el campo TokenEntry
        // Nota: Deep linking se puede implementar manualmente con NavigationService si es necesario
    }

    public void SetToken(string token)
    {
        _token = token;
        if (TokenEntry != null)
        {
            TokenEntry.Text = token;
        }
    }

    public void SetEmail(string email)
    {
        EmailEntry.Text = email;
    }

    private async void OnResetPasswordClicked(object? sender, EventArgs e)
    {
        try
        {
            // Validación básica
            if (string.IsNullOrWhiteSpace(EmailEntry.Text) ||
                string.IsNullOrWhiteSpace(NewPasswordEntry.Text) ||
                string.IsNullOrWhiteSpace(ConfirmPasswordEntry.Text))
            {
                ShowError("Por favor completa todos los campos");
                return;
            }

            // Validar que las contraseñas coincidan
            if (NewPasswordEntry.Text != ConfirmPasswordEntry.Text)
            {
                ShowError("Las contraseñas no coinciden");
                return;
            }

            // Validar longitud mínima
            if (NewPasswordEntry.Text.Length < 4)
            {
                ShowError("La contraseña debe tener al menos 4 caracteres");
                return;
            }

            // Obtener el token del campo o del parámetro
            var tokenToUse = _token ?? TokenEntry.Text;
            
            // Validar que hay token
            if (string.IsNullOrWhiteSpace(tokenToUse))
            {
                ShowError("Token de recuperación requerido. Por favor, ingresa el token del email o solicita un nuevo enlace de recuperación.");
                return;
            }

            // Mostrar loading
            SetLoading(true);
            ErrorLabel.IsVisible = false;
            SuccessLabel.IsVisible = false;

            // Restablecer contraseña
            var request = new ResetPasswordRequest(
                EmailEntry.Text.Trim(),
                tokenToUse,
                NewPasswordEntry.Text
            );

            var result = await _authService.ResetPasswordAsync(request);

            if (result)
            {
                ShowSuccess("Tu contraseña ha sido restablecida exitosamente. Serás redirigido al inicio de sesión.");
                
                // Esperar un momento y redirigir al login
                await Task.Delay(2000);
                var navService = _navigationService ?? Handler?.MauiContext?.Services?.GetRequiredService<INavigationService>();
                if (navService != null)
                    await navService.NavigateToLoginAsync();
            }
            else
            {
                ShowError("No se pudo restablecer la contraseña. El token puede haber expirado o ser inválido. Solicita un nuevo enlace.");
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
            System.Diagnostics.Debug.WriteLine($"Error inesperado al restablecer contraseña: {ex}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnBackToLoginClicked(object? sender, EventArgs e)
    {
        var navService = _navigationService ?? Handler?.MauiContext?.Services?.GetRequiredService<INavigationService>();
        if (navService != null)
            await navService.NavigateToLoginAsync();
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        ResetPasswordButton.IsEnabled = !isLoading;
        BackToLoginButton.IsEnabled = !isLoading;
        EmailEntry.IsEnabled = !isLoading;
        TokenEntry.IsEnabled = !isLoading;
        NewPasswordEntry.IsEnabled = !isLoading;
        ConfirmPasswordEntry.IsEnabled = !isLoading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
        SuccessLabel.IsVisible = false;
    }

    private void ShowSuccess(string message)
    {
        SuccessLabel.Text = message;
        SuccessLabel.IsVisible = true;
        ErrorLabel.IsVisible = false;
    }
}

