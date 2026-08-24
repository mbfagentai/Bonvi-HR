using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Commands;

/// <summary>
/// Xodim kartasining maydonlari — yaratish va tahrirlash uchun umumiy.
/// </summary>
/// <remarks>
/// <para>
/// Ikkala amal bir xil forma bilan ishlaydi, shu sababli buyruq ham bitta.
/// Ayrim maydonlar (<c>SecurityChecked</c>, <c>RegisteredOnMehnatUz</c>)
/// ataylab <c>bool?</c>: «tekshirilmagan» va «tekshirilgan, natija yo'q»
/// bir xil narsa emas — Excel'da bu farq yo'qolgan edi.
/// </para>
/// <para>
/// <c>PhotoKey</c> va <c>BirthDate</c> da <c>null</c> «tozala» EMAS,
/// «tegilmasin» degani (sababi <see cref="EmployeeFields"/> izohida).
/// </para>
/// </remarks>
public sealed record SaveEmployeeCommand(
    string? LastName,
    string? FirstName,
    string? MiddleName = null,
    DateOnly? BirthDate = null,
    Gender? Gender = null,
    string? Pinfl = null,
    string? Passport = null,
    string? Phone = null,
    string? SecondPhone = null,
    string? Address = null,
    string? Speciality = null,
    string? TelegramUrl = null,
    string? PhotoKey = null,
    string? CriminalRecordNote = null,
    bool? SecurityChecked = null,
    bool? RegisteredOnMehnatUz = null,
    IReadOnlyList<Guid>? DirectionIds = null);

/// <summary>
/// Buyruq maydonlarini domen obyektiga o'tkazadi.
/// </summary>
/// <remarks>
/// <para>
/// Har bir qiymat o'z qiymat obyekti orqali o'tadi (<see cref="Pinfl"/>,
/// <see cref="PassportSeries"/>, <see cref="PhoneNumber"/>) — tekshiruv
/// qoidalari faqat domen ichida yashaydi va bu yerda takrorlanmaydi.
/// </para>
/// <para>
/// <b>Qaysi maydon shartsiz o'rnatiladi.</b> Qoida bitta: maydon
/// tahrirlash FORMASIDA bo'lsa — u shartsiz qo'llanadi, chunki bo'sh
/// yuborish «tozala» degan ma'noli buyruq (noto'g'ri telefon yoki manzilni
/// HR o'chira olishi kerak). Forma unga tegmasa — maydon faqat OCHIQ
/// berilgandagina qo'llanadi. Ikkinchi guruhda ikkita maydon bor:
/// </para>
/// <list type="bullet">
///   <item><c>PhotoKey</c> — rasm alohida endpoint bilan boshqariladi
///         (<c>POST/DELETE /api/employees/{id}/photo</c>);</item>
///   <item><c>BirthDate</c> — sanani «bo'shatish» ma'noli amal emas, uni
///         faqat tuzatish mumkin.</item>
/// </list>
/// <para>
/// <c>Gender</c> va <c>DirectionIds</c> allaqachon shu ikkinchi guruhda:
/// jins JSHSHIRdan kelib chiqadi, yo'nalishlar esa faqat to'liq ro'yxat
/// yuborilganda almashtiriladi.
/// </para>
/// </remarks>
internal static class EmployeeFields
{
    public static Result Apply(Employee employee, SaveEmployeeCommand command)
    {
        // JSHSHIR va tug'ilgan sana — birinchi va BIRGA. Ular bir-birini
        // tekshiradi, shu sababli bitta so'rovda ikkalasi kelganda ular
        // eski qiymatlarga emas, faqat bir-biriga qarab baholanishi kerak
        // (sababi `Employee.SetBirthDateAndPinfl` izohida). Qolgan
        // maydonlar bu juftlikka umuman tegmaydi.
        var pinfl = ParsePinfl(command.Pinfl);
        if (pinfl.IsFailure)
        {
            return pinfl.Error;
        }

        var identity = employee.SetBirthDateAndPinfl(command.BirthDate, pinfl.Value);
        if (identity.IsFailure)
        {
            return identity.Error;
        }

        var passport = ParsePassport(command.Passport);
        if (passport.IsFailure)
        {
            return passport.Error;
        }

        employee.SetPassport(passport.Value);

        var phone = ParsePhone(command.Phone);
        if (phone.IsFailure)
        {
            return phone.Error;
        }

        var secondPhone = ParsePhone(command.SecondPhone);
        if (secondPhone.IsFailure)
        {
            return secondPhone.Error;
        }

        employee.SetPhones(phone.Value, secondPhone.Value);

        var telegram = employee.SetTelegram(command.TelegramUrl);
        if (telegram.IsFailure)
        {
            return telegram.Error;
        }

        if (command.Gender is not null)
        {
            employee.SetGender(command.Gender);
        }

        employee.SetAddress(command.Address);
        employee.SetSpeciality(command.Speciality);

        // Rasm bu forma orqali O'ZGARMAYDI. Kalit shartsiz o'rnatilganda
        // «photoKey yuborilmadi» degan eng oddiy holat xodimni suratidan
        // ayirardi — mijoz uni har safar qaytarib yuborishi kerak bo'lardi.
        // Rasmni olib tashlash uchun `DELETE /api/employees/{id}/photo` bor,
        // ya'ni bu yerda uni bo'shatish yo'li umuman bo'lmasligi kerak.
        if (!string.IsNullOrWhiteSpace(command.PhotoKey))
        {
            employee.SetPhoto(command.PhotoKey);
        }

        employee.SetCriminalRecordNote(command.CriminalRecordNote);
        employee.SetSecurityChecked(command.SecurityChecked);
        employee.SetRegisteredOnMehnatUz(command.RegisteredOnMehnatUz);

        if (command.DirectionIds is { } directions)
        {
            // To'liq almashtirish: forma yo'nalishlar ro'yxatini butunlay
            // yuboradi, «qaysi biri qo'shildi/olib tashlandi» ni hisoblash
            // mijozning ishi emas.
            employee.ClearDirections();
            foreach (var directionId in directions)
            {
                employee.AssignDirection(directionId);
            }
        }

        return Result.Success();
    }

    public static Result<Pinfl?> ParsePinfl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success<Pinfl?>(null);
        }

        var parsed = Pinfl.Create(value);
        return parsed.IsFailure ? parsed.Error : Result.Success<Pinfl?>(parsed.Value);
    }

    public static Result<PassportSeries?> ParsePassport(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success<PassportSeries?>(null);
        }

        var parsed = PassportSeries.Create(value);
        return parsed.IsFailure ? parsed.Error : Result.Success<PassportSeries?>(parsed.Value);
    }

    public static Result<PhoneNumber?> ParsePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success<PhoneNumber?>(null);
        }

        var parsed = PhoneNumber.Create(value);
        return parsed.IsFailure ? parsed.Error : Result.Success<PhoneNumber?>(parsed.Value);
    }
}
