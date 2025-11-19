using VisioAnalytica.App.Risk.Models;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk.Pages;

public partial class LoginPage : ContentPage
{
    private readonly IAuthService _authService;

    public LoginPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

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
            var response = await _authService.LoginAsync(request);

            if (response != null)
            {
                // Verificar si debe cambiar la contraseña
                if (_authService.MustChangePassword)
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
            ShowError($"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            ShowError($"Error inesperado: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        // Navegar a la página de registro
        await Shell.Current.GoToAsync("RegisterPage");
    }

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

