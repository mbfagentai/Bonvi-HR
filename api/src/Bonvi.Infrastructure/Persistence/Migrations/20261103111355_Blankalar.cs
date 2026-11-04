using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Blankalar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blank_number_counters",
                columns: table => new
                {
                    template_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blank_number_counters", x => x.template_code);
                });

            migrationBuilder.CreateTable(
                name: "blank_role_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blank_role_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_blank_role_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "blank_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_kind = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blank_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "blank_template_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: false),
                    definition_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    source = table.Column<short>(type: "smallint", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blank_template_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_blank_template_versions_blank_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "blank_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "blank_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    subject_kind = table.Column<short>(type: "smallint", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    employment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    answers = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    computed = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    missing = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    missing_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    return_comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    final_pdf_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blank_submissions", x => x.id);
                    table.CheckConstraint("ck_blank_submissions_subject", "(subject_kind = 1 AND employee_id IS NOT NULL AND candidate_id IS NULL) OR (subject_kind = 2 AND candidate_id IS NOT NULL AND employee_id IS NULL) OR (subject_kind = 3 AND employee_id IS NULL AND candidate_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_blank_submissions_blank_template_versions_template_version_",
                        column: x => x.template_version_id,
                        principalTable: "blank_template_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_blank_submissions_blank_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "blank_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_blank_submissions_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_blank_submissions_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_blank_submissions_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_blank_submissions_employments_employment_id",
                        column: x => x.employment_id,
                        principalTable: "employments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "blank_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    content_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blank_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_blank_attachments_blank_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "blank_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "blank_submission_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    order_no = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    mode = table.Column<short>(type: "smallint", nullable: false),
                    state = table.Column<short>(type: "smallint", nullable: false),
                    decision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    method = table.Column<short>(type: "smallint", nullable: true),
                    acted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signer_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    signature_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    answers_patch = table.Column<string>(type: "jsonb", nullable: true),
                    acted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blank_submission_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_blank_submission_steps_blank_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "blank_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_blank_submission_steps_employees_signer_employee_id",
                        column: x => x.signer_employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_blank_submission_steps_users_acted_by_user_id",
                        column: x => x.acted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_blank_attachments_submission_id",
                table: "blank_attachments",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_blank_role_assignments_role_code_user_id",
                table: "blank_role_assignments",
                columns: new[] { "role_code", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_blank_role_assignments_user_id",
                table: "blank_role_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_blank_submission_steps_acted_by_user_id",
                table: "blank_submission_steps",
                column: "acted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_blank_submission_steps_signer_employee_id",
                table: "blank_submission_steps",
                column: "signer_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_blank_submission_steps_submission_id_step_code",
                table: "blank_submission_steps",
                columns: new[] { "submission_id", "step_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_blank_submissions_candidate_id",
                table: "blank_submissions",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_blank_submissions_client_request_id",
                table: "blank_submissions",
                column: "client_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_blank_submissions_created_at",
                table: "blank_submissions",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_blank_submissions_department_id",
                table: "blank_submissions",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_blank_submissions_employee_id",
                table: "blank_submissions",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_blank_submissions_employment_id",
                table: "blank_submissions",
                column: "employment_id");

            migrationBuilder.CreateIndex(
                name: "ix_blank_submissions_status_template_code",
                table: "blank_submissions",
                columns: new[] { "status", "template_code" });

            migrationBuilder.CreateIndex(
                name: "ix_blank_submissions_template_code_missing_count",
                table: "blank_submissions",
                columns: new[] { "template_code", "missing_count" });

            migrationBuilder.CreateIndex(
                name: "ix_blank_submissions_template_code_number",
                table: "blank_submissions",
                columns: new[] { "template_code", "number" },
                unique: true,
                filter: "number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_blank_submissions_template_id",
                table: "blank_submissions",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_blank_submissions_template_version_id",
                table: "blank_submissions",
                column: "template_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_blank_template_versions_template_id_version",
                table: "blank_template_versions",
                columns: new[] { "template_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_blank_template_versions_published",
                table: "blank_template_versions",
                column: "template_id",
                unique: true,
                filter: "status = 2");

            migrationBuilder.CreateIndex(
                name: "ix_blank_templates_code",
                table: "blank_templates",
                column: "code",
                unique: true);

            // AI o'qish roli — blank_* jadvallari ruxsat ro'yxatiga (SPEC §11). Funksiyani
            // API ishga tushganda chaqiradi (`McpDatabase.ProvisionAsync`).
            migrationBuilder.Sql(ReadOnlyGrantsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Avval funksiya QayerdanBilgan holatiga — keyin jadvallar.
            migrationBuilder.Sql(PreviousReadOnlyGrantsSql);

            migrationBuilder.DropTable(
                name: "blank_attachments");

            migrationBuilder.DropTable(
                name: "blank_number_counters");

            migrationBuilder.DropTable(
                name: "blank_role_assignments");

            migrationBuilder.DropTable(
                name: "blank_submission_steps");

            migrationBuilder.DropTable(
                name: "blank_submissions");

            migrationBuilder.DropTable(
                name: "blank_template_versions");

            migrationBuilder.DropTable(
                name: "blank_templates");
        }

        /*
          `mcp_apply_ro_grants` — `QayerdanBilgan` dagi funksiya, bitta farq bilan: `allowed` ga
          oltita blank_* jadvali (SPEC-blankalar §11). `blank_number_counters` — texnik hisoblagich,
          ochilmaydi. Qolgani AYNAN o'sha.
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
                    'work_schedule_days', 'work_schedules', 'work_sites',
                    -- Blankalar (SPEC-blankalar §11). Fayl kaliti, fayl nomi va imzo
                    -- (`*_storage_key`, `file_name`) `sensitive` naqshi bilan yopiq qoladi.
                    'blank_attachments', 'blank_role_assignments', 'blank_submission_steps',
                    'blank_submissions', 'blank_template_versions', 'blank_templates'
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

        /// <summary>Down uchun — <c>QayerdanBilgan</c> dagi funksiya, o'zgarishsiz.</summary>
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
    }
}
