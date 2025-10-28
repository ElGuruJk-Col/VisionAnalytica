# **VisioAnalytica Suite**

Suite de aplicaciones de analítica e IA para la optimización de procesos empresariales. Incluye VisioAnalytica Risk, AdminPortal y más.

Este repositorio contiene el código fuente completo de la plataforma VisioAnalytica, diseñada bajo principios de Arquitectura Limpia y un enfoque de "Arquitectura Evolutiva" para un despliegue financieramente viable.

## **Productos de la Suite**

* VisioAnalytica Risk  
  App móvil (.NET MAUI) para la inspección y gestión de riesgos. Utiliza IA (Gemini/OpenAI) para analizar imágenes en tiempo real, identificar peligros (SST, operativos, etc.) y generar reportes.  
* VisioAnalytica Admin  
  App web (.NET 8 Blazor) que actúa como el "Centro de Mando" para la gestión de organizaciones, usuarios, plantillas de IA e informes consolidados.

## **Arquitectura**

La solución sigue una estricta separación de conceptos (Clean Architecture):

* **Core**: Define las reglas de negocio, modelos e interfaces (ej. IAiSstAnalyzer).  
* **Infrastructure**: Implementa las interfaces de Core (Entity Framework, Gemini, Azure Blob).  
* **Api**: Expone la lógica de negocio como un backend .NET 8 API.  
* **Apps**: Contiene los "frontends" (MAUI, Blazor).  
* **tests**: Pruebas unitarias y de integración (xUnit).

## **Primeros Pasos**

1. **Requisitos Previos:**  
   * .NET 8 SDK  
   * Visual Studio 2022 (con cargas de trabajo MAUI, Web y Desktop)  
   * SQL Server 2022 Developer Edition (para desarrollo local)  
2. **Clonar el repositorio:**  
   git clone https://\[URL-DE-TU-REPO\]/visioanalytica-suite.git  
   cd visioanalytica-suite

3. Ejecutar el script de fundación:  
   (Solo la primera vez, para crear la estructura del proyecto)  
   .\\setup.ps1

4. **Abrir y configurar:**  
   * Abre src/VisioAnalytica.sln en Visual Studio.  
   * Configura tu cadena de conexión LocalSqlServerConnection en src/Api/appsettings.Development.json.  
   * Establece VisioAnalytica.Api como proyecto de inicio y ejecuta (F5).