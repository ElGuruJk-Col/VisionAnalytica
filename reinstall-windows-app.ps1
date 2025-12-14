# Script para desinstalar y reinstalar la aplicación MAUI en Windows
# Este script es necesario cuando se cambian capacidades en Package.appxmanifest

param(
    [switch]$SkipBuild = $false
)

Write-Host "=== Reinstalación de VisioAnalytica App en Windows ===" -ForegroundColor Cyan
Write-Host ""

# Obtener el nombre del paquete desde el .csproj o usar un nombre por defecto
$packageName = "VisioAnalytica.App.Risk"
$appName = "VisioAnalytica App Risk"

Write-Host "1. Buscando aplicación instalada..." -ForegroundColor Yellow

# Buscar el paquete instalado
$installedPackage = Get-AppxPackage | Where-Object { $_.Name -like "*$packageName*" -or $_.Name -like "*VisioAnalytica*" }

if ($installedPackage) {
    Write-Host "   Aplicación encontrada: $($installedPackage.Name)" -ForegroundColor Gray
    Write-Host "   Versión: $($installedPackage.Version)" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "2. Desinstalando aplicación..." -ForegroundColor Yellow
    try {
        Remove-AppxPackage -Package $installedPackage.PackageFullName -ErrorAction Stop
        Write-Host "   OK: Aplicación desinstalada correctamente" -ForegroundColor Green
    }
    catch {
        Write-Host "   ERROR: No se pudo desinstalar la aplicación: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "   Intentando con método alternativo..." -ForegroundColor Yellow
        
        # Método alternativo: usar el nombre del paquete
        try {
            Get-AppxPackage | Where-Object { $_.Name -like "*$packageName*" } | Remove-AppxPackage
            Write-Host "   OK: Aplicación desinstalada con método alternativo" -ForegroundColor Green
        }
        catch {
            Write-Host "   ERROR: No se pudo desinstalar. Por favor, desinstala manualmente desde Configuración > Aplicaciones" -ForegroundColor Red
            Write-Host "   Continuando con la compilación..." -ForegroundColor Yellow
        }
    }
}
else {
    Write-Host "   No se encontró ninguna versión instalada de la aplicación" -ForegroundColor Gray
    Write-Host ""
}

Write-Host ""

# Limpiar y reconstruir
if (-not $SkipBuild) {
    Write-Host "3. Limpiando proyecto..." -ForegroundColor Yellow
    try {
        dotnet clean src/Apps/VisioAnalytica.App.Risk/VisioAnalytica.App.Risk.csproj --configuration Debug 2>&1 | Out-Null
        Write-Host "   OK: Proyecto limpiado" -ForegroundColor Green
    }
    catch {
        Write-Host "   ADVERTENCIA: Error al limpiar: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "4. Reconstruyendo proyecto..." -ForegroundColor Yellow
    try {
        $buildOutput = dotnet build src/Apps/VisioAnalytica.App.Risk/VisioAnalytica.App.Risk.csproj --configuration Debug 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   OK: Proyecto compilado correctamente" -ForegroundColor Green
        }
        else {
            Write-Host "   ERROR: La compilación falló" -ForegroundColor Red
            $buildOutput | Select-String -Pattern "error" | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
            exit 1
        }
    }
    catch {
        Write-Host "   ERROR: Error al compilar: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "3. Omitiendo compilación (--SkipBuild especificado)" -ForegroundColor Yellow
}

Write-Host ""

# Buscar el archivo .msix o .appx generado
Write-Host "5. Buscando paquete de instalación..." -ForegroundColor Yellow

# Buscar en diferentes ubicaciones posibles
$searchPaths = @(
    "src/Apps/VisioAnalytica.App.Risk/bin/Debug",
    "src/Apps/VisioAnalytica.App.Risk/bin/Debug/net10.0-windows10.0.19041.0",
    "src/Apps/VisioAnalytica.App.Risk/bin/Debug/net10.0-windows10.0.19041.0/AppPackages",
    "src/Apps/VisioAnalytica.App.Risk/bin/Debug/net10.0-windows10.0.19041.0/publish"
)

$msixPath = $null
$appxPath = $null

foreach ($searchPath in $searchPaths) {
    if (Test-Path $searchPath) {
        $foundMsix = Get-ChildItem -Path $searchPath -Filter "*.msix" -Recurse -ErrorAction SilentlyContinue | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -First 1
        
        $foundAppx = Get-ChildItem -Path $searchPath -Filter "*.appx" -Recurse -ErrorAction SilentlyContinue | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -First 1
        
        if ($foundMsix) { $msixPath = $foundMsix.FullName }
        if ($foundAppx) { $appxPath = $foundAppx.FullName }
        
        if ($msixPath -or $appxPath) { break }
    }
}

$packagePath = if ($msixPath) { $msixPath } elseif ($appxPath) { $appxPath } else { $null }

if ($packagePath) {
    Write-Host "   Paquete encontrado: $packagePath" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "6. Instalando aplicación..." -ForegroundColor Yellow
    try {
        # Para Windows 10/11, usar Add-AppxPackage
        Add-AppxPackage -Path $packagePath -ErrorAction Stop
        Write-Host "   OK: Aplicación instalada correctamente" -ForegroundColor Green
        Write-Host ""
        Write-Host "=== Reinstalación completada exitosamente ===" -ForegroundColor Green
        Write-Host ""
        Write-Host "La aplicación está lista para usar. Verifica que los permisos de cámara estén habilitados en:" -ForegroundColor Cyan
        Write-Host "Configuración > Privacidad y seguridad > Cámara" -ForegroundColor Gray
    }
    catch {
        Write-Host "   ERROR: No se pudo instalar: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "   Solución alternativa:" -ForegroundColor Yellow
        Write-Host "   1. Abre el Explorador de archivos" -ForegroundColor Gray
        Write-Host "   2. Navega a: $packagePath" -ForegroundColor Gray
        Write-Host "   3. Haz doble clic en el archivo .msix/.appx para instalarlo manualmente" -ForegroundColor Gray
        exit 1
    }
}
else {
    Write-Host "   No se encontró ningún paquete .msix o .appx" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   El proyecto está configurado con WindowsPackageType=None (modo desarrollo)" -ForegroundColor Gray
    Write-Host "   Para ejecutar la aplicación directamente sin empaquetar:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   dotnet run --project src/Apps/VisioAnalytica.App.Risk/VisioAnalytica.App.Risk.csproj -f net10.0-windows10.0.19041.0" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   O para generar un paquete MSIX, ejecuta:" -ForegroundColor Cyan
    Write-Host "   dotnet publish src/Apps/VisioAnalytica.App.Risk/VisioAnalytica.App.Risk.csproj -f net10.0-windows10.0.19041.0 -c Debug /p:WindowsPackageType=Msix" -ForegroundColor Gray
    Write-Host ""
    
    $runDirectly = Read-Host "¿Deseas ejecutar la aplicación directamente ahora? (S/N)"
    if ($runDirectly -eq "S" -or $runDirectly -eq "s") {
        Write-Host ""
        Write-Host "6. Ejecutando aplicación..." -ForegroundColor Yellow
        Write-Host "   (Presiona Ctrl+C para detener la aplicación)" -ForegroundColor Gray
        Write-Host ""
        dotnet run --project src/Apps/VisioAnalytica.App.Risk/VisioAnalytica.App.Risk.csproj -f net10.0-windows10.0.19041.0
    }
    else {
        Write-Host ""
        Write-Host "=== Proceso completado ===" -ForegroundColor Green
        Write-Host "La aplicación está compilada y lista para ejecutar." -ForegroundColor Cyan
    }
}

