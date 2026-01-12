using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace MS_SQL_MCP.Tools;

[McpServerToolType]
public class InsertData
{
    private readonly SqlConnectionConfig _config;

    public InsertData(SqlConnectionConfig config)
    {
        _config = config;
    }

    [McpServerTool, Description("Inserts one or more rows of data into a table.")]
    public async Task<string> InsertRows(
        [Description("The name of the table to insert data into.")] string tableName,
        [Description("JSON array of objects to insert. Each object should have column names as keys. Example: [{\"Name\":\"John\",\"Age\":30},{\"Name\":\"Jane\",\"Age\":25}]")] string dataJson,
        [Description("The schema of the table. Defaults to 'dbo'.")] string schema = "dbo")
    {
        try
        {
            var rows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(dataJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (rows == null || rows.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    Success = false,
                    Error = "No data provided. Please provide at least one row to insert."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            int totalInserted = 0;
            var insertedIds = new List<object>();

            foreach (var row in rows)
            {
                var columns = row.Keys.ToList();
                var paramNames = columns.Select((_, i) => $"@p{i}").ToList();

                var sql = $@"
                    INSERT INTO [{schema}].[{tableName}] ([{string.Join("], [", columns)}])
                    OUTPUT INSERTED.*
                    VALUES ({string.Join(", ", paramNames)})";

                using var command = new SqlCommand(sql, connection);

                for (int i = 0; i < columns.Count; i++)
                {
                    var value = ConvertJsonElement(row[columns[i]]);
                    command.Parameters.AddWithValue($"@p{i}", value ?? DBNull.Value);
                }

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var insertedRow = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        insertedRow[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    insertedIds.Add(insertedRow);
                    totalInserted++;
                }
            }

            return JsonSerializer.Serialize(new
            {
                Success = true,
                Message = $"Successfully inserted {totalInserted} row(s) into '{schema}.{tableName}'.",
                InsertedRows = insertedIds
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
