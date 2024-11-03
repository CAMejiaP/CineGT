using CineGT.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
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
                    connection.Close();
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
            // Obtener la cadena de conexión del usuario desde la sesión
            string connectionString = HttpContext.Session.GetString("UserConnectionString");
            procedureName = "sp_" + procedureName.Replace(" ", "_");

            // Lista para almacenar los parámetros y sus características
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
                            // Crear una instancia de ProcedureParameter para cada parámetro
                            var parameter = new ProcedureParameter
                            {
                                ProcedureName = procedureName,
                                ParameterName = reader.GetString(0),
                                DataType = reader.GetString(1),
                                MaxLength = reader.GetInt16(2),
                                IsOutput = reader.GetBoolean(3),
                                Value = ""  // Inicializa el valor vacío
                            };

                            // Ajusta el tipo de datos para el input HTML según el tipo de SQL
                            parameter.DataType = parameter.DataType switch
                            {
                                "int" => "number",
                                "varchar" => "text",
                                "datetime" => "datetime",
                                _ => "text"
                            };

                            parameters.Add(parameter);
                        }
                    }
                }
            }

            // Serializar los parámetros en JSON y almacenarlos en TempData
            TempData["ProcedureParameters"] = JsonConvert.SerializeObject(parameters);

            if (procedureName == "sp_venta_asiento_manual")
            {
                return RedirectToAction("ProximasSesiones");
            }

            return View("Menu Ingreso", parameters);
        }


        [HttpPost]
        public IActionResult ExecuteProcedure(string ProcedureName, List<ProcedureParameter> parameters)
        {
            try
            {
                // Obtener la cadena de conexión del usuario desde la sesión
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

                // Asignar el nombre del procedimiento a todos los parámetros
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

        public IActionResult ProximasSesiones()
        {
            // Verificar si hay parámetros almacenados en TempData
            List<ProcedureParameter> parameters = new List<ProcedureParameter>();
            if (TempData.ContainsKey("ProcedureParameters"))
            {
                parameters = JsonConvert.DeserializeObject<List<ProcedureParameter>>(TempData["ProcedureParameters"].ToString());
            }

            // Ejecutar el procedimiento almacenado y obtener los resultados
            var sesiones = new List<(int idSesion, string nombrePelicula, DateTime fechaInicioSesion)>();

            try
            {
                string connectionString = HttpContext.Session.GetString("UserConnectionString");
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand("Ventas.sp_proximax_sesiones", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                sesiones.Add((
                                    reader.GetInt32(0),
                                    reader.GetString(1),
                                    reader.GetDateTime(2)
                                ));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                ViewBag.ErrorMessage = ex.Message;
            }

            return View("ProximasSesiones", (sesiones, parameters));
        }

        [HttpPost]
        public IActionResult MuestreoAsientos(int idSesion, List<ProcedureParameter> ProcedureParameters)
        {
            foreach (ProcedureParameter actual in ProcedureParameters)
            {
                if (actual.ParameterName == "@id_sesion")
                {
                    actual.Value = Convert.ToString(idSesion);
                    break;
                }
            }

            var asientos = new List<(int idAsiento, bool ocupado)>();

            try
            {
                string connectionString = HttpContext.Session.GetString("UserConnectionString");
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand("Ventas.sp_asientos_sesion", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // Agrega el parámetro @id_sesion al comando
                        command.Parameters.Add(new SqlParameter("@id_sesion", SqlDbType.Int) { Value = idSesion });

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                asientos.Add((
                                 reader.GetInt32(0),        // Lee `id_asiento` como `int`
                                 reader.GetInt32(1) != 0    // Convierte el valor `int` a `bool` comprobando si es distinto de 0
                             ));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                ViewBag.ErrorMessage = ex.Message;
            }

            return View("SeleccionManual", (ProcedureParameters,asientos));
        }


        [HttpPost]
        public IActionResult EjecutarProcedimiento(string asientos, List<ProcedureParameter> item1)
        {
            foreach (ProcedureParameter actual in item1)
            {
                if (actual.ParameterName == "@asientos")
                {
                    actual.Value = asientos;
                    break;
                }
            }            

            return ExecuteProcedure(item1[0].ProcedureName, item1);
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
