using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ommaviy oferta bilan rozilik vaqtini IXTIYORIY qiladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima o'zgaradi.</b> <c>candidate_questionnaires.offer_accepted_at</c>
    /// endi <c>NULL</c> bo'lishi mumkin. Yagona amal —
    /// <c>ALTER COLUMN … DROP NOT NULL</c>; hech qanday ustun yoki jadval
    /// o'chirilmaydi, mavjud qiymatlar tegilmaydi.
    /// </para>
    /// <para>
    /// <b>Nega.</b> Ustun majburiy bo'lgani uchun bazada «rozilik umuman
    /// so'ralmagan» degan holatni ifodalab bo'lmasdi. Ommaviy oferta
    /// havolasi (<c>bot.offerUrl</c>) sozlanmaganda bot anketaning
    /// 21-qadamini UMUMAN chizmaydi — nomzod hech qanday oferta ko'rmaydi,
    /// lekin bazaga baribir «rozilik shu lahzada berildi» deb yozilardi
    /// (bot ARIZA YUBORISH vaqtini qo'yardi). Bu huquqiy faktni
    /// soxtalashtirish edi va sababi strukturaviy: ma'nosini ifodalay
    /// oladigan qiymat yo'q edi.
    /// </para>
    /// <para>
    /// Bundan keyin: <c>NULL</c> — rozilik SO'RALMAGAN, qiymat — aynan
    /// o'sha lahzada berilgan.
    /// </para>
    /// </remarks>
    public partial class OfertaRoziligiIxtiyoriy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "offer_accepted_at",
                table: "candidate_questionnaires",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");
        }

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// ⚠️ <b>Orqaga qaytish MA'LUMOT BUZADI va u boshqacha bo'la
        /// olmaydi.</b> Eski sxemada «rozilik so'ralmagan» holatini
        /// ifodalaydigan qiymat yo'q, ya'ni <c>NULL</c> qatorlarga
        /// nimadir yozish SHART — aks holda <c>SET NOT NULL</c> o'sha
        /// qatorlarda yiqiladi va qaytish umuman bajarilmaydi.
        /// </para>
        /// <para>
        /// Bo'sh qiymatlar nomzodning ARIZA KELGAN vaqti bilan
        /// to'ldiriladi — bu aynan eski botning o'zi yozib turgan qiymat
        /// (<c>?? BotClock.Now</c>, yuborish lahzasi). Ya'ni qaytish
        /// eski, NOTO'G'RI holatni aynan tiklaydi va yangi qiymat o'ylab
        /// topmaydi. <c>0001-01-01</c> kabi «bo'sh» sana ataylab
        /// ishlatilmaydi: u ekranda haqiqiy sana bo'lib ko'rinardi.
        /// </para>
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql("""
                UPDATE candidate_questionnaires AS q
                SET offer_accepted_at = c.created_at
                FROM candidates AS c
                WHERE c.id = q.candidate_id
                  AND q.offer_accepted_at IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "offer_accepted_at",
                table: "candidate_questionnaires",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
