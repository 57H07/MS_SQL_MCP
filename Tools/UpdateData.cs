using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace MS_SQL_MCP.Tools;

[McpServerToolType]
public class UpdateData
{
    private readonly SqlConnectionConfig _config;

    public UpdateData(SqlConnectionConfig config)
    {
        _config = config;
    }

    [McpServerTool, Description("Updates existing rows in a table based on a WHERE condition.")]
    public async Task<string> UpdateRows(
        [Description("The name of the table to update.")] string tableName,
        [Description("JSON object containing column-value pairs to update. Example: {\"Name\":\"John Updated\",\"Age\":31}")] string setValuesJson,
        [Description("WHERE clause condition (without the WHERE keyword) to identify rows to update. Example: \"Id = 1\" or \"Status = 'Pending'\". REQUIRED to prevent accidental full table updates.")] string where,
        [Description("The schema of the table. Defaults to 'dbo'.")] string schema = "dbo")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(where))
            {
                return JsonSerializer.Serialize(new
                {
                    Success = false,
                    Error = "WHERE clause is required to prevent accidental updates to all rows. If you want to update all rows, use a condition like '1=1'."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var setValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(setValuesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (setValues == null || setValues.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    Success = false,
                    Error = "No values provided to update. Please provide at least one column-value pair."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            var setClauses = new List<string>();
            var command = new SqlCommand { Connection = connection };

            int paramIndex = 0;
            foreach (var kvp in setValues)
            {
                var paramName = $"@p{paramIndex}";
                setClauses.Add($"[{kvp.Key}] = {paramName}");
                command.Parameters.AddWithValue(paramName, ConvertJsonElement(kvp.Value) ?? DBNull.Value);
                paramIndex++;
            }

            var sql = $@"
                UPDATE [{schema}].[{tableName}]
                SET {string.Join(", ", setClauses)}
                WHERE {where}";

            command.CommandText = sql;

            int rowsAffected = await command.ExecuteNonQueryAsync();

            return JsonSerializer.Serialize(new
            {
                Success = true,
                Message = $"Successfully updated {rowsAffected} row(s) in '{schema}.{tableName}'.",
                RowsAffected = rowsAffected,
                SQL = sql
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new
            {
                Success = false,
                Error = $"Invalid JSON data: {ex.Message}"
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

    [McpServerTool, Description("Deletes rows from a table based on a WHERE condition.")]
    public async Task<string> DeleteRows(
        [Description("The name of the table to delete from.")] string tableName,
        [Description("WHERE clause condition (without the WHERE keyword) to identify rows to delete. Example: \"Id = 1\" or \"Status = 'Deleted'\". REQUIRED to prevent accidental deletion of all rows.")] string where,
        [Description("The schema of the table. Defaults to 'dbo'.")] string schema = "dbo")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(where))
            {
                return JsonSerializer.Serialize(new
                {
                    Success = false,
                    Error = "WHERE clause is required to prevent accidental deletion of all rows. If you want to delete all rows, use a condition like '1=1' or use TRUNCATE."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            // First, count affected rows
            var countSql = $"SELECT COUNT(*) FROM [{schema}].[{tableName}] WHERE {where}";
            using var countCommand = new SqlCommand(countSql, connection);
            int countToDelete = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

            if (countToDelete == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    Success = true,
                    Message = "No rows matched the WHERE condition. Nothing was deleted.",
                    RowsAffected = 0
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var sql = $"DELETE FROM [{schema}].[{tableName}] WHERE {where}";
            using var command = new SqlCommand(sql, connection);
            int rowsAffected = await command.ExecuteNonQueryAsync();

            return JsonSerializer.Serialize(new
            {
                Success = true,
                Message = $"Successfully deleted {rowsAffected} row(s) from '{schema}.{tableName}'.",
                RowsAffected = rowsAffected
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

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
