# Ejemplos de Comandos cURL para la API de VisioAnalytica

Este documento contiene ejemplos de cómo usar `curl` para interactuar con la API de VisioAnalytica.

## Requisitos Previos

1. La API debe estar corriendo en `http://localhost:5170` (o la URL que corresponda)
2. Tener `curl` instalado (disponible en Windows 10+, Linux, macOS)

## 1. Login y Obtención de Token JWT

### Windows (PowerShell)
```powershell
$loginResponse = Invoke-RestMethod -Uri "http://localhost:5170/api/auth/login" -Method Post -Body (@{email="juancarlosfajardo@outlook.com"; password="Jk4031023@"} | ConvertTo-Json) -ContentType "application/json"
$token = $loginResponse.token
Write-Host "Token: $token"
```

### Linux/macOS (curl)
```bash
# Hacer login y guardar el token
TOKEN=$(curl -s -X POST "http://localhost:5170/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"juancarlosfajardo@outlook.com","password":"Jk4031023@"}' \
  | jq -r '.token')

echo "Token: $TOKEN"
```

### Windows (curl desde CMD o Git Bash)
```bash
# Hacer login y guardar el token (requiere jq o procesamiento manual)
curl -X POST "http://localhost:5170/api/auth/login" ^
  -H "Content-Type: application/json" ^
  -d "{\"email\":\"juancarlosfajardo@outlook.com\",\"password\":\"Jk4031023@\"}"
```

## 2. Llamar al Endpoint PerformSstAnalysis

**IMPORTANTE:** Este endpoint requiere autenticación. Debes incluir el token JWT en el header `Authorization`.

### Windows (PowerShell)
```powershell
# Primero obtén el token (ver sección 1)
$token = "TU_TOKEN_AQUI"

# Crear el body de la solicitud
$body = @{
    imageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
} | ConvertTo-Json

# Llamar al endpoint
Invoke-RestMethod -Uri "http://localhost:5170/api/v1/Analysis/PerformSstAnalysis" `
  -Method Post `
  -Body $body `
  -Headers @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $token"
  }
```

### Linux/macOS (curl)
```bash
# Usar el token obtenido en la sección 1
TOKEN="TU_TOKEN_AQUI"

curl -X POST "http://localhost:5170/api/v1/Analysis/PerformSstAnalysis" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "imageBase64": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
  }'
```

### Windows (curl desde CMD)
```cmd
curl -X POST "http://localhost:5170/api/v1/Analysis/PerformSstAnalysis" ^
  -H "Content-Type: application/json" ^
  -H "Authorization: Bearer TU_TOKEN_AQUI" ^
  -d "{\"imageBase64\":\"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==\"}"
```

## 3. Ejemplo Completo: Script de una Línea (PowerShell)

```powershell
# Obtener token y llamar al endpoint en un solo script
$token = (Invoke-RestMethod -Uri "http://localhost:5170/api/auth/login" -Method Post -Body (@{email="juancarlosfajardo@outlook.com"; password="Jk4031023@"} | ConvertTo-Json) -ContentType "application/json").token; Invoke-RestMethod -Uri "http://localhost:5170/api/v1/Analysis/PerformSstAnalysis" -Method Post -Body (@{imageBase64="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="} | ConvertTo-Json) -Headers @{"Authorization"="Bearer $token"; "Content-Type"="application/json"}
```

## 4. Solución al Error 401 Unauthorized

Si recibes un error `401 Unauthorized` al llamar a `/api/v1/Analysis/PerformSstAnalysis`, verifica:

1. **¿Incluiste el header `Authorization`?**
   ```bash
   # ❌ INCORRECTO (sin token)
   curl -X POST "http://localhost:5170/api/v1/Analysis/PerformSstAnalysis" \
     -H "Content-Type: application/json" \
     -d '{"imageBase64":"..."}'
   
   # ✅ CORRECTO (con token)
   curl -X POST "http://localhost:5170/api/v1/Analysis/PerformSstAnalysis" \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer TU_TOKEN_AQUI" \
     -d '{"imageBase64":"..."}'
   ```

2. **¿El token es válido y no ha expirado?**
   - Los tokens JWT tienen una expiración (típicamente 1 hora)
   - Si el token expiró, debes hacer login nuevamente

3. **¿El formato del header es correcto?**
   - Debe ser: `Authorization: Bearer <token>`
   - No debe tener espacios extra o caracteres especiales

## 5. Convertir una Imagen Real a Base64

### Windows (PowerShell)
```powershell
# Convertir imagen a Base64
$imageBytes = [System.IO.File]::ReadAllBytes("ruta\a\tu\imagen.jpg")
$base64 = [Convert]::ToBase64String($imageBytes)
Write-Host $base64
```

### Linux/macOS
```bash
# Convertir imagen a Base64
base64 -i ruta/a/tu/imagen.jpg
```

## 6. Otros Endpoints Protegidos

Todos los endpoints que requieren `[Authorize]` necesitan el token JWT:

- `POST /api/v1/Analysis/PerformSstAnalysis` - Análisis de imagen SST
- `GET /api/v1/Analysis/history` - Historial de inspecciones
- `GET /api/v1/Analysis/{inspectionId}` - Detalles de inspección
- `POST /api/auth/change-password` - Cambiar contraseña
- `GET /api/UserManagement/users` - Listar usuarios
- `POST /api/UserManagement/users` - Crear usuario
- Y otros...

## Notas Adicionales

- El token JWT contiene información del usuario (uid, org_id, roles, etc.)
- El endpoint `PerformSstAnalysis` valida que el token contenga `uid` y `org_id`
- Si el token no tiene estos claims, recibirás un error `403 Forbidden` en lugar de `401 Unauthorized`

