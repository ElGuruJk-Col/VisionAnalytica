# **Solución de Problemas de Conexión - App MAUI**

## **Problemas Resueltos**

### ✅ **1. Previsualización de Imagen No Se Muestra**

**Causa:** El `ImageSource.FromStream()` no estaba funcionando correctamente con el stream.

**Solución:**
- Usar `ImageSource.FromFile()` si `photo.FullPath` está disponible
- Mejorar el layout usando `Grid` para superponer imagen y placeholder
- Crear copia del array de bytes para el stream

### ✅ **2. Error "Connection failure" al Analizar Imagen y Login**

**Causa:** Android bloquea conexiones HTTP (cleartext traffic) por defecto desde Android 9+.

**Soluciones Aplicadas:**

1. **AndroidManifest.xml:**
   - Agregado `android:usesCleartextTraffic="true"`
   - Agregado `android:networkSecurityConfig="@xml/network_security_config"`

2. **network_security_config.xml:**
   - Configuración de seguridad de red que permite HTTP
   - Dominios permitidos: localhost, 10.0.2.2, 192.168.1.83

3. **ApiClient.cs:**
   - Timeout aumentado a 60 segundos
   - Mejor manejo de errores con mensajes más descriptivos
   - Muestra la URL completa en los errores para debugging

## **Verificación de la Configuración**

### **1. Verificar que la API esté accesible desde el dispositivo:**

Desde el navegador del dispositivo móvil, abre:
```
http://192.168.1.83:5170/swagger
```

Si puedes ver Swagger, la conexión funciona.

### **2. Verificar la IP en ApiClient.cs:**

Asegúrate de que la IP en `GetBaseUrl()` sea correcta:
```csharp
#if ANDROID
    return "http://192.168.1.83:5170"; // Tu IP
#endif
```

### **3. Verificar que la API escuche en todas las interfaces:**

En `src/Api/Properties/launchSettings.json`:
```json
"applicationUrl": "http://0.0.0.0:5170"
```

## **Pasos para Aplicar los Cambios**

1. **Limpiar y recompilar:**
   ```powershell
   cd src/Apps/VisioAnalytica.App.Risk
   dotnet clean
   dotnet build -f net9.0-android
   ```

2. **Desinstalar la app anterior** del dispositivo

3. **Instalar la nueva versión**

4. **Probar:**
   - Login debería funcionar
   - Captura de foto debería mostrar preview
   - Análisis debería funcionar

## **Si Aún No Funciona**

### **Verificar Logs:**

```powershell
adb logcat | Select-String "VisioAnalytica"
```

### **Verificar Firewall:**

Asegúrate de que el firewall de Windows permita conexiones en el puerto 5170.

### **Verificar Red:**

- Dispositivo y PC deben estar en la misma red WiFi
- Prueba hacer ping desde el dispositivo a tu PC

### **Actualizar IP si Cambió:**

Si tu IP cambió, actualiza:
1. `ApiClient.cs` - método `GetBaseUrl()`
2. `network_security_config.xml` - agregar el nuevo dominio

---

✅ **Todos los cambios están aplicados. Recompila y prueba nuevamente.**

