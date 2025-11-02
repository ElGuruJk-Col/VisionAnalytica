// En: src/Api/Program.cs
// (¡VERSIÓN 5.0 - REFACTORIZADA!)

using VisioAnalytica.Api.Extensions; // <-- 1. ¡IMPORTAMOS NUESTRA "CAJA DE HERRAMIENTAS"!

var builder = WebApplication.CreateBuilder(args);

// --- "CABLEADO" DE SERVICIOS ---
// ¡Limpieza! Llamamos a nuestro nuevo Método de Extensión.
// Le pasamos el 'builder.Services' (el registro),
// el 'builder.Configuration' (para leer appsettings.json)
// y el 'builder.Environment' (para saber si es 'Development')
builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);

// --- CONSTRUIMOS LA APP ---
var app = builder.Build();

// --- CONFIGURACIÓN DEL PIPELINE HTTP ---
// (Este orden es vital)

// 1. Habilitar Swagger (SOLO en modo "Development")
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "VisioAnalytica API v1");
        options.RoutePrefix = string.Empty; // Swagger en la raíz (ej. http://localhost:5170/)
    });
}

// 2. Redirección HTTPS (seguridad)
app.UseHttpsRedirection();

// 3. ¡VITAL! Habilitar Autenticación y Autorización
app.UseAuthentication(); // ¿Quién eres? (Lee el token)
app.UseAuthorization();  // ¿Tienes permiso? (Comprueba el token)

// 4. Mapear los Controllers (el "recepcionista")
app.MapControllers();

// 5. ¡Arrancar!
app.Run();