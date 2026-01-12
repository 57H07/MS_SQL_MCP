using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace MS_SQL_MCP.Tools;

[McpServerToolType]
public class DropTable
{
    private readonly SqlConnectionConfig _config;

    public DropTable(SqlConnectionConfig config)
    {
        _config = config;
    }

    [McpServerTool, Description("Drops (deletes) a table from the database. This action is irreversible.")]
    public async Task<string> DropTableFromDatabase(
        [Description("The name of the table to drop.")] string tableName,
        [Description("The schema of the table. Defaults to 'dbo'.")] string schema = "dbo",
        [Description("If true, drops the table even if it has foreign key references (cascades). Defaults to false.")] bool force = false)
    {
        try
        {
            using var connection = new SqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

            // Check if table exists
            var checkQuery = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = @Schema";

            using (var checkCommand = new SqlCommand(checkQuery, connection))
            {
                checkCommand.Parameters.AddWithValue("@TableName", tableName);
                checkCommand.Parameters.AddWithValue("@Schema", schema);
                var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;

                if (!exists)
                {
                    return JsonSerializer.Serialize(new
                    {
                        Success = false,
                        Error = $"Table '{schema}.{tableName}' does not exist."
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
            }

            // If force is true, drop foreign key constraints first
            if (force)
            {
                var dropFkQuery = @"
                    DECLARE @sql NVARCHAR(MAX) = '';
                    SELECT @sql += 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + 
                                   QUOTENAME(OBJECT_NAME(parent_object_id)) + 
                                   ' DROP CONSTRAINT ' + QUOTENAME(name) + ';'
                    FROM sys.foreign_keys
                    WHERE referenced_object_id = OBJECT_ID(@FullTableName);
                    EXEC sp_executesql @sql;";

                using var dropFkCommand = new SqlCommand(dropFkQuery, connection);
                dropFkCommand.Parameters.AddWithValue("@FullTableName", $"{schema}.{tableName}");
                await dropFkCommand.ExecuteNonQueryAsync();
            }

            // Drop the table
            var dropQuery = $"DROP TABLE [{schema}].[{tableName}]";

            using var dropCommand = new SqlCommand(dropQuery, connection);
            await dropCommand.ExecuteNonQueryAsync();

            return JsonSerializer.Serialize(new
            {
                Success = true,
                Message = $"Table '{schema}.{tableName}' has been dropped successfully."
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (SqlException ex)
        {
            return JsonSerializer.Serialize(new
            {
                Success = false,
                Error = ex.Message,
                Hint = ex.Message.Contains("FOREIGN KEY") 
                    ? "This table is referenced by foreign keys. Use force=true to drop it anyway." 
                    : null
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
