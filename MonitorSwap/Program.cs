using System.Text;

namespace MonitorSwap;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        AppSettings.MigrateFromOldName();    // eski adla (EkranGeçiş) kaydedilmiş ayarları taşı
        Pal.Apply(AppSettings.Load().Theme); // pencereler oluşmadan önce tema seçilsin

        // Geliştirme/teşhis komutları (normal kullanımda gerekmez).
        if (args.Length >= 2 && args[0] == "--render") { Render(args[1]); return; }
        if (args.Length >= 2 && args[0] == "--selftest") { SelfTest(args[1]); return; }
        if (args.Length >= 3 && args[0] == "--audiotest") { AudioTest(args[1], args[2]); return; }
        if (args.Length >= 2 && args[0] == "--make-icon") { IconFactory.SaveAppIco(args[1]); return; }
        if (args.Length >= 2 && args[0] == "--screenshots") { Screenshots(args[1]); return; }

        using var mutex = new Mutex(true, @"Local\MonitorSwap_TekKopya", out bool created);
        if (!created)
        {
            MessageBox.Show("Monitor Swap zaten çalışıyor. Simgesi görev çubuğunun sağ köşesinde.",
                "Monitor Swap", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Application.Run(new TrayApp(args.Contains("--autostart")));
    }

    /// <summary>Monitör, ses ve pencere tespitini hiçbir şeyi değiştirmeden dener.</summary>
    static void SelfTest(string outFile)
    {
        var sb = new StringBuilder();
        try
        {
            sb.AppendLine("DDC read: " + (Ddc.TryReadInput(out uint v) ? Ddc.InputName(v) : "FAIL"));
            sb.AppendLine("DDC device: " + Ddc.LastDevice);
            var caps = Ddc.ReadCapabilities(); sb.AppendLine("Model: " + caps.Model + " | Girişler: " + (caps.Inputs == null ? "?" : string.Join(", ", caps.Inputs.Select(Ddc.InputName))));
            var (main, other, otherDev) = Ddc.Layout();
            sb.AppendLine($"Layout main={main} other={other} ({otherDev})");
            foreach (var sc in Screen.AllScreens) sb.AppendLine($"Screen {sc.DeviceName} {sc.Bounds} work={sc.WorkingArea} primary={sc.Primary}");

            var devs = Audio.ListOutputs();
            foreach (var d in devs) sb.AppendLine("Audio: " + d.Name + " | " + d.Id);
            string def = Audio.GetDefaultId(Audio.RoleConsole);
            sb.AppendLine("Default: " + devs.FirstOrDefault(d => d.Id == def)?.Name);
            // Varsayılanı kendisine ayarla: hiçbir şey değişmez ama IPolicyConfig'in çalıştığını doğrular.
            sb.AppendLine("SetDefault(same) console: " + Audio.SetDefault(def, Audio.RoleConsole));
        }
        catch (Exception ex) { sb.AppendLine("ERROR: " + ex); }
        File.WriteAllText(outFile, sb.ToString());
    }

    /// <summary>Varsayılan ses cihazını geçici olarak değiştirip geri alır; süreleri ölçer.</summary>
    static void AudioTest(string outFile, string id)
    {
        var sb = new StringBuilder();
        string Name(string x) => Audio.ListOutputs().FirstOrDefault(d => d.Id == x)?.Name ?? x;
        void Run(string label, Action a)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            a();
            sb.AppendLine($"{label}: {sw.ElapsedMilliseconds} ms -> default now {Name(Audio.GetDefaultId(0))}");
        }
        try
        {
            string orig = Audio.GetDefaultId(0);
            sb.AppendLine("orig: " + Name(orig));
            Task.Run(() => Run("MTA set target", () => sb.AppendLine("  result " + Audio.SetDefault(id, 0)))).Wait();
            Thread.Sleep(1500);
            sb.AppendLine("after 1.5s: " + Name(Audio.GetDefaultId(0)));
            Run("STA restore", () => sb.AppendLine("  result " + Audio.SetDefault(orig, 0)));
            Thread.Sleep(500);
            sb.AppendLine("final: " + Name(Audio.GetDefaultId(0)));
        }
        catch (Exception ex) { sb.AppendLine("ERROR: " + ex); }
        File.WriteAllText(outFile, sb.ToString());
    }

    /// <summary>
    /// README görselleri: pencereler ekran dışında açılır ve yalnızca kendi içerikleri çizilir
    /// (ekrandan görüntü alınmaz, arkadaki pencereler asla yakalanmaz).
    /// </summary>
    static void Screenshots(string dir)
    {
        Directory.CreateDirectory(dir);
        void Wait(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); Thread.Sleep(20); } }
        void Capture(Form f, string name)
        {
            using var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height);
            using (var g = Graphics.FromImage(bmp)) g.Clear(f.BackColor);
            var controls = f.Controls.Cast<Control>().Where(c => c.Visible).Reverse();
            foreach (var c in controls) c.DrawToBitmap(bmp, c.Bounds);
            if (f is MiniPanel)
            {
                using var full = new Bitmap(f.Width, f.Height);
                f.DrawToBitmap(full, new Rectangle(0, 0, f.Width, f.Height));
                full.Save(Path.Combine(dir, name));
                return;
            }
            bmp.Save(Path.Combine(dir, name));
        }
        var s = AppSettings.Load();
        foreach (var theme in new[] { "Koyu", "Macera" })
        {
            Pal.Apply(theme);
            string pre = theme == "Koyu" ? "" : "macera-";
            var t = s.Clone();
            t.Theme = theme;
            using (var f = new SettingsForm(t))
            {
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(-10000, -10000);
                f.Show();
                Wait(2500);
                Capture(f, pre + "ayarlar-monitor.png");
                f.ShowPage(3);
                Wait(400);
                Capture(f, pre + "ayarlar-kisayollar.png");
                f.ShowPage(4);
                Wait(400);
                Capture(f, pre + "ayarlar-genel.png");
                f.Close();
            }
            var p = new MiniPanel();
            p.ShowOn(Screen.PrimaryScreen, s.HotkeyPc.ToString(), s.DeviceName);
            p.Location = new Point(-10000, -10000);
            Wait(500);
            Capture(p, pre + "panel.png");
            p.Close();
        }
    }

    /// <summary>Ayarlar penceresi ve mini paneli PNG olarak kaydeder (görsel kontrol için).</summary>
    static void Render(string dir)
    {
        Directory.CreateDirectory(dir);
        void Shot(Form f, string name, int waitMs)
        {
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-10000, -10000);
            f.Show();
            var until = DateTime.Now.AddMilliseconds(waitMs);
            while (DateTime.Now < until) { Application.DoEvents(); Thread.Sleep(30); }
            using var bmp = new Bitmap(f.Width, f.Height);
            f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
            bmp.Save(Path.Combine(dir, name));
            f.Close();
        }
        for (int i = 0; i < 5; i++)
        {
            var f = new SettingsForm(AppSettings.Load());
            f.ShowPage(i);
            Shot(f, $"ayarlar{i}.png", i == 0 ? 2500 : 400);
        }
        var p = new MiniPanel();
        p.ShowOn(Screen.PrimaryScreen, "Ctrl+Alt+Shift+1");
        Shot(p, "panel.png", 300);
        var o = new Osd();
        o.Flash(Screen.PrimaryScreen, "Ses cihazı değişti ama FxSound kendini tekrar varsayılan yaptı.", Theme.Switch);
        Shot(o, "osd.png", 300);
    }
}

