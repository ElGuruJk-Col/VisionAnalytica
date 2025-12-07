using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
                // Reglas de contraseña seguras
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 8;
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

                    // AGREGAR ESTE EVENT HANDLER PARA LOGGING
                    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogError(context.Exception, "Error de autenticación JWT: {Error}", context.Exception.Message);
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogInformation("Token JWT validado exitosamente. Claims: {Claims}",
                                string.Join(", ", context.Principal!.Claims.Select(c => $"{c.Type}={c.Value}")));
                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                            // Logging MUY detallado
                            var authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();
                            var allHeaders = string.Join(", ", context.HttpContext.Request.Headers.Select(h => $"{h.Key}={h.Value}"));

                            logger.LogWarning("═══════════════════════════════════════════════════");
                            logger.LogWarning("🔒 CHALLENGE DE AUTENTICACIÓN DETECTADO");
                            logger.LogWarning("═══════════════════════════════════════════════════");
                            logger.LogWarning("Path: {Path}", context.HttpContext.Request.Path);
                            logger.LogWarning("Method: {Method}", context.HttpContext.Request.Method);
                            logger.LogWarning("Authorization Header: '{AuthHeader}'", authHeader ?? "NULL o VACÍO");
                            logger.LogWarning("Authorization Header Length: {Length}", authHeader?.Length ?? 0);
                            logger.LogWarning("Error: {Error}", context.Error ?? "NULL");
                            logger.LogWarning("ErrorDescription: {Description}", context.ErrorDescription ?? "NULL");
                            logger.LogWarning("IsAuthenticated: {IsAuth}", context.HttpContext.User?.Identity?.IsAuthenticated ?? false);
                            logger.LogWarning("Todos los headers: {Headers}", allHeaders);
                            logger.LogWarning("═══════════════════════════════════════════════════");

                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddAuthorization();

            // --- 4. REGISTRO DE SERVICIOS (Inyección de Dependencias) ---
            // Aquí consolidamos todo.

            // 4.A: Nuestro Servicio de Tokens (de la Tarea 30)
            // TokenService requiere IConfiguration y VisioAnalyticaDbContext
            // El contenedor de DI los resolverá automáticamente
            services.AddScoped<ITokenService, TokenService>();

            // 4.B: Nuestro Servicio de IA (¡El tuyo, v4.0!)
            // Registrado como HttpClient para gestión de pool de conexiones
            services.AddHttpClient<IAiSstAnalyzer, GeminiAnalyzer>();

            // 4.C: Nuestro Servicio de Auth (de la Tarea 30)
            services.AddScoped<IAuthService, AuthService>();

            // 4.C.1: Repositorio de Análisis (requerido por AnalysisService)
            services.AddScoped<IAnalysisRepository, AnalysisRepository>();

            // 4.C.2: Almacenamiento de Archivos (requerido por InspectionService y AnalysisService)
            services.AddScoped<IFileStorage, LocalFileStorage>();

            // 4.C.3: Generador de Reportes PDF (requerido por AnalysisOrchestrator)
            services.AddScoped<IPdfReportGenerator, PdfReportGenerator>();

            // 4.C.4: Servicio de Reportes (requerido por AnalysisController)
            services.AddScoped<IReportService, ReportService>();

            // 4.C.5: Servicio de Inicialización de Roles (requerido al iniciar la aplicación)
            services.AddScoped<RoleSeederService>();

            // 4.C.6: Servicio de Empresas Afiliadas (requerido por AffiliatedCompanyController)
            services.AddScoped<IAffiliatedCompanyService, AffiliatedCompanyService>();

            // 4.C.7: Servicio de Gestión de Usuarios (requerido por UserManagementController)
            services.AddScoped<IUserManagementService, UserManagementService>();

            // 4.D: El "cerebro" orquestador de Análisis de Escritura
            services.AddScoped<IAnalysisService, AnalysisService>();

            // 4.J: ¡NUEVO REGISTRO! Servicio de Inspecciones
            services.AddScoped<IInspectionService, InspectionService>();
            
            // 4.J.1: Servicio de optimización de imágenes en servidor
            services.AddScoped<ServerImageOptimizationService>();
            
            // 4.K: ¡NUEVO REGISTRO! Orquestador de Análisis
            services.AddScoped<IAnalysisOrchestrator, AnalysisOrchestrator>();

            // 4.L: ¡NUEVO REGISTRO! Servicio de limpieza de refresh tokens en segundo plano
            services.AddHostedService<RefreshTokenCleanupService>();

            // 4.K: Configuración de Hangfire para análisis en segundo plano
            var connectionString = config.GetConnectionString("LocalSqlServerConnection");
            services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                }));

            services.AddHangfireServer();

            // 4.I: ¡NUEVO REGISTRO! Servicio de Email (Configurable: SMTP o SendGrid)
            var emailProvider = config["Email:Provider"] ?? "Smtp";
            if (emailProvider == "SendGrid")
            {
                // TODO: Implementar SendGridEmailService cuando sea necesario
                // services.AddScoped<IEmailService, SendGridEmailService>();
                // services.AddSingleton<SendGridClient>(sp => 
                //     new SendGridClient(config["Email:SendGrid:ApiKey"]));
                throw new InvalidOperationException("SendGrid aún no está implementado. Usa 'Smtp' para desarrollo.");
            }
            else if (emailProvider == "Smtp")
            {
                services.AddScoped<IEmailService, SmtpEmailService>();
            }
            else
            {
                throw new InvalidOperationException($"Email provider '{emailProvider}' no es válido. Use 'Smtp' o 'SendGrid'.");
            }


            // --- 5. SERVICIOS ESTÁNDAR DE API ---
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            
            // 5.A: Configurar CORS para desarrollo (permitir peticiones desde Swagger y apps móviles)
            services.AddCors(options =>
            {
                options.AddPolicy("DevelopmentCors", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            // 5.B: Configuración de Swagger (con soporte para Auth)
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "VisioAnalytica API", Version = "v1" });

                // Configuración de seguridad Bearer JWT
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    Description = "Ingrese el token JWT (sin la palabra 'Bearer'). Ejemplo: eyJhbGciOiJIUzUxMiIsInR5cCI6IkpXVCJ9..."
                });

                // Aplicar seguridad Bearer a todos los endpoints
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
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
                    }
                });
            });

            return services;
        }
    }
}