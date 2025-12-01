# Script de Monitoreo de Vulnerabilidades

## 📋 Propósito

El script `CheckVulnerabilities.ps1` automatiza la verificación de vulnerabilidades conocidas en todos los paquetes NuGet utilizados en el proyecto VisioAnalytica, incluyendo dependencias transitivas.

## 🎯 Razón de Uso

### ¿Por qué es necesario?

1. **Seguridad Proactiva**: Detecta vulnerabilidades antes de que sean explotadas
2. **Cumplimiento**: Ayuda a mantener estándares de seguridad en el proyecto
3. **Automatización**: Evita la verificación manual de múltiples proyectos
4. **Dependencias Transitivas**: Verifica no solo paquetes directos, sino también los que vienen como dependencias (como `Newtonsoft.Json` que viene con Hangfire)

### ¿Cuándo ejecutarlo?

- **Mensualmente**: Como parte del mantenimiento rutinario
- **Antes de cada release**: Para asegurar que no hay vulnerabilidades en producción
- **Después de actualizar paquetes**: Para verificar que las actualizaciones no introdujeron nuevas vulnerabilidades
- **Cuando se reciben alertas de seguridad**: Para verificar si afectan al proyecto

## 🚀 Cómo Usarlo

### Opción 1: Ejecución Simple

```powershell
# Desde la raíz del proyecto
.\Scripts\CheckVulnerabilities.ps1
```

### Opción 2: Con Modo Verbose

```powershell
# Ver salida detallada de todos los paquetes
.\Scripts\CheckVulnerabilities.ps1 -Verbose
```

### Opción 3: Guardar Reporte

```powershell
# Guardar reporte en un archivo JSON
.\Scripts\CheckVulnerabilities.ps1 -OutputFile "vulnerability-report.json"
```

### Opción 4: Combinado

```powershell
# Verbose + Guardar reporte
.\Scripts\CheckVulnerabilities.ps1 -Verbose -OutputFile "report-$(Get-Date -Format 'yyyyMMdd').json"
```

## 📊 Interpretación de Resultados

### ✅ Sin Vulnerabilidades

```
=== RESUMEN ===
✅ TODO CORRECTO
No se encontraron vulnerabilidades conocidas en los paquetes.
```

**Acción**: Ninguna. El proyecto está seguro.

### ⚠️ Con Vulnerabilidades

```
=== RESUMEN ===
⚠️ SE ENCONTRARON VULNERABILIDADES

ACCIÓN REQUERIDA:
1. Revisa las vulnerabilidades listadas arriba
2. Actualiza los paquetes afectados a versiones seguras
3. Ejecuta 'dotnet restore' después de actualizar
4. Vuelve a ejecutar este script para verificar
```

**Acción**: 
1. Identifica el paquete vulnerable
2. Busca una versión segura en [NuGet.org](https://www.nuget.org/)
3. Actualiza el paquete en `Directory.Build.props` o en el `.csproj` específico
4. Ejecuta `dotnet restore`
5. Vuelve a ejecutar el script

## 🔧 Ejemplo de Actualización

Si el script detecta que `Newtonsoft.Json 11.0.1` tiene una vulnerabilidad:

1. **Buscar versión segura**: En este caso, `13.0.3` es segura
2. **Actualizar `Directory.Build.props`**:
   ```xml
   <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
   ```
3. **Restaurar paquetes**:
   ```powershell
   dotnet restore
   ```
4. **Verificar nuevamente**:
   ```powershell
   .\Scripts\CheckVulnerabilities.ps1
   ```

## 📝 Integración con CI/CD

Puedes integrar este script en tu pipeline de CI/CD:

```yaml
# Ejemplo para GitHub Actions
- name: Check Vulnerabilities
  run: |
    pwsh -File Scripts/CheckVulnerabilities.ps1
```

```yaml
# Ejemplo para Azure DevOps
- task: PowerShell@2
  displayName: 'Check Vulnerabilities'
  inputs:
    filePath: 'Scripts/CheckVulnerabilities.ps1'
    pwsh: true
```

## 🔍 Códigos de Salida

- **Exit Code 0**: Sin vulnerabilidades encontradas ✅
- **Exit Code 1**: Se encontraron vulnerabilidades (requiere acción) ⚠️

Esto permite usar el script en automatizaciones que pueden fallar el build si hay vulnerabilidades.

## 📚 Referencias

- [NuGet Security Advisory Database](https://github.com/nuget/security-advisories)
- [.NET Security Advisory](https://github.com/dotnet/announcements/labels/Security)
- [OWASP Dependency Check](https://owasp.org/www-project-dependency-check/)

## ⚙️ Configuración

El script verifica automáticamente estos proyectos:
- `src/Api/VisioAnalytica.Api.csproj`
- `src/Infrastructure/VisioAnalytica.Infrastructure.csproj`
- `src/Apps/VisioAnalytica.App.Risk/VisioAnalytica.App.Risk.csproj`
- `src/Core/VisioAnalytica.Core.csproj`

Para agregar más proyectos, edita el array `$projects` en el script.

## 🆘 Solución de Problemas

### Error: "No se puede ejecutar scripts"

```powershell
# Ejecutar como administrador
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Error: "dotnet no se encuentra"

Asegúrate de tener .NET SDK instalado y en el PATH.

### El script no detecta vulnerabilidades conocidas

Asegúrate de tener la versión más reciente del .NET SDK, ya que usa la base de datos de vulnerabilidades de NuGet.

