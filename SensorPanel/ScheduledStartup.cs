using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace SensorPanel;

internal static class ScheduledStartup
{
    private const string Prefix = "AORUS-FAN-MADE-1B7C308F-";
    private static string TaskName => Prefix + WindowsIdentity.GetCurrent().User!.Value;
    private static dynamic Folder()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service") ?? throw new InvalidOperationException("Windows Task Scheduler is unavailable.");
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect(); return service.GetFolder("\\");
    }
    public static bool IsEnabled()
    {
        try { return (bool)Folder().GetTask(TaskName).Enabled; }
        catch (System.Runtime.InteropServices.COMException) { return false; }
        catch (FileNotFoundException) { return false; }
    }
    public static void SetEnabled(bool enabled)
    {
        dynamic folder = Folder();
        if (!enabled) { if (IsEnabled()) folder.DeleteTask(TaskName, 0); return; }
        var type = Type.GetTypeFromProgID("Schedule.Service")!;
        dynamic service = Activator.CreateInstance(type)!; service.Connect();
        dynamic definition = service.NewTask(0);
        string sid = WindowsIdentity.GetCurrent().User!.Value;
        definition.RegistrationInfo.Description = "AORUS Sensor Panel — FAN MADE. Fullscreen sensor display at this user's sign-in.";
        definition.Principal.UserId = sid;
        definition.Principal.LogonType = 3; // InteractiveToken; no stored password.
        definition.Principal.RunLevel = 1; // HighestAvailable; needed by existing PawnIO.
        definition.Settings.MultipleInstances = 2; // IgnoreNew.
        definition.Settings.DisallowStartIfOnBatteries = false;
        definition.Settings.StopIfGoingOnBatteries = false;
        definition.Settings.ExecutionTimeLimit = "PT0S";
        definition.Settings.StartWhenAvailable = true;
        dynamic trigger = definition.Triggers.Create(9); // Logon.
        trigger.UserId = sid; trigger.Delay = "PT10S";
        dynamic action = definition.Actions.Create(0); // Exec.
        action.Path = Environment.ProcessPath!;
        action.WorkingDirectory = AppContext.BaseDirectory;
        folder.RegisterTaskDefinition(TaskName, definition, 6, sid, null, 3, null);
    }
    public static void RemoveForInstallation()
    {
        dynamic folder = Folder();
        foreach (dynamic task in folder.GetTasks(0))
        {
            string name = (string)task.Name;
            if (!name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            dynamic actions = task.Definition.Actions;
            if ((int)actions.Count != 1 || (int)actions[1].Type != 0) continue;
            if (!string.Equals((string)actions[1].Path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) continue;
            folder.DeleteTask(name, 0);
        }
    }
}
