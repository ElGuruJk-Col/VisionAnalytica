using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VisioAnalytica.Core.Interfaces;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Implementación del servicio de email usando SMTP.
    /// Ideal para desarrollo y entornos donde se tiene control del servidor SMTP.
    /// </summary>
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailService> _logger;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly bool _enableSsl;

        public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
        {
            _config = config;
            _logger = logger;
            _fromEmail = _config["Email:From"] ?? throw new InvalidOperationException("Email:From no está configurado");
            _fromName = _config["Email:FromName"] ?? "VisioAnalytica";
            _smtpHost = _config["Email:Smtp:Host"] ?? throw new InvalidOperationException("Email:Smtp:Host no está configurado");
            _smtpPort = int.Parse(_config["Email:Smtp:Port"] ?? "587");
            _smtpUsername = _config["Email:Smtp:Username"] ?? throw new InvalidOperationException("Email:Smtp:Username no está configurado");
            _smtpPassword = _config["Email:Smtp:Password"] ?? throw new InvalidOperationException("Email:Smtp:Password no está configurado");
            _enableSsl = bool.Parse(_config["Email:Smtp:EnableSsl"] ?? "true");
        }

        public async Task<bool> SendEmailAsync(EmailMessage message)
        {
            try
            {
                using var smtpClient = new SmtpClient(_smtpHost)
                {
                    Port = _smtpPort,
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                    EnableSsl = _enableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30000 // 30 segundos
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = message.Subject,
                    Body = message.Body,
                    IsBodyHtml = message.IsHtml
                };

                mailMessage.To.Add(message.To);

                if (message.Cc != null && message.Cc.Any())
                {
                    foreach (var cc in message.Cc)
                    {
                        mailMessage.CC.Add(cc);
                    }
                }

                if (message.Bcc != null && message.Bcc.Any())
                {
                    foreach (var bcc in message.Bcc)
                    {
                        mailMessage.Bcc.Add(bcc);
                    }
                }

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("Email enviado exitosamente a {To}", message.To);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email a {To}", message.To);
                return false;
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string resetUrl)
        {
            var subject = "Recuperación de Contraseña - VisioAnalytica";
            var body = EmailTemplates.GetPasswordResetTemplate(resetUrl, resetToken);

            var message = new EmailMessage
            {
                To = email,
                Subject = subject,
                Body = body,
                IsHtml = true
            };

            return await SendEmailAsync(message);
        }

        public async Task<bool> SendWelcomeEmailAsync(string email, string userName, string temporaryPassword)
        {
            var subject = "Bienvenido a VisioAnalytica";
            var body = EmailTemplates.GetWelcomeTemplate(userName, temporaryPassword);

            var message = new EmailMessage
            {
                To = email,
                Subject = subject,
                Body = body,
                IsHtml = true
            };

            return await SendEmailAsync(message);
        }

        public async Task<bool> SendAnalysisCompleteEmailAsync(string email, Guid inspectionId, string companyName)
        {
            var subject = $"Análisis Completado - {companyName}";
            var body = EmailTemplates.GetAnalysisCompleteTemplate(companyName, inspectionId);

            var message = new EmailMessage
            {
                To = email,
                Subject = subject,
                Body = body,
                IsHtml = true
            };

            return await SendEmailAsync(message);
        }
    }
}

