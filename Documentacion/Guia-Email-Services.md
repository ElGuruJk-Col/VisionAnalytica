# **Guía: Servicios de Email en VisioAnalytica**

## **Arquitectura: Interfaz Configurable**

Implementaremos una **interfaz abstracta** que permita cambiar entre diferentes proveedores de email sin modificar el código de negocio.

---

## **Diseño Propuesto**

### **1. Interfaz Base (`IEmailService`)**

```csharp
namespace VisioAnalytica.Core.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(EmailMessage message);
        Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string resetUrl);
        Task<bool> SendAnalysisCompleteEmailAsync(string email, Guid inspectionId);
        Task<bool> SendWelcomeEmailAsync(string email, string userName, string temporaryPassword);
    }
    
    public class EmailMessage
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public bool IsHtml { get; set; } = true;
        public List<string>? Cc { get; set; }
        public List<string>? Bcc { get; set; }
    }
}
```

### **2. Implementaciones**

#### **A. SendGrid (`SendGridEmailService`)**

**Ventajas:**
- ✅ API REST moderna y fácil de usar
- ✅ Generoso plan gratuito (100 emails/día)
- ✅ Excelente deliverability (llegada a inbox)
- ✅ Analytics y tracking
- ✅ Plantillas de email
- ✅ Escalable para millones de emails

**Desventajas:**
- ⚠️ Requiere cuenta y API key
- ⚠️ Dependencia externa

**Instalación:**
```bash
dotnet add package SendGrid
```

**Ejemplo:**
```csharp
public class SendGridEmailService : IEmailService
{
    private readonly SendGridClient _client;
    private readonly IConfiguration _config;
    
    public async Task<bool> SendEmailAsync(EmailMessage message)
    {
        var msg = new SendGridMessage
        {
            From = new EmailAddress(_config["Email:From"]),
            Subject = message.Subject,
            PlainTextContent = message.Body,
            HtmlContent = message.Body
        };
        msg.AddTo(new EmailAddress(message.To));
        
        var response = await _client.SendEmailAsync(msg);
        return response.IsSuccessStatusCode;
    }
}
```

---

#### **B. SMTP Propio (`SmtpEmailService`)**

**Ventajas:**
- ✅ Control total sobre el servidor
- ✅ Sin límites de terceros
- ✅ Sin costos adicionales (si ya tienes servidor)
- ✅ Privacidad completa

**Desventajas:**
- ⚠️ Requiere servidor SMTP configurado
- ⚠️ Debes manejar deliverability
- ⚠️ Posibles problemas con spam filters
- ⚠️ Mantenimiento del servidor

**Instalación:**
```bash
# Ya viene con .NET, no necesita paquete adicional
```

**Ejemplo:**
```csharp
public class SmtpEmailService : IEmailService
{
    private readonly SmtpClient _smtpClient;
    private readonly IConfiguration _config;
    
    public SmtpEmailService(IConfiguration config)
    {
        _config = config;
        _smtpClient = new SmtpClient(_config["Email:Smtp:Host"])
        {
            Port = int.Parse(_config["Email:Smtp:Port"]),
            Credentials = new NetworkCredential(
                _config["Email:Smtp:Username"],
                _config["Email:Smtp:Password"]),
            EnableSsl = true
        };
    }
    
    public async Task<bool> SendEmailAsync(EmailMessage message)
    {
        var mailMessage = new MailMessage
        {
            From = new MailAddress(_config["Email:From"]),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = message.IsHtml
        };
        mailMessage.To.Add(message.To);
        
        await _smtpClient.SendMailAsync(mailMessage);
        return true;
    }
}
```

---

### **3. Configuración en `appsettings.json`**

```json
{
  "Email": {
    "Provider": "SendGrid", // o "Smtp"
    "From": "noreply@visioanalytica.com",
    "FromName": "VisioAnalytica",
    
    "SendGrid": {
      "ApiKey": "SG.xxxxx"
    },
    
    "Smtp": {
      "Host": "smtp.gmail.com",
      "Port": 587,
      "Username": "tu-email@gmail.com",
      "Password": "tu-password"
    }
  }
}
```

### **4. Registro en DependencyInjection**

```csharp
// En DependencyInjectionExtensions.cs
var emailProvider = config["Email:Provider"];

if (emailProvider == "SendGrid")
{
    services.AddScoped<IEmailService, SendGridEmailService>();
    services.AddSingleton<SendGridClient>(sp => 
        new SendGridClient(config["Email:SendGrid:ApiKey"]));
}
else if (emailProvider == "Smtp")
{
    services.AddScoped<IEmailService, SmtpEmailService>();
}
else
{
    throw new InvalidOperationException($"Email provider '{emailProvider}' no es válido");
}
```

---

## **Plantillas de Email**

### **Estructura Propuesta**

```
src/Infrastructure/Email/
  - Templates/
    - PasswordReset.html
    - Welcome.html
    - AnalysisComplete.html
    - InspectionReport.html
```

### **Ejemplo de Plantilla (Razor o Handlebars)**

```html
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; }
        .container { max-width: 600px; margin: 0 auto; }
        .button { background: #007bff; color: white; padding: 10px 20px; text-decoration: none; }
    </style>
</head>
<body>
    <div class="container">
        <h1>Bienvenido a VisioAnalytica</h1>
        <p>Hola {{UserName}},</p>
        <p>Tu contraseña temporal es: <strong>{{TemporaryPassword}}</strong></p>
        <p>Por favor, cámbiala en tu primer inicio de sesión.</p>
        <a href="{{LoginUrl}}" class="button">Iniciar Sesión</a>
    </div>
</body>
</html>
```

---

## **Recomendación**

### **Para Desarrollo:**
- **SMTP Propio** (Gmail, Outlook) - Fácil de configurar, sin costos

### **Para Producción:**
- **SendGrid** - Mejor deliverability, analytics, escalable

### **Estrategia Híbrida:**
- Implementar ambos
- Configurar por ambiente (Development → SMTP, Production → SendGrid)
- Cambiar con solo modificar `appsettings.json`

---

## **Casos de Uso en VisioAnalytica**

1. **Bienvenida con contraseña temporal**
2. **Recuperación de contraseña**
3. **Notificación de análisis completado**
4. **Reporte de inspección listo**
5. **Recordatorio de cambio de contraseña**

---

¿Procedemos con esta arquitectura configurable?

