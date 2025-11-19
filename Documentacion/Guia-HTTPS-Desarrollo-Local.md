# **Guía: Probar tu API con HTTPS en Desarrollo Local**

Esta guía te explica cómo configurar y probar tu API con HTTPS en tu máquina local.

---

## **Paso 1: Verificar/Generar el Certificado de Desarrollo**

ASP.NET Core usa un certificado de desarrollo para HTTPS local. Primero verifica si está instalado:

### **En PowerShell (Windows):**

```powershell
# Verificar si el certificado de desarrollo existe
dotnet dev-certs https --check
```

Si no existe o está vencido, créalo:

```powershell
# Generar/regenerar el certificado de desarrollo
dotnet dev-certs https --trust
```

**¿Qué hace este comando?**

- ✅ Genera un certificado autofirmado para `localhost`
- ✅ Lo instala en el almacén de certificados de Windows
- ✅ Lo marca como confiable (evita advertencias del navegador)

---

## **Paso 2: Ejecutar la API con el Perfil HTTPS**

Tienes dos opciones para ejecutar tu API con HTTPS:

### **Opción A: Desde la Línea de Comandos**

```powershell
# Ejecutar con el perfil HTTPS
dotnet run --project src/api --launch-profile https
```

O especificando las URLs directamente:

```powershell
dotnet run --project src/api --urls "https://localhost:7005;http://0.0.0.0:5170"
```

### **Opción B: Desde Visual Studio / VS Code**

1. Abre el proyecto en Visual Studio o VS Code
2. En la barra de herramientas, selecciona el perfil **"https"** en lugar de "http"
3. Presiona **F5** o ejecuta el proyecto

---

## **Paso 3: Verificar que Funciona**

### **3.1. Verificar los Logs**

Deberías ver algo como:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7005
      Now listening on: http://0.0.0.0:5170
```

### **3.2. Probar en el Navegador**

Abre:
```
https://localhost:7005/swagger
```

**⚠️ Primera vez:**
- El navegador mostrará una advertencia de certificado no confiable
- Haz clic en **"Avanzado"** → **"Continuar a localhost (no seguro)"**
- Esto es normal con certificados autofirmados en desarrollo

### **3.3. Verificar la Redirección HTTPS**

Si accedes a:
```
http://localhost:5170/swagger
```

Debería redirigir automáticamente a:
```
https://localhost:7005/swagger
```

Esto confirma que `UseHttpsRedirection()` funciona correctamente.

---

## **Paso 4: Probar desde tu App Móvil (Si Aplica)**

Si tu app móvil necesita conectarse por HTTPS:

### **Actualizar ApiClient.cs**

En `src/Apps/VisioAnalytica.App.Risk/Services/ApiClient.cs`, cambia:

```csharp
#if ANDROID
    // Para HTTPS local, usa localhost (solo funciona en emulador)
    // Para dispositivo físico, necesitarás un certificado válido o usar HTTP
    return "https://10.0.2.2:7005"; // Emulador Android apunta a localhost
    // O para dispositivo físico con certificado:
    // return "https://192.168.1.83:7005"; // Tu IP local
#elif IOS
    return "https://localhost:7005"; // Simulador iOS
#else
    return "https://localhost:7005";
#endif
```

**⚠️ Nota Importante:** Los dispositivos físicos pueden tener problemas con certificados autofirmados. Para desarrollo, HTTP suele ser más simple.

---

## **Solución de Problemas Comunes**

### **Problema 1: "El certificado no es confiable"**

**Solución:**
```powershell
# Regenerar y confiar en el certificado
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

Luego reinicia el navegador.

### **Problema 2: "ERR_CERT_AUTHORITY_INVALID"**

El certificado no está marcado como confiable. Ejecuta:

```powershell
dotnet dev-certs https --trust
```

### **Problema 3: "El puerto 7005 está en uso"**

Cambia el puerto en `launchSettings.json`:

```json
"https": {
  "applicationUrl": "https://localhost:7006;http://0.0.0.0:5170"
}
```

### **Problema 4: "No puedo acceder desde otro dispositivo"**

Los certificados autofirmados no funcionan bien desde otros dispositivos. Opciones:

- **Usar HTTP para desarrollo** (`http://192.168.1.83:5170`)
- **Configurar un certificado válido** (más complejo)
- **Usar un túnel como ngrok** (para pruebas rápidas)

---

## **Verificación Rápida**

Ejecuta estos comandos para verificar todo:

```powershell
# 1. Verificar certificado
dotnet dev-certs https --check

# 2. Si no existe, crearlo
dotnet dev-certs https --trust

# 3. Ejecutar con HTTPS
dotnet run --project src/api --launch-profile https

# 4. En otro terminal, probar la redirección
curl -I http://localhost:5170/swagger
# Debería redirigir a https://localhost:7005/swagger
```

---

## **Resumen de URLs**

Con el perfil HTTPS activo, tu API estará disponible en:

| Protocolo | URL | Uso |
|-----------|-----|-----|
| **HTTPS** | `https://localhost:7005` | Acceso seguro desde navegador |
| **HTTP** | `http://0.0.0.0:5170` | Acceso desde red local (app móvil) |

---

## **Configuración Actual en tu Proyecto**

Tu `launchSettings.json` ya tiene configurado el perfil HTTPS:

```json
{
  "profiles": {
    "http": {
      "applicationUrl": "http://0.0.0.0:5170"
    },
    "https": {
      "applicationUrl": "https://localhost:7005;http://0.0.0.0:5170"
    }
  }
}
```

Y tu `Program.cs` ya tiene la verificación condicional para HTTPS:

```csharp
// Solo usar HTTPS redirection si hay HTTPS configurado
if (hasHttpsPort || hasHttpsInUrls)
{
    app.UseHttpsRedirection();
}
```

---

## **Comandos Útiles del Certificado**

```powershell
# Verificar estado del certificado
dotnet dev-certs https --check

# Generar/regenerar certificado
dotnet dev-certs https --trust

# Limpiar certificado existente
dotnet dev-certs https --clean

# Ver detalles del certificado
dotnet dev-certs https --verbose
```

---

## **Notas Importantes**

1. ⚠️ **El certificado de desarrollo solo funciona para `localhost`**
   - No funcionará con IPs como `192.168.1.83`
   - Solo para desarrollo local

2. ⚠️ **No es válido para producción**
   - En producción, usa certificados reales (Let's Encrypt, etc.)

3. ⚠️ **Los dispositivos físicos pueden rechazar certificados autofirmados**
   - Para desarrollo móvil, HTTP suele ser más práctico
   - O configura un certificado válido para tu dominio/IP

4. ✅ **El certificado se regenera automáticamente**
   - Si expira, ejecuta `dotnet dev-certs https --trust` nuevamente

---

## **Flujo de Trabajo Recomendado**

### **Para Desarrollo Local (Navegador):**
```powershell
# 1. Asegurar certificado
dotnet dev-certs https --trust

# 2. Ejecutar con HTTPS
dotnet run --project src/api --launch-profile https

# 3. Acceder a: https://localhost:7005/swagger
```

### **Para Desarrollo Móvil (App):**
```powershell
# 1. Ejecutar con HTTP (más simple para dispositivos)
dotnet run --project src/api --launch-profile http

# 2. App se conecta a: http://192.168.1.83:5170
```

---

## **Referencias**

- [ASP.NET Core HTTPS](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl)
- [Certificados de Desarrollo](https://learn.microsoft.com/en-us/aspnet/core/security/https#trust-the-aspnet-core-https-development-certificate-on-windows-and-macos)
- [Configuración de HTTPS](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints)

---

**Última actualización:** Noviembre 2025

