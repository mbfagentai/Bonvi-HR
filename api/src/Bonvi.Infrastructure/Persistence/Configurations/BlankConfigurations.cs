using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/*
  Blankalar jadvallari (docs/SPEC-blankalar.md §3). Umumiy qoidalar:

  1. Sanoqlar `short` asosida — ustunlar `smallint` (SPEC).
  2. Ustun sukutlari (`answers '{}'`, `version 0`, `is_active true`, …) modelda YO'Q, faqat
     migratsiyada — loyiha qoidasi (`AttendanceConfigurations` boshidagi 2-band): `HasDefaultValue`
     qo'yilgan ustunga EF CLR sukutini (false, 0) yubormay qo'yadi va bazadagi sukut yutadi.
  3. Ustun nomlari AI o'qish roli qoidasiga mos (SPEC §3): AI ko'radiganlarida
     `key|file|attachment|…` yo'q (`step_code`, `actor_role`, `role_code`), ko'rmasligi
     kerakdagilarida esa bor (`*_storage_key`, `file_name`).
*/

/// <summary>Blanka shablonlari (<c>blank_templates</c>).</summary>
public sealed class BlankTemplateConfiguration : IEntityTypeConfiguration<BlankTemplate>
{
    public void Configure(EntityTypeBuilder<BlankTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("blank_templates");

        builder.HasKey(template => template.Id);

        builder.Ignore(template => template.DomainEvents);

        builder.Property(template => template.Code)
            .HasMaxLength(BlankTemplate.MaxCodeLength)
            .IsRequired();

        builder.HasIndex(template => template.Code)
            .IsUnique();

        builder.Property(template => template.Title)
            .HasMaxLength(BlankTemplate.MaxTitleLength)
            .IsRequired();

        builder.Property(template => template.SubjectKind).IsRequired();
        builder.Property(template => template.IsActive).IsRequired();
        builder.Property(template => template.SortOrder).IsRequired();
    }
}

/// <summary>Shablon versiyalari (<c>blank_template_versions</c>).</summary>
/// <remarks>
/// Shablonda bittadan ko'p e'lon qilingan versiya bo'lmasligini qisman unikal indeks kafolatlaydi
/// (<c>WHERE status = 2</c>) — seeder ikki nusxada bir vaqtda ishga tushsa ham.
/// </remarks>
public sealed class BlankTemplateVersionConfiguration : IEntityTypeConfiguration<BlankTemplateVersion>
{
    public void Configure(EntityTypeBuilder<BlankTemplateVersion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("blank_template_versions");

        builder.HasKey(version => version.Id);

        builder.Ignore(version => version.DomainEvents);

        builder.HasOne<BlankTemplate>()
            .WithMany()
            .HasForeignKey(version => version.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(version => version.Version)
            .HasMaxLength(BlankTemplateVersion.MaxVersionLength)
            .IsRequired();

        builder.Property(version => version.Status).IsRequired();

        builder.Property(version => version.Definition)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(version => version.DefinitionSha256)
            .HasMaxLength(BlankTemplateVersion.Sha256HexLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(version => version.Source).IsRequired();

        builder.HasIndex(version => new { version.TemplateId, version.Version })
            .IsUnique();

        builder.HasIndex(version => version.TemplateId)
            .IsUnique()
            .HasFilter("status = 2")
            .HasDatabaseName("ux_blank_template_versions_published");
    }
}

/// <summary>To'ldirilgan blankalar (<c>blank_submissions</c>) va ularning bolalari.</summary>
/// <remarks>
/// <para>
/// <b>Sub'yekt CHECK</b> — <see cref="BlankSubject.Matches"/> dagi qoida bazada ham: xodim
/// blankasida xodim bor va nomzod yo'q, nomzod blankasida aksincha, sub'yektsizda ikkalasi ham yo'q.
/// </para>
/// <para>
/// <c>version</c> — parallellik belgisi (<c>absence_requests</c> bilan bir xil): har bir
/// <c>UPDATE</c> <c>WHERE version = @yuklangan</c> bilan, kechikkan yozuv 0 qatorga tegadi.
/// </para>
/// </remarks>
public sealed class BlankSubmissionConfiguration : IEntityTypeConfiguration<BlankSubmission>
{
    /// <summary>Sub'yekt ustunlari shablon turiga mos (SPEC §3.3).</summary>
    public const string SubjectCheckSql =
        "(subject_kind = 1 AND employee_id IS NOT NULL AND candidate_id IS NULL) OR " +
        "(subject_kind = 2 AND candidate_id IS NOT NULL AND employee_id IS NULL) OR " +
        "(subject_kind = 3 AND employee_id IS NULL AND candidate_id IS NULL)";

    public void Configure(EntityTypeBuilder<BlankSubmission> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("blank_submissions", table =>
            table.HasCheckConstraint("ck_blank_submissions_subject", SubjectCheckSql));

        builder.HasKey(submission => submission.Id);

        builder.Ignore(submission => submission.DomainEvents);
        builder.Ignore(submission => submission.IsFinal);

        builder.Property(submission => submission.TemplateCode)
            .HasMaxLength(BlankTemplate.MaxCodeLength)
            .IsRequired();

        builder.Property(submission => submission.Status).IsRequired();
        builder.Property(submission => submission.SubjectKind).IsRequired();

        builder.Property(submission => submission.Answers).HasColumnType("jsonb").IsRequired();
        builder.Property(submission => submission.Computed).HasColumnType("jsonb").IsRequired();
        builder.Property(submission => submission.Missing).HasColumnType("jsonb").IsRequired();
        builder.Property(submission => submission.MissingCount).IsRequired();

        builder.Property(submission => submission.Version)
            .IsRequired()
            .IsConcurrencyToken();

        builder.Property(submission => submission.CancelReason)
            .HasMaxLength(BlankSubmission.MaxCancelReasonLength);

        builder.Property(submission => submission.ReturnComment)
            .HasMaxLength(BlankSubmission.MaxReturnCommentLength);

        builder.Property(submission => submission.FinalPdfStorageKey)
            .HasMaxLength(BlankSubmissionStep.MaxStorageKeyLength);

        // ── Bog'lanishlar ───────────────────────────────────────────
        builder.HasOne<BlankTemplate>()
            .WithMany()
            .HasForeignKey(submission => submission.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BlankTemplateVersion>()
            .WithMany()
            .HasForeignKey(submission => submission.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(submission => submission.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employment>()
            .WithMany()
            .HasForeignKey(submission => submission.EmploymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(submission => submission.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        // Bo'lim — faqat nusxa: bo'lim o'chirilsa blanka qoladi, bo'limi bo'shaydi.
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(submission => submission.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Qadamlar va fayllar blankasiz ma'nosiz — kaskad, faqat agregat orqali.
        builder.HasMany(submission => submission.Steps)
            .WithOne()
            .HasForeignKey(step => step.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(submission => submission.Steps)
            .HasField("_steps")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(submission => submission.Attachments)
            .WithOne()
            .HasForeignKey(attachment => attachment.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(submission => submission.Attachments)
            .HasField("_attachments")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ── Indekslar ───────────────────────────────────────────────

        // Raqam shablon ichida unikal; qoralamalarda raqam yo'q.
        builder.HasIndex(submission => new { submission.TemplateCode, submission.Number })
            .IsUnique()
            .HasFilter("number IS NOT NULL");

        builder.HasIndex(submission => submission.ClientRequestId)
            .IsUnique();

        // Ro'yxat: holat tabi + shablon filtri; «to'liq emas» filtri; «yangidan eskiga».
        builder.HasIndex(submission => new { submission.Status, submission.TemplateCode });
        builder.HasIndex(submission => new { submission.TemplateCode, submission.MissingCount });
        builder.HasIndex(submission => submission.CreatedAt)
            .IsDescending();

        builder.HasIndex(submission => submission.EmployeeId);
        builder.HasIndex(submission => submission.CandidateId);
        builder.HasIndex(submission => submission.DepartmentId);
    }
}

/// <summary>Blanka imzo qadamlari (<c>blank_submission_steps</c>).</summary>
public sealed class BlankSubmissionStepConfiguration : IEntityTypeConfiguration<BlankSubmissionStep>
{
    public void Configure(EntityTypeBuilder<BlankSubmissionStep> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("blank_submission_steps");

        builder.HasKey(step => step.Id);

        builder.Ignore(step => step.DomainEvents);
        builder.Ignore(step => step.IsDone);

        builder.Property(step => step.StepCode)
            .HasMaxLength(BlankSubmissionStep.MaxStepCodeLength)
            .IsRequired();

        builder.HasIndex(step => new { step.SubmissionId, step.StepCode })
            .IsUnique();

        builder.Property(step => step.OrderNo).IsRequired();

        builder.Property(step => step.Title)
            .HasMaxLength(BlankSubmissionStep.MaxTitleLength)
            .IsRequired();

        builder.Property(step => step.ActorRole)
            .HasMaxLength(BlankActorRoles.MaxLength)
            .IsRequired();

        builder.Property(step => step.Mode).IsRequired();
        builder.Property(step => step.State).IsRequired();

        builder.Property(step => step.Decision).HasMaxLength(BlankSubmissionStep.MaxDecisionLength);
        builder.Property(step => step.Comment).HasMaxLength(BlankSubmissionStep.MaxCommentLength);
        builder.Property(step => step.SignerName).HasMaxLength(BlankSubmissionStep.MaxSignerNameLength);
        builder.Property(step => step.SignatureStorageKey).HasMaxLength(BlankSubmissionStep.MaxStorageKeyLength);
        builder.Property(step => step.AnswersPatch).HasColumnType("jsonb");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(step => step.ActedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(step => step.SignerEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Blanka fayllari (<c>blank_attachments</c>).</summary>
public sealed class BlankAttachmentConfiguration : IEntityTypeConfiguration<BlankAttachment>
{
    public void Configure(EntityTypeBuilder<BlankAttachment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("blank_attachments");

        builder.HasKey(attachment => attachment.Id);

        builder.Ignore(attachment => attachment.DomainEvents);
        builder.Ignore(attachment => attachment.IsActive);

        builder.Property(attachment => attachment.FieldPath)
            .HasMaxLength(BlankAttachment.MaxFieldPathLength)
            .IsRequired();

        builder.Property(attachment => attachment.Kind).IsRequired();

        builder.Property(attachment => attachment.FileName)
            .HasMaxLength(BlankAttachment.MaxFileNameLength)
            .IsRequired();

        builder.Property(attachment => attachment.ContentType)
            .HasMaxLength(BlankAttachment.MaxContentTypeLength)
            .IsRequired();

        builder.Property(attachment => attachment.SizeBytes).IsRequired();

        builder.Property(attachment => attachment.ContentSha256)
            .HasMaxLength(BlankTemplateVersion.Sha256HexLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(attachment => attachment.StorageKey)
            .HasMaxLength(BlankSubmissionStep.MaxStorageKeyLength)
            .IsRequired();

        builder.Property(attachment => attachment.UploadedBy).IsRequired();
        builder.Property(attachment => attachment.UploadedAt).IsRequired();
    }
}

/// <summary>Lavozim rollarini biriktirish (<c>blank_role_assignments</c>).</summary>
public sealed class BlankRoleAssignmentConfiguration : IEntityTypeConfiguration<BlankRoleAssignment>
{
    public void Configure(EntityTypeBuilder<BlankRoleAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("blank_role_assignments");

        builder.HasKey(assignment => assignment.Id);

        builder.Ignore(assignment => assignment.DomainEvents);

        builder.Property(assignment => assignment.RoleCode)
            .HasMaxLength(BlankActorRoles.MaxLength)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(assignment => new { assignment.RoleCode, assignment.UserId })
            .IsUnique();

        builder.HasIndex(assignment => assignment.UserId);
    }
}

/// <summary>Blanka raqamlari hisoblagichi (<c>blank_number_counters</c>).</summary>
public sealed class BlankNumberCounterConfiguration : IEntityTypeConfiguration<BlankNumberCounter>
{
    public void Configure(EntityTypeBuilder<BlankNumberCounter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("blank_number_counters");

        builder.HasKey(counter => counter.TemplateCode);

        builder.Property(counter => counter.TemplateCode)
            .HasMaxLength(BlankTemplate.MaxCodeLength);

        builder.Property(counter => counter.LastNumber).IsRequired();
    }
}
