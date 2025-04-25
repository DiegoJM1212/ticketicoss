using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using ticketicos.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

namespace ticketicos.Pages.Cliente
{
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoginModel> _logger;
        private readonly AuthService _authService;

        public LoginModel(IConfiguration configuration, ILogger<LoginModel> logger, AuthService authService)
        {
            _configuration = configuration;
            _logger = logger;
            _authService = authService;
        }

        [BindProperty]
        public string Correo { get; set; } = string.Empty;

        [BindProperty]
        public string Contraseña { get; set; } = string.Empty;

        [TempData]
        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = string.Empty;

        public IActionResult OnGet(string? returnUrl = "")
        {
            if (!string.IsNullOrEmpty(returnUrl))
            {
                ReturnUrl = returnUrl;
                _logger.LogInformation($"URL de retorno recibida: {ReturnUrl}");
            }

            int? userId = HttpContext.Session.GetInt32("UserId");
            string? twoFactorAuth = HttpContext.Session.GetString("TwoFactorAuthenticated");

            if (userId.HasValue && twoFactorAuth == "true")
            {
                _logger.LogInformation($"Usuario {userId} ya autenticado, verificando URL de retorno");

                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    _logger.LogInformation($"Redirigiendo a URL de retorno: {ReturnUrl}");
                    return Redirect(ReturnUrl);
                }

                string tipoUsuario = GetUserType(userId.Value);

                if (tipoUsuario == "cliente")
                {
                    return RedirectToPage("/Index");
                }
                else
                {
                    return RedirectToPage("/administrativo/Dashboard");
                }
            }

            string? userIdStr = Request.Cookies["UserId"];
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
            {
                HttpContext.Session.SetInt32("UserId", cookieUserId);
                HttpContext.Session.SetString("TwoFactorAuthenticated", "true");

                string tipoUsuario = GetUserType(cookieUserId);
                HttpContext.Session.SetString("UserType", tipoUsuario);

                _logger.LogInformation($"Usuario {cookieUserId} autenticado desde cookie");

                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    _logger.LogInformation($"Redirigiendo a URL de retorno: {ReturnUrl}");
                    return Redirect(ReturnUrl);
                }

                if (tipoUsuario == "cliente")
                {
                    return RedirectToPage("/Index");
                }
                else
                {
                    return RedirectToPage("/administrativo/Dashboard");
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(Correo) || string.IsNullOrEmpty(Contraseña))
                {
                    ErrorMessage = "Por favor, ingresa tu correo y contraseña.";
                    return Page();
                }

                string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                var (userId, requiresTwoFactor) = await _authService.ValidateCredentialsAsync(Correo, Contraseña, clientIp);

                if (!userId.HasValue)
                {
                    ErrorMessage = "Correo o contraseña incorrectos.";
                    return Page();
                }

                HttpContext.Session.SetInt32("UserId", userId.Value);
                HttpContext.Session.SetString("UserEmail", Correo);

                Response.Cookies.Append("UserId", userId.Value.ToString(), new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTime.Now.AddDays(7)
                });

                string tipoUsuario = GetUserType(userId.Value);
                HttpContext.Session.SetString("UserType", tipoUsuario);

                if (requiresTwoFactor)
                {
                    HttpContext.Session.SetString("RequiresTwoFactor", "true");

                    bool codeSent = await _authService.GenerateAndSendTwoFactorCodeAsync(userId.Value);

                    if (!codeSent)
                    {
                        ErrorMessage = "No se pudo enviar el código de verificación. Por favor, intenta nuevamente.";
                        return Page();
                    }

                    return RedirectToPage("/Cliente/doblefactor", new { returnUrl = ReturnUrl });
                }
                else
                {
                    HttpContext.Session.SetString("TwoFactorAuthenticated", "true");

                    _logger.LogInformation($"Usuario {userId.Value} autenticado exitosamente. Tipo: {tipoUsuario}");

                    if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                    {
                        _logger.LogInformation($"Redirigiendo a URL de retorno: {ReturnUrl}");
                        return Redirect(ReturnUrl);
                    }

                    if (tipoUsuario == "cliente")
                    {
                        return RedirectToPage("/Index");
                    }
                    else
                    {
                        return RedirectToPage("/administrativo/Dashboard");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el proceso de login");
                ErrorMessage = "Ocurrió un error al procesar tu solicitud. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        private string GetUserType(int userId)
        {
            try
            {
                string? connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("La cadena de conexión 'DefaultConnection' no está configurada");
                    return "cliente";
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT tipo_usuario FROM Usuarios WHERE id_usuario = @UserId";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        object? result = command.ExecuteScalar();
                        return result?.ToString() ?? "cliente";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener el tipo de usuario {userId}");
                return "cliente";
            }
        }
    }
}
