using System.Text.Json;
using Npgsql;
using MaintainPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Api.Development;

public static class DatabaseInspector
{
    public static async Task<int> RunAsync(IConfiguration configuration)
    {
        try
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Required configuration is unavailable.");
            var settings = new NpgsqlConnectionStringBuilder(connectionString);
            if (settings.Database != "maintainpro_db")
            {
                Console.WriteLine("Inspection stopped: DefaultConnection must target maintainpro_db.");
                return 2;
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            var tables = new List<(string Schema, string Name)>();
            await using (var query = new NpgsqlCommand(
                "SELECT schemaname, tablename FROM pg_catalog.pg_tables " +
                "WHERE schemaname NOT IN ('pg_catalog', 'information_schema') " +
                "AND schemaname NOT LIKE 'pg_toast%' ORDER BY schemaname, tablename", connection))
            await using (var reader = await query.ExecuteReaderAsync())
                while (await reader.ReadAsync()) tables.Add((reader.GetString(0), reader.GetString(1)));

            var inventory = new List<object>();
            var migrations = new List<string>();
            using var quote = new NpgsqlCommandBuilder();
            foreach (var (schema, name) in tables)
            {
                var qualified = $"{quote.QuoteIdentifier(schema)}.{quote.QuoteIdentifier(name)}";
                await using var count = new NpgsqlCommand($"SELECT COUNT(*) FROM {qualified}", connection);
                var rowCount = (long)(await count.ExecuteScalarAsync())!;
                inventory.Add(new { Schema = schema, Table = name, HasRows = rowCount > 0, RowCount = rowCount });
                if (schema == "public" && name == "__EFMigrationsHistory")
                {
                    await using var history = new NpgsqlCommand($"SELECT \"MigrationId\" FROM {qualified} ORDER BY \"MigrationId\"", connection);
                    await using var reader = await history.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) migrations.Add(reader.GetString(0));
                }
            }

            var roles = new List<string>();
            if (tables.Contains(("public", "Roles")))
            {
                await using var roleQuery = new NpgsqlCommand("SELECT \"Name\" FROM public.\"Roles\" ORDER BY \"Name\"", connection);
                await using var reader = await roleQuery.ExecuteReaderAsync();
                while (await reader.ReadAsync()) roles.Add(reader.GetString(0));
            }
            await using var foreignKeys = new NpgsqlCommand(
                "SELECT count(*), count(*) FILTER (WHERE confdeltype <> 'r') FROM pg_catalog.pg_constraint " +
                "WHERE contype = 'f' AND connamespace = 'public'::regnamespace", connection);
            long foreignKeyCount, nonRestrictForeignKeyCount;
            await using (var reader = await foreignKeys.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                foreignKeyCount = reader.GetInt64(0);
                nonRestrictForeignKeyCount = reader.GetInt64(1);
            }
            await using var indexes = new NpgsqlCommand(
                "SELECT count(*) FROM pg_catalog.pg_index i JOIN pg_catalog.pg_class t ON t.oid = i.indrelid " +
                "WHERE i.indisunique AND NOT i.indisprimary AND t.relnamespace = 'public'::regnamespace", connection);
            var uniqueIndexCount = (long)(await indexes.ExecuteScalarAsync())!;
            var actualIndexes = new Dictionary<string, bool>(StringComparer.Ordinal);
            await using (var indexQuery = new NpgsqlCommand(
                "SELECT indexname, indexdef LIKE 'CREATE UNIQUE INDEX%' FROM pg_catalog.pg_indexes WHERE schemaname = 'public'", connection))
            await using (var reader = await indexQuery.ExecuteReaderAsync())
                while (await reader.ReadAsync()) actualIndexes.Add(reader.GetString(0), reader.GetBoolean(1));
            var actualForeignKeys = new Dictionary<string, bool>(StringComparer.Ordinal);
            await using (var keyQuery = new NpgsqlCommand(
                "SELECT conname, confdeltype = 'r' AND convalidated FROM pg_catalog.pg_constraint " +
                "WHERE contype = 'f' AND connamespace = 'public'::regnamespace", connection))
            await using (var reader = await keyQuery.ExecuteReaderAsync())
                while (await reader.ReadAsync()) actualForeignKeys.Add(reader.GetString(0), reader.GetBoolean(1));
            using var modelContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);
            var schemaDifferences = new List<string>();
            foreach (var entity in modelContext.Model.GetEntityTypes())
            {
                if (!tables.Contains(("public", entity.GetTableName()!)))
                    schemaDifferences.Add($"Missing table {entity.GetTableName()}");
                foreach (var index in entity.GetIndexes())
                    if (!actualIndexes.TryGetValue(index.GetDatabaseName()!, out var unique) || unique != index.IsUnique)
                        schemaDifferences.Add($"Missing or incompatible index {index.GetDatabaseName()}");
                foreach (var key in entity.GetForeignKeys())
                    if (!actualForeignKeys.TryGetValue(key.GetConstraintName()!, out var restrict) || !restrict)
                        schemaDifferences.Add($"Missing, unvalidated or non-Restrict foreign key {key.GetConstraintName()}");
            }

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Database = "maintainpro_db", Tables = inventory, AppliedMigrations = migrations,
                RoleDefinitions = roles, ForeignKeyCount = foreignKeyCount,
                NonRestrictForeignKeyCount = nonRestrictForeignKeyCount, UniqueIndexCount = uniqueIndexCount,
                ModelTablesIndexesAndForeignKeysMatch = schemaDifferences.Count == 0,
                SchemaDifferences = schemaDifferences,
                JwtSigningKeyConfigured = !string.IsNullOrWhiteSpace(configuration["Jwt:SigningKey"]),
                BootstrapConfigured = new[] { "EmployeeId", "FirstName", "LastName", "Email", "Password" }
                    .All(key => !string.IsNullOrWhiteSpace(configuration[$"BootstrapAdmin:{key}"]))
            }));
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Development database inspection failed ({exception.GetType().Name}). No database changes were made.");
            return 2;
        }
    }
}
