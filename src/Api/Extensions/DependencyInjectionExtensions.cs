// En: src/Api/Extensions/DependencyInjectionExtensions.cs
// (v5.0 - ¡INYECCIÓN DE REPORTES COMPLETADA!)

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Infrastructure.Data;
using VisioAnalytica.Infrastructure.Services;

namespace VisioAnalytica.Api.Extensions
{
    public static class DependencyInjectionExtensions
    {
        // Nota: Mantenemos tu firma exacta, que incluye 'env' para la lógica del DbContext
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
        {
            // --- 1. CONFIGURACIÓN DEL DbContext ---
            // (Esta lógica de 'env' es la tuya y es correcta)
            if (env.IsDevelopment())
            {
                Console.WriteLine("[VisioAnalytica.Api] Modo: Development (Usando SQL Server Docker)");
                services.AddDbContext<VisioAnalyticaDbContext>(options =>
                    options.UseSqlServer(config.GetConnectionString("LocalSqlServerConnection")));
            }
            else
            {
                Console.WriteLine($"[VisioAnalytica.Api] Modo: {env.EnvironmentName} (Usando SQL Server Docker por ahora)");
                services.AddDbContext<VisioAnalyticaDbContext>(options =>
                    options.UseSqlServer(config.GetConnectionString("LocalSqlServerConnection")));
            }

            // --- 2. CONFIGURACIÓN DE IDENTITY ---
            services.AddIdentityCore<User>(options =>
            {
                // Mantenemos tus reglas de contraseña relajadas para el desarrollo
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 4;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<VisioAnalyticaDbContext>()
            .AddDefaultTokenProviders();

            // --- 3. CONFIGURACIÓN DE AUTENTICACIÓN Y AUTORIZACIÓN ---
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = config["Jwt:Issuer"],
                        ValidAudience = config["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!))
                    };
                });

            services.AddAuthorization();

            // --- 4. REGISTRO DE SERVICIOS (Inyección de Dependencias) ---
            // Aquí consolidamos todo.

            // 4.A: Nuestro Servicio de Tokens (de la Tarea 30)
            services.AddScoped<ITokenService, TokenService>();

            // 4.B: Nuestro Servicio de IA (¡El tuyo, v4.0!)
            // Registrado como HttpClient para gestión de pool de conexiones
            services.AddHttpClient<IAiSstAnalyzer, GeminiAnalyzer>();

            // 4.C: Nuestro Servicio de Auth (de la Tarea 30)
            services.AddScoped<IAuthService, AuthService>();

            // 4.D: El "cerebro" orquestador de Análisis de Escritura
            services.AddScoped<IAnalysisService, AnalysisService>();

            // 4.E: El Repositorio de Persistencia (de lectura/escritura)
            services.AddScoped<IAnalysisRepository, AnalysisRepository>();

            // 4.F: ¡NUEVO REGISTRO! El Servicio de Consultas y Reportes (Capítulo 4)
            services.AddScoped<IReportService, ReportService>(); // << ¡AÑADIDO!


            // --- 5. SERVICIOS ESTÁNDAR DE API ---
            services.AddControllers();
            services.AddEndpointsApiExplorer();

            // 5.B: Configuración de Swagger (con soporte para Auth)
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "VisioAnalytica API", Version = "v1" });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Por favor ingrese el token JWT con 'Bearer ' en el campo",
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }});
            });

            return services;
        }
    }
}