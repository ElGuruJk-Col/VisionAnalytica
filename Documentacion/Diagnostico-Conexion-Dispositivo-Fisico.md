# 🔍 Diagnóstico de Conexión desde Dispositivo Físico

## ✅ Estado Actual Verificado

- ✅ API escuchando en `0.0.0.0:5170` (todas las interfaces)
- ✅ IP de tu máquina: `192.168.1.83`
- ✅ Configuración de `launchSettings.json` correcta

## 🔧 Pasos de Diagnóstico

### Paso 1: Verificar que la API esté corriendo

En la consola donde corre la API, deberías ver:
```
Now listening on: http://0.0.0.0:5170
```

Si no ves esto, la API no está escuchando correctamente.

### Paso 2: Verificar desde tu PC (localhost)

Abre tu navegador en tu PC y ve a:
```
http://localhost:5170/swagger
```

**Si esto NO funciona**, el problema está en la API, no en la red.

### Paso 3: Verificar desde tu PC usando la IP

Abre tu navegador en tu PC y ve a:
```
http://192.168.1.83:5170/swagger
```

**Si esto NO funciona**, hay un problema con la configuración de red de tu PC.

### Paso 4: Verificar desde el dispositivo físico

1. **Abre el navegador en tu dispositivo Android**
2. **Ve a:** `http://192.168.1.83:5170/swagger`
3. **Si NO carga**, el problema es de conectividad de red o firewall

### Paso 5: Verificar Firewall de Windows

El firewall de Windows probablemente está bloqueando las conexiones entrantes.

#### Opción A: Crear regla de firewall (Recomendado)

1. Abre PowerShell **como Administrador**:
   - Presiona `Win + X`
   - Selecciona "Windows PowerShell (Administrador)" o "Terminal (Administrador)"

2. Ejecuta este comando:
   ```powershell
   netsh advfirewall firewall add rule name="VisioAnalytica API - Puerto 5170" dir=in action=allow protocol=TCP localport=5170
   ```

3. Verifica que se creó:
   ```powershell
   netsh advfirewall firewall show rule name="VisioAnalytica API - Puerto 5170"
   ```

#### Opción B: Configurar manualmente

1. Abre "Firewall de Windows Defender" (busca "firewall" en el menú inicio)
2. Haz clic en "Configuración avanzada"
3. Haz clic en "Reglas de entrada" → "Nueva regla..."
4. Selecciona "Puerto" → Siguiente
5. Selecciona "TCP" y "Puertos locales específicos": `5170` → Siguiente
6. Selecciona "Permitir la conexión" → Siguiente
7. Marca todas las casillas (Dominio, Privada, Pública) → Siguiente
8. Nombre: `VisioAnalytica API - Puerto 5170` → Finalizar

#### Opción C: Desactivar temporalmente (Solo para pruebas)

⚠️ **Solo para verificar que el firewall es el problema:**

1. Abre "Firewall de Windows Defender"
2. Desactiva temporalmente el firewall para redes privadas
3. Prueba la conexión desde el dispositivo
4. **Vuelve a activar el firewall** y crea la regla permanente

### Paso 6: Verificar que estén en la misma red

1. **En tu PC**, ejecuta:
   ```powershell
   ipconfig
   ```
   Anota la IP de tu adaptador WiFi/Ethernet (ej: `192.168.1.83`)

2. **En tu dispositivo Android**:
   - Ve a Configuración → Wi-Fi
   - Toca la red a la que estás conectado
   - Verifica la "Puerta de enlace" (Gateway)
   - Debe ser algo como `192.168.1.1` (mismo rango que tu IP)

3. **Si están en redes diferentes**, conecta ambos a la misma red WiFi

### Paso 7: Verificar conectividad con ping

**En tu dispositivo Android**, instala una app de terminal (ej: "Termux") o usa una app de red, y verifica:

```bash
ping 192.168.1.83
```

**Si el ping falla**, hay un problema de conectividad de red básica.

### Paso 8: Verificar que no haya VPN activa

Si tienes una VPN activa en tu PC o dispositivo, puede estar interfiriendo:
- Desactiva la VPN temporalmente
- Prueba la conexión nuevamente

### Paso 9: Verificar el router/firewall

Algunos routers tienen firewalls que bloquean conexiones entre dispositivos:
- Verifica la configuración de tu router
- Asegúrate de que el "Aislamiento de AP" (AP Isolation) esté desactivado

## 🎯 Solución Rápida (Si todo lo anterior falla)

### Usar el perfil "http" explícitamente

Asegúrate de que la API esté corriendo con el perfil "http":

```powershell
cd src/Api
dotnet run --launch-profile http
```

Esto asegura que solo use HTTP en el puerto 5170, sin HTTPS.

## 📝 Checklist Final

Antes de reportar el problema, verifica:

- [ ] La API está corriendo y muestra "Now listening on: http://0.0.0.0:5170"
- [ ] Puedes acceder a `http://localhost:5170/swagger` desde tu PC
- [ ] Puedes acceder a `http://192.168.1.83:5170/swagger` desde tu PC
- [ ] El firewall de Windows tiene una regla permitiendo el puerto 5170
- [ ] Tu dispositivo y PC están en la misma red WiFi
- [ ] No hay VPN activa
- [ ] El dispositivo puede hacer ping a `192.168.1.83`

## 🆘 Si Nada Funciona

Si después de seguir todos estos pasos el problema persiste:

1. **Prueba con un emulador Android** en lugar de dispositivo físico
2. **Usa `10.0.2.2` en lugar de la IP** (solo funciona en emulador)
3. **Verifica los logs de la API** para ver si las peticiones están llegando
4. **Revisa los logs del dispositivo** usando `adb logcat` si tienes Android SDK instalado

