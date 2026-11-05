using System.Globalization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «DavomatQamrovi» is additive: it applies on top of the previous schema and rolls back cleanly.
/// </summary>
/// <remarks>
/// Each test owns a temporary <c>bonvi_migr_xxxxx</c> database, the rule of
/// <see cref="CandidateFileMigrationTests"/>. The development database is never opened.
/// </remarks>
public sealed class DavomatQamroviMigrationTests
{
    private const string Previous = "20261103111355_Blankalar";

    /// <summary>Named, not «latest»: a later migration adding a table mustn't move this count.</summary>
    private const string ThisMigration = "20261105045537_DavomatQamrovi";

    [Fact]
    public async Task Migration_adds_the_table_with_its_keys_and_rolls_back_without_touching_anything_else()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var database = await TemporaryDatabase.CreateAsync(ct);
        await MigrateAsync(database.ConnectionString, Previous, ct);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);

        (await ScalarAsync(connection, "SELECT to_regclass('user_attendance_scopes') IS NULL;", ct)).ShouldBe(true);
        var tablesBefore = await ScalarAsync(connection, TableCount, ct);

        await MigrateAsync(database.ConnectionString, ThisMigration, ct);

        (await ScalarAsync(connection, "SELECT to_regclass('user_attendance_scopes') IS NULL;", ct)).ShouldBe(false);
        (await ScalarAsync(connection, "SELECT count(*) FROM user_attendance_scopes;", ct)).ShouldBe(0L);
        (await ScalarAsync(
            connection,
            """
            SELECT string_agg(conname || ':' || confdeltype::text, ',' ORDER BY conname)
            FROM pg_constraint
            WHERE conrelid = 'user_attendance_scopes'::regclass AND contype = 'f';
            """,
            ct))
            .ShouldBe("fk_user_attendance_scopes_departments_department_id:r,fk_user_attendance_scopes_users_user_id:c");

        (await ScalarAsync(connection, TableCount, ct)).ShouldBe((long)tablesBefore! + 1);

        await MigrateAsync(database.ConnectionString, Previous, ct);

        (await ScalarAsync(connection, "SELECT to_regclass('user_attendance_scopes') IS NULL;", ct)).ShouldBe(true);
        (await ScalarAsync(connection, TableCount, ct)).ShouldBe(tablesBefore);
    }

    private const string TableCount = "SELECT count(*) FROM pg_tables WHERE schemaname = 'public';";

    private static async Task MigrateAsync(string connectionString, string? target, CancellationToken ct)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new AppDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync(target, ct);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        return await command.ExecuteScalarAsync(ct);
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private const string Prefix = "bonvi_migr_";

        private static readonly string AdminConnection =
            Environment.GetEnvironmentVariable("TEST_POSTGRES")
            ?? "Host=postgres;Port=5432;Database=postgres;Username=bonvi;Password=bonvi_dev_parol";

        private readonly string _name;

        private TemporaryDatabase(string name, string connectionString)
        {
            _name = name;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<TemporaryDatabase> CreateAsync(CancellationToken ct)
        {
            var name = Prefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];

            await using (var admin = new NpgsqlConnection(AdminConnection))
            {
                await admin.OpenAsync(ct);

                await using var create = admin.CreateCommand();
                create.CommandText = $"""CREATE DATABASE "{name}";""";
                _ = await create.ExecuteNonQueryAsync(ct);
            }

            var builder = new NpgsqlConnectionStringBuilder(AdminConnection) { Database = name };

            return new TemporaryDatabase(name, builder.ConnectionString);
        }

        public async ValueTask DisposeAsync()
        {
            // Safety net: the DROP can only ever hit a name this test created.
            if (!_name.StartsWith(Prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"«{_name}» was not created by this test and is not dropped.");
            }

            await using var open = new NpgsqlConnection(ConnectionString);
            NpgsqlConnection.ClearPool(open);

            await using var admin = new NpgsqlConnection(AdminConnection);
            await admin.OpenAsync();

            await using var drop = admin.CreateCommand();
            drop.CommandText = $"""DROP DATABASE IF EXISTS "{_name}" WITH (FORCE);""";
            _ = await drop.ExecuteNonQueryAsync();
        }
    }
}
