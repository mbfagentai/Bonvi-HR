namespace Bonvi.Api.Endpoints;

/// <summary>Blanka fayllari va PDF (#17–#20) — S7 to'ldiradi.</summary>
public static class BlankFileEndpoints
{
    public static RouteGroupBuilder MapBlankFileEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group;
    }
}
