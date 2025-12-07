# Script de prueba para verificar el login
param(
    [string]$ApiUrl = "http://localhost:5170",
    [string]$Email = "juancarlosfajardo@outlook.com",
    [string]$Password = "Jk4031023@"
)

Write-Host "=== Prueba de Login - VisioAnalytica ===" -ForegroundColor Cyan
Write-Host ""

# Verificar que la API este corriendo
Write-Host "1. Verificando que la API este corriendo..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$ApiUrl/swagger/index.html" -Method Get -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "   OK: API esta corriendo en $ApiUrl" -ForegroundColor Green
    }
} catch {
    Write-Host "   ERROR: La API no esta corriendo en $ApiUrl" -ForegroundColor Red
    Write-Host "   Por favor, ejecuta: cd src/Api; dotnet run --launch-profile http" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Probar el login
Write-Host "2. Probando login con:" -ForegroundColor Yellow
Write-Host "   Email: $Email" -ForegroundColor Gray
Write-Host "   Password: $Password" -ForegroundColor Gray
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

    Write-Host "   OK: Login exitoso!" -ForegroundColor Green
    Write-Host ""
    Write-Host "   Detalles del usuario:" -ForegroundColor Cyan
    Write-Host "   - Email: $($loginResponse.email)" -ForegroundColor Gray
    Write-Host "   - Nombre: $($loginResponse.firstName)" -ForegroundColor Gray
    $tokenPreview = $loginResponse.token.Substring(0, [Math]::Min(50, $loginResponse.token.Length))
    Write-Host "   - Token recibido: $tokenPreview..." -ForegroundColor Gray
    Write-Host "   - MustChangePassword: $($loginResponse.mustChangePassword)" -ForegroundColor Gray
    
    if ($loginResponse.roles) {
        $rolesStr = $loginResponse.roles -join ', '
        Write-Host "   - Roles: $rolesStr" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "=== Prueba completada exitosamente ===" -ForegroundColor Green
    
} catch {
    $errorMessage = $_.Exception.Message
    if ($_.ErrorDetails.Message) {
        $errorMessage = $_.ErrorDetails.Message
    }
    
    Write-Host "   ERROR en el login:" -ForegroundColor Red
    Write-Host "   $errorMessage" -ForegroundColor Red
    Write-Host ""
    
    # Si es un error 401, el usuario o contraseña pueden ser incorrectos
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "   Posibles causas:" -ForegroundColor Yellow
        Write-Host "   - El usuario no existe en la base de datos" -ForegroundColor Gray
        Write-Host "   - La contraseña es incorrecta" -ForegroundColor Gray
        Write-Host "   - El usuario esta desactivado" -ForegroundColor Gray
    }
    
    exit 1
}
