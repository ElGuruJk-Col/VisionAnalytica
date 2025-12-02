# Plan de Migración: MAUI Shell → NavigationPage + TabbedPage

## Objetivo
Migrar la aplicación móvil de MAUI Shell a NavigationPage + TabbedPage para obtener mayor control sobre la navegación y facilitar la implementación de funcionalidades de valor agregado.

## Razones de la Migración

### Limitaciones de MAUI Shell
1. **Navegación basada en URI**: Difícil de depurar y mantener
2. **Query strings limitadas**: Solo permite pasar strings simples
3. **Poco control sobre la pila de navegación**
4. **Personalización limitada** del TabBar y Flyout
5. **Problemas con objetos complejos**: Requiere NavigationDataService como workaround

### Beneficios de NavigationPage + TabbedPage
1. **Control total** sobre la navegación
2. **Pasar objetos completos** directamente entre páginas
3. **Pila de navegación explícita** (PushAsync/PopAsync)
4. **Personalización completa** del UI
5. **Fácil de depurar** y mantener
6. **Mejor preparado** para funcionalidades futuras (Dashboard, Offline, etc.)

## Estructura Actual vs Nueva

### Estructura Actual (MAUI Shell)
```
App
└─> AppShell (Shell)
    ├─> FlyoutContent (menú lateral)
    ├─> ShellContent (LoginPage)
    ├─> TabBar
    │   ├─> MainPage
    │   ├─> MultiCapturePage
    │   └─> InspectionHistoryPage
    └─> ShellContent (otras páginas)
```

### Estructura Nueva (NavigationPage + TabbedPage)
```
App
└─> NavigationPage (raíz)
    └─> LoginPage (inicial)
        └─> (después de login)
            └─> TabbedPage
                ├─> NavigationPage(MainPage)
                ├─> NavigationPage(MultiCapturePage)
                └─> NavigationPage(InspectionHistoryPage)
```

## Plan de Implementación

### Fase 1: Preparación
1. ✅ Crear plan de migración
2. ✅ Crear branch `feature/migracion-navigation-page`
3. Crear NavigationService centralizado

### Fase 2: Crear Nuevos Componentes
1. Crear `INavigationService` y `NavigationService`
2. Crear `AppNavigationPage` (reemplazo de AppShell)
3. Crear `AppFlyoutPage` o menú personalizado
4. Crear constantes de rutas (opcional, para deep linking futuro)

### Fase 3: Actualizar Configuración
1. Actualizar `App.xaml.cs` para usar NavigationPage
2. Actualizar `MauiProgram.cs`:
   - Registrar NavigationService
   - Eliminar registro de AppShell
   - Eliminar Routing.RegisterRoute

### Fase 4: Migrar Páginas
1. Reemplazar todas las llamadas `Shell.Current.GoToAsync()` por `NavigationService`
2. Eliminar `QueryProperty` y usar parámetros en constructores
3. Actualizar páginas para usar Navigation en lugar de Shell

### Fase 5: Limpieza
1. Eliminar `AppShell.xaml` y `AppShell.xaml.cs`
2. Eliminar código relacionado con Shell
3. Verificar y corregir errores

### Fase 6: Documentación y Finalización
1. Crear documento de migración
2. Verificar compilación sin errores
3. Verificar sin advertencias críticas
4. Commit y push

## Archivos a Modificar

### Archivos Nuevos
- `Services/INavigationService.cs`
- `Services/NavigationService.cs`
- `AppNavigationPage.xaml` (opcional, para TabbedPage)
- `AppFlyoutPage.xaml` (opcional, para menú)

### Archivos a Modificar
- `App.xaml.cs` - Cambiar de Shell a NavigationPage
- `MauiProgram.cs` - Registrar NavigationService, eliminar Shell
- Todas las páginas que usan `Shell.Current.GoToAsync()`

### Archivos a Eliminar
- `AppShell.xaml`
- `AppShell.xaml.cs`

## Cambios Específicos por Archivo

### App.xaml.cs
```csharp
// ANTES
var window = new Window(shell);

// DESPUÉS
var navigationService = serviceProvider.GetRequiredService<INavigationService>();
var initialPage = navigationService.GetInitialPage();
var navigationPage = new NavigationPage(initialPage);
var window = new Window(navigationPage);
```

### MauiProgram.cs
```csharp
// AGREGAR
builder.Services.AddSingleton<INavigationService, NavigationService>();

// ELIMINAR
builder.Services.AddSingleton<AppShell>();
Routing.RegisterRoute(...);
```

### Páginas (ejemplo)
```csharp
// ANTES
await Shell.Current.GoToAsync("//LoginPage");
await Shell.Current.GoToAsync($"InspectionDetailsPage?inspectionId={id}");

// DESPUÉS
await _navigationService.NavigateToLoginAsync();
await _navigationService.NavigateToInspectionDetailsAsync(id);
```

## Consideraciones Especiales

### Deep Linking
- Shell proporciona deep linking automático
- NavigationPage requiere implementación manual
- Para notificaciones push, implementar handler manual

### Flyout Menu
- Shell tiene Flyout integrado
- NavigationPage requiere FlyoutPage o menú personalizado
- Implementar FlyoutPage o menú hamburguesa personalizado

### TabBar
- Shell TabBar se convierte en TabbedPage
- Cada tab necesita su propia NavigationPage
- Mantener la misma estructura visual

### Autenticación
- Verificar autenticación antes de mostrar TabbedPage
- Redirigir a LoginPage si no está autenticado
- Manejar cambios de estado de autenticación

## Testing

### Checklist de Pruebas
- [ ] Login funciona correctamente
- [ ] Navegación entre páginas principales
- [ ] TabBar funciona (3 tabs)
- [ ] Flyout/Menú funciona
- [ ] Navegación hacia atrás funciona
- [ ] Pasar parámetros entre páginas funciona
- [ ] Autenticación y redirección funcionan
- [ ] Cambio de contraseña funciona
- [ ] Historial de inspecciones funciona
- [ ] Detalles de inspección funciona
- [ ] Captura de fotos funciona

## Riesgos y Mitigación

### Riesgo 1: Pérdida de funcionalidad
- **Mitigación**: Testing exhaustivo antes de eliminar Shell

### Riesgo 2: Deep linking no funciona
- **Mitigación**: Implementar handler manual para deep linking

### Riesgo 3: UI diferente
- **Mitigación**: Mantener diseño visual similar

## Tiempo Estimado
- Fase 1-2: 2-3 horas
- Fase 3-4: 4-6 horas
- Fase 5-6: 2-3 horas
- **Total**: 8-12 horas

## Referencias
- [MAUI NavigationPage Documentation](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/pages/navigationpage)
- [MAUI TabbedPage Documentation](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/pages/tabbedpage)
- [MAUI FlyoutPage Documentation](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/pages/flyoutpage)

