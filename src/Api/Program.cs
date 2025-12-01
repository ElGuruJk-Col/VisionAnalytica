// En: src/Api/Program.cs
// (VERSIÓN 5.0 - REFACTORIZADA!)

using Hangfire;
using VisioAnalytica.Api.Extensions; // <-- 1. ¡IMPORTAMOS NUESTRA "CAJA DE HERRAMIENTAS"!
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// --- "CABLEADO" DE SERVICIOS ---
// ¡Limpieza! Llamamos a nuestro nuevo Método de Extensión.
// Le pasamos el 'builder.Services' (el registro),
// el 'builder.Configuration' (para leer appsettings.json)
// y el 'builder.Environment' (para saber si es 'Development')
builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);

// Configurar redirección HTTPS antes de construir la app
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? 
           builder.Configuration["applicationUrl"] ?? 
           string.Empty;

var hasHttpsInUrls = urls.Contains("https://", StringComparison.OrdinalIgnoreCase);
int? httpsPort = null;

if (hasHttpsInUrls)
{
    // Buscar el puerto HTTPS en las URLs (formato: https://localhost:7005)
    var httpsUrl = urls.Split(';')
        .FirstOrDefault(u => u.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    
    if (!string.IsNullOrEmpty(httpsUrl))
    {
        try
        {
            var uri = new Uri(httpsUrl.Trim());
            httpsPort = uri.Port;
        }
        catch
        {
            // Si no se puede parsear la URL, continuar sin puerto específico
        }
    }
}

// Configurar el puerto HTTPS para la redirección si está disponible
if (httpsPort.HasValue)
{
    builder.Services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
    {
        options.HttpsPort = httpsPort.Value;
    });
}

// --- CONSTRUIMOS LA APP ---
var app = builder.Build();

// --- CONFIGURACIÓN DEL PIPELINE HTTP ---
// (Este orden es vital)

// 1. Habilitar Swagger PRIMERO (SOLO en modo "Development")
// Esto permite que Swagger funcione sin redirección HTTPS
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "VisioAnalytica API v1");
        options.RoutePrefix = "swagger"; // Swagger en /swagger/index.html
    });
}

// 2. Redirección HTTPS (seguridad) - DESHABILITADA en desarrollo para permitir conexiones desde dispositivos físicos
// En desarrollo, permitimos HTTP para que funcione desde dispositivos móviles
// En producción, se habilitará HTTPS redirection
if (!app.Environment.IsDevelopment())
{
    // Solo en producción: redirigir a HTTPS
    app.UseHttpsRedirection();
}

// 2.5. Habilitar CORS (DEBE ir antes de UseAuthentication)
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentCors");
}

// 2.7. Habilitar Hangfire Dashboard (solo en desarrollo o para admins)
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [] // En desarrollo, permitir acceso sin restricciones
    });
}

// 2.6. Habilitar archivos estáticos (para servir imágenes de wwwroot/uploads)
// NOTA: Los archivos de uploads ahora se sirven a través del FileController
// que requiere autenticación y verifica permisos por organización.
// Por lo tanto, NO exponemos /uploads públicamente.
app.UseStaticFiles();

// 3. ¡VITAL! Habilitar Autenticación y Autorización
app.UseAuthentication(); // ¿Quién eres? (Lee el token)
app.UseAuthorization();  // ¿Tienes permiso? (Comprueba el token)

// 4. Mapear los Controllers (el "recepcionista")
app.MapControllers();

// 5. Inicializar roles del sistema (si no existen)
using (var scope = app.Services.CreateScope())
{
    var roleSeeder = scope.ServiceProvider.GetRequiredService<VisioAnalytica.Infrastructure.Services.RoleSeederService>();
    await roleSeeder.SeedRolesAsync();
}

// 6. ¡Arrancar!
app.Run();
