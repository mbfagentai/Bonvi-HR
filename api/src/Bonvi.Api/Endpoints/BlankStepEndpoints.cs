namespace Bonvi.Api.Endpoints;

/// <summary>Blanka qadamlari (#12–#16: tasdiqlash, planshetda imzo, qaytarish, rad etish, imzo rasmi) — S6 to'ldiradi.</summary>
public static class BlankStepEndpoints
{
    public static RouteGroupBuilder MapBlankStepEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group;
    }
}
