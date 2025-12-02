using VisioAnalytica.App.Risk.Models;
using VisioAnalytica.App.Risk.Services;

namespace VisioAnalytica.App.Risk.Pages;

public partial class ForgotPasswordPage : ContentPage
{
    private readonly IAuthService _authService;
    private readonly INavigationService? _navigationService;

    public ForgotPasswordPage(IAuthService authService, INavigationService? navigationService = null)
    {
        InitializeComponent();
        _authService = authService;
        _navigationService = navigationService;
    }
    
    private INavigationService GetNavigationService()
    {
        if (_navigationService != null)
            return _navigationService;

        var serviceProvider = Handler?.MauiContext?.Services;
        if (serviceProvider != null)
        {
            return serviceProvider.GetRequiredService<INavigationService>();
        }

        throw new InvalidOperationException("INavigationService no está disponible.");
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        try
        {
            // Validación básica
            if (string.IsNullOrWhiteSpace(EmailEntry.Text))
            {
                ShowError("Por favor ingresa tu email");
                return;
            }

            // Validar formato de email básico
            if (!EmailEntry.Text.Contains("@"))
            {
                ShowError("Por favor ingresa un email válido");
                return;
            }

            // Mostrar loading
            SetLoading(true);
            ErrorLabel.IsVisible = false;
            SuccessLabel.IsVisible = false;

            // Enviar solicitud
            var request = new ForgotPasswordRequest(EmailEntry.Text.Trim());
            var result = await _authService.ForgotPasswordAsync(request);

            if (result)
            {
                ShowSuccess("Si el email está registrado, recibirás un enlace para restablecer tu contraseña.");
            }
            else
            {
                ShowError("No se pudo enviar el email. Intenta nuevamente.");
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
            System.Diagnostics.Debug.WriteLine($"Error inesperado en recuperación de contraseña: {ex}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnBackToLoginClicked(object? sender, EventArgs e)
    {
        await GetNavigationService().NavigateToLoginAsync();
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        SendButton.IsEnabled = !isLoading;
        BackToLoginButton.IsEnabled = !isLoading;
        EmailEntry.IsEnabled = !isLoading;
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
        ErrorLabel.IsVisible = false;
        SuccessLabel.IsVisible = false;
    }
}

