/*
 * ======================================================================
 * VisioAnalytica.Api - Program.cs (VERSIÓN 3 - AUTH COMPLETO)
 * * Añadimos ASP.NET Core Identity
 * * Añadimos Autenticación JWT
 * * Registramos el ITokenService y IAiSstAnalyzer
 * ======================================================================
 */
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Infrastructure.Data;
using VisioAnalytica.Infrastructure.Services; // ¡Importante! Para nuestros servicios

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURACIÓN DEL "INTERRUPTOR DE 3 VÍAS" (Base de Datos) ---
// Lee la variable "Environment" de appsettings.json o de Azure
var environment = builder.Configuration["Environment"] ?? "Development";
Console.WriteLine($"[VisioAnalytica.Api] Modo: {environment}");

switch (environment)
{
    case "Development": // FASE A: SQL Local (Gratis)
        builder.Services.AddDbContext<VisioAnalyticaDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("LocalSqlServerConnection")));
        break;

    case "Demo": // FASE B: SQLite en Azure (Gratis)
        var dbPath = Path.Combine(builder.Environment.ContentRootPath, "visioanalytica_demo.db");
        builder.Services.AddDbContext<VisioAnalyticaDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));
        break;

    case "Production": // FASE C: Azure SQL (Pago)
        builder.Services.AddDbContext<VisioAnalyticaDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("AzureProductionSqlConnection")));
        break;

    default:
        // Si el entorno no es válido, fallamos rápido
        throw new Exception($"Entorno desconocido: {environment}");
}

// --- 2. CONFIGURACIÓN DE IDENTITY (Usuarios y Roles) ---
// Añade los servicios de Identity (UserManager, SignInManager, etc.)
// Le dice que use EF Core y nuestro DbContext.
builder.Services.AddIdentityCore<User>(options =>
{
    // Configuraciones de password (podemos relajarlas para demo)
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 0;
})
.AddEntityFrameworkStores<VisioAnalyticaDbContext>()
.AddDefaultTokenProviders(); // Para reseteo de password, etc.

// --- 3. CONFIGURACIÓN DE AUTENTICACIÓN (JWT) ---
// Le dice a la API cómo "leer" y "validar" los tokens JWT
// que vienen en la cabecera "Authorization"
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, // Validar quién emitió el token
            ValidateAudience = true, // Validar para quién es el token
            ValidateLifetime = true, // Validar que no esté expirado
            ValidateIssuerSigningKey = true, // Validar la firma secreta
            ValidIssuer = builder.Configuration["Jwt:Issuer"], // Leído de appsettings.json
            ValidAudience = builder.Configuration["Jwt:Audience"], // Leído de appsettings.json
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)) // Leído de appsettings.json
        };
    });

// Añade la política de autorización
builder.Services.AddAuthorization();

// --- 4. REGISTRO DE SERVICIOS (Inyección de Dependencias) ---
// Aquí conectamos nuestros "contratos" (Interfaces)
// con nuestras "fábricas" (Implementaciones)
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAiSstAnalyzer, GeminiAnalyzer>(); // (¡Implementación dummy por ahora!)


// --- 5. SERVICIOS ESTÁNDAR DE API ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Para probar la API

// --- CONSTRUIMOS LA APP ---
var app = builder.Build();

// --- 6. CONFIGURACIÓN DEL PIPELINE HTTP ---
// El pipeline define el orden en que se procesan las peticiones
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ¡VITAL! Añadir Autenticación y Autorización al pipeline
// Debe ir ANTES de MapControllers
app.UseAuthentication(); // ¿Quién eres? (Lee el token)
app.UseAuthorization();  // ¿Tienes permiso? (Comprueba el token)

app.MapControllers(); // Envía la petición al Controller correcto

app.Run();

