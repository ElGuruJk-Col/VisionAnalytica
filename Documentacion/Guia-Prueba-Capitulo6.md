# **Guía de Prueba - Capítulo 6: Frontend MAUI**

Esta guía te ayudará a probar la aplicación móvil VisioAnalytica Risk paso a paso.

## **Prerequisitos**

1. ✅ .NET 9 SDK instalado
2. ✅ Visual Studio 2022 con carga de trabajo MAUI
3. ✅ API backend funcionando (Capítulo 5)
4. ✅ Base de datos configurada y migraciones aplicadas

---

## **Paso 1: Verificar y Configurar la API**

### 1.1. Verificar que la API esté configurada

Asegúrate de que `src/Api/appsettings.json` tenga:
- ✅ Cadena de conexión a la base de datos
- ✅ Clave JWT configurada
- ✅ API Key de Gemini (si vas a probar análisis real)
- ✅ Prompt maestro de SST configurado

### 1.2. Ejecutar la API

```powershell
# Desde la carpeta del proyecto
cd src/Api
dotnet run
```

**Verifica que la API esté corriendo:**
- Deberías ver: `Now listening on: http://localhost:XXXX`
- Anota el puerto (ej: `5170`, `5000`, etc.)
- Abre en navegador: `http://localhost:XXXX/swagger` para verificar

---

## **Paso 2: Configurar la URL de la API en la App MAUI**

### 2.1. Actualizar ApiClient.cs

Edita `src/Apps/VisioAnalytica.App.Risk/Services/ApiClient.cs`:

```csharp
// Cambiar esta línea (aproximadamente línea 25):
BaseUrl = "http://localhost:5170"; // TODO: Mover a configuración

// Por el puerto correcto de tu API:
BaseUrl = "http://localhost:5170"; // O el puerto que uses
```

**⚠️ IMPORTANTE para dispositivos físicos:**
- Si pruebas en un **dispositivo físico** (Android/iOS), NO puedes usar `localhost`
- Usa la IP de tu máquina: `BaseUrl = "http://192.168.1.XXX:5170"`
- Para encontrar tu IP: `ipconfig` (Windows) o `ifconfig` (Linux/Mac)

### 2.2. Para Android Emulador

Si usas el **emulador de Android**:
- Android puede acceder a `10.0.2.2` que apunta al `localhost` de tu máquina
- Usa: `BaseUrl = "http://10.0.2.2:5170"`

### 2.3. Para iOS Simulador

Si usas el **simulador de iOS**:
- Puedes usar `localhost` directamente
- Usa: `BaseUrl = "http://localhost:5170"`

---

## **Paso 3: Compilar y Ejecutar la App MAUI**

### 3.1. Desde Visual Studio 2022

1. Abre la solución: `src/VisioAnalytica.sln`
2. Establece `VisioAnalytica.App.Risk` como proyecto de inicio
3. Selecciona la plataforma objetivo:
   - **Windows** (más fácil para empezar)
   - **Android** (requiere Android SDK)
   - **iOS** (solo en Mac)
4. Presiona **F5** o clic en "Ejecutar"

### 3.2. Desde Terminal (Windows)

```powershell
# Navegar a la carpeta del proyecto MAUI
cd src/Apps/VisioAnalytica.App.Risk

# Para Windows
dotnet build -f net9.0-windows10.0.19041.0
dotnet run -f net9.0-windows10.0.19041.0

# Para Android (requiere Android SDK)
dotnet build -f net9.0-android
dotnet run -f net9.0-android
```

---

## **Paso 4: Probar el Flujo Completo**

### 4.1. Registro de Usuario

1. Al abrir la app, deberías ver la **página de Login**
2. Toca **"¿No tienes cuenta? Regístrate"**
3. Completa el formulario:
   - Nombre: `Juan`
   - Apellido: `Pérez`
   - Email: `juan@test.com`
   - Organización: `Empresa Test`
   - Contraseña: `1234` (mínimo 4 caracteres)
   - Confirmar Contraseña: `1234`
4. Toca **"Registrarse"**
5. ✅ Deberías ser redirigido a la página principal

### 4.2. Login (Alternativa)

Si ya tienes un usuario registrado:
1. En la página de Login, ingresa:
   - Email: `juan@test.com`
   - Contraseña: `1234`
2. Toca **"Iniciar Sesión"**
3. ✅ Deberías ser redirigido a la página principal

### 4.3. Capturar y Analizar Foto

1. En la página principal, toca **"Nueva Inspección"** o ve a la pestaña **"Capturar"**
2. Toca **"Tomar Foto"**
   - Si es la primera vez, acepta los permisos de cámara
3. Toma una foto de un escenario con riesgos SST (ej: trabajador sin casco)
4. Verás un preview de la imagen
5. Toca **"Analizar Imagen"**
6. ⏳ Espera mientras se procesa (puede tardar 10-30 segundos)
7. ✅ Deberías ver la página de resultados con los hallazgos

### 4.4. Ver Resultados

En la página de resultados deberías ver:
- ✅ La imagen analizada
- ✅ Lista de hallazgos con:
  - Nivel de riesgo (ALTO/MEDIO/BAJO) con colores
  - Descripción del hallazgo
  - Acción correctiva
  - Acción preventiva

### 4.5. Navegación

- **"Nuevo Análisis"**: Vuelve a la página de captura
- **"Ver Historial"**: Muestra el historial (pendiente de completar)
- **"Cerrar Sesión"**: Cierra sesión y vuelve a Login

---

## **Paso 5: Solución de Problemas Comunes**

### ❌ Error: "No se puede conectar a la API"

**Causa:** La URL de la API no es correcta o la API no está corriendo.

**Solución:**
1. Verifica que la API esté corriendo: `http://localhost:5170/swagger`
2. Verifica la URL en `ApiClient.cs`
3. Si usas dispositivo físico, usa la IP de tu máquina

### ❌ Error: "401 Unauthorized"

**Causa:** El token JWT no se está enviando correctamente.

**Solución:**
1. Verifica que el login haya sido exitoso
2. Revisa que `AuthService` esté guardando el token
3. Verifica que `ApiClient.SetAuthToken()` se esté llamando

### ❌ Error: "No se puede capturar foto"

**Causa:** Permisos de cámara no otorgados.

**Solución:**
1. En Android: Configuración → Apps → VisioAnalytica → Permisos → Cámara
2. En iOS: Configuración → Privacidad → Cámara → VisioAnalytica
3. Reinstala la app si es necesario

### ❌ Error: "Error al analizar imagen"

**Causa:** 
- API Key de Gemini no configurada
- Imagen muy grande
- Error de red

**Solución:**
1. Verifica `appsettings.json` de la API tiene `Gemini:ApiKey`
2. Verifica que la API esté respondiendo en Swagger
3. Revisa los logs de la API para más detalles

### ❌ La app se cierra inesperadamente

**Causa:** Excepción no manejada.

**Solución:**
1. Revisa la consola de Visual Studio para ver el error
2. Verifica que todos los servicios estén registrados en `MauiProgram.cs`
3. Verifica que todas las páginas estén registradas en `AppShell.xaml`

---

## **Paso 6: Verificar Logs y Debugging**

### 6.1. Logs de la API

En la consola donde corre la API, deberías ver:
```
[VisioAnalytica.Api] Modo: Development (Usando SQL Server Docker)
Iniciando PerformSstAnalysisAsync para el usuario {UserId}...
Imagen guardada en: /uploads/{orgId}/{filename}
Inspección {InspectionId} persistida en la BBDD...
```

### 6.2. Logs de la App MAUI

En Visual Studio, abre la **Ventana de Salida** → **Depuración** para ver logs.

### 6.3. Usar Swagger para Probar la API Directamente

1. Abre: `http://localhost:5170/swagger`
2. Prueba el endpoint `/api/auth/register` o `/api/auth/login`
3. Copia el token JWT
4. Usa "Authorize" en Swagger para probar endpoints protegidos

---

## **Checklist de Prueba**

- [ ] API corriendo y accesible
- [ ] URL de API configurada correctamente en `ApiClient.cs`
- [ ] App MAUI compila sin errores
- [ ] App se ejecuta correctamente
- [ ] Registro de usuario funciona
- [ ] Login funciona
- [ ] Captura de foto funciona
- [ ] Análisis de imagen funciona
- [ ] Resultados se muestran correctamente
- [ ] Navegación entre páginas funciona
- [ ] Cerrar sesión funciona

---

## **Próximos Pasos Después de Probar**

1. **Completar Historial**: Integrar el endpoint `/api/v1/analysis/history`
2. **Mejorar UX**: Agregar indicadores de carga más visibles
3. **Manejo de Errores**: Mejorar mensajes de error para el usuario
4. **Configuración**: Mover URL de API a configuración (appsettings.json)
5. **Testing**: Crear pruebas unitarias para servicios

---

## **Notas Importantes**

- ⚠️ **Para producción**: Cambia la URL de la API a una URL real (no localhost)
- ⚠️ **Seguridad**: En producción, usa HTTPS, no HTTP
- ⚠️ **Tokens**: Los tokens JWT tienen expiración (7 días por defecto)
- ⚠️ **Imágenes**: Las imágenes se guardan localmente en `wwwroot/uploads` (desarrollo)

---

¡Listo para probar! 🚀

