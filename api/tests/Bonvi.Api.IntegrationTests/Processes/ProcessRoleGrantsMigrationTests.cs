using System.Globalization;
using Bonvi.Domain.Identity;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// <c>JarayonRuxsatlariRollarga</c>: the seeder does not rewrite existing roles, so this migration alone decides whether
/// HR can open «Mening ishlarim» on a running database. A fresh database has no such rows, hence a temporary one
/// (the method of <see cref="EmployeePasswordHandedOutMigrationTests"/>).
/// </summary>
public sealed class ProcessRoleGrantsMigrationTests
{
    private const string Previous = "20261104124759_Processes";
    private const string Target = "20261105100333_JarayonRuxsatlariRollarga";
    private const string Custom = "kadr_bolimi";

    [Fact]
    public async Task Each_role_gets_its_section_9_2_grants_after_what_it_already_holds()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await using var database = await TemporaryDatabase.CreateAsync(cancellation);
        await MigrateAsync(database.ConnectionString, Previous, cancellation);

        await using var connection = await OpenAsync(database, cancellation);
        await AddRoleAsync(connection, SystemRoles.HrManager, [Permissions.EmployeesView], cancellation);
        await AddRoleAsync(connection, SystemRoles.MainBoss, [Permissions.EmployeesView, Permissions.SalaryView], cancellation);

        await MigrateAsync(database.ConnectionString, Target, cancellation);

        (await PermissionsOfAsync(connection, SystemRoles.HrManager, cancellation)).ShouldBe(
        [
            Permissions.EmployeesView,
            Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage,
        ]);

        (await PermissionsOfAsync(connection, SystemRoles.MainBoss, cancellation)).ShouldBe(
        [
            Permissions.EmployeesView, Permissions.SalaryView,
            Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage, Permissions.ProcessesOverride,
        ]);
    }

    [Fact]
    public async Task A_code_given_by_hand_is_not_repeated_and_no_other_role_is_touched()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await using var database = await TemporaryDatabase.CreateAsync(cancellation);
        await MigrateAsync(database.ConnectionString, Previous, cancellation);

        await using var connection = await OpenAsync(database, cancellation);
        await AddRoleAsync(connection, SystemRoles.HrManager, [Permissions.ProcessesWork, Permissions.EmployeesView], cancellation);
        await AddRoleAsync(connection, SystemRoles.SuperAdmin, [Permissions.EmployeesView], cancellation);
        await AddRoleAsync(connection, Custom, [Permissions.ProcessesView], cancellation);

        var before = await SnapshotAsync(connection, cancellation);

        await MigrateAsync(database.ConnectionString, Target, cancellation);

        (await PermissionsOfAsync(connection, SystemRoles.HrManager, cancellation)).ShouldBe(
        [
            Permissions.ProcessesWork, Permissions.EmployeesView,
            Permissions.ProcessesView, Permissions.ProcessesManage,
        ]);

        var after = await SnapshotAsync(connection, cancellation);
        after[SystemRoles.SuperAdmin].ShouldBe(before[SystemRoles.SuperAdmin], "the seeder keeps super_admin, not this migration");
        after[Custom].ShouldBe(before[Custom]);
    }

    [Fact]
    public async Task Running_it_again_changes_nothing()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await using var database = await TemporaryDatabase.CreateAsync(cancellation);
        await MigrateAsync(database.ConnectionString, Previous, cancellation);

        await using var connection = await OpenAsync(database, cancellation);
        await AddRoleAsync(connection, SystemRoles.HrManager, [Permissions.EmployeesView], cancellation);
        await AddRoleAsync(connection, SystemRoles.MainBoss, [], cancellation);
        await AddRoleAsync(connection, Custom, [Permissions.ProcessesView], cancellation);

        await MigrateAsync(database.ConnectionString, Target, cancellation);
        var first = await SnapshotAsync(connection, cancellation);

        // Forgetting the history row makes EF execute the same Up again; migrating down and up would prove less.
        await ExecuteAsync(connection, $"""DELETE FROM "__EFMigrationsHistory" WHERE migration_id = '{Target}';""", cancellation);
        await MigrateAsync(database.ConnectionString, Target, cancellation);

        (await ScalarAsync(connection, $"""SELECT count(*) FROM "__EFMigrationsHistory" WHERE migration_id = '{Target}';""", cancellation))
            .ShouldBe(1L, "the second run has to have really happened");

        var second = await SnapshotAsync(connection, cancellation);
        second.Keys.ShouldBe(first.Keys, ignoreOrder: true);
        foreach (var (code, row) in first)
        {
            second[code].ShouldBe(row, "xmin is included: the row was not even rewritten");
        }
    }

    [Fact]
    public async Task A_role_that_already_holds_everything_is_not_rewritten()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await using var database = await TemporaryDatabase.CreateAsync(cancellation);
        await MigrateAsync(database.ConnectionString, Previous, cancellation);

        await using var connection = await OpenAsync(database, cancellation);
        await AddRoleAsync(
            connection,
            SystemRoles.MainBoss,
            [Permissions.ProcessesOverride, Permissions.ProcessesManage, Permissions.ProcessesWork, Permissions.ProcessesView],
            cancellation);

        var before = await SnapshotAsync(connection, cancellation);

        await MigrateAsync(database.ConnectionString, Target, cancellation);

        (await SnapshotAsync(connection, cancellation))[SystemRoles.MainBoss].ShouldBe(before[SystemRoles.MainBoss]);
    }

    [Fact]
    public async Task Down_takes_back_only_what_up_could_have_given()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await using var database = await TemporaryDatabase.CreateAsync(cancellation);
        await MigrateAsync(database.ConnectionString, Previous, cancellation);

        await using var connection = await OpenAsync(database, cancellation);
        await AddRoleAsync(connection, SystemRoles.HrManager, [Permissions.EmployeesView, Permissions.ProcessesOverride], cancellation);
        await AddRoleAsync(connection, SystemRoles.MainBoss, [Permissions.SalaryView], cancellation);
        await AddRoleAsync(connection, Custom, [Permissions.ProcessesView, Permissions.ProcessesOverride], cancellation);

        var before = await SnapshotAsync(connection, cancellation);

        await MigrateAsync(database.ConnectionString, Target, cancellation);
        await MigrateAsync(database.ConnectionString, Previous, cancellation);

        (await PermissionsOfAsync(connection, SystemRoles.HrManager, cancellation))
            .ShouldBe([Permissions.EmployeesView, Permissions.ProcessesOverride], "Up never gave hr_manager the override");
        (await PermissionsOfAsync(connection, SystemRoles.MainBoss, cancellation)).ShouldBe([Permissions.SalaryView]);
        (await SnapshotAsync(connection, cancellation))[Custom].ShouldBe(before[Custom]);
    }

    private static async Task<NpgsqlConnection> OpenAsync(TemporaryDatabase database, CancellationToken cancellation)
    {
        var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellation);
        return connection;
    }

    private static async Task AddRoleAsync(NpgsqlConnection connection, string code, string[] permissions, CancellationToken cancellation)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO roles (id, code, name, is_system, permissions, created_at)
            VALUES ($1, $2, $2, true, $3, now());
            """;
        _ = command.Parameters.Add(new NpgsqlParameter { Value = Guid.CreateVersion7() });
        _ = command.Parameters.Add(new NpgsqlParameter { Value = code });
        _ = command.Parameters.Add(new NpgsqlParameter { Value = permissions });

        _ = await command.ExecuteNonQueryAsync(cancellation);
    }

    private static async Task<string[]> PermissionsOfAsync(NpgsqlConnection connection, string code, CancellationToken cancellation)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT permissions FROM roles WHERE code = $1;";
        _ = command.Parameters.Add(new NpgsqlParameter { Value = code });

        return (string[])(await command.ExecuteScalarAsync(cancellation))!;
    }

    /// <summary>Each role as text, <c>xmin</c> and <c>updated_at</c> included: a rewritten row differs even with equal permissions.</summary>
    private static async Task<Dictionary<string, string>> SnapshotAsync(NpgsqlConnection connection, CancellationToken cancellation)
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT code, concat_ws(' | ', permissions::text, xmin::text, updated_at::text) FROM roles;";

        await using var reader = await command.ExecuteReaderAsync(cancellation);
        while (await reader.ReadAsync(cancellation))
        {
            rows[reader.GetString(0)] = reader.GetString(1);
        }

        return rows;
    }

    private static async Task MigrateAsync(string connectionString, string target, CancellationToken cancellation)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new AppDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync(target, cancellation);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellation)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = await command.ExecuteNonQueryAsync(cancellation);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, CancellationToken cancellation)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellation);
    }

    /// <summary>A database per test, dropped afterwards.</summary>
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

        public static async Task<TemporaryDatabase> CreateAsync(CancellationToken cancellation)
        {
            var name = Prefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];

            await using (var admin = new NpgsqlConnection(AdminConnection))
            {
                await admin.OpenAsync(cancellation);

                await using var create = admin.CreateCommand();
                create.CommandText = $"""CREATE DATABASE "{name}";""";
                _ = await create.ExecuteNonQueryAsync(cancellation);
            }

            var builder = new NpgsqlConnectionStringBuilder(AdminConnection) { Database = name };

            return new TemporaryDatabase(name, builder.ConnectionString);
        }

        public async ValueTask DisposeAsync()
        {
            // Safety net: DROP can only land on a name these tests made.
            if (!_name.StartsWith(Prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"«{_name}» was not created by these tests; refusing to drop it.");
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
