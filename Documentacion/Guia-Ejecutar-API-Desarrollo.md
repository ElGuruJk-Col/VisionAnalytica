# **Guía: Ejecutar API para Pruebas con Dispositivo Físico**

## **Configuración Actual**

La API ya está configurada para escuchar en todas las interfaces de red (`0.0.0.0:5170`), lo que permite el acceso desde dispositivos externos en la misma red.

## **Forma Correcta de Ejecutar la API**

### **Opción 1: Usar el Perfil HTTP (Recomendado para Desarrollo)**

1. **En Visual Studio:**
   - Abre el proyecto `VisioAnalytica.Api`
   - En la barra de herramientas, selecciona el perfil **"http"** (no "https")
   - Presiona F5 o haz clic en "Ejecutar"

2. **Desde la Terminal (PowerShell/CMD):**
   ```powershell
   cd src/Api
   dotnet run --launch-profile http
   ```

3. **Verificar que está escuchando:**
   Deberías ver en la consola:
   ```
   Now listening on: http://0.0.0.0:5170
   ```

### **Opción 2: Usar el Perfil HTTPS (También funciona)**

El perfil "https" también escucha en `http://0.0.0.0:5170`, pero además escucha en `https://localhost:7005`.

```powershell
cd src/Api
dotnet run --launch-profile https
```

## **Verificar Accesibilidad desde la Red**

### **1. Verificar que la API está escuchando en todas las interfaces:**

Ejecuta en PowerShell:
```powershell
netstat -an | Select-String "5170"
```

Deberías ver algo como:
```
TCP    0.0.0.0:5170           0.0.0.0:0              LISTENING
```

Si solo ves `127.0.0.1:5170` o `localhost:5170`, la API no está accesible desde la red.

### **2. Verificar tu IP de red:**

```powershell
ipconfig | Select-String "IPv4"
```

Anota tu IP (ej: `192.168.1.83`).

### **3. Probar desde el navegador:**

Abre en tu navegador (desde la misma máquina):
- `http://localhost:5170/swagger` (debe funcionar)
- `http://192.168.1.83:5170/swagger` (debe funcionar desde la misma máquina)

### **4. Probar desde el dispositivo Android:**

En el dispositivo Android, abre un navegador y visita:
- `http://192.168.1.83:5170/swagger`

Si puedes ver Swagger desde el dispositivo, la API es accesible.

## **Configurar Firewall de Windows**

Si no puedes acceder desde el dispositivo, es probable que el firewall de Windows esté bloqueando el puerto.

### **Método 1: Permitir a través de la interfaz gráfica**

1. Abre **"Firewall de Windows Defender"**
2. Haz clic en **"Configuración avanzada"**
3. Haz clic en **"Reglas de entrada"** → **"Nueva regla"**
4. Selecciona **"Puerto"** → **Siguiente**
5. Selecciona **"TCP"** y escribe **5170** → **Siguiente**
6. Selecciona **"Permitir la conexión"** → **Siguiente**
7. Marca todas las opciones (Dominio, Privada, Pública) → **Siguiente**
8. Nombre: "VisioAnalytica API - Puerto 5170" → **Finalizar**

### **Método 2: Usar PowerShell (Administrador)**

```powershell
New-NetFirewallRule -DisplayName "VisioAnalytica API - Puerto 5170" -Direction Inbound -LocalPort 5170 -Protocol TCP -Action Allow
```

## **Verificar que el Dispositivo está en la Misma Red**

1. En el dispositivo Android, ve a **Configuración** → **Wi-Fi**
2. Verifica que estás conectado a la misma red Wi-Fi que tu máquina
3. O si usas USB, verifica que el dispositivo tiene acceso a la red local

## **Troubleshooting**

### **Problema: "Connection failure" desde el dispositivo**

**Soluciones:**
1. ✅ Verifica que la API está ejecutándose (`dotnet run`)
2. ✅ Verifica que escucha en `0.0.0.0:5170` (no solo `localhost`)
3. ✅ Verifica que el firewall permite conexiones en el puerto 5170
4. ✅ Verifica que el dispositivo está en la misma red Wi-Fi
5. ✅ Verifica que la IP en `ApiClient.cs` es correcta (`192.168.1.83:5170`)

### **Problema: La API solo escucha en localhost**

Si ves en la consola:
```
Now listening on: http://127.0.0.1:5170
```

**Solución:** Verifica que `launchSettings.json` tiene:
```json
"applicationUrl": "http://0.0.0.0:5170"
```

### **Problema: El firewall bloquea las conexiones**

**Solución:** Sigue los pasos de "Configurar Firewall de Windows" arriba.

## **Comandos Útiles**

### **Verificar puertos en uso:**
```powershell
netstat -an | Select-String "5170"
```

### **Verificar procesos que escuchan:**
```powershell
Get-NetTCPConnection -LocalPort 5170
```

### **Probar conectividad desde PowerShell:**
```powershell
Test-NetConnection -ComputerName 192.168.1.83 -Port 5170
```

### **Ver todas las IPs de tu máquina:**
```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.IPAddress -notlike "127.*"} | Select-Object IPAddress, InterfaceAlias
```

## **Resumen de Pasos Recomendados**

1. ✅ Ejecuta la API con: `dotnet run --launch-profile http`
2. ✅ Verifica que escucha en `0.0.0.0:5170`
3. ✅ Configura el firewall para permitir el puerto 5170
4. ✅ Verifica tu IP de red (`ipconfig`)
5. ✅ Actualiza `ApiClient.cs` con tu IP si es necesario
6. ✅ Prueba desde el navegador del dispositivo: `http://192.168.1.83:5170/swagger`
7. ✅ Si Swagger funciona, la app MAUI también debería funcionar

---

**Nota:** Si cambias de red Wi-Fi, tu IP puede cambiar. Verifica con `ipconfig` y actualiza `ApiClient.cs` si es necesario.

