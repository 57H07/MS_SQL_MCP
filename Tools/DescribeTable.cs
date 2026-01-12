using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace MS_SQL_MCP.Tools;

[McpServerToolType]
public class DescribeTable
{
    private readonly SqlConnectionConfig _config;

    public DescribeTable(SqlConnectionConfig config)
    {
        _config = config;
    }

    [McpServerTool, Description("Describes a table's structure including columns, data types, constraints, and keys.")]
    public async Task<string> DescribeTableStructure(
        [Description("The name of the table to describe.")] string tableName,
        [Description("The schema of the table. Defaults to 'dbo'.")] string schema = "dbo")
    {
        using var connection = new SqlConnection(_config.ConnectionString);
        await connection.OpenAsync();

        // Get column information
        var columnsQuery = @"
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.IS_NULLABLE,
                c.COLUMN_DEFAULT,
                COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_NAME = @TableName AND c.TABLE_SCHEMA = @Schema
            ORDER BY c.ORDINAL_POSITION";

        var columns = new List<object>();

        using (var command = new SqlCommand(columnsQuery, connection))
        {
            command.Parameters.AddWithValue("@TableName", tableName);
            command.Parameters.AddWithValue("@Schema", schema);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    MaxLength = reader.IsDBNull(2) ? null : (int?)reader.GetInt32(2),
                    Precision = reader.IsDBNull(3) ? null : (byte?)reader.GetByte(3),
                    Scale = reader.IsDBNull(4) ? null : (int?)reader.GetInt32(4),
                    IsNullable = reader.GetString(5) == "YES",
                    DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsIdentity = reader.GetInt32(7) == 1
                });
            }
        }

        if (columns.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                Success = false,
                Error = $"Table '{schema}.{tableName}' not found."
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        // Get primary key information
        var pkQuery = @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + QUOTENAME(CONSTRAINT_NAME)), 'IsPrimaryKey') = 1
            AND TABLE_NAME = @TableName AND TABLE_SCHEMA = @Schema
            ORDER BY ORDINAL_POSITION";

        var primaryKeys = new List<string>();

        using (var command = new SqlCommand(pkQuery, connection))
        {
            command.Parameters.AddWithValue("@TableName", tableName);
            command.Parameters.AddWithValue("@Schema", schema);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                primaryKeys.Add(reader.GetString(0));
            }
        }

        // Get foreign key information
        var fkQuery = @"
            SELECT 
                fk.name AS FK_Name,
                COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS Column_Name,
                OBJECT_SCHEMA_NAME(fkc.referenced_object_id) AS Referenced_Schema,
                OBJECT_NAME(fkc.referenced_object_id) AS Referenced_Table,
                COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS Referenced_Column
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            WHERE OBJECT_NAME(fk.parent_object_id) = @TableName 
            AND OBJECT_SCHEMA_NAME(fk.parent_object_id) = @Schema";

        var foreignKeys = new List<object>();

        using (var command = new SqlCommand(fkQuery, connection))
        {
            command.Parameters.AddWithValue("@TableName", tableName);
            command.Parameters.AddWithValue("@Schema", schema);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                foreignKeys.Add(new
                {
                    Name = reader.GetString(0),
                    Column = reader.GetString(1),
                    ReferencedSchema = reader.GetString(2),
                    ReferencedTable = reader.GetString(3),
                    ReferencedColumn = reader.GetString(4)
                });
            }
        }

        // Get indexes
        var indexQuery = @"
            SELECT 
                i.name AS IndexName,
                i.type_desc AS IndexType,
                i.is_unique AS IsUnique,
                STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE i.object_id = OBJECT_ID(@FullTableName) AND i.name IS NOT NULL
            GROUP BY i.name, i.type_desc, i.is_unique";

        var indexes = new List<object>();

        using (var command = new SqlCommand(indexQuery, connection))
        {
            command.Parameters.AddWithValue("@FullTableName", $"{schema}.{tableName}");

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                indexes.Add(new
                {
                    Name = reader.GetString(0),
                    Type = reader.GetString(1),
                    IsUnique = reader.GetBoolean(2),
                    Columns = reader.GetString(3)
                });
            }
        }

        return JsonSerializer.Serialize(new
        {
            Success = true,
            Table = $"{schema}.{tableName}",
            Columns = columns,
            PrimaryKey = primaryKeys,
            ForeignKeys = foreignKeys,
            Indexes = indexes
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
