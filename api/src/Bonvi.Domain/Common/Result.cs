namespace Bonvi.Domain.Common;

/// <summary>
/// Amal natijasi: muvaffaqiyat yoki xato.
/// </summary>
/// <remarks>
/// Nega istisno emas: metod imzosining o'zi "bu amal muvaffaqiyatsiz bo'lishi mumkin"
/// deb aytadi. Kompilyator natijani tekshirishga majbur qiladi —
/// xatoni e'tibordan qoldirish imkoni yo'q.
/// </remarks>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("Muvaffaqiyatli natijada xato bo'lishi mumkin emas.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("Muvaffaqiyatsiz natijada xato ko'rsatilishi shart.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Xatoni to'g'ridan-to'g'ri qaytarish imkoni:
    /// <c>return Error.Validation(…);</c> — <c>Result.Failure(…)</c> yozish shart emas.
    /// </summary>
    public static implicit operator Result(Error error) => Failure(error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary>Birinchi muvaffaqiyatsiz natijani qaytaradi, bo'lmasa muvaffaqiyat.</summary>
    public static Result Combine(params Result[] results)
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Success();
    }
}

/// <summary>Qiymat qaytaradigan amal natijasi.</summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    /// <summary>Natija qiymati. Muvaffaqiyatsiz natijada murojaat qilish — istisno.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Muvaffaqiyatsiz natijaning qiymatiga murojaat qilib bo'lmaydi.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);

    /// <summary>Muvaffaqiyat va xato holatlarini bitta ifodada qayta ishlash.</summary>
    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure)
        => IsSuccess ? onSuccess(Value) : onFailure(Error);
}
