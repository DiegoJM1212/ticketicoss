using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestSharp;
using RestSharp.Authenticators;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace ticketicos.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly string _connectionString;
        private string _mailgunDomain;
        private string _mailgunApiKey;
        private readonly Dictionary<string, string> _configCache;

        private static readonly Dictionary<int, (string Code, DateTime Expiration)> VerificationCodes = new Dictionary<int, (string, DateTime)>();

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");

            _configCache = new Dictionary<string, string>();

            // Cargar configuraciones desde la base de datos
            LoadConfigurationsAsync().GetAwaiter().GetResult();
        }

        private async Task LoadConfigurationsAsync()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT clave, valor FROM Configuraciones";
                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            _configCache.Clear();
                            while (await reader.ReadAsync())
                            {
                                var clave = reader.GetString(0);
                                var valor = reader.GetString(1);
                                _configCache[clave] = valor;
                                if (clave == "MailgunDomain") _mailgunDomain = valor;
                                if (clave == "MailgunApiKey") _mailgunApiKey = valor;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(_mailgunDomain) || string.IsNullOrEmpty(_mailgunApiKey))
                {
                    throw new InvalidOperationException("No se encontraron las credenciales de Mailgun en la base de datos.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar configuraciones desde la base de datos");
                throw;
            }
        }

        private async Task<string> GetConfigValueAsync(string clave, string defaultValue = "")
        {
            try
            {
                if (_configCache.ContainsKey(clave))
                {
                    return _configCache[clave];
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT valor FROM Configuraciones WHERE clave = @Clave";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Clave", clave);
                        var result = await command.ExecuteScalarAsync();
                        var value = result?.ToString() ?? defaultValue;
                        _configCache[clave] = value;
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener configuración {clave}");
                return defaultValue;
            }
        }

        private string FormatTemplate(string template, params object[] args)
        {
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, $"Error al formatear la plantilla: {template}. Usando valor por defecto.");
                return string.Join(" ", args); // Une los argumentos como fallback
            }
        }

        public async Task<bool> SendPasswordChangeNotificationAsync(string recipientEmail)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Soporte <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                request.AddParameter("subject", await GetConfigValueAsync("PasswordChangeSubject", "Notificación de Cambio de Contraseña"));
                request.AddParameter("text", await GetConfigValueAsync("PasswordChangeText", "Su contraseña ha sido cambiada exitosamente."));
                request.AddParameter("html", "<h1>Su contraseña ha sido cambiada exitosamente.</h1>");

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Notificación de cambio de contraseña enviada a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar notificación de cambio de contraseña: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar notificación de cambio de contraseña a {recipientEmail}");
                return false;
            }
        }

        public async Task<bool> SendVerificationCodeAsync(string recipientEmail, string verificationCode)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Verificación <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                var subject = await GetConfigValueAsync("VerificationCodeSubject", "Código de Verificación");
                request.AddParameter("subject", subject);
                var textTemplate = await GetConfigValueAsync("VerificationCodeText", "Su código de verificación es: {0}");
                request.AddParameter("text", FormatTemplate(textTemplate, verificationCode));
                request.AddParameter("html", $"<h1>Su código de verificación es: <strong>{verificationCode}</strong></h1>");

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Código de verificación enviado a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar código de verificación: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar código de verificación a {recipientEmail}");
                return false;
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(string recipientEmail, string resetLink)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Soporte <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                var subject = await GetConfigValueAsync("PasswordResetSubject", "Recuperación de Contraseña");
                request.AddParameter("subject", subject);
                var textTemplate = await GetConfigValueAsync("PasswordResetText", "Haz clic en el siguiente enlace para recuperar tu contraseña: {0}");
                request.AddParameter("text", FormatTemplate(textTemplate, resetLink));
                request.AddParameter("html", $"<h1>Recuperación de Contraseña</h1><p>Haz clic en el siguiente enlace para recuperar tu contraseña: <a href=\"{resetLink}\">{resetLink}</a></p>");

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Correo de recuperación de contraseña enviado a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar correo de recuperación de contraseña: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar correo de recuperación de contraseña a {recipientEmail}");
                return false;
            }
        }

        public async Task<bool> SendInvoiceAsync(string recipientEmail, string invoiceNumber, string invoiceHtml)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Facturación <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                var subjectTemplate = await GetConfigValueAsync("InvoiceSubject", "Factura #{0} - Tickicos");
                request.AddParameter("subject", FormatTemplate(subjectTemplate, invoiceNumber));
                var textTemplate = await GetConfigValueAsync("InvoiceText", "Adjuntamos su factura #{0}. Gracias por su compra.");
                request.AddParameter("text", FormatTemplate(textTemplate, invoiceNumber));
                request.AddParameter("html", invoiceHtml);

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Factura enviada a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar factura: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar factura a {recipientEmail}");
                return false;
            }
        }

        public async Task<bool> SendTicketsAsync(string recipientEmail, string eventName, string ticketsHtml)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Entradas <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                var subjectTemplate = await GetConfigValueAsync("TicketsSubject", "Tus entradas para {0} - Tickicos");
                request.AddParameter("subject", FormatTemplate(subjectTemplate, eventName));
                var textTemplate = await GetConfigValueAsync("TicketsText", "Adjuntamos tus entradas para {0}. ¡Disfruta del evento!");
                request.AddParameter("text", FormatTemplate(textTemplate, eventName));
                request.AddParameter("html", ticketsHtml);

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Entradas enviadas a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar entradas: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar entradas a {recipientEmail}");
                return false;
            }
        }

        public async Task<bool> SendPromoCodeAsync(string recipientEmail, string promoCode)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Promociones <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                var subjectTemplate = await GetConfigValueAsync("PromoCodeSubject", "Código Promocional");
                request.AddParameter("subject", subjectTemplate);
                var textTemplate = await GetConfigValueAsync("PromoCodeText", "Tu código promocional es: {0}");
                request.AddParameter("text", FormatTemplate(textTemplate, promoCode));
                request.AddParameter("html", $"<h1>Tu código promocional es: <strong>{promoCode}</strong></h1>");

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Código promocional enviado a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar código promocional: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar código promocional a {recipientEmail}");
                return false;
            }
        }

        public async Task<bool> GenerateAndSendTwoFactorCodeAsync(int userId)
        {
            try
            {
                string verificationCode = GenerateVerificationCode();
                DateTime expiration = DateTime.Now.AddMinutes(10);
                VerificationCodes[userId] = (verificationCode, expiration);

                _logger.LogInformation($"Nuevo código generado para usuario {userId}: {verificationCode}, expira: {expiration}");

                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);

                var request = new RestRequest("messages");
                request.AddParameter("from", $"Verificación <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", $"user{userId}@example.com");
                var subjectTemplate = await GetConfigValueAsync("TwoFactorSubject", "Código de Verificación");
                request.AddParameter("subject", subjectTemplate);
                var textTemplate = await GetConfigValueAsync("TwoFactorText", "Tu código de verificación es: {0}. Este código expirará en 10 minutos.");
                request.AddParameter("text", FormatTemplate(textTemplate, verificationCode));
                request.AddParameter("html", $"<h1>Tu código de verificación es: <strong>{verificationCode}</strong></h1><p>Este código expirará en <strong>10 minutos</strong>.</p>");

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Código de verificación enviado a user{userId}@example.com");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar código de verificación: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al generar o enviar el código de verificación para el usuario {userId}");
                return false;
            }
        }

        public async Task<bool> VerifyTwoFactorCodeAsync(int userId, string codeToVerify, string clientIp)
        {
            try
            {
                await Task.Delay(1);
                _logger.LogInformation($"Intentando verificar código para usuario {userId}: {codeToVerify}");

                if (VerificationCodes.ContainsKey(userId))
                {
                    var (storedCode, expiration) = VerificationCodes[userId];
                    if (DateTime.Now > expiration)
                    {
                        _logger.LogWarning($"Código de verificación expirado para el usuario {userId}. Expiró a las {expiration}");
                        return false;
                    }
                    if (storedCode == codeToVerify)
                    {
                        VerificationCodes.Remove(userId);
                        _logger.LogInformation($"Código de verificación correcto para el usuario {userId} desde IP {clientIp}");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning($"Código de verificación incorrecto para el usuario {userId} desde IP {clientIp}. Esperado: {storedCode}, Recibido: {codeToVerify}");
                        return false;
                    }
                }
                else
                {
                    _logger.LogWarning($"No se encontró código de verificación para el usuario {userId}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al verificar el código de verificación para el usuario {userId} desde IP {clientIp}");
                return false;
            }
        }

        private async Task<string> GetUserEmailAsync(int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("SELECT correo FROM Usuarios WHERE id_usuario = @userId", connection))
                    {
                        command.Parameters.AddWithValue("@userId", userId);
                        var result = await command.ExecuteScalarAsync();
                        return result?.ToString() ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener el correo electrónico del usuario {userId}");
                return string.Empty;
            }
        }

        private string GenerateVerificationCode()
        {
            return Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        }

        public async Task<bool> SendNewsletterAsync(string recipientEmail, string subject, string htmlContent)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Tickicos Marketing <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                request.AddParameter("subject", subject);
                request.AddParameter("text", "Este correo contiene contenido HTML. Por favor, usa un cliente de correo que soporte HTML.");
                request.AddParameter("html", htmlContent);

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Newsletter enviado a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar newsletter: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar newsletter a {recipientEmail}");
                return false;
            }
        }
    }
}