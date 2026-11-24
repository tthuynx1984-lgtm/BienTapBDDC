// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – bộ máy lập mẫu hồ sơ chạy ngầm trong công cụ. © Nguyễn Xuân Thủy
// Các mẫu đăng ký, hồ sơ, kết xuất dùng chung bộ mẫu chuẩn của M8 Mobile (BienTapBDDC.html), chạy bằng Edge / Chrome KHÔNG CỬA SỔ:
// công cụ gửi lệnh → bộ máy lập mẫu → tệp kết quả gửi về công cụ, lưu vào thư mục hồ sơ và mở ngay;
// hộp thoại của mẫu hiện bằng hộp thoại của công cụ; lệnh cần chạm thửa thì chạm trên bản vẽ MicroStation.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace M8Pro
{
    public static partial class Web
    {
        static Process eng; static IntPtr job = IntPtr.Zero; static System.Threading.Timer idle;
        static DateTime lastSeen = DateTime.MinValue, engStart = DateTime.MinValue, lastCmd = DateTime.MinValue, lastAct = DateTime.MinValue;
        static volatile bool noEngine;   // bộ máy không khởi động được trên máy này: dùng cửa sổ hồ sơ
        public static string LastOut = "";
        static bool EngAlive { get { try { return eng != null && !eng.HasExited; } catch { return false; } } }
        public static bool EngineRunning { get { return EngAlive; } }
        public static string EngineInfo() { var b = Browser(); return (b == null ? "chưa thấy Edge / Chrome trên máy" : "trình duyệt " + b) + (EngAlive ? " · bộ máy đang chạy" : " · bộ máy chưa chạy") + (noEngine ? " · không khởi động được, dùng cửa sổ hồ sơ" : ""); }
        static bool NgamOk { get { return Cfg.I.ChayNgam && !noEngine; } }
        static string Browser()
        {
            var env = Environment.GetEnvironmentVariable("BTB_BROWSER"); if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            var ds = new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("LOCALAPPDATA") };
            foreach (var rel in new[] { @"Microsoft\Edge\Application\msedge.exe", @"Google\Chrome\Application\chrome.exe" })
                foreach (var d in ds) { if (string.IsNullOrEmpty(d)) continue; var f = Path.Combine(d, rel); if (File.Exists(f)) return f; }
            return null;
        }
        // khởi động bộ máy lập mẫu (nếu chưa chạy); false: máy không có Edge / Chrome
        public static bool Engine()
        {
            if (EngAlive) return true;
            KillEngine();
            var exe = Browser(); if (exe == null || !Start()) return false;
            try
            {
                var dir = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "M8Pro"), "maylapmau"); Directory.CreateDirectory(dir);
                var args = "--headless --disable-gpu --no-first-run --no-default-browser-check --disable-extensions --disable-sync --mute-audio --window-size=1280,900 --user-data-dir=\"" + dir + "\" \"http://127.0.0.1:" + Cong + "/?may=1\"";
                eng = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true }); engStart = DateTime.UtcNow; lastAct = DateTime.UtcNow; KillWithUs(eng);
                AppDomain.CurrentDomain.ProcessExit -= OnExit; AppDomain.CurrentDomain.ProcessExit += OnExit;
                // nghỉ lâu không lập hồ sơ: tắt bộ máy cho nhẹ máy (lần sau tự mở lại)
                if (idle == null) idle = new System.Threading.Timer(_ => { try { if (EngAlive && Pending() == 0 && (DateTime.UtcNow - Max(lastCmd, lastAct)).TotalMinutes > 20) KillEngine(); } catch { } }, null, 60000, 60000);
                return true;
            }
            catch (Exception ex) { Program.Log(ex); eng = null; return false; }
        }
        static DateTime Max(DateTime a, DateTime b) { return a > b ? a : b; }
        static void OnExit(object s, EventArgs e) { KillEngine(); }
        public static void KillEngine() { var p = eng; eng = null; try { if (p != null && !p.HasExited) p.Kill(); } catch { } }
        // mở menu Đăng ký / Hồ sơ / Kết xuất: khởi động sẵn bộ máy và nạp sẵn tờ bản đồ để lệnh chạy nhanh
        public static void Warm()
        {
            try
            {
                if (!NgamOk || Html == null || string.IsNullOrEmpty(Tep) || !Start() || !Engine()) return;
                lock (lk) { if (Q.Count == 0) Q.Enqueue("{\"id\":" + Interlocked.Increment(ref seq) + ",\"cmd\":\"nap\",\"tep\":" + J(Tep) + "}"); }
            }
            catch (Exception ex) { Program.Log(ex); }
        }
        // bộ máy không nhận lệnh (trình duyệt bị chặn chế độ không cửa sổ…): mở cửa sổ hồ sơ để lập
        static void WatchEngine(string j)
        {
            var t0 = DateTime.UtcNow;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                for (int i = 0; i < 90; i++) { Thread.Sleep(500); lock (lk) { if (!Q.Contains(j)) return; } }
                if (lastSeen > t0 || (lastSeen > engStart && EngAlive)) return;   // bộ máy vẫn chạy, đang bận lệnh trước
                noEngine = true; KillEngine();
                Program.Ui(() => { var m = Program.Main_; if (m != null) m.Msg("Bộ máy lập mẫu chạy ngầm chưa khởi động được trên máy này — mở cửa sổ hồ sơ để lập"); Show(false); });
            });
        }

        // thư mục lưu hồ sơ: theo thiết lập, hoặc HoSo cạnh tệp bản đồ
        public static string OutDir()
        {
            var c = (Cfg.I.ThuMucHoSo ?? "").Trim(); if (c != "") return c;
            var t = Tep; if (!string.IsNullOrEmpty(t)) try { return Path.Combine(Path.GetDirectoryName(t), "HoSo"); } catch { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HoSo");
        }
        static string SafeName(string n)
        {
            n = (n ?? "").Replace('/', '_').Replace('\\', '_').Trim(); foreach (var ch in Path.GetInvalidFileNameChars()) n = n.Replace(ch, '_');
            n = n.Trim('.', ' '); if (n.Length > 150) { var e = Path.GetExtension(n); n = n.Substring(0, 150 - e.Length) + e; } return n == "" ? "ho-so" : n;
        }
        static string Unique(string f)
        {
            if (!File.Exists(f)) return f; var d = Path.GetDirectoryName(f); var b = Path.GetFileNameWithoutExtension(f); var e = Path.GetExtension(f);
            for (int i = 2; i < 1000; i++) { var g = Path.Combine(d, b + " (" + i + ")" + e); if (!File.Exists(g)) return g; } return f;
        }
        // tệp kết quả từ bộ máy lập mẫu: lưu vào thư mục hồ sơ, mở bằng chương trình mặc định (tệp .dgn: mở thư mục, chọn sẵn tệp)
        static string SaveOut(string name, byte[] body)
        {
            var dir = OutDir(); Directory.CreateDirectory(dir); var f = Unique(Path.Combine(dir, SafeName(name))); File.WriteAllBytes(f, body); LastOut = f; lastAct = DateTime.UtcNow;
            Program.Ui(() =>
            {
                var m = Program.Main_; if (m != null && !m.IsDisposed) m.Msg("📄 Đã lập " + Path.GetFileName(f) + " – lưu tại " + dir);
                if (!Cfg.I.MoHoSo) return; var e = Path.GetExtension(f).ToLowerInvariant();
                try { if (e == ".dgn" || e == ".dxf" || e == ".zip" || e == ".shp") Process.Start("explorer.exe", "/select,\"" + f + "\""); else Process.Start(f); }
                catch (Exception ex) { Program.Log(ex); try { Process.Start("explorer.exe", "/select,\"" + f + "\""); } catch { } }
            });
            return f;
        }
        public static void OpenOutDir()
        {
            var d = OutDir(); try { Directory.CreateDirectory(d); } catch { }
            try { if (LastOut != "" && File.Exists(LastOut) && string.Equals(Path.GetDirectoryName(LastOut), d, StringComparison.OrdinalIgnoreCase)) Process.Start("explorer.exe", "/select,\"" + LastOut + "\""); else Process.Start("explorer.exe", "\"" + d + "\""); }
            catch (Exception ex) { Program.Log(ex); }
        }

        // ---------- câu hỏi của bộ máy lập mẫu cho người dùng ----------
        // trả lời trong 20 giây thì gửi ngay, chưa xong thì bộ máy hỏi lại theo mã câu hỏi (yêu cầu không treo lâu)
        class HoiQ { public string Ans; public readonly ManualResetEvent Done = new ManualResetEvent(false); }
        static readonly Dictionary<int, HoiQ> hoiQ = new Dictionary<int, HoiQ>(); static int hoiSeq;
        static int Pending() { lock (hoiQ) return hoiQ.Count; }
        static string HoiNew(Dictionary<string, object> q)
        {
            var h = new HoiQ(); int id; lock (hoiQ) { id = ++hoiSeq; hoiQ[id] = h; }
            int once = 0; Action<string> fin = a => { if (Interlocked.Exchange(ref once, 1) != 0) return; h.Ans = a; h.Done.Set(); };
            var thu = HoiDlg.Thu;
            if (thu != null) { string a0 = "{\"i\":null,\"v\":null,\"x\":null}"; try { a0 = thu(q); } catch (Exception ex) { Program.Log(ex); } fin(a0); }
            else if (MiniJson.S(q, "kind") == "pick")
            {
                var msg = MiniJson.S(q, "msg"); if (msg == "") msg = "Chạm vào thửa cần lập hồ sơ";
                Program.Ui(() =>
                {
                    var m = Program.Main_; if (m == null || m.IsDisposed) { fin("{\"x\":null}"); return; }
                    try { m.PickFor(msg, p => fin("{\"x\":" + N(p.Lp[0]) + ",\"y\":" + N(p.Lp[1]) + ",\"so\":" + J(p.So) + "}"), () => fin("{\"x\":null}")); }
                    catch (Exception ex) { Program.Log(ex); fin("{\"x\":null}"); }
                });
            }
            else Program.Ui(() => { string a = "{\"i\":null,\"v\":null}"; try { a = HoiDlg.Answer(q); } catch (Exception ex) { Program.Log(ex); } fin(a); });
            return HoiWait(id);
        }
        static string HoiWait(int id)
        {
            HoiQ h; lock (hoiQ) hoiQ.TryGetValue(id, out h);
            if (h == null) return "{\"i\":null,\"v\":null,\"x\":null}";
            if (!h.Done.WaitOne(TimeSpan.FromSeconds(20))) return "{\"cho\":1,\"id\":" + id + "}";
            lock (hoiQ) hoiQ.Remove(id); lastAct = DateTime.UtcNow; return h.Ans ?? "{}";
        }
        static void Status(string t)
        {
            if (string.IsNullOrEmpty(t)) return; lastAct = DateTime.UtcNow; t = t.Length > 300 ? t.Substring(0, 300) + "…" : t;
            Program.Ui(() => { var m = Program.Main_; if (m != null && !m.IsDisposed) m.Msg("Hồ sơ: " + t); });
        }
        // các yêu cầu riêng của bộ máy lập mẫu (gọi từ Handle, đã kiểm tra mã phiên)
        static bool EngineApi(string p, Req r, System.Net.Sockets.NetworkStream s)
        {
            switch (p)
            {
                case "/api/ketqua":
                    {
                        if (r.Method != "POST") { Text(s, 405, "Chỉ nhận POST"); return true; }
                        string ten; r.Q.TryGetValue("ten", out ten); if (r.Body == null || r.Body.Length == 0) { Text(s, 500, "Tệp rỗng"); return true; }
                        var f = SaveOut(ten, r.Body); Json(s, "{\"ok\":true,\"tep\":" + J(f) + "}"); return true;
                    }
                case "/api/hoi":
                    {
                        string ids; int id;
                        if (r.Q.TryGetValue("id", out ids) && int.TryParse(ids, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) { Json(s, HoiWait(id)); return true; }
                        var q = MiniJson.Parse(U8.GetString(r.Body ?? new byte[0])) as Dictionary<string, object>;
                        Json(s, q == null ? "{\"i\":null,\"v\":null}" : HoiNew(q)); return true;
                    }
                case "/api/st": Status(U8.GetString(r.Body ?? new byte[0])); Json(s, "{\"ok\":true}"); return true;
            }
            return false;
        }

        // bộ máy (và các tiến trình con) tự tắt khi MicroStation tắt: nhóm tiến trình "tắt cùng" (Windows 8 trở lên)
        [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr a, string n);
        [DllImport("kernel32")] static extern bool SetInformationJobObject(IntPtr j, int cls, IntPtr info, uint len);
        [DllImport("kernel32")] static extern bool AssignProcessToJobObject(IntPtr j, IntPtr p);
        [StructLayout(LayoutKind.Sequential)] struct BASIC { public long a, b; public uint LimitFlags; public UIntPtr c, d; public uint e; public UIntPtr f; public uint g, h; }
        [StructLayout(LayoutKind.Sequential)] struct IOC { public ulong a, b, c, d, e, f; }
        [StructLayout(LayoutKind.Sequential)] struct EXT { public BASIC B; public IOC I; public UIntPtr a, b, c, d; }
        static void KillWithUs(Process p)
        {
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT || Environment.OSVersion.Version < new Version(6, 2)) return;
                if (job == IntPtr.Zero)
                {
                    var j = CreateJobObject(IntPtr.Zero, null); if (j == IntPtr.Zero) return;
                    var x = new EXT(); x.B.LimitFlags = 0x2000; int n = Marshal.SizeOf(typeof(EXT)); var m = Marshal.AllocHGlobal(n);
                    try { Marshal.StructureToPtr(x, m, false); if (!SetInformationJobObject(j, 9, m, (uint)n)) return; } finally { Marshal.FreeHGlobal(m); }
                    job = j;
                }
                AssignProcessToJobObject(job, p.Handle);
            }
            catch { }
        }
    }

    // ---------- JSON tối giản (đọc) ----------
    public static class MiniJson
    {
        public static object Parse(string s) { int i = 0; try { return Val(s, ref i); } catch { return null; } }
        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        static object Val(string s, ref int i)
        {
            Ws(s, ref i); if (i >= s.Length) return null; char c = s[i];
            if (c == '{')
            {
                i++; var o = new Dictionary<string, object>(); Ws(s, ref i); if (s[i] == '}') { i++; return o; }
                while (true) { Ws(s, ref i); var k = Str(s, ref i); Ws(s, ref i); i++; o[k] = Val(s, ref i); Ws(s, ref i); if (s[i] == ',') { i++; continue; } i++; return o; }
            }
            if (c == '[')
            {
                i++; var a = new List<object>(); Ws(s, ref i); if (s[i] == ']') { i++; return a; }
                while (true) { a.Add(Val(s, ref i)); Ws(s, ref i); if (s[i] == ',') { i++; continue; } i++; return a; }
            }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int b = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == b) throw new FormatException("JSON"); double d; double.TryParse(s.Substring(b, i - b), NumberStyles.Float, CultureInfo.InvariantCulture, out d); return d;
        }
        static string Str(string s, ref int i)
        {
            var b = new StringBuilder(); i++;
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char e = s[++i];
                    if (e == 'u' && i + 4 < s.Length) { b.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else b.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                }
                else b.Append(s[i]);
                i++;
            }
            i++; return b.ToString();
        }
        public static string S(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : ""; }
        public static bool B(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is bool && (bool)v; }
        public static List<object> L(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) ? v as List<object> ?? new List<object>() : new List<object>(); }
    }

    // ---------- hộp thoại của công cụ cho các mẫu hồ sơ ----------
    public static class HoiDlg
    {
        public static Func<Dictionary<string, object>, string> Thu;   // chạy thử tự động: trả lời thay người dùng
        static Form Owner() { var m = Program.Main_; return m != null && !m.IsDisposed ? (Form)m : null; }
        static string Clean(string t) { return (t ?? "").Replace("\r", "").Trim(); }
        public static string Answer(Dictionary<string, object> q)
        {
            var kind = MiniJson.S(q, "kind");
            if (kind == "msg")
            {
                var msg = Clean(MiniJson.S(q, "msg"));
                if (MiniJson.B(q, "input")) { var v = Input(Bt.Ten, msg, MiniJson.S(q, "def")); return "{\"v\":" + (v == null ? "null" : Web.J(v)) + "}"; }
                if (MiniJson.B(q, "cancel")) return MessageBox.Show(Owner(), msg, Bt.Ten, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK ? "{\"v\":true}" : "{\"v\":null}";
                MessageBox.Show(Owner(), msg, Bt.Ten, MessageBoxButtons.OK, MessageBoxIcon.Information); return "{\"v\":true}";
            }
            if (kind == "lic")
            {
                var v = Input("Kích hoạt bản quyền mẫu hồ sơ M8", Clean(MiniJson.S(q, "msg")) + "\n\nNhập mã kích hoạt (bắt đầu bằng MAPDGN-):", "");
                return "{\"v\":" + (v == null ? "null" : Web.J(v)) + "}";
            }
            return Form_(q);
        }
        static Form NewForm(string title)
        {
            var f = new Form { Text = title, Font = new Font("Segoe UI", 9f), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, StartPosition = FormStartPosition.CenterScreen, TopMost = true };
            GiaoDien.KhongCat(f); return f;   // co giãn được, không nhỏ hơn cỡ ban đầu
        }
        // hộp nhập một dòng, lời nhắn dài tự xuống dòng
        public static string Input(string title, string msg, string def)
        {
            using (var f = NewForm(title))
            {
                var lb = new Label { Text = msg, AutoSize = true, MaximumSize = new Size(460, 0), Location = new Point(12, 12) }; f.Controls.Add(lb);
                var sz = lb.GetPreferredSize(new Size(460, 0)); var tb = new TextBox { Text = def ?? "", Location = new Point(12, 20 + sz.Height), Width = 460, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right }; f.Controls.Add(tb);
                var ok = new Button { Text = "Đồng ý", DialogResult = DialogResult.OK, Size = new Size(90, 28), Location = new Point(282, tb.Bottom + 12), Anchor = AnchorStyles.Top | AnchorStyles.Right };
                var no = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Size = new Size(90, 28), Location = new Point(382, tb.Bottom + 12), Anchor = AnchorStyles.Top | AnchorStyles.Right };
                f.Controls.Add(ok); f.Controls.Add(no); f.AcceptButton = ok; f.CancelButton = no; f.ClientSize = new Size(484, ok.Bottom + 12);
                f.Shown += (s, e) => { tb.Focus(); tb.SelectAll(); };
                return f.ShowDialog(Owner()) == DialogResult.OK ? tb.Text : null;
            }
        }
        // hộp thoại có ô nhập như mẫu: ô chữ, danh sách chọn, ô đánh dấu; nhiều ô thì cuộn (đặt chỗ từng ô, không phụ thuộc bảng tự co giãn)
        static string Form_(Dictionary<string, object> q)
        {
            var fields = MiniJson.L(q, "fields"); var btns = MiniJson.L(q, "btns");
            if (btns.Count == 0) btns.Add(new Dictionary<string, object> { { "t", "Đóng" }, { "p", true } });
            using (var f = NewForm(Clean(MiniJson.S(q, "title"))))
            {
                int IW = 330; const int GAP = 14; var fnt = f.Font;
                // bề rộng cột tên ô: theo tên dài nhất (tối đa 260)
                int labW = 120;
                foreach (var o in fields) { var d = o as Dictionary<string, object>; if (d == null || MiniJson.S(d, "type") == "check") continue; labW = Math.Max(labW, Math.Min(260, TextRenderer.MeasureText(MiniJson.S(d, "label"), fnt).Width + 4)); }
                // ô chọn: rộng vừa lựa chọn dài nhất (tối đa 560) để chữ không bị cắt
                foreach (var o in fields) { var d = o as Dictionary<string, object>; if (d == null || MiniJson.S(d, "type") != "select") continue; foreach (var op in MiniJson.L(d, "options")) { var od = op as Dictionary<string, object>; if (od != null) IW = Math.Max(IW, Math.Min(560, TextRenderer.MeasureText(MiniJson.S(od, "t"), fnt).Width + 34)); } }
                int W = labW + GAP + IW, y = 0;
                var sp = new Panel { AutoScroll = true, Location = new Point(12, 12), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
                Func<string, int, int> hText = (tx, wd) => TextRenderer.MeasureText(tx, fnt, new Size(wd, 0), TextFormatFlags.WordBreak).Height;
                var text = Clean(MiniJson.S(q, "text"));
                if (text != "") { int h = hText(text, W) + 4; sp.Controls.Add(new Label { Text = text, AutoSize = false, Location = new Point(0, y), Size = new Size(W, h), ForeColor = Color.FromArgb(60, 60, 60) }); y += h + 10; }
                var get = new List<KeyValuePair<string, Func<object>>>(); Control first = null;
                foreach (var o in fields)
                {
                    var d = o as Dictionary<string, object>; if (d == null) continue; var k = MiniJson.S(d, "k"); if (k == "") continue;
                    var ty = MiniJson.S(d, "type"); var lab = MiniJson.S(d, "label"); object val; d.TryGetValue("value", out val);
                    if (ty == "check")
                    {
                        int h = Math.Max(22, hText(lab, W - 22) + 6);
                        var cb = new OTich { Text = lab, AutoSize = false, Location = new Point(0, y), Size = new Size(W, h), Checked = val is bool && (bool)val, TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                        sp.Controls.Add(cb); get.Add(new KeyValuePair<string, Func<object>>(k, () => cb.Checked)); y += h + 4; continue;
                    }
                    Control input; Func<object> rd;
                    if (ty == "select")
                    {
                        var cbo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = IW, DropDownWidth = Math.Max(IW, 440) }; var vs = new List<string>(); int sel = 0; var cur = Convert.ToString(val, CultureInfo.InvariantCulture);
                        foreach (var op in MiniJson.L(d, "options")) { var od = op as Dictionary<string, object>; if (od == null) continue; vs.Add(MiniJson.S(od, "v")); cbo.Items.Add(MiniJson.S(od, "t")); if (MiniJson.S(od, "v") == cur) sel = vs.Count - 1; }
                        if (cbo.Items.Count > 0) cbo.SelectedIndex = sel; input = cbo; rd = () => cbo.SelectedIndex >= 0 && cbo.SelectedIndex < vs.Count ? vs[cbo.SelectedIndex] : "";
                    }
                    else
                    {
                        var tb = new TextBox { Width = IW, Text = Convert.ToString(val, CultureInfo.InvariantCulture) ?? "" };
                        if (ty == "textarea") { tb.Multiline = true; tb.Height = 70; tb.ScrollBars = ScrollBars.Vertical; }
                        input = tb; rd = () => tb.Text;
                    }
                    int lh = hText(lab, labW), rh = Math.Max(lh, input.Height);
                    sp.Controls.Add(new Label { Text = lab, AutoSize = false, Location = new Point(0, y + Math.Max(0, (Math.Min(rh, 2 * input.Height) - lh) / 2)), Size = new Size(labW, lh + 2) });
                    input.Location = new Point(labW + GAP, y); input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; sp.Controls.Add(input); if (first == null) first = input;
                    get.Add(new KeyValuePair<string, Func<object>>(k, rd)); y += rh + 7;
                }
                // vùng ô nhập: quá cao thì cuộn
                int maxH = Math.Max(300, (int)(Screen.PrimaryScreen.WorkingArea.Height * .72)); bool scroll = y > maxH;
                sp.Size = new Size(W + (scroll ? SystemInformation.VerticalScrollBarWidth + 6 : 0), Math.Max(10, Math.Min(y, maxH))); f.Controls.Add(sp);
                var bp = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
                int pick = -1;
                for (int i = btns.Count - 1; i >= 0; i--)
                {
                    var bd = btns[i] as Dictionary<string, object>; var bt = new Button { Text = MiniJson.S(bd, "t"), AutoSize = true, MinimumSize = new Size(90, 28) }; int ix = i;
                    bt.Click += (s, e) => { pick = ix; f.DialogResult = DialogResult.OK; };
                    if (MiniJson.B(bd, "p")) { bt.Font = new Font(fnt, FontStyle.Bold); f.AcceptButton = bt; }
                    if (MiniJson.B(bd, "c")) f.CancelButton = bt;
                    bp.Controls.Add(bt);
                }
                f.Controls.Add(bp); var bs = bp.GetPreferredSize(Size.Empty);
                int w = Math.Max(sp.Width, bs.Width) + 24; bp.Location = new Point(w - 12 - bs.Width, sp.Bottom + 12); bp.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; f.ClientSize = new Size(w, bp.Location.Y + bs.Height + 12);
                if (first != null) f.Shown += (s, e) => first.Focus();
                var res = f.ShowDialog(Owner());
                if (res != DialogResult.OK || pick < 0) return "{\"i\":null}";
                var sb = new StringBuilder("{\"i\":" + pick + ",\"vals\":{"); bool fst = true;
                foreach (var kv in get)
                {
                    if (!fst) sb.Append(','); fst = false; var v = kv.Value();
                    sb.Append(Web.J(kv.Key)).Append(':').Append(v is bool ? ((bool)v ? "true" : "false") : Web.J(Convert.ToString(v, CultureInfo.InvariantCulture)));
                }
                return sb.Append("}}").ToString();
            }
        }
    }
}
