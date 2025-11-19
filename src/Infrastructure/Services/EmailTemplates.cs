namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Plantillas HTML para emails del sistema.
    /// </summary>
    public static class EmailTemplates
    {
        /// <summary>
        /// Plantilla para email de recuperación de contraseña.
        /// </summary>
        public static string GetPasswordResetTemplate(string resetUrl, string resetToken)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 8px;
            padding: 30px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
        }}
        .logo {{
            font-size: 24px;
            font-weight: bold;
            color: #007bff;
            margin-bottom: 10px;
        }}
        .content {{
            margin-bottom: 30px;
        }}
        .button {{
            display: inline-block;
            background-color: #007bff;
            color: #ffffff !important;
            padding: 12px 30px;
            text-decoration: none;
            border-radius: 5px;
            margin: 20px 0;
            font-weight: bold;
        }}
        .button:hover {{
            background-color: #0056b3;
        }}
        .token-box {{
            background-color: #f8f9fa;
            border: 1px solid #dee2e6;
            border-radius: 4px;
            padding: 15px;
            margin: 20px 0;
            font-family: 'Courier New', monospace;
            word-break: break-all;
            text-align: center;
        }}
        .footer {{
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #dee2e6;
            font-size: 12px;
            color: #6c757d;
            text-align: center;
        }}
        .warning {{
            background-color: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 10px;
            margin: 20px 0;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo'>VisioAnalytica</div>
        </div>
        <div class='content'>
            <h2>Recuperación de Contraseña</h2>
            <p>Hemos recibido una solicitud para restablecer tu contraseña.</p>
            <p>Haz clic en el siguiente botón para restablecer tu contraseña:</p>
            <div style='text-align: center;'>
                <a href='{resetUrl}?token={resetToken}' class='button'>Restablecer Contraseña</a>
            </div>
            <p>O copia y pega el siguiente enlace en tu navegador:</p>
            <div class='token-box'>{resetUrl}?token={resetToken}</div>
            <div class='warning'>
                <strong>⚠️ Importante:</strong> Este enlace expirará en 24 horas. Si no solicitaste este cambio, ignora este email.
            </div>
        </div>
        <div class='footer'>
            <p>Este es un email automático, por favor no respondas.</p>
            <p>&copy; {DateTime.Now.Year} VisioAnalytica. Todos los derechos reservados.</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Plantilla para email de bienvenida con contraseña temporal.
        /// </summary>
        public static string GetWelcomeTemplate(string userName, string temporaryPassword)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 8px;
            padding: 30px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
        }}
        .logo {{
            font-size: 24px;
            font-weight: bold;
            color: #007bff;
            margin-bottom: 10px;
        }}
        .content {{
            margin-bottom: 30px;
        }}
        .password-box {{
            background-color: #f8f9fa;
            border: 2px solid #007bff;
            border-radius: 4px;
            padding: 20px;
            margin: 20px 0;
            font-family: 'Courier New', monospace;
            font-size: 18px;
            font-weight: bold;
            text-align: center;
            color: #007bff;
        }}
        .button {{
            display: inline-block;
            background-color: #28a745;
            color: #ffffff !important;
            padding: 12px 30px;
            text-decoration: none;
            border-radius: 5px;
            margin: 20px 0;
            font-weight: bold;
        }}
        .button:hover {{
            background-color: #218838;
        }}
        .footer {{
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #dee2e6;
            font-size: 12px;
            color: #6c757d;
            text-align: center;
        }}
        .warning {{
            background-color: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 10px;
            margin: 20px 0;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo'>VisioAnalytica</div>
        </div>
        <div class='content'>
            <h2>¡Bienvenido a VisioAnalytica!</h2>
            <p>Hola <strong>{userName}</strong>,</p>
            <p>Tu cuenta ha sido creada exitosamente. A continuación encontrarás tus credenciales de acceso:</p>
            <div class='password-box'>
                Contraseña Temporal: {temporaryPassword}
            </div>
            <div class='warning'>
                <strong>⚠️ Importante:</strong> Por seguridad, deberás cambiar esta contraseña en tu primer inicio de sesión.
            </div>
            <p>Puedes iniciar sesión con tu email y la contraseña temporal proporcionada arriba.</p>
            <div style='text-align: center;'>
                <a href='#' class='button'>Iniciar Sesión</a>
            </div>
        </div>
        <div class='footer'>
            <p>Este es un email automático, por favor no respondas.</p>
            <p>&copy; {DateTime.Now.Year} VisioAnalytica. Todos los derechos reservados.</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Plantilla para email de notificación de análisis completado.
        /// </summary>
        public static string GetAnalysisCompleteTemplate(string companyName, Guid inspectionId)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 8px;
            padding: 30px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
        }}
        .logo {{
            font-size: 24px;
            font-weight: bold;
            color: #007bff;
            margin-bottom: 10px;
        }}
        .content {{
            margin-bottom: 30px;
        }}
        .success-box {{
            background-color: #d4edda;
            border-left: 4px solid #28a745;
            padding: 15px;
            margin: 20px 0;
        }}
        .button {{
            display: inline-block;
            background-color: #007bff;
            color: #ffffff !important;
            padding: 12px 30px;
            text-decoration: none;
            border-radius: 5px;
            margin: 20px 0;
            font-weight: bold;
        }}
        .button:hover {{
            background-color: #0056b3;
        }}
        .footer {{
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #dee2e6;
            font-size: 12px;
            color: #6c757d;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo'>VisioAnalytica</div>
        </div>
        <div class='content'>
            <h2>Análisis Completado</h2>
            <div class='success-box'>
                <strong>✅ El análisis de la inspección ha sido completado exitosamente.</strong>
            </div>
            <p>La auditoría realizada a <strong>{companyName}</strong> ha sido procesada y el informe está listo para revisión.</p>
            <p><strong>ID de Inspección:</strong> {inspectionId}</p>
            <p>Puedes acceder al informe completo desde la aplicación.</p>
            <div style='text-align: center;'>
                <a href='#' class='button'>Ver Informe</a>
            </div>
        </div>
        <div class='footer'>
            <p>Este es un email automático, por favor no respondas.</p>
            <p>&copy; {DateTime.Now.Year} VisioAnalytica. Todos los derechos reservados.</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}

