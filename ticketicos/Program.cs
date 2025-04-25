using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using ticketicos.Data;
using ticketicos.Hubs;
using ticketicos.Services;

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios de conversión a PDF
builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
builder.Services.AddSignalR(); // Agregar SignalR
// Agregar cadena de conexión a SQL Server
var connectionString = "Server=DESKTOP-02SKPLN;Database=SistemaVentaBoletosVL1;User Id=sa;Password=AdminM27;TrustServerCertificate=True;MultipleActiveResultSets=True;";

// Registrar los contextos de base de datos
builder.Services.AddDbContext<VerEventoContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Configurar sesiones
builder.Services.AddDistributedMemoryCache(); // Cache en memoria para sesiones
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Tiempo de expiración de la sesión
    options.Cookie.HttpOnly = true; // Seguridad
    options.Cookie.IsEssential = true; // GDPR
});

// Registrar servicios personalizados
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmailService>();

// Agregar Razor Pages
builder.Services.AddRazorPages();

// Configurar logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Configurar autenticación y autorización
builder.Services.AddAuthentication().AddCookie(options =>
{
    options.LoginPath = "/Cliente/Login";
    options.AccessDeniedPath = "/Cliente/Login";
});
builder.Services.AddAuthorization();

var app = builder.Build();

// Configurar el pipeline de la solicitud HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession(); // Habilitar sesiones
app.UseAuthentication(); // No olvides esta línea
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<ChatHub>("/chatHub"); // Mapear el hub

app.Run();
