# Script de prueba para verificar el endpoint PerformSstAnalysis
param(
    [string]$ApiUrl = "http://localhost:5170",
    [string]$Email = "juancarlosfajardo@outlook.com",
    [string]$Password = "Jk4031023@"
)

Write-Host "=== Prueba de PerformSstAnalysis - VisioAnalytica ===" -ForegroundColor Cyan
Write-Host ""

# Paso 1: Hacer login para obtener el token JWT
Write-Host "1. Obteniendo token JWT mediante login..." -ForegroundColor Yellow
Write-Host "   Email: $Email" -ForegroundColor Gray
Write-Host ""

try {
    $loginBody = @{
        email = $Email
        password = $Password
    } | ConvertTo-Json

    $headers = @{
        "Content-Type" = "application/json"
    }

    $loginResponse = Invoke-RestMethod -Uri "$ApiUrl/api/auth/login" -Method Post -Body $loginBody -Headers $headers -ErrorAction Stop

    if (-not $loginResponse.token) {
        Write-Host "   ERROR: No se recibio token en la respuesta de login" -ForegroundColor Red
        exit 1
    }

    $token = $loginResponse.token
    Write-Host "   OK: Token JWT obtenido exitosamente" -ForegroundColor Green
    Write-Host "   Token preview: $($token.Substring(0, [Math]::Min(50, $token.Length)))..." -ForegroundColor Gray
    Write-Host ""

} catch {
    $errorMessage = $_.Exception.Message
    if ($_.ErrorDetails.Message) {
        $errorMessage = $_.ErrorDetails.Message
    }
    
    Write-Host "   ERROR en el login:" -ForegroundColor Red
    Write-Host "   $errorMessage" -ForegroundColor Red
    exit 1
}

# Paso 2: Crear una imagen Base64 de ejemplo (un PNG de 1x1 pixel transparente)
# Esto es solo para demostración. En producción, usarías una imagen real.
$sampleImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="

Write-Host "2. Llamando al endpoint PerformSstAnalysis..." -ForegroundColor Yellow
Write-Host "   Endpoint: $ApiUrl/api/v1/Analysis/PerformSstAnalysis" -ForegroundColor Gray
Write-Host ""

try {
    # Crear el body de la solicitud según AnalysisRequestDto
    $analysisBody = @{
        imageBase64 = $sampleImageBase64
        # promptTemplateId = $null  # Opcional
        # customPrompt = $null      # Opcional
    } | ConvertTo-Json

    # Headers con el token JWT
    $authHeaders = @{
        "Content-Type" = "application/json"
        "Authorization" = "Bearer $token"
    }

    Write-Host "   Enviando solicitud..." -ForegroundColor Gray
    
    $analysisResponse = Invoke-RestMethod -Uri "$ApiUrl/api/v1/Analysis/PerformSstAnalysis" -Method Post -Body $analysisBody -Headers $authHeaders -ErrorAction Stop

    Write-Host "   OK: Analisis completado exitosamente!" -ForegroundColor Green
    Write-Host ""
    Write-Host "   Resultado:" -ForegroundColor Cyan
    Write-Host "   $($analysisResponse | ConvertTo-Json -Depth 5)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "=== Prueba completada exitosamente ===" -ForegroundColor Green

} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $errorMessage = $_.Exception.Message
    
    if ($_.ErrorDetails.Message) {
        $errorMessage = $_.ErrorDetails.Message
    }
    
    Write-Host "   ERROR en PerformSstAnalysis:" -ForegroundColor Red
    Write-Host "   Status Code: $statusCode" -ForegroundColor Red
    Write-Host "   Mensaje: $errorMessage" -ForegroundColor Red
    Write-Host ""
    
    if ($statusCode -eq 401) {
        Write-Host "   Causa: Token JWT invalido o expirado" -ForegroundColor Yellow
        Write-Host "   Solucion: Verifica que el token sea valido y no haya expirado" -ForegroundColor Yellow
    } elseif ($statusCode -eq 403) {
        Write-Host "   Causa: El token no contiene 'uid' o 'org_id' validos" -ForegroundColor Yellow
        Write-Host "   Solucion: Verifica que el usuario tenga una organizacion asignada" -ForegroundColor Yellow
    } elseif ($statusCode -eq 400) {
        Write-Host "   Causa: La solicitud es invalida (posiblemente la imagen Base64)" -ForegroundColor Yellow
        Write-Host "   Solucion: Verifica que la imagen Base64 sea valida" -ForegroundColor Yellow
    }
    
    exit 1
}

