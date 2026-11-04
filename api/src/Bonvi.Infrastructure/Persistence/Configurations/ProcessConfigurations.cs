using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Recruiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/*
  Process engine tables (docs/SPEC-mening-ishlarim.md §6). Common rules:

  1. Enums are `short` based — the columns are `smallint`.
  2. No column defaults in the model: the domain sets every value (project rule, see the head of
     `BlankConfigurations`).
  3. Column names stay outside the AI read role's hidden pattern (`mcp_apply_ro_grants`
     `sensitive`): `subject_ref`, `binding_code`, `board_column`, `evidence_sha256` — never
     `*_key`, `*hash*`, `*token*`, `*secret*`, `*file*`.
  4. `HasAuditedChange` is a transient marker read by `AuditCatalog`, never a column.
*/

/// <summary>Process templates (<c>process_templates</c>).</summary>
public sealed class ProcessTemplateConfiguration : IEntityTypeConfiguration<ProcessTemplate>
{
    public void Configure(EntityTypeBuilder<ProcessTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("process_templates");

        builder.HasKey(template => template.Id);

        builder.Ignore(template => template.DomainEvents);

        builder.Property(template => template.Code)
            .HasMaxLength(ProcessTemplate.MaxCodeLength)
            .IsRequired();

        builder.HasIndex(template => template.Code)
            .IsUnique();

        builder.Property(template => template.Title)
            .HasMaxLength(ProcessTemplate.MaxTitleLength)
            .IsRequired();

        builder.Property(template => template.Source)
            .HasMaxLength(ProcessTemplate.MaxSourceLength)
            .IsRequired();

        builder.Property(template => template.SubjectKind).IsRequired();
        builder.Property(template => template.IsActive).IsRequired();
    }
}

/// <summary>Process template versions (<c>process_template_versions</c>).</summary>
/// <remarks>At most one Published version per template — partial unique index (<c>WHERE status = 2</c>).</remarks>
public sealed class ProcessTemplateVersionConfiguration : IEntityTypeConfiguration<ProcessTemplateVersion>
{
    public void Configure(EntityTypeBuilder<ProcessTemplateVersion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("process_template_versions");

        builder.HasKey(version => version.Id);

        builder.Ignore(version => version.DomainEvents);

        builder.HasOne<ProcessTemplate>()
            .WithMany()
            .HasForeignKey(version => version.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(version => version.Version)
            .HasMaxLength(ProcessTemplateVersion.MaxVersionLength)
            .IsRequired();

        builder.Property(version => version.Status).IsRequired();

        builder.Property(version => version.Definition)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(version => version.DefinitionSha256)
            .HasMaxLength(ProcessTemplateVersion.Sha256HexLength)
            .IsFixedLength()
            .IsRequired();

        builder.HasIndex(version => new { version.TemplateId, version.Version })
            .IsUnique();

        builder.HasIndex(version => version.TemplateId)
            .IsUnique()
            .HasFilter("status = 2")
            .HasDatabaseName("ux_process_template_versions_published");
    }
}

/// <summary>Process cases (<c>process_cases</c>) and their steps.</summary>
/// <remarks>
/// <para>
/// <b>One case per subject</b>: UNIQUE (process_code, subject_ref) over all statuses — starters
/// insert with <c>ON CONFLICT DO NOTHING</c> (§7.3); and one case per employment and process
/// (partial UNIQUE, §3.2) so a candidate hire never yields two formalise cases.
/// </para>
/// <para>
/// Subject references are <c>Restrict</c> (none of them is hard-deleted; a deleted subject cancels
/// the case, §6.9). The department is a copy refreshed each pass — <c>SetNull</c>, as in blanks.
/// </para>
/// </remarks>
public sealed class ProcessCaseConfiguration : IEntityTypeConfiguration<ProcessCase>
{
    public void Configure(EntityTypeBuilder<ProcessCase> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("process_cases");

        builder.HasKey(processCase => processCase.Id);

        builder.Ignore(processCase => processCase.DomainEvents);
        builder.Ignore(processCase => processCase.HasAuditedChange);

        builder.Property(processCase => processCase.ProcessCode)
            .HasMaxLength(ProcessTemplate.MaxCodeLength)
            .IsRequired();

        builder.Property(processCase => processCase.Status).IsRequired();
        builder.Property(processCase => processCase.Outcome).HasMaxLength(ProcessCase.MaxOutcomeLength);
        builder.Property(processCase => processCase.Legacy).IsRequired();
        builder.Property(processCase => processCase.SubjectKind).IsRequired();

        builder.Property(processCase => processCase.SubjectRef)
            .HasMaxLength(ProcessCase.MaxSubjectRefLength)
            .IsRequired();

        builder.Property(processCase => processCase.StartedOn).IsRequired();
        builder.Property(processCase => processCase.CancelReason).HasMaxLength(ProcessCase.MaxCancelReasonLength);

        builder.Property(processCase => processCase.Version)
            .IsRequired()
            .IsConcurrencyToken();

        builder.Property(processCase => processCase.CreatedAt).IsRequired();
        builder.Property(processCase => processCase.UpdatedAt).IsRequired();

        // ── Relationships ───────────────────────────────────────────
        builder.HasOne<ProcessTemplateVersion>()
            .WithMany()
            .HasForeignKey(processCase => processCase.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VacancyRequest>()
            .WithMany()
            .HasForeignKey(processCase => processCase.VacancyRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(processCase => processCase.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employment>()
            .WithMany()
            .HasForeignKey(processCase => processCase.EmploymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Probation>()
            .WithMany()
            .HasForeignKey(processCase => processCase.ProbationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(processCase => processCase.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(processCase => processCase.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ProcessCase>()
            .WithMany()
            .HasForeignKey(processCase => processCase.ParentCaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(processCase => processCase.StartedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Steps are meaningless without the case — cascade, changed only through the aggregate.
        builder.HasMany(processCase => processCase.Steps)
            .WithOne()
            .HasForeignKey(step => step.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(processCase => processCase.Steps)
            .HasField("_steps")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ── Indexes (§6.8) ──────────────────────────────────────────
        builder.HasIndex(processCase => new { processCase.ProcessCode, processCase.SubjectRef })
            .IsUnique();

        builder.HasIndex(processCase => new { processCase.ProcessCode, processCase.EmploymentId })
            .IsUnique()
            .HasFilter("employment_id IS NOT NULL")
            .HasDatabaseName("ux_process_cases_process_code_employment_id");

        builder.HasIndex(processCase => new { processCase.Status, processCase.ProcessCode });
        builder.HasIndex(processCase => processCase.EmployeeId);
        builder.HasIndex(processCase => processCase.CandidateId);

        builder.HasIndex(processCase => processCase.DepartmentId)
            .HasFilter("status = 1")
            .HasDatabaseName("ix_process_cases_department_id_active");
    }
}

/// <summary>Case steps (<c>process_case_steps</c>).</summary>
/// <remarks>The three task indexes cover only open work (Active / Blocked), §6.8.</remarks>
public sealed class ProcessCaseStepConfiguration : IEntityTypeConfiguration<ProcessCaseStep>
{
    private const string OpenWorkFilter = "state IN (2, 4)";

    public void Configure(EntityTypeBuilder<ProcessCaseStep> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("process_case_steps");

        builder.HasKey(step => step.Id);

        builder.Ignore(step => step.DomainEvents);
        builder.Ignore(step => step.HasAuditedChange);
        builder.Ignore(step => step.IsClosed);
        builder.Ignore(step => step.IsFactDone);
        builder.Ignore(step => step.IsUserClosed);

        builder.Property(step => step.StepCode)
            .HasMaxLength(ProcessCaseStep.MaxStepCodeLength)
            .IsRequired();

        builder.HasIndex(step => new { step.CaseId, step.StepCode })
            .IsUnique();

        builder.Property(step => step.OrderNo).IsRequired();

        builder.Property(step => step.BoardColumn)
            .HasMaxLength(ProcessCaseStep.MaxBoardColumnLength)
            .IsRequired();

        builder.Property(step => step.State).IsRequired();
        builder.Property(step => step.HolderRole).HasMaxLength(BlankActorRoles.MaxLength);
        builder.Property(step => step.Payload).HasColumnType("jsonb");

        builder.Property(step => step.EvidenceSha256)
            .HasMaxLength(ProcessCaseStep.Sha256HexLength)
            .IsFixedLength();

        builder.Property(step => step.BlockedGateCode).HasMaxLength(ProcessCaseStep.MaxGateCodeLength);
        builder.Property(step => step.UpdatedAt).IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(step => step.HolderEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(step => step.DoneBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BlankSubmission>()
            .WithMany()
            .HasForeignKey(step => step.LinkedBlankSubmissionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(step => new { step.State, step.HolderRole })
            .HasFilter(OpenWorkFilter)
            .HasDatabaseName("ix_process_case_steps_state_holder_role_open");

        builder.HasIndex(step => step.HolderEmployeeId)
            .HasFilter(OpenWorkFilter)
            .HasDatabaseName("ix_process_case_steps_holder_employee_id_open");

        builder.HasIndex(step => step.DueDate)
            .HasFilter(OpenWorkFilter)
            .HasDatabaseName("ix_process_case_steps_due_date_open");
    }
}

/// <summary>Gate overrides (<c>process_gate_overrides</c>).</summary>
public sealed class ProcessGateOverrideConfiguration : IEntityTypeConfiguration<ProcessGateOverride>
{
    public void Configure(EntityTypeBuilder<ProcessGateOverride> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("process_gate_overrides");

        builder.HasKey(row => row.Id);

        builder.Ignore(row => row.DomainEvents);

        builder.Property(row => row.GateCode)
            .HasMaxLength(ProcessCaseStep.MaxGateCodeLength)
            .IsRequired();

        builder.Property(row => row.StepCode).HasMaxLength(ProcessCaseStep.MaxStepCodeLength);

        builder.Property(row => row.Reason)
            .HasMaxLength(ProcessGateOverride.MaxReasonLength)
            .IsRequired();

        builder.Property(row => row.GrantedAt).IsRequired();
        builder.Property(row => row.ExpiresAt).IsRequired();
        builder.Property(row => row.ConsumedOperation).HasMaxLength(ProcessGateOperations.MaxLength);

        builder.HasOne<ProcessCase>()
            .WithMany()
            .HasForeignKey(row => row.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employment>()
            .WithMany()
            .HasForeignKey(row => row.EmploymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(row => row.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(row => row.ForUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(row => row.GrantedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // The guard's lookup: usable overrides of a gate; «Kim kechikdi»: the last 30 days.
        builder.HasIndex(row => new { row.GateCode, row.ExpiresAt })
            .HasFilter("consumed_at IS NULL")
            .HasDatabaseName("ix_process_gate_overrides_gate_code_expires_at_unused");

        builder.HasIndex(row => row.GrantedAt)
            .IsDescending();

        builder.HasIndex(row => row.CaseId);
    }
}

/// <summary>Pipeline stage bindings (<c>process_stage_bindings</c>).</summary>
public sealed class ProcessStageBindingConfiguration : IEntityTypeConfiguration<ProcessStageBinding>
{
    public void Configure(EntityTypeBuilder<ProcessStageBinding> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("process_stage_bindings");

        builder.HasKey(row => row.Id);

        builder.Ignore(row => row.DomainEvents);

        builder.Property(row => row.BindingCode)
            .HasMaxLength(ProcessStageBinding.MaxCodeLength)
            .IsRequired();

        builder.HasIndex(row => row.BindingCode)
            .IsUnique();

        // A deleted stage unbinds — the step falls back to its manual close.
        builder.HasOne<PipelineStage>()
            .WithMany()
            .HasForeignKey(row => row.PipelineStageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Working-day calendar (<c>process_holidays</c>).</summary>
public sealed class ProcessHolidayConfiguration : IEntityTypeConfiguration<ProcessHoliday>
{
    public void Configure(EntityTypeBuilder<ProcessHoliday> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("process_holidays");

        builder.HasKey(row => row.Id);

        builder.Ignore(row => row.DomainEvents);

        builder.Property(row => row.Date).IsRequired();

        builder.HasIndex(row => row.Date)
            .IsUnique();

        builder.Property(row => row.Kind).IsRequired();

        builder.Property(row => row.Title)
            .HasMaxLength(ProcessHoliday.MaxTitleLength)
            .IsRequired();
    }
}
