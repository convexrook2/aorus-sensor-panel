namespace SensorPanel;

internal sealed record ShipProfile(string Id, string Name, string Faction, string Class, string Role, string Artist)
{
    public static IReadOnlyList<ShipProfile> All { get; } =
    [
        new("Rocinante", "ROCINANTE", "ROCI", "CORVETTE", "FLIGHT ENGINEERING", "MikeMac999"),
        new("Donnager", "MCRN DONNAGER", "MCRN", "BATTLESHIP", "TACTICAL ENGINEERING", "Ryan Begemann"),
        new("AgathaKing", "UNN AGATHA KING", "UNN", "TRUMAN CLASS", "FLEET ENGINEERING", "mohamedhussien"),
        new("Tynan", "OPAS TYNAN", "OPA", "SALVAGE SHIP", "SALVAGE ENGINEERING", "Frank Yang"),
        new("Razorback", "RAZORBACK", "RACE", "PINNACE", "PERFORMANCE TELEMETRY", "Ryan Begemann"),
        new("Morrigan", "MCRN MORRIGAN", "MCRN", "PATROL DESTROYER", "PATROL ENGINEERING", "Ryan Begemann"),
        new("Dropship", "MCRN DROPSHIP", "MCRN", "DROPSHIP", "LANDING ENGINEERING", "Ryan Begemann"),
        new("Epstein", "EPSTEIN'S YACHT", "MARS", "YACHT", "DRIVE ENGINEERING", "Chris Kuhn / owlstraw"),
        new("Canterbury", "CANTERBURY", "PUR'N'KLEEN", "ICE HAULER", "HAULAGE ENGINEERING", "X890 reconstruction / Dening and Danelon references"),
        new("Knight", "KNIGHT", "PUR'N'KLEEN", "SHUTTLE", "SHUTTLE ENGINEERING", "SYFY / Voodoo, CC BY 4.0 / X890 adaptation"),
        new("Anubis", "ANUBIS", "PROTOGEN", "STEALTH FRIGATE", "SYSTEMS ENGINEERING", "Original fan model"),
        new("Pella", "PELLA", "FREE NAVY", "LIGHT CRUISER", "FLEET ENGINEERING", "Martin Taichl / CC BY 4.0 · adapted"),
        new("Medina", "MEDINA STATION", "OPA", "FORMER NAUVOO / BEHEMOTH", "STATION ENGINEERING", "RingBuilder / X890"),
        new("Scopuli", "SCOPULI", "OPA", "LIGHT FREIGHTER", "CARGO ENGINEERING", "Original fan model"),
        new("Scirocco", "MCRN SCIROCCO", "MCRN", "ASSAULT CRUISER", "MARINE ENGINEERING", "cg / Printables 477957 / CC BY 4.0 · adapted"),
        new("Hammurabi", "MCRN HAMMURABI", "MCRN", "SCIROCCO CLASS", "FLEET ENGINEERING", "Original fan model"),
        new("Guanshiyin", "GUANSHIYIN", "MAO-KWIK", "YACHT", "YACHT ENGINEERING", "Original fan model"),
        new("Arboghast", "UNS ARBOGHAST", "UN", "SCIENCE VESSEL", "SURVEY ENGINEERING", "X890 reconstruction / Chris Danelon references"),
        new("BashiBazouk", "BASHI BAZOUK", "FREE NAVY", "BELTER SHIP", "CARGO ENGINEERING", "Reference: Lee Fitzgerald / North Front"),
        new("SolRing", "SOL RING", "RING BUILDERS", "GATE", "SYSTEMS MONITORING", "Original v3 fan reconstruction")
    ];
    internal static string CanonicalId(string id) => id.ToUpperInvariant() switch
    {
        "TRUMAN" => "AgathaKing",
        "NAUVOO" or "BEHEMOTH" or "MEDINA" => "Medina",
        _ => id
    };
    public static ShipProfile Resolve(string id) => All.Single(s => s.Id == CanonicalId(id));
}
