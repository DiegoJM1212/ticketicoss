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
    public class TwoFactorAuthModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TwoFactorAuthModel> _logger;
        private readonly AuthService _authService;

        public TwoFactorAuthModel(IConfiguration configuration, ILogger<TwoFactorAuthModel> logger, AuthService authService)
        {
            _configuration = configuration;
            _logger = logger;
            _authService = authService;
        }

        [BindProperty]
        public string[] VerificationCode { get; set; } = new string[5];

        [BindProperty]
        public string FullVerificationCode { get; set; } = "";

        [TempData]
        public string? ErrorMessage { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = string.Empty;

        public IActionResult OnGet(string? returnUrl = "")
        {
            if (!string.IsNullOrEmpty(returnUrl))
            {
                ReturnUrl = returnUrl;
                _logger.LogInformation($"URL de retorno recibida en doblefactor: {ReturnUrl}");
            }

            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToPage("/Cliente/Login", new { returnUrl = ReturnUrl });
            }

            bool requiresTwoFactor = HttpContext.Session.GetString("RequiresTwoFactor") == "true";
            if (!requiresTwoFactor)
            {
                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    _logger.LogInformation($"Redirigiendo a URL de retorno: {ReturnUrl}");
                    return Redirect(ReturnUrl);
                }

                string tipoUsuario = HttpContext.Session.GetString("UserType") ?? "cliente";
                if (tipoUsuario == "cliente")
                {
                    return RedirectToPage("/Index");
                }
                else
                {
                    return RedirectToPage("/administrativo/Dashboard");
                }
            }

            bool twoFactorCompleted = HttpContext.Session.GetString("TwoFactorAuthenticated") == "true";
            if (twoFactorCompleted)
            {
                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    _logger.LogInformation($"Redirigiendo a URL de retorno: {ReturnUrl}");
                    return Redirect(ReturnUrl);
                }

                string tipoUsuario = HttpContext.Session.GetString("UserType") ?? "cliente";
                if (tipoUsuario == "cliente")
                {
                    return RedirectToPage("/Index");
                }
                else
                {
                    return RedirectToPage("/administrativo/AdminCodigosPromo");
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return RedirectToPage("/Cliente/Login", new { returnUrl = ReturnUrl });
                }

                string code;

                if (!string.IsNullOrEmpty(FullVerificationCode))
                {
                    code = FullVerificationCode.Trim();
                }
                else
                {
                    code = string.Join("", VerificationCode);
                }

                if (string.IsNullOrEmpty(code) || code.Length != 5)
                {
                    ErrorMessage = "Por favor, ingresa el código de verificación completo.";
                    return Page();
                }

                string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                _logger.LogInformation($"Verificando código: {code} para usuario: {userId}");

                bool isValid = await _authService.VerifyTwoFactorCodeAsync(userId.Value, code, clientIp);

                if (!isValid)
                {
                    ErrorMessage = "Código de verificación inválido o expirado. Por favor, solicita un nuevo código.";
                    return Page();
                }

                HttpContext.Session.SetString("TwoFactorAuthenticated", "true");
                HttpContext.Session.Remove("RequiresTwoFactor");

                _logger.LogInformation($"Usuario {userId} completó autenticación de doble factor exitosamente");

                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    _logger.LogInformation($"Redirigiendo a URL de retorno después de 2FA: {ReturnUrl}");
                    return Redirect(ReturnUrl);
                }

                string tipoUsuario = HttpContext.Session.GetString("UserType") ?? "cliente";
                if (tipoUsuario == "cliente")
                {
                    return RedirectToPage("/Index");
                }
                else
                {
                    return RedirectToPage("/administrativo/Dashboard");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la verificación de doble factor");
                ErrorMessage = "Ocurrió un error al procesar tu solicitud. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostResendCodeAsync()
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return new JsonResult(new { success = false, message = "Sesión inválida. Por favor, inicia sesión nuevamente." });
                }

                _logger.LogInformation($"Solicitando reenvío de código para usuario {userId}");

                bool codeSent = await _authService.GenerateAndSendTwoFactorCodeAsync(userId.Value);

                if (!codeSent)
                {
                    _logger.LogError($"No se pudo enviar el código para usuario {userId}");
                    return new JsonResult(new { success = false, message = "No se pudo enviar el código. Por favor, intenta nuevamente." });
                }

                _logger.LogInformation($"Código reenviado exitosamente para usuario {userId}");
                return new JsonResult(new { success = true, message = "Se ha enviado un nuevo código a tu correo electrónico. Este código expirará en 10 minutos." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reenviar código de verificación");
                return new JsonResult(new { success = false, message = "Ocurrió un error al enviar el código." });
            }
        }
    }
}