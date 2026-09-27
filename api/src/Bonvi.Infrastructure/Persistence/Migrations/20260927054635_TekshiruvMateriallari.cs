using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TekshiruvMateriallari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /*
              ⚠️ «employees.mehnat_uz_screenshot_*» ustunlari ATAYLAB
              O'CHIRILMAYDI (EF shunday taklif qilgan edi). Bu kengaytirish/
              qisqartirish (expand/contract) usuli:

              · Bu reliz: yangi jadval yaratiladi va eski skrinshotlar unga
                NUSXALANADI. Eski ustunlar bazada qoladi, yangi kod ularga
                tegmaydi.
              · Sabab — deploy tartibi: muammo chiqsa, eski image'ga qaytiladi
                (`rollback-<commit>` tegi). Eski kod shu ustunlarni o'qiydi;
                ular o'chirilgan bo'lsa, xodim kartasi butunlay yiqilardi.
              · Keyingi relizda, yangi versiya barqarorligi tasdiqlangach,
                ustunlar alohida migratsiya bilan olib tashlanadi.

              Modelda (va snapshot'da) ular endi yo'q — keyingi
              `migrations add` ularni qayta taklif qilmaydi, ya'ni
              qisqartirish migratsiyasi QO'LDA yoziladi.
            */
            migrationBuilder.CreateTable(
                name: "employee_check_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_check_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_check_files_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_check_files_employee_id",
                table: "employee_check_files",
                column: "employee_id",
                filter: "deleted_at IS NULL");

            /*
              Eski yagona skrinshot ro'yxatning BIRINCHI materiali bo'ladi.
              Fayl diskda o'z joyida qoladi — faqat kalit ko'chadi (nusxa
              yo'q, disk ikki barobar band bo'lmaydi).

              · Nom saqlanmagan edi — tur bo'yicha yasaladi.
              · Hajm saqlanmagan edi va migratsiya diskni o'qimaydi —
                `size_bytes` NULL («noma'lum»).
              · Vaqt — xodim yozuvining oxirgi o'zgarishi: skrinshot
                yuklanganda aynan u yangilanardi.
              · Yumshoq o'chirilgan xodimlar ham ko'chadi: dalil ular
                uchun ham yo'qolmasligi kerak.
            */
            migrationBuilder.Sql("""
                INSERT INTO employee_check_files
                    (id, employee_id, file_key, file_name, content_type, size_bytes, created_at, created_by)
                SELECT gen_random_uuid(),
                       e.id,
                       e.mehnat_uz_screenshot_key,
                       'mehnat-uz-skrinshot' || CASE e.mehnat_uz_screenshot_content_type
                           WHEN 'application/pdf' THEN '.pdf'
                           WHEN 'image/png'       THEN '.png'
                           WHEN 'image/webp'      THEN '.webp'
                           WHEN 'image/jpeg'      THEN '.jpg'
                           ELSE ''
                       END,
                       coalesce(e.mehnat_uz_screenshot_content_type, 'application/octet-stream'),
                       NULL,
                       coalesce(e.updated_at, e.created_at),
                       coalesce(e.updated_by, e.created_by)
                  FROM employees AS e
                 WHERE e.mehnat_uz_screenshot_key IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            /*
              Eski ustunlar Up'da o'chirilmagan va yangi kod ularga
              tegmagan — ya'ni ulardagi skrinshot o'z holicha turibdi va
              qaytishda tiklash kerak emas.

              ⚠️ Up'dan KEYIN yuklangan materiallarning yozuvlari yo'qoladi
              (fayllarning o'zi diskda qoladi). Ularni saqlash kerak bo'lsa —
              Down emas, zaxiradan tiklash (`docs/DEPLOY-CHECKLIST.md`).
            */
            migrationBuilder.DropTable(
                name: "employee_check_files");
        }
    }
}
