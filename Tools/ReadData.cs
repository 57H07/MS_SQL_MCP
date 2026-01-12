using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace MS_SQL_MCP.Tools;

[McpServerToolType]
public class ReadData
{
    private readonly SqlConnectionConfig _config;

    public ReadData(SqlConnectionConfig config)
    {
        _config = config;
    }

    [McpServerTool, Description("Reads data from a table with optional filtering, sorting, and pagination.")]
    public async Task<string> ReadFromTable(
        [Description("The name of the table to read from.")] string tableName,
        [Description("Comma-separated list of columns to select. Use '*' for all columns.")] string columns = "*",
        [Description("Optional WHERE clause condition (without the WHERE keyword). Example: \"Age > 18 AND Status = 'Active'\"")] string? where = null,
        [Description("Optional ORDER BY clause (without the ORDER BY keyword). Example: \"Name ASC, CreatedDate DESC\"")] string? orderBy = null,
        [Description("Maximum number of rows to return. Defaults to 100.")] int top = 100,
        [Description("Number of rows to skip (for pagination). Defaults to 0.")] int offset = 0,
        [Description("The schema of the table. Defaults to 'dbo'.")] string schema = "dbo")
    {
        try
        {
            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            // Build the query
            var sql = $"SELECT";

            if (string.IsNullOrEmpty(orderBy) && offset == 0)
            {
                sql += $" TOP {top}";
            }

            sql += $" {columns} FROM [{schema}].[{tableName}]";

            if (!string.IsNullOrEmpty(where))
            {
                sql += $" WHERE {where}";
            }

            if (!string.IsNullOrEmpty(orderBy))
            {
                sql += $" ORDER BY {orderBy}";
                sql += $" OFFSET {offset} ROWS FETCH NEXT {top} ROWS ONLY";
            }
            else if (offset > 0)
            {
                // Need ORDER BY for OFFSET, use primary key or first column
                sql += " ORDER BY (SELECT NULL) OFFSET " + offset + " ROWS FETCH NEXT " + top + " ROWS ONLY";
            }

            using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 30;

            using var reader = await command.ExecuteReaderAsync();

            var rows = new List<Dictionary<string, object?>>();
            var columnNames = new List<string>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[columnNames[i]] = reader.IsDBNull(i) ? null : ConvertValue(reader.GetValue(i));
                }
                rows.Add(row);
            }

            // Get total count
            var countSql = $"SELECT COUNT(*) FROM [{schema}].[{tableName}]";
            if (!string.IsNullOrEmpty(where))
            {
                countSql += $" WHERE {where}";
            }

            using var countCommand = new SqlCommand(countSql, connection);
            var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

            return JsonSerializer.Serialize(new
            {
                Success = true,
                Table = $"{schema}.{tableName}",
                TotalCount = totalCount,
                ReturnedCount = rows.Count,
                Offset = offset,
                Columns = columnNames,
                Rows = rows
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

    [McpServerTool, Description("Executes a custom SQL SELECT query and returns the results.")]
    public async Task<string> ExecuteQuery(
        [Description("The SQL SELECT query to execute. Only SELECT statements are allowed.")] string query)
    {
        try
        {
            // Basic validation - only allow SELECT statements
            var trimmedQuery = query.Trim();
            if (!trimmedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Serialize(new
                {
                    Success = false,
                    Error = "Only SELECT queries are allowed. Use other tools for INSERT, UPDATE, or DELETE operations."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 30;

            using var reader = await command.ExecuteReaderAsync();

            var rows = new List<Dictionary<string, object?>>();
            var columnNames = new List<string>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[columnNames[i]] = reader.IsDBNull(i) ? null : ConvertValue(reader.GetValue(i));
                }
                rows.Add(row);
            }

            return JsonSerializer.Serialize(new
            {
                Success = true,
                RowCount = rows.Count,
                Columns = columnNames,
                Rows = rows
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

    private static object ConvertValue(object value)
    {
        return value switch
        {
            DateTime dt => dt.ToString("O"),
            DateTimeOffset dto => dto.ToString("O"),
            byte[] bytes => Convert.ToBase64String(bytes),
            Guid guid => guid.ToString(),
            _ => value
        };
    }
}
