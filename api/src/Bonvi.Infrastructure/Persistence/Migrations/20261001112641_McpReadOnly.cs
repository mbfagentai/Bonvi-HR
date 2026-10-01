using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// AI ulanish (MCP): <c>mcp_*</c> jadvallari va o'qish rolining ruxsat
    /// ro'yxati — <c>mcp_apply_ro_grants(role)</c> funksiyasi
    /// (<c>docs/SPEC-mcp.md</c> §3, §4). Hech narsa O'CHIRILMAYDI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Ruxsat ro'yxati — RAD ETISH sukut bilan.</b> Funksiya avval rolning
    /// barcha jadvallardagi ruxsatini oladi, keyin faqat quyida sanalganlarni
    /// beradi. Kelajakdagi migratsiya yaratgan jadval shu yerga qo'shilmaguncha
    /// YOPIQ. <c>employees</c>, <c>candidates</c> va boshqa nozik jadvallarda
    /// ustunlar ham ro'yxat bilan beriladi: yangi ustun (masalan yana bir fayl
    /// kaliti) avtomatik ochilib qolmaydi.
    /// </para>
    /// <para>
    /// Pasport (<c>pinfl</c>, <c>passport</c>) va fayl ustunlari (<c>*_key</c>,
    /// <c>file_name</c>, hujjat izohi) hech qaysi ro'yxatda yo'q — o'qish roli
    /// ularni so'rasa PostgreSQL <c>42501</c> qaytaradi.
    /// </para>
    /// <para>
    /// Funksiyani API har ishga tushishda chaqiradi (<c>McpDatabase.ProvisionAsync</c>),
    /// ya'ni ro'yxatni o'zgartirish = funksiyani qayta yaratadigan YANGI migratsiya.
    /// </para>
    /// </remarks>
    public partial class McpReadOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mcp_audit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tool = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    arguments = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mcp_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mcp_auth_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    redirect_uri = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    code_challenge = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mcp_auth_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mcp_clients",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    redirect_uris = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mcp_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mcp_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mcp_grants", x => x.id);
                    table.ForeignKey(
                        name: "fk_mcp_grants_mcp_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "mcp_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mcp_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mcp_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_mcp_tokens_mcp_grants_grant_id",
                        column: x => x.grant_id,
                        principalTable: "mcp_grants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mcp_audit_at",
                table: "mcp_audit",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_audit_grant_id",
                table: "mcp_audit",
                column: "grant_id");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_auth_codes_code_hash",
                table: "mcp_auth_codes",
                column: "code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mcp_auth_codes_expires_at",
                table: "mcp_auth_codes",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_clients_created_at",
                table: "mcp_clients",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_grants_client_id",
                table: "mcp_grants",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_grants_user_id",
                table: "mcp_grants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_tokens_expires_at",
                table: "mcp_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_tokens_grant_id",
                table: "mcp_tokens",
                column: "grant_id");

            migrationBuilder.CreateIndex(
                name: "ix_mcp_tokens_token_hash",
                table: "mcp_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.Sql(ApplyReadOnlyGrantsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.mcp_apply_ro_grants(text);");

            migrationBuilder.DropTable(
                name: "mcp_audit");

            migrationBuilder.DropTable(
                name: "mcp_auth_codes");

            migrationBuilder.DropTable(
                name: "mcp_tokens");

            migrationBuilder.DropTable(
                name: "mcp_grants");

            migrationBuilder.DropTable(
                name: "mcp_clients");
        }

        /*
          `allowed` — butun jadval (SELECT). `employees`, `candidates`,
          `employee_documents`, `attendance_passes`, `attendance_devices`,
          `users` — faqat sanalgan ustunlar. Qolgan HAMMA jadval yopiq,
          jumladan: api_keys, audit_logs (eski/yangi qiymatlarda pasport
          bo'lishi mumkin), employee_accounts, *refresh_tokens,
          system_settings (bot tokeni), notification_states,
          attendance_identities, device_enrollments, employee_check_files,
          candidate_files, mcp_*.
        */
        private const string ApplyReadOnlyGrantsSql = """
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
