using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Bonvi.Api.Mcp;

/// <summary>
/// Faqat-o'qish vositalarining bir vaqtdagi chaqiruvlar chegarasi — <c>/mcp</c>
/// va tashqi API'ning <c>/full</c> yo'li uchun UMUMIY.
/// </summary>
/// <remarks>
/// <para>
/// Ikkala yo'l bitta faqat-o'qish ulanishlar hovuzidan foydalanadi
/// (<c>McpDatabase</c>, ko'pi bilan 8 ulanish). Chegara navbatsiz: parallel og'ir
/// so'rovlar yoyadigan AI bazani bo'g'ish o'rniga darhol 429 oladi.
/// </para>
/// <list type="bullet">
///   <item>bitta bo'linma (MCP ulanishi yoki kalit) — <see cref="McpLimits.ConcurrentPerGrant"/>;</item>
///   <item>butun jarayon — <see cref="McpLimits.ConcurrentGlobal"/>.</item>
/// </list>
/// <para>
/// Jarayon ichidagi holat — bitta API nusxasi sharti bilan (kirish cheklagichi kabi).
/// </para>
/// </remarks>
public sealed class McpConcurrencyGate : IDisposable
{
    private readonly SemaphoreSlim _global = new(McpLimits.ConcurrentGlobal, McpLimits.ConcurrentGlobal);
    private readonly ConcurrentDictionary<string, int> _perPartition = new(StringComparer.Ordinal);

    /// <summary>Joy bo'lsa egallaydi; <paramref name="lease"/> yopilganda bo'shatiladi.</summary>
    /// <param name="partition">Bo'linma: MCP ulanishi (grant) yoki «api-key:&lt;prefiks&gt;».</param>
    /// <param name="lease">Egallangan joy — <c>using</c> bilan.</param>
    /// <returns><c>false</c> — chegara to'lgan, chaqiruv bajarilmasligi kerak.</returns>
    public bool TryEnter(string partition, [NotNullWhen(true)] out IDisposable? lease)
    {
        ArgumentNullException.ThrowIfNull(partition);

        lease = null;

        if (_perPartition.AddOrUpdate(partition, 1, (_, count) => count + 1) > McpLimits.ConcurrentPerGrant)
        {
            Release(partition);
            return false;
        }

        if (!_global.Wait(0))
        {
            Release(partition);
            return false;
        }

        lease = new Lease(this, partition);
        return true;
    }

    /// <inheritdoc />
    public void Dispose() => _global.Dispose();

    private void Exit(string partition)
    {
        _global.Release();
        Release(partition);
    }

    private void Release(string partition)
    {
        if (_perPartition.AddOrUpdate(partition, 0, (_, count) => count - 1) <= 0)
        {
            _perPartition.TryRemove(new KeyValuePair<string, int>(partition, 0));
        }
    }

    private sealed class Lease(McpConcurrencyGate gate, string partition) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                gate.Exit(partition);
            }
        }
    }
}
