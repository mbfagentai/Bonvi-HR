using Bonvi.Application.Mcp;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Mcp;

/// <summary>
/// <see cref="IMcpGrantRevoker"/> — <c>mcp_grants</c> ga bitta UPDATE.
/// </summary>
/// <remarks>
/// <c>McpDatabase.CreateStoreContext</c> orqali, so'rovning o'z tranzaksiyasidan
/// TASHQARIDA: chaqiruvchining keyingi saqlashi yiqilsa ham ulanishlar yopiq
/// qoladi — xavfsiz tomonga xato. MCP serverda sozlanmagan bo'lsa ham ishlaydi
/// (asosiy ulanish bilan): parol keyinroq olib tashlangan bo'lishi mumkin.
/// </remarks>
public sealed class McpGrantRevoker(McpDatabase database) : IMcpGrantRevoker
{
    public async Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var now = DateTimeOffset.UtcNow;
        var by = reason.Length <= Domain.Mcp.McpGrant.MaxRevokedByLength
            ? reason
            : reason[..Domain.Mcp.McpGrant.MaxRevokedByLength];

        await using var store = database.CreateStoreContext();

        return await store.McpGrants
            .Where(grant => grant.UserId == userId && grant.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(grant => grant.RevokedAt, now)
                    .SetProperty(grant => grant.RevokedBy, by),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
