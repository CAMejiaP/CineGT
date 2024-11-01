using CineGT.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace CineGT.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            // Construir la cadena de conexión con las credenciales ingresadas
            string connectionString = $"Server=tcp:DESKTOP-P1Q2Q5U;Database=CineGT;User Id={username};Password={password};TrustServerCertificate=True;";

            try
            {
                // Intentar abrir una conexión para verificar las credenciales
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Guardar la cadena de conexión en la sesión del usuario
                    HttpContext.Session.SetString("UserConnectionString", connectionString);
                }

                // Llamar a un método para determinar el rol del usuario
                return RedirectToAction("UserDashboard");
            }
            catch
            {
                // Manejar error de autenticación
                ModelState.AddModelError("", "Credenciales incorrectas.");
                return View("Index");
            }
        }

        public IActionResult UserDashboard()
        {
            // Obtener la cadena de conexión del usuario desde la sesión
            string connectionString = HttpContext.Session.GetString("UserConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                return RedirectToAction("Index"); // Redirigir al login si no hay conexión
            }

            string role = null;
            string userName = new SqlConnectionStringBuilder(connectionString).UserID;

            // Conectar a la base de datos para determinar el rol del usuario
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = @"
                select r.name as Role
                from master.sys.server_role_members rm
                inner join master.sys.server_principals r on r.principal_id = rm.role_principal_id and r.type = 'R'
                inner join master.sys.server_principals m on m.principal_id = rm.member_principal_id
                where m.name = @UserName;";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserName", userName);
                    role = (string)command.ExecuteScalar();
                }
            }

            // Redirigir o mostrar vistas en función del rol
            if (role == "Ventas")
            {
                return View("VentasDashboard");
            }
            else if (role == "Reportes")
            {
                return View("ReportesDashboard");
            }

            return View("AccessDenied");
        }



        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
