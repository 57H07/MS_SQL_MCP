# MS SQL MCP Server

A Model Context Protocol (MCP) server for SQL Server database operations built with .NET 8.

## Features

This MCP server provides the following tools for SQL Server database access:

| Tool | Description |
|------|-------------|
| **ListAllTables** | Lists all tables in the database with optional schema filtering |
| **DescribeTableStructure** | Describes a table's structure including columns, data types, keys, and indexes |
| **CreateNewTable** | Creates a new table with specified columns and constraints |
| **DropTableFromDatabase** | Drops a table from the database (with optional force mode) |
| **InsertRows** | Inserts one or more rows into a table |
| **ReadFromTable** | Reads data with filtering, sorting, and pagination |
| **ExecuteQuery** | Executes custom SELECT queries |
| **UpdateRows** | Updates existing rows based on a WHERE condition |
| **DeleteRows** | Deletes rows based on a WHERE condition |
| **GetServerInfo** | Gets database server information and properties |
| **ListDatabases** | Lists all databases on the server |
| **ListStoredProcedures** | Lists all stored procedures in the database |
| **ExecuteStoredProcedure** | Executes a stored procedure with parameters |
| **TestConnection** | Tests the database connection |

## Prerequisites

- .NET 8.0 SDK
- SQL Server (any edition: LocalDB, Express, Standard, Enterprise, Azure SQL)

## Building

```bash
dotnet restore
dotnet build
```

## Usage

Run the MCP server with your SQL Server connection string as the first argument:

```bash
dotnet run -- "Server=localhost;Database=MyDatabase;Trusted_Connection=True;TrustServerCertificate=True;"
```

Or after publishing:

```bash
MS_SQL_MCP.exe "Server=localhost;Database=MyDatabase;Trusted_Connection=True;TrustServerCertificate=True;"
```

### Connection String Examples

**Windows Authentication:**
```
Server=localhost;Database=MyDatabase;Trusted_Connection=True;TrustServerCertificate=True;
```

**SQL Server Authentication:**
```
Server=localhost;Database=MyDatabase;User Id=sa;Password=YourPassword;TrustServerCertificate=True;
```

**Azure SQL:**
```
Server=yourserver.database.windows.net;Database=MyDatabase;User Id=user;Password=password;Encrypt=True;
```

**LocalDB:**
```
Server=(localdb)\MSSQLLocalDB;Database=MyDatabase;Integrated Security=True;
```

## MCP Client Configuration

### Claude Desktop (claude_desktop_config.json)

```json
{
  "mcpServers": {
    "mssql": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\EU3734\\source\\repos\\MS_SQL_MCP",
        "--",
        "Server=localhost;Database=MyDatabase;Trusted_Connection=True;TrustServerCertificate=True;"
      ]
    }
  }
}
```

Or with a published executable:

```json
{
  "mcpServers": {
    "mssql": {
      "command": "D:\\EU3734\\source\\repos\\MS_SQL_MCP\\bin\\Release\\net8.0\\MS_SQL_MCP.exe",
      "args": [
        "Server=localhost;Database=MyDatabase;Trusted_Connection=True;TrustServerCertificate=True;"
      ]
    }
  }
}
```

## Tool Examples

### Create a Table

```json
{
  "tableName": "Users",
  "columnsJson": "[{\"name\":\"Id\",\"type\":\"INT\",\"isPrimaryKey\":true,\"isIdentity\":true},{\"name\":\"Username\",\"type\":\"NVARCHAR(50)\",\"isNullable\":false},{\"name\":\"Email\",\"type\":\"NVARCHAR(100)\"},{\"name\":\"CreatedAt\",\"type\":\"DATETIME2\",\"defaultValue\":\"GETUTCDATE()\"}]",
  "schema": "dbo"
}
```

### Insert Data

```json
{
  "tableName": "Users",
  "dataJson": "[{\"Username\":\"john_doe\",\"Email\":\"john@example.com\"},{\"Username\":\"jane_doe\",\"Email\":\"jane@example.com\"}]"
}
```

### Read Data with Filtering

```json
{
  "tableName": "Users",
  "columns": "Id, Username, Email",
  "where": "Username LIKE 'john%'",
  "orderBy": "CreatedAt DESC",
  "top": 10
}
```

### Update Data

```json
{
  "tableName": "Users",
  "setValuesJson": "{\"Email\":\"newemail@example.com\"}",
  "where": "Id = 1"
}
```

## License

MIT
