using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.Recruiting;

/// <summary>Nomzod va uning voronkadagi yo'li.</summary>
public sealed class CandidateTests
{
    private static readonly Guid VakansiyaId = Guid.CreateVersion7();
    private static readonly Guid AnketaId = Guid.CreateVersion7();
    private static readonly Guid KengashId = Guid.CreateVersion7();
    private static readonly DateOnly Sana = new(2026, 3, 1);

    private static Candidate Nomzod()
        => Candidate.Register(Ism(), VakansiyaId, CandidateSource.Manual).Value;

    private static FullName Ism(string familiya = "Каримов", string ism = "Азиз")
        => FullName.Create(familiya, ism, null).Value;

    // ────────────────────── Kiritish ──────────────────────

    [Fact]
    public void Vakansiyasiz_kiritilmaydi()
    {
        var natija = Candidate.Register(Ism(), Guid.Empty);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.vakansiya_yoq");
    }

    [Fact]
    public void Kelajakdagi_tugilgan_sana_qabul_qilinmaydi()
    {
        var ertaga = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var natija = Candidate.Register(Ism(), VakansiyaId, birthDate: ertaga);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.tugilgan_sana");
    }

    [Fact]
    public void Yangi_nomzod_jarayonda_boladi()
    {
        var natija = Candidate.Register(
            Ism(),
            VakansiyaId,
            CandidateSource.TelegramBot,
            phone: PhoneNumber.Create("905088888").Value,
            speciality: "Иқтисодчи");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Status.ShouldBe(CandidateStatus.InProgress);
        natija.Value.IsActive.ShouldBeTrue();
        natija.Value.Source.ShouldBe(CandidateSource.TelegramBot, "manba botdan kelganini saqlaydi");
        natija.Value.CurrentStageId.ShouldBeNull("bosqichni use-case qo'yadi, domen emas");
        natija.Value.StageEntries.ShouldBeEmpty();
    }

    // ────────────────────── Voronka ──────────────────────

    [Fact]
    public void Bosqich_yozilganda_joriy_bosqich_yangilanadi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.RecordStage(AnketaId, Sana);

        natija.IsSuccess.ShouldBeTrue();
        nomzod.CurrentStageId.ShouldBe(AnketaId);
        nomzod.StageEntries.Count.ShouldBe(1);
        nomzod.StageEntries[0].Outcome.ShouldBe(StageOutcome.Pending, "sukut — «kutilmoqda»");
    }

    [Fact]
    public void Ochiq_yozuvga_natija_yozilsa_AYNI_yozuv_toldiriladi()
    {
        // HR bosqichni oldindan ochadi («ertaga kengash»), natijani keyin
        // yozadi. Bu ikkita yozuv emas, BITTA yozuvning ikki holati — lekin
        // to'ldiriladigan yozuv ANIQ ko'rsatiladi.
        var nomzod = Nomzod();
        var ochiq = nomzod.RecordStage(KengashId, Sana).Value;

        var natija = nomzod.EditStageEntry(
            ochiq.Id, StageOutcome.Passed, Sana.AddDays(1), "Кенгаш хулосаси: маъқулланди");

        natija.IsSuccess.ShouldBeTrue();
        nomzod.StageEntries.Count.ShouldBe(1, "yangi yozuv qo'shilmaydi");
        nomzod.StageEntries[0].Outcome.ShouldBe(StageOutcome.Passed);
        nomzod.StageEntries[0].Date.ShouldBe(Sana.AddDays(1));
        nomzod.StageEntries[0].Conclusion.ShouldBe("Кенгаш хулосаси: маъқулланди");
        nomzod.StageEntries[0].UpdatedAt.ShouldNotBeNull("tuzatilgani ko'rinib turishi kerak");
    }

    [Fact]
    public void Ayni_bosqich_qayta_yozilsa_YANGI_yozuv_qoshiladi()
    {
        /*
          ⭐ Talabning o'zagi (2026-09-16): «besh marta gaplashilsa —
          beshta holat». Ilgari ikkinchi yozuv birinchisining USTIDAN
          yozilardi va HR yozgan izoh jimgina yo'qolardi.
        */
        var nomzod = Nomzod();
        nomzod.RecordStage(KengashId, Sana, StageOutcome.Pending, "Биринчи қўнғироқ");

        nomzod.RecordStage(KengashId, Sana.AddDays(3), StageOutcome.Pending, "Иккинчи қўнғироқ");

        nomzod.StageEntries.Count.ShouldBe(2, "har bir aloqa — alohida yozuv");
        nomzod.StageEntries[0].Conclusion.ShouldBe("Биринчи қўнғироқ", "eski izoh joyida qoladi");
        nomzod.StageEntries[1].Conclusion.ShouldBe("Иккинчи қўнғироқ");
    }

    [Fact]
    public void Izohsiz_qayta_yozish_eski_izohni_OCHIRMAYDI()
    {
        // Voronkada kartani sudrash va tugmani qayta bosish aynan shunday
        // so'rov yuboradi (xulosa bo'sh). Ilgari bu eski izohni NULL bilan
        // almashtirardi — ogohlantirishsiz.
        var nomzod = Nomzod();
        nomzod.RecordStage(KengashId, Sana, StageOutcome.Pending, "Телефон рақами нотўғри");

        nomzod.RecordStage(KengashId, Sana);

        nomzod.StageEntries[0].Conclusion.ShouldBe("Телефон рақами нотўғри");
    }

    [Fact]
    public void Tuzatish_yozuvning_lentadagi_joyini_ozgartirmaydi()
    {
        // Lenta `RecordedAt` bo'yicha saralanadi: u tuzatishda o'zgarsa,
        // eski qator tarixning tepasiga sakrab chiqardi.
        var nomzod = Nomzod();
        var birinchi = nomzod.RecordStage(AnketaId, Sana).Value;
        nomzod.RecordStage(KengashId, Sana.AddDays(1));

        var yozilgan = birinchi.RecordedAt;
        nomzod.EditStageEntry(birinchi.Id, StageOutcome.Passed, Sana, "Анкета тўлиқ");

        birinchi.RecordedAt.ShouldBe(yozilgan, "kiritilgan vaqt o'zgarmaydi");
        nomzod.CurrentStageId.ShouldBe(KengashId, "tuzatish nomzodni voronkada SURMAYDI");
    }

    [Fact]
    public void Notogri_yozuv_tuzatilmaydi()
    {
        var nomzod = Nomzod();
        nomzod.RecordStage(AnketaId, Sana);

        var natija = nomzod.EditStageEntry(
            Guid.CreateVersion7(), StageOutcome.Passed, Sana, null);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich_yozuvi.topilmadi");
    }

    [Fact]
    public void Voronkadan_chiqqan_nomzodning_yozuvi_ham_tuzatiladi()
    {
        // Rad etilgan nomzodning tarixidagi xato ham tuzatilishi kerak:
        // yangi bosqich yozish taqiqlanadi, TUZATISH esa yo'q.
        var nomzod = Nomzod();
        var yozuv = nomzod.RecordStage(KengashId, Sana).Value;
        nomzod.Reject(Sana.AddDays(1));

        var natija = nomzod.EditStageEntry(
            yozuv.Id, StageOutcome.Failed, Sana, "Кенгаш рад этди");

        natija.IsSuccess.ShouldBeTrue();
        nomzod.StageEntries[0].Conclusion.ShouldBe("Кенгаш рад этди");
        nomzod.Status.ShouldBe(CandidateStatus.Rejected, "holat o'zgarmaydi");
    }

    [Fact]
    public void Boshqa_bosqich_yozilsa_yangi_yozuv_qoshiladi()
    {
        var nomzod = Nomzod();
        nomzod.RecordStage(AnketaId, Sana, StageOutcome.Passed);

        nomzod.RecordStage(KengashId, Sana.AddDays(2));

        nomzod.StageEntries.Count.ShouldBe(2);
        nomzod.CurrentStageId.ShouldBe(KengashId);
    }

    [Fact]
    public void Yakunlangan_bosqich_takrorlansa_ikkinchi_yozuv_qoshiladi()
    {
        // Qayta suhbat — haqiqiy hol va u tarixda alohida fakt bo'lishi kerak.
        var nomzod = Nomzod();
        nomzod.RecordStage(KengashId, Sana, StageOutcome.Failed, "Биринчи кенгаш: рад");

        nomzod.RecordStage(KengashId, Sana.AddDays(30), StageOutcome.Passed, "Иккинчи кенгаш: маъқул");

        nomzod.StageEntries.Count.ShouldBe(2);
        nomzod.StageEntries[0].Outcome.ShouldBe(StageOutcome.Failed, "eski xulosa qayta yozilmaydi");
        nomzod.StageEntries[1].Outcome.ShouldBe(StageOutcome.Passed);
    }

    [Fact]
    public void Voronka_orqaga_qaytishga_ruxsat_beradi()
    {
        // Bosqichlar tartibi BOR, lekin qat'iy ketma-ketlik ATAYLAB
        // majburlanmaydi: amalda HR bosqichni o'tkazib yuboradi yoki
        // nomzodni orqaga qaytaradi.
        var nomzod = Nomzod();
        nomzod.RecordStage(KengashId, Sana, StageOutcome.Passed);

        var natija = nomzod.RecordStage(AnketaId, Sana.AddDays(1));

        natija.IsSuccess.ShouldBeTrue();
        nomzod.CurrentStageId.ShouldBe(AnketaId);
    }

    [Fact]
    public void Bosqichsiz_yozuv_qabul_qilinmaydi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.RecordStage(Guid.Empty, Sana);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich_yozuvi.bosqich_yoq");
    }

    // ────────────────────── Voronkadan chiqish ──────────────────────

    [Fact]
    public void Rad_etilgan_nomzod_voronkada_faol_qolmaydi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.Reject(Sana, "Тажрибаси етарли эмас");

        natija.IsSuccess.ShouldBeTrue();
        nomzod.Status.ShouldBe(CandidateStatus.Rejected);
        nomzod.IsActive.ShouldBeFalse();
        nomzod.ClosedAt.ShouldBe(Sana);
        nomzod.ClosingNote.ShouldBe("Тажрибаси етарли эмас");
    }

    [Fact]
    public void Oz_voz_kechish_rad_etishdan_ALOHIDA_holat()
    {
        var nomzod = Nomzod();

        nomzod.Withdraw(Sana);

        nomzod.Status.ShouldBe(
            CandidateStatus.Withdrawn,
            "«biz rad etdik» va «u ketib qoldi» hisobotda boshqa-boshqa ma'no beradi");
    }

    [Fact]
    public void Ishga_olingan_nomzod_voronkadan_chiqadi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.MarkHired(Sana);

        natija.IsSuccess.ShouldBeTrue();
        nomzod.Status.ShouldBe(CandidateStatus.Hired);
        nomzod.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Ikki_marta_ishga_olinmaydi()
    {
        var nomzod = Nomzod();
        nomzod.MarkHired(Sana);

        var natija = nomzod.MarkHired(Sana.AddDays(1));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
    }

    [Fact]
    public void Rad_etilgan_nomzod_ishga_olinmaydi()
    {
        var nomzod = Nomzod();
        nomzod.Reject(Sana);

        var natija = nomzod.MarkHired(Sana.AddDays(1));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
        nomzod.Status.ShouldBe(CandidateStatus.Rejected, "holat o'zgarmaydi");
    }

    [Fact]
    public void Voronkadan_chiqqan_nomzodga_bosqich_yozilmaydi()
    {
        var nomzod = Nomzod();
        nomzod.Reject(Sana);

        var natija = nomzod.RecordStage(KengashId, Sana.AddDays(1));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
    }

    [Fact]
    public void Voronkadan_chiqqan_nomzod_kochirilmaydi()
    {
        var nomzod = Nomzod();
        nomzod.MarkHired(Sana);

        var natija = nomzod.Reassign(Guid.CreateVersion7());

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
    }

    [Fact]
    public void Kochirishda_bosqichlar_tarixi_saqlanadi()
    {
        var nomzod = Nomzod();
        nomzod.RecordStage(AnketaId, Sana, StageOutcome.Passed);
        var yangiVakansiya = Guid.CreateVersion7();

        var natija = nomzod.Reassign(yangiVakansiya);

        natija.IsSuccess.ShouldBeTrue();
        nomzod.VacancyId.ShouldBe(yangiVakansiya);
        nomzod.StageEntries.Count.ShouldBe(1, "telefon suhbatini qaytadan o'tkazish ma'nosiz");
        nomzod.CurrentStageId.ShouldBe(AnketaId);
    }

    // ────────────────────── Rezerv ──────────────────────

    [Fact]
    public void Rezervga_otgan_nomzod_voronkada_faol_qolmaydi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.MoveToReserve(Sana, "Ҳозир ўрин йўқ");

        natija.IsSuccess.ShouldBeTrue();
        nomzod.Status.ShouldBe(CandidateStatus.Reserve);
        nomzod.IsActive.ShouldBeFalse("rezervdagi nomzod voronka taxtasida turmasligi kerak");
        nomzod.ClosedAt.ShouldBe(Sana);
        nomzod.ClosingNote.ShouldBe("Ҳозир ўрин йўқ");
    }

    [Fact]
    public void Rezerv_rad_etishdan_ALOHIDA_holat()
    {
        var nomzod = Nomzod();

        nomzod.MoveToReserve(Sana);

        nomzod.Status.ShouldBe(
            CandidateStatus.Reserve,
            "«бизга тўғри келмади» ва «яхши одам, лекин ўрин йўқ» — бошқа-бошқа қарор");
    }

    [Fact]
    public void Voronkadan_chiqqan_nomzod_rezervga_otkazilmaydi()
    {
        // Rezerv ham YAKUN: rad etilgan nomzodni «aslida rezerv edi» deb
        // o'zgartirish qarorni keyinchalik qayta yozish bo'lardi.
        var nomzod = Nomzod();
        nomzod.Reject(Sana);

        var natija = nomzod.MoveToReserve(Sana.AddDays(1));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.jarayonda_emas");
        nomzod.Status.ShouldBe(CandidateStatus.Rejected, "holat o'zgarmaydi");
    }

    [Fact]
    public void Rezervdan_qaytgan_nomzod_yana_voronkada_boladi()
    {
        var nomzod = Nomzod();
        nomzod.RecordStage(KengashId, Sana, StageOutcome.Passed);
        nomzod.MoveToReserve(Sana.AddDays(1), "Ҳозир ўрин йўқ");

        var natija = nomzod.ReturnToFunnel();

        natija.IsSuccess.ShouldBeTrue();
        nomzod.Status.ShouldBe(CandidateStatus.InProgress);
        nomzod.IsActive.ShouldBeTrue();
        nomzod.ClosedAt.ShouldBeNull("nomzod endi voronkadan chiqqan emas");
        nomzod.ClosingNote.ShouldBeNull();
        nomzod.CurrentStageId.ShouldBe(KengashId, "qayerda to'xtagan bo'lsa, o'sha yerdan davom etadi");
        nomzod.StageEntries.Count.ShouldBe(1, "bosqichlar tarixi saqlanadi");
    }

    [Fact]
    public void Rezervdan_qaytgan_nomzod_yana_rezervga_otkaziladi()
    {
        // Voronkaga qaytgan nomzod — oddiy jarayondagi nomzod: unga
        // barcha yakunlar, shu jumladan rezerv, yana ochiq.
        var nomzod = Nomzod();
        nomzod.MoveToReserve(Sana);
        nomzod.ReturnToFunnel();

        var natija = nomzod.MoveToReserve(Sana.AddDays(30), "Кейинги мавсумга");

        natija.IsSuccess.ShouldBeTrue();
        nomzod.Status.ShouldBe(CandidateStatus.Reserve);
        nomzod.ClosingNote.ShouldBe("Кейинги мавсумга");
    }

    [Fact]
    public void Ishga_olingan_nomzod_voronkaga_QAYTARILMAYDI()
    {
        // ⭐ Eng muhim to'siq: «yopishni bekor qilish» degan umumiy amal
        // yo'q. Bo'lganida, ishga olingan odam bir vaqtda ham xodim, ham
        // faol nomzod bo'lib qolardi.
        var nomzod = Nomzod();
        nomzod.MarkHired(Sana);

        var natija = nomzod.ReturnToFunnel();

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.qaytarilmaydi");
        nomzod.Status.ShouldBe(CandidateStatus.Hired, "holat o'zgarmaydi");
    }

    [Fact]
    public void Rad_etilgan_nomzod_SABAB_bilan_qaytariladi()
    {
        /*
          ⭐ Foydalanuvchi talabi (2026-09-16): rad etilgan odam ko'pincha
          o'zi qayta aloqaga chiqadi. Uni noldan kiritish butun tarixni,
          anketani va fayllarni yo'qotardi.
        */
        var nomzod = Nomzod();
        nomzod.RecordStage(KengashId, Sana, StageOutcome.Failed);
        nomzod.Reject(Sana, "Тажрибаси етарли эмас");

        var natija = nomzod.ReturnToFunnel("Ўзи қайта мурожаат қилди, тажриба орттирган");

        natija.IsSuccess.ShouldBeTrue();
        nomzod.Status.ShouldBe(CandidateStatus.InProgress);
        nomzod.ClosedAt.ShouldBeNull();
        nomzod.ClosingNote.ShouldBeNull("chiqish sababi endi o'rinsiz");
        nomzod.CurrentStageId.ShouldBe(KengashId, "qayerda to'xtagan bo'lsa, o'sha yerdan");
        nomzod.StageEntries.Count.ShouldBe(1, "bosqichlar tarixi saqlanadi");

        /*
          ⭐ Ikkala sabab ham LENTADA qoladi: rad etishniki ham,
          qaytarishniki ham. `ClosingNote` tozalanadi — «nega rad
          etilgan edi» degan savolning javobi faqat shu yerda saqlanadi.
        */
        nomzod.Notes.Count.ShouldBe(2);
        nomzod.Notes[0].Kind.ShouldBe(CandidateNoteKind.Rejected);
        nomzod.Notes[0].Text.ShouldBe("Тажрибаси етарли эмас");
        nomzod.Notes[1].Kind.ShouldBe(CandidateNoteKind.ReturnToFunnel);
        nomzod.Notes[1].Text.ShouldBe("Ўзи қайта мурожаат қилди, тажриба орттирган");
    }

    [Fact]
    public void Rad_etilganni_SABABSIZ_qaytarib_bolmaydi()
    {
        // Qarorni bekor qilyapmiz — «nega» degan savol albatta tug'iladi.
        var nomzod = Nomzod();
        nomzod.Reject(Sana, "Тажрибаси етарли эмас");

        var natija = nomzod.ReturnToFunnel("   ");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.qaytarish_sababi_yoq");
        nomzod.Status.ShouldBe(CandidateStatus.Rejected, "holat o'zgarmaydi");
    }

    [Fact]
    public void Voz_kechgan_nomzod_ham_qaytariladi()
    {
        var nomzod = Nomzod();
        nomzod.Withdraw(Sana, "Бошқа жойга ишга кирди");

        var natija = nomzod.ReturnToFunnel("Ўша иш тўғри келмабди");

        natija.IsSuccess.ShouldBeTrue();
        nomzod.Status.ShouldBe(CandidateStatus.InProgress);
        nomzod.Notes[0].Kind.ShouldBe(CandidateNoteKind.Withdrawn);
        nomzod.Notes[1].Kind.ShouldBe(CandidateNoteKind.ReturnToFunnel);
    }

    [Fact]
    public void Qaytarilgan_nomzodga_rad_xabari_QAYTA_yuboriladi()
    {
        /*
          Belgi tozalanmasa, ikkinchi marta rad etilgan nomzod javobsiz
          qolardi: `MarkRejectionNoticeSent` dagi `??=` uni «allaqachon
          xabardor» deb hisoblardi.
        */
        var nomzod = Nomzod();
        nomzod.Reject(Sana, "Ўрин йўқ");
        nomzod.MarkRejectionNoticeSent(DateTimeOffset.UtcNow);

        nomzod.ReturnToFunnel("Ўрин бўшади").IsSuccess.ShouldBeTrue();

        nomzod.RejectionNoticeSentAt.ShouldBeNull();
    }

    [Fact]
    public void Rezervdan_qaytarishda_sabab_SHART_emas()
    {
        // Rezervning butun ma'nosi «o'rin bo'shasa chaqiramiz» — qaytish
        // o'sha holatning tabiiy yakuni va tushuntirish talab qilmaydi.
        var nomzod = Nomzod();
        nomzod.MoveToReserve(Sana);

        var natija = nomzod.ReturnToFunnel();

        natija.IsSuccess.ShouldBeTrue();
        nomzod.Notes.ShouldBeEmpty("sababsiz qaytarish lentaga qator yozmaydi");
    }

    [Fact]
    public void Jarayondagi_nomzod_voronkaga_QAYTARILMAYDI()
    {
        // U allaqachon voronkada — bu amal uchun manba yo'q.
        var nomzod = Nomzod();

        var natija = nomzod.ReturnToFunnel();

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.qaytarilmaydi");
    }

    // ────────────────────── Fayllar ──────────────────────

    [Fact]
    public void Fayl_biriktiriladi_va_olib_tashlanadi()
    {
        var nomzod = Nomzod();

        var natija = nomzod.AttachFile("kalit-1", "C:\\Users\\hr\\rezyume.pdf", "application/pdf", 1024);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.FileKey.ShouldBe("kalit-1");
        natija.Value.FileName.ShouldBe("rezyume.pdf", "mijoz yuborgan yo'l qismi tashlanadi");
        natija.Value.ContentType.ShouldBe("application/pdf");
        natija.Value.SizeBytes.ShouldBe(1024, "bazaga DISKKA tushgan hajm yoziladi");
        nomzod.Files.Count.ShouldBe(1);
        nomzod.Files[0].ShouldBeSameAs(natija.Value, "yozuv agregat ichida turadi");

        var olib = nomzod.RemoveFile(natija.Value.Id);

        olib.IsSuccess.ShouldBeTrue();
        olib.Value.ShouldBe("kalit-1", "chaqiruvchi diskdagi baytni shu kalit bilan o'chiradi");
        nomzod.Files.ShouldBeEmpty();
    }

    [Fact]
    public void Fayl_nomi_bosh_bolsa_ZAXIRA_nom_qoyiladi()
    {
        // Migratsiyada ko'chgan eski rezyumelar ham aynan shu nomni oldi —
        // ikkalasi kartada bir xil ko'rinishi kerak.
        var nomzod = Nomzod();

        var natija = nomzod.AttachFile("kalit-1", "   ", "application/pdf", 1024);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.FileName.ShouldBe("rezyume");
    }

    [Theory]
    [InlineData("  ", "application/pdf", 1024)]
    [InlineData("kalit-1", "   ", 1024)]
    [InlineData("kalit-1", "application/pdf", 0)]
    [InlineData("kalit-1", "application/pdf", -1)]
    public void Notogri_qiymat_bilan_fayl_biriktirilmaydi(string kalit, string tur, long hajm)
    {
        var nomzod = Nomzod();

        var natija = nomzod.AttachFile(kalit, "rezyume.pdf", tur, hajm);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.rezyume_bosh");
        nomzod.Files.ShouldBeEmpty("rad etilgan fayl yozuvga tushmaydi");
    }

    [Fact]
    public void Ontadan_ortiq_fayl_biriktirilmaydi()
    {
        // Pasport, diplom va mehnat daftarchasi uch-to'rt varaq bo'lib
        // keladi — o'nta o'rin shu uchun. O'n birinchisi esa ANIQ xato
        // beradi, jimgina tashlab yuborilmaydi.
        var nomzod = Nomzod();

        for (var i = 1; i <= Candidate.MaxFiles; i++)
        {
            nomzod
                .AttachFile($"kalit-{i}", $"varaq-{i}.jpg", "image/jpeg", 1024)
                .IsSuccess.ShouldBeTrue($"{i}-fayl sig'ishi kerak");
        }

        var ortiqcha = nomzod.AttachFile("kalit-11", "varaq-11.jpg", "image/jpeg", 1024);

        ortiqcha.IsFailure.ShouldBeTrue();
        ortiqcha.Error.Code.ShouldBe("nomzod.rezyume_kop");
        nomzod.Files.Count.ShouldBe(Candidate.MaxFiles);
    }

    [Fact]
    public void Chegaraga_TENG_kelgan_fayl_sigadi_oshgani_sigmaydi()
    {
        // Diskni haqiqatan unga tushgan narsa to'ldiradi — chegara
        // SAQLANGAN hajmlar yig'indisiga qo'yiladi.
        var nomzod = Nomzod();

        nomzod
            .AttachFile("kalit-1", "pasport.jpg", "image/jpeg", Candidate.MaxFilesTotalBytes - 1024)
            .IsSuccess.ShouldBeTrue();

        nomzod
            .AttachFile("kalit-2", "diplom.jpg", "image/jpeg", 1024)
            .IsSuccess.ShouldBeTrue("chegaraga TENG kelgan fayl qabul qilinadi");

        var oshgan = nomzod.AttachFile("kalit-3", "yana.jpg", "image/jpeg", 1);

        oshgan.IsFailure.ShouldBeTrue();
        oshgan.Error.Code.ShouldBe("nomzod.rezyume_joy");
        nomzod.Files.Count.ShouldBe(2);
        nomzod.Files.Sum(fayl => fayl.SizeBytes).ShouldBe(Candidate.MaxFilesTotalBytes);
    }

    [Fact]
    public void Voronkadan_chiqqan_nomzodga_ham_fayl_biriktiriladi()
    {
        // ⭐ Bosqich yozishdan ATAYLAB farq qiladi: HR diplom nusxasini rad
        //    etilgan nomzodga ertasi kuni ham yuklaydi va bunga to'sqinlik
        //    qilish faylni tizimdan tashqariga surib qo'yardi.
        var nomzod = Nomzod();
        nomzod.Reject(Sana, "Тажрибаси етарли эмас");

        var natija = nomzod.AttachFile("kalit-1", "diplom.pdf", "application/pdf", 2048);

        natija.IsSuccess.ShouldBeTrue();
        nomzod.Files.Count.ShouldBe(1);
        nomzod.Status.ShouldBe(CandidateStatus.Rejected, "holat o'zgarmaydi");
    }

    [Fact]
    public void Nomavjud_fayl_olib_tashlanmaydi()
    {
        // Amal IDEMPOTENT emas: chaqiruvchi ANIQ bir qatorni nomlagan va
        // jimgina «bajarildi» deyish allaqachon yo'q faylni ko'rsatib
        // turgan eskirgan ekranni yashirardi.
        var nomzod = Nomzod();
        nomzod.AttachFile("kalit-1", "rezyume.pdf", "application/pdf", 1024);

        var natija = nomzod.RemoveFile(Guid.CreateVersion7());

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("nomzod.rezyume_topilmadi");
        nomzod.Files.Count.ShouldBe(1, "boshqa fayl tegilmaydi");
    }

    [Fact]
    public void Fayl_olib_tashlansa_ORIN_boshaydi()
    {
        var nomzod = Nomzod();

        for (var i = 1; i <= Candidate.MaxFiles; i++)
        {
            nomzod.AttachFile($"kalit-{i}", $"varaq-{i}.jpg", "image/jpeg", 1024);
        }

        nomzod.AttachFile("kalit-11", "varaq-11.jpg", "image/jpeg", 1024)
            .Error.Code.ShouldBe("nomzod.rezyume_kop");

        nomzod.RemoveFile(nomzod.Files[0].Id).IsSuccess.ShouldBeTrue();

        var yana = nomzod.AttachFile("kalit-11", "varaq-11.jpg", "image/jpeg", 1024);

        yana.IsSuccess.ShouldBeTrue("bo'shagan o'rin darhol ishlatiladi");
        nomzod.Files.Count.ShouldBe(Candidate.MaxFiles);
    }

    /// <summary>Eski BITTA rezyume yo'li qaytib kelmasligi kerak.</summary>
    /// <remarks>
    /// To'rt ustun (<c>resume_file_key</c> va uning qardoshlari) migratsiyada
    /// O'CHIRILDI. Metod yoki xossa qaytib kelsa, u hech qanday ustunga
    /// yozmaydigan o'lik kod bo'lardi va uni faqat shu tekshiruv ushlaydi:
    /// kompilyator eski nomni qayta qo'shishga qarshilik qilmaydi.
    /// </remarks>
    [Fact]
    public void Eski_rezyume_azolari_QAYTIB_kelmaydi()
    {
        var azolar = typeof(Candidate)
            .GetMembers()
            .Select(azo => azo.Name)
            .ToList();

        azolar.ShouldNotContain("AttachResume");
        azolar.ShouldNotContain("RemoveResume");
        azolar.ShouldNotContain("ResumeFileKey");
        azolar.ShouldNotContain("ResumeFileName");
        azolar.ShouldNotContain("ResumeContentType");
        azolar.ShouldNotContain("ResumeSizeBytes");
    }

    // ────────────────────── Keyingi aloqa sanasi ──────────────────────

    [Fact]
    public void Keyingi_aloqa_sanasi_qoyiladi_va_tozalanadi()
    {
        var nomzod = Nomzod();
        nomzod.NextContactDate.ShouldBeNull("yangi nomzodda reja yo'q");

        nomzod.SetNextContactDate(Sana.AddDays(5));
        nomzod.NextContactDate.ShouldBe(Sana.AddDays(5));

        nomzod.SetNextContactDate(null);
        nomzod.NextContactDate.ShouldBeNull("null — TOZALASH");
    }

    [Fact]
    public void Otgan_aloqa_sanasi_QABUL_qilinadi()
    {
        // Maydonning butun maqsadi «bugun yoki muddati o'tgan» ro'yxati —
        // ya'ni o'tgan sana ODATIY holat, xato emas. Suhbat vaqtidagi
        // «kechagi kun — xato» qoidasini bu yerga ko'chirib bo'lmaydi.
        var nomzod = Nomzod();
        var otgan = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);

        nomzod.SetNextContactDate(otgan);

        nomzod.NextContactDate.ShouldBe(otgan);
    }

    [Fact]
    public void Rad_etish_keyingi_aloqa_sanasini_TOZALAYDI()
    {
        // Aks holda rad etilgan nomzod «bugun bog'lanish kerak» ro'yxatida
        // abadiy qolib ketardi va HR ro'yxatga umuman ishonmay qo'yardi.
        var nomzod = Nomzod();
        nomzod.SetNextContactDate(Sana.AddDays(3));

        nomzod.Reject(Sana).IsSuccess.ShouldBeTrue();

        nomzod.NextContactDate.ShouldBeNull();
    }

    [Fact]
    public void Voz_kechish_keyingi_aloqa_sanasini_TOZALAYDI()
    {
        var nomzod = Nomzod();
        nomzod.SetNextContactDate(Sana.AddDays(3));

        nomzod.Withdraw(Sana).IsSuccess.ShouldBeTrue();

        nomzod.NextContactDate.ShouldBeNull();
    }

    [Fact]
    public void Ishga_olish_keyingi_aloqa_sanasini_TOZALAYDI()
    {
        var nomzod = Nomzod();
        nomzod.SetNextContactDate(Sana.AddDays(3));

        nomzod.MarkHired(Sana).IsSuccess.ShouldBeTrue();

        nomzod.NextContactDate.ShouldBeNull();
    }

    [Fact]
    public void Rezerv_keyingi_aloqa_sanasini_SAQLAYDI()
    {
        // ⭐ Yakunlar orasida YAGONA istisno: rezerv chiqish emas,
        //    KECHIKTIRISH («o'rin bo'shasa chaqiramiz») — qo'ng'iroq rejasi
        //    aynan o'sha yerda o'rinli.
        var nomzod = Nomzod();
        nomzod.SetNextContactDate(Sana.AddDays(3));

        nomzod.MoveToReserve(Sana, "Ҳозир ўрин йўқ").IsSuccess.ShouldBeTrue();

        nomzod.NextContactDate.ShouldBe(Sana.AddDays(3));
        nomzod.Status.ShouldBe(CandidateStatus.Reserve);
    }

    [Fact]
    public void Rezervga_otkazishda_yangi_aloqa_sanasi_qoyiladi()
    {
        var nomzod = Nomzod();
        nomzod.SetNextContactDate(Sana.AddDays(3));

        nomzod.MoveToReserve(Sana, "Кейинги мавсумга", Sana.AddDays(60)).IsSuccess.ShouldBeTrue();

        nomzod.NextContactDate.ShouldBe(Sana.AddDays(60));
    }

    [Fact]
    public void Rezervdan_qaytarish_aloqa_sanasiga_TEGMAYDI()
    {
        var nomzod = Nomzod();
        nomzod.MoveToReserve(Sana, "Ҳозир ўрин йўқ", Sana.AddDays(10));

        nomzod.ReturnToFunnel().IsSuccess.ShouldBeTrue();

        nomzod.ClosedAt.ShouldBeNull("chiqish fakti yo'q");
        nomzod.ClosingNote.ShouldBeNull();
        nomzod.NextContactDate.ShouldBe(
            Sana.AddDays(10),
            "«nega chiqqan» tozalanadi, «keyin nima qilamiz» esa qoladi");
    }

    [Fact]
    public void Rezervdan_qaytarishda_yangi_aloqa_sanasi_qoyiladi()
    {
        var nomzod = Nomzod();
        nomzod.MoveToReserve(Sana, nextContactDate: Sana.AddDays(10));

        nomzod.ReturnToFunnel(nextContactDate: Sana.AddDays(2)).IsSuccess.ShouldBeTrue();

        nomzod.NextContactDate.ShouldBe(Sana.AddDays(2));
    }

    // ────────────────────── Amal izohlari ──────────────────────

    [Fact]
    public void Izoh_turi_bilan_saqlanadi_va_matni_tozalanadi()
    {
        var nomzod = Nomzod();
        var kim = Guid.CreateVersion7();

        var izoh = nomzod.AddNote(
            CandidateNoteKind.Reassign, "  Қўнғироқ қилдим, 5-6 кунда келади  ", kim);

        izoh.Kind.ShouldBe(CandidateNoteKind.Reassign);
        izoh.Text.ShouldBe("Қўнғироқ қилдим, 5-6 кунда келади", "matn tozalanadi");
        izoh.CreatedBy.ShouldBe(kim);
        izoh.CandidateId.ShouldBe(nomzod.Id);
        nomzod.Notes.Count.ShouldBe(1);
    }

    [Fact]
    public void Izohlar_KETMA_KET_yigiladi()
    {
        var nomzod = Nomzod();

        nomzod.AddNote(CandidateNoteKind.Reassign, "Бошқа вакансияга");
        nomzod.AddNote(CandidateNoteKind.ReturnToFunnel, "Ўрин бўшади");

        nomzod.Notes.Count.ShouldBe(2);
        nomzod.Notes[0].Kind.ShouldBe(CandidateNoteKind.Reassign);
        nomzod.Notes[1].Kind.ShouldBe(CandidateNoteKind.ReturnToFunnel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Bosh_izoh_yozuv_YARATMAYDI(string matn)
    {
        // Yozuv FAQAT matn borligi uchun mavjud. Domen bo'sh matnda istisno
        // tashlaydi (xato kodi emas — sababi `CandidateNote.Create` izohida),
        // ya'ni «izoh yozilmadi» holatini CHAQIRUVCHI tekshiradi va bu
        // metodni umuman chaqirmaydi.
        var nomzod = Nomzod();

        Should.Throw<ArgumentException>(() => nomzod.AddNote(CandidateNoteKind.Reassign, matn));

        nomzod.Notes.ShouldBeEmpty();
    }
}
