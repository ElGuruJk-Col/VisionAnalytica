namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Contrato para el servicio de envío de emails.
    /// Permite cambiar entre diferentes proveedores (SMTP, SendGrid, etc.)
    /// sin modificar el código de negocio.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Envía un email genérico.
        /// </summary>
        /// <param name="message">Mensaje de email a enviar</param>
        /// <returns>True si se envió correctamente, False en caso contrario</returns>
        Task<bool> SendEmailAsync(EmailMessage message);

        /// <summary>
        /// Envía un email de recuperación de contraseña con contraseña temporal.
        /// </summary>
        /// <param name="email">Email del destinatario</param>
        /// <param name="temporaryPassword">Contraseña temporal generada</param>
        /// <param name="userName">Nombre del usuario</param>
        /// <returns>True si se envió correctamente</returns>
        Task<bool> SendPasswordResetEmailAsync(string email, string temporaryPassword, string userName);

        /// <summary>
        /// Envía un email de bienvenida con contraseña temporal.
        /// </summary>
        /// <param name="email">Email del destinatario</param>
        /// <param name="userName">Nombre del usuario</param>
        /// <param name="temporaryPassword">Contraseña temporal</param>
        /// <returns>True si se envió correctamente</returns>
        Task<bool> SendWelcomeEmailAsync(string email, string userName, string temporaryPassword);

        /// <summary>
        /// Envía un email de notificación cuando un análisis de inspección está completo.
        /// </summary>
        /// <param name="email">Email del destinatario</param>
        /// <param name="inspectionId">ID de la inspección completada</param>
        /// <param name="companyName">Nombre de la empresa auditada</param>
        /// <returns>True si se envió correctamente</returns>
        Task<bool> SendAnalysisCompleteEmailAsync(string email, Guid inspectionId, string companyName);

        /// <summary>
        /// Envía un email de notificación cuando una cuenta ha sido bloqueada.
        /// </summary>
        /// <param name="email">Email del destinatario</param>
        /// <param name="userName">Nombre del usuario</param>
        /// <returns>True si se envió correctamente</returns>
        Task<bool> SendAccountLockedEmailAsync(string email, string userName);

        /// <summary>
        /// Envía un email de notificación al supervisor cuando un inspector no tiene empresas asignadas.
        /// </summary>
        /// <param name="supervisorEmail">Email del supervisor</param>
        /// <param name="supervisorName">Nombre del supervisor</param>
        /// <param name="inspectorEmail">Email del inspector</param>
        /// <param name="inspectorName">Nombre completo del inspector</param>
        /// <returns>True si se envió correctamente</returns>
        Task<bool> SendInspectorWithoutCompaniesEmailAsync(string supervisorEmail, string supervisorName, string inspectorEmail, string inspectorName);
    }

    /// <summary>
    /// Modelo para un mensaje de email genérico.
    /// </summary>
    public class EmailMessage
    {
        /// <summary>
        /// Email del destinatario.
        /// </summary>
        public string To { get; set; } = null!;

        /// <summary>
        /// Asunto del email.
        /// </summary>
        public string Subject { get; set; } = null!;

        /// <summary>
        /// Cuerpo del email (puede ser HTML o texto plano).
        /// </summary>
        public string Body { get; set; } = null!;

        /// <summary>
        /// Indica si el cuerpo es HTML.
        /// </summary>
        public bool IsHtml { get; set; } = true;

        /// <summary>
        /// Lista de emails en copia (CC).
        /// </summary>
        public List<string>? Cc { get; set; }

        /// <summary>
        /// Lista de emails en copia oculta (BCC).
        /// </summary>
        public List<string>? Bcc { get; set; }
    }
}

