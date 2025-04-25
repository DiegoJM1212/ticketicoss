using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;

namespace ticketicos
{
    public class AuthenticationFilter : IPageFilter
    {
        private readonly ILogger<AuthenticationFilter> _logger;

        public AuthenticationFilter(ILogger<AuthenticationFilter> logger)
        {
            _logger = logger;
        }

        public void OnPageHandlerSelected(PageHandlerSelectedContext context)
        {
            // No se requiere implementación
        }

        public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            // Obtener la ruta actual
            string currentPath = context.HttpContext.Request.Path.Value?.ToLower() ?? "";

            // Verificar si estamos ya en la página de login para evitar bucles de redirección
            if (currentPath.Contains("/cliente/login"))
            {
                // Ya estamos en la página de login, no hacer nada
                return;
            }

            // Verificar si la página requiere autenticación
            bool requiresAuth = RequiresAuthentication(currentPath);

            if (requiresAuth)
            {
                // Verificar si el usuario está autenticado
                int? userId = context.HttpContext.Session.GetInt32("UserId");

                // Si no está en la sesión, intentar obtenerlo de la cookie
                if (!userId.HasValue)
                {
                    string userIdStr = context.HttpContext.Request.Cookies["UserId"];
                    if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                    {
                        // Si se encuentra en la cookie, restaurar a la sesión también
                        userId = cookieUserId;
                        context.HttpContext.Session.SetInt32("UserId", cookieUserId);
                        _logger.LogInformation($"Usuario {userId} autenticado desde cookie");
                    }
                }

                // Si aún no está autenticado, redirigir a login
                if (!userId.HasValue)
                {
                    _logger.LogInformation($"Usuario no autenticado, redirigiendo a login desde {currentPath}");

                    // Crear una URL de retorno limpia (solo la ruta actual, sin query string)
                    string returnUrl = currentPath;

                    // Redirigir a login con la URL de retorno
                    context.Result = new RedirectToPageResult("/Cliente/Login", new { returnUrl });
                }
            }
        }

        public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            // No se requiere implementación
        }

        private bool RequiresAuthentication(string path)
        {
            // Páginas que no requieren autenticación
            string[] publicPages = new[]
            {
                "/cliente/login",
                "/cliente/registro",
                "/cliente/resetpassword",
                "/cliente/doblefactor",
                "/index",
                "/error",
                "/cliente/codigospromo",
                "/cliente/eventosdestacados"
            };

            // Verificar si la ruta actual está en la lista de páginas públicas
            foreach (var publicPage in publicPages)
            {
                if (path.Contains(publicPage))
                {
                    return false;
                }
            }

            // Por defecto, todas las demás páginas requieren autenticación
            return true;
        }
    }
}

