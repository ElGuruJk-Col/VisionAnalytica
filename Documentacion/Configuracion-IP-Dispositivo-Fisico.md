# **Configuración de IP para Dispositivos Físicos**

Cuando ejecutas la app MAUI en un **dispositivo físico** (Android/iOS), no puedes usar `localhost` porque el dispositivo no puede acceder al `localhost` de tu máquina de desarrollo.

## **Solución: Usar la IP de tu Máquina**

### **Paso 1: Encontrar tu IP Local**

#### **Windows:**
```powershell
ipconfig
```
Busca la línea que dice "Dirección IPv4" (ej: `192.168.1.83`)

#### **Linux/Mac:**
```bash
ifconfig
# o
ip addr
```

### **Paso 2: Actualizar ApiClient.cs**

Edita el archivo: `src/Apps/VisioAnalytica.App.Risk/Services/ApiClient.cs`

En el método `GetBaseUrl()`, cambia la IP:

```csharp
#if ANDROID
    // Cambia 192.168.1.83 por TU IP
    return "http://192.168.1.83:5170";
#elif IOS
    // Cambia 192.168.1.83 por TU IP
    return "http://192.168.1.83:5170";
#else
    return "http://localhost:5170";
#endif
```

### **Paso 3: Verificar que la API Acepte Conexiones Externas**

La API debe estar configurada para aceptar conexiones desde otras máquinas en la red local.

#### **Verificar launchSettings.json:**

Asegúrate de que la API esté escuchando en todas las interfaces:

```json
{
  "applicationUrl": "http://0.0.0.0:5170"
}
```

O específicamente en tu IP:

```json
{
  "applicationUrl": "http://192.168.1.83:5170"
}
```

#### **Si usas dotnet run:**

```powershell
dotnet run --urls "http://0.0.0.0:5170"
```

### **Paso 4: Verificar Firewall**

Asegúrate de que el firewall de Windows permita conexiones en el puerto 5170:

1. Abre "Firewall de Windows Defender"
2. "Configuración avanzada"
3. "Reglas de entrada"
4. Crea una nueva regla para el puerto 5170 (TCP)

O temporalmente desactiva el firewall para probar.

### **Paso 5: Verificar que Dispositivo y PC estén en la Misma Red**

- Ambos deben estar en la misma red WiFi
- Verifica que puedas hacer ping desde el dispositivo a tu PC

---

## **Configuraciones por Plataforma**

### **Android Emulador**

Si usas el **emulador de Android**, puedes usar:
```csharp
return "http://10.0.2.2:5170"; // 10.0.2.2 apunta al localhost de tu máquina
```

### **iOS Simulador**

Si usas el **simulador de iOS**, puedes usar:
```csharp
return "http://localhost:5170"; // Funciona directamente
```

### **Dispositivo Físico Android/iOS**

**Siempre** usa la IP de tu máquina:
```csharp
return "http://192.168.1.83:5170"; // Tu IP local
```

---

## **Solución Alternativa: Configuración Dinámica**

Para hacerlo más flexible, puedes crear un archivo de configuración o usar variables de entorno.

### **Opción 1: Usar appsettings.json (Futuro)**

```json
{
  "ApiSettings": {
    "BaseUrl": "http://192.168.1.83:5170"
  }
}
```

### **Opción 2: Detectar Automáticamente (Avanzado)**

Puedes detectar si estás en un emulador o dispositivo físico y ajustar la URL automáticamente.

---

## **Troubleshooting**

### ❌ "Connection failure" o "No se puede conectar"

1. ✅ Verifica que la IP en `ApiClient.cs` sea correcta
2. ✅ Verifica que la API esté corriendo: `http://TU_IP:5170/swagger`
3. ✅ Verifica que dispositivo y PC estén en la misma red WiFi
4. ✅ Verifica el firewall
5. ✅ Prueba hacer ping desde el dispositivo a tu PC

### ❌ "Connection timeout"

- La API no está escuchando en `0.0.0.0` o en tu IP
- El firewall está bloqueando
- La IP cambió (las IPs locales pueden cambiar)

### ✅ Verificar Conexión

Desde el dispositivo, abre un navegador y prueba:
```
http://TU_IP:5170/swagger
```

Si puedes ver Swagger, la conexión funciona.

---

## **Tu IP Actual**

Según el sistema, tu IP es: **192.168.1.83**

Asegúrate de actualizar `ApiClient.cs` con esta IP si usas un dispositivo físico.

