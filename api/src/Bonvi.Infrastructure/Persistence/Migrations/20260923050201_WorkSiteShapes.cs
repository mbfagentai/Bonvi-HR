using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Obyekt shakllari — doira va ko'pburchak (SPEC-joylashuvlar §A, §B).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Faqat QO'SHADI.</b> <c>work_sites</c> ga to'rtta ustun
    /// (<c>address</c>, <c>shape</c>, <c>polygon</c>, <c>edge_buffer_meters</c>),
    /// <c>mobile_check_ins</c> ga bitta (<c>site_shape</c>). Mavjud qatorlar
    /// <c>shape = 'circle'</c>, <c>edge_buffer_meters = 50</c>,
    /// <c>site_shape = 'circle'</c> bo'lib qoladi — va QAYTA YOZILMAYDI:
    /// PostgreSQL 11+ doimiy sukutli ustunni katalogda saqlaydi.
    /// </para>
    /// <para>
    /// Migratsiyadagi ikki «DROP» ham ATAYLAB va ma'lumotga tegmaydi:
    /// (1) <c>ck_mobile_check_ins_radius</c> olib tashlanib, shaklga qarab
    /// tekshiradigan ko'rinishda qayta qo'yiladi (ko'pburchak zaxirasi 0–500,
    /// eski cheklov esa 50 dan kichikni rad etardi); (2) ustun sukutlari
    /// qatorlarni to'ldirgandan keyin olib tashlanadi — sababi pastda.
    /// </para>
    /// <para>
    /// ⚠️ <b>ORQAGA QAYTARISH — ko'pburchak bor bazada BAJARILMAYDI.</b>
    /// <c>Down</c> avval tekshiradi: <c>shape = 'polygon'</c> obyekt yoki
    /// <c>site_shape = 'polygon'</c> belgi topilsa, tushunarli xato bilan
    /// to'xtaydi. Aks holda ko'pburchak obyekt jimgina «markazdan 200 m»
    /// doiraga aylanib, telefon belgilarini HR chizmagan hudud bo'yicha
    /// qabul qila boshlardi, ko'pburchak belgilarining dalil qatorlari esa
    /// eski radius cheklovini buzib, qaytarishni yarim yo'lda yiqitardi.
    /// Doiralargina bor bazada <c>Down</c> faqat manzillarni yo'qotadi.
    /// </para>
    /// </remarks>
    public partial class WorkSiteShapes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /*
              QULF NAVBATI — `AddEmployeeMobileFlag` dagi bilan bir xil sabab.
              Ikkala jadval ham kichik (obyektlar — o'nlab, belgilar — yiliga
              ~36 000), lekin ustun va cheklov qo'shish `ACCESS EXCLUSIVE`
              qulfini so'raydi va uzoq hisobot ortida kutib qolsa, butun
              telefon belgilari oqimi navbatga tushardi.
            */
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            // ── work_sites ─────────────────────────────────────────────

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "work_sites",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shape",
                table: "work_sites",
                type: "text",
                nullable: false,
                defaultValue: "circle");

            migrationBuilder.AddColumn<string>(
                name: "polygon",
                table: "work_sites",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "edge_buffer_meters",
                table: "work_sites",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            /*
              ⚠️ USTUN SUKUTLARI mavjud qatorlarni to'ldirgach DARHOL olib
              tashlanadi — `AddEmployeeMobileFlag` dagi qoida
              (`EmployeeAppConfigurations` boshidagi 2-band): qator har doim
              domen bergan qiymat bilan yoziladi.

              Bu yerda qoidaning o'z xavfi ham bor. `edge_buffer_meters` da 0
              — HAQIQIY qiymat (chiziq aynan devor bo'ylab). Kimdir bazadagi
              sukutni ko'rib, modelga `HasDefaultValue(50)` qo'shsa, EF CLR
              sukutini (0) «qiymat berilmadi» deb o'qiydi va HR ataylab
              qo'ygan 0 jimgina 50 metr bo'lib yozilardi — to'siq kimsaning
              qarorisiz kengayardi.
            */
            migrationBuilder.Sql(
                """
                ALTER TABLE work_sites
                    ALTER COLUMN shape DROP DEFAULT,
                    ALTER COLUMN edge_buffer_meters DROP DEFAULT;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_sites_shape",
                table: "work_sites",
                sql: "shape IN ('circle', 'polygon')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_sites_polygon",
                table: "work_sites",
                sql: "(shape = 'polygon') = (polygon IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_sites_edge_buffer",
                table: "work_sites",
                sql: "edge_buffer_meters BETWEEN 0 AND 500");

            // ── mobile_check_ins ───────────────────────────────────────

            // Hozirgacha qabul qilingan har bir belgini DOIRA qoidasi hukm
            // qilgan — boshqa shakl yo'q edi. Shu sababli 'circle' taxmin
            // emas, fakt.
            migrationBuilder.AddColumn<string>(
                name: "site_shape",
                table: "mobile_check_ins",
                type: "text",
                nullable: false,
                defaultValue: "circle");

            migrationBuilder.Sql(
                "ALTER TABLE mobile_check_ins ALTER COLUMN site_shape DROP DEFAULT;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mobile_check_ins_radius",
                table: "mobile_check_ins");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mobile_check_ins_radius",
                table: "mobile_check_ins",
                sql: "(site_shape = 'circle' AND site_radius_meters BETWEEN 50 AND 5000) OR (site_shape = 'polygon' AND site_radius_meters BETWEEN 0 AND 500)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mobile_check_ins_site_shape",
                table: "mobile_check_ins",
                sql: "site_shape IN ('circle', 'polygon')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            // Sinf izohidagi to'siq: ko'pburchak ma'lumoti bor bazada qaytarish
            // HECH NARSANI o'zgartirmasdan to'xtaydi. Yumshoq o'chirilgan
            // obyektlar ham sanaladi — ularga tarixiy belgilar ishora qiladi.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM work_sites WHERE shape = 'polygon')
                       OR EXISTS (SELECT 1 FROM mobile_check_ins WHERE site_shape = 'polygon') THEN
                        RAISE EXCEPTION
                            'WorkSiteShapes: ko''pburchak obyekt yoki belgi bor — qaytarish ularni doiraga aylantirardi. Avval ularni qo''lda hal qiling.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_mobile_check_ins_site_shape",
                table: "mobile_check_ins");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mobile_check_ins_radius",
                table: "mobile_check_ins");

            migrationBuilder.DropColumn(
                name: "site_shape",
                table: "mobile_check_ins");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mobile_check_ins_radius",
                table: "mobile_check_ins",
                sql: "site_radius_meters BETWEEN 50 AND 5000");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_sites_edge_buffer",
                table: "work_sites");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_sites_polygon",
                table: "work_sites");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_sites_shape",
                table: "work_sites");

            migrationBuilder.DropColumn(
                name: "edge_buffer_meters",
                table: "work_sites");

            migrationBuilder.DropColumn(
                name: "polygon",
                table: "work_sites");

            migrationBuilder.DropColumn(
                name: "shape",
                table: "work_sites");

            migrationBuilder.DropColumn(
                name: "address",
                table: "work_sites");
        }
    }
}
