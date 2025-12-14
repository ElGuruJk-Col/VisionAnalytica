# Script para probar el endpoint de login
# Verifica que el endpoint responda correctamente sin error 405

$baseUrl = "http://localhost:5170"
$loginEndpoint = "$baseUrl/api/Auth/login"

Write-Host "=== Prueba del Endpoint de Login ===" -ForegroundColor Cyan
Write-Host ""

# Verificar que la API esté corriendo
Write-Host "1. Verificando que la API esté disponible..." -ForegroundColor Yellow
try {
    $healthCheck = Invoke-WebRequest -Uri "$baseUrl/swagger" -Method GET -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
    Write-Host "   OK: API está disponible" -ForegroundColor Green
}
catch {
    Write-Host "   ERROR: La API no está disponible en $baseUrl" -ForegroundColor Red
    Write-Host "   Asegúrate de que la API esté ejecutándose" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Probar el endpoint de login
Write-Host "2. Probando endpoint de login..." -ForegroundColor Yellow
Write-Host "   URL: $loginEndpoint" -ForegroundColor Gray
Write-Host "   Método: POST" -ForegroundColor Gray
Write-Host ""

$loginBody = @{
    email = "juancarlosfajardo@outlook.com"
    password = "Jk4031023@"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri $loginEndpoint -Method POST -Body $loginBody -ContentType "application/json" -TimeoutSec 10 -ErrorAction Stop
    
    Write-Host "   OK: Login exitoso" -ForegroundColor Green
    Write-Host ""
    Write-Host "   Respuesta:" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 3 | Write-Host
    
    if ($response.token) {
        Write-Host ""
        Write-Host "   Token recibido: $($response.token.Substring(0, [Math]::Min(50, $response.token.Length)))..." -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "=== Prueba completada exitosamente ===" -ForegroundColor Green
    exit 0
}
catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $statusDescription = $_.Exception.Response.StatusCode
    
    Write-Host "   ERROR: La petición falló" -ForegroundColor Red
    Write-Host "   Código de estado: $statusCode ($statusDescription)" -ForegroundColor Red
    
    if ($statusCode -eq 405) {
        Write-Host ""
        Write-Host "   El error 405 (Method Not Allowed) indica que:" -ForegroundColor Yellow
        Write-Host "   - El endpoint no acepta el método POST" -ForegroundColor Yellow
        Write-Host "   - O hay una redirección que cambia el método HTTP" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "   Verifica que el middleware de redirección HTTPS permita /api/Auth" -ForegroundColor Yellow
    }
    elseif ($statusCode -eq 401) {
        Write-Host ""
        Write-Host "   El error 401 (Unauthorized) es esperado si las credenciales son incorrectas" -ForegroundColor Yellow
        Write-Host "   Esto significa que el endpoint está funcionando correctamente" -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "   Mensaje de error: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Intentar obtener más detalles del error
    try {
        $errorStream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($errorStream)
        $errorBody = $reader.ReadToEnd()
        if ($errorBody) {
            Write-Host ""
            Write-Host "   Detalles del error:" -ForegroundColor Yellow
            Write-Host "   $errorBody" -ForegroundColor Gray
        }
    }
    catch {
        # No se pudo leer el cuerpo del error
    }
    
    exit 1
}

