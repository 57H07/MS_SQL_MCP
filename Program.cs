using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using MS_SQL_MCP;

var builder = Host.CreateApplicationBuilder(args);

// Get connection string from command line arguments
string connectionString = args.Length > 0 ? args[0] : "";

if (string.IsNullOrEmpty(connectionString))
{
    Console.Error.WriteLine("Usage: MS_SQL_MCP <connection_string>");
    Console.Error.WriteLine("Example: MS_SQL_MCP \"Server=localhost;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True;\"");
    Environment.Exit(1);
}

// Register the connection string as a singleton
builder.Services.AddSingleton(new SqlConnectionConfig(connectionString));

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

await app.RunAsync();

namespace MS_SQL_MCP
{
    public record SqlConnectionConfig(string ConnectionString);
}
