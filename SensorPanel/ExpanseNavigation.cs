namespace SensorPanel;

// TV-series associations, not invented homeports or live spacecraft positions.
internal sealed record ShipNavigation(string Body, string Place, string Relation, string Source);

internal static class ExpanseNavigation
{
    internal static ShipNavigation ForShip(string? ship) => ship switch
    {
        "Rocinante" => new("Tycho", "TYCHO STATION", "REFIT / SAFE HARBOR", "https://expanse.fandom.com/wiki/Rocinante_%28TV%29"),
        "Donnager" => new("Callisto", "CALLISTO SHIPYARDS", "BUILD SITE / JUPITER FLEET", "https://expanse.fandom.com/wiki/Donnager-class_%28TV%29"),
        "Truman" => new("Earth", "EARTH / UNITED NATIONS", "UNN AFFILIATION", "https://expanse.fandom.com/wiki/Truman-class_dreadnought_%28TV%29"),
        "Tynan" => new("Medina", "MEDINA / RING SPACE", "FORMER OPERATING BASE", "https://expanse.fandom.com/wiki/Tynan"),
        "Razorback" => new("Earth", "EARTH / PROSERPINA STARWORKS", "BUILDER HEADQUARTERS", "https://expanse.fandom.com/wiki/Proserpina_Starworks"),
        "Morrigan" => new("Mars", "MARS / MARINER VALLEY", "MCRN PATROL CLASS / STORY LOCATION", "https://expanse.fandom.com/wiki/Mariner_Valley_shipyard_bombing"),
        "Dropship" => new("Mars", "MARS / MCRN", "MARINE TRANSPORT AFFILIATION", "https://expanse.fandom.com/wiki/Skiff"),
        "Epstein" => new("Mars", "MARS / SOLOMON EPSTEIN", "EXPERIMENTAL TEST FLIGHT", "https://expanse.fandom.com/wiki/Epstein%27s_yacht"),
        _ => new("Sun", "SOL SYSTEM", "NAVIGATION OVERVIEW", "https://expanse.fandom.com/wiki/Sol_system")
    };
}
