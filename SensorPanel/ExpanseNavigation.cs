namespace SensorPanel;

// TV-series associations, not invented homeports or live spacecraft positions.
internal sealed record ShipNavigation(string Body, string Place, string Relation, string Source);

internal static class ExpanseNavigation
{
    internal static ShipNavigation ForShip(string? ship) => ship switch
    {
        "SolRing" => new("SolGate", "SOL RING", "FICTIONAL GATE", "https://expanse.fandom.com/wiki/Sol_gate"),
        "Rocinante" => new("Tycho", "TYCHO STATION", "REFIT / SAFE HARBOR", "https://expanse.fandom.com/wiki/Rocinante_%28TV%29"),
        "Donnager" => new("Callisto", "CALLISTO SHIPYARDS", "BUILD SITE / JUPITER FLEET", "https://expanse.fandom.com/wiki/Donnager-class_%28TV%29"),
        "Truman" => new("Earth", "EARTH / UNITED NATIONS", "UNN AFFILIATION", "https://expanse.fandom.com/wiki/Truman-class_dreadnought_%28TV%29"),
        "Tynan" => new("Medina", "MEDINA / RING SPACE", "FORMER OPERATING BASE", "https://expanse.fandom.com/wiki/Tynan"),
        "Razorback" => new("Earth", "EARTH / PROSERPINA STARWORKS", "BUILDER HEADQUARTERS", "https://expanse.fandom.com/wiki/Proserpina_Starworks"),
        "Morrigan" => new("Mars", "MARS / MARINER VALLEY", "MCRN PATROL CLASS / STORY LOCATION", "https://expanse.fandom.com/wiki/Mariner_Valley_shipyard_bombing"),
        "Dropship" => new("Mars", "MARS / MCRN", "MARINE TRANSPORT AFFILIATION", "https://expanse.fandom.com/wiki/Skiff"),
        "Epstein" => new("Mars", "MARS / SOLOMON EPSTEIN", "EXPERIMENTAL TEST FLIGHT", "https://expanse.fandom.com/wiki/Epstein%27s_yacht"),
        "Canterbury" => new("Ceres", "CERES / WATER DELIVERY ROUTE", "ICE HAULER ASSOCIATION", "https://expanse.fandom.com/wiki/Canterbury_(TV)"),
        "Knight" => new("Ceres", "CANTERBURY / CERES ROUTE", "CARRIER ASSOCIATION", "https://expanse.fandom.com/wiki/Knight"),
        "Anubis" => new("Eros", "EROS / PROTOGEN OPERATION", "STORY ASSOCIATION", "https://expanse.fandom.com/wiki/Anubis_(TV)"),
        "Pella" => new("Mars", "FORMER MCRN HULL / FREE NAVY", "HULL ORIGIN", "https://expanse.fandom.com/wiki/Pella_(TV)"),
        "Nauvoo" => new("Tycho", "TYCHO STATION", "CONSTRUCTION SITE", "https://expanse.fandom.com/wiki/Nauvoo_(TV)"),
        "Behemoth" => new("SolGate", "SOL RING / EXPEDITION", "STORY ASSOCIATION", "https://expanse.fandom.com/wiki/Nauvoo_(TV)"),
        "Medina" => new("Medina", "MEDINA / RING SPACE", "FICTIONAL STATION", "https://expanse.fandom.com/wiki/Nauvoo_(TV)"),
        "Scopuli" => new("Eros", "EROS / REGISTRATION", "REGISTERED PORT", "https://expanse.fandom.com/wiki/Scopuli"),
        "Scirocco" => new("Ganymede", "GANYMEDE / MCRN", "STORY ASSOCIATION", "https://expanse.fandom.com/wiki/Scirocco-class_(TV)"),
        "Hammurabi" => new("Io", "IO / MCRN FLEET", "STORY ASSOCIATION", "https://expanse.fandom.com/wiki/Hammurabi_(TV)"),
        "Guanshiyin" => new("Earth", "EARTH / MAO-KWIK", "OWNER AFFILIATION", "https://expanse.fandom.com/wiki/Guanshiyin"),
        "Arboghast" => new("Venus", "VENUS / SCIENCE MISSION", "SURVEY ASSOCIATION", "https://expanse.fandom.com/wiki/Arboghast"),
        "AgathaKing" => new("Earth", "EARTH / UNITED NATIONS", "UNN AFFILIATION", "https://expanse.fandom.com/wiki/Agatha_King_(TV)"),
        "BashiBazouk" => new("Sun", "SOL SYSTEM", "NAVIGATION OVERVIEW", "https://www.artstation.com/artwork/xYGE4O"),
        _ => new("Sun", "SOL SYSTEM", "NAVIGATION OVERVIEW", "https://expanse.fandom.com/wiki/Sol_system")
    };
}
