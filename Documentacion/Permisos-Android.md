# **Configuración de Permisos en Android**

Esta guía explica los permisos configurados en la aplicación Android y cómo funcionan.

## **Permisos Configurados en AndroidManifest.xml**

### **Permisos de Red**
- `ACCESS_NETWORK_STATE`: Verifica el estado de la conexión de red
- `INTERNET`: Permite conexiones a Internet (necesario para la API)

### **Permisos de Cámara**
- `CAMERA`: Permite acceder a la cámara del dispositivo
- `android.hardware.camera`: Declara que la app puede usar la cámara (no requerida)
- `android.hardware.camera.autofocus`: Declara soporte para autofocus (no requerida)

### **Permisos de Almacenamiento**

#### **Android 12 y anteriores (API ≤ 32)**
- `READ_EXTERNAL_STORAGE`: Leer archivos del almacenamiento externo
- `WRITE_EXTERNAL_STORAGE`: Escribir archivos (solo hasta Android 10, API 29)

#### **Android 13+ (API 33+)**
- `READ_MEDIA_IMAGES`: Leer imágenes del almacenamiento (permiso granular)

## **Solicitud de Permisos en Tiempo de Ejecución**

La aplicación solicita permisos dinámicamente cuando el usuario intenta:
- **Tomar una foto**: Solicita permiso de cámara
- **Acceder a almacenamiento**: Solicita permiso de lectura (si es necesario)

### **Cómo Funciona**

1. Al presionar "Tomar Foto", la app verifica si tiene permiso de cámara
2. Si no lo tiene, muestra un diálogo del sistema pidiendo permiso
3. El usuario puede aceptar o denegar
4. Si se deniega, se muestra un mensaje explicativo

## **Solución de Problemas**

### ❌ "No se han establecido permisos de acceso a la cámara"

**Causa:** El `AndroidManifest.xml` no tenía los permisos declarados.

**Solución:** ✅ Ya corregido - Los permisos están ahora en el manifest.

### ❌ El usuario deniega el permiso

**Solución:**
1. Ve a Configuración del dispositivo
2. Apps → VisioAnalytica Risk → Permisos
3. Habilita "Cámara"

### ❌ "Permission Denied" en Android 13+

**Causa:** Android 13+ tiene permisos granulares más estrictos.

**Solución:** 
- La app ya maneja esto automáticamente
- Asegúrate de que el manifest tenga `READ_MEDIA_IMAGES` para Android 13+

## **Verificar Permisos Manualmente**

### **Desde el Dispositivo:**
1. Configuración → Apps → VisioAnalytica Risk → Permisos
2. Verifica que "Cámara" esté habilitada
3. Si usas Android 12 o anterior, verifica "Almacenamiento"

### **Desde Código:**
El código en `CapturePage.xaml.cs` verifica y solicita permisos automáticamente.

## **Notas Importantes**

- ⚠️ **Android 6.0+ (API 23+)**: Los permisos peligrosos (cámara, almacenamiento) se solicitan en tiempo de ejecución
- ⚠️ **Android 10+ (API 29+)**: `WRITE_EXTERNAL_STORAGE` ya no es necesario para apps que usan scoped storage
- ⚠️ **Android 13+ (API 33+)**: Permisos de almacenamiento son granulares (`READ_MEDIA_IMAGES`, `READ_MEDIA_VIDEO`, etc.)

## **Próximos Pasos**

Después de agregar los permisos al manifest:
1. **Recompila la app** completamente (Clean + Build)
2. **Desinstala la versión anterior** del dispositivo (si existe)
3. **Instala la nueva versión** con los permisos configurados
4. **Prueba tomar una foto** - debería solicitar permisos automáticamente

---

✅ **Los permisos ya están configurados correctamente en el AndroidManifest.xml**

