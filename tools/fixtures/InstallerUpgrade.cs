// Inert native-window fixture: no sensors, real settings, startup tasks or assets.
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
#if OLD
[assembly: AssemblyVersion("1.0.0.0")]
#else
[assembly: AssemblyVersion("2.0.0.0")]
#endif
class UpgradeFixture : Form
{
    readonly string mode;
    readonly string root = AppDomain.CurrentDomain.BaseDirectory;
    UpgradeFixture(string mode)
    {
        this.mode = mode;
        Text = "Isolated installer fixture - " + mode;
        Width = 420;
        Height = 120;
        Controls.Add(new Label { Text = "No sensors or production settings. Installer upgrade test.", Dock = DockStyle.Fill });
        Shown += delegate {
            if (mode == "tray") Hide();
            File.WriteAllText(Path.Combine(root, "ready.txt"), mode);
        };
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x11 || m.Msg == 0x16) // WM_QUERYENDSESSION / WM_ENDSESSION
        {
            File.AppendAllText(Path.Combine(root, "shutdown.txt"), m.Msg + Environment.NewLine);
            if (mode == "stubborn") { m.Result = IntPtr.Zero; return; }
        }
        if (mode == "stubborn" && m.Msg == 0x10) { m.Result = IntPtr.Zero; return; }
        base.WndProc(ref m);
    }
    [STAThread]
    static void Main(string[] args)
    {
        using (var mutex = new Mutex(false, "Local\\AORUS-FAN-MADE-SensorPanel-1B7C308F"))
            Application.Run(new UpgradeFixture(args.Length > 0 ? args[0] : "visible"));
    }
}
