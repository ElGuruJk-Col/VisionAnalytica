# **Guía: Recrear Base de Datos con Entity Framework**

Esta guía te ayudará a eliminar y recrear completamente la base de datos usando Entity Framework Core cuando haya problemas de sincronización entre el modelo y la base de datos.

## **⚠️ Advertencia Importante**

**ANTES DE PROCEDER:**
- ⚠️ **Haz un backup de tu base de datos** si contiene datos importantes
- ⚠️ **Todos los datos se perderán** al eliminar la base de datos
- ⚠️ Asegúrate de tener la cadena de conexión correcta configurada

---

## **Paso 1: Hacer Backup de la Base de Datos (Recomendado)**

Si tienes datos importantes, crea un backup antes de proceder:

### **Desde SQL Server Management Studio (SSMS):**

```sql
-- Conectarte a tu instancia de SQL Server
USE master;
GO

-- Crear backup
BACKUP DATABASE [TuNombreDeBaseDeDatos] 
TO DISK = 'C:\Backups\TuNombreDeBaseDeDatos.bak'
WITH FORMAT, COMPRESSION;
GO
```

### **Desde PowerShell:**

```powershell
# Reemplaza los valores según tu configuración
$server = "localhost,1401"
$database = "TuNombreDeBaseDeDatos"
$backupPath = "C:\Backups\$database-$(Get-Date -Format 'yyyyMMdd-HHmmss').bak"
$user = "sa"
$password = "tu_password"

sqlcmd -S $server -U $user -P $password -Q "BACKUP DATABASE [$database] TO DISK = '$backupPath' WITH FORMAT, COMPRESSION"
```

---

## **Paso 2: Eliminar la Base de Datos**

### **Opción A: Desde SQL Server Management Studio (SSMS)**

1. Abre **SQL Server Management Studio**
2. Conéctate a tu instancia de SQL Server
3. Ejecuta el siguiente script:

```sql
USE master;
GO

-- Eliminar la base de datos si existe
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'TuNombreDeBaseDeDatos')
BEGIN
    -- Cerrar todas las conexiones activas
    ALTER DATABASE [TuNombreDeBaseDeDatos] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    
    -- Eliminar la base de datos
    DROP DATABASE [TuNombreDeBaseDeDatos];
    
    PRINT 'Base de datos eliminada correctamente.';
END
ELSE
BEGIN
    PRINT 'La base de datos no existe.';
END
GO
```

**⚠️ Reemplaza `TuNombreDeBaseDeDatos` con el nombre real de tu base de datos.**

### **Opción B: Desde PowerShell**

```powershell
# Configuración (ajusta según tu entorno)
$server = "localhost,1401"
$database = "TuNombreDeBaseDeDatos"
$user = "sa"
$password = "tu_password"

# Eliminar la base de datos
sqlcmd -S $server -U $user -P $password -Q "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database];"
```

---

## **Paso 3: Verificar que la Base de Datos fue Eliminada**

En SSMS, ejecuta:

```sql
SELECT name FROM sys.databases WHERE name = 'TuNombreDeBaseDeDatos';
```

Si no devuelve resultados, la base de datos fue eliminada correctamente.

---

## **Paso 4: Aplicar Todas las Migraciones desde Cero**

Desde PowerShell, navega a la raíz del proyecto y ejecuta:

```powershell
# Navegar a la raíz del proyecto
cd D:\Cursor\Proyectos\VisioAnalytica\VisionAnalytica

# Aplicar todas las migraciones (esto creará la base de datos si no existe)
dotnet ef database update --project src/Infrastructure --startup-project src/api
```

### **Con salida detallada (verbose):**

```powershell
dotnet ef database update --project src/Infrastructure --startup-project src/api --verbose
```

Esto ejecutará todas las migraciones en orden:
1. `20251029200845_InitialCreate` - Crea las tablas base (Organizations, AspNetUsers, etc.)
2. `20251102121656_AddAnalysisPersistenceEntities` - Crea las tablas Inspections y Findings con todas las columnas, incluyendo `OrganizationId`

---

## **Paso 5: Verificar que la Base de Datos se Creó Correctamente**

### **5.1. Verificar que la Base de Datos Existe**

En SSMS:

```sql
SELECT name FROM sys.databases WHERE name = 'TuNombreDeBaseDeDatos';
```

### **5.2. Verificar que la Tabla Inspections Tiene la Columna OrganizationId**

```sql
USE [TuNombreDeBaseDeDatos];
GO

-- Ver todas las columnas de la tabla Inspections
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Inspections'
ORDER BY ORDINAL_POSITION;
GO
```

**Deberías ver:**
- `Id` (uniqueidentifier)
- `AnalysisDate` (datetime2)
- `ImageUrl` (nvarchar(255))
- `UserId` (uniqueidentifier)
- **`OrganizationId` (uniqueidentifier)** ← **Esta es la columna crítica**

### **5.3. Verificar Todas las Tablas Creadas**

```sql
USE [TuNombreDeBaseDeDatos];
GO

SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;
GO
```

**Deberías ver:**
- `AspNetRoles`
- `AspNetUsers`
- `AspNetUserClaims`
- `AspNetUserLogins`
- `AspNetUserRoles`
- `AspNetUserTokens`
- `AspNetRoleClaims`
- `Findings`
- `Inspections`
- `Organizations`
- `__EFMigrationsHistory`

### **5.4. Verificar las Foreign Keys**

```sql
USE [TuNombreDeBaseDeDatos];
GO

SELECT 
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS TableName,
    COL_NAME(fc.parent_object_id, fc.parent_column_id) AS ColumnName,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTableName,
    COL_NAME(fc.referenced_object_id, fc.referenced_column_id) AS ReferencedColumnName
FROM sys.foreign_keys AS fk
INNER JOIN sys.foreign_key_columns AS fc ON fk.object_id = fc.constraint_object_id
WHERE OBJECT_NAME(fk.parent_object_id) = 'Inspections'
ORDER BY fk.name;
GO
```

**Deberías ver:**
- `FK_Inspections_AspNetUsers_UserId`
- `FK_Inspections_Organizations_OrganizationId` ← **Esta es la clave foránea crítica**

### **5.5. Verificar los Índices**

```sql
USE [TuNombreDeBaseDeDatos];
GO

SELECT 
    i.name AS IndexName,
    COL_NAME(ic.object_id, ic.column_id) AS ColumnName
FROM sys.indexes AS i
INNER JOIN sys.index_columns AS ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
WHERE OBJECT_NAME(i.object_id) = 'Inspections'
ORDER BY i.name, ic.key_ordinal;
GO
```

**Deberías ver:**
- `PK_Inspections` (en `Id`)
- `IX_Inspections_OrganizationId` (en `OrganizationId`) ← **Este índice es importante**
- `IX_Inspections_UserId` (en `UserId`)

---

## **Paso 6: Verificar el Historial de Migraciones**

```sql
USE [TuNombreDeBaseDeDatos];
GO

SELECT 
    MigrationId,
    ProductVersion
FROM [__EFMigrationsHistory]
ORDER BY MigrationId;
GO
```

**Deberías ver:**
- `20251029200845_InitialCreate`
- `20251102121656_AddAnalysisPersistenceEntities`

---

## **Comandos Útiles Adicionales**

### **Generar Script SQL de Todas las Migraciones**

Si quieres ver o ejecutar manualmente el script SQL:

```powershell
dotnet ef migrations script --project src/Infrastructure --startup-project src/api --output migration-script.sql
```

Esto generará un archivo `migration-script.sql` en la raíz del proyecto que puedes ejecutar en SSMS.

### **Listar Todas las Migraciones Disponibles**

```powershell
dotnet ef migrations list --project src/Infrastructure --startup-project src/api
```

### **Ver el Estado de las Migraciones en la Base de Datos**

```sql
USE [TuNombreDeBaseDeDatos];
GO

SELECT * FROM [__EFMigrationsHistory];
GO
```

---

## **Solución de Problemas**

### **Error: "Cannot drop database because it is currently in use"**

**Solución:** Cierra todas las conexiones a la base de datos:

```sql
USE master;
GO

ALTER DATABASE [TuNombreDeBaseDeDatos] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [TuNombreDeBaseDeDatos];
GO
```

### **Error: "Invalid column name 'OrganizationId'" después de recrear**

**Solución:** 
1. Verifica que todas las migraciones se aplicaron correctamente (Paso 5.2)
2. Si la columna no existe, ejecuta manualmente:

```sql
ALTER TABLE [Inspections]
ADD [OrganizationId] uniqueidentifier NOT NULL;

ALTER TABLE [Inspections]
ADD CONSTRAINT [FK_Inspections_Organizations_OrganizationId]
FOREIGN KEY ([OrganizationId]) 
REFERENCES [Organizations] ([Id]) 
ON DELETE NO ACTION;

CREATE INDEX [IX_Inspections_OrganizationId] 
ON [Inspections] ([OrganizationId]);
```

### **Error: "Migration already applied" pero la columna no existe**

**Solución:** 
1. Elimina el registro de la migración de la tabla `__EFMigrationsHistory`:

```sql
DELETE FROM [__EFMigrationsHistory] 
WHERE MigrationId = '20251102121656_AddAnalysisPersistenceEntities';
```

2. Luego ejecuta nuevamente:

```powershell
dotnet ef database update --project src/Infrastructure --startup-project src/api
```

---

## **Resumen de Comandos Rápidos**

```powershell
# 1. Eliminar BD (desde SSMS o PowerShell)
# sqlcmd -S localhost,1401 -U sa -P "password" -Q "DROP DATABASE [NombreBD]"

# 2. Recrear BD con todas las migraciones
cd D:\Cursor\Proyectos\VisioAnalytica\VisionAnalytica
dotnet ef database update --project src/Infrastructure --startup-project src/api

# 3. Verificar migraciones aplicadas
dotnet ef migrations list --project src/Infrastructure --startup-project src/api
```

---

## **Notas Finales**

- ✅ Después de recrear la base de datos, todas las tablas estarán vacías
- ✅ Necesitarás crear nuevos usuarios y organizaciones
- ✅ La estructura de la base de datos estará completamente sincronizada con el modelo de Entity Framework
- ✅ La columna `OrganizationId` estará presente en la tabla `Inspections`

---

**Última actualización:** Noviembre 2025

