using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Suhbat holatining saqlanishi va unutilishi.
/// </summary>
/// <remarks>
/// Yarim yozilgan ariza xotirada ABADIY qolmasligi kerak: nomzod ismini
/// yozmasdan ketib qolishi odatiy hol va bu ham xotira, ham maxfiylik
/// masalasi (<c>ConversationStore</c> izohi).
/// </remarks>
public sealed class ConversationStoreTests
{
    [Fact]
    public void Saqlangan_suhbat_qaytariladi()
    {
        var store = new ConversationStore(TimeSpan.FromMinutes(60));

        store.Save(42, new ConversationState { Step = ConversationStep.Name, Language = BotLanguage.Latin });

        var found = store.Find(42);

        found.ShouldNotBeNull();
        found.Step.ShouldBe(ConversationStep.Name);
        found.Language.ShouldBe(BotLanguage.Latin);
    }

    [Fact]
    public void Notanish_suhbat_null_qaytaradi()
    {
        new ConversationStore(TimeSpan.FromMinutes(60)).Find(7).ShouldBeNull();
    }

    [Fact]
    public void Muddati_otgan_suhbat_unutiladi()
    {
        // Nol muddat: saqlangan zahoti eskirgan hisoblanadi.
        var store = new ConversationStore(TimeSpan.Zero);

        store.Save(42, new ConversationState());

        store.Find(42).ShouldBeNull();
    }

    [Fact]
    public void Tozalash_faqat_eskirganini_oladi()
    {
        var store = new ConversationStore(TimeSpan.FromMinutes(60));

        store.Save(1, new ConversationState());
        store.Save(2, new ConversationState());

        // Hech biri eskirmagan — tozalash hech narsani olmaydi.
        store.RemoveExpired().ShouldBe(0);

        store.Find(1).ShouldNotBeNull();
        store.Find(2).ShouldNotBeNull();
    }

    [Fact]
    public void Tugagan_ariza_unutiladi()
    {
        // Yuborilgan ariza botning xotirasida QOLMAYDI: u endi HR
        // tizimida va ikkinchi nusxa faqat maxfiylik yuki bo'lardi.
        var store = new ConversationStore(TimeSpan.FromMinutes(60));

        store.Save(42, new ConversationState { Step = ConversationStep.Phone });
        store.Forget(42);

        store.Find(42).ShouldBeNull();
    }

    [Fact]
    public void Yangi_holat_eskisini_almashtiradi()
    {
        var store = new ConversationStore(TimeSpan.FromMinutes(60));

        store.Save(42, new ConversationState { Step = ConversationStep.Vacancy });
        store.Save(42, new ConversationState { Step = ConversationStep.Phone });

        store.Find(42)!.Step.ShouldBe(ConversationStep.Phone);
    }
}
