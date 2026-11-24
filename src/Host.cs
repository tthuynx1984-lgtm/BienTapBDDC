// M8 Pro Địa chính – nạp vào MicroStation V8i như một ứng dụng nạp (AddIn .NET). © Nguyễn Xuân Thủy
//   mdl load M8ProDiaChinh            (khi tệp nằm trong thư mục ứng dụng của MicroStation)
//   mdl load D:\M8Pro\M8ProDiaChinh.dll
// Lệnh gõ (key-in) dùng để gắn vào menu / thanh công cụ của MicroStation:
//   M8PRO MO | M8PRO NGAT | M8PRO DONG | M8PRO MENU | M8PRO TAOTHUA | M8PRO BANG | M8PRO KIEMTRA | M8PRO NHAN | M8PRO CHONTHUA | M8PRO TACH | M8PRO GOP | M8PRO TRICHLUC | M8PRO THIETLAP
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace M8Pro
{
#if ADDIN && ADAPTER
    // cửa sổ nằm trong MicroStation: dùng lớp cửa sổ của MicroStation để bàn phím, tiêu điểm hoạt động đúng
    public class FormBase : Bentley.MicroStation.WinForms.Adapter
    {
        // gắn làm cửa sổ cấp cao của MicroStation (luôn nổi trên cửa sổ bản vẽ)
        public bool AttachMs()
        {
            try
            {
                var m = typeof(Bentley.MicroStation.WinForms.Adapter).GetMethods().FirstOrDefault(x => x.Name == "AttachAsTopLevelForm" && x.GetParameters().Length == 2);
                if (m != null && Host.Addin != null) { m.Invoke(this, new object[] { Host.Addin, false }); return true; }
            }
            catch { }
            return false;
        }
    }

#elif ADDIN
    public class FormBase : Form { public bool AttachMs() { return false; } }
#endif
#if ADDIN
    [Bentley.MicroStation.AddIn(MdlTaskID = "M8PRO", KeyinTree = "M8ProDiaChinh.commands.xml")]
    public sealed class M8ProAddin : Bentley.MicroStation.AddIn
    {
        private M8ProAddin(IntPtr mdlDesc) : base(mdlDesc) { }
        protected override int Run(string[] commandLine)
        {
            Host.Addin = this; CapNhat.DanhDau();
            try { Ms.App = Bentley.MicroStation.InteropServices.Utilities.ComApp; } catch { }
            AnhNen.GanSuKien(this);   // đóng tệp bản đồ: tự gỡ ảnh vệ tinh nền
            try { MsMenu.Install(); } catch (Exception ex) { Host.MenuMode = "lỗi: " + ex.Message; Program.Log(ex); }
            if (Cfg.I.TuMo) Host.Open(); else Ms.Status(Bt.Ten + " đã nạp – mở từ menu Biên tập BĐĐC");
            return 0;
        }
    }

    // các lệnh gõ – mỗi hàm nhận phần chữ còn lại của lệnh
    public static class Keyins
    {
        public static void Mo(string u) { Host.Open(); }
        public static void Ngat(string u) { Host.Disconnect(); }
        public static void Dong(string u) { Host.CloseApp(); }
        public static void Menu(string u) { try { MsMenu.Install(); Ms.Status("Biên tập BĐĐC: " + Host.MenuMode); } catch (Exception ex) { Ms.Status("Biên tập BĐĐC: " + ex.Message); } }
        public static void TaoThua(string u) { Host.Cmd("Tạo thửa"); }
        public static void SuaLoi(string u) { Host.Cmd("Sửa lỗi tự động"); }
        public static void VeNhan(string u) { Host.Cmd("Vẽ nhãn"); }
        // thư mục hồ sơ vừa lập; "cuaso": mở cửa sổ hồ sơ (bộ mẫu M8 Mobile có giao diện) để xem, dò lỗi
        public static void HoSo(string u) { Host.Open(); if ((u ?? "").Trim().ToLowerInvariant().StartsWith("cua")) Web.Show(true); else Web.OpenOutDir(); }
        public static void Phieu17(string u) { Host.Cmd("Phiếu đo đạc chỉnh lý thửa đất (Phụ lục 17)"); }
        public static void SoDo(string u) { Host.Cmd("Sơ đồ thửa đất (DGN): ranh thửa level 10, số thửa level 13, loại đất level 2, diện tích level 4"); }
        public static void Bang(string u) { Host.Cmd("Bảng thông tin thuộc tính"); }
        public static void KetNoiGtp(string u) { Host.Cmd("Kết nối tệp thuộc tính (.gtp)…"); }
        public static void DonViHC(string u) { Host.Cmd("Thiết lập đơn vị hành chính…"); }
        public static void KiemTra(string u) { Host.Cmd("Tìm và sửa lỗi"); }
        public static void Nhan(string u) { Host.Cmd("Vẽ nhãn thửa chọn"); }
        public static void ChonThua(string u) { Host.Cmd("Chọn thửa trên bản đồ"); }
        public static void Tach(string u) { Host.Cmd("Tách thửa"); }
        public static void Gop(string u) { Host.Cmd("Hợp thửa"); }
        public static void Hop(string u) { Host.Cmd("Hợp thửa"); }
        public static void BanVe(string u) { Host.Cmd("Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22)"); }
        public static void Google(string u) { Host.Cmd(AnhNen.TEN); }
        public static void AnhNen_(string u) { Host.Cmd(AnhNen.TEN); }
        public static void NenMau_(string u) { Host.Cmd(NenMau.TEN); }
        public static void TrichLuc(string u) { Host.Cmd("Trích lục bản đồ địa chính (Phụ lục 19)"); }
        public static void ThietLap(string u) { Host.Cmd("Thiết lập (tờ bản đồ, level, nhãn…)"); }
        public static void CapNhatBanMoi(string u) { M8Pro.CapNhat.Mo(); }
    }

    // ================= MENU "M8 Pro" TRÊN MICROSTATION =================
    // Cách 1: thêm thẳng vào thanh menu của cửa sổ MicroStation (nếu là menu chuẩn Windows).
    // Cách 2: nút "M8 Pro ▾" gắn ở cuối hàng menu, luôn đi theo cửa sổ MicroStation.
    public static class MsMenu
    {
        [DllImport("user32")] static extern IntPtr GetMenu(IntPtr h);
        [DllImport("user32")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32", CharSet = CharSet.Unicode)] static extern bool AppendMenuW(IntPtr m, uint f, UIntPtr id, string s);
        [DllImport("user32")] static extern bool DrawMenuBar(IntPtr h);
        [DllImport("user32")] static extern int GetMenuItemCount(IntPtr m);
        [DllImport("user32")] static extern bool DeleteMenu(IntPtr m, uint pos, uint f);
        [DllImport("user32")] static extern IntPtr GetSubMenu(IntPtr m, int pos);
        [DllImport("user32")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32")] public static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32")] public static extern bool ClientToScreen(IntPtr h, ref Point p);
        [DllImport("user32")] static extern bool EnumWindows(EnumProc f, IntPtr l);
        [DllImport("user32")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, System.Text.StringBuilder s, int n);
        [DllImport("user32")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
        const uint MF_STRING = 0, MF_POPUP = 0x10, MF_SEPARATOR = 0x800, MF_BYCOMMAND = 0, MF_BYPOSITION = 0x400;
        const int ID0 = 0xE7A0;

        // mục menu: tên hiển thị → việc làm (null = đường kẻ)
        public static List<KeyValuePair<string, Action>> Items()
        {
            Func<string, Action> C = n => () => Host.Cmd(n);
            return new List<KeyValuePair<string, Action>> {
                new KeyValuePair<string, Action>("Mở " + Bt.Ten, Host.Open),
                new KeyValuePair<string, Action>("Kết nối lại với bản đồ đang mở", Host.Reconnect),
                new KeyValuePair<string, Action>("Ngắt kết nối", Host.Disconnect),
                new KeyValuePair<string, Action>(null, null),
                new KeyValuePair<string, Action>("Sửa lỗi tự động", C("Sửa lỗi tự động")),
                new KeyValuePair<string, Action>("Tìm và sửa lỗi", C("Tìm và sửa lỗi")),
                new KeyValuePair<string, Action>("Tạo thửa", C("Tạo thửa")),
                new KeyValuePair<string, Action>("Vẽ nhãn", C("Vẽ nhãn")),
                new KeyValuePair<string, Action>("Chọn thửa trên bản đồ", C("Chọn thửa trên bản đồ")),
                new KeyValuePair<string, Action>("Bảng thông tin thuộc tính", C("Bảng thông tin thuộc tính")),
                new KeyValuePair<string, Action>("Kết nối tệp thuộc tính (.gtp)", C("Kết nối tệp thuộc tính (.gtp)…")),
                new KeyValuePair<string, Action>("Thiết lập đơn vị hành chính", C("Thiết lập đơn vị hành chính…")),
                new KeyValuePair<string, Action>(null, null),
                new KeyValuePair<string, Action>("Mở thư mục hồ sơ", Web.OpenOutDir),
                new KeyValuePair<string, Action>("Tách thửa", C("Tách thửa")),
                new KeyValuePair<string, Action>("Hợp thửa", C("Hợp thửa")),
                new KeyValuePair<string, Action>("Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22)", C("Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22)")),
                new KeyValuePair<string, Action>("Ảnh vệ tinh nền (bật / tắt)", C(AnhNen.TEN)),
                new KeyValuePair<string, Action>("Nền màu loại đất (bật / tắt)", C(NenMau.TEN)),
                new KeyValuePair<string, Action>("Phiếu đo đạc chỉnh lý thửa đất (PL17)", C("Phiếu đo đạc chỉnh lý thửa đất (Phụ lục 17)")),
                new KeyValuePair<string, Action>("Trích lục bản đồ địa chính (PL19)", C("Trích lục bản đồ địa chính (Phụ lục 19)")),
                new KeyValuePair<string, Action>(null, null),
                new KeyValuePair<string, Action>("Thiết lập", C("Thiết lập (tờ bản đồ, level, nhãn…)")),
                new KeyValuePair<string, Action>("Chẩn đoán kết nối", C("Chẩn đoán kết nối và đọc dữ liệu")),
                new KeyValuePair<string, Action>("Cập nhật phiên bản mới…", CapNhat.Mo),
                new KeyValuePair<string, Action>("Đóng " + Bt.Ten, Host.CloseApp) };
        }

        // cửa sổ chính của MicroStation
        public static IntPtr MainWnd()
        {
            var me = Process.GetCurrentProcess(); var h = me.MainWindowHandle; if (h != IntPtr.Zero && Title(h).IndexOf("MicroStation", StringComparison.OrdinalIgnoreCase) >= 0) return h;
            IntPtr best = IntPtr.Zero; long area = 0; uint id = (uint)me.Id;
            EnumWindows((w, l) =>
            {
                uint pid; GetWindowThreadProcessId(w, out pid); if (pid != id || !IsWindowVisible(w) || GetWindow(w, 4) != IntPtr.Zero) return true;   // 4 = GW_OWNER: bỏ cửa sổ con nổi
                if (Title(w).IndexOf("MicroStation", StringComparison.OrdinalIgnoreCase) < 0) return true;
                RECT r; GetClientRect(w, out r); long a = (long)(r.R - r.L) * (r.B - r.T); if (a > area) { area = a; best = w; } return true;
            }, IntPtr.Zero);
            return best != IntPtr.Zero ? best : h;
        }
        static string Title(IntPtr h) { var sb = new System.Text.StringBuilder(256); GetWindowTextW(h, sb, 256); return sb.ToString(); }

        static IntPtr hWin, hBar, hPop; static Hook hook; static MenuButton btn; static List<KeyValuePair<string, Action>> cur;
        public static void Install()
        {
            Remove(); if (!Cfg.I.NutMenu) { Host.MenuMode = "đã tắt trong Thiết lập"; return; }
            hWin = MainWnd(); if (hWin == IntPtr.Zero) { Host.MenuMode = "không tìm thấy cửa sổ MicroStation"; return; }
            cur = Items();
            // cách 1: menu chuẩn Windows
            var bar = GetMenu(hWin);
            if (bar != IntPtr.Zero && GetMenuItemCount(bar) > 0)
            {
                hPop = CreatePopupMenu();
                for (int i = 0; i < cur.Count; i++) if (cur[i].Key == null) AppendMenuW(hPop, MF_SEPARATOR, UIntPtr.Zero, null); else AppendMenuW(hPop, MF_STRING, (UIntPtr)(uint)(ID0 + i), cur[i].Key);
                if (AppendMenuW(bar, MF_POPUP, (UIntPtr)(ulong)hPop.ToInt64(), "&Biên tập BĐĐC")) { hBar = bar; hook = new Hook(); hook.AssignHandle(hWin); DrawMenuBar(hWin); Host.MenuMode = "menu Biên tập BĐĐC trên thanh menu MicroStation"; return; }
            }
            // cách 2: nút menu gắn ở cuối hàng menu
            btn = new MenuButton(hWin, cur); btn.Place(); btn.Show(new Host.W(hWin)); Host.MenuMode = "nút Biên tập BĐĐC ở cuối hàng menu MicroStation";
        }
        public static void Remove()
        {
            try
            {
                if (hBar != IntPtr.Zero && IsWindow(hWin))
                {
                    int n = GetMenuItemCount(hBar); for (int i = n - 1; i >= 0; i--) if (GetSubMenu(hBar, i) == hPop) DeleteMenu(hBar, (uint)i, MF_BYPOSITION);
                    DrawMenuBar(hWin);
                }
            }
            catch { }
            hBar = hPop = IntPtr.Zero; if (hook != null) { try { hook.ReleaseHandle(); } catch { } hook = null; }
            if (btn != null) { try { btn.Close(); btn.Dispose(); } catch { } btn = null; }
        }
        public static void Refresh() { try { Install(); } catch (Exception ex) { Program.Log(ex); } }
        public static void Status() { if (btn != null && !btn.IsDisposed) btn.Status(); }

        // bắt lệnh menu gửi tới cửa sổ MicroStation
        class Hook : NativeWindow
        {
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x0111 && (m.WParam.ToInt64() >> 16 & 0xFFFF) == 0)   // WM_COMMAND từ menu
                {
                    int id = (int)(m.WParam.ToInt64() & 0xFFFF) - ID0;
                    if (cur != null && id >= 0 && id < cur.Count && cur[id].Value != null) { var a = cur[id].Value; Program.Ui(a); return; }
                }
                base.WndProc(ref m);
            }
        }
    }

    // nút "M8 Pro ▾" không viền, không lấy tiêu điểm, đi theo cửa sổ MicroStation
    public class MenuButton : Form
    {
        IntPtr ms; MenuStrip bar = new MenuStrip(); ToolStripMenuItem top; Timer t = new Timer { Interval = 300 };
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }   // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
        public MenuButton(IntPtr owner, List<KeyValuePair<string, Action>> items)
        {
            ms = owner; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; BackColor = SystemColors.Control; Font = new Font("Segoe UI", 9f);
            bar.Dock = DockStyle.Fill; bar.Padding = new Padding(0); bar.GripStyle = ToolStripGripStyle.Hidden; bar.BackColor = SystemColors.Control; bar.RenderMode = ToolStripRenderMode.System;
            top = new ToolStripMenuItem("Biên tập BĐĐC ▾") { Font = new Font("Segoe UI", 9f, FontStyle.Bold), ToolTipText = Bt.Ten + " – Nguyễn Xuân Thủy" };
            try { top.Image = Icons.LogoImg(16); } catch { }
            foreach (var kv in items)
            {
                if (kv.Key == null) { top.DropDownItems.Add(new ToolStripSeparator()); continue; }
                var a = kv.Value; top.DropDownItems.Add(new ToolStripMenuItem(kv.Key, null, (s, e) => Program.Ui(a)));
            }
            top.DropDownItems.Add(new ToolStripSeparator());
            top.DropDownItems.Add(new ToolStripMenuItem("Ẩn nút này (bật lại trong Thiết lập)", null, (s, e) => { Cfg.I.NutMenu = false; Cfg.I.Save(); MsMenu.Remove(); }));
            bar.Items.Add(top); Controls.Add(bar);
            Size = new Size(TextRenderer.MeasureText(top.Text, top.Font).Width + 44, Math.Max(22, bar.PreferredSize.Height));
            t.Tick += (s, e) => { try { Place(); } catch { } }; t.Start();
            FormClosed += (s, e) => { t.Stop(); t.Dispose(); };
            Status();
        }
        public void Status()
        {
            bool on = Program.Main_ != null && !Program.Main_.IsDisposed && Ms.Connected;
            top.ForeColor = on ? Color.FromArgb(0, 110, 40) : Color.FromArgb(40, 40, 40);
        }
        // cuối hàng menu (góc phải trên vùng làm việc của MicroStation)
        public void Place()
        {
            if (!MsMenu.IsWindow(ms)) { Close(); return; }
            bool vis = MsMenu.IsWindowVisible(ms) && !MsMenu.IsIconic(ms);
            if (!vis) { if (Visible) Hide(); return; } else if (!Visible) Show(new Host.W(ms));
            MsMenu.RECT r; MsMenu.GetClientRect(ms, out r); var p = new Point(0, 0); MsMenu.ClientToScreen(ms, ref p);
            var loc = new Point(p.X + r.R - Width - 6, p.Y + 1); if (Location != loc) Location = loc;
            Status();
        }
    }
#else
    public class FormBase : Form { public bool AttachMs() { return false; } }
#endif

    public static class Host
    {
#if ADDIN
        public static Bentley.MicroStation.AddIn Addin;
        public const bool InMs = true;
#else
        public static object Addin;
        public const bool InMs = false;
#endif
        public static string MenuMode = "chạy ngoài MicroStation";
        // cửa sổ chính của MicroStation (làm chủ cho các cửa sổ của ứng dụng)
        public static IWin32Window MsWindow
        {
            get
            {
#if ADDIN
                try { var h = MsMenu.MainWnd(); return h == IntPtr.Zero ? null : new W(h); } catch { return null; }
#else
                try { var h = Process.GetCurrentProcess().MainWindowHandle; return h == IntPtr.Zero ? null : new W(h); } catch { return null; }
#endif
            }
        }
        public class W : IWin32Window { IntPtr h; public W(IntPtr x) { h = x; } public IntPtr Handle { get { return h; } } }
        public static void Open()
        {
            var f = Program.Main_;
            if (f == null || f.IsDisposed) { f = Program.Main_ = new MainForm(); if (InMs && !f.AttachMs()) { var o = MsWindow; if (o != null) { f.Show(o); MenuStatus(); return; } } f.Show(); }
            else { if (f.WindowState == FormWindowState.Minimized) f.WindowState = FormWindowState.Normal; f.Show(); f.Activate(); }
            MenuStatus();
        }
        public static void Cmd(string name) { Open(); Program.Main_.RunByName(name); }
        public static void Reconnect() { Open(); var f = Program.Main_; if (Ms.Connected) f.Msg("Đang kết nối: " + Ms.FileName + " – tờ số " + Dc.To); else f.RunByName("Kết nối MicroStation"); MenuStatus(); }
        public static void Disconnect() { var f = Program.Main_; if (f != null && !f.IsDisposed) f.Disconnect(); else { Ms.Disconnect(); Dc.Reset(); } MenuStatus(); try { Ms.Status("Biên tập BĐĐC: đã ngắt kết nối"); } catch { } }
        public static void CloseApp() { var f = Program.Main_; if (f != null && !f.IsDisposed) f.Close(); MenuStatus(); }
        public static void MenuRefresh()
        {
#if ADDIN
            MsMenu.Refresh();
#endif
        }
        public static void MenuStatus()
        {
#if ADDIN
            try { MsMenu.Status(); } catch { }
#endif
        }
    }
}
