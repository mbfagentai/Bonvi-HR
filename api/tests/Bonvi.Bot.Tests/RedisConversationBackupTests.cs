using System.Text;
using Bonvi.Bot.Conversation;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Suhbat nusxasi Redis'ga yozilib, qayta ishga tushgan bot uni o'qiy oladimi.
/// </summary>
/// <remarks>
/// Redis'ning o'zi emas, SHAKL tekshiriladi: 22 qadamli anketaning har bir
/// maydoni aylanib kelishi va buzilgan/yo'q Redis botni to'xtatmasligi.
/// </remarks>
public sealed class RedisConversationBackupTests
{
    [Fact]
    public void Toliq_anketa_nusxadan_ozgarishsiz_qaytadi()
    {
        var nusxa = new RedisConversationBackup(new LugatKesh(), NullLogger<RedisConversationBackup>.Instance);

        var holat = new ConversationState
        {
            Step = ConversationStep.ExpectedSalary,
            Language = BotLanguage.Russian,
            VacancyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            VacancyTitle = "Кассир",
            Name = new PersonName("Каримов", "Азиз", null),
            BirthDate = new DateOnly(1990, 10, 10),
            Address = "Риштон",
            Phone = "+998901234567",
            Education = EducationAnswer.HigherComplete,
            MaritalStatus = MaritalAnswer.Married,
            RussianLevel = RussianLevelAnswer.Advanced,
            HasDriverLicense = true,
            DriverLicenseCategory = "BC",
            PhotoFileId = "photo-1",
            PhoneTypingAllowed = true,
            UpdatedAt = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero),
        };

        nusxa.Store(42, holat, TimeSpan.FromHours(24));

        nusxa.Load(42).ShouldBe(holat);
    }

    [Fact]
    public void Sanoqlar_MATN_bolib_yoziladi()
    {
        var kesh = new LugatKesh();
        var nusxa = new RedisConversationBackup(kesh, NullLogger<RedisConversationBackup>.Instance);

        nusxa.Store(42, new ConversationState { Step = ConversationStep.Phone }, TimeSpan.FromHours(1));

        // Raqam bilan yozilsa, sanoqqa yangi qiymat qo'shilishi eski
        // nusxani jimgina boshqa qadamga aylantirardi.
        kesh.Matn("conversation:42").ShouldContain("\"Phone\"");
    }

    [Fact]
    public void Buzilgan_nusxa_yangi_suhbat_hisoblanadi()
    {
        var kesh = new LugatKesh();
        kesh.Set("conversation:42", Encoding.UTF8.GetBytes("{buzilgan"), new DistributedCacheEntryOptions());

        new RedisConversationBackup(kesh, NullLogger<RedisConversationBackup>.Instance)
            .Load(42)
            .ShouldBeNull();
    }

    [Fact]
    public void Redis_ishlamasa_bot_toxtamaydi()
    {
        var nusxa = new RedisConversationBackup(new IshlamaydiganKesh(), NullLogger<RedisConversationBackup>.Instance);
        var suhbatlar = new ConversationStore(TimeSpan.FromHours(1), nusxa);

        suhbatlar.Save(42, new ConversationState { Step = ConversationStep.Name });

        suhbatlar.Find(42).ShouldNotBeNull("xotirada davom etadi").Step.ShouldBe(ConversationStep.Name);
        Should.NotThrow(() => suhbatlar.Forget(42));
    }

    [Fact]
    public void Redis_xato_bergach_bir_muddat_chetlab_otiladi()
    {
        // Mijoz uzilgan ulanishda har bir buyruqni kutish muddati tugaguncha
        // ushlaydi — bot esa yangiliklarni birma-bir ishlaydi
        var kesh = new IshlamaydiganKesh();
        var soat = new QolSoat { Hozir = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero) };
        var nusxa = new RedisConversationBackup(kesh, NullLogger<RedisConversationBackup>.Instance, soat);

        nusxa.Store(1, new ConversationState(), TimeSpan.FromHours(1));
        nusxa.Store(2, new ConversationState(), TimeSpan.FromHours(1));
        nusxa.Load(3).ShouldBeNull();

        kesh.Chaqiruvlar.ShouldBe(1, "birinchi xatodan keyin Redis'ga murojaat qilinmaydi");

        soat.Hozir = soat.Hozir.AddSeconds(31);
        nusxa.Store(4, new ConversationState(), TimeSpan.FromHours(1));

        kesh.Chaqiruvlar.ShouldBe(2, "tanaffusdan keyin yana uriniladi");
    }

    private sealed class QolSoat : TimeProvider
    {
        public DateTimeOffset Hozir { get; set; }

        public override DateTimeOffset GetUtcNow() => Hozir;
    }

    private sealed class LugatKesh : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _qiymatlar = [];

        public string Matn(string key) => Encoding.UTF8.GetString(_qiymatlar[key]);

        public byte[]? Get(string key) => _qiymatlar.GetValueOrDefault(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _qiymatlar[key] = value;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => _qiymatlar.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class IshlamaydiganKesh : IDistributedCache
    {
        public int Chaqiruvlar { get; private set; }

        public byte[]? Get(string key)
        {
            Chaqiruvlar++;
            throw new InvalidOperationException("Redis o'chiq");
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new InvalidOperationException();

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            Chaqiruvlar++;
            throw new InvalidOperationException("Redis o'chiq");
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            => throw new InvalidOperationException();

        public void Refresh(string key) => throw new InvalidOperationException();

        public Task RefreshAsync(string key, CancellationToken token = default) => throw new InvalidOperationException();

        public void Remove(string key)
        {
            Chaqiruvlar++;
            throw new InvalidOperationException("Redis o'chiq");
        }

        public Task RemoveAsync(string key, CancellationToken token = default) => throw new InvalidOperationException();
    }
}
