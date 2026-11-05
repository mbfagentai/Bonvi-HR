using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// AI read role: the title of a vacancy request, column by column. <c>process_cases</c> (SPEC-mening-ishlarim §12)
    /// shows a subject name, and a hire-request case's subject is a vacancy request; the table was not open to the
    /// role at all, so any list holding such a case failed with 42501.
    /// </summary>
    /// <remarks>Touches only the function; the API calls it at start-up (<c>McpDatabase.ProvisionAsync</c>).</remarks>
    public partial class JarayonMcpVakansiyaSorovi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReadOnlyGrantsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PreviousReadOnlyGrantsSql);
        }

        /*
          `mcp_apply_ro_grants` — the function of `Processes`, one difference: `vacancy_requests` in
          `column_grants`. Everything else is EXACTLY that body.
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
                        'id', 'first_name', 'last_name', 'middle_name', 'employee_id', 'is_active', 'deleted_at'),
                    -- Jarayonlar (SPEC-mening-ishlarim §12): the subject name of a hire-request case. The notes and
                    -- who asked stay closed.
                    'vacancy_requests', jsonb_build_array('id', 'title', 'deleted_at')
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

        /// <summary>For Down — the function of <c>Processes</c>, unchanged.</summary>
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
    }
}
