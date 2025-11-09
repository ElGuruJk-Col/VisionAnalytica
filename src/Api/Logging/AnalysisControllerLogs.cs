using Microsoft.Extensions.Logging;
using System;

// 1. Este archivo SÓLO define este espacio de nombres.
namespace VisioAnalytica.Api.Logging
{
    /// <summary>
    /// Contiene los mensajes de log de alto rendimiento (generados en origen)
    /// para el AnalysisController. (v1.1 - Corregida accesibilidad)
    /// </summary>
    internal static partial class AnalysisControllerLogs
    {
        [LoggerMessage(
            EventId = 1001,
            Level = LogLevel.Warning,
            Message = "El servicio IAnalysisService devolvió null para el usuario {UserId}."
        )]
        // 2. Corregido: 'internal' para coincidir con la clase 'internal'
        internal static partial void AnalysisServiceReturnedNull(
            this ILogger logger,
            string? userId);

        [LoggerMessage(
            EventId = 1002,
            Level = LogLevel.Error,
            Message = "Error catastrófico en AnalysisController para el usuario {UserId}."
        )]
        // 3. Corregido: 'internal'
        internal static partial void CatastrophicError(
            this ILogger logger,
            string? userId,
            Exception ex);

        [LoggerMessage(
            EventId = 1003,
            Level = LogLevel.Information,
            Message = "AnalysisController: Petición de análisis recibida para el usuario {UserId}."
        )]
        // 4. Corregido: 'internal'
        internal static partial void AnalysisRequestReceived(
            this ILogger logger,
            string? userId);
    }
}