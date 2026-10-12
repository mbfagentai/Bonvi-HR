namespace Bonvi.Application.Common.Auditing;

/// <summary>
/// <see cref="Abstractions.IAuditScope"/> ning ichi: amal nomi va guruh — ichma-ich qamrovlar bilan.
/// </summary>
/// <remarks>
/// HTTP (<c>HttpRequestContext</c>) va HTTP'siz (<c>SystemAuditContext</c>)
/// amalga oshirilishlar shu bitta sinfni ishlatadi: «qamrov yopilganda oldingisi
/// tiklanadi» qoidasi ikki joyda alohida yozilsa, bittasida unutilardi va
/// keyingi urug'ning qatorlari oldingi amal nomi bilan yozilib qolardi.
/// Oqimga xavfsiz EMAS va bo'lishi ham shart emas: bitta DI qamrovi (so'rov,
/// vazifa) bitta oqimda ketma-ket ishlaydi.
/// </remarks>
public sealed class AuditScopeTracker
{
    private Frame? _current;

    /// <summary>Joriy amal nomi; qamrov ochilmagan bo'lsa <c>null</c>.</summary>
    public string? Operation => _current?.Operation;

    /// <summary>Joriy guruh; qamrov ochilmagan bo'lsa <c>null</c>.</summary>
    public Guid? CorrelationId => _current?.CorrelationId;

    /// <summary>Qamrov ochilganmi.</summary>
    public bool IsActive => _current is not null;

    /// <summary>Yangi qamrov — yangi guruh bilan.</summary>
    /// <param name="operation">Amal nomi.</param>
    /// <returns>Yopilganda oldingi qamrov tiklanadi.</returns>
    public IDisposable Push(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var frame = new Frame(operation, Guid.CreateVersion7(), _current);
        _current = frame;

        return new Restore(this, frame);
    }

    private sealed record Frame(string Operation, Guid CorrelationId, Frame? Previous);

    private sealed class Restore(AuditScopeTracker owner, Frame frame) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Faqat O'Z qamrovi tepada bo'lsa — tartibsiz yopilgan ichki
            // qamrov tashqarisini «yutib» yubormasin.
            if (ReferenceEquals(owner._current, frame))
            {
                owner._current = frame.Previous;
            }
        }
    }
}
