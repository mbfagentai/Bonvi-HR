using Bonvi.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Extensions;

/// <summary>
/// Domen natijasini (<see cref="Result"/>) HTTP javobiga aylantiradi.
/// </summary>
/// <remarks>
/// Bu yagona joyda "qaysi xato qaysi HTTP statusiga mos keladi" hal qilinadi.
/// Endpoint'larda <c>if (result.IsFailure) return BadRequest(...)</c> takrorlanmaydi.
/// </remarks>
public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Results.NoContent()
            : Problem(result.Error);
    }

    public static IResult ToHttpResult<TValue>(this Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Problem(result.Error);
    }

    public static IResult ToCreatedResult<TValue>(this Result<TValue> result, string location)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Results.Created(location, result.Value)
            : Problem(result.Error);
    }

    private static IResult Problem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = error.Code,
            Detail = error.Message,
        };

        // Yonma-yon kuchda bo'lgan sabablar. Bitta amal bir nechta qoidaga
        // urilishi mumkin (xodim bir vaqtda qora ro'yxatda ham, nazoratda
        // ham), va HR to'liq rasmni ko'rishi kerak — aks holda u birinchi
        // to'siqni yopib, ikkinchisiga urilib, «yana nima?» deb qolardi.
        //
        // `Extensions` ProblemDetails'da YUQORI darajaga yoyiladi, ya'ni
        // javobda oddiy `"warnings": [...]` maydoni bo'lib ko'rinadi.
        if (error.Related.Count > 0)
        {
            problem.Extensions["warnings"] = error.Related;
        }

        return Results.Problem(problem);
    }
}
