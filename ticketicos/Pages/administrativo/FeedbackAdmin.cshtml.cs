using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ticketicos.Pages.Admin
{
    public class FeedbackAdminModel : PageModel
    {
        private readonly ILogger<FeedbackAdminModel> _logger;
        private readonly string _connectionString;

        public FeedbackAdminModel(ILogger<FeedbackAdminModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
            FeedbackList = new List<FeedbackViewModel>();
        }

        public List<FeedbackViewModel> FeedbackList { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue || !EsAdmin(userId.Value))
            {
                return RedirectToPage("/Cliente/Login");
            }

            try
            {
                await CargarFeedbackAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar feedback: {Message}", ex.Message);
                TempData["ErrorMessage"] = $"Error al cargar los datos: {ex.Message}.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int idFeedback)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "DELETE FROM Feedback WHERE IdFeedback = @IdFeedback";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdFeedback", idFeedback);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                TempData["SuccessMessage"] = "Feedback eliminado correctamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar feedback: {Message}", ex.Message);
                TempData["ErrorMessage"] = $"Error al eliminar feedback: {ex.Message}.";
            }
            return RedirectToPage();
        }

        private bool EsAdmin(int userId)
        {
            // Reemplaza con tu lógica para verificar si es admin
            return true; // Temporal
        }

        private async Task CargarFeedbackAsync()
        {
            FeedbackList.Clear();
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT IdFeedback, Comentario, FechaFeedback FROM Feedback";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                FeedbackList.Add(new FeedbackViewModel
                                {
                                    IdFeedback = reader.GetInt32(0),
                                    Comentario = reader.GetString(1),
                                    FechaFeedback = reader.GetDateTime(2)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar feedback: {Message}", ex.Message);
                throw;
            }
        }

        public class FeedbackViewModel
        {
            public int IdFeedback { get; set; }
            public string Comentario { get; set; }
            public DateTime FechaFeedback { get; set; }
        }
    }
}