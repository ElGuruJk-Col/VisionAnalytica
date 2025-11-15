# Script de Prueba para Capítulo 6 - Frontend MAUI
# Este script ayuda a verificar que todo esté listo para probar la app MAUI

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Prueba del Capítulo 6 - Frontend MAUI" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar que la API esté corriendo
Write-Host "1. Verificando si la API está corriendo..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5170/swagger" -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200) {
        Write-Host "   ✅ API está corriendo en http://localhost:5170" -ForegroundColor Green
    }
} catch {
    Write-Host "   ❌ API NO está corriendo en http://localhost:5170" -ForegroundColor Red
    Write-Host "   💡 Ejecuta: cd src/Api && dotnet run" -ForegroundColor Yellow
    Write-Host ""
}

# 2. Verificar configuración de la API
Write-Host ""
Write-Host "2. Verificando configuración de la API..." -ForegroundColor Yellow
$appsettingsPath = "src/Api/appsettings.json"
if (Test-Path $appsettingsPath) {
    $appsettings = Get-Content $appsettingsPath | ConvertFrom-Json
    
    $hasConnectionString = $appsettings.ConnectionStrings.LocalSqlServerConnection -ne ""
    $hasJwtKey = $appsettings.Jwt.Key -ne ""
    $hasGeminiKey = $appsettings.Gemini.ApiKey -ne ""
    $hasPrompt = $appsettings.AiPrompts.MasterSst -ne ""
    
    if ($hasConnectionString) {
        Write-Host "   ✅ Cadena de conexión configurada" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Cadena de conexión NO configurada" -ForegroundColor Yellow
    }
    
    if ($hasJwtKey) {
        Write-Host "   ✅ Clave JWT configurada" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Clave JWT NO configurada" -ForegroundColor Yellow
    }
    
    if ($hasGeminiKey) {
        Write-Host "   ✅ API Key de Gemini configurada" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  API Key de Gemini NO configurada (necesaria para análisis real)" -ForegroundColor Yellow
    }
    
    if ($hasPrompt) {
        Write-Host "   ✅ Prompt maestro configurado" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Prompt maestro NO configurado" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ❌ No se encontró appsettings.json" -ForegroundColor Red
}

# 3. Verificar configuración de ApiClient
Write-Host ""
Write-Host "3. Verificando configuración de ApiClient..." -ForegroundColor Yellow
$apiClientPath = "src/Apps/VisioAnalytica.App.Risk/Services/ApiClient.cs"
if (Test-Path $apiClientPath) {
    $apiClientContent = Get-Content $apiClientPath -Raw
    if ($apiClientContent -match 'BaseUrl = "http://localhost:5170"') {
        Write-Host "   ✅ ApiClient configurado para http://localhost:5170" -ForegroundColor Green
        Write-Host "   💡 Si pruebas en dispositivo físico, cambia a la IP de tu máquina" -ForegroundColor Yellow
    } else {
        Write-Host "   ⚠️  Verifica la URL en ApiClient.cs" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ❌ No se encontró ApiClient.cs" -ForegroundColor Red
}

# 4. Verificar que el proyecto MAUI compile
Write-Host ""
Write-Host "4. Verificando compilación del proyecto MAUI..." -ForegroundColor Yellow
$mauiProjectPath = "src/Apps/VisioAnalytica.App.Risk/VisioAnalytica.App.Risk.csproj"
if (Test-Path $mauiProjectPath) {
    Write-Host "   Compilando proyecto MAUI..." -ForegroundColor Gray
    Push-Location "src/Apps/VisioAnalytica.App.Risk"
    try {
        $buildOutput = dotnet build -f net9.0-windows10.0.19041.0 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ Proyecto MAUI compila correctamente" -ForegroundColor Green
        } else {
            Write-Host "   ❌ Errores de compilación encontrados:" -ForegroundColor Red
            $buildOutput | Select-String "error" | ForEach-Object { Write-Host "      $_" -ForegroundColor Red }
        }
    } catch {
        Write-Host "   ⚠️  No se pudo compilar (puede requerir Visual Studio)" -ForegroundColor Yellow
    }
    Pop-Location
} else {
    Write-Host "   ❌ No se encontró el proyecto MAUI" -ForegroundColor Red
}

# 5. Resumen y próximos pasos
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Resumen y Próximos Pasos" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Para probar la app MAUI:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Asegúrate de que la API esté corriendo:" -ForegroundColor White
Write-Host "   cd src/Api" -ForegroundColor Gray
Write-Host "   dotnet run" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Abre Visual Studio 2022 y:" -ForegroundColor White
Write-Host "   - Abre: src/VisioAnalytica.sln" -ForegroundColor Gray
Write-Host "   - Establece VisioAnalytica.App.Risk como proyecto de inicio" -ForegroundColor Gray
Write-Host "   - Selecciona Windows como plataforma" -ForegroundColor Gray
Write-Host "   - Presiona F5 para ejecutar" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Flujo de prueba:" -ForegroundColor White
Write-Host "   - Registra un nuevo usuario" -ForegroundColor Gray
Write-Host "   - Inicia sesión" -ForegroundColor Gray
Write-Host "   - Captura una foto" -ForegroundColor Gray
Write-Host "   - Analiza la imagen" -ForegroundColor Gray
Write-Host "   - Revisa los resultados" -ForegroundColor Gray
Write-Host ""
Write-Host "📖 Para más detalles, consulta: Documentacion/Guia-Prueba-Capitulo6.md" -ForegroundColor Cyan
Write-Host ""

