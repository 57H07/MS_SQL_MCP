using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace MS_SQL_MCP.Tools;

[McpServerToolType]
public class CreateTable
{
    private readonly SqlConnectionConfig _config;

    public CreateTable(SqlConnectionConfig config)
    {
        _config = config;
    }

    [McpServerTool, Description("Creates a new table in the database with the specified columns and constraints.")]
    public async Task<string> CreateNewTable(
        [Description("The name of the table to create.")] string tableName,
        [Description("JSON array of column definitions. Each column should have: name, type, isNullable (optional, default true), isPrimaryKey (optional), isIdentity (optional), defaultValue (optional). Example: [{\"name\":\"Id\",\"type\":\"INT\",\"isPrimaryKey\":true,\"isIdentity\":true},{\"name\":\"Name\",\"type\":\"NVARCHAR(100)\",\"isNullable\":false}]")] string columnsJson,
        [Description("The schema for the table. Defaults to 'dbo'.")] string schema = "dbo")
    {
        try
        {
            var columns = JsonSerializer.Deserialize<List<ColumnDefinition>>(columnsJson, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (columns == null || columns.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    Success = false,
                    Error = "No columns defined. Please provide at least one column."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var columnDefs = new List<string>();
            var primaryKeys = new List<string>();

            foreach (var col in columns)
            {
                var colDef = $"[{col.Name}] {col.Type}";

                if (col.IsIdentity)
                {
                    colDef += " IDENTITY(1,1)";
                }

                if (!col.IsNullable)
                {
                    colDef += " NOT NULL";
                }
                else if (!col.IsIdentity && !col.IsPrimaryKey)
                {
                    colDef += " NULL";
                }

                if (!string.IsNullOrEmpty(col.DefaultValue))
                {
                    colDef += $" DEFAULT {col.DefaultValue}";
                }

                if (col.IsPrimaryKey)
                {
                    primaryKeys.Add($"[{col.Name}]");
                }

                columnDefs.Add(colDef);
            }

            var sql = $"CREATE TABLE [{schema}].[{tableName}] (\n    {string.Join(",\n    ", columnDefs)}";

            if (primaryKeys.Count > 0)
            {
                sql += $",\n    CONSTRAINT [PK_{tableName}] PRIMARY KEY ({string.Join(", ", primaryKeys)})";
            }

            sql += "\n)";

            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            return JsonSerializer.Serialize(new
            {
                Success = true,
                Message = $"Table '{schema}.{tableName}' created successfully.",
                SQL = sql
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new
            {
                Success = false,
                Error = $"Invalid JSON for columns: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (SqlException ex)
        {
            return JsonSerializer.Serialize(new
            {
                Success = false,
                Error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private class ColumnDefinition
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public bool IsNullable { get; set; } = true;
        public bool IsPrimaryKey { get; set; } = false;
        public bool IsIdentity { get; set; } = false;
        public string? DefaultValue { get; set; }
    }
}
