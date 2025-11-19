namespace VisioAnalytica.Core.Constants
{
    /// <summary>
    /// Constantes para los roles del sistema.
    /// Estos roles se usan con ASP.NET Core Identity.
    /// </summary>
    public static class Roles
    {
        /// <summary>
        /// Super Administrador - Equipo de VisioAnalytica.
        /// Tiene acceso completo al sistema, puede crear organizaciones y administradores.
        /// </summary>
        public const string SuperAdmin = "SuperAdmin";

        /// <summary>
        /// Administrador - Administrador de una organización cliente.
        /// Puede gestionar usuarios de su organización, crear empresas afiliadas,
        /// asignar inspectores y ver reportes de su organización.
        /// </summary>
        public const string Admin = "Admin";

        /// <summary>
        /// Inspector - Usuario que realiza auditorías.
        /// Puede realizar inspecciones a las empresas afiliadas asignadas,
        /// ver sus propias inspecciones y reportes.
        /// </summary>
        public const string Inspector = "Inspector";

        /// <summary>
        /// Cliente - Usuario de una empresa afiliada.
        /// Puede ver reportes y hallazgos de las auditorías realizadas a su empresa,
        /// con acceso limitado y de solo lectura.
        /// </summary>
        public const string Cliente = "Cliente";

        /// <summary>
        /// Obtiene todos los roles del sistema.
        /// </summary>
        public static string[] GetAll() => new[]
        {
            SuperAdmin,
            Admin,
            Inspector,
            Cliente
        };
    }
}

