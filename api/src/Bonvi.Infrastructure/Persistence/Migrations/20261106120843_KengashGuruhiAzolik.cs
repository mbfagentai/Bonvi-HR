using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KengashGuruhiAzolik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "council_members",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<bool>(
                name: "name_confirmed",
                table: "council_members",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "telegram_name",
                table: "council_members",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telegram_username",
                table: "council_members",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Rows so far were typed by hand, so their names are real.
            migrationBuilder.Sql("UPDATE council_members SET name_confirmed = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A member from the group has no phone. The old schema needs a unique one, and the image reads it as text.
            migrationBuilder.Sql("UPDATE council_members SET phone = 'tg:' || telegram_user_id WHERE phone IS NULL;");

            migrationBuilder.DropColumn(
                name: "name_confirmed",
                table: "council_members");

            migrationBuilder.DropColumn(
                name: "telegram_name",
                table: "council_members");

            migrationBuilder.DropColumn(
                name: "telegram_username",
                table: "council_members");

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "council_members",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
