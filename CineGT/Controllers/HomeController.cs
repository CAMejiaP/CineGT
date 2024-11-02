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
            string connectionString = $"Server=tcp:DESKTOP-P1Q2Q5U;Database=CineGT;User Id={username};Password={password};TrustServerCertificate=True;";

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
        [HttpPost]
        public IActionResult GetParametersProcedure(string procedureName)
        {
            // Obtener la cadena de conexi�n del usuario desde la sesi�n
            string connectionString = HttpContext.Session.GetString("UserConnectionString");
            procedureName = "sp_" + procedureName.Replace(" ", "_");


            // Lista para almacenar los par�metros y sus caracter�sticas
            List<ProcedureParameter> parameters = new List<ProcedureParameter>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Especifica solo el nombre del procedimiento almacenado
                string query = "Todos.sp_get_procedure_parameters";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@ProcedureName", procedureName);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Crear una instancia de ProcedureParameter para cada par�metro
                            var parameter = new ProcedureParameter
                            {
                                ProcedureName = procedureName,
                                ParameterName = reader.GetString(0),
                                DataType = reader.GetString(1),
                                MaxLength = reader.GetInt16(2),
                                IsOutput = reader.GetBoolean(3),
                                Value = ""  // Inicializa el valor vac�o
                            };

                            // Ajusta el tipo de datos para el input HTML seg�n el tipo de SQL
                            parameter.DataType = parameter.DataType switch
                            {
                                "int" => "number",
                                "varchar" => "text",
                                _ => "text"
                            };

                            parameters.Add(parameter);
                        }
                    }
                }
            }

            // Devolver la lista de par�metros a la vista
            return View("Menu Ingreso", parameters);
        }

        [HttpPost]
        public IActionResult ExecuteProcedure(string ProcedureName, List<ProcedureParameter> parameters)
        {
            try
            {
                // Obtener la cadena de conexi�n del usuario desde la sesi�n
                string connectionString = HttpContext.Session.GetString("UserConnectionString");

                // Extraer el nombre de usuario y rol
                string userName = new SqlConnectionStringBuilder(connectionString).UserID;
                string role = null;

                // Verificar y construir el nombre del procedimiento con el rol
                if (!ProcedureName.Contains("."))
                {
                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = "Todos.sp_mi_rol @UserName";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@UserName", userName);
                            role = (string)command.ExecuteScalar();
                        }
                        connection.Close();
                    }

                    ProcedureName = $"{role}.{ProcedureName}";
                }

                // Asignar el nombre del procedimiento a todos los par�metros
                parameters.ForEach(param => param.ProcedureName = ProcedureName);

                // Ejecutar el procedimiento y manejar resultados
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (var command = new SqlCommand(ProcedureName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // Add parameters to the command
                        foreach (var param in parameters)
                        {
                            SqlParameter sqlParam = new SqlParameter(param.ParameterName, param.Value);
                            sqlParam.SqlDbType = param.DataType == "number" ? SqlDbType.Int : SqlDbType.VarChar;
                            sqlParam.Size = param.MaxLength;
                            sqlParam.Value = string.IsNullOrEmpty(param.Value) ? (object)DBNull.Value : param.Value;
                            sqlParam.Direction = param.IsOutput ? ParameterDirection.Output : ParameterDirection.Input;
                            command.Parameters.Add(sqlParam);
                        }

                        // Create a DataTable to store the result
                        DataTable resultTable = new DataTable();

                        // Try to fill the DataTable with SqlDataAdapter
                        using (var adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(resultTable);
                        }

                        // Check if resultTable has rows
                        if (resultTable.Rows.Count > 0)
                        {
                            // If it has rows, assign it to ViewBag for display
                            ViewBag.ResultTable = resultTable;
                        }
                        else
                        {
                            // Collect output parameter values
                            var outputResults = new Dictionary<string, object>();
                            foreach (SqlParameter sqlParam in command.Parameters)
                            {
                                if (sqlParam.Direction == ParameterDirection.Output)
                                {
                                    outputResults[sqlParam.ParameterName] = sqlParam.Value;
                                }
                            }

                            ViewBag.OutputResults = outputResults;
                        }
                    }
                }


                return View("Menu Ingreso", parameters);
            }
            catch (SqlException ex)
            {
                // Pasar el mensaje de error a la vista
                ViewBag.ErrorMessage = ex.Message;
                return View("Menu Ingreso", parameters);
            }
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
