using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VisioAnalytica.Infrastructure.Data;

namespace VisioAnalytica.Infrastructure.Services;

/// <summary>
/// Servicio en segundo plano que limpia periódicamente los refresh tokens expirados y revocados.
/// Se ejecuta cada 24 horas para mantener la base de datos limpia.
/// </summary>
public class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RefreshTokenCleanupService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _cleanupInterval;

    public RefreshTokenCleanupService(
        IServiceProvider serviceProvider,
        ILogger<RefreshTokenCleanupService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        
        // Intervalo de limpieza: por defecto 24 horas, configurable desde appsettings
        var intervalHours = _configuration.GetValue<int>("Jwt:TokenCleanupIntervalHours", 24);
        _cleanupInterval = TimeSpan.FromHours(intervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 RefreshTokenCleanupService iniciado. Intervalo de limpieza: {Interval}", _cleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredTokensAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error durante la limpieza de tokens expirados");
            }

            // Esperar el intervalo configurado antes de la próxima limpieza
            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Limpia los refresh tokens expirados y revocados de la base de datos.
    /// </summary>
    private async Task CleanupExpiredTokensAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VisioAnalyticaDbContext>();

        try
        {
            var now = DateTime.UtcNow;
            
            // Obtener tokens expirados o revocados hace más de 7 días
            // (damos un margen para auditoría antes de eliminarlos completamente)
            var cutoffDate = now.AddDays(-7);
            
            var expiredTokens = await context.RefreshTokens
                .Where(rt => 
                    (rt.ExpiresAt < now && rt.RevokedAt == null) || // Tokens expirados no revocados
                    (rt.RevokedAt.HasValue && rt.RevokedAt.Value < cutoffDate)) // Tokens revocados hace más de 7 días
                .ToListAsync(cancellationToken);

            if (expiredTokens.Count > 0)
            {
                _logger.LogInformation("🧹 Limpiando {Count} refresh tokens expirados/revocados", expiredTokens.Count);
                
                context.RefreshTokens.RemoveRange(expiredTokens);
                var deleted = await context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("✅ Eliminados {Deleted} refresh tokens de la base de datos", deleted);
            }
            else
            {
                _logger.LogDebug("✅ No hay tokens expirados para limpiar");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error al limpiar tokens expirados");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🛑 RefreshTokenCleanupService deteniéndose...");
        await base.StopAsync(cancellationToken);
    }
}

