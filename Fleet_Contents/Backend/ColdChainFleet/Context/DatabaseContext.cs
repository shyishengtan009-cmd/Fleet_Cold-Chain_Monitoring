using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

namespace FleetCore.Context;

// Simplified for this demo to take a single connection string (what Neon/Supabase/Railway
// all hand you) instead of the original's separate Server/Database/UserId/Password fields,
// which only made sense against Azure's segmented config style.
public class DatabaseContext
{
    private readonly string _connectionString;

    public DatabaseContext(IConfiguration config)
    {
        _connectionString = config["Database:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Database:ConnectionString missing. Set it in appsettings.Development.json " +
                "(gitignored, local-only) or as an environment variable in production.");
    }

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
