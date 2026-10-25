namespace SensorCollector;

internal static class SelfTests
{
    public static int Run(bool checkNativeBridge = true)
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
        if (checkNativeBridge) FrameViewSource.CheckAbi();
        else { FrameViewSource.CheckSamples(); Console.WriteLine("SKIP: optional FrameView native ABI (use --self-test with the built bridge)."); }
        CheckAutomaticGames();
        CheckCaptureDemand();
        CheckPresentingApplications();
        CheckDesktopFrames();
        if (Svi3Source.TableSize(0x621202) != 0x994 || Svi3Source.TableSize(0x620205) != 0x994 || Svi3Source.TableSize(0x621201) != 0 ||
            Svi3Source.Decode("cpu/core-voltage", "Vcore", 0, 0, time, "test").Value is not null ||
            Svi3Source.Decode("cpu/core-voltage", "Vcore", float.NaN, 0, time, "test").Value is not null)
            throw new Exception("Measured CPU voltage requires an explicit PMT layout and finite nonzero rail reading.");
        if (PresentMonSource.AverageFps([5, 15, double.NaN, 0]) != 100 ||
            double.IsFinite(PresentMonSource.AverageFps([0, double.PositiveInfinity])) ||
            PresentMonSource.Csv("\"Game, test.exe\",1,\"a\"\"b\"")[0] != "Game, test.exe" ||
            PresentMonSource.Csv("\"Game, test.exe\",1,\"a\"\"b\"")[2] != "a\"b" ||
            !PresentMonSource.CsvField("\"Game, test.exe\",123,\"a\"\"b\"",1).SequenceEqual("123") ||
            !PresentMonSource.CsvField("\"Game\"\" one.exe\",\"456\",tail",1).SequenceEqual("456") ||
            !PresentMonSource.CsvField("Game.exe,123,",2).IsEmpty ||
            !PresentMonSource.CsvField("Game.exe,123",8).IsEmpty)
            throw new Exception("Displayed FPS must average frame durations, reject NA/zero intervals, and preserve quoted CSV fields.");
        Console.WriteLine("PASS: sensor contracts, voltage quarantine, DDR5 ratio/BCLK and FrameView sample validation." + (checkNativeBridge ? " Native x64 ABI validated." : " Native ABI not tested."));
        return 0;
    }

    private static void CheckCaptureDemand()
    {
        using var demand = new PresentMonDemand();
        using var shutdown = new CancellationTokenSource();
        var pending = Task.Run(() => demand.Acquire(shutdown.Token));
        if (pending.Wait(50)) throw new Exception("Desktop-only startup must not acquire an FPS capture.");
        demand.Update(true);
        if (!pending.Wait(2000)) throw new Exception("A newly detected game must wake FPS capture.");
        var first = pending.Result;
        demand.Update(true);
        if (first.IsCancellationRequested) throw new Exception("Desktop focus or another running game must retain capture.");
        demand.Update(false);
        if (!first.IsCancellationRequested) throw new Exception("The last game's exit must cancel even a quiet FPS pipe.");
        // A new game arriving before the old helper retires must not resurrect
        // the canceled read or lose the pending request for a fresh session.
        demand.Update(true);
        demand.Release();
        var second = demand.Acquire(shutdown.Token);
        if (second.IsCancellationRequested) throw new Exception("A later game must get a fresh capture token.");
        shutdown.Cancel();
        if (!second.IsCancellationRequested) throw new Exception("App shutdown must interrupt active FPS capture.");
        demand.Release();
        demand.Update(false);
        try { demand.Acquire(shutdown.Token); throw new Exception("App shutdown must also interrupt idle FPS waiting."); }
        catch (OperationCanceledException) { }
        Console.WriteLine("PASS: FPS demand startup, focus retention, last-game exit, rapid restart and active/idle shutdown.");
    }

    private static void CheckDesktopFrames()
    {
        if (!DesktopFrameWindow.TryParse("#X890DesktopFrame,4,12,99,12345000,10000000", out int pid, out ulong id, out double time) ||
            pid != 4 || id != 99 || time != 1234.5 ||
            DesktopFrameWindow.TryParse("#X890DesktopFrame,4,12,99,12345000,0", out _, out _, out _) ||
            DesktopFrameWindow.TryParse("#X890DesktopFrame,4,12,99,12345000,10000000,extra", out _, out _, out _))
            throw new Exception("Desktop protocol must validate its schema and normalize raw QPC using its measured frequency.");
        var desktop = new DesktopFrameWindow();
        desktop.Add(4, 10, 100, 100); desktop.Add(4, 11, 110, 110); desktop.Add(4, 99, 120, 120);
        desktop.Add(4, 99, 121, 121); desktop.Add(4, 98, 122, 122); desktop.Add(4, 100, 130, 129);
        var reading = desktop.Read(130);
        if (reading is null || reading.Value.Fps != 100 || reading.Value.Count != 3 || desktop.Read(3121) is not null)
            throw new Exception("Desktop FPS must count observed unique events, not missing frame IDs; reject duplicate, reversed, future and stale events.");
        desktop.Add(8, 1, 200, 200);
        if (desktop.Read(200) is not null) throw new Exception("A restarted DWM process must establish a fresh frame window.");
        desktop.Add(8, 2, 220, 220);
        if (desktop.Read(220)?.Fps != 50) throw new Exception("Restarted DWM must use only its new frame observations.");
        desktop.Add(8, 1, 4000, 4000); desktop.Add(8, 2, 4020, 4020);
        if (desktop.Read(4020)?.Fps != 50) throw new Exception("After a stale gap, a reset DWM frame ID must establish a fresh window.");
        Console.WriteLine("PASS: desktop QPC protocol, observed-event rate, gap/duplicate/stale rejection and DWM restart.");
    }

    private static void CheckPresentingApplications()
    {
        var csv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["QPCTime"] = "114984.3518042", ["msBetweenPresents"] = "6.25" };
        string Field(string key) => csv.GetValueOrDefault(key, "");
        if (!PresentMonSource.TryFrameTime(Field, true, out double stamp, out double interval) ||
            Math.Abs(stamp - 114984351.8042) > .001 || interval != 6.25 ||
            PresentMonSource.TryFrameTime(Field, false, out _, out _))
            throw new Exception("PresentMon v1 QPC seconds must be converted to milliseconds, and runtime FPS must not be labelled displayed FPS.");
        csv.Clear(); csv["CPUStartQPCTimeInMs"] = "114984351.8042"; csv["MsBetweenDisplayChange"] = "5";
        if (!PresentMonSource.TryFrameTime(Field, false, out stamp, out interval) || interval != 5 ||
            Math.Abs(stamp - 114984351.8042) > .001)
            throw new Exception("PresentMon display intervals and QPC milliseconds must preserve their units.");
        csv["MsBetweenDisplayChange"] = "NA";
        if (PresentMonSource.TryFrameTime(Field, false, out _, out _)) throw new Exception("Unavailable intervals cannot become FPS.");
        var frames = new PresentMonFrames();
        frames.Add(new("browser.exe", 101, "0x1", 100, 10), 100);
        frames.Add(new("browser.exe", 101, "0x1", 110, 10), 110);
        frames.Add(new("editor.exe", 202, "0x1", 100, 20), 110);
        frames.Add(new("editor.exe", 202, "0x1", 120, 20), 120);
        if (frames.Applications(120).Length != 2 || frames.Applications(120).Single(a => a.ProcessId == 101).FramesPerSecond != 100 ||
            frames.Applications(120).Single(a => a.ProcessId == 202).FramesPerSecond != 50 || frames.Select(202, 101, 120) != 202 ||
            frames.Select(999, 101, 120) != 101 || frames.Read(101, 120).Length != 2 ||
            PresentMonSource.AverageFps(frames.Read(202, 120).Select(f => f.DisplayMs)) != 50)
            throw new Exception("All presenting apps must be discovered, selected by focus, and separated by PID even with identical swap-chain addresses.");
        frames.Add(new("browser.exe", 101, "0x1", 110, 10), 120);
        frames.Add(new("bad.exe", 303, "0x1", 120, double.NaN), 120);
        frames.Add(new("bad.exe", 303, "0x1", 121, 5), 120);
        if (frames.Read(101, 120).Length != 2 || frames.Applications(120).Length != 2 ||
            frames.Applications(3121).Length != 0 || frames.Select(101, 202, 3121) != 0)
            throw new Exception("Duplicate, malformed, future and stale display events must not become FPS or active applications.");
        Console.WriteLine("PASS: presenting app discovery, focus retention, PID isolation and invalid/stale frame rejection.");
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
        if (GameDiscovery.Select([first, second], 202, 101, 101)?.ProcessId != 101 ||
            GameDiscovery.Select([first, second], 202, 101, 999) is not null ||
            GameDiscovery.Select([], 999, 101, 101) is not null)
            throw new Exception("Pinning must accept only a recognized running game and clear after it exits.");
        var frames = new PresentMonFrames();
        foreach (var app in new[] { (101, "game-a.exe"), (202, "game-b.exe"), (303, "browser.exe"), (404, "dwm.exe"), (505, "ChatGPT.exe") })
        {
            frames.Add(new(app.Item2, app.Item1, "0x1", 100, 10), 110);
            frames.Add(new(app.Item2, app.Item1, "0x1", 110, 10), 110);
        }
        var eligible = frames.Games([first, second], 110);
        if (eligible.Length != 2 || eligible.Single(a => a.ProcessId == 101).Name != "Game A" ||
            eligible.Single(a => a.ProcessId == 202).Name != "Game B" ||
            frames.Games([second], 110).Any(a => a.ProcessId != 202) || frames.Games([], 110).Length != 0 ||
            frames.Games([first, second], 3111).Length != 0)
            throw new Exception("FPS menu must expose fresh recognized games with their names, excluding desktop/apps and exited games.");
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
        writer.Write((byte)1);writer.Write(4u);writer.Write(System.Text.Encoding.UTF8.GetBytes(new string('x',512*1024)));writer.Write((byte)0);
        writer.Write((byte)8); writer.Write((byte)8); writer.Write((byte)8);
        long entryEnd = memory.Position; writer.Write(0u); long table = memory.Position;
        writer.Write(10005u); foreach (string key in new[] { "appinfo", "common", "type", "name","unused" }) writer.Write(System.Text.Encoding.UTF8.GetBytes(key + "\0"));
        for(int i=0;i<10000;i++)writer.Write(System.Text.Encoding.UTF8.GetBytes("unused-launch-configuration-"+i+"\0"));
        memory.Position = sizeOffset; writer.Write((uint)(entryEnd - entryStart)); memory.Position = 8; writer.Write(table); memory.Position = 0;
        using var reader = new BinaryReader(memory, System.Text.Encoding.UTF8, true);
        long before=GC.GetAllocatedBytesForCurrentThread();
        var parsed = SteamAppCache.Read(reader, [123]);
        if(GC.GetAllocatedBytesForCurrentThread()-before>64*1024)throw new Exception("Steam metadata retained/copied the unused application payload.");
        if (!parsed.TryGetValue(123, out var game) || game.Name != "Game A" || game.Type != "Game") throw new Exception("Steam's v41 indexed game metadata was not decoded correctly.");
        var catalog=new GameDiscovery.Catalog(Enumerable.Range(0,250).Select(i=>new InstalledGame("Game "+i,@"C:\Games\Library"+i,"Steam game")).ToArray());
        _=catalog.Match(@"C:\OrdinaryApps\App.exe");before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++)if(catalog.Match(@"C:\OrdinaryApps\App.exe") is not null)throw new Exception("Unrecognized applications entered the game catalog.");
        if(GC.GetAllocatedBytesForCurrentThread()-before>128*1024)throw new Exception("Game matching repeatedly normalized every installed path.");
        memory.Position=sizeOffset;writer.Write((uint)(entryEnd-entryStart-1));memory.Position=0;
        bool rejected=false;try{SteamAppCache.Read(reader,[123]);}catch(InvalidDataException){rejected=true;}
        if(!rejected)throw new Exception("Steam metadata read crossed a truncated application boundary.");
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
