using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/// <summary><see cref="UserAttendanceScope"/> (<c>user_attendance_scopes</c>).</summary>
public sealed class UserAttendanceScopeConfiguration : IEntityTypeConfiguration<UserAttendanceScope>
{
    public void Configure(EntityTypeBuilder<UserAttendanceScope> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_attendance_scopes");

        builder.HasKey(scope => new { scope.UserId, scope.DepartmentId });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(scope => scope.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A department still assigned to someone must not disappear from under them
        // (the same rule as an employment's department).
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(scope => scope.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // The composite key serves «by user»; this one serves the FK check on the department.
        builder.HasIndex(scope => scope.DepartmentId);
    }
}
