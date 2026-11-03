using Bonvi.Domain.Blanks;

namespace Bonvi.Domain.Tests.Blanks;

/// <summary>Shablon, versiya va rol biriktirish qoidalari (docs/SPEC-blankalar.md §3.1, §3.2, §3.6).</summary>
public sealed class BlankTemplateTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Yangi_shablon_faol_va_tozalangan()
    {
        var template = BlankTemplate.Create("  HR-F06 ", " Moslashtirish rejasi ", BlankSubjectKind.Employee, 6).Value;

        template.Code.ShouldBe("HR-F06");
        template.Title.ShouldBe("Moslashtirish rejasi");
        template.IsActive.ShouldBeTrue();
        template.SortOrder.ShouldBe(6);

        template.Deactivate();
        template.IsActive.ShouldBeFalse();
        template.Activate();
        template.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "Nom")]
    [InlineData("HR-F06-TOO-LONG-CODE", "Nom")]
    [InlineData("HR-F06", " ")]
    public void Notogri_shablon_rad(string code, string title)
        => BlankTemplate.Create(code, title, BlankSubjectKind.Employee).Error.ShouldBe(BlankErrors.Invalid);

    [Fact]
    public void Versiya_qoralamadan_elon_va_eskirishga()
    {
        var version = BlankTemplateVersion.Create(Guid.NewGuid(), "1.0", "{}", Sha, BlankTemplateSource.Seed).Value;
        version.Status.ShouldBe(BlankTemplateVersionStatus.Draft);

        version.Retire().Error.ShouldBe(BlankErrors.InvalidStatus, "qoralama eskirmaydi");

        var by = Guid.NewGuid();
        version.Publish(by, At).IsSuccess.ShouldBeTrue();
        version.Status.ShouldBe(BlankTemplateVersionStatus.Published);
        version.PublishedAt.ShouldBe(At);
        version.PublishedBy.ShouldBe(by);

        version.Publish(by, At).Error.ShouldBe(BlankErrors.InvalidStatus, "ikkinchi marta e'lon qilinmaydi");

        version.Retire().IsSuccess.ShouldBeTrue();
        version.Status.ShouldBe(BlankTemplateVersionStatus.Retired);
        version.Publish(by, At).Error.ShouldBe(BlankErrors.InvalidStatus, "eskirgan qayta e'lon qilinmaydi");
    }

    [Fact]
    public void Versiya_cheklovlari()
    {
        var template = Guid.NewGuid();

        BlankTemplateVersion.Create(template, "", "{}", Sha, BlankTemplateSource.Seed).Error.ShouldBe(BlankErrors.Invalid);
        BlankTemplateVersion.Create(template, "1.0", " ", Sha, BlankTemplateSource.Seed).Error.ShouldBe(BlankErrors.InvalidJson);
        BlankTemplateVersion.Create(template, "1.0", "{}", "ABC", BlankTemplateSource.Seed).Error.ShouldBe(BlankErrors.Invalid);
        BlankTemplateVersion.Create(template, "1.0", "{}", Sha.ToUpperInvariant(), BlankTemplateSource.Seed)
            .Error.ShouldBe(BlankErrors.Invalid, "hex kichik harfda — seeder solishtiruvi bir xil bo'lsin");

        var katta = "{\"x\":\"" + new string('a', BlankTemplateVersion.MaxDefinitionBytes) + "\"}";
        BlankTemplateVersion.Create(template, "1.0", katta, Sha, BlankTemplateSource.Seed).Error.ShouldBe(BlankErrors.TooLarge);
    }

    [Fact]
    public void Rol_faqat_lavozim_roliga_biriktiriladi()
    {
        var user = Guid.NewGuid();

        BlankRoleAssignment.Create(BlankActorRoles.Hr1, user).Value.RoleCode.ShouldBe("hr1");
        BlankRoleAssignment.Create(BlankActorRoles.SubjectEmployee, user).Error.ShouldBe(BlankErrors.InvalidRole);
        BlankRoleAssignment.Create("boss", user).Error.ShouldBe(BlankErrors.InvalidRole);
        BlankRoleAssignment.Create(BlankActorRoles.Director, Guid.Empty).Error.ShouldBe(BlankErrors.UserNotFound);
    }

    [Theory]
    [InlineData("hr4", true)]
    [InlineData("departmentHead", true)]
    [InlineData("field:mentor", true)]
    [InlineData("field:", false)]
    [InlineData("HR1", false)]
    public void Qadam_rollari(string role, bool known)
        => BlankActorRoles.IsKnown(role).ShouldBe(known);
}
