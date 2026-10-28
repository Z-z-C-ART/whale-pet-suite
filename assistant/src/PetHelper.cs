// 鲸鱼娘桌宠助手 —— 把「启动 / 停止 / 检查 / 隐藏 / 显示 / 开机自启」收进一个小窗口
// 编译器：C# 5（.NET Framework 4.x 自带 csc.exe），不依赖第三方库
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WhalePetHelper
{
    static class Cfg
    {
        public static readonly string UserHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static readonly string RuntimePath = Path.Combine(UserHome, @".dsh\dsh-pet\standalone-runtime.json");
        public static readonly string PetCmd = Path.Combine(AppData, @"npm\dsh-pet-standalone.cmd");
        public static readonly string LogoPath = Path.Combine(AppData, @"npm\node_modules\dsh-pet\assets\logo.png");
        public static readonly string StartupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        public static readonly string AutostartVbs = Path.Combine(StartupDir, "鲸鱼娘桌宠助手.vbs");
        public const string Owner = "鲸鱼娘助手";
        public const string HideLabel = "隐藏桌宠";
        public const string PetId = "main";
        public const int PortMin = 3080;
        public const int PortMax = 3115;
        public static string ExePath { get { return Application.ExecutablePath; } }
    }

    class PetRuntime
    {
        public int Port;
        public int Pid;
        public string Token;
    }

    class PetInstance
    {
        public int Port;
        public int Pid;
        public int Uptime;
    }

    static class Json
    {
        static readonly JavaScriptSerializer Ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        public static Dictionary<string, object> Parse(string s)
        {
            try { return Ser.Deserialize<Dictionary<string, object>>(s); }
            catch { return null; }
        }
        public static object Get(object root, params string[] path)
        {
            object cur = root;
            foreach (var key in path)
            {
                var d = cur as Dictionary<string, object>;
                if (d == null || !d.ContainsKey(key)) return null;
                cur = d[key];
            }
            return cur;
        }
        public static string Str(object root, params string[] path)
        {
            var v = Get(root, path);
            return v == null ? null : Convert.ToString(v);
        }
    }

    static class Pet
    {
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };

        static async Task<string> GetText(string url, CancellationToken ct)
        {
            using (var resp = await Http.GetAsync(url, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        public static Task<string> HttpGet(string url, CancellationToken ct)
        {
            return GetText(url, ct);
        }

        public static PetRuntime ReadRuntime()
        {
            try
            {
                if (!File.Exists(Cfg.RuntimePath)) return null;
                var dict = Json.Parse(File.ReadAllText(Cfg.RuntimePath, Encoding.UTF8));
                if (dict == null) return null;
                var rt = new PetRuntime();
                rt.Port = Convert.ToInt32(Json.Get(dict, "port"));
                rt.Pid = Convert.ToInt32(Json.Get(dict, "pid"));
                rt.Token = Json.Str(dict, "token");
                return rt.Port > 0 ? rt : null;
            }
            catch { return null; }
        }

        static Dictionary<string, object> HealthInfo(int port, int timeoutMs)
        {
            try
            {
                using (var cts = new CancellationTokenSource(timeoutMs))
                {
                    var t = GetText("http://127.0.0.1:" + port + "/health", cts.Token);
                    t.Wait();
                    return Json.Parse(t.Result);
                }
            }
            catch { return null; }
        }

        static bool IsStandaloneInfo(Dictionary<string, object> dict)
        {
            return dict != null && Json.Str(dict, "name") == "dsh-pet-standalone";
        }

        public static bool Health(int port)
        {
            if (port <= 0) return false;
            return IsStandaloneInfo(HealthInfo(port, 800));
        }

        static readonly object InstanceGate = new object();
        static List<PetInstance> instanceCache = new List<PetInstance>();
        static DateTime instanceCacheAt = DateTime.MinValue;
        static bool starting;

        public static List<PetInstance> FindInstances(bool force)
        {
            lock (InstanceGate)
            {
                if (!force && (DateTime.Now - instanceCacheAt).TotalSeconds < 4) return new List<PetInstance>(instanceCache);
                var found = new List<PetInstance>();
                var tasks = new List<Task<PetInstance>>();
                for (int p = Cfg.PortMin; p <= Cfg.PortMax; p++)
                {
                    int port = p;
                    tasks.Add(Task.Run(delegate
                    {
                        var info = HealthInfo(port, 500);
                        if (!IsStandaloneInfo(info)) return null;
                        var inst = new PetInstance();
                        inst.Port = port;
                        try { inst.Pid = Convert.ToInt32(Json.Get(info, "pid")); } catch { inst.Pid = 0; }
                        try { inst.Uptime = Convert.ToInt32(Json.Get(info, "uptimeSec")); } catch { inst.Uptime = 0; }
                        return inst;
                    }));
                }
                try { Task.WaitAll(tasks.ToArray(), 6000); } catch { }
                foreach (var t in tasks) if (t.Status == TaskStatus.RanToCompletion && t.Result != null) found.Add(t.Result);
                found.Sort(delegate(PetInstance a, PetInstance b) { return a.Port.CompareTo(b.Port); });
                instanceCache = found;
                instanceCacheAt = DateTime.Now;
                return new List<PetInstance>(found);
            }
        }

        public static bool StandaloneAlive(out PetRuntime rt)
        {
            rt = ReadRuntime();
            return rt != null && Health(rt.Port);
        }

        static bool IsPidAlive(int pid) { try { var p = Process.GetProcessById(pid); return !p.HasExited; } catch { return false; } }

        static void KillByPid(int pid)
        {
            try { var p = Process.GetProcessById(pid); if (!p.HasExited) p.Kill(); } catch { }
        }

        public static int StopAllInstances()
        {
            var rt = ReadRuntime();
            var before = FindInstances(true);
            int stopped = before.Count;
            if (rt != null)
            {
                bool tracked = false;
                foreach (var inst in before) if (inst.Port == rt.Port) tracked = true;
                if (tracked)
                {
                    try
                    {
                        var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:" + rt.Port + "/shutdown");
                        req.Headers.Add("x-dsh-pet-token", rt.Token ?? "");
                        Http.SendAsync(req).Wait();
                    }
                    catch { }
                }
            }
            Thread.Sleep(1200);
            foreach (var inst in FindInstances(true)) KillByPid(inst.Pid);
            Thread.Sleep(600);
            var after = ReadRuntime();
            if (after != null && !IsPidAlive(after.Pid))
            {
                try { File.Delete(Cfg.RuntimePath); } catch { }
            }
            instanceCacheAt = DateTime.MinValue;
            return stopped;
        }

        static string SpawnStandalone()
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c \"" + Cfg.PetCmd + "\"");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(psi);
            return null;
        }

        public static string StartStandalone()
        {
            if (starting) return "正在启动中，请稍候…";
            starting = true;
            try
            {
                if (!File.Exists(Cfg.PetCmd)) return "找不到 dsh-pet-standalone.cmd，请先 npm install -g dsh-pet";
                var list = FindInstances(true);
                if (list.Count == 1)
                {
                    var rt = ReadRuntime();
                    if (rt != null && rt.Port == list[0].Port && !string.IsNullOrEmpty(rt.Token))
                    {
                        bool shown = EnsureVisible(list[0].Port, rt.Token);
                        return "独立桌宠已经在运行（端口 " + list[0].Port + (shown ? "，已确保显示" : "，但显示失败") + "）";
                    }
                    return RestartSingle(list[0]);
                }
                if (list.Count > 1)
                {
                    StopAllInstances();
                    Thread.Sleep(900);
                }

                SpawnStandalone();
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 45000)
                {
                    Thread.Sleep(600);
                    var found = FindInstances(true);
                    if (found.Count == 1)
                    {
                        var rt = ReadRuntime();
                        if (rt != null && rt.Port == found[0].Port)
                        {
                            EnsureVisible(found[0].Port, rt.Token);
                            return "已启动（端口 " + found[0].Port + "）";
                        }
                        return "已启动（端口 " + found[0].Port + "，运行时记录稍后生成）";
                    }
                    if (found.Count > 1)
                    {
                        StopAllInstances();
                        return "启动异常：出现了多个实例，已自动清理，请重试一次";
                    }
                }
                return "启动失败：45 秒内没检测到桌宠进程，请检查 dsh-pet 全局安装是否正常";
            }
            finally { starting = false; }
        }

        static string RestartSingle(PetInstance inst)
        {
            KillByPid(inst.Pid);
            Thread.Sleep(1200);
            try { File.Delete(Cfg.RuntimePath); } catch { }
            SpawnStandalone();
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 45000)
            {
                Thread.Sleep(600);
                var rt = ReadRuntime();
                if (rt != null && Health(rt.Port))
                {
                    EnsureVisible(rt.Port, rt.Token);
                    return "已重启并恢复运行时记录（端口 " + rt.Port + "）";
                }
            }
            return "已尝试重建运行时记录，但 45 秒内未就绪";
        }

        public static string StopStandalone()
        {
            var list = FindInstances(true);
            if (list.Count == 0) return "独立桌宠本来就没在运行";
            StopAllInstances();
            return "已停止独立桌宠（" + list.Count + " 个实例）";
        }
        public static string SetDisplay(int port, string display, string token, out bool ok)
        {
            ok = false;
            try
            {
                var body = "{\"id\":\"" + Cfg.PetId + "\",\"display\":\"" + display + "\"}";
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:" + port + "/dsh-pet-7340/pet-display");
                req.Content = content;
                if (!string.IsNullOrEmpty(token)) req.Headers.Add("x-dsh-pet-token", token);
                var resp = Http.SendAsync(req).Result;
                ok = resp.IsSuccessStatusCode;
                return ok ? null : "HTTP " + (int)resp.StatusCode;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public static bool EnsureVisible(int port, string token)
        {
            bool ok;
            SetDisplay(port, "desktop", token, out ok);
            return ok;
        }

        public static string GetDisplay(int port)
        {
            try
            {
                var t = GetText("http://127.0.0.1:" + port + "/dsh-pet-7340/config", CancellationToken.None);
                t.Wait();
                var dict = Json.Parse(t.Result);
                var pets = Json.Get(dict, "pets") as IList;
                if (pets == null)
                {
                    var main = Json.Get(dict, "main") as Dictionary<string, object>;
                    if (main != null) pets = Json.Get(main, "pets") as IList;
                }
                if (pets != null && pets.Count > 0) return Json.Str(pets[0], "display");
            }
            catch { }
            return null;
        }
        public static bool RestoreIfHidden(int port, string token)
        {
            if (GetDisplay(port) != "none") return false;
            bool ok;
            SetDisplay(port, "desktop", token, out ok);
            return ok;
        }
        public static Dictionary<string, object> State(int port)
        {
            try
            {
                var t = GetText("http://127.0.0.1:" + port + "/dsh-pet-7340/state", CancellationToken.None);
                t.Wait();
                return Json.Parse(t.Result);
            }
            catch { return null; }
        }

        public static string RegisterMenu(int port, string token)
        {
            try
            {
                var body = "{\"owner\":\"" + Cfg.Owner + "\",\"labels\":[\"" + Cfg.HideLabel + "\"]}";
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:" + port + "/dsh-pet-7340/menu/item");
                req.Content = content;
                if (!string.IsNullOrEmpty(token)) req.Headers.Add("x-dsh-pet-token", token);
                var resp = Http.SendAsync(req).Result;
                return resp.IsSuccessStatusCode ? null : "HTTP " + (int)resp.StatusCode;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public static string UnregisterMenu(int port, string token)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Delete, "http://127.0.0.1:" + port + "/dsh-pet-7340/menu/item?owner=" + Uri.EscapeDataString(Cfg.Owner));
                if (!string.IsNullOrEmpty(token)) req.Headers.Add("x-dsh-pet-token", token);
                Http.SendAsync(req).Wait();
                return null;
            }
            catch { return "unregister failed"; }
        }

        // 在监听端口里找 DSH 宿主（插件路由是公开的，直接探 /state）
        public static int DiscoverDsh(int excludePort)
        {
            var ports = new List<int>();
            try
            {
                foreach (var ep in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                {
                    if (ep.Port < 1024) continue;
                    if (excludePort > 0 && ep.Port == excludePort) continue;
                    if (ports.Contains(ep.Port)) continue;
                    ports.Add(ep.Port);
                }
            }
            catch { }
            if (ports.Count == 0) return 0;

            int found = 0;
            var tasks = new List<Task>();
            var gate = new SemaphoreSlim(16);
            foreach (var port in ports)
            {
                int p = port;
                tasks.Add(Task.Run(async delegate
                {
                    await gate.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        using (var cts = new CancellationTokenSource(450))
                        {
                            var s = await GetText("http://127.0.0.1:" + p + "/dsh-pet-7340/state", cts.Token).ConfigureAwait(false);
                            if (s != null && s.Contains("\"sections\"")) Interlocked.CompareExchange(ref found, p, 0);
                        }
                    }
                    catch { }
                    finally { gate.Release(); }
                }));
            }
            try { Task.WaitAll(tasks.ToArray(), 9000); } catch { }
            return found;
        }
    }

    class MenuWatcher
    {
        readonly Action onHide;
        readonly Action onRestored;
        volatile bool stopping;
        public MenuWatcher(Action onHide) : this(onHide, null) { }
        public MenuWatcher(Action onHide, Action onRestored) { this.onHide = onHide; this.onRestored = onRestored; }

        public void Stop() { stopping = true; }

        public void Start()
        {
            var t = new Thread(Loop);
            t.IsBackground = true;
            t.Start();
        }

        void Loop()
        {
            int dshPort = 0;
            int lastDshPort = 0;
            DateTime dshAt = DateTime.MinValue;
            while (!stopping)
            {
                PetRuntime rt;
                int stdPort = Pet.StandaloneAlive(out rt) ? rt.Port : 0;
                if ((DateTime.Now - dshAt).TotalSeconds > 30)
                {
                    dshPort = Pet.DiscoverDsh(stdPort);
                    dshAt = DateTime.Now;
                }

                if (dshPort > 0 && dshPort != lastDshPort)
                {
                    // 新发现一个 DSH 宿主（DSH 重启会换端口，或助手刚启动）：把上次的「隐藏」自动恢复
                    if (Pet.RestoreIfHidden(dshPort, null) && onRestored != null) onRestored();
                    lastDshPort = dshPort;
                }
                else if (dshPort == 0) lastDshPort = 0;

                bool polled = false;
                if (stdPort > 0) { PollOnce(stdPort, rt.Token); polled = true; }
                if (dshPort > 0 && dshPort != stdPort) { PollOnce(dshPort, null); polled = true; }
                if (!polled) Thread.Sleep(3000);
            }
        }

        void PollOnce(int port, string token)
        {
            if (Pet.RegisterMenu(port, token) != null) return;
            try
            {
                var url = "http://127.0.0.1:" + port + "/dsh-pet-7340/menu/events?owner=" + Uri.EscapeDataString(Cfg.Owner) + "&wait=15000";
                using (var cts = new CancellationTokenSource(20000))
                {
                    var t = Pet.HttpGet(url, cts.Token);
                    t.Wait();
                    var dict = Json.Parse(t.Result);
                    if (dict == null) return;
                    foreach (var kv in dict)
                    {
                        if (kv.Value != null) { onHide(); return; }
                    }
                }
            }
            catch { }
        }
    }

    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "WhalePetHelper";

        public static bool Enabled
        {
            get
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                        return k != null && k.GetValue(RunName) != null;
                }
                catch { return false; }
            }
        }

        public static void Enable()
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                k.SetValue(RunName, "\"" + Cfg.ExePath + "\" --tray");
        }

        public static void Disable()
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                if (k != null) k.DeleteValue(RunName, false);
        }
    }
    class MainForm : Form
    {
        readonly Label lblStd = new Label();
        readonly Label lblDsh = new Label();
        readonly Label lblPet = new Label();
        readonly Label lblAuto = new Label();
        readonly TextBox log = new TextBox();
        readonly CheckBox chkAuto = new CheckBox();
        readonly NotifyIcon tray;
        readonly MenuWatcher watcher;
        int cachedDshPort;
        DateTime dshFoundAt = DateTime.MinValue;

        public MainForm(bool startHidden)
        {
            Text = "鲸鱼娘桌宠助手";
            ClientSize = new Size(660, 470);
            MinimumSize = new Size(660, 470);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9.5f);
            BackColor = Color.FromArgb(248, 249, 252);
            Icon = LoadIcon();

            var title = new Label();
            title.Text = "🐋  鲸鱼娘桌宠助手";
            title.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(20, 16);
            Controls.Add(title);

            var card = new Panel();
            card.Location = new Point(20, 56);
            card.Size = new Size(620, 150);
            card.BackColor = Color.White;
            card.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(card);

            StyleStatus(lblStd, 14, 12);
            StyleStatus(lblDsh, 14, 52);
            StyleStatus(lblPet, 14, 92);
            card.Controls.Add(lblStd); card.Controls.Add(lblDsh); card.Controls.Add(lblPet);

            int y = 224;
            var startBtn = AddButton("启动桌宠", 20, y, 96, null);
            startBtn.Click += delegate { RunBusy(startBtn, delegate { return Pet.StartStandalone(); }); };
            var stopBtn = AddButton("停止桌宠", 124, y, 96, null);
            stopBtn.Click += delegate { RunBusy(stopBtn, delegate { return Pet.StopStandalone(); }); };
            AddButton("隐藏桌宠", 228, y, 96, delegate { HideAll(); });
            AddButton("显示桌宠", 332, y, 96, delegate { ShowAll(); });
            AddButton("刷新状态", 436, y, 96, delegate { RefreshStatus(); });
            var cleanBtn = AddButton("清理重复", 540, y, 100, null);
            cleanBtn.Click += delegate { RunBusy(cleanBtn, delegate { return CleanDuplicatesNow(); }); };

            y += 44;
            AddButton("打开 DSH", 20, y, 96, delegate { OpenDsh(); });
            AddButton("打开配置目录", 124, y, 120, delegate { OpenConfig(); });
            chkAuto.Text = "开机自动启动（静默到托盘）";
            chkAuto.AutoSize = true;
            chkAuto.Location = new Point(258, y + 8);
            chkAuto.Checked = Autostart.Enabled;
            chkAuto.CheckedChanged += delegate
            {
                try
                {
                    if (chkAuto.Checked) Autostart.Enable(); else Autostart.Disable();
                    AppendLog(chkAuto.Checked ? "已开启开机自启" : "已关闭开机自启（快捷方式进回收站）");
                }
                catch (Exception ex) { AppendLog("开机自启设置失败：" + ex.ToString()); }
                UpdateAutoLabel();
            };
            Controls.Add(chkAuto);

            y += 44;
            lblAuto.Text = "";
            lblAuto.AutoSize = true;
            lblAuto.ForeColor = Color.FromArgb(110, 116, 130);
            lblAuto.Location = new Point(20, y);
            Controls.Add(lblAuto);

            log.Location = new Point(20, y + 26);
            log.Size = new Size(620, 110);
            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.BackColor = Color.FromArgb(243, 245, 249);
            log.BorderStyle = BorderStyle.FixedSingle;
            log.Font = new Font("Consolas", 8.5f);
            Controls.Add(log);

            var tip = new Label();
            tip.Text = "右键桌宠 →「更多」→ 鲸鱼娘助手 → 隐藏桌宠；隐藏后从这里或托盘「显示桌宠」恢复。";
            tip.AutoSize = false;
            tip.Size = new Size(620, 34);
            tip.ForeColor = Color.FromArgb(120, 126, 140);
            tip.Location = new Point(20, y + 142);
            Controls.Add(tip);

            watcher = new MenuWatcher(HideAll, delegate
            {
                AppendLog("检测到 DSH 启动或助手重启：已自动恢复桌宠显示");
                RefreshStatus();
            });
            watcher.Start();

            tray = new NotifyIcon();
            tray.Icon = Icon;
            tray.Text = "鲸鱼娘桌宠助手";
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowPanel(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("打开面板", null, delegate { ShowPanel(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("显示桌宠", null, delegate { ShowAll(); });
            menu.Items.Add("隐藏桌宠", null, delegate { HideAll(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("启动独立桌宠", null, delegate { RunAsync(delegate { return Pet.StartStandalone(); }); });
            menu.Items.Add("停止独立桌宠", null, delegate { RunAsync(delegate { return Pet.StopStandalone(); }); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { ExitApp(); });
            tray.ContextMenuStrip = menu;

            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 2500;
            timer.Tick += delegate { RefreshStatus(); };
            timer.Start();

            Shown += delegate
            {
                if (startHidden) { Hide(); ShowInTaskbar = false; }
                RefreshStatus();
            };
            FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                    ShowInTaskbar = false;
                    tray.ShowBalloonTip(1500, "鲸鱼娘桌宠助手", "已最小化到托盘，双击图标可打开。", ToolTipIcon.Info);
                }
            };
            AppendLog("助手已启动。");

        }

        static void StyleStatus(Label l, int x, int y)
        {
            l.AutoSize = false;
            l.Size = new Size(486, 34);
            l.Location = new Point(x, y);
            l.Font = new Font("Microsoft YaHei UI", 10.5f);
            l.Text = "…";
        }

        Button AddButton(string text, int x, int y, int w, EventHandler onClick)
        {
            var b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, 34);
            b.FlatStyle = FlatStyle.System;
            if (onClick != null) b.Click += onClick;
            Controls.Add(b);
            return b;
        }

        void RunBusy(Button btn, Func<string> work)
        {
            btn.Enabled = false;
            Task.Run(delegate
            {
                string msg;
                try { msg = work(); }
                catch (Exception ex) { msg = "操作失败：" + ex.Message; }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        btn.Enabled = true;
                        AppendLog(msg);
                        RefreshStatus();
                    }));
                }
                catch { }
            });
        }

        Icon LoadIcon()
        {
            try
            {
                var embedded = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (embedded != null) return embedded;
            }
            catch { }
            try
            {
                if (File.Exists(Cfg.LogoPath))
                {
                    using (var bmp = new Bitmap(Cfg.LogoPath))
                    {
                        using (var small = new Bitmap(bmp, new Size(32, 32)))
                        {
                            IntPtr h = small.GetHicon();
                            using (var tmp = Icon.FromHandle(h))
                            {
                                return (Icon)tmp.Clone();
                            }
                        }
                    }
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        void ShowPanel()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();
            RefreshStatus();
        }

        void ExitApp()
        {
            watcher.Stop();
            try
            {
                // 退出前把「隐藏」恢复成可见，避免下次开 DSH 时因为 display=none 不出现
                var list = Pet.FindInstances(false);
                var rt = Pet.ReadRuntime();
                foreach (var inst in list)
                {
                    string token = (rt != null && rt.Port == inst.Port) ? rt.Token : null;
                    Pet.RestoreIfHidden(inst.Port, token);
                }
                if (cachedDshPort > 0) Pet.RestoreIfHidden(cachedDshPort, null);
                if (Pet.StandaloneAlive(out rt)) Pet.UnregisterMenu(rt.Port, rt.Token);
                if (cachedDshPort > 0) Pet.UnregisterMenu(cachedDshPort, null);
            }
            catch { }
            tray.Visible = false;
            tray.Dispose();
            Environment.Exit(0);
        }

        void AppendLog(string s)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(AppendLog), s); return; }
            log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + s + Environment.NewLine);
        }

        void RunAsync(Func<string> work)
        {
            Task.Run(delegate
            {
                string msg;
                try { msg = work(); }
                catch (Exception ex) { msg = "操作失败：" + ex.Message; }
                if (!string.IsNullOrEmpty(msg)) AppendLog(msg);
            });
        }

        void ApplyDisplayToAll(string display, string verb)
        {
            RunAsync(delegate
            {
                var list = Pet.FindInstances(true);
                var rt = Pet.ReadRuntime();
                int dsh = CurrentDshPort();
                var parts = new List<string>();
                if (list.Count == 0 && dsh <= 0) return "没有找到正在运行的桌宠";
                foreach (var inst in list)
                {
                    string token = (rt != null && rt.Port == inst.Port) ? rt.Token : null;
                    bool ok; var err = Pet.SetDisplay(inst.Port, display, token, out ok);
                    parts.Add(ok ? ("独立桌宠（" + inst.Port + "）已" + verb) : ("独立桌宠（" + inst.Port + "）" + verb + "失败：" + err));
                }
                if (dsh > 0)
                {
                    bool ok; var err = Pet.SetDisplay(dsh, display, null, out ok);
                    parts.Add(ok ? ("DSH 桌宠已" + verb) : ("DSH 桌宠" + verb + "失败：" + err));
                }
                if (list.Count > 1) parts.Add("检测到 " + list.Count + " 个独立实例，建议点「清理重复」");
                return string.Join("；", parts.ToArray());
            });
        }

        void HideAll() { ApplyDisplayToAll("none", "隐藏"); }

        void ShowAll() { ApplyDisplayToAll("desktop", "显示"); }

        string CleanDuplicatesNow()
        {
            int n = Pet.StopAllInstances();
            return n > 0 ? ("已清理 " + n + " 个独立桌宠实例") : "没有检测到独立桌宠实例";
        }
        int CurrentDshPort()
        {
            if (cachedDshPort > 0 && (DateTime.Now - dshFoundAt).TotalSeconds < 30) return cachedDshPort;
            PetRuntime rt;
            int std = Pet.StandaloneAlive(out rt) ? rt.Port : 0;
            int found = Pet.DiscoverDsh(std);
            cachedDshPort = found;
            dshFoundAt = DateTime.Now;
            return found;
        }

        void OpenDsh()
        {
            try
            {
                Process.Start(new ProcessStartInfo("dsh://open") { UseShellExecute = true });
                AppendLog("已请求唤起 DSH。");
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo(@"D:\dsh桌面版\DeepSeek Harness.exe") { UseShellExecute = true });
                    AppendLog("已启动 DSH 桌面版。");
                }
                catch (Exception ex) { AppendLog("打开 DSH 失败：" + ex.Message); }
            }
        }

        void OpenConfig()
        {
            try { Process.Start("explorer.exe", Path.Combine(Cfg.UserHome, @".dsh\dsh-pet")); }
            catch { }
        }

        void UpdateAutoLabel()
        {
            lblAuto.Text = Autostart.Enabled ? "开机自启：已开启（登录后静默运行）" : "开机自启：未开启";
        }

        void RefreshStatus()
        {
            Task.Run(delegate
            {
                var instances = Pet.FindInstances(false);
                int stdPort = instances.Count > 0 ? instances[0].Port : 0;
                int dsh;
                if (cachedDshPort > 0 && (DateTime.Now - dshFoundAt).TotalSeconds < 30) dsh = cachedDshPort;
                else { dsh = Pet.DiscoverDsh(stdPort); cachedDshPort = dsh; dshFoundAt = DateTime.Now; }

                string petInfo = "任务状态：—";
                if (dsh > 0)
                {
                    var st = Pet.State(dsh);
                    var work = Json.Str(st, "sections", "workStatus", "data", "state");
                    string money = null;
                    var wallets = Json.Get(st, "sections", "balance", "data", "data", "wallets") as IList;
                    if (wallets != null && wallets.Count > 0) money = Json.Str(wallets[0], "total");
                    petInfo = "任务状态：" + (work ?? "—") + (money != null ? "　余额：¥" + money : "");
                }

                string stdText;
                Color stdColor;
                if (instances.Count == 0)
                {
                    stdText = "独立桌宠：○ 未运行";
                    stdColor = Color.FromArgb(130, 136, 150);
                }
                else if (instances.Count == 1)
                {
                    string disp = Pet.GetDisplay(instances[0].Port);
                    if (disp == "none")
                    {
                        stdText = "独立桌宠：● 运行中（已隐藏）　端口 " + instances[0].Port;
                        stdColor = Color.FromArgb(200, 110, 20);
                    }
                    else
                    {
                        stdText = "独立桌宠：● 运行中　端口 " + instances[0].Port + "　PID " + instances[0].Pid;
                        stdColor = Color.FromArgb(28, 140, 90);
                    }
                }
                else
                {
                    var ports = new List<string>();
                    foreach (var i in instances) ports.Add(i.Port.ToString());
                    stdText = "独立桌宠：● 运行中 ×" + instances.Count + "（端口 " + string.Join(",", ports.ToArray()) + "）⚠ 重复";
                    stdColor = Color.FromArgb(200, 110, 20);
                }

                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        lblStd.Text = stdText;
                        lblStd.ForeColor = stdColor;
                        lblDsh.Text = dsh > 0 ? "DSH：● 已连接　端口 " + dsh : "DSH：○ 未运行（或未装桌宠插件）";
                        lblDsh.ForeColor = dsh > 0 ? Color.FromArgb(28, 140, 90) : Color.FromArgb(130, 136, 150);
                        lblPet.Text = petInfo;
                        lblPet.ForeColor = Color.FromArgb(60, 66, 80);
                        UpdateAutoLabel();
                    }));
                }
                catch { }
            });
        }
    }
    static class Program
    {
        static Mutex mutex;
        static EventWaitHandle showEvent;

        static string InstalledExe()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\WhalePetHelper\鲸鱼娘桌宠助手.exe");
        }

        // 从交付目录直接双击时，先自安装到 %LOCALAPPDATA%\Programs，再从那里启动。
        // 这样开机自启、注册表、开始菜单写入都不会被工作区的执行限制影响。
        static bool RelocateIfNeeded(string[] args)
        {
            try
            {
                var installed = InstalledExe();
                var current = Path.GetFullPath(Application.ExecutablePath);
                if (string.Equals(current, Path.GetFullPath(installed), StringComparison.OrdinalIgnoreCase)) return false;
                var dir = Path.GetDirectoryName(installed);
                Directory.CreateDirectory(dir);
                if (!File.Exists(installed) || File.GetLastWriteTimeUtc(current) > File.GetLastWriteTimeUtc(installed))
                    File.Copy(current, installed, true);
                var psi = new ProcessStartInfo(installed);
                foreach (var a in args) psi.Arguments += (psi.Arguments.Length > 0 ? " " : "") + a;
                psi.UseShellExecute = true;
                psi.WorkingDirectory = dir;
                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (RelocateIfNeeded(args)) return;
            bool created;
            mutex = new Mutex(true, "WhalePetHelper.SingleInstance", out created);
            if (!created)
            {
                try
                {
                    showEvent = EventWaitHandle.OpenExisting("WhalePetHelper.Show");
                    showEvent.Set();
                }
                catch { }
                return;
            }
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "WhalePetHelper.Show");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool startHidden = Array.IndexOf(args, "--tray") >= 0 || Array.IndexOf(args, "--autostart") >= 0;
            var form = new MainForm(startHidden);

            var waiter = new Thread(delegate()
            {
                while (true)
                {
                    showEvent.WaitOne();
                    try { form.BeginInvoke(new Action(delegate { form.Show(); form.ShowInTaskbar = true; form.WindowState = FormWindowState.Normal; form.Activate(); })); }
                    catch { }
                }
            });
            waiter.IsBackground = true;
            waiter.Start();

            if (startHidden)
            {
                Task.Run(delegate { Pet.StartStandalone(); });
                Application.Run();
            }
            else
            {
                Application.Run(form);
            }
        }
    }
}
