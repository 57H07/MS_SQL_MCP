using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace MS_SQL_MCP.Tools;

[McpServerToolType]
public class ListTables
{
    private readonly SqlConnectionConfig _config;

    public ListTables(SqlConnectionConfig config)
    {
        _config = config;
    }

    [McpServerTool, Description("Lists all tables in the database. Returns table names with their schemas.")]
    public async Task<string> ListAllTables(
        [Description("Optional schema name to filter tables. Leave empty for all schemas.")] string? schema = null)
    {
        var tables = new List<object>();

        using var connection = new SqlConnection(_config.ConnectionString);
        await connection.OpenAsync();

        var query = @"
            SELECT 
                TABLE_SCHEMA,
                TABLE_NAME,
                TABLE_TYPE
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE IN ('BASE TABLE', 'VIEW')";

        if (!string.IsNullOrEmpty(schema))
        {
            query += " AND TABLE_SCHEMA = @Schema";
        }

        query += " ORDER BY TABLE_SCHEMA, TABLE_NAME";

        using var command = new SqlCommand(query, connection);
        
        if (!string.IsNullOrEmpty(schema))
        {
            command.Parameters.AddWithValue("@Schema", schema);
        }

        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            tables.Add(new
            {
                Schema = reader.GetString(0),
                TableName = reader.GetString(1),
                Type = reader.GetString(2)
            });
        }

        return JsonSerializer.Serialize(new
        {
            Success = true,
            Count = tables.Count,
            Tables = tables
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
