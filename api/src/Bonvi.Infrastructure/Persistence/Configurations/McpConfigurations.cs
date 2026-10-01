using Bonvi.Domain.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bonvi.Infrastructure.Persistence.Configurations;

/*
  ═══════════════════════════════════════════════════════════
   AI ULANISH (MCP) — besh jadval (`docs/SPEC-mcp.md` §5, §6).

   Hammasi `mcp_` bilan boshlanadi va o'qish rolining (`bonvi_mcp`)
   ruxsat ro'yxatida YO'Q: AI tokenlar, kodlar va jurnalni ko'ra olmaydi.

   `users` ga FK ATAYLAB qo'yilmagan (sababi `McpGrant` izohida).
  ═══════════════════════════════════════════════════════════
*/

public sealed class McpClientConfiguration : IEntityTypeConfiguration<McpClient>
{
    public void Configure(EntityTypeBuilder<McpClient> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("mcp_clients");
        builder.HasKey(client => client.Id);

        builder.Property(client => client.Id).HasMaxLength(McpClient.MaxIdLength);
        builder.Property(client => client.Name).HasMaxLength(McpClient.MaxNameLength).IsRequired();

        builder.PrimitiveCollection(client => client.RedirectUris)
            .HasField("_redirectUris")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        builder.HasIndex(client => client.CreatedAt);
    }
}

public sealed class McpGrantConfiguration : IEntityTypeConfiguration<McpGrant>
{
    public void Configure(EntityTypeBuilder<McpGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("mcp_grants");
        builder.HasKey(grant => grant.Id);
        builder.Property(grant => grant.Id).ValueGeneratedNever();

        builder.Property(grant => grant.ClientId).HasMaxLength(McpClient.MaxIdLength).IsRequired();
        builder.Property(grant => grant.Resource).HasMaxLength(McpClient.MaxUriLength).IsRequired();
        builder.Property(grant => grant.RevokedBy).HasMaxLength(McpGrant.MaxRevokedByLength);

        builder.HasOne<McpClient>()
            .WithMany()
            .HasForeignKey(grant => grant.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(grant => grant.UserId);
    }
}

public sealed class McpAuthCodeConfiguration : IEntityTypeConfiguration<McpAuthCode>
{
    public void Configure(EntityTypeBuilder<McpAuthCode> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("mcp_auth_codes");
        builder.HasKey(code => code.Id);
        builder.Property(code => code.Id).ValueGeneratedNever();

        // SHA-256 hex — 64 belgi.
        builder.Property(code => code.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(code => code.ClientId).HasMaxLength(McpClient.MaxIdLength).IsRequired();
        builder.Property(code => code.RedirectUri).HasMaxLength(McpClient.MaxUriLength).IsRequired();
        builder.Property(code => code.CodeChallenge).HasMaxLength(64).IsRequired();
        builder.Property(code => code.Resource).HasMaxLength(McpClient.MaxUriLength).IsRequired();

        builder.HasIndex(code => code.CodeHash).IsUnique();
        builder.HasIndex(code => code.ExpiresAt);
    }
}

public sealed class McpTokenConfiguration : IEntityTypeConfiguration<McpToken>
{
    public void Configure(EntityTypeBuilder<McpToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("mcp_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.Kind).HasMaxLength(16).IsRequired();
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();

        builder.HasOne<McpGrant>()
            .WithMany()
            .HasForeignKey(token => token.GrantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.GrantId);
        builder.HasIndex(token => token.ExpiresAt);
    }
}

public sealed class McpAuditEntryConfiguration : IEntityTypeConfiguration<McpAuditEntry>
{
    public void Configure(EntityTypeBuilder<McpAuditEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("mcp_audit");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).UseIdentityByDefaultColumn();

        builder.Property(entry => entry.UserName).HasMaxLength(McpAuditEntry.MaxNameLength).IsRequired();
        builder.Property(entry => entry.ClientId).HasMaxLength(McpClient.MaxIdLength).IsRequired();
        builder.Property(entry => entry.ClientName).HasMaxLength(McpAuditEntry.MaxNameLength).IsRequired();
        builder.Property(entry => entry.Tool).HasMaxLength(McpAuditEntry.MaxToolLength).IsRequired();
        builder.Property(entry => entry.Arguments).HasMaxLength(McpAuditEntry.MaxArgumentsLength).IsRequired();
        builder.Property(entry => entry.Outcome).HasMaxLength(16).IsRequired();
        builder.Property(entry => entry.Ip).HasMaxLength(McpAuditEntry.MaxIpLength);
        builder.Property(entry => entry.UserAgent).HasMaxLength(McpAuditEntry.MaxUserAgentLength);

        // «Oxirgi 100 ta so'rov» va kunlik tozalash — ikkalasi ham vaqt bo'yicha.
        builder.HasIndex(entry => entry.At);
        builder.HasIndex(entry => entry.GrantId);
    }
}
