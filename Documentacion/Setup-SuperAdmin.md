# Guía: Crear el Primer SuperAdmin

## 📋 Descripción

Este documento explica cómo crear el primer usuario **SuperAdmin** en el sistema VisioAnalytica. El SuperAdmin es necesario para gestionar organizaciones y usuarios del sistema.

## ⚠️ Importante

- **Solo puedes crear un SuperAdmin si no existe ninguno en el sistema**
- Una vez creado el primer SuperAdmin, el endpoint se deshabilita automáticamente por seguridad
- El SuperAdmin debe cambiar su contraseña después del primer login

## 🚀 Método 1: Usando el Endpoint de Setup (Recomendado)

### Paso 1: Verificar el estado del sistema

Antes de crear el SuperAdmin, verifica si el sistema ya está inicializado:

```bash
GET http://localhost:7000/api/setup/check-status
```

**Respuesta si NO está inicializado:**
```json
{
  "isInitialized": false,
  "hasSuperAdmin": false,
  "roleExists": true,
  "organizationExists": false,
  "message": "El sistema no está inicializado. Puedes usar /api/setup/initialize-superadmin para crear el primer SuperAdmin."
}
```

### Paso 2: Crear el SuperAdmin

**Endpoint:** `POST /api/setup/initialize-superadmin`

**Headers:**
```
Content-Type: application/json
```

**Body (JSON):**
```json
{
  "email": "admin@visioanalytica.com",
  "password": "TempPassword123!@#",
  "firstName": "Super",
  "lastName": "Administrator"
}
```

**Requisitos de contraseña:**
- Mínimo 8 caracteres
- Debe contener al menos un dígito
- Debe contener al menos una letra minúscula
- Debe contener al menos una letra mayúscula
- Debe contener al menos un carácter no alfanumérico (!@#$%^&*)

### Ejemplos de uso

#### Usando cURL:
```bash
curl -X POST http://localhost:7000/api/setup/initialize-superadmin \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@visioanalytica.com",
    "password": "TempPassword123!@#",
    "firstName": "Super",
    "lastName": "Administrator"
  }'
```

#### Usando PowerShell:
```powershell
$body = @{
    email = "admin@visioanalytica.com"
    password = "TempPassword123!@#"
    firstName = "Super"
    lastName = "Administrator"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7000/api/setup/initialize-superadmin" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
```

#### Usando Postman/Thunder Client:
1. Método: `POST`
2. URL: `http://localhost:7000/api/setup/initialize-superadmin`
3. Headers: `Content-Type: application/json`
4. Body (raw JSON):
```json
{
  "email": "admin@visioanalytica.com",
  "password": "TempPassword123!@#",
  "firstName": "Super",
  "lastName": "Administrator"
}
```

### Respuesta exitosa:
```json
{
  "message": "SuperAdmin creado exitosamente.",
  "userId": "guid-del-usuario",
  "email": "admin@visioanalytica.com",
  "organizationId": "guid-de-la-organizacion",
  "warning": "⚠️ IMPORTANTE: Cambia la contraseña después del primer login."
}
```

### Errores comunes:

#### 1. Ya existe un SuperAdmin:
```json
{
  "message": "El sistema ya tiene un SuperAdmin. Este endpoint está deshabilitado por seguridad.",
  "error": "SuperAdmin already exists"
}
```
**Solución:** El sistema ya está inicializado. Usa el SuperAdmin existente.

#### 2. Email ya en uso:
```json
{
  "message": "El email admin@visioanalytica.com ya está en uso.",
  "error": "Email already exists"
}
```
**Solución:** Usa un email diferente.

#### 3. Contraseña no cumple requisitos:
```json
{
  "message": "Error al crear el usuario: Passwords must be at least 8 characters...",
  "error": "User creation failed"
}
```
**Solución:** Asegúrate de que la contraseña cumpla todos los requisitos.

#### 4. Rol SuperAdmin no existe:
```json
{
  "message": "El rol SuperAdmin no existe. Ejecuta primero el RoleSeeder.",
  "error": "Role not found"
}
```
**Solución:** Los roles se crean automáticamente al iniciar la API. Reinicia la API.

## 🔐 Paso 3: Cambiar la contraseña

Después de crear el SuperAdmin:

1. Inicia sesión en la aplicación MAUI con las credenciales creadas
2. El sistema te pedirá cambiar la contraseña automáticamente
3. Usa una contraseña segura y única

## 📝 Notas adicionales

- El endpoint `/api/setup/initialize-superadmin` solo funciona **una vez**
- Después de crear el primer SuperAdmin, el endpoint se deshabilita automáticamente
- La organización "VisioAnalytica" se crea automáticamente si no existe
- Todos los logs se registran en el sistema para auditoría

## 🛠️ Solución de problemas

### El endpoint no funciona:
1. Verifica que la API esté ejecutándose
2. Verifica que los roles estén creados (se crean automáticamente al iniciar)
3. Verifica que no exista ya un SuperAdmin usando `/api/setup/check-status`

### No puedo iniciar sesión:
1. Verifica que el email y contraseña sean correctos
2. Verifica que el usuario esté activo (`IsActive = true`)
3. Revisa los logs de la API para ver errores específicos

## 🔒 Seguridad

- El endpoint solo funciona si NO existe ningún SuperAdmin
- No requiere autenticación (solo para el setup inicial)
- Se deshabilita automáticamente después de crear el primer SuperAdmin
- Todas las acciones se registran en logs

