// See https://aka.ms/new-console-template for more information
//InfinityMcpServer
using InfinityMcpServer.Client;
using InfinityMcpServer.Server;
using Microsoft.Extensions.Logging;


// Configurar logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<McpServer>();

// Obtener URL de la API desde variable de entorno o usar default
var apiUrl = Environment.GetEnvironmentVariable("SQL_API_URL")
    ?? "http://localhost:7000";

logger.LogInformation("Conectando a API en: {ApiUrl}", apiUrl);

// Crear instancias
var apiClient = new SqlApiClient(apiUrl);
var server = new McpServer(logger, apiClient);

// Ejecutar servidor
await server.RunAsync();
