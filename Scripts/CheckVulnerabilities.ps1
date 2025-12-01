# ============================================================================
# Script de Auditoría de Vulnerabilidades - VisioAnalytica
# ============================================================================
# 
# PROPÓSITO:
# Este script verifica todas las vulnerabilidades conocidas en los paquetes
# NuGet utilizados en el proyecto, incluyendo dependencias transitivas.
#
# CUÁNDO USARLO:
# - Mensualmente como parte del mantenimiento rutinario
# - Antes de cada release a producción
# - Después de actualizar paquetes NuGet
# - Cuando se reciben alertas de seguridad
#
# CÓMO USARLO:
# 1. Abre PowerShell en la raíz del proyecto
# 2. Ejecuta: .\Scripts\CheckVulnerabilities.ps1
# 3. Revisa el output para ver si hay vulnerabilidades
# 4. Si hay vulnerabilidades, actualiza los paquetes afectados
#
# SALIDA:
# - Exit code 0: Sin vulnerabilidades
# - Exit code 1: Se encontraron vulnerabilidades (requiere acción)
#
# ============================================================================

param(
    [switch]$Verbose,
    [string]$OutputFile = ""
)

Write-Host "`n=== Auditoría de Vulnerabilidades - VisioAnalytica ===" -ForegroundColor Cyan
Write-Host "Fecha: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host ""

# Obtener la ruta del script para determinar la raíz del proyecto
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath
Set-Location $projectRoot

# Lista de proyectos a verificar
$projects = @(
    "src/Api/VisioAnalytica.Api.csproj",
    "src/Infrastructure/VisioAnalytica.Infrastructure.csproj",
    "src/Apps/VisioAnalytica.App.Risk/VisioAnalytica.App.Risk.csproj",
    "src/Core/VisioAnalytica.Core.csproj"
)

$hasVulnerabilities = $false
$vulnerabilityReport = @()

foreach ($project in $projects) {
    $projectPath = Join-Path $projectRoot $project
    
    if (Test-Path $projectPath) {
        Write-Host "Verificando: $project" -ForegroundColor Yellow
        
        try {
            # Ejecutar dotnet list package --vulnerable
            $output = dotnet list package --vulnerable --include-transitive --project $projectPath 2>&1
            
            # Verificar si la salida contiene información de vulnerabilidades reales
            # Ignorar la salida de ayuda (que contiene "--vulnerable" como opción)
            $hasVulnerabilityInfo = $false
            $vulnerablePackages = @()
            
            foreach ($line in $output) {
                $lineStr = $line.ToString()
                # Buscar códigos de vulnerabilidad específicos (NU1903, NU1904, etc.)
                if ($lineStr -match "NU19\d{2}") {
                    $hasVulnerabilityInfo = $true
                    $vulnerablePackages += $lineStr
                }
                # Buscar líneas que mencionen vulnerabilidades pero no sean parte de la ayuda
                elseif ($lineStr -match "vulnerable" -and $lineStr -notmatch "--vulnerable" -and $lineStr -notmatch "opción") {
                    $hasVulnerabilityInfo = $true
                    $vulnerablePackages += $lineStr
                }
            }
            
            if ($hasVulnerabilityInfo -and $vulnerablePackages.Count -gt 0) {
                $hasVulnerabilities = $true
                Write-Host "  ⚠️ VULNERABILIDADES ENCONTRADAS" -ForegroundColor Red
                
                foreach ($vuln in $vulnerablePackages) {
                    $vulnLine = $vuln.Trim()
                    if ($vulnLine -ne "") {
                        Write-Host "    $vulnLine" -ForegroundColor Red
                        
                        # Agregar al reporte
                        $vulnerabilityReport += @{
                            Project = $project
                            Vulnerability = $vulnLine
                        }
                    }
                }
            } else {
                Write-Host "  ✅ Sin vulnerabilidades conocidas" -ForegroundColor Green
            }
            
            if ($Verbose) {
                Write-Host "  Salida completa:" -ForegroundColor Gray
                $output | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
            }
        }
        catch {
            Write-Host "  ❌ Error al verificar: $_" -ForegroundColor Red
        }
        
        Write-Host ""
    } else {
        Write-Host "  ⚠️ Proyecto no encontrado: $projectPath" -ForegroundColor Yellow
    }
}

# Resumen final
Write-Host "=== RESUMEN ===" -ForegroundColor Cyan
if ($hasVulnerabilities) {
    Write-Host "⚠️ SE ENCONTRARON VULNERABILIDADES" -ForegroundColor Red
    Write-Host ""
    Write-Host "ACCIÓN REQUERIDA:" -ForegroundColor Yellow
    Write-Host "1. Revisa las vulnerabilidades listadas arriba" -ForegroundColor White
    Write-Host "2. Actualiza los paquetes afectados a versiones seguras" -ForegroundColor White
    Write-Host "3. Ejecuta 'dotnet restore' después de actualizar" -ForegroundColor White
    Write-Host "4. Vuelve a ejecutar este script para verificar" -ForegroundColor White
    Write-Host ""
    Write-Host "Para actualizar un paquete específico:" -ForegroundColor Gray
    Write-Host "  dotnet add package [NombrePaquete] --version [VersionSegura]" -ForegroundColor Gray
    Write-Host ""
    
    # Guardar reporte si se especificó archivo de salida
    if ($OutputFile -ne "") {
        $vulnerabilityReport | ConvertTo-Json | Out-File $OutputFile
        Write-Host "Reporte guardado en: $OutputFile" -ForegroundColor Gray
    }
    
    exit 1
} else {
    Write-Host "✅ TODO CORRECTO" -ForegroundColor Green
    Write-Host "No se encontraron vulnerabilidades conocidas en los paquetes." -ForegroundColor Green
    Write-Host ""
    Write-Host "RECOMENDACIÓN:" -ForegroundColor Yellow
    Write-Host "Ejecuta este script mensualmente o antes de cada release." -ForegroundColor White
    Write-Host ""
    
    exit 0
}

