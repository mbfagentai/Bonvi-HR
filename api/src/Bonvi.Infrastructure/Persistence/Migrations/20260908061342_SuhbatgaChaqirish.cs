using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Nomzodni suhbatga chaqirish: bosqichdagi bayroq, suhbat vaqti va
    /// Telegram taklifi navbati.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Uchta ustun.</b> <c>pipeline_stages.invites_to_interview</c> —
    /// qaysi bosqich taklif yuborishini BOSQICHNING O'ZI aytadi (sababi
    /// <c>PipelineStage.InvitesToInterview</c> izohida).
    /// <c>candidates.interview_at</c> — suhbat lahzasi,
    /// <c>candidates.interview_notice_sent_at</c> — xabar navbatining
    /// belgisi (<c>rejection_notice_sent_at</c> bilan aynan bir xil naqsh).
    /// </para>
    /// <para>
    /// <b>Qisman indeks</b> — rad etish navbatidagi bilan bir xil sabab:
    /// bot bu ro'yxatni har necha o'n soniyada so'raydi va javob odatda
    /// BO'SH bo'ladi, jadvalda esa minglab nomzod yotadi.
    /// </para>
    /// <para>
    /// <b>Bayroq mavjud bazada ham qo'yiladi.</b> Urug' fayli faqat bo'sh
    /// ma'lumotnomani to'ldiradi, ishlab turgan bazada esa
    /// «Кенгашга чақирилди» bosqichi oldingi migratsiya bilan qo'shilgan
    /// (<c>VoronkaBosqichlariToldirildi</c>) va u bayroqsiz turibdi —
    /// ya'ni tugma bosilganda suhbat vaqti so'ralmasdi.
    /// </para>
    /// </remarks>
    public partial class SuhbatgaChaqirish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<bool>(
                name: "invites_to_interview",
                table: "pipeline_stages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "interview_at",
                table: "candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "interview_notice_sent_at",
                table: "candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidates_interview_notice_sent_at",
                table: "candidates",
                column: "interview_notice_sent_at",
                filter: "telegram_chat_id IS NOT NULL AND interview_at IS NOT NULL AND interview_notice_sent_at IS NULL");

            // Nom bo'yicha, chunki bosqichning identifikatori har bazada
            // boshqacha (u `gen_random_uuid()` bilan yaratilgan). Bosqich
            // topilmasa (HR qayta nomlagan) hech narsa o'zgarmaydi va
            // migratsiya yiqilmaydi — bayroqni ekrandan qo'yish mumkin.
            migrationBuilder.Sql("""
                UPDATE pipeline_stages
                   SET invites_to_interview = true
                 WHERE deleted_at IS NULL
                   AND lower(name) = lower('Кенгашга чақирилди');
                """);

            /*
              Suhbat taklifining ikkita qatori — telefon va manzil.

              ⭐ Nega migratsiyada. Sozlama yozuvi bazada FAQAT qiymat
              qo'yilganda paydo bo'ladi (urug'lantiruvchi sozlamalarga
              tegmaydi), ya'ni bu maydonlar bo'sh bo'lib turardi va
              birinchi taklif telefonsiz, manzilsiz ketardi. Qiymatlar
              foydalanuvchining O'ZI bergan namunasidan olingan.

              `ON CONFLICT DO NOTHING`: HR ekrandan boshqa raqam
              qo'ygan bo'lsa ustidan yozilmaydi — bu SUKUT qiymat,
              majburiy emas.
            */
            migrationBuilder.Sql("""
                INSERT INTO system_settings (id, key, value, created_at)
                VALUES
                    (gen_random_uuid(), 'bot.interview.phone', '+998 99 980 87 00', now()),
                    (gen_random_uuid(), 'bot.interview.address', 'Qo''qon', now())
                ON CONFLICT (key) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        /// <summary>
        /// Ustunlarni olib tashlaydi — ular bilan birga chaqirilgan suhbat
        /// vaqtlari ham yo'qoladi.
        /// </summary>
        /// <remarks>
        /// Bu ustunlar faqat SHU funksiyaga tegishli va boshqa hech qayerda
        /// ishlatilmaydi: orqaga qaytish nomzodning bosqich tarixiga ham,
        /// holatiga ham tegmaydi. Ya'ni yo'qoladigan yagona narsa —
        /// yuborilgan taklifning vaqti, va u xabarning o'zida qolgan.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "ix_candidates_interview_notice_sent_at",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "invites_to_interview",
                table: "pipeline_stages");

            migrationBuilder.DropColumn(
                name: "interview_at",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "interview_notice_sent_at",
                table: "candidates");

            // Sozlamalar ham olib tashlanadi: ular faqat suhbat taklifiga
            // tegishli va boshqa hech qayerda o'qilmaydi.
            migrationBuilder.Sql("""
                DELETE FROM system_settings
                 WHERE key IN ('bot.interview.phone', 'bot.interview.address');
                """);
        }
    }
}
