using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Tests.Settings;

/// <summary>
/// Tizim sozlamalarining xotiradagi jadvali.
/// </summary>
/// <remarks>
/// <para>
/// Mock EMAS, haqiqiy soxta sinf — xuddi <c>FakeNotificationStates</c>
/// kabi. Sabab: bu bo'limning asosiy qoidalari YOZISH va keyingi
/// O'QISH orasidagi bog'liqlikni tekshiradi («saqlangan qiymat sukut
/// qiymatni almashtiradi», «ikkinchi yozuv yangi qator ochmaydi»).
/// Mock bilan bunday testni yozib bo'lmaydi: u faqat qaysi metod
/// chaqirilganini ko'rsatadi, natijani emas.
/// </para>
/// <para>
/// <see cref="ReadCalls"/> sanog'i kesh testlari uchun: o'quvchi bitta
/// so'rov davomida bazaga BIR MARTA murojaat qilishi kerak.
/// </para>
/// </remarks>
internal sealed class FakeSystemSettings : ISystemSettingRepository
{
    private readonly List<SystemSetting> _rows = [];

    /// <summary>Saqlangan barcha yozuvlar — tekshiruv uchun.</summary>
    public IReadOnlyList<SystemSetting> Rows => _rows;

    /// <summary>«Hammasini o'qish» necha marta chaqirilgani.</summary>
    public int ReadCalls { get; private set; }

    /// <summary>
    /// Testga tayyor qiymat yozib qo'yadi — «foydalanuvchi buni allaqachon
    /// o'zgartirgan» holatini yasaydi.
    /// </summary>
    /// <remarks>
    /// Yozuv HAQIQIY yo'ldan (<see cref="SystemSetting.Create"/>) o'tadi:
    /// test ma'lumoti domen tekshiruvidan o'tmagan bo'lsa, u hech qachon
    /// bazada bo'lishi mumkin bo'lmagan holatni tekshirardi.
    /// </remarks>
    public SystemSetting Seed(string key, string value)
    {
        var created = SystemSetting.Create(key, value);

        created.IsSuccess.ShouldBeTrue($"«{key}» = «{value}» test ma'lumoti yaroqsiz");

        _rows.Add(created.Value);
        return created.Value;
    }

    public Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        ReadCalls++;

        return Task.FromResult<IReadOnlyList<SystemSetting>>([.. _rows.OrderBy(row => row.Key, StringComparer.Ordinal)]);
    }

    public Task<SystemSetting?> FindAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_rows.Find(row => string.Equals(row.Key, key, StringComparison.Ordinal)));

    public Task<SystemSetting?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_rows.Find(row => row.Id == id));

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_rows.Exists(row => row.Id == id));

    public void Add(SystemSetting aggregate) => _rows.Add(aggregate);

    public void Remove(SystemSetting aggregate) => _rows.Remove(aggregate);
}
