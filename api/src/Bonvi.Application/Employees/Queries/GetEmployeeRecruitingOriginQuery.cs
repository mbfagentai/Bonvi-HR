using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Employees.Queries;

/// <summary>
/// Xodimning rekrutingdan kelgan qismi: nomzod kartasiga havola va
/// nomzodlik davrida yuklangan fayllar.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega fayllar ishga qabulda KO'CHIRILMAYDI.</b> Muammo
/// foydalanuvchi aytgan holatdan keldi (2026-09-19): nomzodning anketa
/// suratlari fayllar bo'limiga yuklangan, odam sinov muddatiga ishga
/// olingan, xodim kartasidagi «Hujjatlar» esa bo'p-bo'sh chiqqan va HR
/// hamma narsani qaytadan yuklashga majbur bo'lgan. Ko'chirishning
/// to'sig'i bitta: <c>employee_documents.document_type_id</c> MAJBURIY,
/// nomzod faylida esa tur tushunchasi umuman yo'q — ya'ni avtomatik
/// nusxa olish har fayl uchun taxmin qilishni talab qilardi va tur
/// bo'yicha butun guruhlash yolg'onga aylanardi.
/// </para>
/// <para>
/// Shu sababli tanlangan yo'l — NUSXA EMAS, KO'RSATISH: xodim kartasi
/// fayllarni rekrutingdagi joyidan o'qiydi. Qarorning narxi (nomzod
/// fayli qattiq o'chiriladi, ya'ni o'chirilsa xodim kartasidan ham
/// yo'qoladi) <c>IEmployeeReadModel.ListCandidateFilesAsync</c> izohida
/// yozilgan va qabul qilingan.
/// </para>
/// <para>
/// ⭐ <b>Nega javobda HAVOLA ham bor.</b> Ikkinchi talab (o'sha kun):
/// «hujjat bo'lmasa ham nomzodlik paytida qanday edi degan savolga
/// javob topilsin». Fayllar ro'yxati bo'sh bo'lsa ham nomzod kartasiga
/// o'tish yo'li qolishi kerak — shu sababli nomzodlar ro'yxati
/// fayllardan MUSTAQIL so'raladi va ikkalasi bitta javobda qaytadi
/// (sababi <c>EmployeeRecruitingOriginDto</c> izohida).
/// </para>
/// <para>
/// <b>FAQAT O'QISH.</b> Bu yerdan fayl yuklab ham, o'chirib ham
/// bo'lmaydi — <c>GetEmployeeStagesQuery</c> dagi bilan bir xil qoida va
/// bir xil sabab: fayl rekrutingga tegishli va uning yagona egasi
/// nomzod kartasi. Ikkinchi yozish yo'li ikkita haqiqat manbai degani
/// bo'lardi.
/// </para>
/// </remarks>
public sealed record GetEmployeeRecruitingOriginQuery(Guid EmployeeId);

public sealed class GetEmployeeRecruitingOriginQueryHandler
{
    private readonly IEmployeeReadModel _readModel;
    private readonly IEmployeeRepository _employees;

    public GetEmployeeRecruitingOriginQueryHandler(
        IEmployeeReadModel readModel,
        IEmployeeRepository employees)
    {
        _readModel = readModel;
        _employees = employees;
    }

    public async Task<Result<EmployeeRecruitingOriginDto>> HandleAsync(
        GetEmployeeRecruitingOriginQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Xodim bor-yo'qligi ALOHIDA tekshiriladi: busiz bo'sh javob
        // «bu xodim rekruting orqali kelmagan» va «bunday xodim yo'q»
        // holatlarini farqlamas edi (`GetEmployeeStagesQueryHandler` dagi
        // bilan bir xil mulohaza).
        var exists = await _employees.ExistsAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return EmployeeErrors.NotFound;
        }

        /*
          Ikki so'rov KETMA-KET, parallel emas: ikkalasi ham bitta
          `DbContext` ni ishlatadi va u bir vaqtda ikkita amalni
          ko'tarmaydi (`InvalidOperationException`). Ikkalasi ham bir xil
          indeksga tushadigan arzon proyeksiya — yutuq ham bo'lmasdi.
        */
        var candidates = await _readModel
            .ListOriginCandidatesAsync(query.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        var files = await _readModel
            .ListCandidateFilesAsync(query.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new EmployeeRecruitingOriginDto(
            [
                .. candidates.Select(row => new EmployeeOriginCandidateDto(
                    row.Id,
                    row.Status,
                    row.ClosedAt,
                    row.Vacancy)),
            ],
            [
                .. files.Select(row => new EmployeeCandidateFileDto(
                    row.Id,
                    row.CandidateId,
                    row.FileName,
                    row.ContentType,
                    row.SizeBytes,
                    row.UploadedAt)),
            ]));
    }
}
