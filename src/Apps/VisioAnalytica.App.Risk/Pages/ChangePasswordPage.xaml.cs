using VisioAnalytica.App.Risk.Models;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk.Pages;

public partial class ChangePasswordPage : ContentPage
{
    private readonly IAuthService _authService;
    private bool _isTemporaryPassword;
    private string? _previousRoute;

    public ChangePasswordPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Obtener la ruta anterior desde AppShell
        if (Shell.Current is AppShell appShell && !string.IsNullOrEmpty(AppShell.PreviousRoute))
        {
            _previousRoute = AppShell.PreviousRoute;
        }
        
        // Si no hay ruta anterior guardada y el usuario está autenticado, usar MainPage como default
        if (string.IsNullOrEmpty(_previousRoute) && _authService.IsAuthenticated)
        {
            _previousRoute = "//MainPage";
        }
        
        // Detectar si viene de contraseña temporal
        _isTemporaryPassword = _authService.MustChangePassword;
        
        if (_isTemporaryPassword)
        {
            // Ocultar campo de contraseña actual
            CurrentPasswordLabel.IsVisible = false;
            CurrentPasswordGrid.IsVisible = false;
            
            // Ocultar botón regresar (no puede regresar sin cambiar contraseña temporal)
            BackButton.IsVisible = false;
            
            // Actualizar mensaje informativo
            InfoLabel.Text = "Tu contraseña actual es de un solo uso. Por favor, ingresa una nueva contraseña segura.";
            InfoLabel.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#FF6B00"); // Naranja para advertencia
        }
        else
        {
            // Mostrar campo de contraseña actual
            CurrentPasswordLabel.IsVisible = true;
            CurrentPasswordGrid.IsVisible = true;
            
            // Mostrar botón regresar (puede regresar si no es contraseña temporal)
            BackButton.IsVisible = true;
            
            // Mensaje normal
            InfoLabel.Text = "Debes cambiar tu contraseña antes de continuar";
            InfoLabel.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#808080"); // Gris
        }
        
        // Limpiar campos
        ClearPasswordFields();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Limpiar campos al salir de la página
        ClearPasswordFields();
    }

    private void ClearPasswordFields()
    {
        CurrentPasswordEntry.Text = string.Empty;
        NewPasswordEntry.Text = string.Empty;
        ConfirmPasswordEntry.Text = string.Empty;
        ErrorLabel.IsVisible = false;
    }

    private async void OnChangePasswordClicked(object? sender, EventArgs e)
    {
        try
        {
            // Validación básica
            if (!_isTemporaryPassword && string.IsNullOrWhiteSpace(CurrentPasswordEntry.Text))
            {
                ShowError("Por favor ingresa tu contraseña actual");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(NewPasswordEntry.Text) ||
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

            // Validar que la nueva contraseña sea diferente (solo si no es temporal)
            if (!_isTemporaryPassword && CurrentPasswordEntry.Text == NewPasswordEntry.Text)
            {
                ShowError("La nueva contraseña debe ser diferente a la actual");
                return;
            }

            // Validar contraseña según reglas de seguridad
            var passwordValidation = ValidatePassword(NewPasswordEntry.Text);
            if (!passwordValidation.IsValid)
            {
                ShowError(passwordValidation.ErrorMessage);
                return;
            }

            // Mostrar loading
            SetLoading(true);
            ErrorLabel.IsVisible = false;

            // Cambiar contraseña
            var request = new ChangePasswordRequest(
                _isTemporaryPassword ? null : CurrentPasswordEntry.Text,
                NewPasswordEntry.Text
            );

            var result = await _authService.ChangePasswordAsync(request);

            if (result)
            {
                // Contraseña cambiada exitosamente
                await DisplayAlertAsync(
                    "Éxito",
                    "Tu contraseña ha sido cambiada exitosamente. Debes iniciar sesión nuevamente con tu nueva contraseña.",
                    "OK");

                // Cerrar sesión y redirigir al login
                await _authService.LogoutAsync();
                
                // Actualizar el menú del Flyout
                if (Shell.Current is AppShell appShell)
                {
                    appShell.UpdateFlyoutMenu();
                }
                
                // Redirigir al login
                await Shell.Current.GoToAsync("//LoginPage");
            }
            else
            {
                ShowError("No se pudo cambiar la contraseña. Verifica tu contraseña actual.");
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
            System.Diagnostics.Debug.WriteLine($"Error inesperado al cambiar contraseña: {ex}");
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
        BackButton.IsEnabled = !isLoading;
        CurrentPasswordEntry.IsEnabled = !isLoading;
        NewPasswordEntry.IsEnabled = !isLoading;
        ConfirmPasswordEntry.IsEnabled = !isLoading;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        try
        {
            // Intentar navegar a la ruta anterior guardada
            if (!string.IsNullOrEmpty(_previousRoute) && !_previousRoute.Contains("ChangePasswordPage"))
            {
                await Shell.Current.GoToAsync(_previousRoute);
            }
            else if (_authService.IsAuthenticated)
            {
                // Si no hay ruta anterior pero está autenticado, ir a MainPage
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                // Si no está autenticado, ir al login
                await Shell.Current.GoToAsync("//LoginPage");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al regresar: {ex.Message}");
            // Fallback: navegar a MainPage si está autenticado, sino al login
            try
            {
                if (_authService.IsAuthenticated)
                {
                    await Shell.Current.GoToAsync("//MainPage");
                }
                else
                {
                    await Shell.Current.GoToAsync("//LoginPage");
                }
            }
            catch
            {
                // Si todo falla, intentar usar Navigation.PopAsync
                if (Navigation.NavigationStack.Count > 1)
                {
                    await Navigation.PopAsync();
                }
            }
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void OnToggleCurrentPasswordClicked(object? sender, EventArgs e)
    {
        CurrentPasswordEntry.IsPassword = !CurrentPasswordEntry.IsPassword;
        ToggleCurrentPasswordButton.Text = CurrentPasswordEntry.IsPassword ? "👁" : "🔒";
    }

    private void OnToggleNewPasswordClicked(object? sender, EventArgs e)
    {
        NewPasswordEntry.IsPassword = !NewPasswordEntry.IsPassword;
        ToggleNewPasswordButton.Text = NewPasswordEntry.IsPassword ? "👁" : "🔒";
    }

    private void OnToggleConfirmPasswordClicked(object? sender, EventArgs e)
    {
        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;
        ToggleConfirmPasswordButton.Text = ConfirmPasswordEntry.IsPassword ? "👁" : "🔒";
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

