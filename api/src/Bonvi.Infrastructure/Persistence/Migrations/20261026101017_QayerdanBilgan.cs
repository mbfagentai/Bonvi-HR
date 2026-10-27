using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// «Ish haqida qayerdan bilgan» — tahrirlanadigan ma'lumotnoma va nomzodning o'z maydoni
    /// (SPEC-qayerdan-bilgan §3, §4): yangi <c>referral_channels</c> jadvali (sakkizta boshlang'ich
    /// kanal bilan), <c>candidates.referral_channel_id</c> + <c>referral_detail</c>, bot anketalaridagi
    /// eski javobni ko'chirish va <c>candidate_questionnaires.referral_source</c> ni olib tashlash.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Tartib — ma'lumot yo'qolmasligi uchun.</b> Avval kanallar QAT'IY identifikatorlar bilan
    /// yoziladi (oxirgi raqam — eski sanoq qiymati, ya'ni ko'chirish har bazada bir xil natija beradi),
    /// keyin nomzodga ustunlar qo'shiladi, keyin <c>referral_source</c> → <c>referral_channel_id</c>
    /// ko'chiriladi va FAQAT SHUNDAN KEYIN eski ustun o'chiriladi. Prod'da 275 anketa (2026-10-26).
    /// </para>
    /// <para>
    /// Nom bo'yicha unikal indeks — <c>ux_referral_channels_name</c> (<c>lower(name)</c>, yumshoq
    /// o'chirilganlar chetda): <c>PostgresFault</c> aynan <c>ux_*_name</c> naqshini «nom band» (409) deb
    /// taniydi. <c>asks_detail</c> ning sukuti (<c>false</c>) faqat bazada — loyiha qoidasi
    /// (<c>AbsenceTypeConfiguration</c> izohi).
    /// </para>
    /// <para>
    /// AI ulanishining o'qish roli (<c>mcp_apply_ro_grants</c>) yangi jadvalni va ikkita yangi ustunni
    /// ko'rishi uchun funksiya QAYTA yaratiladi (<c>McpReadOnly</c> izohi: ro'yxatni o'zgartirish —
    /// yangi migratsiya). Ruxsatlarni API ishga tushganda qo'llaydi (<c>McpDatabase.ProvisionAsync</c>).
    /// </para>
    /// <para>
    /// ⚠️ <b>Eski image bu migratsiyadan keyin anketa yoza olmaydi</b> (<c>referral_source</c> yo'q).
    /// Rollback — Down (yoki zaxira): <c>referral_source</c> qaytadi va seed kanallar eski qiymatlariga,
    /// HR qo'shgan boshqa har qanday kanal esa 6 («Boshqa») ga o'giriladi; anketasi yo'q nomzodlarning
    /// kanali va BARCHA aniqlik matnlari yo'qoladi (eski sxemada ular uchun joy yo'q).
    /// </para>
    /// </remarks>
    public partial class QayerdanBilgan : Migration
    {
        /// <summary>
        /// Eski sanoq qiymati → seed kanal: <c>(eski qiymat, identifikator)</c>. Up ham, Down ham shu
        /// ro'yxatdan — ikki yo'nalish bir-biridan ajralib ketmaydi.
        /// </summary>
        private const string SeedMapSql = """
            (VALUES
                (1, '0199f0a0-5e1d-7c00-8000-000000000001'::uuid),
                (2, '0199f0a0-5e1d-7c00-8000-000000000002'::uuid),
                (3, '0199f0a0-5e1d-7c00-8000-000000000003'::uuid),
                (4, '0199f0a0-5e1d-7c00-8000-000000000004'::uuid),
                (5, '0199f0a0-5e1d-7c00-8000-000000000005'::uuid),
                (6, '0199f0a0-5e1d-7c00-8000-000000000006'::uuid),
                (7, '0199f0a0-5e1d-7c00-8000-000000000007'::uuid),
                (8, '0199f0a0-5e1d-7c00-8000-000000000008'::uuid)) AS seed(old_value, channel_id)
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            // ── 1. Ma'lumotnoma ──────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "referral_channels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asks_detail = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name = table.Column<string>(type: "text", nullable: false, collation: "uz-Cyrl-x-icu"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referral_channels", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_referral_channels_is_active_name",
                table: "referral_channels",
                columns: new[] { "is_active", "name" });

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_referral_channels_name
                    ON referral_channels (lower(name))
                    WHERE deleted_at IS NULL;
                """);

            // Bugungi bot tugmalaridagi nomlar (SPEC §3 jadvali); tutuq belgisi lokaldagidek (’)
            migrationBuilder.Sql(
                """
                INSERT INTO referral_channels (id, name, asks_detail, is_active, created_at) VALUES
                    ('0199f0a0-5e1d-7c00-8000-000000000005', 'Telegram',             false, true, now()),
                    ('0199f0a0-5e1d-7c00-8000-000000000002', 'Instagram / Facebook', false, true, now()),
                    ('0199f0a0-5e1d-7c00-8000-000000000003', 'OLX.uz',               false, true, now()),
                    ('0199f0a0-5e1d-7c00-8000-000000000004', 'hh.uz',                false, true, now()),
                    ('0199f0a0-5e1d-7c00-8000-000000000007', 'E’lon',                false, true, now()),
                    ('0199f0a0-5e1d-7c00-8000-000000000008', 'Kollej / OTM',         false, true, now()),
                    ('0199f0a0-5e1d-7c00-8000-000000000001', 'Tanishlar orqali',     true,  true, now()),
                    ('0199f0a0-5e1d-7c00-8000-000000000006', 'Boshqa',               true,  true, now());
                """);

            // ── 2. Nomzodning o'z maydoni ────────────────────────────
            migrationBuilder.AddColumn<Guid>(
                name: "referral_channel_id",
                table: "candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "referral_detail",
                table: "candidates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidates_referral_channel_id",
                table: "candidates",
                column: "referral_channel_id");

            migrationBuilder.AddForeignKey(
                name: "fk_candidates_referral_channels_referral_channel_id",
                table: "candidates",
                column: "referral_channel_id",
                principalTable: "referral_channels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ── 3. Bot anketalaridagi javob → nomzod (aniqlik bo'lmagan — bot uni so'ramagan) ──
            migrationBuilder.Sql(
                $"""
                UPDATE candidates AS c
                   SET referral_channel_id = seed.channel_id
                  FROM candidate_questionnaires AS q
                  JOIN {SeedMapSql} ON seed.old_value = q.referral_source
                 WHERE q.candidate_id = c.id;
                """);

            // ── 4. Eski ustun — faqat ko'chirilgandan KEYIN ──────────
            migrationBuilder.DropColumn(
                name: "referral_source",
                table: "candidate_questionnaires");

            // ── 5. AI o'qish roli — yangi jadval va ustunlar ─────────
            migrationBuilder.Sql(ReadOnlyGrantsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            migrationBuilder.Sql(PreviousReadOnlyGrantsSql);

            // Eski ustun qaytadi: avval sukut 6 («Boshqa») bilan — HR qo'shgan kanal ham, kanalsiz
            // anketali nomzod ham shunga tushadi; seed kanallar o'z qiymatiga. Keyin sukut olinadi —
            // ustun asl holiga (`NomzodAnketasi`: integer NOT NULL, sukutsiz) qaytadi.
            migrationBuilder.AddColumn<int>(
                name: "referral_source",
                table: "candidate_questionnaires",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.Sql(
                $"""
                UPDATE candidate_questionnaires AS q
                   SET referral_source = seed.old_value
                  FROM candidates AS c
                  JOIN {SeedMapSql} ON seed.channel_id = c.referral_channel_id
                 WHERE c.id = q.candidate_id;

                ALTER TABLE candidate_questionnaires ALTER COLUMN referral_source DROP DEFAULT;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_candidates_referral_channels_referral_channel_id",
                table: "candidates");

            migrationBuilder.DropIndex(
                name: "ix_candidates_referral_channel_id",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "referral_channel_id",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "referral_detail",
                table: "candidates");

            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_referral_channels_name;");

            migrationBuilder.DropTable(
                name: "referral_channels");
        }

        /*
          `mcp_apply_ro_grants` — `McpReadOnly` dagi funksiya, ikkita farq bilan:
          `allowed` ga `referral_channels` (butun jadval — nom, faollik, izoh), `candidates`
          ustunlariga `referral_channel_id` va `referral_detail`. Qolgani AYNAN o'sha.
        */
        private const string ReadOnlyGrantsSql = """
            CREATE OR REPLACE FUNCTION public.mcp_apply_ro_grants(ro_role text) RETURNS void
            LANGUAGE plpgsql
            SET search_path = public, pg_temp
            AS $fn$
            DECLARE
                t text;
                cols text;
                -- Nomi shunga mos ustun hech qanday ro'yxat bilan berilmaydi
                -- (`McpDatabase.SensitiveColumnPattern` bilan BIR XIL).
                sensitive text := '(key|file|photo|passport|pasport|pinfl|jshshir|token|secret|hash|screenshot|selfie|attachment|password|login)';
                allowed text[] := ARRAY[
                    'absence_requests', 'absence_types', 'attendance_days', 'award_types',
                    'candidate_notes', 'candidate_questionnaires', 'candidate_stage_entries',
                    'departments', 'directions', 'document_types', 'employee_competencies',
                    'employee_competency_languages', 'employee_flags', 'employment_events',
                    'employments', 'exit_interviews', 'incentives', 'incidents', 'legal_entities',
                    'mobile_check_ins', 'penalty_types', 'pipeline_stages', 'position_duties',
                    'positions', 'probations', 'referral_channels', 'roles', 'schedule_assignments',
                    'termination_reasons', 'vacancies', 'work_schedule_dates',
                    'work_schedule_days', 'work_schedules', 'work_sites'
                ];
                column_grants jsonb := jsonb_build_object(
                    'employees', jsonb_build_array(
                        'id', 'first_name', 'last_name', 'middle_name', 'birth_date', 'gender',
                        'phone', 'second_phone', 'address', 'speciality', 'telegram_url',
                        'criminal_record_note', 'has_criminal_record', 'direction_ids',
                        'needs_review', 'review_note', 'mehnat_uz_url', 'mobile_check_in_allowed',
                        'created_at', 'created_by', 'updated_at', 'updated_by', 'deleted_at', 'deleted_by'),
                    'candidates', jsonb_build_array(
                        'id', 'vacancy_id', 'current_stage_id', 'status', 'source', 'first_name',
                        'last_name', 'middle_name', 'birth_date', 'phone', 'address', 'speciality',
                        'note', 'closed_at', 'closing_note', 'rejection_notice_sent_at',
                        'telegram_language', 'interview_at', 'interview_notice_sent_at',
                        'next_contact_date', 'referral_channel_id', 'referral_detail',
                        'created_at', 'created_by', 'updated_at', 'updated_by',
                        'deleted_at', 'deleted_by'),
                    'employee_documents', jsonb_build_array(
                        'id', 'employee_id', 'document_type_id', 'created_at', 'created_by', 'deleted_at'),
                    'attendance_passes', jsonb_build_array(
                        'id', 'device_id', 'resolved_employee_id', 'occurred_at', 'local_date',
                        'direction', 'direction_source', 'method', 'received_at'),
                    'attendance_devices', jsonb_build_array(
                        'id', 'name', 'model', 'provider', 'direction_role', 'legal_entity_id',
                        'department_id', 'location_note', 'is_active', 'last_seen_at',
                        'last_event_at', 'deleted_at'),
                    'users', jsonb_build_array(
                        'id', 'first_name', 'last_name', 'middle_name', 'employee_id', 'is_active', 'deleted_at')
                );
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = ro_role) THEN
                    RETURN;
                END IF;

                -- Noldan: avval berilgan hamma narsa (jadval VA ustun darajasida) olinadi.
                FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' LOOP
                    EXECUTE format('REVOKE ALL ON public.%I FROM %I', t, ro_role);
                END LOOP;

                -- «Butun jadval» ham USTUN darajasida beriladi va nomi nozikka
                -- o'xshagan ustun (`sensitive`) HECH QACHON berilmaydi: kelajakdagi
                -- `absence_requests.attachment_key` yoki `mobile_check_ins.selfie_key`
                -- kabi ustun o'z-o'zidan AI'ga ochilib qolmasin.
                FOREACH t IN ARRAY allowed LOOP
                    IF to_regclass('public.' || quote_ident(t)) IS NULL THEN
                        CONTINUE;
                    END IF;

                    SELECT string_agg(quote_ident(c.column_name), ', ')
                      INTO cols
                      FROM information_schema.columns c
                     WHERE c.table_schema = 'public'
                       AND c.table_name = t
                       AND c.column_name !~* sensitive;

                    IF cols IS NOT NULL THEN
                        EXECUTE format('GRANT SELECT (%s) ON public.%I TO %I', cols, t, ro_role);
                    END IF;
                END LOOP;

                FOR t IN SELECT jsonb_object_keys(column_grants) LOOP
                    IF to_regclass('public.' || quote_ident(t)) IS NULL THEN
                        CONTINUE;
                    END IF;

                    -- Faqat haqiqatan MAVJUD ustunlar: ro'yxatdagi nom keyinchalik
                    -- o'chirilsa, GRANT butun funksiyani yiqitmasin.
                    SELECT string_agg(quote_ident(c.column_name), ', ')
                      INTO cols
                      FROM information_schema.columns c
                     WHERE c.table_schema = 'public'
                       AND c.table_name = t
                       AND c.column_name IN (SELECT jsonb_array_elements_text(column_grants -> t))
                       AND c.column_name !~* sensitive;

                    IF cols IS NOT NULL THEN
                        EXECUTE format('GRANT SELECT (%s) ON public.%I TO %I', cols, t, ro_role);
                    END IF;
                END LOOP;
            END
            $fn$;

            REVOKE ALL ON FUNCTION public.mcp_apply_ro_grants(text) FROM PUBLIC;
            """;

        /// <summary>Down uchun — <c>McpReadOnly</c> dagi asl funksiya, o'zgarishsiz.</summary>
        private const string PreviousReadOnlyGrantsSql = """
            CREATE OR REPLACE FUNCTION public.mcp_apply_ro_grants(ro_role text) RETURNS void
            LANGUAGE plpgsql
            SET search_path = public, pg_temp
            AS $fn$
            DECLARE
                t text;
                cols text;
                -- Nomi shunga mos ustun hech qanday ro'yxat bilan berilmaydi
                -- (`McpDatabase.SensitiveColumnPattern` bilan BIR XIL).
                sensitive text := '(key|file|photo|passport|pasport|pinfl|jshshir|token|secret|hash|screenshot|selfie|attachment|password|login)';
                allowed text[] := ARRAY[
                    'absence_requests', 'absence_types', 'attendance_days', 'award_types',
                    'candidate_notes', 'candidate_questionnaires', 'candidate_stage_entries',
                    'departments', 'directions', 'document_types', 'employee_competencies',
                    'employee_competency_languages', 'employee_flags', 'employment_events',
                    'employments', 'exit_interviews', 'incentives', 'incidents', 'legal_entities',
                    'mobile_check_ins', 'penalty_types', 'pipeline_stages', 'position_duties',
                    'positions', 'probations', 'roles', 'schedule_assignments',
                    'termination_reasons', 'vacancies', 'work_schedule_dates',
                    'work_schedule_days', 'work_schedules', 'work_sites'
                ];
                column_grants jsonb := jsonb_build_object(
                    'employees', jsonb_build_array(
                        'id', 'first_name', 'last_name', 'middle_name', 'birth_date', 'gender',
                        'phone', 'second_phone', 'address', 'speciality', 'telegram_url',
                        'criminal_record_note', 'has_criminal_record', 'direction_ids',
                        'needs_review', 'review_note', 'mehnat_uz_url', 'mobile_check_in_allowed',
                        'created_at', 'created_by', 'updated_at', 'updated_by', 'deleted_at', 'deleted_by'),
                    'candidates', jsonb_build_array(
                        'id', 'vacancy_id', 'current_stage_id', 'status', 'source', 'first_name',
                        'last_name', 'middle_name', 'birth_date', 'phone', 'address', 'speciality',
                        'note', 'closed_at', 'closing_note', 'rejection_notice_sent_at',
                        'telegram_language', 'interview_at', 'interview_notice_sent_at',
                        'next_contact_date', 'created_at', 'created_by', 'updated_at', 'updated_by',
                        'deleted_at', 'deleted_by'),
                    'employee_documents', jsonb_build_array(
                        'id', 'employee_id', 'document_type_id', 'created_at', 'created_by', 'deleted_at'),
                    'attendance_passes', jsonb_build_array(
                        'id', 'device_id', 'resolved_employee_id', 'occurred_at', 'local_date',
                        'direction', 'direction_source', 'method', 'received_at'),
                    'attendance_devices', jsonb_build_array(
                        'id', 'name', 'model', 'provider', 'direction_role', 'legal_entity_id',
                        'department_id', 'location_note', 'is_active', 'last_seen_at',
                        'last_event_at', 'deleted_at'),
                    'users', jsonb_build_array(
                        'id', 'first_name', 'last_name', 'middle_name', 'employee_id', 'is_active', 'deleted_at')
                );
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = ro_role) THEN
                    RETURN;
                END IF;

                -- Noldan: avval berilgan hamma narsa (jadval VA ustun darajasida) olinadi.
                FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' LOOP
                    EXECUTE format('REVOKE ALL ON public.%I FROM %I', t, ro_role);
                END LOOP;

                -- «Butun jadval» ham USTUN darajasida beriladi va nomi nozikka
                -- o'xshagan ustun (`sensitive`) HECH QACHON berilmaydi: kelajakdagi
                -- `absence_requests.attachment_key` yoki `mobile_check_ins.selfie_key`
                -- kabi ustun o'z-o'zidan AI'ga ochilib qolmasin.
                FOREACH t IN ARRAY allowed LOOP
                    IF to_regclass('public.' || quote_ident(t)) IS NULL THEN
                        CONTINUE;
                    END IF;

                    SELECT string_agg(quote_ident(c.column_name), ', ')
                      INTO cols
                      FROM information_schema.columns c
                     WHERE c.table_schema = 'public'
                       AND c.table_name = t
                       AND c.column_name !~* sensitive;

                    IF cols IS NOT NULL THEN
                        EXECUTE format('GRANT SELECT (%s) ON public.%I TO %I', cols, t, ro_role);
                    END IF;
                END LOOP;

                FOR t IN SELECT jsonb_object_keys(column_grants) LOOP
                    IF to_regclass('public.' || quote_ident(t)) IS NULL THEN
                        CONTINUE;
                    END IF;

                    -- Faqat haqiqatan MAVJUD ustunlar: ro'yxatdagi nom keyinchalik
                    -- o'chirilsa, GRANT butun funksiyani yiqitmasin.
                    SELECT string_agg(quote_ident(c.column_name), ', ')
                      INTO cols
                      FROM information_schema.columns c
                     WHERE c.table_schema = 'public'
                       AND c.table_name = t
                       AND c.column_name IN (SELECT jsonb_array_elements_text(column_grants -> t))
                       AND c.column_name !~* sensitive;

                    IF cols IS NOT NULL THEN
                        EXECUTE format('GRANT SELECT (%s) ON public.%I TO %I', cols, t, ro_role);
                    END IF;
                END LOOP;
            END
            $fn$;

            REVOKE ALL ON FUNCTION public.mcp_apply_ro_grants(text) FROM PUBLIC;
            """;
    }
}
