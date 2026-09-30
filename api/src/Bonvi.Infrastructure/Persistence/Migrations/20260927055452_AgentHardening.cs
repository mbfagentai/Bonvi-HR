using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rejected_version",
                table: "device_enrollments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "clock_offset_seconds",
                table: "attendance_passes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "agent_seen_at",
                table: "attendance_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "foreign_persons",
                table: "attendance_devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "held_removals",
                table: "attendance_devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_rejected_at",
                table: "attendance_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reconciled_at",
                table: "attendance_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "removals_approved_until",
                table: "attendance_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "terminal_seen_at",
                table: "attendance_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "terminal_serial",
                table: "attendance_devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            /*
              Mavjud qurilmalarning aloqa belgilarini bor ma'lumotdan to'ldirish
              (SPEC-agent §L). Soati o'lchangan qurilma — agent orqali ishlaydi:
              uning «oxirgi aloqa» si agentniki, terminal bilan aloqasi esa soat
              o'lchovi yoki oxirgi o'tish. Qolganlari o'zi push qiladi — ular
              uchun har bir so'rov terminalning o'zidan.
            */
            migrationBuilder.Sql(
                """
                UPDATE attendance_devices
                SET agent_seen_at = CASE WHEN clock_checked_at IS NOT NULL THEN last_seen_at END,
                    terminal_seen_at = CASE
                        WHEN clock_checked_at IS NOT NULL THEN GREATEST(clock_checked_at, last_event_at)
                        ELSE last_seen_at
                    END;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_attendance_identities_managed_employee",
                table: "attendance_identities",
                columns: new[] { "provider", "employee_id" },
                unique: true,
                filter: "deleted_at IS NULL AND length(external_person_id) = 9 AND left(external_person_id, 1) = '9'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_attendance_identities_managed_employee",
                table: "attendance_identities");

            migrationBuilder.DropColumn(
                name: "rejected_version",
                table: "device_enrollments");

            migrationBuilder.DropColumn(
                name: "clock_offset_seconds",
                table: "attendance_passes");

            migrationBuilder.DropColumn(
                name: "agent_seen_at",
                table: "attendance_devices");

            migrationBuilder.DropColumn(
                name: "foreign_persons",
                table: "attendance_devices");

            migrationBuilder.DropColumn(
                name: "held_removals",
                table: "attendance_devices");

            migrationBuilder.DropColumn(
                name: "last_rejected_at",
                table: "attendance_devices");

            migrationBuilder.DropColumn(
                name: "reconciled_at",
                table: "attendance_devices");

            migrationBuilder.DropColumn(
                name: "removals_approved_until",
                table: "attendance_devices");

            migrationBuilder.DropColumn(
                name: "terminal_seen_at",
                table: "attendance_devices");

            migrationBuilder.DropColumn(
                name: "terminal_serial",
                table: "attendance_devices");
        }
    }
}
