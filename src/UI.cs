// M8 Pro Địa chính – giao diện (cửa sổ thanh menu + thanh công cụ đặt nổi trên MicroStation). © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace M8Pro
{
    public static class Program
    {
        public static MainForm Main_;
        // chạy trên luồng giao diện; mọi lỗi được bắt lại để không làm treo MicroStation
        public static void Ui(Action a)
        {
            Action safe = () => { try { a(); } catch (Exception ex) { Log(ex); try { if (Main_ != null && !Main_.IsDisposed) Main_.Msg("Lỗi: " + ex.Message); } catch { } } };
            try { if (Main_ != null && !Main_.IsDisposed && Main_.IsHandleCreated) Main_.BeginInvoke(safe); else safe(); } catch { }
        }
        public static void Log(Exception ex)
        {
            try { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "M8Pro"); Directory.CreateDirectory(d); File.AppendAllText(Path.Combine(d, "log.txt"), DateTime.Now + "  " + ex + "\r\n\r\n", System.Text.Encoding.UTF8); } catch { }
        }
#if !ADDIN
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => MessageBox.Show(e.Exception.Message, Bt.Ten, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Main_ = new MainForm(); Application.Run(Main_);
        }
#endif
    }
    // ---------- hộp hỏi nhanh ----------
    // Info: dòng thông tin (không nhập), chiếm cả hai cột
    public class F { public string K, L, V; public string[] Opt; public bool Check, Info; public F(string k, string l, string v) { K = k; L = l; V = v; } }
    public static class Ask
    {
        public static Dictionary<string, string> Show(IWin32Window owner, string title, params F[] fs)
        {
            var f = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10), Font = new Font("Segoe UI", 9f), TopMost = true };
            var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill }; var ctl = new Dictionary<string, Control>();
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // ô nhập giãn theo hộp thoại
            foreach (var x in fs)
            {
                Control c;
                if (x.Info)
                {
                    var lb = new Label { Text = x.L, AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(3, 4, 3, 4), ForeColor = x.L.StartsWith("⚠") ? Color.FromArgb(170, 60, 0) : Color.FromArgb(40, 60, 90) };
                    t.Controls.Add(lb); t.SetColumnSpan(lb, 2); continue;
                }
                if (x.Check) c = new OTich { Text = x.L, Checked = x.V == "1", AutoSize = true };
                else if (x.Opt != null)
                {
                    var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, Anchor = AnchorStyles.Left | AnchorStyles.Right }; cb.Items.AddRange(x.Opt); cb.SelectedIndex = Math.Max(0, Array.IndexOf(x.Opt, x.V));
                    int rw = x.Opt.Select(o => TextRenderer.MeasureText(o ?? "", f.Font).Width).DefaultIfEmpty(0).Max() + 30; cb.Width = Math.Max(300, Math.Min(560, rw)); cb.DropDownWidth = Math.Max(cb.Width, Math.Min(900, rw)); c = cb;
                }
                else c = new TextBox { Text = x.V, Width = Math.Max(300, Math.Min(560, TextRenderer.MeasureText(x.V ?? "", f.Font).Width + 20)), Anchor = AnchorStyles.Left | AnchorStyles.Right };
                if (x.Check) { t.Controls.Add(new Label()); t.Controls.Add(c); } else { t.Controls.Add(new Label { Text = x.L, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 8, 3) }); t.Controls.Add(c); }
                ctl[x.K] = c;
            }
            var ok = GiaoDien.Nut("Đồng ý", 90, true); ok.DialogResult = DialogResult.OK; var no = GiaoDien.Nut("Hủy", 90, false); no.DialogResult = DialogResult.Cancel;
            var bp = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Bottom }; bp.Controls.Add(no); bp.Controls.Add(ok);
            t.Controls.Add(new Label()); t.Controls.Add(bp); f.Controls.Add(t); f.AcceptButton = ok; f.CancelButton = no; GiaoDien.KhongCat(f);
            if (f.ShowDialog(owner) != DialogResult.OK) return null;
            var r = new Dictionary<string, string>();
            foreach (var kv in ctl) r[kv.Key] = kv.Value is CheckBox ? (((CheckBox)kv.Value).Checked ? "1" : "0") : kv.Value is ComboBox ? Convert.ToString(((ComboBox)kv.Value).SelectedItem) : kv.Value.Text;
            return r;
        }
    }

    public partial class MainForm : FormBase
    {
        MenuStrip menu = new MenuStrip(); ToolStrip tools = new ToolStrip(); StatusStrip st = new StatusStrip(); ToolStripStatusLabel stt = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        ToolStripStatusLabel conn = new ToolStripStatusLabel { IsLink = true }; ToolStripButton btnConn = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText }; Timer tm = new Timer { Interval = 1500 };
        public Parcel Cur; AttrForm attr; ErrForm errf; ThuaForm pform; HoSoForm hoso;
        public MainForm()
        {
            Text = Bt.Ten; Font = new Font("Segoe UI", 9f); TopMost = !Host.InMs; ShowInTaskbar = !Host.InMs; StartPosition = FormStartPosition.Manual;
            try { Icon = Icons.AppIcon(); } catch { try { Icon = Icon.FromHandle(((Bitmap)Icons.LogoImg()).GetHicon()); } catch { } }
            var wa = Screen.PrimaryScreen.WorkingArea; Width = Math.Min(1020, wa.Width - 40); Location = new Point(wa.Right - Width - 20, wa.Top + 120);
            tools.ImageScalingSize = new Size(20, 20); tools.GripStyle = ToolStripGripStyle.Hidden; tools.RenderMode = ToolStripRenderMode.System;
            st.Items.Add(stt); st.Items.Add(conn); st.SizingGrip = false;
            Controls.Add(tools); Controls.Add(menu); Controls.Add(st); MainMenuStrip = menu;
            Build();
            XemChuDe(ChuDe.Hien); HandleCreated += (s, e) => ChuDe.TieuDe(this, ChuDe.DangDung);
            var cm = new ContextMenuStrip(); cm.Items.Add("Màu chủ đề thanh ứng dụng…", Icons.Get("palette", 16), (s, e) => ChonChuDe()); menu.ContextMenuStrip = cm; tools.ContextMenuStrip = cm; st.ContextMenuStrip = cm;
            FitH(); Shown += (s, e) => FitH();
            tm.Tick += (s, e) => Watch(); tm.Start(); Load += (s, e) => { Connect(false); };
            Shown += (s, e) => { var t1 = new Timer { Interval = 1500 }; t1.Tick += (s1, e1) => { t1.Stop(); t1.Dispose(); if (!IsDisposed) CapNhat.KiemTraBanCai(this); }; t1.Start(); };
            Action onCh = () => { if (IsDisposed) return; RefreshTitle(); if (attr != null && !attr.IsDisposed) attr.Reload(); if (pform != null && !pform.IsDisposed) pform.Show_(Cur); };
            Dc.Changed += onCh;
            FormClosing += (s, e) =>
            {
                if (Dc.G != null && Dc.G.Dirty)
                {
                    var r = MessageBox.Show(this, "Tệp thuộc tính " + Dc.G.Name + " có thay đổi chưa lưu. Lưu trước khi đóng?", Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (r == DialogResult.Cancel) { e.Cancel = true; return; }
                    if (r == DialogResult.Yes) try { Dc.G.Save(Dc.G.Path_); } catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); e.Cancel = true; return; }
                }
                Cfg.I.Save();
            };
            FormClosed += (s, e) =>
            {
                tm.Stop(); tm.Dispose(); Dc.Changed -= onCh; Web.Stop();
                foreach (var f in Application.OpenForms.Cast<Form>().Where(f => f != this && (f is AttrForm || f is ParcelForm || f is ErrForm || f is StatForm || f is HoSoForm)).ToList()) try { f.Close(); } catch { }
                Ms.Disconnect(); Dc.Reset(); lastFile = "";
            };
        }
        void FitH() { PerformLayout(); MinimumSize = Size.Empty; MaximumSize = Size.Empty; ClientSize = new Size(ClientSize.Width, menu.PreferredSize.Height + tools.PreferredSize.Height + st.PreferredSize.Height + 4); MinimumSize = new Size(420, Height); MaximumSize = new Size(4000, Height); }
        public void Msg(string s) { stt.Text = s; Ms.Status(s); }
        void RefreshTitle() { Text = Bt.Ten + (Ms.FileName != "" ? " (" + Ms.FileName + ")" : "") + (Cfg.I.Place != "" ? " - " + Cfg.I.Place : ""); }
        string lastFile = "";
        void Watch()
        {
            bool ok = Ms.Connected; Web.Tep = ok ? Ms.FullName : ""; conn.Text = ok ? "● Đã kết nối MicroStation" : "○ Chưa kết nối MicroStation"; MauConn(ok);
            if (ok && Ms.FullName != lastFile)
            {
                // chỉ xảy ra khi thật sự đổi sang tệp bản đồ khác (hoặc lần đầu kết nối)
                bool first = lastFile == ""; lastFile = Ms.FullName; Ms.ResetCache(); Dc.OnMapChanged(Ms.FileName); Cur = null; RefreshTitle();
                // hỏi kết nối tệp thuộc tính cùng tên, cùng thư mục hoặc tệp khác (hoặc tự kết nối theo lựa chọn đã nhớ)
                OnMapOpened(lastFile);
                Msg("Đang làm việc với " + Dc.MapName + " – tờ số " + Dc.To + " (theo " + Dc.ToSrc + ") – bấm Bản đồ → Tạo thửa");
                if (!first && attr != null && !attr.IsDisposed && !Dc.DocLap) { try { Msg(Dc.Build(false)); if (Dc.P.Count == 0 && Dc.G != null) Msg(Dc.FromGtp()); } catch (Exception ex) { Program.Log(ex); } }
                Dc.Notify();
            }
            btnConn.Text = ok ? "Ngắt kết nối" : "Kết nối"; btnConn.ToolTipText = ok ? "Ngắt kết nối với MicroStation" : "Kết nối với MicroStation";
        }
        // ngắt kết nối: đóng các bảng, giải phóng tệp thuộc tính, bỏ liên kết với MicroStation (bản vẽ không bị ảnh hưởng)
        public void Disconnect()
        {
            if (Dc.G != null && Dc.G.Dirty)
            {
                var r = MessageBox.Show(this, "Tệp thuộc tính " + Dc.G.Name + " có thay đổi chưa lưu. Lưu trước khi ngắt kết nối?", Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return; if (r == DialogResult.Yes) Dc.G.Save(Dc.G.Path_);
            }
            foreach (var f in Application.OpenForms.Cast<Form>().Where(f => f is AttrForm || f is ParcelForm || f is ErrForm || f is StatForm || f is HoSoForm).ToList()) try { f.Close(); } catch { }
            AnhNen.Tat(false); NenMau.Tat(false); Ms.Disconnect(); Dc.Reset(); lastFile = ""; Cur = null; Watch(); RefreshTitle(); Msg("Đã ngắt kết nối MicroStation – bấm “Kết nối” để làm việc tiếp");
        }
        void ToggleConn() { if (Ms.Connected) Disconnect(); else if (Connect(true)) Msg("Đã kết nối: " + Ms.FileName + " – tờ số " + Dc.To); }
        bool Connect(bool ask)
        {
            if (Ms.Connected) { Watch(); return true; } var e = Ms.Connect(); if (e == null) { Watch(); return true; }
            if (!ask) { Msg(e); return false; }
            var u = Ms.FindUstation();
            if (u != null && MessageBox.Show(this, "MicroStation V8i chưa chạy. Mở MicroStation ngay?\n" + u, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) { System.Diagnostics.Process.Start(u); Msg("Đang mở MicroStation… mở tệp bản đồ xong thì bấm lại lệnh"); }
            else MessageBox.Show(this, e + "\nHãy mở MicroStation V8i và tệp bản đồ (.dgn) trước.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        // chạy lệnh: kiểm tra kết nối, bắt lỗi
        void Run(Action a, bool needMap)
        {
            if (needMap && !Connect(true)) return; if (!needMap && Ms.Connected) Watch(); Cursor = Cursors.WaitCursor;
            try { a(); } catch (Exception ex) { Program.Log(ex); Msg("Lỗi: " + ex.Message); MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); } finally { Cursor = Cursors.Default; }
        }
        bool NeedTopo() { if (Dc.P.Count == 0 || Dc.Dirty) Msg(Dc.Build(false)); return Dc.P.Count > 0; }
        // ---------- thanh menu + thanh công cụ ----------
        class It { public string T, Ic; public Action A; public bool Map = true, Sep; }
        static It I(string t, string ic, Action a) { return new It { T = t, Ic = ic, A = a }; }
        static It S() { return new It { Sep = true }; }
        void Build()
        {
            var OLD = new List<KeyValuePair<string, It[]>> {
                new KeyValuePair<string, It[]>("Hệ thống", new[] { new It { T = "Kết nối MicroStation", Ic = "plug", A = () => { if (Connect(true)) Msg("Đã kết nối: " + Ms.FileName + " – tờ số " + Dc.To); }, Map = false }, new It { T = "Ngắt kết nối MicroStation", Ic = "plug", A = Disconnect, Map = false }, new It { T = "Thiết lập (tờ bản đồ, level, nhãn…)", Ic = "gear", A = Setup, Map = false }, S(), new It { T = "Chẩn đoán kết nối và đọc dữ liệu", Ic = "search", A = Diagnose }, S(), new It { T = "Giới thiệu", Ic = "info", A = About, Map = false }, new It { T = "Đóng M8 Pro", Ic = "trash", A = Close, Map = false } }),
                new KeyValuePair<string, It[]>("Thửa đất", new[] { I("Tạo thửa từ ranh", "polygon", BtCreate), I("Chọn thửa trên bản đồ", "cursor", PickParcel), I("Thông tin thửa đất", "info", () => { if (Cur != null && Dc.P.Contains(Cur)) ShowParcel(Cur); else PickParcel(); }), I("Bảng thông tin thuộc tính", "table", ShowAttr), new It { T = "Tạo thửa theo hình trong tệp thuộc tính (.gtp)", Ic = "polygon", A = () => Msg(Dc.FromGtp()), Map = false } }),
                new KeyValuePair<string, It[]>("Đo đạc", new[] { I("Triển điểm đo từ tệp toạ độ", "coord", TrienDiem), I("Nối điểm thành ranh thửa", "polygon", NoiDiem), S(), I("Chia thửa theo diện tích", "scissors", ChiaThua) }),
                new KeyValuePair<string, It[]>("Kiểm tra", new[] { I("Tìm lỗi ranh và nhãn", "search", CheckErr), I("Sửa lỗi ranh tự động", "wrench", FixErr), S(), I("Đối chiếu nhãn với tệp thuộc tính", "search", DoiChieu), I("Cập nhật lại nhãn theo tệp thuộc tính", "refresh", CapNhatNhan) }),
                new KeyValuePair<string, It[]>("Nhãn thửa", new[] { I("Vẽ nhãn tất cả thửa", "tag", () => { if (NeedTopo() && Confirm("Vẽ lại nhãn cho " + Dc.P.Count + " thửa (" + Dc.NhanCuMoTa + " trong thửa)?")) { Dc.DrawLabels(Dc.P, true); Msg("Đã vẽ nhãn " + Dc.P.Count + " thửa" + Dc.NhanCuBao()); } }),
                    I("Vẽ nhãn thửa chọn", "cursor", () => Pick("Chạm vào trong thửa cần vẽ nhãn", p => { Dc.DrawLabels(new[] { p }, true); Msg("Đã vẽ nhãn thửa " + p.So); })), I("Đánh số thửa tự động", "hash", AutoNumber), I("Ghi tên chủ sử dụng lên bản đồ", "pen", () => ForParcels("Ghi tên chủ sử dụng", ps => Msg("Đã ghi tên chủ " + Nv.DrawOwners(ps) + " thửa trên level " + Cfg.I.ChuLop))), S(), I("Xoá nhãn thửa", "trash", () => { if (NeedTopo() && Confirm("Xoá toàn bộ chữ nhãn nằm trong các thửa?")) Msg("Đã xoá " + Dc.DeleteLabels() + " chữ nhãn"); }) }),
                new KeyValuePair<string, It[]>("Biên tập", new[] { I("Tách thửa", "scissors", TachThua), I("Hợp thửa", "merge", HopThua), I("Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22)", "scissors", () => BtForm("t2b", "Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22)", true)), S(),
                    I("Ghi kích thước cạnh", "dim", () => ForParcels("Ghi kích thước cạnh", Dc.Dims)), I("Đánh số hiệu đỉnh thửa", "vmove", () => ForParcels("Đánh số hiệu đỉnh", Dc.VertNums)), I("Vẽ khung bản đồ địa chính", "frame", Frame), I("Tô màu theo loại đất", "fillmap", () => { if (NeedTopo() && Confirm("Tạo vùng tô màu loại đất trên level " + Cfg.I.ToLop + "?")) Msg("Đã tô màu " + Dc.FillColors() + " thửa trên level " + Cfg.I.ToLop); }), S(),
                    I("Đổi phông chữ TCVN3 ↔ Unicode", "tag", DoiPhong), I("Tìm và thay chữ trên bản đồ", "search", TimThay) }),
                new KeyValuePair<string, It[]>("Thuộc tính", new[] { I("Bảng thông tin thuộc tính", "table", ShowAttr), new It { T = "Mở tệp thuộc tính (.gtp)", Ic = "folder", A = OpenGtp, Map = false }, I("Cập nhật từ bản đồ vào tệp thuộc tính", "upload", Push), new It { T = "Lưu tệp thuộc tính", Ic = "save", A = SaveGtp, Map = false }, new It { T = "Đóng tệp thuộc tính", Ic = "trash", A = () => { var n = Dc.G != null ? Dc.G.Name : ""; Dc.Commit(); if (Dc.G != null) { Dc.G.Dispose(); Dc.G = null; Dc.GRows = new List<GRow>(); foreach (var p in Dc.P) { p.Gid = null; p.Row = null; } Dc.P.RemoveAll(p => p.FromGtp); Dc.Notify(); } Msg("Đã đóng " + n); }, Map = false }, S(),
                    I("Xuất danh sách thửa ra Excel (CSV)", "export", ExportCsv), I("Nhập thông tin từ Excel (CSV)", "download", ImportCsv) }),
                new KeyValuePair<string, It[]>("Hồ sơ", new[] { I("Trích lục bản đồ địa chính", "doc", () => Pick("Chạm vào thửa cần trích lục", Reports.TrichLuc)), I("Phiếu đo đạc chỉnh lý thửa đất", "ruler", () => Pick("Chạm vào thửa cần lập phiếu", Reports.Phieu)),
                    I("Biên bản xác định ranh giới, mốc giới", "pen", () => Pick("Chạm vào thửa cần lập biên bản", Reports.BienBan)), I("Bảng toạ độ đỉnh thửa", "coord", () => Pick("Chạm vào thửa", Reports.BangToaDo)), I("Sổ mục kê đất đai (tờ bản đồ)", "list", () => { if (NeedTopo()) Reports.SoMucKe(); }), S(),
                    I("In hồ sơ nhiều thửa", "doc", InNhieu), I("Danh sách công khai", "list", () => { if (!NeedTopo()) return; var r = Ask.Show(this, "Danh sách công khai", new F("t", "Các thửa (ví dụ 12, 15-20; trống = tất cả)", "")); if (r != null) Reports.CongKhai(Nv.Pick(r["t"])); }) }),
                new KeyValuePair<string, It[]>("Thống kê", new[] { I("Diện tích theo loại đất", "chart", () => { if (NeedTopo()) new StatForm().Show(this); }), I("In bảng thống kê", "chart", () => { if (NeedTopo()) Reports.ThongKe(); }), I("Thửa chênh lệch diện tích", "search", Lech), I("Diện tích trong / ngoài quy hoạch", "chart", QuyHoach) }) };
            Func<string, It> O = n => { foreach (var kv in OLD) foreach (var it in kv.Value) if (!it.Sep && it.T == n) return it; throw new Exception("Thiếu lệnh " + n); };
            // ===== MENU THEO NGHIỆP VỤ ĐỊA CHÍNH =====
            var M = new List<KeyValuePair<string, It[]>> {
                new KeyValuePair<string, It[]>("Hệ thống", new[] { O("Kết nối MicroStation"), O("Ngắt kết nối MicroStation"), S(),
                    new It { T = "Kết nối tệp thuộc tính (.gtp)…", Ic = "link", A = () => KetNoiGtp(false) }, new It { T = "Kết nối tệp thuộc tính độc lập – mọi tờ, không theo bản đồ (.gtp)…", Ic = "link", A = () => KetNoiGtpDocLap(null), Map = false },
                    new It { T = "Ngắt kết nối tệp thuộc tính", Ic = "x", A = () => CloseGtp(true), Map = false }, new It { T = "Chuyển số tờ trong tệp thuộc tính theo tên tờ bản đồ (một / nhiều tệp)…", Ic = "layers", A = ChuyenSoTo, Map = false },
                    new It { T = "Gộp CSDL bản đồ và thuộc tính toàn xã (nhiều tờ, nhiều thư mục)…", Ic = "merge", A = GopToanXa, Map = false },
                    new It { T = "Thiết lập đơn vị hành chính…", Ic = "layers", A = DonViHanhChinh, Map = false }, O("Thiết lập (tờ bản đồ, level, nhãn…)"), new It { T = "Màu chủ đề thanh ứng dụng…", Ic = "palette", A = ChonChuDe, Map = false }, S(),
                    new It { T = "Mở thư mục hồ sơ", Ic = "folder", A = Web.OpenOutDir, Map = false }, O("Chẩn đoán kết nối và đọc dữ liệu"), S(), new It { T = "Cập nhật phiên bản mới…", Ic = "update", A = CapNhat.Mo, Map = false }, O("Giới thiệu"), new It { T = "Đóng " + Bt.Ten, Ic = "trash", A = Close, Map = false } }),
                new KeyValuePair<string, It[]>("Bản đồ", new[] { I("Sửa lỗi tự động", "wrench", BtFix), I("Tìm và sửa lỗi", "search", BtCheck), I("Tạo thửa", "polygon", BtCreate), I("Vẽ nhãn", "tag", BtLabels), I("Vẽ văn tự động", "tag", BtVanTu), O("Bảng thông tin thuộc tính"), S(),
                    O("Tách thửa"), O("Hợp thửa"), O("Chia thửa theo diện tích"), S(), new It { T = AnhNen.TEN, Ic = "globe", A = () => AnhNen.BatTat(this) }, new It { T = AnhNen.TEN_TL, Ic = "gear", A = () => AnhNen.ThietLap(this, false), Map = false }, new It { T = "Mở Google Maps tại thửa (trình duyệt)", Ic = "globe", A = AnhGoogle },
                    new It { T = NenMau.TEN, Ic = "landbg", A = () => NenMau.BatTat(this) }, new It { T = NenMau.TEN_BM, Ic = "palette", A = () => NenMau.BangMau(this), Map = false } }),
                new KeyValuePair<string, It[]>("Đăng ký", new[] { F_("hsdn", "Đơn đăng ký đất đai, tài sản gắn liền với đất (Mẫu số 15)", "pen", true), F_("hsck", "Danh sách công khai kết quả kiểm tra hồ sơ (Mẫu số 06/ĐK)", "list", false),
                    F_("hskk", "Kê khai đăng ký (Excel)", "export", false), S(), F_("hsmt", "Bản mô tả ranh giới, mốc giới thửa đất (Phụ lục 12)", "doc", true), F_("hsth", "Bảng tổng hợp diện tích, số thửa, số người sử dụng đất (Phụ lục 16)", "chart", false) }),
                new KeyValuePair<string, It[]>("Hồ sơ", new[] { F_("hsdc", "Sổ địa chính (Mẫu số 01/ĐK)", "doc", false), F_("hsmk", "Sổ mục kê đất đai (Phụ lục 18)", "list", false), F_("hsgn", "Sổ cấp Giấy chứng nhận (Mẫu số 02/ĐK)", "doc", false),
                    F_("btbbd", "Sổ đăng ký biến động đất đai", "doc", false), S(), F_("hstd", "Bảng thống kê tọa độ điểm mốc ranh giới (Phụ lục 14a)", "coord", true), F_("txl", "Danh sách thửa và thống kê (Excel)", "export", false) }),
                new KeyValuePair<string, It[]>("Hồ sơ toàn xã", new[] { new It { T = "Gộp CSDL bản đồ và thuộc tính toàn xã (nhiều tờ, nhiều thư mục)…", Ic = "merge", A = GopToanXa, Map = false },
                    new It { T = "Chọn tệp thuộc tính toàn xã (.gtp)…", Ic = "folder", A = ChonGtpTx, Map = false },
                    new It { T = "Bảng thuộc tính toàn xã (kết nối độc lập tệp toàn xã – mọi tờ, mọi thửa)…", Ic = "table", A = BangThuocTinhTx, Map = false }, S(),
                    TX_("hsdc", "Sổ địa chính toàn xã (Mẫu số 01/ĐK)", "doc"), TX_("hsmk", "Sổ mục kê đất đai toàn xã (Phụ lục 18)", "list"), TX_("hsgn", "Sổ cấp Giấy chứng nhận toàn xã (Mẫu số 02/ĐK)", "doc"),
                    TX_("btbbd", "Sổ đăng ký biến động đất đai toàn xã", "doc"), S(), TX_("hskk", "Kê khai đăng ký toàn xã (Excel)", "export"),
                    TX_("hsck", "Danh sách công khai kết quả kiểm tra hồ sơ toàn xã (Mẫu số 06/ĐK)", "list"), TX_("hsth", "Bảng tổng hợp diện tích, số thửa, số người sử dụng đất toàn xã (Phụ lục 16)", "chart"),
                    TX_("txl", "Danh sách thửa và thống kê toàn xã (Excel)", "export") }),
                new KeyValuePair<string, It[]>("Kết xuất", new[] { new It { T = "Bảng hồ sơ thửa đất (các mẫu hồ sơ theo nhóm)…", Ic = "frame", A = () => ShowHoSo(Cur) }, S(), F_("hshh", "Sơ đồ thửa đất (DGN): ranh thửa level 10, số thửa level 13, loại đất level 2, diện tích level 4", "polygon", false), F_("thp", "Phiếu đo đạc chỉnh lý thửa đất (Phụ lục 17)", "ruler", true),
                    F_("ttl", "Trích lục bản đồ địa chính (Phụ lục 19)", "doc", true), F_("t2b", "Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22)", "scissors", true), F_("hsht", "Phiếu xác nhận kết quả đo đạc hiện trạng thửa đất (Phụ lục 14)", "doc", true),
                    F_("hsgc", "Sơ đồ thửa đất in trên Giấy chứng nhận", "frame", true), S(), F_("hsshp", "Shapefile (VN-2000)", "export", false), F_("hsgj", "GeoJSON (WGS84)", "export", false) }) };
            // ===== NÚT ĐỘC LẬP TRÊN THANH CÔNG CỤ (các lệnh còn lại) =====
            var T = new[] { O("Chọn thửa trên bản đồ"), O("Thông tin thửa đất"), new It { T = "Bảng hồ sơ thửa đất", Ic = "frame", A = () => ShowHoSo(Cur) }, null, O("Tách thửa"), O("Hợp thửa"), O("Chia thửa theo diện tích"), O("Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22)"), null,
                O("Đánh số thửa tự động"), O("Vẽ nhãn thửa chọn"), O("Xoá nhãn thửa"), O("Ghi kích thước cạnh"), O("Đánh số hiệu đỉnh thửa"), O("Vẽ khung bản đồ địa chính"), O("Tô màu theo loại đất"), O("Ghi tên chủ sử dụng lên bản đồ"), null,
                O("Triển điểm đo từ tệp toạ độ"), O("Nối điểm thành ranh thửa"), null,
                new It { T = AnhNen.TEN, Ic = "globe", A = () => AnhNen.BatTat(this) }, new It { T = NenMau.TEN, Ic = "landbg", A = () => NenMau.BatTat(this) }, new It { T = "Chỉ đường tới thửa (Google Maps)", Ic = "route", A = () => GoogleMap(true) }, null, O("Đối chiếu nhãn với tệp thuộc tính"), O("Cập nhật lại nhãn theo tệp thuộc tính"), O("Thửa chênh lệch diện tích"), O("Diện tích trong / ngoài quy hoạch"), O("Diện tích theo loại đất"), null,
                O("Mở tệp thuộc tính (.gtp)"), O("Cập nhật từ bản đồ vào tệp thuộc tính"), O("Lưu tệp thuộc tính"), O("Xuất danh sách thửa ra Excel (CSV)"), O("Nhập thông tin từ Excel (CSV)"), null,
                O("Đổi phông chữ TCVN3 ↔ Unicode"), O("Tìm và thay chữ trên bản đồ"), null, O("Thiết lập (tờ bản đồ, level, nhãn…)") };
            Cmds.Clear();
            foreach (var kv in M)
            {
                var top = new ToolStripMenuItem(kv.Key); menu.Items.Add(top);
                // mở menu lập hồ sơ: khởi động sẵn bộ máy lập mẫu chạy ngầm, nạp sẵn tờ bản đồ
                if (kv.Key == "Đăng ký" || kv.Key == "Hồ sơ" || kv.Key == "Kết xuất") top.DropDownOpening += (s0, e0) => Web.Warm();
                foreach (var it in kv.Value) { if (it.Sep) { top.DropDownItems.Add(new ToolStripSeparator()); continue; } var x = it; var mi = new ToolStripMenuItem(x.T, Icons.Get(Icons.For(x.T, x.Ic), 16), (s, e) => Run(x.A, x.Map)); top.DropDownItems.Add(mi); Cmds[x.T] = x; if (x.T == AnhNen.TEN) miAnh = mi; if (x.T == NenMau.TEN) miMau = mi; }
            }
            // các lệnh cũ không còn trên menu vẫn gọi được bằng tên (lệnh gõ)
            foreach (var kv in OLD) foreach (var it in kv.Value) if (!it.Sep && !Cmds.ContainsKey(it.T)) Cmds[it.T] = it;
            btnConn.Image = Icons.Get("plug"); btnConn.Tag = "plug"; btnConn.Click += (s, e) => { try { ToggleConn(); } catch (Exception ex) { Msg("Lỗi: " + ex.Message); } }; tools.Items.Add(btnConn); tools.Items.Add(new ToolStripSeparator());
            conn.Click += (s, e) => { try { ToggleConn(); } catch { } };
            foreach (var t in T)
            {
                if (t == null) { tools.Items.Add(new ToolStripSeparator()); continue; }
                var x = t; var b = new ToolStripButton(null, Icons.Get(Icons.For(x.T, x.Ic)), (s, e) => Run(x.A, x.Map)) { ToolTipText = x.T, DisplayStyle = ToolStripItemDisplayStyle.Image, Tag = Icons.For(x.T, x.Ic) }; tools.Items.Add(b);
                if (x.T == AnhNen.TEN) btnAnh = b; if (x.T == NenMau.TEN) btnMau = b;
            }
            // nút ảnh vệ tinh nền: hiện đang bấm khi bật
            AnhNen.Doi += AnhDoi; FormClosed += (s, e) => { AnhNen.Doi -= AnhDoi; AnhNen.Tat(false); };
            // nút nền màu loại đất: hiện đang bấm khi bật
            NenMau.Doi += MauDoi; FormClosed += (s, e) => { NenMau.Doi -= MauDoi; NenMau.Tat(false); };
        }
        // màu chủ đề thanh ứng dụng: xem thử ngay khi bấm ô màu, Đồng ý thì lưu vào thiết lập
        void ChonChuDe()
        {
            using (var f = new ChuDeForm(XemChuDe))
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    Cfg.I.MauChuDe = f.Chon ?? ""; Cfg.I.Save(); XemChuDe(ChuDe.Hien);
                    Msg(Cfg.I.MauChuDe == "" ? "Thanh ứng dụng: trở lại giao diện Windows mặc định" : "Đã đổi màu chủ đề thanh ứng dụng (" + Cfg.I.MauChuDe + ")");
                }
        }
        public void XemChuDe(Color? c)
        {
            ChuDe.Ap(this, menu, tools, st, c); bool toi = c.HasValue && ChuDe.Toi(c.Value);
            foreach (ToolStripItem it in tools.Items) { var ten = it.Tag as string; if (ten != null) it.Image = toi ? ChuDe.BieuTuongSang(ten, 20) : Icons.Get(ten); }
            MauConn(conn.Text.StartsWith("●")); ChuDe.TieuDe(this, c);
        }
        // chữ "Đã kết nối / Chưa kết nối": xanh lá / xám, sáng hơn khi thanh nền tối; giao diện mặc định giữ màu liên kết như cũ
        void MauConn(bool ok) { conn.ForeColor = ChuDe.MauKetNoi(ok); conn.LinkColor = conn.ActiveLinkColor = conn.VisitedLinkColor = ChuDe.DangDung.HasValue ? conn.ForeColor : Color.Empty; }
        ToolStripButton btnAnh, btnMau; ToolStripMenuItem miAnh, miMau;
        void MauDoi()
        {
            if (IsDisposed) return; bool on = NenMau.On;
            if (btnMau != null) { btnMau.Checked = on; btnMau.ToolTipText = NenMau.TEN + (on ? " – đang bật, bấm để tắt" : ""); }
            if (miMau != null) miMau.Checked = on;
        }
        void AnhDoi()
        {
            if (IsDisposed) return; bool on = AnhNen.On;
            if (btnAnh != null) { btnAnh.Checked = on; btnAnh.ToolTipText = AnhNen.TEN + (on ? " – đang bật, bấm để tắt" : ""); }
            if (miAnh != null) miAnh.Checked = on;
        }
        readonly Dictionary<string, It> Cmds = new Dictionary<string, It>();
        // tên cũ của lệnh (lệnh gõ, menu M8 Pro cũ) → tên mới
        static readonly Dictionary<string, string> Alias = new Dictionary<string, string> {
            { "Tạo thửa từ ranh", "Tạo thửa" }, { "Tìm lỗi ranh và nhãn", "Tìm và sửa lỗi" }, { "Sửa lỗi ranh tự động", "Sửa lỗi tự động" }, { "Vẽ nhãn tất cả thửa", "Vẽ nhãn" },
            { "Trích lục bản đồ địa chính", "Trích lục bản đồ địa chính (Phụ lục 19)" }, { "Phiếu đo đạc chỉnh lý thửa đất", "Phiếu đo đạc chỉnh lý thửa đất (Phụ lục 17)" }, { "Bảng thuộc tính", "Bảng thông tin thuộc tính" },
            { "Tách thửa (vẽ đường chia)", "Tách thửa" }, { "Cập nhật thửa sau khi tự vẽ đường chia", "Tách thửa" }, { "Gộp thửa", "Hợp thửa" }, { "Xem ảnh vệ tinh Google Maps tại thửa", AnhNen.TEN }, { "Ảnh vệ tinh Google đúng toạ độ (VN-2000, múi chiếu 3°)", AnhNen.TEN }, { "Ảnh vệ tinh Google đúng toạ độ", AnhNen.TEN },
            { "Ảnh Google", AnhNen.TEN }, { "Ảnh vệ tinh", AnhNen.TEN }, { "Nền màu loại đất", NenMau.TEN }, { "Nền màu", NenMau.TEN }, { "Mẫu 22", "Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22)" } };
        // chạy lệnh theo tên mục menu (dùng cho lệnh gõ trong MicroStation)
        public void RunByName(string name)
        {
            string a; if (!Cmds.ContainsKey(name) && Alias.TryGetValue(name, out a)) name = a;
            It x; if (Cmds.TryGetValue(name, out x)) { Run(x.A, x.Map); return; }
            Msg("Không có lệnh: " + name);
        }
        bool Confirm(string q) { return MessageBox.Show(this, q, Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK; }
        // ---------- chọn điểm / thửa trên bản vẽ MicroStation ----------
        public void Pick(string prompt, Action<Parcel> done)
        {
            if (!NeedTopo()) return; Msg(prompt + " (trong cửa sổ MicroStation; chuột phải để huỷ)");
            Ms.PickPoint(prompt, (x, y) => { var p = Dc.Find(x, y); if (p == null) { Msg("Không có thửa tại điểm vừa chọn"); return; } Cur = p; Dc.Zoom(p, .3); Ms.Hilite(p.Rings); if (attr != null && !attr.IsDisposed) attr.SelectParcel(p); if (hoso != null && !hoso.IsDisposed) hoso.DatThua(p); try { done(p); } catch (Exception ex) { Msg("Lỗi: " + ex.Message); } }, () => Msg("Đã huỷ chọn"));
        }
        // Google Maps tại thửa đang chọn (chưa chọn thì chạm thửa): ảnh vệ tinh hoặc chỉ đường (toạ độ VN-2000 đổi sang WGS84 như M8 Mobile)
        void GoogleMap(bool dir)
        {
            HoiKtt();
            Action<Parcel> go = p =>
            {
                var w = Geo.Vn2000ToWgs(p.Lp[0], p.Lp[1], Cfg.I.Ktt); string q = w[0].ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture) + "," + w[1].ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);
                var u = dir ? "https://www.google.com/maps/dir/?api=1&travelmode=driving&destination=" + q : "https://www.google.com/maps/@?api=1&map_action=map&basemap=satellite&zoom=19&center=" + q;
                try { System.Diagnostics.Process.Start(u); } catch (Exception ex) { Msg("Không mở được trình duyệt: " + ex.Message); return; }
                Msg((dir ? "Chỉ đường tới thửa " : "Ảnh vệ tinh tại thửa ") + p.So + " – vĩ độ, kinh độ " + q.Replace(",", ", ") + " (VN-2000, kinh tuyến trục " + Ktt(Cfg.I.Ktt) + ", múi chiếu 3°)");
            };
            if (Cur != null && Dc.P.Contains(Cur)) { go(Cur); return; }
            Pick("Chạm vào thửa cần " + (dir ? "chỉ đường" : "xem ảnh vệ tinh"), go);
        }
        void PickParcel() { Pick("Chạm vào trong thửa đất", p => { ShowParcel(p); Msg("Thửa " + p.So + " – " + Dc.FD(p.Dt) + " m²"); }); }
        // thông tin thửa đất: sửa thửa, loại đất chi tiết, chủ sử dụng; Ghi là cập nhật ngay vào tệp thuộc tính
        public void ShowParcel(Parcel p)
        {
            if (pform == null || pform.IsDisposed) { pform = new ThuaForm(this); if (!(Host.InMs && pform.AttachMs())) pform.Show(this); else pform.Show(); }
            else if (!pform.ChoChuyen(p)) { pform.Show(); pform.Activate(); return; }
            Cur = p; pform.Show_(p); pform.Show(); pform.Activate(); if (attr != null && !attr.IsDisposed) attr.SelectParcel(p);
            // nhấp tiếp vào thửa khác trên bản đồ là hộp chuyển sang thửa đó
            if (!pform.DangChon && !Dc.DocLap) pform.ChonTren();
        }
        // hộp Thông tin thửa đất vừa chuyển sang thửa khác (chọn trên bản đồ): chọn dòng thửa đó trong bảng thuộc tính
        public void ChonThua(Parcel p) { Cur = p; if (attr != null && !attr.IsDisposed) attr.SelectParcel(p); }
        // bảng Hồ sơ thửa đất: các mẫu hồ sơ theo nhóm, lập cho thửa đang chọn
        public void ShowHoSo(Parcel p)
        {
            if (Dc.P.Count == 0 && Ms.Connected) { try { Msg(Dc.Build(false)); } catch (Exception ex) { Program.Log(ex); } }
            if (hoso == null || hoso.IsDisposed) { hoso = new HoSoForm(this); if (Host.InMs && hoso.AttachMs()) hoso.Show(); else hoso.Show(this); }
            if (p != null && Dc.P.Contains(p)) hoso.DatThua(p); hoso.Show(); hoso.Activate();
        }
        public void ShowAttr()
        {
            // bảng mở được cả khi chưa tạo được thửa (hiện dữ liệu tệp thuộc tính); kết nối độc lập: bảng hiện mọi tờ của tệp, không dựng thửa từ bản đồ
            if (!Dc.DocLap)
            {
                if (Dc.P.Count == 0 && Ms.Connected) { try { Msg(Dc.Build(false)); } catch (Exception ex) { Msg("Chưa tạo được thửa: " + ex.Message); } }
                if (Dc.P.Count == 0 && Dc.G != null) Msg(stt.Text + " → " + Dc.FromGtp());
                if (Dc.P.Count == 0 && Dc.G == null) Msg("Chưa có thửa và chưa mở tệp thuộc tính – bảng đang trống. " + stt.Text);
            }
            if (attr == null || attr.IsDisposed) { attr = new AttrForm(this); if (Host.InMs && attr.AttachMs()) attr.Show(); else attr.Show(this); } else { attr.Reload(); attr.Show(); attr.Activate(); }
        }
        void CheckErr() { Msg(Dc.Build(false)); Dc.Check(); if (errf == null || errf.IsDisposed) { errf = new ErrForm(this); errf.Show(this); } errf.Reload(); Msg("Tìm thấy " + Dc.Errs.Count + " lỗi"); }
        void FixErr()
        {
            Msg(Dc.Build(false)); var r = Ask.Show(this, "Sửa lỗi ranh tự động", new F("d", "Khoảng nối tối đa (m)", Cfg.I.FixTol.ToString()), new F("c", "Cắt ranh đúng tại giao điểm (chỉ ngắt đường tại điểm cắt, giữ nguyên mọi đỉnh)", Cfg.I.CatGiao ? "1" : "0") { Check = true },
                new F("u", "Nối đầu hở: kéo dài theo hướng của đường tới đường khác (bắt chưa tới)", "0") { Check = true }, new F("o", "Cắt phần thừa: đoạn cuối cắt qua đường khác rồi thò ra (bắt quá)", "0") { Check = true }, new F("dup", "Xoá đoạn thẳng trùng hệt nhau (cùng level)", "0") { Check = true }); if (r == null) return;
            double d; if (double.TryParse(r["d"].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) Cfg.I.FixTol = d;
            Cfg.I.CatGiao = r["c"] == "1"; Cfg.I.Save();
            bool u = r["u"] == "1", o = r["o"] == "1", dp = r["dup"] == "1"; if (!u && !o && !dp && !Cfg.I.CatGiao) { Msg("Chưa chọn việc sửa nào"); return; }
            var s = u || o || dp ? Dc.Fix(u, o, dp) : ""; Dc.Dirty = true;
            if (Cfg.I.CatGiao) s += (s != "" ? "; " : "") + CatGiao(Cfg.Lvs(Cfg.I.LvRanh));
            Msg(s + " · " + Dc.Build(false)); CheckErr();
        }
        void AutoNumber()
        {
            if (!NeedTopo()) return; var r = Ask.Show(this, "Đánh số thửa tự động", new F("b", "Bắt đầu từ số", "1"), new F("m", "Phạm vi", "Đánh lại toàn bộ") { Opt = new[] { "Đánh lại toàn bộ", "Chỉ thửa chưa có số (tiếp số lớn nhất)" } },
                new F("z", "Thứ tự", "Zích zắc từ trên xuống") { Opt = new[] { "Zích zắc từ trên xuống", "Mỗi hàng từ trái sang phải" } }, new F("v", "Vẽ lại nhãn sau khi đánh số", "1") { Check = true }); if (r == null) return;
            int b; int.TryParse(r["b"], out b); bool onlyNew = r["m"].StartsWith("Chỉ"); var before = onlyNew ? Dc.P.Where(p => p.So == "").ToList() : Dc.P.ToList();
            Dc.AutoNumber(Math.Max(1, b), onlyNew, r["z"].StartsWith("Zích")); if (r["v"] == "1") Dc.DrawLabels(before, true); Msg("Đã đánh số " + before.Count + " thửa");
        }
        void ForParcels(string title, Action<IEnumerable<Parcel>> f)
        {
            if (!NeedTopo()) return; var r = Ask.Show(this, title, new F("m", "Áp dụng cho", "Thửa chọn trên bản đồ") { Opt = new[] { "Thửa chọn trên bản đồ", "Thửa đang xem", "Tất cả thửa" } }); if (r == null) return;
            if (r["m"] == "Tất cả thửa") { f(Dc.P); Msg(title + ": " + Dc.P.Count + " thửa"); } else if (r["m"] == "Thửa đang xem") { if (Cur == null) { Msg("Chưa chọn thửa"); return; } f(new[] { Cur }); Msg(title + ": thửa " + Cur.So); }
            else Pick("Chạm vào thửa", p => { f(new[] { p }); Msg(title + ": thửa " + p.So); });
        }
        void Frame()
        {
            if (!NeedTopo()) return; var c = Cfg.I; var r = Ask.Show(this, "Vẽ khung bản đồ địa chính", new F("to", "Tờ bản đồ số", Dc.To), new F("tl", "Tỷ lệ 1:", c.TyLe.ToString()), new F("xa", "Xã / phường", c.Xa), new F("huyen", "Huyện", c.Huyen), new F("tinh", "Tỉnh", c.Tinh)); if (r == null) return;
            Dc.SetTo(r["to"]); int tl; if (int.TryParse(r["tl"], out tl)) c.TyLe = tl; c.Xa = r["xa"]; c.Huyen = r["huyen"]; c.Tinh = r["tinh"]; c.Save(); Msg(Dc.Frame(Dc.AllBB()));
        }
        // nút "Mở tệp thuộc tính": dùng bảng kết nối tệp thuộc tính
        void OpenGtp() { KetNoiGtp(false); }
        // ⇄ chuyển số tờ ghi trong tệp thuộc tính về số tờ theo tên tờ bản đồ: tệp đang kết nối, thêm nhiều tệp hoặc cả thư mục
        // gộp các tờ bản đồ (.dgn) và tệp thuộc tính (.gtp) của cả xã – chọn tệp ở nhiều thư mục; xong hỏi mở bản đồ toàn xã
        public void GopToanXa()
        {
            string bd, tt;
            using (var f = new GopForm()) { f.ShowDialog(this); bd = f.BanDoGop; tt = f.ThuocTinhGop; }
            if (bd == "" && tt == "") return;
            if (tt != "") { Cfg.I.GtpTx = tt; Cfg.I.Save(); }      // Hồ sơ toàn xã lập từ tệp thuộc tính vừa gộp
            Msg("Đã gộp CSDL toàn xã: " + string.Join(" + ", new[] { bd, tt }.Where(x => x != "").Select(Path.GetFileName).ToArray()) + " – " + Path.GetDirectoryName(bd != "" ? bd : tt));
            // tệp thuộc tính toàn xã: kết nối độc lập (không cần tờ bản đồ ứng với tệp), bảng thuộc tính hiện đủ mọi tờ, mọi thửa
            if (tt != "" && File.Exists(tt) && Confirm("Kết nối độc lập tệp thuộc tính toàn xã " + Path.GetFileName(tt) + " (không cần bản đồ) và mở Bảng thông tin thuộc tính với đủ mọi tờ, mọi thửa?"))
                KetNoiGtpDocLap(tt);
            if (bd != "" && Ms.Connected && Confirm("Mở bản đồ toàn xã " + Path.GetFileName(bd) + " ngay?" + (tt != "" ? "\r\n(tệp thuộc tính toàn xã " + Path.GetFileName(tt) + " cùng tên, cùng thư mục – kết nối như các tờ khác)" : "")))
            {
                try { ((dynamic)Ms.App).OpenDesignFile(bd, false); }
                catch (Exception ex) { Program.Log(ex); Msg("Chưa mở được " + Path.GetFileName(bd) + ": " + ex.Message + " – mở bằng File → Open"); }
            }
        }
        // Hồ sơ toàn xã → Bảng thuộc tính toàn xã: tệp toàn xã (đã chọn / vừa gộp) kết nối độc lập
        public void BangThuocTinhTx() { var p = GtpTx(false); if (p != null) KetNoiGtpDocLap(p); }
        public void ChuyenSoTo()
        {
            var tep = new List<string>(); if (Dc.G != null && !Dc.DocLap) tep.Add(Dc.G.Path_);   // tệp kết nối độc lập (mọi tờ) không chuyển số tờ theo tên tờ bản đồ đang mở
            using (var f = new SoToForm(tep)) { f.ShowDialog(this); if (f.DaChuyen) { Lam(); Msg("Đã chuyển số tờ trong tệp thuộc tính" + (Dc.G != null ? " · bảng thuộc tính tờ " + Dc.To + " – " + Dc.G.Name : "")); } }
        }
        void Push()
        {
            if (Dc.G == null) { Msg("Mở tệp thuộc tính trước"); return; } if (Dc.DocLap) { Msg(Dc.DocLapBao("cập nhật từ bản đồ vào tệp")); return; }
            if (!NeedTopo()) return; var r = Ask.Show(this, "Cập nhật từ bản đồ vào tệp thuộc tính", new F("so", "Số thửa", "1") { Check = true }, new F("dt", "Diện tích theo bản đồ", "1") { Check = true }, new F("ld", "Loại đất (ký hiệu)", "0") { Check = true },
                new F("tam", "Toạ độ tâm thửa", "1") { Check = true }, new F("geom", "Hình thửa (cột hình học, nếu có)", "0") { Check = true }, new F("add", "Thêm thửa chưa có trong tệp", "1") { Check = true }); if (r == null) return;
            Msg(Dc.Push(r["so"] == "1", r["dt"] == "1", r["ld"] == "1", r["tam"] == "1", r["geom"] == "1", r["add"] == "1"));
        }
        void SaveGtp()
        {
            if (Dc.G == null) { Msg("Chưa mở tệp thuộc tính"); return; }
            if (!Dc.G.Dirty && Cfg.I.TuLuu) { Msg("Tệp " + Dc.G.Name + " đã được lưu sau mỗi lần sửa"); return; }
            if (!Confirm("Ghi vào tệp " + Dc.G.Path_ + "?\n(Bản gốc được giữ lại thành .gtp.bak)")) return; Dc.G.Save(Dc.G.Path_); Msg("Đã lưu " + Dc.G.Name);
        }
        void ExportCsv()
        {
            if (!NeedTopo()) return; var d = new SaveFileDialog { Filter = "CSV (Excel)|*.csv", FileName = Path.GetFileNameWithoutExtension(Dc.MapName) + "_thua.csv" }; if (d.ShowDialog(this) != DialogResult.OK) return;
            Reports.Csv(d.FileName); Msg("Đã xuất " + d.FileName); try { System.Diagnostics.Process.Start(d.FileName); } catch { }
        }
        void ImportCsv() { if (!NeedTopo()) return; var d = new OpenFileDialog { Filter = "CSV (lưu từ Excel)|*.csv" }; if (d.ShowDialog(this) != DialogResult.OK) return; Msg("Đã nhập thông tin " + Reports.ImportCsv(d.FileName) + " thửa"); Dc.Notify(); }
        void Lech()
        {
            if (!NeedTopo()) return; Dc.Errs = Dc.P.Where(p => { var r = p.Dtpl ?? p.DtNhan; return r != null && Math.Abs(r.Value - p.Dt) > Math.Max(.1, p.Dt * Cfg.I.Lech / 100); })
                .Select(p => new Err { Text = "Thửa " + p.So + ": bản đồ " + Dc.FD(p.Dt) + " – pháp lý/nhãn " + Dc.FD((p.Dtpl ?? p.DtNhan).Value) + " m²", X = p.Lp[0], Y = p.Lp[1], P = p }).ToList();
            if (errf == null || errf.IsDisposed) { errf = new ErrForm(this); errf.Show(this); } errf.Reload(); Msg(Dc.Errs.Count + " thửa chênh lệch quá " + Cfg.I.Lech + "%");
        }
        void Setup() { var f = new Form { Text = "Thiết lập " + Bt.Ten, Width = 520, Height = 640, StartPosition = FormStartPosition.CenterParent, Font = Font, TopMost = true }; var g = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = Cfg.I, ToolbarVisible = false }; f.Controls.Add(g); var to0 = Cfg.I.To; f.ShowDialog(this); if ((Cfg.I.To ?? "") != (to0 ?? "")) Dc.SetTo(Cfg.I.To); Cfg.I.Save(); Dc.Dirty = true; RefreshTitle(); Host.MenuRefresh(); if (Dc.G != null) { Dc.Link(); Dc.Notify(); } }
        // ---------- đo đạc ----------
        void TrienDiem()
        {
            var d = new OpenFileDialog { Filter = "Tệp toạ độ (*.txt;*.csv;*.dat;*.asc)|*.txt;*.csv;*.dat;*.asc|Tất cả|*.*", Title = "Chọn tệp toạ độ điểm đo" }; if (d.ShowDialog(this) != DialogResult.OK) return;
            var r = Ask.Show(this, "Triển điểm đo", new F("o", "Thứ tự cột (tên, X, Y, H, mã)", "tên,X,Y,H"), new F("b", "Cột X là toạ độ Bắc (VN-2000)", "1") { Check = true }, new F("h", "Ghi độ cao điểm", "0") { Check = true }); if (r == null) return;
            var pts = Nv.ReadPoints(d.FileName, r["o"], r["b"] == "1"); if (pts.Count == 0) { Msg("Không đọc được điểm nào – kiểm tra thứ tự cột"); return; }
            Nv.DrawPoints(pts, r["h"] == "1"); double x0 = pts.Min(p => double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)), y0 = pts.Min(p => double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture)), x1 = pts.Max(p => double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)), y1 = pts.Max(p => double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture));
            Ms.ZoomTo(x0 - 10, y0 - 10, x1 + 10, y1 + 10); Msg("Đã triển " + pts.Count + " điểm lên level " + Cfg.I.DiemLop + " – dùng “Nối điểm thành ranh thửa” để nối");
        }
        void NoiDiem()
        {
            if (Nv.Diem.Count == 0) { Msg("Chưa có điểm – hãy “Triển điểm đo từ tệp toạ độ” trước"); return; }
            var r = Ask.Show(this, "Nối điểm thành ranh thửa (" + Nv.Diem.Count + " điểm)", new F("s", "Chuỗi điểm (ví dụ 1-2-3-4-1; 4-5-6)", "")); if (r == null || r["s"].Trim() == "") return;
            Msg("Đã nối " + Nv.Connect(r["s"]) + " đường ranh – bấm Bản đồ → Tạo thửa để cập nhật thửa");
        }
        // chia thửa theo diện tích: tính đường chia, vẽ trên lớp ranh thửa rồi tách thửa như lệnh Tách thửa (số thửa mới, thửa gốc lưu vào thửa cũ, nhãn, Mẫu số 22)
        void ChiaThua()
        {
            Pick("Chia thửa › chạm vào thửa cần chia", p =>
            {
                var r = Ask.Show(this, "Chia thửa " + p.So + " (" + Dc.FD(p.Dt) + " m²)", new F("a", "Diện tích phần cần tách (m²)", "")); if (r == null) return; double tg;
                if (!double.TryParse(r["a"].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out tg) || tg <= 0 || tg >= p.Dt) { Msg("Diện tích phần tách phải lớn hơn 0 và nhỏ hơn diện tích thửa"); return; }
                Msg("Chọn 2 điểm chỉ HƯỚNG đường chia (ví dụ 2 đầu một cạnh thửa – đường chia sẽ song song)");
                Ms.PickPoint("Chia thửa › điểm 1 chỉ hướng", (ax, ay) => Ms.PickPoint("Chia thửa › điểm 2 chỉ hướng", (bx, by) => Ms.PickPoint("Chia thửa › chạm vào phía phần cần tách", (px, py) =>
                {
                    var line = Nv.SplitLine(p.Rings, ax, ay, bx, by, px, py, tg); var got = Nv.AreaSide(p.Rings, line, px, py);
                    var c = line == null ? null : Dc.ClipToRing(line, p.Rings[0]); if (c == null) { Msg("Không tính được đường chia cắt qua thửa " + p.So); return; }
                    if (!Confirm("Phần tách: " + Dc.FD(got) + " m², phần còn lại: " + Dc.FD(p.Dt - got) + " m².\nVẽ đường chia trên level ranh thửa và tách thửa " + p.So + "?")) { Msg("Đã huỷ chia thửa"); return; }
                    var cu = Dc.P.Where(q => !q.Virtual && !q.FromGtp).ToList();
                    var lr = Cfg.Lvs(Cfg.I.LvRanh); int lv = lr.Contains(Cfg.I.TachLop) || lr.Count == 0 ? Cfg.I.TachLop : lr[0];
                    Ms.AddLine(c, lv, -1, -1); Msg(Dc.Build(false));
                    var a = Dc.Find(px, py); if (a == null) { Msg("Đã vẽ đường chia nhưng chưa dựng được thửa mới – kiểm tra ranh rồi dùng lệnh Tách thửa"); return; }
                    string loi; var nhom = TachHop.NhomTach(new List<Parcel> { a }, cu, out loi);
                    if (nhom == null) { Msg("⚠ " + loi); return; }
                    foreach (var g in nhom) if (!TachMot(g)) return;
                }, () => Msg("Đã huỷ chia thửa")), () => Msg("Đã huỷ chia thửa")), () => Msg("Đã huỷ chia thửa"));
            });
        }
        // ---------- kiểm tra nhãn ↔ tệp thuộc tính ----------
        void DoiChieu()
        {
            if (Dc.G == null) { Msg("Mở tệp thuộc tính trước"); return; } if (Dc.DocLap) { Msg(Dc.DocLapBao("đối chiếu nhãn trên bản đồ với tệp")); return; }
            Msg(Dc.Build(false)); List<Parcel> w; Dc.Errs = Nv.CompareLabels(out w);
            if (errf == null || errf.IsDisposed) { errf = new ErrForm(this); errf.Show(this); } errf.Reload(); Msg(Dc.Errs.Count + " chỗ khác nhau giữa bản đồ và tệp thuộc tính · " + w.Count + " nhãn cần vẽ lại");
        }
        void CapNhatNhan()
        {
            if (Dc.G == null) { Msg("Mở tệp thuộc tính trước"); return; } if (Dc.DocLap) { Msg(Dc.DocLapBao("vẽ lại nhãn theo tệp")); return; }
            Msg(Dc.Build(false)); List<Parcel> w; Nv.CompareLabels(out w); if (w.Count == 0) { Msg("Nhãn đã khớp tệp thuộc tính"); return; }
            if (!Confirm("Vẽ lại nhãn cho " + w.Count + " thửa có nhãn khác tệp thuộc tính (" + string.Join(", ", w.Take(15).Select(p => p.So).ToArray()) + (w.Count > 15 ? "…" : "") + ")?")) return;
            Dc.DrawLabels(w, true); Msg("Đã vẽ lại nhãn " + w.Count + " thửa theo tệp thuộc tính" + Dc.NhanCuBao());
        }
        // ---------- chữ trên bản đồ ----------
        void DoiPhong()
        {
            var r = Ask.Show(this, "Đổi phông chữ trên bản đồ", new F("c", "Chiều đổi", "TCVN3 → Unicode") { Opt = new[] { "TCVN3 → Unicode", "Unicode → TCVN3" } }); if (r == null) return;
            bool toU = r["c"].StartsWith("TCVN3"); if (!Confirm("Đổi mã toàn bộ chữ trên bản đồ " + Dc.MapName + " (" + r["c"] + ")? Dùng Undo của MicroStation nếu cần hoàn tác.")) return;
            int sk; int n = Nv.ReplaceTexts(s => toU ? Tcvn.ToUnicode(s) : Tcvn.FromUnicode(s), out sk);
            Msg("Đã đổi " + n + " chữ" + (sk > 0 ? " · " + sk + " khối chữ nhiều dòng chưa đổi được" : "") + (toU ? " – đổi phông hiển thị sang phông Unicode (Arial, Times New Roman) để xem đúng" : ""));
        }
        void TimThay()
        {
            var r = Ask.Show(this, "Tìm và thay chữ trên bản đồ", new F("f", "Tìm", ""), new F("t", "Thay bằng (để trống = xoá cụm chữ)", ""), new F("w", "Chỉ thay khi trùng toàn bộ nội dung chữ", "0") { Check = true }); if (r == null || r["f"] == "") return;
            string f = r["f"], t = r["t"]; bool whole = r["w"] == "1"; int sk;
            int n = Nv.ReplaceTexts(s => whole ? (s.Trim() == f ? t : s) : s.Replace(f, t), out sk); Msg("Đã thay " + n + " chữ" + (sk > 0 ? " · " + sk + " khối chữ nhiều dòng bỏ qua" : ""));
        }
        // ---------- hồ sơ ----------
        void InNhieu()
        {
            if (!NeedTopo()) return;
            var r = Ask.Show(this, "In hồ sơ nhiều thửa", new F("t", "Các thửa (ví dụ 12, 15-20; trống = tất cả)", ""), new F("k", "Loại hồ sơ", "Trích lục bản đồ địa chính") { Opt = new[] { "Trích lục bản đồ địa chính", "Phiếu đo đạc chỉnh lý thửa đất", "Biên bản xác định ranh giới, mốc giới", "Bảng toạ độ đỉnh thửa" } }); if (r == null) return;
            var ps = Nv.Pick(r["t"]); if (ps.Count == 0) { Msg("Không có thửa nào khớp"); return; } if (ps.Count > 60 && !Confirm("In " + ps.Count + " thửa trong một tệp?")) return;
            Action<Parcel> f = r["k"].StartsWith("Trích") ? (Action<Parcel>)Reports.TrichLuc : r["k"].StartsWith("Phiếu") ? (Action<Parcel>)Reports.Phieu : r["k"].StartsWith("Biên") ? (Action<Parcel>)Reports.BienBan : Reports.BangToaDo;
            Msg("Đã tạo " + Reports.Batch(r["k"] + " – " + ps.Count + " thửa", ps, f) + " hồ sơ – mở trong trình duyệt để in hoặc lưu PDF");
        }
        void QuyHoach()
        {
            if (!NeedTopo()) return;
            var qh = Nv.QhRings(Ms.Read(new[] { Cfg.I.QhLop }, new[] { 3, 4, 6, 12, 14, 15, 16 })); if (qh.Count == 0) { Msg("Không thấy vùng quy hoạch khép kín trên level " + Cfg.I.QhLop + " (Thiết lập → Ranh quy hoạch – level)"); return; }
            var kq = Nv.Planning(Dc.P, qh); if (kq.Count == 0) { Msg("Không có thửa nào nằm trong " + qh.Count + " vùng quy hoạch"); return; }
            Reports.QuyHoach(kq); Msg(kq.Count + " thửa có phần nằm trong quy hoạch · tổng " + Dc.FD(kq.Sum(k => k.Trong)) + " m²");
        }
        void Diagnose()
        {
            var sb = new System.Text.StringBuilder(); Action<string> L = x => sb.AppendLine(x);
            L(Bt.Ten + " (nền M8 Pro Địa chính) – chẩn đoán " + DateTime.Now); L("Chạy " + (Host.InMs ? "trong MicroStation (ứng dụng nạp)" : "bên ngoài MicroStation") + ", " + (IntPtr.Size * 8) + "-bit");
            L("Tệp bản đồ: " + Ms.FullName);
            try { int n = 0; var names = new List<string>(); foreach (Bentley.Interop.MicroStationDGN.Level lv in Ms.App.ActiveDesignFile.Levels) { n++; if (names.Count < 40) { string nm = "?", no = "?"; try { nm = lv.Name; } catch { } try { no = lv.Number.ToString(); } catch { } names.Add(no + "=" + nm); } } L("Số level trong tệp: " + n + " → " + string.Join(", ", names.ToArray())); } catch (Exception ex) { L("Không đọc được danh sách level: " + ex.Message); }
            L("Tờ bản đồ: " + Dc.To + " (theo " + Dc.ToSrc + "); menu trên MicroStation: " + Host.MenuMode);
            L("Thiết lập: level ranh " + Cfg.I.LvRanh + ", level nhãn " + Cfg.I.LvNhan + ", dung sai " + Cfg.I.Tol + " m, tờ " + Dc.To);
            try { var c = Ms.Census(); L("Phần tử theo level · kiểu (3 đường, 4 đường gấp khúc, 6 vùng, 12/14 phức, 15 tròn, 16 cung, 17 chữ, 7 khối chữ, 2 ô):"); foreach (var kv in c.OrderBy(k => k.Key)) L("   " + kv.Key + ": " + kv.Value); } catch (Exception ex) { L("Không duyệt được phần tử: " + ex.Message); }
            try { var r = Ms.Read(Cfg.Lvs(Cfg.I.LvRanh), new[] { 3, 4, 6, 12, 14, 15, 16 }); L("Đọc level ranh: " + Ms.LastRead); var m = r.FirstOrDefault(x => x.Lines.Count > 0); if (m != null) L("   Ví dụ: kiểu " + m.Kind + ", " + m.Lines[0].Length / 2 + " đỉnh, điểm đầu " + m.Lines[0][0].ToString("F3") + "; " + m.Lines[0][1].ToString("F3")); } catch (Exception ex) { L("Lỗi đọc level ranh: " + ex.Message); }
            try { var lv = new HashSet<int>(Cfg.Lvs(Cfg.I.LvNhan)); var r = Ms.Read(lv, new[] { 17, 7 }); L("Đọc chữ nhãn: " + Ms.LastRead + "; ví dụ: " + string.Join(" | ", r.Take(6).Select(x => x.Text).ToArray())); } catch (Exception ex) { L("Lỗi đọc chữ: " + ex.Message); }
            try { L("Tạo thửa: " + Dc.Build(false)); } catch (Exception ex) { L("Lỗi tạo thửa: " + ex.Message + "\n" + ex.StackTrace); }
            if (Dc.G != null) L("Tệp thuộc tính: " + Dc.G.Path_ + " – " + Dc.GRows.Count + " thửa; các tờ: " + string.Join(", ", Dc.GRows.Select(x => x.To).Distinct().Take(20).ToArray()) + "; liên kết " + Dc.P.Count(p => p.Gid != null) + "/" + Dc.P.Count);
            else L("Chưa mở tệp thuộc tính");
            L("Lập hồ sơ: " + (Cfg.I.ChayNgam ? "chạy ngầm, " + Web.EngineInfo() : "cửa sổ hồ sơ riêng") + "; thư mục hồ sơ: " + Web.OutDir() + "; bộ mẫu: " + (Web.Html ?? "(thiếu BienTapBDDC.html)"));
            var f = new Form { Text = "Chẩn đoán – chụp màn hình hoặc bấm Sao chép rồi gửi tác giả", Width = 760, Height = 560, StartPosition = FormStartPosition.CenterScreen, TopMost = true };
            var t = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 9f), Text = sb.ToString(), WordWrap = false };
            var b = GiaoDien.Nut("Sao chép", 100, false); b.Dock = DockStyle.Bottom; b.Click += (s2, e2) => { try { Clipboard.SetText(t.Text); b.Text = "Đã sao chép"; } catch { } };
            f.Controls.Add(t); f.Controls.Add(b); f.ShowDialog(this);
        }
        void About() { MessageBox.Show(this, Bt.Ten + " – phiên bản " + CapNhat.Ngay + " (" + CapNhat.PhienBan + ")\n(nền M8 Pro Địa chính, kế thừa M8 Mobile)\nCông cụ biên tập bản đồ địa chính làm việc trực tiếp với MicroStation V8i.\n\nTác giả, bản quyền: Nguyễn Xuân Thủy", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }

    // ---------- Bảng thông tin thuộc tính: thanh lệnh (nhóm, menu thả), hàng lọc dưới tiêu đề cột, dòng tổng diện tích, thanh chuyển dòng ----------
    public class AttrForm : FormBase
    {
        MainForm M; DataGridView g = new DataGridView(); DataTable T = new DataTable(); ToolStrip tb = new ToolStrip(); ToolStripTextBox flt = new ToolStripTextBox { Width = 150 };
        ThanhChuyen nav = new ThanhChuyen("thửa"); ToolStripLabel s2 = new ToolStripLabel { ForeColor = Color.FromArgb(40, 60, 90), Margin = new Padding(16, 1, 0, 2) };
        Panel chan = new Panel { Dock = DockStyle.Bottom, Height = 22 }; Dictionary<string, Label> tong = new Dictionary<string, Label>(); Dictionary<string, OLoc> loc = new Dictionary<string, OLoc>();
        List<object> rows = new List<object>(); bool loading, syncing; Timer tmSel = new Timer { Interval = 700 }; string lastSel = ""; Font fDam;
        string locTo = "";   // kết nối độc lập: chỉ hiện một tờ ("" = mọi tờ)
        int dlTo; double dlDt;   // kết nối độc lập: số tờ, tổng diện tích trong tệp (tính khi nạp bảng)
        bool canVua = true;      // nạp bảng mới: cột vừa lại chữ
        const int CAO_LOC = 22; int caoTd = 24;   // tiêu đề cột: tên cột một dòng + ô lọc
        // nút chọn / chuyển số tờ của bảng: theo tên tờ bản đồ đang mở, hoặc tờ khác có trong tệp thuộc tính (nhớ riêng cho từng tệp bản đồ)
        ToolStripDropDownButton bTo = new ToolStripDropDownButton { ToolTipText = "Chọn số tờ cho bảng thuộc tính: theo tên tờ bản đồ đang mở, hoặc tờ khác có trong tệp thuộc tính (nhớ riêng cho tệp bản đồ này)" };
        // cột của bảng (đúng thứ tự hiện): thông tin thửa, hình thức sử dụng, rồi đủ trường căn cước của từng người sử dụng
        // (người 1 như chủ sử dụng; vợ/chồng, thành viên hộ, người đồng sử dụng: tự thêm nhóm cột “Người 2 – …”, “Người 3 – …” theo số người nhiều nhất của một thửa)
        static readonly string[][] C_DAU = { new[] { "to", "Số tờ" }, new[] { "so", "Số thửa" }, new[] { "ld", "Loại đất" }, new[] { "dtg", "Diện tích (m²)" }, new[] { "mdct", "Diện tích chi tiết" }, new[] { "diachi", "Địa chỉ thửa" }, new[] { "ht", "Hình thức sử dụng" } };
        static readonly string[][] C_CUOI = { new[] { "ghichu", "Ghi chú" }, new[] { "lk", "Liên kết" } };
        List<string[]> C = new List<string[]>(); int soNguoi = -1;
        // tên cột gọn trên một dòng (rê chuột lên tên cột thấy tên đầy đủ); người thứ 2 trở đi thêm số thứ tự: "Họ và tên 2"
        static readonly Dictionary<string, string> GON = new Dictionary<string, string> { { "ten", "Họ và tên" }, { "lgt", "Giấy tờ" }, { "so", "Số định danh" } };
        static string Gon(TruongNguoi t) { string g; return GON.TryGetValue(t.K, out g) ? g : t.L; }
        List<List<NguoiSD>> nguoi = new List<List<NguoiSD>>(); List<string> htDong = new List<string>();   // theo dòng (_i)
        static readonly HashSet<string> EDIT = new HashSet<string> { "so", "ld", "diachi", "ghichu", "ht" };
        static readonly HashSet<string> NUM = new HashSet<string> { "dtg" };   // chỉ 2 cột diện tích: diện tích thửa và diện tích chi tiết theo mục đích
        static readonly System.Globalization.CultureInfo IC = System.Globalization.CultureInfo.InvariantCulture;
        static string KeyNguoi(int i, string f) { return "n" + i + "_" + f; }
        static bool LaNguoi(string k, out int i, out string f)
        {
            i = 0; f = null; if (k == null || !k.StartsWith("n")) return false; int u = k.IndexOf('_'); if (u < 2 || !int.TryParse(k.Substring(1, u - 1), out i)) return false; f = k.Substring(u + 1); return true;
        }
        // mẫu hồ sơ lập cho thửa đang chọn: mã lệnh – tên
        public static readonly string[][] HS = { new[] { "thp", "Phiếu đo đạc chỉnh lý thửa đất (Phụ lục 17)" }, new[] { "ttl", "Trích lục bản đồ địa chính (Phụ lục 19)" }, new[] { "hsht", "Phiếu xác nhận kết quả đo đạc hiện trạng thửa đất (Phụ lục 14)" },
            new[] { "hsmt", "Bản mô tả ranh giới, mốc giới thửa đất (Phụ lục 12)" }, new[] { "hsgc", "Sơ đồ thửa đất in trên Giấy chứng nhận" }, new[] { "hstd", "Bảng thống kê tọa độ điểm mốc ranh giới (Phụ lục 14a)" }, new[] { "hsdn", "Đơn đăng ký đất đai, tài sản gắn liền với đất (Mẫu số 15)" } };
        // cách viết số như mọi chỗ khác của công cụ (dấu thập phân theo Thiết lập, không tách nghìn)
        static System.Globalization.NumberFormatInfo DinhDangSo()
        {
            var nf = (System.Globalization.NumberFormatInfo)System.Globalization.CultureInfo.InvariantCulture.NumberFormat.Clone();
            nf.NumberDecimalSeparator = "."; nf.NumberGroupSeparator = ""; return nf;   // diện tích: dạng số, dấu “.” thập phân
        }
        public AttrForm(MainForm m)
        {
            M = m; Text = "Bảng thông tin thuộc tính"; Font = m.Font; fDam = new Font(Font, FontStyle.Bold); Width = 1180; Height = 420; MinimumSize = new Size(640, 300); g.Tag = "lon"; g.ShowCellToolTips = true; GiaoDien.KhongCat(this); StartPosition = FormStartPosition.Manual; var wa = Screen.PrimaryScreen.WorkingArea; Location = new Point(wa.Left + 40, wa.Bottom - Height - 30); ShowInTaskbar = false; Icon = m.Icon;
            tb.ImageScalingSize = new Size(18, 18); tb.GripStyle = ToolStripGripStyle.Hidden; tb.Padding = new Padding(2, 1, 2, 1);
            tb.LayoutStyle = ToolStripLayoutStyle.Flow; nav.LayoutStyle = ToolStripLayoutStyle.Flow;   // hẹp thì xuống hàng, không giấu lệnh, không cắt chữ
            Func<string, string, Action, ToolStripButton> B = (t, ic, a) => { var b = new ToolStripButton(t, Icons.Get(ic, 16), (s, e) => { try { a(); } catch (Exception ex) { M.Msg("Lỗi: " + ex.Message); } }); return b; };
            Func<string, string, ToolStripDropDownButton> D = (t, ic) => new ToolStripDropDownButton(t, Icons.Get(ic, 16));
            Action<ToolStripDropDownButton, string, string, Action> DI = (dd, t, ic, a) => dd.DropDownItems.Add(new ToolStripMenuItem(t, ic != null ? Icons.Get(ic, 16) : null, (s, e) => { try { a(); } catch (Exception ex) { M.Msg("Lỗi: " + ex.Message); } }));
            try { bTo.Image = Icons.Get("layers", 16); } catch { } tb.Items.Add(bTo); tb.Items.Add(new ToolStripSeparator());
            tb.Items.Add(B("Thông tin chi tiết", "info", () => { var p = Sel(); if (p != null) M.ShowParcel(p); }));
            tb.Items.Add(B("Toàn bộ thông tin", "list", () => { var r = SelRow(); if (r == null) { M.Msg("Thửa chưa liên kết tệp thuộc tính"); return; } new GtpForm(r, () => { Reload(); }).Show(this); }));
            tb.Items.Add(new ToolStripSeparator());
            // hồ sơ của thửa đang chọn – không phải chạm lại thửa trên bản đồ
            var dHs = D("Hồ sơ thửa", "doc");
            foreach (var h in HS) { var hh = h; if (hh[0] == "hsdn") dHs.DropDownItems.Add(new ToolStripSeparator()); DI(dHs, hh[1], Icons.For(hh[1], "doc"), () => HoSo(hh[0], hh[1])); }
            dHs.DropDownItems.Add(new ToolStripSeparator()); DI(dHs, "Bảng hồ sơ thửa đất…", "frame", () => M.ShowHoSo(Sel()));
            tb.Items.Add(dHs);
            var dTi = D("Tiện ích", "gear");
            DI(dTi, "Vẽ nhãn thửa", "tag", () => { var p = SelMap(); if (p != null) { Dc.DrawLabels(new[] { p }, true); M.Msg("Đã vẽ nhãn thửa " + p.So + Dc.NhanCuBao()); } });
            DI(dTi, "Ghi kích thước cạnh", "dim", () => { var p = SelMap(); if (p != null) { Dc.Dims(new[] { p }); M.Msg("Đã ghi kích thước cạnh thửa " + p.So); } });
            DI(dTi, "Đánh số hiệu đỉnh thửa", "vmove", () => { var p = SelMap(); if (p != null) { Dc.VertNums(new[] { p }); M.Msg("Đã đánh số hiệu đỉnh thửa " + p.So); } });
            tb.Items.Add(dTi);
            // bên phải: tìm nhanh, xuất dữ liệu, lấy lại dữ liệu, phóng tới
            flt.Control.MinimumSize = new Size(150, 0); flt.ToolTipText = "Tìm nhanh trong mọi cột"; flt.TextChanged += (s, e) => Filter();
            var dX = D("Xuất dữ liệu", "export");
            DI(dX, "Danh sách thửa ra Excel (CSV)…", "export", () =>
            {
                var ten = Dc.DocLap ? System.IO.Path.GetFileNameWithoutExtension(Dc.G.Name) + "_cac_to" : System.IO.Path.GetFileNameWithoutExtension(Dc.MapName) + "_thua";
                var d = new SaveFileDialog { Filter = "CSV (Excel)|*.csv", FileName = ten + ".csv" }; if (d.ShowDialog(this) != DialogResult.OK) return;
                if (Dc.DocLap) XuatBang(d.FileName); else Reports.Csv(d.FileName); M.Msg("Đã xuất " + d.FileName);
            });
            DI(dX, "Sổ mục kê (bản xem nhanh)", "list", () => { if (Dc.DocLap) M.Msg("Kết nối độc lập (mọi tờ): dùng Hồ sơ toàn xã → Sổ mục kê đất đai toàn xã"); else Reports.SoMucKe(); });
            var bLay = B("Lấy lại dữ liệu", "refresh", () => { if (Dc.DocLap) { Dc.GRows = Dc.G.Rows(); Dc.Notify(); M.Msg("Đã đọc lại " + Dc.G.Name + ": " + Dc.GRows.Count + " thửa"); } else M.Msg(Dc.Build(false)); });
            var bPh = B("Phóng tới", "zin", () => { var p = Sel(); if (p == null) return; if (p.Rings == null) { M.Msg("Thửa " + p.So + " không có hình trong tệp thuộc tính"); return; } Dc.Zoom(p, .6); if (Dc.DocLap) Ms.Hilite(p.Rings); });
            var bXoa = new ToolStripButton("✕") { ToolTipText = "Bỏ mọi điều kiện lọc" }; bXoa.Click += (s, e) => { foreach (var x in loc.Values) x.Text = ""; flt.Text = ""; };
            tb.Items.Add(new ToolStripSeparator()); tb.Items.Add(bPh); tb.Items.Add(bLay); tb.Items.Add(dX); tb.Items.Add(new ToolStripSeparator());
            tb.Items.Add(new ToolStripLabel("Tìm:")); tb.Items.Add(flt); tb.Items.Add(bXoa);
            g.Dock = DockStyle.Fill; g.AllowUserToAddRows = false; g.AllowUserToDeleteRows = false; g.SelectionMode = DataGridViewSelectionMode.FullRowSelect; g.MultiSelect = false; g.RowHeadersWidth = 24; g.BackgroundColor = Color.White;
            g.AlternatingRowsDefaultCellStyle.BackColor = GiaoDien.NenXen; g.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2; g.BorderStyle = BorderStyle.None;
            // tiêu đề cột hai tầng: tên cột ở trên, ô lọc ở dưới
            g.EnableHeadersVisualStyles = false; g.ColumnHeadersDefaultCellStyle.BackColor = GiaoDien.NenNhom; g.ColumnHeadersDefaultCellStyle.ForeColor = GiaoDien.ChuNhom; g.ColumnHeadersDefaultCellStyle.Font = fDam;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing; g.ColumnHeadersHeight = caoTd + CAO_LOC + 3; g.AllowUserToResizeColumns = true;
            g.CellPainting += (s, e) =>
            {
                if (e.RowIndex != -1) return; e.PaintBackground(e.CellBounds, false);
                if (e.ColumnIndex >= 0)
                {
                    var col = g.Columns[e.ColumnIndex]; var txt = Convert.ToString(col.HeaderText) + (col.HeaderCell.SortGlyphDirection == SortOrder.Ascending ? " ▲" : col.HeaderCell.SortGlyphDirection == SortOrder.Descending ? " ▼" : "");
                    TextRenderer.DrawText(e.Graphics, txt, fDam, new Rectangle(e.CellBounds.X + 4, e.CellBounds.Y + 2, e.CellBounds.Width - 8, caoTd - 2), GiaoDien.ChuNhom, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                e.Handled = true;
            };
            foreach (var k in NUM) { var l = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Font = fDam, ForeColor = GiaoDien.ChuNhom, BackColor = Color.Transparent }; chan.Controls.Add(l); tong[k] = l; }
            DungCot(1);
            chan.BackColor = GiaoDien.NenNhom; chan.Paint += (s, e) => { using (var p = new Pen(GiaoDien.VienNhom)) e.Graphics.DrawLine(p, 0, 0, chan.Width, 0); };
            var tc = new Label { Text = "Cộng", AutoSize = false, Font = fDam, ForeColor = GiaoDien.ChuNhom, TextAlign = ContentAlignment.MiddleLeft, Location = new Point(28, 1), Size = new Size(60, 20), BackColor = Color.Transparent }; chan.Controls.Add(tc);
            g.ColumnWidthChanged += (s, e) => ViTri(); g.Scroll += (s, e) => ViTri(); g.SizeChanged += (s, e) => ViTri(); g.ColumnDisplayIndexChanged += (s, e) => ViTri(); g.ColumnStateChanged += (s, e) => { if (e.StateChanged == DataGridViewElementStates.Visible || e.StateChanged == DataGridViewElementStates.Frozen) ViTri(); };
            g.DataBindingComplete += (s, e) =>
            {
                int ii; string ff;
                foreach (var c in C) { var col = g.Columns[c[0]]; if (col == null) continue; col.HeaderText = c[1]; col.ToolTipText = c.Length > 2 ? c[2] : c[1]; col.ReadOnly = !EDIT.Contains(c[0]) && !LaNguoi(c[0], out ii, out ff); if (NUM.Contains(c[0])) { col.DefaultCellStyle.Format = "F" + Cfg.I.DtLe; col.DefaultCellStyle.FormatProvider = DinhDangSo(); col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; } }
                g.Columns["_i"].Visible = false; g.Columns["_n"].Visible = false; g.Columns["so"].SortMode = DataGridViewColumnSortMode.Programmatic; g.Columns["to"].SortMode = DataGridViewColumnSortMode.Programmatic;
                // cột vừa tiêu đề và chữ dài nhất của bảng (một lần mỗi lần nạp bảng; chữ dài hơn 560 điểm ảnh: rê chuột lên ô xem đủ)
                if (canVua) { canVua = false; VuaCotGon(); }
                for (int i = 0; i < C.Count; i++) if (g.Columns[C[i][0]] != null) g.Columns[C[i][0]].DisplayIndex = i;
                // số tờ, số thửa đứng yên khi trượt ngang
                g.Columns["to"].Frozen = true; g.Columns["so"].Frozen = true; g.Columns["so"].DividerWidth = 1;
                ViTri();
            };
            // số thửa xếp theo giá trị số (1, 2, … 10), không theo chữ
            g.ColumnHeaderMouseClick += (s, e) =>
            {
                var ten = g.Columns[e.ColumnIndex].Name; if (ten != "so" && ten != "to") return; bool asc = !T.DefaultView.Sort.StartsWith("_n ASC");
                T.DefaultView.Sort = "_n " + (asc ? "ASC" : "DESC"); foreach (var k in new[] { "so", "to" }) g.Columns[k].HeaderCell.SortGlyphDirection = k == ten ? (asc ? SortOrder.Ascending : SortOrder.Descending) : SortOrder.None; g.Invalidate();
            };
            g.CellClick += (s, e) => { if (e.RowIndex < 0) return; if (Dc.DocLap) { UpStat(); return; } var p = Sel(); if (p != null) { M.Cur = p; Dc.Zoom(p, .6); if (!p.Virtual) Ms.Hilite(p.Rings); } UpStat(); };   // kết nối độc lập: không tự phóng bản vẽ (bấm Phóng tới)
            // chọn dòng (cả bằng phím mũi tên) → thửa sáng trên bản đồ; chọn thửa trên bản đồ → dòng được chọn trong bảng
            g.SelectionChanged += (s, e) => { if (loading || syncing) return; var p = SelQuiet(); if (p != null && !p.Virtual) { M.Cur = p; Ms.Hilite(p.Rings); } UpStat(); };
            tmSel.Tick += (s, e) => FromMap(); tmSel.Start(); FormClosed += (s, e) => { tmSel.Stop(); Ms.Hilite(null); };
            g.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && g.Columns[e.ColumnIndex].ReadOnly) { var r = SelRow(); if (r != null && Dc.G != null) new GtpForm(r, () => Reload()).Show(this); else { var p = Sel(); if (p != null) M.ShowParcel(p); } } };
            // ô của người sử dụng: chỉ sửa được khi thửa có người đó (thêm người ở Thông tin chi tiết)
            g.CellBeginEdit += (s, e) =>
            {
                int ni; string nf; var k = g.Columns[e.ColumnIndex].Name; if (!LaNguoi(k, out ni, out nf)) return;
                var dr = ((DataRowView)g.Rows[e.RowIndex].DataBoundItem).Row; var ds = nguoi[(int)dr["_i"]];
                if (ds.Count < ni) { e.Cancel = true; M.Msg("Thửa này chưa có người thứ " + ni + " – thêm người sử dụng ở Thông tin chi tiết (theo hình thức sử dụng)"); }
            };
            g.CellEndEdit += (s, e) =>
            {
                if (loading) return; var dr = ((DataRowView)g.Rows[e.RowIndex].DataBoundItem).Row; var k = g.Columns[e.ColumnIndex].Name; int di = (int)dr["_i"]; var o = rows[di]; var v = Convert.ToString(dr[k], System.Globalization.CultureInfo.InvariantCulture);
                try
                {
                    Parcel p; if (o is Parcel) p = (Parcel)o; else { var r = (GRow)o; p = Dc.Virtual(r) ?? new Parcel { Gid = r.Id, Row = r, So = r.So, Virtual = true }; }
                    int ni; string nf; bool tep = Dc.G != null && p.Gid != null && ChuSD.CoBang(Dc.G), laNguoi = LaNguoi(k, out ni, out nf);
                    if (k == "ht" || laNguoi)
                    {
                        var ds = nguoi[di];
                        if (k == "ht")
                        {
                            v = (v ?? "").Trim();
                            if (v != "" && !ChuSD.TenHinhThuc.Contains(v))
                            {
                                var hop = ChuSD.TenHinhThuc.Where(h => h.IndexOf(v, StringComparison.CurrentCultureIgnoreCase) >= 0).ToArray();
                                if (hop.Length != 1) { M.Msg("Hình thức sử dụng “" + v + "” không có trong danh sách – bấm biểu tượng lọc của cột để xem các hình thức theo Thông tư 10/2024/TT-BTNMT"); dr[k] = htDong[di]; return; }
                                v = hop[0];
                            }
                            dr[k] = v; htDong[di] = v;
                            if (tep) ChuSD.GhiHT(Dc.G, p.Gid, v); else ChuSD.GhiMay(p.So, p.So, v, ds);
                        }
                        else
                        {
                            var n = ds[ni - 1]; n[nf] = (v ?? "").Trim();
                            if (tep && n.Id != null) { ChuSD.GhiMot(Dc.G, p.Gid, n.Id, nf, n[nf]); if (ni == 1 && nf == "ten") { p.Chu = n[nf]; if (p.Row != null) p.Row.Chu = n[nf]; } }
                            else { if (ni == 1 && nf == "ten") Dc.SetField(p, "chu", n[nf]); ChuSD.GhiMay(p.So, p.So, htDong[di], ds); }
                        }
                        string sv = tep ? Dc.Commit() : "";
                        M.Msg(tep ? (sv != "" ? "Đã lưu vào " + Dc.G.Name : "Đã sửa trong tệp thuộc tính – nhớ bấm Lưu") : "Đã cập nhật (lưu trên máy)"); UpStat(); return;
                    }
                    var toDong = Dc.DocLap && p.Row != null ? p.Row.To : Dc.To;
                    if (k == "so" && v != "" && Dc.DocLap ? Dc.GRows.Any(q => q != p.Row && q.To == toDong && q.So == v) : k == "so" && v != "" && (Dc.P.Any(q => q != p && q.So == v && q.Row != p.Row) || Dc.SheetRows().Any(q => q != p.Row && q.So == v))) { M.Msg("Số thửa " + v + " đã có trong tờ " + toDong); dr[k] = p.So; return; }
                    Dc.SetField(p, k, v); var rw = p.Row; if (rw != null && k == "ld") dr["mdct"] = rw.MdText;
                    M.Msg(p.Gid != null ? (Cfg.I.TuLuu ? "Đã lưu vào " + Dc.G.Name : "Đã sửa trong tệp thuộc tính – nhớ bấm Lưu") + (k == "so" || k == "ld" ? " · bấm “Vẽ nhãn” (menu Bản đồ) để cập nhật nhãn trên bản đồ" : "") : "Đã cập nhật (thửa chưa liên kết tệp thuộc tính – lưu trên máy)");
                    UpStat();
                }
                catch (Exception ex) { Program.Log(ex); M.Msg("Lỗi: " + ex.Message); MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            // ô diện tích: nhận cả 1200,5 và 1200.5; xoá trắng = bỏ giá trị
            g.CellParsing += (s, e) =>
            {
                if (e.ColumnIndex < 0 || !NUM.Contains(g.Columns[e.ColumnIndex].Name)) return; var t = Convert.ToString(e.Value).Trim(); double d;
                if (t == "") { e.Value = DBNull.Value; e.ParsingApplied = true; } else if (GiaoDien.So(t, out d)) { e.Value = d; e.ParsingApplied = true; }
            };
            g.DataError += (s, e) => { e.ThrowException = false; };
            // thanh chuyển dòng: về đầu, lùi, tới, về cuối – bản đồ phóng tới thửa
            nav.Chuyen += d =>
            {
                int n = g.Rows.Count; if (n == 0) return; int i0 = g.CurrentRow != null ? g.CurrentRow.Index : -1;
                int j = d == -2 ? 0 : d == 2 ? n - 1 : Math.Max(0, Math.Min(n - 1, i0 + d)); g.CurrentCell = g.Rows[j].Cells[g.Columns["so"].Index];
                var p = SelQuiet(); if (p != null && !p.Virtual) Dc.Zoom(p, .6); UpStat();
            };
            nav.Items.Add(s2);
            Controls.Add(g); Controls.Add(chan); Controls.Add(tb); Controls.Add(nav); Reload();
        }
        // cột gọn: vừa tên cột (một dòng, chừa chỗ dấu sắp xếp) và chữ dài nhất của ô (tối đa 260 điểm ảnh, rê chuột xem đủ)
        // kéo mép tiêu đề để co giãn cột như Excel
        void VuaCotGon()
        {
            var f = g.DefaultCellStyle.Font ?? g.Font; var cu = IC;
            foreach (DataGridViewColumn col in g.Columns)
            {
                if (!col.Visible) continue; col.Resizable = DataGridViewTriState.True; col.MinimumWidth = 28;
                int w = TextRenderer.MeasureText(col.HeaderText ?? "", fDam).Width + 12, td = w + (col.Name == "to" || col.Name == "so" ? 14 : 0);   // số tờ, số thửa: chừa chỗ dấu ▲▼
                if (T.Columns.Contains(col.Name))
                {
                    var dc = T.Columns[col.Name]; var dai = new List<string>(); int n = 0;
                    foreach (DataRow r in T.Rows)
                    {
                        if (n++ > 20000) break; var v = r[dc]; if (v == DBNull.Value || v == null) continue;
                        var t = v is double ? GiaoDien.SoDT((double)v) : Convert.ToString(v, cu); if (t.Length > 0) dai.Add(t);
                        if (dai.Count > 400) dai = dai.OrderByDescending(x => x.Length).Take(30).ToList();
                    }
                    foreach (var t in dai.OrderByDescending(x => x.Length).Take(30)) w = Math.Max(w, TextRenderer.MeasureText(t, f).Width + 10);
                }
                col.Width = Math.Max(Math.Max(34, td), Math.Min(w, 260));
            }
        }
        // ô lọc nằm dưới tên cột, dòng tổng nằm dưới cột diện tích – theo vị trí cột đang hiện
        void ViTri()
        {
            foreach (var kv in loc)
            {
                var col = g.Columns[kv.Key]; Label l0; if (col == null || !col.Visible) { kv.Value.Visible = false; if (tong.TryGetValue(kv.Key, out l0)) l0.Visible = false; continue; }
                var r = g.GetColumnDisplayRectangle(col.Index, true);
                if (r.Width < 12) { kv.Value.Visible = false; } else { kv.Value.SetBounds(r.X + 1, caoTd + 1, r.Width - 2, CAO_LOC); kv.Value.Visible = true; }
                Label l; if (tong.TryGetValue(kv.Key, out l)) { if (r.Width < 12) l.Visible = false; else { l.SetBounds(g.Left + r.X, 1, r.Width - 4, 20); l.Visible = true; } }
            }
        }
        static double SoN(string so) { double v; return double.TryParse(so, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 1e12; }
        Parcel Sel()
        {
            if (g.CurrentRow == null) return null; var dr = ((DataRowView)g.CurrentRow.DataBoundItem).Row; var o = rows[(int)dr["_i"]]; if (o is Parcel) return (Parcel)o;
            if (Dc.DocLap) { var q = Dc.RowParcel((GRow)o); M.Msg("Thửa " + q.So + " · tờ " + ((GRow)o).To + " (tệp thuộc tính kết nối độc lập)"); return q; }
            var p = Dc.Virtual((GRow)o); if (p == null) M.Msg("Thửa này không có hình trong tệp thuộc tính"); else M.Msg("Thửa " + p.So + " theo hình trong tệp thuộc tính (chưa tạo thửa từ bản đồ)"); return p;
        }
        // thửa trên bản đồ của dòng đang chọn (lệnh vẽ lên bản đồ, lập hồ sơ)
        Parcel SelMap()
        {
            var p = Sel(); if (p == null || !p.Virtual) return p;
            M.Msg(Dc.DocLap ? "Bảng đang xem tệp thuộc tính kết nối độc lập (mọi tờ) – lập hồ sơ, vẽ lên bản đồ: mở tờ bản đồ của thửa rồi kết nối tệp theo tờ (Hệ thống → Kết nối tệp thuộc tính)" : "Thửa " + p.So + " chưa có trên bản đồ (chỉ có trong tệp thuộc tính) – tạo thửa từ bản đồ trước"); return null;
        }
        void HoSo(string id, string name) { var p = SelMap(); if (p == null) { if (g.CurrentRow == null) M.Msg("Chọn một dòng thửa trong bảng trước"); return; } M.BtForm(id, name, true, p, true); }
        Parcel SelQuiet() { if (g.CurrentRow == null || g.CurrentRow.DataBoundItem == null) return null; var o = rows[(int)((DataRowView)g.CurrentRow.DataBoundItem).Row["_i"]]; return o as Parcel; }
        // chọn dòng của thửa (không phóng bản vẽ)
        public void SelectParcel(Parcel p)
        {
            if (p == null) return; syncing = true;
            try
            {
                foreach (DataGridViewRow r in g.Rows)
                {
                    var o = rows[(int)((DataRowView)r.DataBoundItem).Row["_i"]];
                    if (o == p || (p.Row != null && o == p.Row)) { g.CurrentCell = r.Cells[g.Columns["so"].Index]; if (!r.Displayed) g.FirstDisplayedScrollingRowIndex = Math.Max(0, r.Index - 3); break; }
                }
            }
            catch { }
            finally { syncing = false; }
            if (!p.Virtual) Ms.Hilite(p.Rings); UpStat();
        }
        // phần tử đang chọn trong bản vẽ (nhãn, chữ, ô, vùng) nằm trong thửa nào thì chọn dòng thửa đó
        void FromMap()
        {
            if (!Visible || loading || Dc.P.Count == 0 || !Ms.Connected) return;
            List<Bentley.Interop.MicroStationDGN.Element> sel; try { sel = Ms.Selected(); } catch { return; }
            if (sel.Count == 0 || sel.Count > 30) { lastSel = ""; return; }
            var key = string.Join(",", sel.Select(x => Ms.Id(x).ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray()); if (key == lastSel) return; lastSel = key;
            var pg = new Dc.PGrid(Dc.P); Parcel hit = null;
            foreach (var el in sel)
            {
                int t; double[] R; try { t = (int)el.Type; R = Ms.Range(el); } catch { continue; }
                if (t != 17 && t != 7 && t != 2 && t != 6 && t != 14) continue;
                hit = pg.Find((R[0] + R[2]) / 2, (R[1] + R[3]) / 2); if (hit != null) break;
            }
            if (hit != null && hit != SelQuiet()) { M.Cur = hit; SelectParcel(hit); }
        }
        GRow SelRow() { if (g.CurrentRow == null) return null; var dr = ((DataRowView)g.CurrentRow.DataBoundItem).Row; var o = rows[(int)dr["_i"]]; return o is GRow ? (GRow)o : ((Parcel)o).Row; }
        // danh sách tờ: “Theo tên tờ bản đồ” + các tờ có trong tệp thuộc tính (kèm số thửa); đánh dấu tờ đang dùng
        void NapTo()
        {
            if (Dc.DocLap) { NapToDocLap(); return; }
            bTo.Text = "Tờ " + (Dc.To != "" ? Dc.To : "?"); bTo.DropDownItems.Clear(); var tu = Dc.ToTheoTen(); var chon = (Cfg.I.To ?? "").Trim();
            Action<string, string> them = (to, ten) => { var it = new ToolStripMenuItem(ten) { Checked = to == chon, Tag = to }; it.Click += (s, e) => BeginInvoke(new Action(() => { try { DoiTo(to); } catch (Exception ex) { Program.Log(ex); M.Msg("Lỗi: " + ex.Message); } })); bTo.DropDownItems.Add(it); };
            them("", "Theo tên tờ bản đồ" + (tu != "" ? " – tờ " + tu : " (tên tệp không có số tờ)"));
            bTo.DropDownItems.Add(new ToolStripSeparator());
            var dem = Dc.GRows.GroupBy(r => r.To ?? "").ToDictionary(gr => gr.Key, gr => gr.Count());
            var ds = dem.Keys.Where(k => k != "").ToList(); if (chon != "" && !ds.Contains(chon)) ds.Add(chon);
            foreach (var k in ds.OrderBy(k => SoN(k)).ThenBy(k => k, StringComparer.Ordinal)) them(k, "Tờ " + k + (dem.ContainsKey(k) ? " · " + dem[k] + " thửa trong tệp" : " · chưa có trong tệp") + (k == tu ? " (theo tên)" : ""));
            if (ds.Count == 0) bTo.DropDownItems.Add(new ToolStripMenuItem(Dc.G != null ? "(tệp thuộc tính chưa ghi số tờ)" : "(chưa kết nối tệp thuộc tính)") { Enabled = false });
            bTo.DropDownItems.Add(new ToolStripSeparator());
            var cv = new ToolStripMenuItem("⇄ Chuyển số tờ ghi trong tệp thuộc tính theo tên tờ bản đồ" + (tu != "" ? " (tờ " + tu + ")" : "") + "…"); cv.Click += (s, e) => BeginInvoke(new Action(() => { try { M.ChuyenSoTo(); } catch (Exception ex) { Program.Log(ex); M.Msg("Lỗi: " + ex.Message); } }));
            bTo.DropDownItems.Add(cv);
        }
        // kết nối độc lập: nút Tờ lọc bảng theo tờ (mọi tờ / từng tờ); đổi sang kết nối theo tờ bản đồ, ngắt kết nối
        void NapToDocLap()
        {
            var ds = Dc.CacTo(); if (locTo != "" && !ds.Any(kv => kv.Key == locTo)) locTo = "";
            bTo.Text = locTo == "" ? "Mọi tờ" : "Tờ " + locTo; bTo.DropDownItems.Clear();
            Action<string, string> them = (to, ten) => { var it = new ToolStripMenuItem(ten) { Checked = to == locTo }; it.Click += (s, e) => BeginInvoke(new Action(() => { locTo = to; NapToDocLap(); Filter(); })); bTo.DropDownItems.Add(it); };
            them("", "Mọi tờ (" + ds.Count(kv => kv.Key != "") + " tờ · " + Dc.GRows.Count + " thửa)"); bTo.DropDownItems.Add(new ToolStripSeparator());
            foreach (var kv in ds) them(kv.Key, (kv.Key == "" ? "(chưa ghi số tờ)" : "Tờ " + kv.Key) + " · " + kv.Value + " thửa");
            bTo.DropDownItems.Add(new ToolStripSeparator());
            var kn = new ToolStripMenuItem("Kết nối tệp thuộc tính theo tờ bản đồ đang mở…"); kn.Click += (s, e) => BeginInvoke(new Action(() => { try { M.KetNoiGtp(false); } catch (Exception ex) { Program.Log(ex); M.Msg("Lỗi: " + ex.Message); } }));
            var ng = new ToolStripMenuItem("Ngắt kết nối tệp thuộc tính"); ng.Click += (s, e) => BeginInvoke(new Action(() => { try { M.CloseGtp(true); } catch (Exception ex) { Program.Log(ex); M.Msg("Lỗi: " + ex.Message); } }));
            bTo.DropDownItems.Add(kn); bTo.DropDownItems.Add(ng);
        }
        // xuất các dòng đang hiện của bảng (đúng thứ tự cột, đúng lọc) ra CSV – dấu phân cách theo cài đặt vùng của Windows để Excel mở đúng cột
        void XuatBang(string file)
        {
            var cu = System.Globalization.CultureInfo.CurrentCulture; string sep = cu.TextInfo.ListSeparator; if (string.IsNullOrEmpty(sep) || sep.Length != 1) sep = ","; if (sep == cu.NumberFormat.NumberDecimalSeparator) sep = ";";
            var cot = g.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible && C.Any(x => x[0] == c.Name)).OrderBy(c => c.DisplayIndex).ToList();
            Func<object, string> q = v => v is double ? ((double)v).ToString("0.###", cu) : "\"" + Convert.ToString(v, cu).Replace("\"", "\"\"") + "\"";
            var sb = new System.Text.StringBuilder(); sb.AppendLine(string.Join(sep, cot.Select(c => q(c.HeaderText)).ToArray()));
            foreach (DataRowView r in T.DefaultView) sb.AppendLine(string.Join(sep, cot.Select(c => r[c.Name] == DBNull.Value ? "" : q(r[c.Name])).ToArray()));
            System.IO.File.WriteAllText(file, sb.ToString(), new System.Text.UTF8Encoding(true));
        }
        // chuyển số tờ: bảng và liên kết thửa trên bản đồ – thửa trong tệp theo tờ vừa chọn; nhớ cho tệp bản đồ này
        void DoiTo(string to)
        {
            if ((to ?? "") == (Cfg.I.To ?? "").Trim()) return;
            Dc.SetTo(to);
            // dựng lại thửa để liên kết sạch theo tờ mới (thông tin của tờ cũ không còn dính vào thửa)
            if (Dc.P.Any(p => p.FromGtp) && Dc.G != null) Dc.FromGtp();
            else if (Dc.P.Any(p => !p.Virtual) && Ms.Connected) { try { Dc.Build(false); } catch (Exception ex) { Program.Log(ex); Dc.Link(); Dc.Notify(); } }
            else { Dc.Link(); Dc.Notify(); }
            int n = Dc.G != null ? Dc.SheetRows().Count() : 0, lk = Dc.P.Count(p => p.Gid != null && !p.FromGtp), tr = Dc.P.Count(p => !p.Virtual && !p.FromGtp);
            M.Msg("Bảng thuộc tính chuyển sang tờ " + Dc.To + " (" + Dc.ToSrc + ")" + (Dc.G != null ? " · " + n + " thửa trong " + Dc.G.Name + ", liên kết " + lk + "/" + tr + " thửa trên bản đồ" : "") +
                (Dc.G != null && tr > 0 && lk == 0 ? " ⚠ không thửa nào trên bản đồ khớp với tờ này – kiểm tra lại số tờ" : ""));
        }
        // dựng lại cột của bảng theo số người sử dụng nhiều nhất (giữ nội dung ô lọc đang gõ)
        void DungCot(int n)
        {
            n = Math.Max(1, n); if (n == soNguoi) return; soNguoi = n;
            C = new List<string[]>(C_DAU);
            for (int i = 1; i <= n; i++) foreach (var t in ChuSD.TRUONG) C.Add(new[] { KeyNguoi(i, t.K), Gon(t) + (i == 1 ? "" : " " + i), (i == 1 ? "Người sử dụng" : "Người " + i) + " – " + t.L });
            C.AddRange(C_CUOI);
            var sort = T.DefaultView.Sort; g.DataSource = null;
            T = new DataTable { Locale = IC };
            foreach (var c in C) T.Columns.Add(c[0], NUM.Contains(c[0]) ? typeof(double) : typeof(string)); T.Columns.Add("_i", typeof(int)); T.Columns.Add("_n", typeof(double));
            if (sort != "") T.DefaultView.Sort = sort;
            foreach (var k in loc.Keys.ToList()) if (!C.Any(c => c[0] == k)) { g.Controls.Remove(loc[k]); loc[k].Dispose(); loc.Remove(k); }
            foreach (var c in C)
            {
                if (loc.ContainsKey(c[0])) continue; var k = c[0]; var x = new OLoc { Visible = false, Vien = g.GridColor, Font = Font };
                x.Tip = "Lọc theo " + (c.Length > 2 ? c[2] : c[1]).ToLowerInvariant() + ": gõ một phần nội dung, hoặc bấm biểu tượng lọc để chọn giá trị trong danh sách";
                x.GiaTri = () => GiaTriCot(k); x.Doi += (s, e) => Filter(); g.Controls.Add(x); loc[k] = x;
            }
            canVua = true; g.DataSource = T;
        }
        // các giá trị khác nhau của cột (danh sách chọn của ô lọc)
        IEnumerable<string> GiaTriCot(string k)
        {
            if (k == "ht") return ChuSD.TenHinhThuc.Concat(T.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[k], IC))).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            var ds = T.Rows.Cast<DataRow>().Select(r => r[k]).Where(v => v != DBNull.Value && Convert.ToString(v, IC).Trim() != "").Select(v => v is double ? GiaoDien.SoDT((double)v) : Convert.ToString(v, IC).Trim()).Distinct();
            bool so = NUM.Contains(k) || k == "to" || k == "so";
            return (so ? ds.OrderBy(x => SoN(x)).ThenBy(x => x, StringComparer.Ordinal) : ds.OrderBy(x => x, StringComparer.CurrentCulture)).Take(2000).ToList();
        }
        // người sử dụng, hình thức sử dụng của mọi thửa (một lần đọc cho cả bảng)
        Dictionary<string, List<NguoiSD>> tatCa; Dictionary<string, string> tatCaHt;
        void NapNguoi()
        {
            tatCa = new Dictionary<string, List<NguoiSD>>(); tatCaHt = new Dictionary<string, string>();
            try { if (Dc.G != null && ChuSD.CoBang(Dc.G)) { tatCa = ChuSD.TatCa(Dc.G); tatCaHt = ChuSD.TatCaHT(Dc.G); } } catch (Exception ex) { Program.Log(ex); }
        }
        List<NguoiSD> NguoiCua(object gid, string so, string chu, string ns, string dc, out string ht)
        {
            ht = ""; List<NguoiSD> ds;
            if (gid != null && Dc.G != null && ChuSD.CoBang(Dc.G)) { var k = Convert.ToString(gid, IC); tatCaHt.TryGetValue(k, out ht); ht = ht ?? ""; if (tatCa.TryGetValue(k, out ds)) return ds; ds = new List<NguoiSD>(); }
            else if (Dc.G == null) ds = ChuSD.DocMay(so, out ht); else ds = new List<NguoiSD>();
            if (ds.Count == 0 && !string.IsNullOrEmpty(chu)) { var n = new NguoiSD(); n["qh"] = "Người sử dụng đất"; n["ten"] = chu; n["ns"] = ns ?? ""; n["ct"] = dc ?? ""; ds.Add(n); }
            return ds;
        }
        // thêm một dòng: giá trị theo thứ tự cột của bảng
        void ThemDong(object o, Dictionary<string, object> v, List<NguoiSD> ds, string ht, double n)
        {
            rows.Add(o); nguoi.Add(ds); htDong.Add(ht ?? ""); v["ht"] = ht ?? "";
            for (int i = 1; i <= soNguoi; i++) foreach (var t in ChuSD.TRUONG) v[KeyNguoi(i, t.K)] = i <= ds.Count ? ds[i - 1][t.K] : "";
            var a = new object[C.Count + 2]; for (int i = 0; i < C.Count; i++) { object x; a[i] = v.TryGetValue(C[i][0], out x) && x != null ? x : (NUM.Contains(C[i][0]) ? (object)DBNull.Value : ""); }
            a[C.Count] = rows.Count - 1; a[C.Count + 1] = n; T.Rows.Add(a);
        }
        // thửa chưa liên kết tệp: một loại đất nhận cả diện tích thửa, nhiều loại chỉ ghi ký hiệu
        static string MdMay(Parcel p)
        {
            var c = (p.Ld ?? "").Split('+', ',').Select(x => x.Trim()).Where(x => x != "").ToArray();
            return c.Length == 1 ? c[0] + ": " + GiaoDien.SoDT(p.Dt) + " m²" : string.Join("; ", c);
        }
        static object D(double? v) { return v.HasValue ? (object)v.Value : DBNull.Value; }
        public void Reload()
        {
            if (Dc.DocLap) { ReloadDocLap(); return; }
            NapTo(); loading = true; NapNguoi();
            var ps = Dc.P.OrderBy(p => { int v; return int.TryParse(p.So, out v) ? v : int.MaxValue; }).ToList();
            var used = new HashSet<object>(Dc.P.Where(p => p.Row != null).Select(p => (object)p.Row));
            var con = Dc.G != null ? Dc.SheetRows().Where(r => !used.Contains(r)).ToList() : new List<GRow>();
            var dsP = ps.Select(p => { string h; var w = p.Row; var d = NguoiCua(p.Gid, p.So, p.Chu, w != null ? w.NamSinh : "", w != null ? w.ChuDc : "", out h); return new KeyValuePair<List<NguoiSD>, string>(d, h); }).ToList();
            var dsR = con.Select(r => { string h; var d = NguoiCua(r.Id, r.So, r.Chu, r.NamSinh, r.ChuDc, out h); return new KeyValuePair<List<NguoiSD>, string>(d, h); }).ToList();
            DungCot(dsP.Concat(dsR).Select(x => x.Key.Count).DefaultIfEmpty(1).Max());
            canVua = true; T.BeginLoadData(); T.Rows.Clear(); rows.Clear(); nguoi.Clear(); htDong.Clear();
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i]; var w = p.Row;
                ThemDong(p, new Dictionary<string, object> { { "to", Dc.To }, { "so", p.So }, { "ld", p.Ld }, { "mdct", w != null ? w.MdText : MdMay(p) }, { "dtg", w != null && w.Dt.HasValue ? w.Dt.Value : Math.Round(p.Dt, Cfg.I.DtLe) }, { "dtpl", D(p.Dtpl) }, { "diachi", p.DiaChi }, { "ghichu", p.GhiChu },
                    { "lk", p.Gid != null ? (p.FromGtp ? "Hình trong tệp" : "Tệp thuộc tính") : (Dc.G != null ? "Chưa liên kết" : "Trên máy") } }, dsP[i].Key, dsP[i].Value, SoN(p.So));
            }
            for (int i = 0; i < con.Count; i++)
            {
                var r = con[i];
                ThemDong(r, new Dictionary<string, object> { { "to", r.To }, { "so", r.So }, { "ld", r.Kh }, { "mdct", r.MdText }, { "dtg", D(r.Dt) }, { "dtpl", D(r.Dtpl) }, { "diachi", r.DiaChi }, { "ghichu", r.GhiChu }, { "lk", "Chưa có trên bản đồ" } }, dsR[i].Key, dsR[i].Value, SoN(r.So));
            }
            T.EndLoadData(); if (T.DefaultView.Sort == "") T.DefaultView.Sort = "_n ASC";
            loading = false; Filter(); Text = "Bảng thông tin thuộc tính – tờ " + Dc.To + (Dc.G != null ? " – " + Dc.G.Name : "");
        }
        // kết nối độc lập: mọi tờ, mọi thửa của tệp (xếp theo tờ rồi số thửa); không có thửa trên bản đồ
        void ReloadDocLap()
        {
            NapToDocLap(); loading = true; NapNguoi();
            var dsR = Dc.GRows.Select(r => { string h; var d = NguoiCua(r.Id, r.So, r.Chu, r.NamSinh, r.ChuDc, out h); return new KeyValuePair<List<NguoiSD>, string>(d, h); }).ToList();
            DungCot(dsR.Select(x => x.Key.Count).DefaultIfEmpty(1).Max());
            canVua = true; T.BeginLoadData(); T.Rows.Clear(); rows.Clear(); nguoi.Clear(); htDong.Clear();
            for (int i = 0; i < Dc.GRows.Count; i++)
            {
                var r = Dc.GRows[i]; double kt = Math.Min(SoN(r.To ?? ""), 99999), ks = Math.Min(SoN(r.So ?? ""), 999999);
                ThemDong(r, new Dictionary<string, object> { { "to", r.To }, { "so", r.So }, { "ld", r.Kh }, { "mdct", r.MdText }, { "dtg", D(r.Dt) }, { "dtpl", D(r.Dtpl) }, { "diachi", r.DiaChi }, { "ghichu", r.GhiChu }, { "lk", "Tệp độc lập" } }, dsR[i].Key, dsR[i].Value, kt * 1e6 + ks);
            }
            T.EndLoadData(); if (T.DefaultView.Sort == "") T.DefaultView.Sort = "_n ASC";
            dlTo = Dc.CacTo().Count(kv => kv.Key != ""); dlDt = Dc.GRows.Sum(r => r.Dt ?? 0);
            loading = false; Filter();
            Text = "Bảng thông tin thuộc tính – " + Dc.G.Name + " – mọi tờ (" + dlTo + " tờ, " + Dc.GRows.Count + " thửa) – kết nối độc lập";
        }
        // lọc: ô tìm nhanh (mọi cột) và ô lọc từng cột (cùng thoả)
        void Filter()
        {
            Func<string, string> q = x => x.Trim().Replace("'", "''").Replace("[", "").Replace("]", "").Replace("%", "").Replace("*", "");
            var dk = new List<string>(); var gq = q(flt.Text);
            if (gq != "") dk.Add("(" + string.Join(" OR ", C.Select(c => "Convert([" + c[0] + "],'System.String') LIKE '%" + gq + "%'").ToArray()) + ")");
            foreach (var kv in loc)
            {
                var t = kv.Value.Text ?? "";
                if (t.StartsWith("="))   // chọn trong danh sách: khớp đúng giá trị ("=" trống: ô trống)
                {
                    var x = t.Substring(1).Trim(); double d;
                    if (NUM.Contains(kv.Key)) dk.Add(x == "" ? "[" + kv.Key + "] IS NULL" : GiaoDien.So(x, out d) ? "[" + kv.Key + "] = " + d.ToString("R", IC) : "1=0");
                    else dk.Add("ISNULL(Convert([" + kv.Key + "],'System.String'),'') = '" + x.Replace("'", "''") + "'");
                    continue;
                }
                var v = q(t); if (v != "") dk.Add("Convert([" + kv.Key + "],'System.String') LIKE '%" + v + "%'");
            }
            if (Dc.DocLap && locTo != "") dk.Add("[to] = '" + locTo.Replace("'", "''") + "'");
            try { T.DefaultView.RowFilter = string.Join(" AND ", dk.ToArray()); } catch (Exception ex) { Program.Log(ex); }
            UpStat();
        }
        void UpStat()
        {
            int n = T.DefaultView.Count; nav.Dat(g.CurrentRow != null ? g.CurrentRow.Index : -1, n);
            foreach (var kv in tong) { double t = 0; bool co = false; foreach (DataRowView r in T.DefaultView) { var v = r[kv.Key]; if (v != DBNull.Value) { t += Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture); co = true; } } kv.Value.Text = co ? GiaoDien.SoDT(t) : ""; }
            s2.Text = (n < T.Rows.Count ? "Đang lọc " + n + "/" + T.Rows.Count + " dòng · " : "") + (Dc.DocLap ? "Kết nối độc lập " + Dc.G.Name + ": " + dlTo + " tờ · " + Dc.GRows.Count + " thửa · tổng diện tích trong tệp " + GiaoDien.SoDT(dlDt) + " m²" :
                "Trên bản đồ: " + Dc.P.Count + " thửa (" + Dc.P.Count(p => p.Gid != null) + " đã liên kết) · Tổng diện tích các thửa: " + GiaoDien.SoDT(Dc.P.Sum(p => p.Dt)) + " m²");
        }
    }

    // ---------- Thông tin thửa ----------
    public class ParcelForm : FormBase
    {
        MainForm M; Parcel P; TableLayoutPanel t = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(6) }; Dictionary<string, TextBox> tx = new Dictionary<string, TextBox>();
        DataGridView v = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = Color.White }; Label info = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 36, Padding = new Padding(6, 2, 6, 2), ForeColor = Color.DimGray };
        public ParcelForm(MainForm m)
        {
            M = m; Text = "Thông tin thửa đất"; Font = m.Font; Width = 480; Height = 560; ShowInTaskbar = false; Icon = m.Icon; GiaoDien.KhongCat(this); StartPosition = FormStartPosition.Manual; var wa = Screen.PrimaryScreen.WorkingArea; Location = new Point(wa.Right - Width - 20, wa.Top + 260);
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var f in new[] { new[] { "so", "Số thửa" }, new[] { "ld", "Mục đích sử dụng" }, new[] { "dtpl", "Diện tích pháp lý (m²)" }, new[] { "chu", "Tên chủ sử dụng" }, new[] { "diachi", "Địa chỉ thửa đất" }, new[] { "ghichu", "Ghi chú" } })
            {
                t.Controls.Add(new Label { Text = f[1], AutoSize = true, Anchor = AnchorStyles.Left }); var b = new TextBox { Dock = DockStyle.Fill }; var k = f[0];
                b.Leave += (s, e) => { if (P == null || b.Text == Cur(k)) return; if (k == "so" && b.Text.Trim() != "" && Dc.P.Any(q => q != P && q.So == b.Text.Trim())) { M.Msg("Số thửa " + b.Text + " đã có"); b.Text = P.So; return; } Dc.SetField(P, k, b.Text); M.Msg("Đã cập nhật thửa " + P.So + " – bấm “Vẽ nhãn” để cập nhật nhãn"); }; tx[k] = b; t.Controls.Add(b);
            }
            var tb = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, ImageScalingSize = new Size(20, 20) };
            Action<string, string, Action<Parcel>> B = (s, ic, a) => tb.Items.Add(new ToolStripButton(s, Icons.Get(ic), (o, e) => { if (P != null) try { a(P); } catch (Exception ex) { M.Msg("Lỗi: " + ex.Message); } }));
            B("Vẽ nhãn", "tag", p => Dc.DrawLabels(new[] { p }, true)); B("Phóng tới", "zin", p => Dc.Zoom(p, .6)); B("Kích thước", "dim", p => Dc.Dims(new[] { p })); B("Số đỉnh", "vmove", p => Dc.VertNums(new[] { p }));
            B("Trích lục", "doc", Reports.TrichLuc); B("Phiếu", "ruler", Reports.Phieu); B("Biên bản", "pen", Reports.BienBan);
            Controls.Add(v); Controls.Add(info); Controls.Add(t); Controls.Add(tb);
            v.Columns.Add("i", "Đỉnh"); v.Columns.Add("x", "X (m)"); v.Columns.Add("y", "Y (m)"); v.Columns.Add("c", "Cạnh"); v.Columns.Add("l", "Dài (m)");
        }
        string Cur(string k) { var p = P; return k == "so" ? p.So : k == "ld" ? p.Ld : k == "dtpl" ? (p.Dtpl.HasValue ? p.Dtpl.Value.ToString() : "") : k == "chu" ? p.Chu : k == "diachi" ? p.DiaChi : p.GhiChu; }
        public void Show_(Parcel p)
        {
            P = p; if (p == null) { Text = "Thông tin thửa đất – chưa chọn thửa"; return; }
            Text = "Thửa " + (p.So == "" ? "(chưa có số)" : p.So) + " – " + Dc.FD(p.Dt) + " m²";
            tx["so"].Text = p.So; tx["ld"].Text = p.Ld; tx["dtpl"].Text = p.Dtpl.HasValue ? p.Dtpl.Value.ToString() : ""; tx["chu"].Text = p.Chu; tx["diachi"].Text = p.DiaChi; tx["ghichu"].Text = p.GhiChu;
            info.Text = Dc.LdName(p.Ld) + (p.DtNhan.HasValue ? " · diện tích trên nhãn " + Dc.FD(p.DtNhan.Value) + " m²" : "") + (p.Gid != null ? " · đã liên kết tệp thuộc tính" : "") + " · liền kề: " + string.Join(", ", Dc.Neighbors(p).Select(q => q.So).ToArray());
            v.Rows.Clear(); var vt = Dc.Verts(p); int n = vt.Length / 2;
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; v.Rows.Add(i + 1, Dc.F(vt[2 * i + 1], 3), Dc.F(vt[2 * i], 3), (i + 1) + "-" + (j + 1), Dc.F(Geo.Dist(vt[2 * i], vt[2 * i + 1], vt[2 * j], vt[2 * j + 1]), 2)); }
        }
    }

    // ---------- danh sách lỗi ----------
    public class ErrForm : FormBase
    {
        MainForm M; ListView L = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
        public ErrForm(MainForm m)
        {
            M = m; Text = "Lỗi ranh và nhãn"; Font = m.Font; Width = 560; Height = 400; ShowInTaskbar = false; Icon = m.Icon; GiaoDien.KhongCat(this);
            L.Columns.Add("#", 40); L.Columns.Add("Lỗi", 300); L.Columns.Add("X", 90); L.Columns.Add("Y", 90); Controls.Add(L);
            L.SelectedIndexChanged += (s, e) =>
            {
                if (L.SelectedItems.Count == 0) return; var er = (Err)L.SelectedItems[0].Tag; double d = Math.Max(Cfg.I.H * 6, 5);
                if (er.P != null) { M.Cur = er.P; Dc.Zoom(er.P, .6); } else Ms.ZoomTo(er.X - d, er.Y - d, er.X + d, er.Y + d);
            };
        }
        public void Reload()
        {
            L.Items.Clear(); int i = 0; L.BeginUpdate();
            foreach (var e in Dc.Errs.Take(5000)) L.Items.Add(new ListViewItem(new[] { (++i).ToString(), e.Text, Dc.F(e.Y, 2), Dc.F(e.X, 2) }) { Tag = e });
            L.EndUpdate(); GiaoDien.VuaCot(L); Text = "Danh sách kiểm tra (" + Dc.Errs.Count + ") – nhấp dòng để phóng tới";
        }
    }

    // ---------- thống kê ----------
    public class StatForm : FormBase
    {
        public StatForm()
        {
            Text = "Thống kê diện tích theo loại đất – tờ " + Dc.To; Width = 560; Height = 420; ShowInTaskbar = false; Font = new Font("Segoe UI", 9f); GiaoDien.KhongCat(this);
            var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = Color.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            g.Columns.Add("k", "Loại đất"); g.Columns.Add("n", "Tên loại đất"); g.Columns.Add("c", "Số thửa"); g.Columns.Add("a", "Diện tích (m²)"); g.Columns.Add("p", "%");
            double tot = Math.Max(Dc.P.Sum(p => p.Dt), 1e-9); foreach (var kv in Dc.StatLD()) g.Rows.Add(kv.Key, Dc.LoaiDat.ContainsKey(kv.Key) ? Dc.LoaiDat[kv.Key] : "", kv.Value[0], Dc.FD(kv.Value[1]), Dc.F(kv.Value[1] / tot * 100, 2));
            g.Rows.Add("Tổng", "", Dc.P.Count, Dc.FD(tot), "100");
            var b = GiaoDien.Nut("In bảng thống kê", 130, true); b.Dock = DockStyle.Bottom; b.Click += (s, e) => Reports.ThongKe(); Controls.Add(g); Controls.Add(b);
        }
    }
}
