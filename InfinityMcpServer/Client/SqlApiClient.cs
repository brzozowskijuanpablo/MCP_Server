 
using System.Text;
using System.Text.Json;

namespace InfinityMcpServer.Client
{
    public class SqlApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public SqlApiClient(string baseUrl)
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };

            // Agregar headers si tu API requiere autenticación
            // _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer YOUR_TOKEN");
        }

        public async Task<string> ExecuteQueryAsync(string query, string database)
        {
            try
            {
                var payload = new
                {
                    query = query,
                    database = database
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync("/api/query/execute", content);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();

                // Formatear el resultado para mejor legibilidad
                return FormatQueryResult(result);
            }
            catch (HttpRequestException ex)
            {
                return $"Error ejecutando query: {ex.Message}";
            }
        }

        public async Task<string> ListDatabasesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/databases");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                return FormatListResult(result, "Bases de datos disponibles:");
            }
            catch (HttpRequestException ex)
            {
                return $"Error listando bases de datos: {ex.Message}";
            }
        }

        public async Task<string> GetTableSchemaAsync(string database, string table)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"/api/schema/{database}/{table}"
                );
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                return FormatSchemaResult(result, database, table);
            }
            catch (HttpRequestException ex)
            {
                return $"Error obteniendo esquema: {ex.Message}";
            }
        }

        private string FormatQueryResult(string jsonResult)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var doc = JsonDocument.Parse(jsonResult);
                return JsonSerializer.Serialize(doc, options);
            }
            catch
            {
                return jsonResult;
            }
        }

        private string FormatListResult(string jsonResult, string title)
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<string>>(jsonResult);
                if (items == null || items.Count == 0)
                    return "No se encontraron elementos.";

                var sb = new StringBuilder();
                sb.AppendLine(title);
                foreach (var item in items)
                {
                    sb.AppendLine($"  - {item}");
                }
                return sb.ToString();
            }
            catch
            {
                return jsonResult;
            }
        }

        private string FormatSchemaResult(string jsonResult, string database, string table)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var doc = JsonDocument.Parse(jsonResult);
                var formatted = JsonSerializer.Serialize(doc, options);
                return $"Esquema de {database}.{table}:\n{formatted}";
            }
            catch
            {
                return jsonResult;
            }
        }
    }
}