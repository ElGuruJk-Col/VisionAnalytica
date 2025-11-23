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

            // Validar contraseña según reglas de seguridad
            var passwordValidation = ValidatePassword(PasswordEntry.Text);
            if (!passwordValidation.IsValid)
            {
                ShowError(passwordValidation.ErrorMessage);
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
        FirstNameEntry.Text = string.Empty;
        LastNameEntry.Text = string.Empty;
        EmailEntry.Text = string.Empty;
        OrganizationEntry.Text = string.Empty;
        PasswordEntry.Text = string.Empty;
        ConfirmPasswordEntry.Text = string.Empty;
        ErrorLabel.IsVisible = false;
    }

    /// <summary>
    /// Valida que la contraseña cumpla con los requisitos de seguridad.
    /// </summary>
    private (bool IsValid, string ErrorMessage) ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "La contraseña no puede estar vacía");
        }

        // Longitud mínima: 8 caracteres
        if (password.Length < 8)
        {
            return (false, "La contraseña debe tener al menos 8 caracteres");
        }

        // Verificar que contenga al menos una letra minúscula
        if (!password.Any(char.IsLower))
        {
            return (false, "La contraseña debe contener al menos una letra minúscula");
        }

        // Verificar que contenga al menos una letra mayúscula
        if (!password.Any(char.IsUpper))
        {
            return (false, "La contraseña debe contener al menos una letra mayúscula");
        }

        // Verificar que contenga al menos un número
        if (!password.Any(char.IsDigit))
        {
            return (false, "La contraseña debe contener al menos un número");
        }

        // Verificar que contenga al menos un carácter especial
        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return (false, "La contraseña debe contener al menos un carácter especial (!, ?, @, #, $, %, etc.)");
        }

        return (true, string.Empty);
    }
}

