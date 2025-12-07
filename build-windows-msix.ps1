# Script para generar un paquete MSIX de la aplicación MAUI para Windows
# Esto genera un paquete instalable que incluye todas las capacidades del manifest

param(
    [string]$Configuration = "Debug"
)

Write-Host "=== Generación de Paquete MSIX para Windows ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "1. Limpiando proyecto..." -ForegroundColor Yellow
dotnet clean src/Apps/VisioAnalytica.App.Risk/VisioAnalytica.App.Risk.csproj --configuration $Configuration 2>&1 | Out-Null
Write-Host "   OK: Proyecto limpiado" -ForegroundColor Green
Write-Host ""

Write-Host "2. Publicando y generando paquete MSIX..." -ForegroundColor Yellow
Write-Host "   Esto puede tardar varios minutos..." -ForegroundColor Gray
Write-Host ""

try {
    $publishOutput = dotnet publish `
        src/Apps/VisioAnalytica.App.Risk/VisioAnalytica.App.Risk.csproj `
        -f net10.0-windows10.0.19041.0 `
        -c $Configuration `
        /p:WindowsPackageType=Msix `
        /p:GenerateAppInstallerFile=false `
        2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "   OK: Paquete MSIX generado correctamente" -ForegroundColor Green
        Write-Host ""
        
        # Buscar el archivo MSIX generado
        $msixPath = Get-ChildItem -Path "src/Apps/VisioAnalytica.App.Risk/bin/$Configuration/net10.0-windows10.0.19041.0" -Filter "*.msix" -Recurse -ErrorAction SilentlyContinue | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -First 1

        if ($msixPath) {
            Write-Host "3. Paquete MSIX generado:" -ForegroundColor Yellow
            Write-Host "   $($msixPath.FullName)" -ForegroundColor Gray
            Write-Host ""
            Write-Host "   Tamaño: $([math]::Round($msixPath.Length / 1MB, 2)) MB" -ForegroundColor Gray
            Write-Host ""
            
            $install = Read-Host "¿Deseas instalar el paquete ahora? (S/N)"
            if ($install -eq "S" -or $install -eq "s") {
                Write-Host ""
                Write-Host "4. Instalando paquete..." -ForegroundColor Yellow
                try {
                    Add-AppxPackage -Path $msixPath.FullName -ErrorAction Stop
                    Write-Host "   OK: Aplicación instalada correctamente" -ForegroundColor Green
                    Write-Host ""
                    Write-Host "=== Instalación completada ===" -ForegroundColor Green
                }
                catch {
                    Write-Host "   ERROR: No se pudo instalar: $($_.Exception.Message)" -ForegroundColor Red
                    Write-Host ""
                    Write-Host "   Instala manualmente haciendo doble clic en:" -ForegroundColor Yellow
                    Write-Host "   $($msixPath.FullName)" -ForegroundColor Gray
                }
            }
            else {
                Write-Host ""
                Write-Host "=== Paquete generado ===" -ForegroundColor Green
                Write-Host "Instala el paquete haciendo doble clic en:" -ForegroundColor Cyan
                Write-Host "$($msixPath.FullName)" -ForegroundColor Gray
            }
        }
        else {
            Write-Host "   ADVERTENCIA: No se encontró el archivo .msix generado" -ForegroundColor Yellow
            Write-Host "   Busca manualmente en: src/Apps/VisioAnalytica.App.Risk/bin/$Configuration/net10.0-windows10.0.19041.0" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "   ERROR: La generación del paquete falló" -ForegroundColor Red
        $publishOutput | Select-String -Pattern "error" | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
        exit 1
    }
}
catch {
    Write-Host "   ERROR: Error al generar el paquete: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

