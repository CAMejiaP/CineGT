using CineGT.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
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
            // Construir la cadena de conexi�n con las credenciales ingresadas
            string connectionString = $"Server=tcp:LAPTOP-V7L0FOS4;Database=CineGT;User Id={username};Password={password};TrustServerCertificate=True;";

            try
            {
                // Intentar abrir una conexi�n para verificar las credenciales
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Guardar la cadena de conexi�n en la sesi�n del usuario
                    HttpContext.Session.SetString("UserConnectionString", connectionString);
                }

                // Llamar a un m�todo para determinar el rol del usuario
                return RedirectToAction("UserDashboard");
            }
            catch
            {
                // Manejar error de autenticaci�n
                ModelState.AddModelError("", "Credenciales incorrectas.");
                return View("Index");
            }
        }

        public IActionResult UserDashboard()
        {
            // Obtener la cadena de conexi�n del usuario desde la sesi�n
            string connectionString = HttpContext.Session.GetString("UserConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                return RedirectToAction("Index"); // Redirigir al login si no hay conexi�n
            }

            string role = null;
            string userName = new SqlConnectionStringBuilder(connectionString).UserID;

            // Conectar a la base de datos para determinar el rol del usuario
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "Todos.sp_mi_rol @UserName";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserName", userName);
                    role = (string)command.ExecuteScalar();
                }
            }

            if (role != null)
            {
                List<string> ProcedureNames = GetStoredProcedures(role);
                return View("VentasDashboard", ProcedureNames);
            }
            else
            {
                return View("AccessDenied");
            }

            }

        public List<string> GetStoredProcedures(string roleName)
        {
            var storedProcedures = new List<string>();

            // Retrieve connection string from session or configuration
            string connectionString = HttpContext.Session.GetString("UserConnectionString");

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Set up the command to call the stored procedure
                using (var command = new SqlCommand("Todos.sp_listado_sps", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    // Add parameter to stored procedure
                    command.Parameters.AddWithValue("@nombre", roleName);

                    // Execute and read results
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Read each row and add to the list as a tuple
                            string procedureName = reader.GetString(0);
                            procedureName = procedureName.Substring(3);
                            procedureName = procedureName.Replace("_", " ");
                            storedProcedures.Add(procedureName);
                        }
                    }
                }
            }

            return storedProcedures;
        }

        [HttpPost]
        public IActionResult GetParametersProcedure(string procedureName)
        {
            // Obtener la cadena de conexi�n del usuario desde la sesi�n
            string connectionString = HttpContext.Session.GetString("UserConnectionString");
            procedureName = "sp_" + procedureName.Replace(" ", "_");

            // Lista para almacenar los par�metros y sus caracter�sticas
            List<(string ParameterName, string DataType, int MaxLength, bool IsOutput)> parameters = new List<(string, string, int, bool)>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Especifica solo el nombre del procedimiento almacenado
                string query = "Todos.sp_get_procedure_parameters";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@ProcedureName", procedureName); // Aseg�rate de que el nombre coincide

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Leer cada columna de la fila actual
                            string parameterName = reader.GetString(0); // Nombre del par�metro
                            string dataType = reader.GetString(1);      // Tipo de datos
                            int maxLength = reader.GetInt16(2);         // Longitud m�xima
                            bool isOutput = reader.GetBoolean(3);       // Indicador de par�metro de salida

                            // Agregar la tupla a la lista
                            parameters.Add((parameterName, dataType, maxLength, isOutput));
                        }
                    }
                }
            }

            // Devolver la lista a la vista o manejar seg�n sea necesario
            return View("Menu Ingreso",parameters);
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
