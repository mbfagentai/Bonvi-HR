using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// O'zgarmas audit jurnali (SPEC-rollar-va-audit §5.3, §5.4, §7.7): sakkizta yangi
    /// ustun, ikkita qisman indeks va «faqat qo'shiladi» triggeri. Hech narsa
    /// O'CHIRILMAYDI va turi O'ZGARTIRILMAYDI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Faqat kengaytirish (§5.7).</b> Yangi ustunlar bo'sh bo'la oladi yoki
    /// sukutli (<c>actor_kind</c> = 1, ya'ni User — eski qatorlarning hammasi shunday).
    /// <c>old_values</c>/<c>new_values</c> <c>text</c> bo'lib qoladi: <c>jsonb</c> ga
    /// o'tkazish oldingi relizni (u ularni matn deb bog'laydi) kod qaytarilganda sindirardi.
    /// Oldingi reliz yangi sxemada ishlayveradi — u qatorlarni faqat qo'shadi.
    /// </para>
    /// <para>
    /// ⭐ <b>Trigger ENG OXIRIDA</b> — yuqoridagi qadamlarni to'smasin. U
    /// <c>UPDATE</c>, <c>DELETE</c> va <c>TRUNCATE</c> ni har qanday rol uchun, superuser
    /// uchun ham, rad etadi. Kelajakda jurnal qatorlarini haqiqatan qayta yozishi kerak
    /// bo'lgan migratsiya triggerni kod ko'rib chiqishda KO'RINADIGAN qilib o'chirishi
    /// kerak (<c>ALTER TABLE … DISABLE TRIGGER</c>).
    /// </para>
    /// <para>
    /// <b>Halol chegara:</b> serverda root huquqi bor odam triggerni o'chirib qatorni
    /// tahrirlay oladi. Buni bitta mashina ichida to'sib bo'lmaydi — kunlik
    /// <c>pg_dump</c> zaxiralari bilan ANIQLASH mumkin (§7.7, §16).
    /// </para>
    /// <para>
    /// <c>Down</c> faqat ishlab chiqish uchun: production'da hech qachon
    /// bajarilmaydi — u tarix ustunlarini o'chiradi (§13.3).
    /// </para>
    /// </remarks>
    public partial class AuditJurnaliKengaytirildi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "actor_kind",
                table: "audit_logs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "actor_name",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actor_role",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "changed_fields",
                table: "audit_logs",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "correlation_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_label",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "operation",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "subject_employee_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_correlation_id",
                table: "audit_logs",
                column: "correlation_id",
                filter: "correlation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_subject_employee_id_occurred_at",
                table: "audit_logs",
                columns: new[] { "subject_employee_id", "occurred_at" },
                descending: new[] { false, true },
                filter: "subject_employee_id IS NOT NULL");

            // ---------- O'zgarmaslik: trigger — ENG OXIRIDA ----------
            //
            // ⚠️ `OR REPLACE` va `DROP TRIGGER IF EXISTS` — qayta bajarilishi uchun.
            // Migratsiyadan OLDINGI zaxira `pg_restore --clean` bilan tiklansa, jadval
            // (va uning triggerlari) qayta yaratiladi, lekin bu funksiya eski zaxirada
            // yo'q, ya'ni u o'chirilmay qoladi. Keyingi deploy shu migratsiyani yana
            // bajaradi — oddiy `CREATE FUNCTION` «already exists» bilan yiqilib, API
            // ishga tushmay qolardi.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit_logs_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  RAISE EXCEPTION 'audit_logs: yozuvlar o''zgartirilmaydi va o''chirilmaydi (%)', TG_OP;
                END $$;
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_logs_append_only ON audit_logs;
                CREATE TRIGGER trg_audit_logs_append_only BEFORE UPDATE OR DELETE ON audit_logs
                  FOR EACH ROW EXECUTE FUNCTION audit_logs_append_only();
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_logs_no_truncate ON audit_logs;
                CREATE TRIGGER trg_audit_logs_no_truncate BEFORE TRUNCATE ON audit_logs
                  FOR EACH STATEMENT EXECUTE FUNCTION audit_logs_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Teskari tartib: avval trigger (aks holda ustunlarni olib tashlash
            // emas, balki undan keyingi har qanday qayta yozish to'silardi).
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_logs_no_truncate ON audit_logs;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_logs_append_only ON audit_logs;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS audit_logs_append_only();");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_correlation_id",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_subject_employee_id_occurred_at",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "actor_kind",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "actor_name",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "actor_role",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "changed_fields",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "entity_label",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "operation",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "subject_employee_id",
                table: "audit_logs");
        }
    }
}
