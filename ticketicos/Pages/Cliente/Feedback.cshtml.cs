using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace ticketicos.Pages.Cliente
{
    public class FeedbackModel : PageModel
    {
        private readonly string _connectionString;

        public FeedbackModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        [BindProperty]
        public FeedbackData FeedbackData { get; set; }

        public void OnGet()
        {
            FeedbackData = new FeedbackData();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "INSERT INTO Feedback (Comentario, FechaFeedback) VALUES (@Comentario, GETDATE())";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Comentario", FeedbackData.Comentario);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                return RedirectToPage("/Index");
            }
            catch (SqlException)
            {
                ModelState.AddModelError("", "Error al guardar el feedback. Intenta de nuevo.");
                return Page();
            }
        }
    }

    public class FeedbackData
    {
        public string Comentario { get; set; }
    }
}