using System.Text;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Referrals;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Recruiting.Forms;

/// <summary>
/// Ochiq onlayn formadan kelgan anketa (SPEC-planshet-anketa §2.1, multipart qismlari).
/// </summary>
/// <param name="Answers">Javoblar — JSON matni (§3).</param>
/// <param name="ClientSubmissionId">Forma yasagan identifikator (uuid matni).</param>
/// <param name="Language">Nomzod tanlagan til: <c>uz-Latn</c>, <c>uz-Cyrl</c>, <c>ru</c>.</param>
/// <param name="Photo">Selfi.</param>
/// <param name="PhotoContentType">Mijoz e'lon qilgan tur (rasm quvuri baribir mazmunni tekshiradi).</param>
/// <param name="PhotoSizeBytes">Yuklangan hajm.</param>
/// <param name="Signature">Barmoq bilan chizilgan imzo (PNG).</param>
/// <param name="SignatureSizeBytes">Yuklangan hajm.</param>
/// <param name="Website">
/// «Tuzoq» maydon (honeypot): formada odamga ko'rinmaydi, BO'SH bo'lishi shart. Uni to'ldirgan —
/// sahifadagi hamma maydonni ko'r-ko'rona to'ldiradigan bot.
/// </param>
/// <param name="TurnstileToken">Bot tekshiruvi tokeni (SPEC §2.3.2) — faqat tekshiruv yoqiq bo'lsa majburiy.</param>
/// <param name="ClientIp">Mijozning haqiqiy IP'si (<c>X-Bonvi-Client-Ip</c>) — bot tekshiruviga beriladi.</param>
public sealed record SubmitQuestionnaireFormCommand(
    string? Answers,
    string? ClientSubmissionId,
    string? Language,
    Stream? Photo,
    string? PhotoContentType,
    long PhotoSizeBytes,
    Stream? Signature,
    long SignatureSizeBytes,
    string? Website,
    string? TurnstileToken,
    string? ClientIp);

/// <summary>Formaga qaytadigan javob — nomzod yozuvi haqida HECH NARSA yo'q (endpoint ochiq).</summary>
/// <param name="FormNumber">«Anketa №».</param>
/// <param name="SubmittedAt">Qabul qilingan lahza (UTC).</param>
public sealed record QuestionnaireSubmittedDto(int FormNumber, DateTimeOffset SubmittedAt);

/// <summary>Natija va u yangimi (201) yoki takroriy yuborishmi (200).</summary>
public sealed record QuestionnaireSubmission(QuestionnaireSubmittedDto Result, bool Created);

/// <summary>
/// Onlayn anketani qabul qiladi: nomzod, anketa, surat, PDF — bitta saqlashda.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Qaysi nomzod</b> (SPEC §4). Telefoni VA familiya-ismi bir xil JARAYONDAGI
/// nomzod bo'lsa — anketa unga yoziladi: odatiy yo'l «bot orqali ariza → suhbatga
/// chaqirildi → ofisda qog'oz anketa». Yangi karta ochilganida bitta odam ikki
/// kartaga bo'linib ketardi. Ism ham solishtiriladi (lotin/kirill farqsiz): faqat
/// telefon bo'yicha bitta raqami adashgan nomzodning sudlanganligi, sog'lig'i va
/// qarzi boshqa odamning kartasiga tushardi va u odamning o'zgarmas anketasi
/// begona javoblardan yasalardi. Bunday nomzod bo'lmasa — yangi nomzod (manba
/// «Onlayn anketa»). Qora ro'yxat va qayta ariza muddati formani TO'SMAYDI: qarorni
/// HR qiladi (kartadagi telefon mosligi ogohlantirishi unga tarixni ko'rsatadi).
/// Formaga esa bu haqda hech narsa aytilmaydi — endpoint ochiq.
/// </para>
/// <para>
/// ⭐ <b>Ikkinchi kalit — F.I. + tug'ilgan sana</b> (egasi, 2026-10-31): telefon bo'yicha topilmasa,
/// familiya, ism va tug'ilgan sanasi bir xil JARAYONDAGI nomzod olinadi — botning uchinchi kaliti
/// (<see cref="CandidatePersonKey"/>). Prod'da odam botga bir raqam bilan yozib, anketaga boshqasini
/// kiritgan va ikkinchi karta ochilgan edi. Kartadagi telefon anketadagisiga ALMASHADI (egasi: anketa
/// yangiroq), avvalgisi <see cref="CandidateNoteKind.OtherPhone"/> izohi bo'lib lentada qoladi — bu HR
/// uchun belgi ham: begona odam F.I. va sanani bilib anketa qo'shsa, almashish shu yerda ko'rinadi.
/// </para>
/// <para>
/// <b>Vakansiya HAR IKKALA yo'lda birinchi tekshiriladi</b>: javob (404/409) telefon
/// mos kelgan-kelmaganiga bog'liq bo'lmasin — aks holda ataylab yopiq vakansiya bilan
/// yuborib, raqam voronkada bor-yo'qligini bilib olish mumkin edi.
/// </para>
/// <para>
/// <b>Mavjud nomzodda faqat BO'SH maydonlar to'ldiriladi</b> (tug'ilgan sana,
/// manzil, surat, «qayerdan bilgan»): HR kiritgan yoki bot olgan ma'lumot ustidan yozilmaydi.
/// Ism o'zgartirilmaydi. Botning anketasi (<see cref="CandidateQuestionnaire"/>)
/// o'zgarmas — bo'lsa tegilmaydi, bo'lmasa forma javoblaridan yaratiladi.
/// </para>
/// <para>
/// <b>Fayllar chegarasi anketani TO'XTATMAYDI</b> (§6.2): PDF biriktirilmasa ham
/// anketa saqlanadi, HR PDF'ni «Onlayn anketa» kartasidan oladi.
/// </para>
/// <para>
/// <b>Bitta <c>SaveChanges</c></b> — nomzod, anketa, fayl yozuvi birga yoziladi
/// yoki hech biri. Diskka oldinroq tushgan fayllar (surat, imzo, PDF) saqlash
/// yiqilsa o'chiriladi — yetim fayl qolmasin.
/// </para>
/// <para>
/// <b>Tekshiruvlar tartibi</b> (SPEC §2.3.2): «tuzoq» maydon → takroriy yuborish (allaqachon
/// qabul qilingan identifikator — 200, token SO'RALMAYDI: javobi yo'qolgan nomzodning tokeni
/// eskirgan bo'lishi mumkin) → bot tekshiruvi (Turnstile, yoqiq bo'lsa) → qolgan hamma ish.
/// Bot tekshiruvidan o'tmagan so'rov bazaga ham, diskka ham hech narsa yozmaydi.
/// </para>
/// </remarks>
public sealed class SubmitQuestionnaireFormCommandHandler
{
    /// <summary>Imzo PNG'ining chegarasi (SPEC §2.1).</summary>
    public const long MaxSignatureBytes = 300 * 1024;

    /// <summary>Imzo enining chegarasi (piksel) — forma 900 px eksport qiladi.</summary>
    public const int MaxSignatureWidth = 1200;

    /// <summary>Imzo bo'yining chegarasi (piksel) — formada odatda 220–330.</summary>
    public const int MaxSignatureHeight = 600;

    /// <summary>
    /// Forma suratining chegarasi — HR yuklashidagi 10 MB dan kichik: forma suratni
    /// yuborishdan oldin 1600 px JPEG qiladi (odatda 300 KB atrofida).
    /// </summary>
    public const long MaxPhotoBytes = 5L * 1024 * 1024;

    /// <summary>
    /// <c>answers</c> matnining chegarasi (belgi) — JSON o'qilishidan OLDIN tekshiriladi.
    /// </summary>
    /// <remarks>
    /// Haqiqiy anketa 100 KB dan kichik. Chegarasiz maydonda 4 MB lik «javob» millionlab qator
    /// obyektiga aylanib, har so'rovda yuzlab MB xotira olardi (xavfsizlik ko'rigi M3).
    /// </remarks>
    public const int MaxAnswersLength = 256_000;

    /// <summary>Qabul qilinadigan tillar.</summary>
    public static IReadOnlySet<string> Languages { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "uz-Latn", "uz-Cyrl", "ru" };

    private const string GeneralPoolLabel = "Umumiy";

    private readonly ICandidateRepository _candidates;
    private readonly ICandidateFormRepository _forms;
    private readonly IVacancyRepository _vacancies;
    private readonly IPipelineStageRepository _stages;
    private readonly IReferenceRepository<ReferralChannel> _referralChannels;
    private readonly IRecruitingReadModel _readModel;
    private readonly SystemSettingsReader _settings;
    private readonly SquarePhotoStore _photos;
    private readonly ScaledDocumentStore _documents;
    private readonly IFileStorage _storage;
    private readonly IFileContentInspector _inspector;
    private readonly IQuestionnairePdfRenderer _pdf;
    private readonly IQuestionnaireFormJournal _journal;
    private readonly IQuestionnaireBotCheck _botCheck;
    private readonly IImageProcessor _images;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitQuestionnaireFormCommandHandler(
        ICandidateRepository candidates,
        ICandidateFormRepository forms,
        IVacancyRepository vacancies,
        IPipelineStageRepository stages,
        IReferenceRepository<ReferralChannel> referralChannels,
        IRecruitingReadModel readModel,
        SystemSettingsReader settings,
        SquarePhotoStore photos,
        ScaledDocumentStore documents,
        IFileStorage storage,
        IFileContentInspector inspector,
        IQuestionnairePdfRenderer pdf,
        IQuestionnaireFormJournal journal,
        IQuestionnaireBotCheck botCheck,
        IImageProcessor images,
        IDatabaseFault fault,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _forms = forms;
        _vacancies = vacancies;
        _stages = stages;
        _referralChannels = referralChannels;
        _readModel = readModel;
        _settings = settings;
        _photos = photos;
        _documents = documents;
        _storage = storage;
        _inspector = inspector;
        _pdf = pdf;
        _journal = journal;
        _botCheck = botCheck;
        _images = images;
        _fault = fault;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<QuestionnaireSubmission>> HandleAsync(
        SubmitQuestionnaireFormCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // «Tuzoq» maydon — HAMMASIDAN oldin: bot hech narsa yozdira olmaydi, takroriy yuborish
        // javobini ham olmaydi. Kod umumiy («ruxsat etilmagan belgi») — botga sababni aytmaymiz.
        if (!string.IsNullOrEmpty(command.Website))
        {
            return QuestionnaireFormErrors.ForbiddenCharacter.At("website");
        }

        if (!Guid.TryParse(command.ClientSubmissionId, out var submissionId) || submissionId == Guid.Empty)
        {
            return QuestionnaireFormErrors.Invalid.At("clientSubmissionId");
        }

        // Hajm JSON o'qilishidan OLDIN (MaxAnswersLength izohi)
        if (command.Answers is { Length: > MaxAnswersLength })
        {
            return QuestionnaireFormErrors.Invalid.At("answers");
        }

        // ── Takroriy yuborish: javob yo'qolgan va forma qayta yuborgan ──
        var earlier = await _forms.FindBySubmissionAsync(submissionId, cancellationToken).ConfigureAwait(false);
        if (earlier is not null)
        {
            return Repeated(earlier);
        }

        // ── Bot tekshiruvi — boshqa har qanday ishdan OLDIN (sinf izohi, «Tekshiruvlar tartibi») ──
        if (_botCheck.SiteKey is not null
            && !await _botCheck
                .VerifyAsync(command.TurnstileToken, command.ClientIp, submissionId, cancellationToken)
                .ConfigureAwait(false))
        {
            return QuestionnaireFormErrors.BotCheckFailed.At("turnstileToken");
        }

        if (command.Language is not { } language || !Languages.Contains(language))
        {
            return QuestionnaireFormErrors.Invalid.At("language");
        }

        if (QuestionnaireFormJson.Read(command.Answers) is not { } raw)
        {
            return QuestionnaireFormErrors.Unreadable;
        }

        // «Qayerdan bilgan» kanallari BITTA so'rovda: faollari — tekshiruvchiga (yangi tanlov
        // faqat ulardan), hammasi — PDF'ga (SPEC-qayerdan-bilgan §5)
        var referralChannels = await _referralChannels.ListAsync(onlyActive: false, cancellationToken).ConfigureAwait(false);

        var validated = QuestionnaireFormValidator.Validate(
            raw,
            _clock.Today,
            [
                .. referralChannels
                    .Where(channel => channel.IsActive)
                    .Select(channel => new ReferralChannelOptionDto(channel.Id, channel.Name, channel.IsActive, channel.AsksDetail)),
            ]);

        if (validated.IsFailure)
        {
            return validated.Error;
        }

        var answers = validated.Value;

        var photoUpload = await ReadPhotoAsync(command, cancellationToken).ConfigureAwait(false);
        if (photoUpload.IsFailure)
        {
            return photoUpload.Error;
        }

        var signature = await ReadSignatureAsync(command, cancellationToken).ConfigureAwait(false);
        if (signature.IsFailure)
        {
            return signature.Error;
        }

        // ── Nomzod: jarayondagisi yoki yangisi ──
        var target = await ResolveCandidateAsync(answers, cancellationToken).ConfigureAwait(false);
        if (target.IsFailure)
        {
            return target.Error;
        }

        var (candidate, isNew, vacancyTitle) = target.Value;

        // ── Fayllar diskka — saqlash yiqilsa hammasi o'chiriladi ──
        var stored = new List<string>();

        try
        {
            var (photoBytes, photoType) = photoUpload.Value;

            using var photoStream = new MemoryStream(photoBytes, writable: false);

            var photo = await _photos
                .AcceptAsync(photoStream, photoType, photoBytes.Length, cancellationToken)
                .ConfigureAwait(false);

            if (photo.Key is not { } photoKey)
            {
                // Formaga bitta kod (QuestionnaireFormErrors.PhotoInvalid izohi)
                return QuestionnaireFormErrors.PhotoInvalid.At("photo");
            }

            stored.Add(photoKey);

            /*
              ⭐ Anketa surati nomzodning PROFIL suratiga ham — bor bo'lsa ham ALMASHADI (egasi,
              2026-10-30): anketani HR boshlab beradi va suratni o'zi sifatli qilib oladi, botdagi
              (Telegram) surat esa odatda tasodifiy. Anketaga MUSTAQIL nusxa — profil surati
              keyin almashsa yoki o'chirilsa ham imzolangan anketa o'z suratini saqlaydi.
              Eski profil surati diskdan faqat saqlash muvaffaqiyatli bo'lgach o'chiriladi
              (`UploadCandidatePhotoCommandHandler` dagi tartib).
            */
            var replacedPhotoKey = candidate.PhotoKey;

            candidate.SetPhoto(photoKey);
            var formPhotoKey = await _photos.CopyAsync(photoKey, cancellationToken).ConfigureAwait(false) ?? photoKey;

            if (formPhotoKey != photoKey)
            {
                stored.Add(formPhotoKey);
            }

            using var signatureStream = new MemoryStream(signature.Value);
            var signatureFile = await _storage
                .SaveAsync(signatureStream, "imzo.png", "image/png", cancellationToken)
                .ConfigureAwait(false);

            stored.Add(signatureFile.Key);

            var filled = FillBlanks(candidate, answers, isNew);
            if (filled.IsFailure)
            {
                await DeleteAsync(stored, cancellationToken).ConfigureAwait(false);
                return filled.Error;
            }

            // «Qayerdan bilgan» — yangi nomzodda doim, mavjudida faqat bo'sh bo'lsa (bot yoki HR
            // qo'ygan kanal ustidan yozilmaydi). Kanal va aniqlik tekshiruvchidan o'tgan.
            if (candidate.ReferralChannelId is null)
            {
                var referral = candidate.SetReferral(answers.ReferralChannelId!.Value, answers.ReferralDetail);
                if (referral.IsFailure)
                {
                    await DeleteAsync(stored, cancellationToken).ConfigureAwait(false);
                    return referral.Error.At("referralDetail");
                }
            }

            // Bazaning aniqligi — mikrosekund: takroriy yuborish vaqtni bazadan o'qiydi va javob
            // AYNAN birinchisidagidek bo'lishi kerak (SPEC §2.1 «same body»)
            var now = _clock.UtcNow;
            var submittedAt = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));

            if (candidate.Questionnaire is null)
            {
                var questionnaire = QuestionnaireFormMapping.ToQuestionnaire(candidate.Id, answers, submittedAt);
                var attached = questionnaire.IsFailure ? Result.Failure(questionnaire.Error) : candidate.AttachQuestionnaire(questionnaire.Value);

                if (attached.IsFailure)
                {
                    await DeleteAsync(stored, cancellationToken).ConfigureAwait(false);
                    return attached.Error;
                }
            }

            var number = await _forms.NextNumberAsync(cancellationToken).ConfigureAwait(false);

            var squarePhoto = await ReadAsync(_photos.OpenAsync(formPhotoKey, null, cancellationToken), cancellationToken).ConfigureAwait(false);

            // Deploy paytida ochiq qolgan eski sahifadan kelgan anketa — o'z versiyasi bilan (IsLegacy izohi)
            var formVersion = QuestionnaireFormValidator.IsLegacy(answers)
                ? CandidateForm.HrF02LegacyVersion
                : CandidateForm.HrF02Version;

            var pdf = await _pdf.RenderAsync(
                new QuestionnairePdfModel(
                    answers,
                    number,
                    submittedAt.ToTashkent(),
                    vacancyTitle,
                    language,
                    squarePhoto,
                    signature.Value,
                    QuestionnairePdfChoice.ReferralChannels(referralChannels, answers.ReferralChannelId),
                    formVersion),
                cancellationToken).ConfigureAwait(false);

            var fileId = await AttachPdfAsync(candidate, number, answers, pdf, stored, cancellationToken).ConfigureAwait(false);

            var form = CandidateForm.Submit(
                candidate.Id,
                number,
                QuestionnaireFormJson.Write(answers),
                language,
                formPhotoKey,
                signatureFile.Key,
                submissionId,
                fileId,
                submittedAt,
                formVersion);

            if (form.IsFailure)
            {
                await DeleteAsync(stored, cancellationToken).ConfigureAwait(false);
                return form.Error;
            }

            if (isNew)
            {
                _candidates.Add(candidate);
            }
            else
            {
                // Mavjud kartaga qayta murojaat — ro'yxatda tepaga chiqadi (`Candidate.ReappliedAt`)
                candidate.MarkReapplied(submittedAt);
            }

            _forms.Add(form.Value);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception failure) when (_fault.IsDuplicateFormSubmission(failure))
            {
                /*
                  Poyga: javobi kechikkan so'rov qayta yuborildi va ikkalasi ham boshidagi
                  «oldin yozilganmi» tekshiruvidan o'tdi. G'olib allaqachon yozildi — bu
                  so'rovning fayllari o'chiriladi va javob takroriy yuborishdagidek
                  (o'sha raqam, 200). Tranzaksiya bekor bo'lgan, yangi nomzod ham yozilmadi.
                */
                await DeleteAsync(stored, CancellationToken.None).ConfigureAwait(false);

                var winner = await _forms.FindBySubmissionAsync(submissionId, cancellationToken).ConfigureAwait(false);
                if (winner is null)
                {
                    throw;
                }

                return Repeated(winner);
            }

            // Almashtirilgan profil surati — saqlangandan KEYIN (yuqoridagi izoh). Shu yerdan
            // keyin `stored` dagi fayllar bazada yozilgan: pastdagi xato ularni o'chirmasligi kerak.
            stored.Clear();
            await DeleteReplacedPhotoAsync(candidate.Id, replacedPhotoKey, CancellationToken.None).ConfigureAwait(false);

            return new QuestionnaireSubmission(new QuestionnaireSubmittedDto(number, submittedAt), Created: true);
        }
        catch
        {
            // Bazaga yozilmadi — diskdagi fayllar yetim qolmasin. Xato o'zi yuqoriga ketadi.
            await DeleteAsync(stored, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    // ════════════════════════════════════════════════ nomzod

    private async Task<Result<(Candidate Candidate, bool IsNew, string VacancyTitle)>> ResolveCandidateAsync(
        QuestionnaireFormAnswers answers,
        CancellationToken cancellationToken)
    {
        var poolId = await GeneralPoolVacancy.IdAsync(_settings, cancellationToken).ConfigureAwait(false);

        // Vakansiya IKKALA yo'lda birinchi (sinf izohi — javob telefonga bog'liq bo'lmasin)
        var vacancy = await ResolveVacancyAsync(answers.VacancyId, poolId, cancellationToken).ConfigureAwait(false);
        if (vacancy.IsFailure)
        {
            return vacancy.Error;
        }

        var existing = await FindSamePersonAsync(answers, vacancy.Value.Id, cancellationToken).ConfigureAwait(false);

        if (existing is null
            && await FindByPersonKeyAsync(answers, vacancy.Value.Id, cancellationToken).ConfigureAwait(false) is { } byPerson)
        {
            existing = byPerson;

            // Telefon anketadagisiga almashadi, avvalgisi lentada (sinf izohi)
            if (!string.Equals(byPerson.Phone?.Value, answers.Phone, StringComparison.Ordinal))
            {
                var previous = byPerson.Phone?.Value ?? "yo'q";
                byPerson.ChangePhone(PhoneNumber.FromTrusted(answers.Phone!));
                byPerson.AddNote(
                    CandidateNoteKind.OtherPhone,
                    $"Onlayn anketadagi telefon qo'yildi: {answers.Phone}. Avvalgi telefon: {previous}.");
            }
        }

        if (existing is not null)
        {
            /*
              ⭐ Anketada BOSHQA vakansiya tanlangan bo'lsa — karta o'sha vakansiyaga o'tadi
              (egasi, 2026-10-30): botda «Kassir» ga yozgan odam ofisda «Omborchi» ni tanlasa,
              uning hozirgi niyati — anketadagisi. Ilgari karta eski vakansiyada qolardi va
              yangi tanlov hech qayerda ko'rinmasdi. Bosqichlar tarixi o'zgarmaydi
              (`Candidate.Reassign`), kartada «Boshqa vakansiyaga o'tkazildi» izohi qoladi.

              «Umumiy anketa» (vakansiya tanlanmagan) aniq vakansiyaning o'rnini OLMAYDI —
              bu tanlov emas, tanlovning yo'qligi.
            */
            var chosen = vacancy.Value;

            if (chosen.Id != existing.VacancyId && chosen.Id != poolId)
            {
                var previousTitle = existing.VacancyId == poolId
                    ? GeneralPoolLabel
                    : (await _vacancies.GetByIdAsync(existing.VacancyId, cancellationToken).ConfigureAwait(false))?.Title
                        ?? GeneralPoolLabel;

                var reassigned = existing.Reassign(chosen.Id);
                if (reassigned.IsFailure)
                {
                    return reassigned.Error;
                }

                existing.AddNote(
                    CandidateNoteKind.Reassign,
                    $"Onlayn anketada «{chosen.Title}» tanlandi. Avvalgi vakansiya: «{previousTitle}».");

                return (existing, false, chosen.Title);
            }

            var title = existing.VacancyId == poolId
                ? GeneralPoolLabel
                : (await _vacancies.GetByIdAsync(existing.VacancyId, cancellationToken).ConfigureAwait(false))?.Title
                    ?? GeneralPoolLabel;

            return (existing, false, title);
        }

        var stages = await _stages.ListAsync(onlyActive: true, cancellationToken).ConfigureAwait(false);
        var first = stages.OrderBy(stage => stage.SortOrder).FirstOrDefault();

        if (first is null)
        {
            return RecruitingErrors.NoActiveStages;
        }

        var fullName = FullName.Create(answers.LastName, answers.FirstName, answers.MiddleName);
        if (fullName.IsFailure)
        {
            return fullName.Error.At("lastName");
        }

        var registered = Candidate.Register(
            fullName.Value,
            vacancy.Value.Id,
            CandidateSource.OnlineForm,
            answers.BirthDate,
            PhoneNumber.FromTrusted(answers.Phone!),
            answers.ActualAddress);

        if (registered.IsFailure)
        {
            return registered.Error;
        }

        var candidate = registered.Value;

        // Birinchi bosqich «kutilmoqda» — botdan kelgan nomzoddagidek (RegisterCandidate izohi)
        var recorded = candidate.RecordStage(first.Id, _clock.Today);
        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        return (candidate, true, vacancy.Value.Id == poolId ? GeneralPoolLabel : vacancy.Value.Title);
    }

    /// <summary>
    /// Telefoni VA familiya-ismi mos JARAYONDAGI nomzod (sinf izohi); bo'lmasa — <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Tartib o'qish modelidan: avval jarayondagilar, ichida eng yangisi. Taqqoslash
    /// <see cref="QuestionnaireFormValidator.Fold"/> bilan — «Каримов Азиз» (bot) va
    /// «Karimov Aziz» (onlayn anketa) bitta odam. Otasining ismi solishtirilmaydi: bot uni
    /// ko'pincha so'ramagan.
    /// </remarks>
    /// <param name="answers">Forma javoblari.</param>
    /// <param name="chosenVacancyId">
    /// Anketada tanlangan vakansiya: odamning bir nechta jarayondagi kartasi bo'lsa, AYNAN shu
    /// vakansiyadagisi olinadi — boshqa karta jimgina ko'chirilib, bitta vakansiyada ikkita karta
    /// qolmasin (qa-review 2026-10-30).
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    private async Task<Candidate?> FindSamePersonAsync(
        QuestionnaireFormAnswers answers,
        Guid chosenVacancyId,
        CancellationToken cancellationToken)
    {
        // Kanonik qiymat «+998901234567» — qidiruv raqamlar bo'yicha
        var digits = answers.Phone![1..];

        var matches = await _readModel
            .FindPhoneMatchesAsync(digits, exceptId: null, limit: 5, cancellationToken)
            .ConfigureAwait(false);

        var lastName = QuestionnaireFormValidator.Fold(answers.LastName);
        var firstName = QuestionnaireFormValidator.Fold(answers.FirstName);

        Candidate? first = null;

        foreach (var match in matches.Where(match => match.Status == CandidateStatus.InProgress))
        {
            var candidate = await _candidates.GetWithStagesAsync(match.Id, cancellationToken).ConfigureAwait(false);

            if (candidate is not null
                && string.Equals(QuestionnaireFormValidator.Fold(candidate.FullName.LastName), lastName, StringComparison.Ordinal)
                && string.Equals(QuestionnaireFormValidator.Fold(candidate.FullName.FirstName), firstName, StringComparison.Ordinal))
            {
                if (candidate.VacancyId == chosenVacancyId)
                {
                    return candidate;
                }

                first ??= candidate;
            }
        }

        return first;
    }

    /// <summary>
    /// F.I. va tug'ilgan sanasi mos JARAYONDAGI nomzod — telefon bo'yicha topilmaganda (sinf izohi).
    /// </summary>
    /// <remarks>
    /// Bir nechta bo'lsa, anketada tanlangan vakansiyadagisi, bo'lmasa eng yangisi —
    /// <see cref="FindSamePersonAsync"/> dagi qoidaning o'zi.
    /// </remarks>
    private async Task<Candidate?> FindByPersonKeyAsync(
        QuestionnaireFormAnswers answers,
        Guid chosenVacancyId,
        CancellationToken cancellationToken)
    {
        if (answers.BirthDate is not { } birthDate
            || string.IsNullOrWhiteSpace(answers.LastName)
            || string.IsNullOrWhiteSpace(answers.FirstName))
        {
            return null;
        }

        var ids = await _readModel
            .FindInProgressByPersonAsync(
                new CandidatePersonKey(answers.LastName.Trim(), answers.FirstName.Trim(), birthDate),
                limit: 5,
                cancellationToken)
            .ConfigureAwait(false);

        Candidate? first = null;

        foreach (var id in ids)
        {
            var candidate = await _candidates.GetWithStagesAsync(id, cancellationToken).ConfigureAwait(false);
            if (candidate is null)
            {
                continue;
            }

            if (candidate.VacancyId == chosenVacancyId)
            {
                return candidate;
            }

            first ??= candidate;
        }

        return first;
    }

    /// <summary>
    /// Vakansiya: tanlangani yoki «Umumiy anketa» — <c>RegisterCandidateCommandHandler</c> bilan bir xil qoida.
    /// </summary>
    /// <remarks>
    /// Xatoga <c>field: vacancyId</c> qo'shiladi: anketa to'ldirilayotganda vakansiya
    /// yopilib qolishi mumkin va forma nomzodni birinchi qadamga qaytarib, boshqasini
    /// tanlatadi — yigirma daqiqalik javoblar yo'qolmaydi.
    /// </remarks>
    private async Task<Result<Vacancy>> ResolveVacancyAsync(Guid? requested, Guid? poolId, CancellationToken cancellationToken)
    {
        if (requested is { } vacancyId)
        {
            var chosen = await _vacancies.GetByIdAsync(vacancyId, cancellationToken).ConfigureAwait(false);

            if (chosen is null)
            {
                return RecruitingErrors.VacancyNotFound.At("vacancyId");
            }

            return chosen.AcceptsCandidates
                ? chosen
                : RecruitingErrors.VacancyNotAccepting.At("vacancyId");
        }

        var pool = poolId is { } id
            ? await _vacancies.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
            : null;

        return pool is { AcceptsCandidates: true }
            ? pool
            : RecruitingErrors.GeneralPoolUnavailable.At("vacancyId");
    }

    /// <summary>Mavjud nomzodda faqat bo'sh maydonlar; yangisida hammasi allaqachon yozilgan.</summary>
    private static Result FillBlanks(Candidate candidate, QuestionnaireFormAnswers answers, bool isNew)
    {
        if (isNew || (candidate.BirthDate is not null && candidate.Address is not null))
        {
            return Result.Success();
        }

        return candidate.Update(
            candidate.FullName,
            candidate.BirthDate ?? answers.BirthDate,
            candidate.Phone,
            candidate.Address ?? answers.ActualAddress,
            candidate.Speciality,
            candidate.Note);
    }

    // ════════════════════════════════════════════════ fayllar

    /// <summary>PDF'ni nomzod fayllariga qo'shadi; chegara to'lgan bo'lsa — <c>null</c>, anketa baribir saqlanadi.</summary>
    private async Task<Guid?> AttachPdfAsync(
        Candidate candidate,
        int number,
        QuestionnaireFormAnswers answers,
        byte[] pdf,
        List<string> stored,
        CancellationToken cancellationToken)
    {
        using var content = new MemoryStream(pdf);

        var accepted = await _documents.AcceptAsync(content, pdf.Length, cancellationToken).ConfigureAwait(false);

        if (accepted.Key is not { } key)
        {
            _journal.PdfNotAttached(number, accepted.Rejection?.ToString() ?? "rad_etildi");
            return null;
        }

        stored.Add(key);

        var attached = candidate.AttachFile(
            key,
            FileName(number, answers),
            accepted.ContentType!,
            accepted.StoredBytes,
            createdBy: null);

        if (attached.IsFailure)
        {
            stored.Remove(key);
            await _documents.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            _journal.PdfNotAttached(number, attached.Error.Code);

            return null;
        }

        return attached.Value.Id;
    }

    /// <summary><c>Anketa-HR-F02-1024-Gulomov-Sardor.pdf</c> — tutuqlarsiz, bo'shliqlar chiziqcha.</summary>
    public static string FileName(int number, QuestionnaireFormAnswers answers)
    {
        var builder = new StringBuilder($"Anketa-{CandidateForm.HrF02}-{number}");

        foreach (var part in new[] { answers.LastName, answers.FirstName })
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            _ = builder.Append('-');

            foreach (var symbol in part.Trim())
            {
                if (char.IsLetterOrDigit(symbol))
                {
                    _ = builder.Append(symbol);
                }
                else if (symbol is ' ' or '-' or '_' && builder[^1] != '-')
                {
                    _ = builder.Append('-');
                }
            }
        }

        return builder.Append(".pdf").ToString();
    }

    /// <summary>
    /// Takroriy yuborish — o'sha javob (200), hech narsa yozilmaydi.
    /// </summary>
    /// <remarks>
    /// Identifikatorning o'zi yetadi (taqqoslaydigan qurilma yo'q): uuid v4 ni taxmin qilib
    /// bo'lmaydi, javobda esa faqat raqam va vaqt — nomzod haqida hech narsa.
    /// </remarks>
    private static QuestionnaireSubmission Repeated(CandidateForm earlier)
        => new(new QuestionnaireSubmittedDto(earlier.Number, earlier.SubmittedAt), Created: false);

    /// <summary>
    /// Suratni o'qiydi: 5 MB gacha, MAZMUNI bo'yicha JPEG yoki PNG (e'lon qilingan turga ishonilmaydi).
    /// </summary>
    /// <remarks>
    /// Nomzod yozuviga tegishdan OLDIN — arzon tekshiruvlar oldinda. Dekodlash va piksel
    /// chegarasi keyin, rasm quvurida (<c>SquarePhotoStore</c> → <c>IImageProcessor</c>).
    /// </remarks>
    private async Task<Result<(byte[] Bytes, string ContentType)>> ReadPhotoAsync(
        SubmitQuestionnaireFormCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Photo is null || command.PhotoSizeBytes <= 0)
        {
            return QuestionnaireFormErrors.PhotoMissing.At("photo");
        }

        if (command.PhotoSizeBytes > MaxPhotoBytes)
        {
            return QuestionnaireFormErrors.PhotoInvalid.At("photo");
        }

        using var buffer = new MemoryStream();
        await command.Photo.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        var bytes = buffer.ToArray();

        if (bytes.Length is 0 or > (int)MaxPhotoBytes)
        {
            return QuestionnaireFormErrors.PhotoInvalid.At("photo");
        }

        return _inspector.Detect(bytes) switch
        {
            DetectedFileKind.Jpeg => (bytes, "image/jpeg"),
            DetectedFileKind.Png => (bytes, "image/png"),
            _ => QuestionnaireFormErrors.PhotoInvalid.At("photo"),
        };
    }

    /// <summary>
    /// Imzoni o'qiydi va QAYTA CHIZADI: faqat PNG, ko'pi bilan
    /// <see cref="MaxSignatureWidth"/>×<see cref="MaxSignatureHeight"/>, oq fonda.
    /// </summary>
    /// <remarks>
    /// Saqlanadigani — serverning o'zi kodlagan PNG, mijoz baytlari EMAS: imzo keyin har safar
    /// HR PDF'ni ochganda dekodlanadi va kichik faylda ulkan o'lcham e'lon qilgan PNG har safar
    /// API jarayonini o'ldirardi (xavfsizlik ko'rigi H1).
    /// </remarks>
    private async Task<Result<byte[]>> ReadSignatureAsync(SubmitQuestionnaireFormCommand command, CancellationToken cancellationToken)
    {
        if (command.Signature is null || command.SignatureSizeBytes <= 0)
        {
            return QuestionnaireFormErrors.SignatureMissing.At("signature");
        }

        if (command.SignatureSizeBytes > MaxSignatureBytes)
        {
            return QuestionnaireFormErrors.SignatureInvalid.At("signature");
        }

        using var buffer = new MemoryStream();
        await command.Signature.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        var bytes = buffer.ToArray();

        // Mazmun bo'yicha — e'lon qilingan sarlavhaga ishonilmaydi
        if (bytes.Length is 0 or > (int)MaxSignatureBytes || _inspector.Detect(bytes) != DetectedFileKind.Png)
        {
            return QuestionnaireFormErrors.SignatureInvalid.At("signature");
        }

        using var source = new MemoryStream(bytes, writable: false);

        var flat = await _images
            .ToFlatPngAsync(source, MaxSignatureWidth, MaxSignatureHeight, cancellationToken)
            .ConfigureAwait(false);

        if (flat is null)
        {
            return QuestionnaireFormErrors.SignatureInvalid.At("signature");
        }

        await using (flat.Content.ConfigureAwait(false))
        {
            using var output = new MemoryStream();
            await flat.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

            return output.ToArray();
        }
    }

    private static async Task<byte[]?> ReadAsync(Task<PhotoContent?> opening, CancellationToken cancellationToken)
    {
        var photo = await opening.ConfigureAwait(false);

        if (photo?.Content is not { } stream)
        {
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

            return buffer.ToArray();
        }
    }

    /// <summary>Onlayn anketa almashtirgan profil suratini diskdan o'chiradi.</summary>
    /// <remarks>
    /// Kalit nomzodning biror anketasida ham turgan bo'lsa — o'chirilmaydi: nusxa olish yiqilgan
    /// eski yozuvlarda anketa va profil BITTA faylni bo'lishgan (<c>CopyAsync</c> <c>null</c>).
    /// Baza yoki disk xatosi javobni buzmaydi — anketa allaqachon saqlangan, yetim fayl esa zararsiz.
    /// </remarks>
    private async Task DeleteReplacedPhotoAsync(Guid candidateId, string? key, CancellationToken cancellationToken)
    {
        if (key is null)
        {
            return;
        }

        try
        {
            var forms = await _forms.ListByCandidateAsync(candidateId, cancellationToken).ConfigureAwait(false);
            if (forms.Any(form => form.PhotoKey == key))
            {
                return;
            }

            await _photos.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Yetim fayl qoladi — anketa allaqachon yozilgan; 500 bilan javob berish nomzodni
            // qayta yuborishga majburlardi
        }
    }

    private async Task DeleteAsync(List<string> keys, CancellationToken cancellationToken)
    {
        foreach (var key in keys)
        {
            await _storage.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
        }

        keys.Clear();
    }

}
