using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Processes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "process_holidays",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_holidays", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "process_stage_bindings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    binding_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    pipeline_stage_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_stage_bindings", x => x.id);
                    table.ForeignKey(
                        name: "fk_process_stage_bindings_pipeline_stages_pipeline_stage_id",
                        column: x => x.pipeline_stage_id,
                        principalTable: "pipeline_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "process_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject_kind = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "process_template_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: false),
                    definition_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_template_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_process_template_versions_process_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "process_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    template_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    legacy = table.Column<bool>(type: "boolean", nullable: false),
                    subject_kind = table.Column<short>(type: "smallint", nullable: false),
                    subject_ref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    vacancy_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    employment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    probation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    anchor_date = table.Column<DateOnly>(type: "date", nullable: true),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    started_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_cases", x => x.id);
                    table.ForeignKey(
                        name: "fk_process_cases_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_cases_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_process_cases_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_cases_employments_employment_id",
                        column: x => x.employment_id,
                        principalTable: "employments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_cases_probations_probation_id",
                        column: x => x.probation_id,
                        principalTable: "probations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_cases_process_cases_parent_case_id",
                        column: x => x.parent_case_id,
                        principalTable: "process_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_cases_process_template_versions_template_version_id",
                        column: x => x.template_version_id,
                        principalTable: "process_template_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_cases_users_started_by",
                        column: x => x.started_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_cases_vacancy_requests_vacancy_request_id",
                        column: x => x.vacancy_request_id,
                        principalTable: "vacancy_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_case_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    order_no = table.Column<int>(type: "integer", nullable: false),
                    board_column = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    state = table.Column<short>(type: "smallint", nullable: false),
                    resolution = table.Column<short>(type: "smallint", nullable: true),
                    holder_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    holder_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    done_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    done_by = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    evidence_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    blocked_gate_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    linked_blank_submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    linked_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_case_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_process_case_steps_blank_submissions_linked_blank_submissio",
                        column: x => x.linked_blank_submission_id,
                        principalTable: "blank_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_process_case_steps_employees_holder_employee_id",
                        column: x => x.holder_employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_case_steps_process_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "process_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_process_case_steps_users_done_by",
                        column: x => x.done_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_gate_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gate_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    step_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    employment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    for_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consumed_operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_gate_overrides", x => x.id);
                    table.ForeignKey(
                        name: "fk_process_gate_overrides_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_gate_overrides_employments_employment_id",
                        column: x => x.employment_id,
                        principalTable: "employments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_gate_overrides_process_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "process_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_gate_overrides_users_for_user_id",
                        column: x => x.for_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_process_gate_overrides_users_granted_by",
                        column: x => x.granted_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_process_case_steps_case_id_step_code",
                table: "process_case_steps",
                columns: new[] { "case_id", "step_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_process_case_steps_done_by",
                table: "process_case_steps",
                column: "done_by");

            migrationBuilder.CreateIndex(
                name: "ix_process_case_steps_due_date_open",
                table: "process_case_steps",
                column: "due_date",
                filter: "state IN (2, 4)");

            migrationBuilder.CreateIndex(
                name: "ix_process_case_steps_holder_employee_id_open",
                table: "process_case_steps",
                column: "holder_employee_id",
                filter: "state IN (2, 4)");

            migrationBuilder.CreateIndex(
                name: "ix_process_case_steps_linked_blank_submission_id",
                table: "process_case_steps",
                column: "linked_blank_submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_case_steps_state_holder_role_open",
                table: "process_case_steps",
                columns: new[] { "state", "holder_role" },
                filter: "state IN (2, 4)");

            migrationBuilder.CreateIndex(
                name: "ix_process_cases_candidate_id",
                table: "process_cases",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_cases_department_id_active",
                table: "process_cases",
                column: "department_id",
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_process_cases_employee_id",
                table: "process_cases",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_cases_employment_id",
                table: "process_cases",
                column: "employment_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_cases_parent_case_id",
                table: "process_cases",
                column: "parent_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_cases_probation_id",
                table: "process_cases",
                column: "probation_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_cases_process_code_subject_ref",
                table: "process_cases",
                columns: new[] { "process_code", "subject_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_process_cases_started_by",
                table: "process_cases",
                column: "started_by");

            migrationBuilder.CreateIndex(
                name: "ix_process_cases_status_process_code",
                table: "process_cases",
                columns: new[] { "status", "process_code" });

            migrationBuilder.CreateIndex(
                name: "ix_process_cases_template_version_id",
                table: "process_cases",
                column: "template_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_cases_vacancy_request_id",
                table: "process_cases",
                column: "vacancy_request_id");

            migrationBuilder.CreateIndex(
                name: "ux_process_cases_process_code_employment_id",
                table: "process_cases",
                columns: new[] { "process_code", "employment_id" },
                unique: true,
                filter: "employment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_process_gate_overrides_candidate_id",
                table: "process_gate_overrides",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_gate_overrides_case_id",
                table: "process_gate_overrides",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_gate_overrides_employment_id",
                table: "process_gate_overrides",
                column: "employment_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_gate_overrides_for_user_id",
                table: "process_gate_overrides",
                column: "for_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_gate_overrides_gate_code_expires_at_unused",
                table: "process_gate_overrides",
                columns: new[] { "gate_code", "expires_at" },
                filter: "consumed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_process_gate_overrides_granted_at",
                table: "process_gate_overrides",
                column: "granted_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_process_gate_overrides_granted_by",
                table: "process_gate_overrides",
                column: "granted_by");

            migrationBuilder.CreateIndex(
                name: "ix_process_holidays_date",
                table: "process_holidays",
                column: "date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_process_stage_bindings_binding_code",
                table: "process_stage_bindings",
                column: "binding_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_process_stage_bindings_pipeline_stage_id",
                table: "process_stage_bindings",
                column: "pipeline_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_template_versions_template_id_version",
                table: "process_template_versions",
                columns: new[] { "template_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_process_template_versions_published",
                table: "process_template_versions",
                column: "template_id",
                unique: true,
                filter: "status = 2");

            migrationBuilder.CreateIndex(
                name: "ix_process_templates_code",
                table: "process_templates",
                column: "code",
                unique: true);

            // AI read role — the seven process_* tables join the allow list (SPEC-mening-ishlarim §12,
            // §13 H19). The API calls the function at start-up (`McpDatabase.ProvisionAsync`).
            migrationBuilder.Sql(ReadOnlyGrantsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The function back to its Blankalar body first — then the tables.
            migrationBuilder.Sql(PreviousReadOnlyGrantsSql);

            // Up writes no setting: the seeder and the settings screen do. Left behind, `processes.startedOn` would keep
            // the old go-live date for the next deploy, and every subject since then would count as new, not legacy.
            migrationBuilder.Sql("DELETE FROM system_settings WHERE key LIKE 'processes.%';");

            migrationBuilder.DropTable(
                name: "process_case_steps");

            migrationBuilder.DropTable(
                name: "process_gate_overrides");

            migrationBuilder.DropTable(
                name: "process_holidays");

            migrationBuilder.DropTable(
                name: "process_stage_bindings");

            migrationBuilder.DropTable(
                name: "process_cases");

            migrationBuilder.DropTable(
                name: "process_template_versions");

            migrationBuilder.DropTable(
                name: "process_templates");
        }

        /*
          `mcp_apply_ro_grants` — the function of `Blankalar`, one difference: the seven process_*
          tables in `allowed`. Column names matching `sensitive` stay closed as everywhere else.
          Everything else is EXACTLY that body.
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
                    'blank_submissions', 'blank_template_versions', 'blank_templates',
                    -- Jarayonlar (SPEC-mening-ishlarim §12): the MCP read tools (scope
                    -- `processes.view`) read cases, steps and the calendar.
                    'process_case_steps', 'process_cases', 'process_gate_overrides', 'process_holidays',
                    'process_stage_bindings', 'process_template_versions', 'process_templates'
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

        /// <summary>For Down — the function of <c>Blankalar</c>, unchanged.</summary>
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
    }
}
