using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// «HR parolni bilib oldi» belgisi (<c>employee_accounts.password_handed_out_at</c>) —
    /// usiz xodim ilovasiga kirib bo'lmaydi (sababi <c>EmployeeAccount.PasswordHandedOutAt</c> izohida).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Faqat kengaytirish.</b> Bitta bo'sh bo'la oladigan ustun; oldingi reliz uni
    /// bilmaydi va ishlashda davom etadi (u belgini tekshirmaydi — kod qaytarilsa
    /// kirish avvalgidek hammaga ochiq bo'ladi).
    /// </para>
    /// <para>
    /// ⭐ <b>Eski hisoblar audit izidan to'ldiriladi</b>, aks holda ilovadan allaqachon
    /// foydalanayotgan xodim deploy'dan keyin kira olmay qolardi. HR parolni ko'rgan
    /// (<c>password_viewed</c>) yoki o'zi yozgan (<c>password_set</c>) har bir hisob uchun —
    /// JORIY parol berilgandan keyingi birinchi shunday hodisa vaqti. Undan oldingi
    /// ko'rish hisobga olinmaydi: keyin «Yangi parol yasash» bosilgan bo'lsa, yangi
    /// parolni hech kim bilmaydi. 5 soniyalik zaxira — <c>password_set</c> hodisasi
    /// parol vaqtidan bir necha millisekund keyin yozilgan. Audit jadvaliga faqat
    /// O'QISH (uning triggeri UPDATE/DELETE ni rad etadi, bu yerda ularning keragi yo'q).
    /// </para>
    /// <para>
    /// Shifrlangan nusxasi yo'q eski parol (2026-09-27 dan oldin) ham «berilgan»: u
    /// paytda parolni HR o'zi berardi va boshqa yo'li yo'q edi. Joriy parol bilan
    /// allaqachon kirilgan hisob ham — kirish parolni kimdir bilganining isboti.
    /// </para>
    /// </remarks>
    public partial class XodimParoliBerilgani : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "password_handed_out_at",
                table: "employee_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE employee_accounts AS a
                SET password_handed_out_at = h.first_at
                FROM (
                    SELECT acc.id, MIN(l.occurred_at) AS first_at
                    FROM employee_accounts acc
                    JOIN audit_logs l
                      ON l.entity_name = 'EmployeeAccount'
                     AND l.entity_id = acc.id::text
                     AND (l.new_values LIKE '%"password_viewed"%' OR l.new_values LIKE '%"password_set"%')
                     AND l.occurred_at >= acc.password_set_at - interval '5 seconds'
                    GROUP BY acc.id
                ) AS h
                WHERE a.id = h.id
                  AND a.password_handed_out_at IS NULL;
                """);

            // Shifrlangan nusxasi yo'q eski parol (2026-09-27 qarori oldidan) — uni HR o'sha
            // paytdagi tartibda o'zi berib bo'lgan; yoki joriy parol bilan kimdir ALLAQACHON
            // kirgan — demak uni biladi. Ikkalasi ham «berilgan»: ilovadan foydalanayotgan
            // xodim deploy'dan keyin kira olmay qolmasligining kafolati.
            migrationBuilder.Sql("""
                UPDATE employee_accounts
                SET password_handed_out_at = password_set_at
                WHERE password_handed_out_at IS NULL
                  AND (password_protected IS NULL OR last_login_at >= password_set_at);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_handed_out_at",
                table: "employee_accounts");
        }
    }
}
