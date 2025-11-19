using VisioAnalytica.App.Risk.Models;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk.Pages;

public partial class ChangePasswordPage : ContentPage
{
    private readonly IAuthService _authService;

    public ChangePasswordPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void OnChangePasswordClicked(object? sender, EventArgs e)
    {
        try
        {
            // Validación básica
            if (string.IsNullOrWhiteSpace(CurrentPasswordEntry.Text) ||
                string.IsNullOrWhiteSpace(NewPasswordEntry.Text) ||
                string.IsNullOrWhiteSpace(ConfirmPasswordEntry.Text))
            {
                ShowError("Por favor completa todos los campos");
                return;
            }

            // Validar que las contraseñas nuevas coincidan
            if (NewPasswordEntry.Text != ConfirmPasswordEntry.Text)
            {
                ShowError("Las contraseñas nuevas no coinciden");
                return;
            }

            // Validar que la nueva contraseña sea diferente
            if (CurrentPasswordEntry.Text == NewPasswordEntry.Text)
            {
                ShowError("La nueva contraseña debe ser diferente a la actual");
                return;
            }

            // Validar longitud mínima
            if (NewPasswordEntry.Text.Length < 4)
            {
                ShowError("La nueva contraseña debe tener al menos 4 caracteres");
                return;
            }

            // Mostrar loading
            SetLoading(true);
            ErrorLabel.IsVisible = false;

            // Cambiar contraseña
            var request = new ChangePasswordRequest(
                CurrentPasswordEntry.Text,
                NewPasswordEntry.Text
            );

            var result = await _authService.ChangePasswordAsync(request);

            if (result)
            {
                // Contraseña cambiada exitosamente
                await DisplayAlertAsync(
                    "Éxito",
                    "Tu contraseña ha sido cambiada exitosamente. Serás redirigido al inicio.",
                    "OK");

                // Recargar el token para actualizar mustChangePassword
                // (El backend debería actualizar el token en la respuesta)
                // Por ahora, simplemente redirigimos al login
                await _authService.LogoutAsync();
                await Shell.Current.GoToAsync("//LoginPage");
            }
            else
            {
                ShowError("No se pudo cambiar la contraseña. Verifica tu contraseña actual.");
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

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        ChangePasswordButton.IsEnabled = !isLoading;
        CurrentPasswordEntry.IsEnabled = !isLoading;
        NewPasswordEntry.IsEnabled = !isLoading;
        ConfirmPasswordEntry.IsEnabled = !isLoading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}

