namespace SensorCollector;

internal static class SelfTests
{
    public static int Run()
    {
        var time = DateTimeOffset.UtcNow;
        var missing = GigabyteTemperature.Decode(0, 0, time);
        var positive = GigabyteTemperature.Decode(1, 64, time);
        var negative = GigabyteTemperature.Decode(2, 255, time);
        if (missing.Value is not null || missing.Status != "unavailable" || missing.RawValue != 0)
            throw new Exception("Absent Gigabyte sensor must never become 0°C.");
        if (positive.Value != 64 || positive.Status != "unmapped" || positive.SampledAt != time)
            throw new Exception("Valid raw temperature must preserve value, time, and unmapped status.");
        if (negative.Value != -1 || negative.RawValue != 255)
            throw new Exception("Signed-byte temperature decoding must preserve the raw byte.");
        if (positive.Name.Contains("CPU") || positive.Id != "gigabyte/temperature/1")
            throw new Exception("Raw channels must not be relabelled CPU sensors without mapping evidence.");
        var wmi = new FakeWmiObject();
        if (!Equals(WindowsWmi.Property(wmi, "__PATH"), "verified-instance") ||
            !Equals(WindowsWmi.Property(wmi, "Active"), true))
            throw new Exception("WMI paths must use system metadata; ordinary values must use class properties.");
        if (LibreSource.Decode("/cpu/temperature/0", "CPU", float.NaN, "°C", time).Value is not null ||
            LibreSource.Decode("/cpu/temperature/0", "CPU", null, "°C", time).Status != "unavailable" ||
            LibreSource.Decode("/board/fan/0", "Fan", 0, "RPM", time).Value != 0)
            throw new Exception("Missing/nonfinite sensors must be unavailable; a valid stopped fan may read zero.");
        var voltage = LibreSource.Decode("/board/voltage/0", "Voltage", 1.86f, "V", time);
        if (voltage.Status != "unmapped" || voltage.Value is null)
            throw new Exception("Unverified voltage data must be retained diagnostically without becoming confirmed readings.");
        if (LibreSource.DecodeMemoryClock(3000, 99.8) != 2994 || LibreSource.DecodeMemoryClock(0, 100) is not null ||
            LibreSource.DecodeMemoryClock(uint.MaxValue, 100) is not null || LibreSource.DecodeMemoryClock(3000, null) is not null)
            throw new Exception("DDR5 clock decoding must reject missing registers/BCLK and keep MHz separate from MT/s.");
        FrameViewSource.CheckAbi();
        CheckAutomaticGames();
        if (Svi3Source.TableSize(0x621202) != 0x994 || Svi3Source.TableSize(0x620205) != 0x994 || Svi3Source.TableSize(0x621201) != 0 ||
            Svi3Source.Decode("cpu/core-voltage", "Vcore", 0, 0, time, "test").Value is not null ||
            Svi3Source.Decode("cpu/core-voltage", "Vcore", float.NaN, 0, time, "test").Value is not null)
            throw new Exception("Measured CPU voltage requires an explicit PMT layout and finite nonzero rail reading.");
        if (PresentMonSource.AverageFps([5, 15, double.NaN, 0]) != 100 ||
            double.IsFinite(PresentMonSource.AverageFps([0, double.PositiveInfinity])) ||
            PresentMonSource.Csv("\"Game, test.exe\",1,\"a\"\"b\"")[0] != "Game, test.exe" ||
            PresentMonSource.Csv("\"Game, test.exe\",1,\"a\"\"b\"")[2] != "a\"b")
            throw new Exception("Displayed FPS must average frame durations, reject NA/zero intervals, and preserve quoted CSV fields.");
        Console.WriteLine("PASS: sensor contracts, voltage quarantine, DDR5 ratio/BCLK and FrameView x64 ABI/sample validation.");
        return 0;
    }

    private static void CheckAutomaticGames()
    {
        var first = new RunningGame(101, "Game A", @"C:\Games\A\Game.exe", DateTime.UtcNow.AddMinutes(-2), "Steam game");
        var second = first with { ProcessId = 202, Name = "Game B", StartedAt = DateTime.UtcNow };
        if (GameDiscovery.Select([first, second], 202, 101)?.ProcessId != 202 ||
            GameDiscovery.Select([first, second], 999, 101)?.ProcessId != 101 ||
            GameDiscovery.Select([second], 999, 101)?.ProcessId != 202 ||
            GameDiscovery.Select([], 999, 101) is not null)
            throw new Exception("Game focus, desktop focus, game exit, and no-game selection contracts failed.");
        InstalledGame[] installed = [new("Game A", @"C:\Games\A", "Steam game")];
        if (GameDiscovery.Match(@"C:\Games\A\Game.exe", installed) is null ||
            GameDiscovery.Match(@"C:\Games\A2\Game.exe", installed) is not null ||
            GameDiscovery.Match(@"C:\Games\A\CrashReportClient.exe", installed) is not null ||
            GameDiscovery.IsGameType("Application") || GameDiscovery.IsGameType("Tool") || !GameDiscovery.IsGameType("Game"))
            throw new Exception("Automatic FPS must exclude desktop apps, Steam software/tools, helpers, and sibling paths.");
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true);
        writer.Write(0x07564429u); writer.Write(1u); writer.Write(0L);
        writer.Write(123u); long sizeOffset = memory.Position; writer.Write(0u); long entryStart = memory.Position;
        writer.Write(new byte[60]);
        writer.Write((byte)0); writer.Write(0u); writer.Write((byte)0); writer.Write(1u);
        writer.Write((byte)1); writer.Write(2u); writer.Write(System.Text.Encoding.UTF8.GetBytes("Game\0"));
        writer.Write((byte)1); writer.Write(3u); writer.Write(System.Text.Encoding.UTF8.GetBytes("Game A\0"));
        writer.Write((byte)8); writer.Write((byte)8); writer.Write((byte)8);
        long entryEnd = memory.Position; writer.Write(0u); long table = memory.Position;
        writer.Write(4u); foreach (string key in new[] { "appinfo", "common", "type", "name" }) writer.Write(System.Text.Encoding.UTF8.GetBytes(key + "\0"));
        memory.Position = sizeOffset; writer.Write((uint)(entryEnd - entryStart)); memory.Position = 8; writer.Write(table); memory.Position = 0;
        using var reader = new BinaryReader(memory, System.Text.Encoding.UTF8, true);
        var parsed = SteamAppCache.Read(reader, [123]);
        if (!parsed.TryGetValue(123, out var game) || game.Name != "Game A" || game.Type != "Game") throw new Exception("Steam's v41 indexed game metadata was not decoded correctly.");
        Console.WriteLine("PASS: automatic games, desktop exclusion, focus/exit selection and Steam v41 metadata.");
    }

    internal sealed class FakeWmiObject
    {
        public FakeProperties Properties_ { get; } = new("Active", true);
        public FakeProperties SystemProperties_ { get; } = new("__PATH", "verified-instance");
    }
    internal sealed class FakeProperties(string name, object value)
    {
        public FakeProperty Item(string requested) => requested == name ? new(value)
            : throw new KeyNotFoundException(requested);
    }
    internal sealed class FakeProperty(object value) { public object Value { get; } = value; }
}
