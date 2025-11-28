 
using System.Text.Json;
using InfinityMcpServer.Client;
using InfinityMcpServer.Models;
using Microsoft.Extensions.Logging;


namespace InfinityMcpServer.Server
{
    public class McpServer
    {
        private readonly ILogger<McpServer> _logger;
        private readonly SqlApiClient _apiClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public McpServer(ILogger<McpServer> logger, SqlApiClient apiClient)
        {
            _logger = logger;
            _apiClient = apiClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task RunAsync()
        {
            _logger.LogInformation("MCP Server iniciado. Esperando mensajes...");

            while (true)
            {
                var line = await Console.In.ReadLineAsync();
                if (string.IsNullOrEmpty(line))
                    break;

                try
                {
                    var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
                    if (request == null)
                        continue;

                    var response = await HandleRequestAsync(request);
                    var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                    await Console.Out.WriteLineAsync(responseJson);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando request");
                    var errorResponse = new JsonRpcResponse
                    {
                        Id = null,
                        Error = new JsonRpcError
                        {
                            Code = -32603,
                            Message = "Internal error",
                            Data = ex.Message
                        }
                    };
                    var errorJson = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                    await Console.Out.WriteLineAsync(errorJson);
                }
            }
        }

        private async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
        {
            _logger.LogInformation("Método recibido: {Method}", request.Method);

            return request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "initialized" => HandleInitialized(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => await HandleToolsCallAsync(request),
                _ => new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32601,
                        Message = $"Method not found: {request.Method}"
                    }
                }
            };
        }

        private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = new InitializeResult
                {
                    ProtocolVersion = "2024-11-05",
                    Capabilities = new ServerCapabilities
                    {
                        Tools = new { }
                    },
                    ServerInfo = new ServerInfo
                    {
                        Name = "sql-query-server",
                        Version = "1.0.0"
                    }
                }
            };
        }

        private JsonRpcResponse HandleInitialized(JsonRpcRequest request)
        {
            _logger.LogInformation("Cliente inicializado");
            // Este método no requiere respuesta según el protocolo MCP
            return new JsonRpcResponse { Id = request.Id, Result = new { } };
        }

        private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
        {
            var tools = new ToolsList
            {
                Tools = new List<Tool>
                {
                    new Tool
                    {
                        Name = "execute_query",
                        Description = "Ejecuta una consulta SQL a través de la API",
                        InputSchema = new InputSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, PropertySchema>
                            {
                                ["query"] = new PropertySchema
                                {
                                    Type = "string",
                                    Description = "La consulta SQL a ejecutar"
                                },
                                ["database"] = new PropertySchema
                                {
                                    Type = "string",
                                    Description = "Nombre de la base de datos"
                                }
                            },
                            Required = new List<string> { "query", "database" }
                        }
                    },
                    new Tool
                    {
                        Name = "list_databases",
                        Description = "Lista todas las bases de datos disponibles",
                        InputSchema = new InputSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, PropertySchema>(),
                            Required = new List<string>()
                        }
                    },
                    new Tool
                    {
                        Name = "get_table_schema",
                        Description = "Obtiene el esquema de una tabla específica",
                        InputSchema = new InputSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, PropertySchema>
                            {
                                ["database"] = new PropertySchema
                                {
                                    Type = "string",
                                    Description = "Nombre de la base de datos"
                                },
                                ["table"] = new PropertySchema
                                {
                                    Type = "string",
                                    Description = "Nombre de la tabla"
                                }
                            },
                            Required = new List<string> { "database", "table" }
                        }
                    }
                }
            };

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = tools
            };
        }

        private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request)
        {
            try
            {
                var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
                var toolCall = JsonSerializer.Deserialize<ToolCallParams>(paramsJson, _jsonOptions);

                if (toolCall == null)
                {
                    return new JsonRpcResponse
                    {
                        Id = request.Id,
                        Error = new JsonRpcError
                        {
                            Code = -32602,
                            Message = "Invalid params"
                        }
                    };
                }

                string resultText = toolCall.Name switch
                {
                    "execute_query" => await _apiClient.ExecuteQueryAsync(
                        toolCall.Arguments?["query"]?.ToString() ?? "",
                        toolCall.Arguments?["database"]?.ToString() ?? ""
                    ),
                    "list_databases" => await _apiClient.ListDatabasesAsync(),
                    "get_table_schema" => await _apiClient.GetTableSchemaAsync(
                        toolCall.Arguments?["database"]?.ToString() ?? "",
                        toolCall.Arguments?["table"]?.ToString() ?? ""
                    ),
                    _ => $"Tool desconocida: {toolCall.Name}"
                };

                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = new ToolCallResult
                    {
                        Content = new List<Content>
                        {
                            new Content
                            {
                                Type = "text",
                                Text = resultText
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando tool");
                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32603,
                        Message = "Error ejecutando tool",
                        Data = ex.Message
                    }
                };
            }
        }
    }
}