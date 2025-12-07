using System.Timers;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Servicio que verifica periódicamente si el token JWT ha expirado o está por expirar.
/// </summary>
public class TokenVerificationService : IDisposable
{
    private readonly IAuthService _authService;
    private readonly INavigationService? _navigationService;
    private System.Timers.Timer? _verificationTimer;
    private bool _disposed = false;
    
    // Verificar cada 5 minutos
    private static readonly TimeSpan VerificationInterval = TimeSpan.FromMinutes(5);
    
    // Umbral de advertencia: 30 minutos antes de expirar
    private static readonly TimeSpan WarningThreshold = TimeSpan.FromMinutes(30);

    public TokenVerificationService(IAuthService authService, INavigationService? navigationService = null)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _navigationService = navigationService;
    }

    /// <summary>
    /// Inicia la verificación periódica del token.
    /// </summary>
    public void StartVerification()
    {
        if (_verificationTimer != null)
            return; // Ya está iniciado
        
        _verificationTimer = new System.Timers.Timer(VerificationInterval.TotalMilliseconds);
        _verificationTimer.Elapsed += OnVerificationTimerElapsed;
        _verificationTimer.AutoReset = true;
        _verificationTimer.Start();
        
        System.Diagnostics.Debug.WriteLine($"✅ TokenVerificationService iniciado. Verificando cada {VerificationInterval.TotalMinutes} minutos.");
        
        // Verificar inmediatamente al iniciar
        _ = Task.Run(async () => await VerifyTokenAsync());
    }

    /// <summary>
    /// Detiene la verificación periódica del token.
    /// </summary>
    public void StopVerification()
    {
        _verificationTimer?.Stop();
        _verificationTimer?.Dispose();
        _verificationTimer = null;
        
        System.Diagnostics.Debug.WriteLine("⏹️ TokenVerificationService detenido.");
    }

    private async void OnVerificationTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        await VerifyTokenAsync();
    }

    /// <summary>
    /// Verifica el estado del token y toma acciones si es necesario.
    /// </summary>
    private async Task VerifyTokenAsync()
    {
        try
        {
            // Dar un delay para asegurar que la inicialización haya tenido tiempo
            // Los métodos de AuthService ahora inician la inicialización en background
            await Task.Delay(500);
            
            // Si no hay sesión activa, no hacer nada
            if (!_authService.IsAuthenticated)
            {
                return;
            }

            // Verificar si el token expiró
            // IsTokenExpired() ahora inicia la inicialización en background si es necesario
            if (_authService.IsTokenExpired())
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Token expirado detectado por verificación periódica. Cerrando sesión...");
                await HandleTokenExpiredAsync();
                return;
            }

            // Verificar si el token está por expirar pronto
            if (_authService.IsTokenExpiringSoon(WarningThreshold))
            {
                var timeRemaining = _authService.GetTokenTimeRemaining();
                if (timeRemaining.HasValue)
                {
                    var minutesRemaining = (int)timeRemaining.Value.TotalMinutes;
                    System.Diagnostics.Debug.WriteLine($"⚠️ Token expirará en {minutesRemaining} minutos.");
                    
                    // Opcional: Mostrar notificación al usuario
                    // Por ahora solo logueamos, pero se puede agregar una notificación
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Error en verificación de token: {ex.Message}");
        }
    }

    /// <summary>
    /// Maneja el caso cuando el token ha expirado.
    /// </summary>
    private async Task HandleTokenExpiredAsync()
    {
        try
        {
            // Cerrar sesión
            await _authService.LogoutAsync();
            
            // Redirigir al login si hay NavigationService disponible
            if (_navigationService != null)
            {
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        await _navigationService.NavigateToLoginAsync();
                        System.Diagnostics.Debug.WriteLine("✅ Redirigido al login por token expirado.");
                    }
                    catch (Exception navEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Error al redirigir al login: {navEx.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Error al manejar token expirado: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        
        StopVerification();
        _disposed = true;
    }
}

