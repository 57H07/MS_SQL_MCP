using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace MS_SQL_MCP.Tools;

[McpServerToolType]
public class Tools
{
    private readonly SqlConnectionConfig _config;

    public Tools(SqlConnectionConfig config)
    {
        _config = config;
    }

    [McpServerTool, Description("Gets information about the connected database server including version, database name, and server properties.")]
    public async Task<string> GetServerInfo()
    {
        try
        {
            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            var info = new Dictionary<string, object?>
            {
                ["ServerVersion"] = connection.ServerVersion,
                ["Database"] = connection.Database,
                ["DataSource"] = connection.DataSource,
                ["WorkstationId"] = connection.WorkstationId
            };

            // Get additional server properties
            var propertiesQuery = @"
                SELECT 
                    SERVERPROPERTY('ProductVersion') AS ProductVersion,
                    SERVERPROPERTY('ProductLevel') AS ProductLevel,
                    SERVERPROPERTY('Edition') AS Edition,
                    SERVERPROPERTY('EngineEdition') AS EngineEdition,
                    SERVERPROPERTY('MachineName') AS MachineName,
                    SERVERPROPERTY('ServerName') AS ServerName,
                    SERVERPROPERTY('Collation') AS Collation,
                    DB_NAME() AS CurrentDatabase";

            using var command = new SqlCommand(propertiesQuery, connection);
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                info["ProductVersion"] = reader.IsDBNull(0) ? null : reader.GetValue(0).ToString();
                info["ProductLevel"] = reader.IsDBNull(1) ? null : reader.GetValue(1).ToString();
                info["Edition"] = reader.IsDBNull(2) ? null : reader.GetValue(2).ToString();
                info["EngineEdition"] = reader.IsDBNull(3) ? null : reader.GetValue(3);
                info["MachineName"] = reader.IsDBNull(4) ? null : reader.GetValue(4).ToString();
                info["ServerName"] = reader.IsDBNull(5) ? null : reader.GetValue(5).ToString();
                info["Collation"] = reader.IsDBNull(6) ? null : reader.GetValue(6).ToString();
                info["CurrentDatabase"] = reader.IsDBNull(7) ? null : reader.GetValue(7).ToString();
            }

            return JsonSerializer.Serialize(new
            {
                Success = true,
                ServerInfo = info
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

    [McpServerTool, Description("Lists all databases on the server.")]
    public async Task<string> ListDatabases()
    {
        try
        {
            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    name,
                    database_id,
                    create_date,
                    state_desc,
                    recovery_model_desc
                FROM sys.databases
                ORDER BY name";

            var databases = new List<object>();

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                databases.Add(new
                {
                    Name = reader.GetString(0),
                    DatabaseId = reader.GetInt32(1),
                    CreatedDate = reader.GetDateTime(2).ToString("O"),
                    State = reader.GetString(3),
                    RecoveryModel = reader.GetString(4)
                });
            }

            return JsonSerializer.Serialize(new
            {
                Success = true,
                Count = databases.Count,
                Databases = databases
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

    [McpServerTool, Description("Lists all stored procedures in the database.")]
    public async Task<string> ListStoredProcedures(
        [Description("Optional schema name to filter procedures. Leave empty for all schemas.")] string? schema = null)
    {
        try
        {
            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    ROUTINE_SCHEMA,
                    ROUTINE_NAME,
                    CREATED,
                    LAST_ALTERED
                FROM INFORMATION_SCHEMA.ROUTINES
                WHERE ROUTINE_TYPE = 'PROCEDURE'";

            if (!string.IsNullOrEmpty(schema))
            {
                query += " AND ROUTINE_SCHEMA = @Schema";
            }

            query += " ORDER BY ROUTINE_SCHEMA, ROUTINE_NAME";

            var procedures = new List<object>();

            using var command = new SqlCommand(query, connection);
            if (!string.IsNullOrEmpty(schema))
            {
                command.Parameters.AddWithValue("@Schema", schema);
            }

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                procedures.Add(new
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1),
                    Created = reader.GetDateTime(2).ToString("O"),
                    LastAltered = reader.GetDateTime(3).ToString("O")
                });
            }

            return JsonSerializer.Serialize(new
            {
                Success = true,
                Count = procedures.Count,
                StoredProcedures = procedures
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

    [McpServerTool, Description("Executes a stored procedure with optional parameters.")]
    public async Task<string> ExecuteStoredProcedure(
        [Description("The name of the stored procedure to execute.")] string procedureName,
        [Description("JSON object containing parameter names and values. Example: {\"@param1\":\"value1\",\"@param2\":123}")] string? parametersJson = null,
        [Description("The schema of the stored procedure. Defaults to 'dbo'.")] string schema = "dbo")
    {
        try
        {
            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand($"[{schema}].[{procedureName}]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            if (!string.IsNullOrEmpty(parametersJson))
            {
                var parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(parametersJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parameters != null)
                {
                    foreach (var kvp in parameters)
                    {
                        var paramName = kvp.Key.StartsWith("@") ? kvp.Key : $"@{kvp.Key}";
                        command.Parameters.AddWithValue(paramName, ConvertJsonElement(kvp.Value) ?? DBNull.Value);
                    }
                }
            }

            using var reader = await command.ExecuteReaderAsync();

            var resultSets = new List<List<Dictionary<string, object?>>>();

            do
            {
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
                        row[columnNames[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    rows.Add(row);
                }

                if (rows.Count > 0 || columnNames.Count > 0)
                {
                    resultSets.Add(rows);
                }
            } while (await reader.NextResultAsync());

            return JsonSerializer.Serialize(new
            {
                Success = true,
                Message = $"Stored procedure '{schema}.{procedureName}' executed successfully.",
                ResultSetCount = resultSets.Count,
                ResultSets = resultSets
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

    [McpServerTool, Description("Tests the database connection.")]
    public async Task<string> TestConnection()
    {
        try
        {
            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            return JsonSerializer.Serialize(new
            {
                Success = true,
                Message = "Connection successful!",
                Database = connection.Database,
                Server = connection.DataSource,
                ServerVersion = connection.ServerVersion
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (SqlException ex)
        {
            return JsonSerializer.Serialize(new
            {
                Success = false,
                Error = ex.Message,
                ErrorNumber = ex.Number
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
