using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bot xabarlarining natijasi: <c>candidates.rejection_notice_delivered</c> va
    /// <c>candidates.interview_notice_delivered</c> (egasining qarori, 2026-10-05 —
    /// «1 marta yuborilsa yetadi»).
    /// </summary>
    /// <remarks>
    /// Faqat ikkita bo'sh (nullable) ustun — mavjud qatorlar QAYTA YOZILMAYDI.
    /// <c>null</c> eski yozuvlarda «o'shanda faqat yetkazilganlar belgilanardi»
    /// degani, ya'ni eski kod bilan ham, image rollback bilan ham mos.
    /// </remarks>
    public partial class NotificationDeliveryOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "interview_notice_delivered",
                table: "candidates",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "rejection_notice_delivered",
                table: "candidates",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "interview_notice_delivered",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "rejection_notice_delivered",
                table: "candidates");
        }
    }
}
