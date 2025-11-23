using VisioAnalytica.App.Risk.Models;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk.Pages;

public partial class RegisterPage : ContentPage
{
    private readonly IAuthService _authService;

    public RegisterPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        try
        {
            // Validación básica
            if (string.IsNullOrWhiteSpace(FirstNameEntry.Text) ||
                string.IsNullOrWhiteSpace(LastNameEntry.Text) ||
                string.IsNullOrWhiteSpace(EmailEntry.Text) ||
                string.IsNullOrWhiteSpace(OrganizationEntry.Text) ||
                string.IsNullOrWhiteSpace(PasswordEntry.Text))
            {
                ShowError("Por favor completa todos los campos");
                return;
            }

            // Validar contraseñas
            if (PasswordEntry.Text != ConfirmPasswordEntry.Text)
            {
                ShowError("Las contraseñas no coinciden");
                return;
            }

            if (PasswordEntry.Text.Length < 4)
            {
                ShowError("La contraseña debe tener al menos 4 caracteres");
                return;
            }

            // Mostrar loading
            SetLoading(true);
            ErrorLabel.IsVisible = false;

            // Intentar registro
            var request = new RegisterRequest(
                EmailEntry.Text.Trim(),
                PasswordEntry.Text,
                FirstNameEntry.Text.Trim(),
                LastNameEntry.Text.Trim(),
                OrganizationEntry.Text.Trim()
            );

            var response = await _authService.RegisterAsync(request);

            if (response != null)
            {
                // Registro exitoso - navegar a la página principal
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                ShowError("Error al registrar. Intenta nuevamente.");
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
            System.Diagnostics.Debug.WriteLine($"Error inesperado en registro: {ex}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        RegisterButton.IsEnabled = !isLoading;
        BackButton.IsEnabled = !isLoading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}

