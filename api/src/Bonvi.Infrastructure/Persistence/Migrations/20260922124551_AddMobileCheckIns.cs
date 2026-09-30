using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Telefondan kelgan belgilarning koordinatalari
    /// (SPEC-xodim-ilovasi §D3, §K1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>ORQAGA QAYTARISH — MA'LUMOT YO'QOLADI, ZAXIRA SHART.</b>
    /// <c>Down</c> qabul qilingan belgilarning koordinatalarini o'chiradi.
    /// O'TISHLARNING o'zi omon qoladi, ya'ni tabel va hisobotlar
    /// o'zgarmaydi — yo'qoladigani faqat DALIL.
    /// </para>
    /// <para>
    /// ⭐ <c>pass_id</c> — <c>ON DELETE CASCADE</c>, butun modulda yagona
    /// kaskad. <c>attendance_passes</c> saqlash muddati siyosati bilan
    /// HAQIQATAN o'chiriladi, ya'ni odatdagi <c>RESTRICT</c> bilan tungi
    /// tozalash birinchi telefon belgisidan bir yil keyin yiqilib,
    /// keyin har kecha yiqilaverardi.
    /// </para>
    /// <para>
    /// ⚠️ <c>ix_mobile_check_ins_employee_id</c> va
    /// <c>ix_mobile_check_ins_work_site_id</c> — EF KONVENSIYASI qo'shgan
    /// indekslar. Ma'lumot modeli hujjati (§E.2) ularni ataylab
    /// ISTAMAGAN edi: PostgreSQL tashqi kalitni o'zi indekslamaydi va
    /// indekssiz bola ustuni faqat OTA qator o'chirilganda qimmatga
    /// tushadi — bu ikkala ota esa yumshoq o'chiriladi. Ular baribir
    /// qoldirildi, chunki konvensiyani o'chirishning ochiq (public) EF
    /// API si yo'q va uni model metama'lumotidan qo'lda olib tashlash
    /// model surati bilan ajralib ketish xavfini tug'dirardi. Narxi
    /// kichik: yiliga ~36 000 qatorli jadvalda ikkita tor b-tree.
    /// </para>
    /// </remarks>
    public partial class AddMobileCheckIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /*
              QULF NAVBATI.

              Jadvalning o'zi yangi va bo'sh, ya'ni qayta yozish xavfi
              yo'q. Lekin `attendance_passes` ga tashqi kalit qo'shish
              o'sha jadvalda qisqa vaqtga `SHARE ROW EXCLUSIVE` qulfini
              oladi va u har bir qabul `INSERT` i ushlab turadigan
              `ROW EXCLUSIVE` bilan to'qnashadi. Odatda millisekundlar;
              uzoq hisobot ochiq bo'lsa — noma'lum muddat, va orqasida
              butun turniket oqimi navbatga tushardi.

              Joriy qilish tinch oynada (~21:00–06:00) bajariladi, bu
              esa ikkinchi himoya: to'silgan migratsiya 3 soniyada
              tushunarli xato bilan tugaydi.
            */
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            migrationBuilder.CreateTable(
                name: "mobile_check_ins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pass_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    accuracy_meters = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    distance_meters = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false),
                    site_radius_meters = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mobile_check_ins", x => x.id);
                    table.CheckConstraint("ck_mobile_check_ins_coordinates", "latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_mobile_check_ins_meters", "accuracy_meters >= 0 AND distance_meters >= 0");
                    table.CheckConstraint("ck_mobile_check_ins_radius", "site_radius_meters BETWEEN 50 AND 5000");
                    table.ForeignKey(
                        name: "fk_mobile_check_ins_attendance_passes_pass_id",
                        column: x => x.pass_id,
                        principalTable: "attendance_passes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mobile_check_ins_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mobile_check_ins_work_sites_work_site_id",
                        column: x => x.work_site_id,
                        principalTable: "work_sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mobile_check_ins_employee_id",
                table: "mobile_check_ins",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_mobile_check_ins_work_site_id",
                table: "mobile_check_ins",
                column: "work_site_id");

            migrationBuilder.CreateIndex(
                name: "ux_mobile_check_ins_pass",
                table: "mobile_check_ins",
                column: "pass_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            migrationBuilder.DropTable(
                name: "mobile_check_ins");
        }
    }
}
