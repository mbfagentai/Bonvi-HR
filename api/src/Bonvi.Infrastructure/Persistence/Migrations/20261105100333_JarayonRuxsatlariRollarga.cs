using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives the existing HR manager and Bosh rahbar roles the default process grants of
    /// docs/SPEC-mening-ishlarim.md §9.2. No schema change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>IdentitySeeder</c> writes defaults only into a new database and rewrites only <c>super_admin</c>. Without this,
    /// on a running database nobody else could open «Mening ishlarim». The rule beside <c>SystemRoles</c> (a new
    /// permission reaches these roles through «Rollar», never silently) gives way here because the SPEC names these
    /// grants as the module's defaults; this is a one-off for §9.2, not a pattern for the next permission.
    /// </para>
    /// <para>
    /// Every statement is guarded, so a role that already holds a code (given by hand in «Rollar») is not rewritten, gets
    /// no duplicate and keeps its order. Codes are literals on purpose: a migration states what it did on the day it was
    /// written, whatever <c>Permissions</c> says later.
    /// </para>
    /// <para>
    /// <see cref="Down"/> takes back exactly what <see cref="Up"/> could have given, so a code the owner had given these
    /// roles by hand before this migration goes too: rows do not record who granted a code. <c>processes.override</c> on
    /// <c>hr_manager</c> and every code on any other role are never this migration's and stay.
    /// </para>
    /// </remarks>
    public partial class JarayonRuxsatlariRollarga : Migration
    {
        private const string HrManager = "hr_manager";
        private const string MainBoss = "main_boss";

        private static readonly (string Role, string Permission)[] Grants =
        [
            (HrManager, "processes.view"),
            (HrManager, "processes.work"),
            (HrManager, "processes.manage"),
            (MainBoss, "processes.view"),
            (MainBoss, "processes.work"),
            (MainBoss, "processes.manage"),
            (MainBoss, "processes.override"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (role, permission) in Grants)
            {
                migrationBuilder.Sql($"""
                    UPDATE roles
                    SET permissions = array_append(permissions, '{permission}'), updated_at = now()
                    WHERE code = '{role}' AND NOT ('{permission}' = ANY (permissions));
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (role, permission) in Grants)
            {
                migrationBuilder.Sql($"""
                    UPDATE roles
                    SET permissions = array_remove(permissions, '{permission}'), updated_at = now()
                    WHERE code = '{role}' AND '{permission}' = ANY (permissions);
                    """);
            }
        }
    }
}
