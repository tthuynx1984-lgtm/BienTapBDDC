// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – kết nối tệp thuộc tính (.gtp) khi mở tờ bản đồ, thiết lập đơn vị hành chính. © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace M8Pro
{
    // nhớ tệp thuộc tính đã chọn cho từng tệp bản đồ ("-" = đã chọn không kết nối)
    public static class GtpMem
    {
        static string F { get { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "M8Pro"); Directory.CreateDirectory(d); return Path.Combine(d, "ketnoi_gtp.txt"); } }
        static Dictionary<string, string> All()
        {
            var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try { if (File.Exists(F)) foreach (var l in File.ReadAllLines(F, Encoding.UTF8)) { var a = l.Split('\t'); if (a.Length == 2 && a[0] != "") o[a[0]] = a[1]; } } catch { }
            return o;
        }
        public static string Get(string dgn) { string v; return !string.IsNullOrEmpty(dgn) && All().TryGetValue(dgn, out v) ? v : null; }
        public static void Set(string dgn, string gtp)
        {
            if (string.IsNullOrEmpty(dgn)) return; var d = All(); d[dgn] = string.IsNullOrEmpty(gtp) ? "-" : gtp;
            try { File.WriteAllLines(F, d.Select(kv => kv.Key + "\t" + kv.Value).ToArray(), Encoding.UTF8); } catch { }
        }
    }

    // tên đơn vị hành chính: thêm cấp (xã, huyện, tỉnh…) nếu thiếu, để in đúng trên khung bản đồ và hồ sơ
    public static class Dvhc
    {
        // 34 đơn vị hành chính cấp tỉnh từ 12/6/2025 (gõ tên khác được, danh sách chỉ để chọn nhanh)
        public static readonly string[] Tinh = {
            "thành phố Hà Nội", "thành phố Huế", "thành phố Hải Phòng", "thành phố Đà Nẵng", "thành phố Hồ Chí Minh", "thành phố Cần Thơ",
            "tỉnh An Giang", "tỉnh Bắc Ninh", "tỉnh Cà Mau", "tỉnh Cao Bằng", "tỉnh Đắk Lắk", "tỉnh Điện Biên", "tỉnh Đồng Nai", "tỉnh Đồng Tháp",
            "tỉnh Gia Lai", "tỉnh Hà Tĩnh", "tỉnh Hưng Yên", "tỉnh Khánh Hòa", "tỉnh Lai Châu", "tỉnh Lâm Đồng", "tỉnh Lạng Sơn", "tỉnh Lào Cai",
            "tỉnh Nghệ An", "tỉnh Ninh Bình", "tỉnh Phú Thọ", "tỉnh Quảng Ngãi", "tỉnh Quảng Ninh", "tỉnh Quảng Trị", "tỉnh Sơn La", "tỉnh Tây Ninh",
            "tỉnh Thái Nguyên", "tỉnh Thanh Hóa", "tỉnh Tuyên Quang", "tỉnh Vĩnh Long" };
        static readonly string[] PXa = { "xã", "phường", "thị trấn", "đặc khu" }, PHuyen = { "huyện", "quận", "thị xã", "thành phố" }, PTinh = { "tỉnh", "thành phố" };
        const string TP = "thành phố ";
        public static string Pre(string s, string cap)
        {
            s = (s ?? "").Trim(); if (s == "") return "";
            var ps = cap == "xã" ? PXa : cap == "huyện" ? PHuyen : PTinh;
            foreach (var p in ps) if (s.Length > p.Length + 1 && s.StartsWith(p + " ", StringComparison.OrdinalIgnoreCase)) return p + s.Substring(p.Length);
            if (cap == "tỉnh" && Tinh.Any(t => t.StartsWith(TP, StringComparison.Ordinal) && string.Equals(t.Substring(TP.Length), s, StringComparison.OrdinalIgnoreCase))) return TP + s;
            return cap + " " + s;
        }
        public static string Join(params string[] a) { return string.Join(", ", a.Where(x => !string.IsNullOrEmpty(x)).ToArray()); }
    }

    // thông tin chung trong tệp .gtp (bảng ThongTinMoRong): tên xã, huyện, tỉnh, mã xã, số hiệu tờ
    public static class GtpInfo
    {
        public static Dictionary<string, string> Read(Gtp g)
        {
            var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (g == null || !g.Has("ThongTinMoRong", "truong", "giaTri")) return o;
            try
            {
                foreach (var r in g.Db.Query("select truong as k, giaTri as v from ThongTinMoRong"))
                {
                    var k = Convert.ToString(r["k"], CultureInfo.InvariantCulture); var v = Convert.ToString(r["v"], CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v) && !o.ContainsKey(k)) o[k] = v.Trim();
                }
            }
            catch { }
            return o;
        }
        public static string Get(Dictionary<string, string> d, string k) { string v; return d != null && d.TryGetValue(k, out v) ? v : ""; }
        public static string Dv(Dictionary<string, string> d) { return Dvhc.Join(Dvhc.Pre(Get(d, "TenXa"), "xã"), Dvhc.Pre(Get(d, "TenHuyen"), "huyện"), Dvhc.Pre(Get(d, "TenTinh"), "tỉnh")); }
        static int Num(string s) { int v; return int.TryParse(s, out v) ? v : int.MaxValue; }
        static long Count(Gtp g, string sql) { try { var r = g.Query(sql); return r.Count > 0 ? Convert.ToInt64(r[0].Values.First(), CultureInfo.InvariantCulture) : 0; } catch { return 0; } }
        // tóm tắt nhanh một tệp .gtp đã mở (chỉ đếm, không đọc hình thửa): số thửa, số thửa có chủ, các tờ, đơn vị hành chính, ngày sửa
        public static string Summary(Gtp g, string path)
        {
            long n = g.Has("ThuaDat", "thuaDatId") ? Count(g, "select count(*) from ThuaDat") : 0;
            long nc = g.Has("DangKyThua", "thuaDatId") ? Count(g, "select count(distinct thuaDatId) from DangKyThua") : 0;
            var tos = new List<string>(); if (g.Has("ThuaDat", "soHieuToBanDo")) try { tos = g.Query("select distinct soHieuToBanDo as t from ThuaDat").Select(r => Gtp.Norm(Convert.ToString(r["t"], CultureInfo.InvariantCulture))).Where(z => z != "").OrderBy(z => Num(z)).ToList(); } catch { }
            var dv = Dv(Read(g));
            return n + " thửa, " + nc + " thửa có chủ" + (tos.Count > 0 ? " · tờ " + string.Join(", ", tos.Take(10).ToArray()) + (tos.Count > 10 ? " …" : "") : "") +
                (dv != "" ? "\r\n" + dv : "") + "\r\nSửa lần cuối " + File.GetLastWriteTime(path).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }
    }

    // ================= BẢNG KẾT NỐI TỆP THUỘC TÍNH: hiện khi mở tờ bản đồ trong MicroStation =================
    public class GtpConnForm : Form
    {
        public string Result; public bool AskNext = true; public Gtp ResultGtp;   // tệp đã mở để xem trước: dùng luôn khi kết nối (không mở lại)
        readonly Dictionary<string, Gtp> opened = new Dictionary<string, Gtp>(StringComparer.OrdinalIgnoreCase);
        readonly string dgn, dir, same; readonly List<string> others; readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        RadioButton rSame = new RadioButton { AutoSize = true }, rDir = new RadioButton { Text = "Tệp khác trong cùng thư mục:", AutoSize = true },
            rPick = new RadioButton { Text = "Tệp ở thư mục khác:", AutoSize = true }, rNone = new RadioButton { Text = "Không kết nối – chỉ làm việc với bản đồ", AutoSize = true };
        ComboBox cDir = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        TextBox tPick = new TextBox { Width = 300, ReadOnly = true }; Button bPick = GiaoDien.Nut("Chọn…", 80, false);
        Label info = new Label { Dock = DockStyle.Top, Height = 64, Padding = new Padding(8, 6, 8, 4), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(246, 248, 251) };
        CheckBox cAsk = new OTich { Dock = DockStyle.Top, Height = 28, Text = "Hỏi lại mỗi khi mở tờ bản đồ (bỏ chọn: lần sau tự kết nối tệp đã chọn cho tờ đó)", Padding = new Padding(8, 0, 0, 0) };
        public GtpConnForm(string dgn, string cur, string mem, bool auto)
        {
            this.dgn = dgn; dir = Path.GetDirectoryName(dgn); same = Path.ChangeExtension(dgn, ".gtp"); bool hasSame = File.Exists(same);
            others = new List<string>(); try { others = Directory.GetFiles(dir, "*.gtp").Where(x => !string.Equals(x, same, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(); } catch { }
            Text = "Kết nối tệp thuộc tính (.gtp)"; Font = new Font("Segoe UI", 9f); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen; TopMost = true; Width = 600; Height = 388; AskNext = Cfg.I.HoiGtp; cAsk.Checked = Cfg.I.HoiGtp;
            var hd = new Label { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8, 8, 8, 0), Text = (auto ? "Vừa mở tờ bản đồ: " : "Tờ bản đồ đang mở: ") + Path.GetFileName(dgn) + "\r\nThư mục: " + dir };   // đường dẫn đầy đủ, dài thì xuống dòng
            hd.Font = new Font(Font, FontStyle.Bold);
            rSame.Text = "Tệp cùng tên, cùng thư mục: " + Path.GetFileName(same) + (hasSame ? "" : "  (không có)"); rSame.Enabled = hasSame;
            foreach (var o in others) cDir.Items.Add(Path.GetFileName(o)); rDir.Enabled = cDir.Enabled = others.Count > 0; if (others.Count > 0) cDir.SelectedIndex = 0;
            var t = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, RowCount = 4, Padding = new Padding(6, 4, 6, 4), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            rSame.Dock = DockStyle.Fill; GiaoDien.Gian(rSame, 22); rNone.Dock = DockStyle.Fill; GiaoDien.Gian(rNone, 22);   // tên tệp dài: xuống dòng
            t.Controls.Add(rSame, 0, 0); t.SetColumnSpan(rSame, 3);
            t.Controls.Add(rDir, 0, 1); t.Controls.Add(cDir, 1, 1); cDir.Dock = DockStyle.Fill;
            t.Controls.Add(rPick, 0, 2); t.Controls.Add(tPick, 1, 2); t.Controls.Add(bPick, 2, 2); tPick.Dock = DockStyle.Fill;
            t.Controls.Add(rNone, 0, 3); t.SetColumnSpan(rNone, 3);
            var gb = new GroupBox { Dock = DockStyle.Top, Height = 150, Text = "Tệp thuộc tính dùng cho tờ bản đồ này", Padding = new Padding(4) }; gb.Controls.Add(t);
            t.SizeChanged += (s, e) => gb.Height = Math.Max(150, t.Height + 26);   // khung cao theo các dòng (tên tệp dài xuống dòng)
            var bp = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var ok = GiaoDien.Nut("Kết nối", 100, true); var no = GiaoDien.Nut("Bỏ qua", 90, false); no.DialogResult = DialogResult.Cancel;
            bp.Controls.Add(no); bp.Controls.Add(ok); AcceptButton = ok; CancelButton = no;
            Controls.Add(cAsk); Controls.Add(info); Controls.Add(gb); Controls.Add(hd); Controls.Add(bp);
            // chọn sẵn: tệp đang kết nối → tệp đã chọn lần trước cho tờ này → tệp cùng tên → tệp khác cùng thư mục
            string pre = cur ?? (mem != null && mem != "-" && File.Exists(mem) ? mem : null) ?? (hasSame ? same : null);
            if (pre == null && mem == "-") rNone.Checked = true; else if (pre != null) Pick(pre); else if (others.Count > 0) rDir.Checked = true; else rPick.Checked = true;
            EventHandler upd = (s, e) => { if (((RadioButton)s).Checked) Info(); };
            rSame.CheckedChanged += upd; rDir.CheckedChanged += upd; rPick.CheckedChanged += upd; rNone.CheckedChanged += upd;
            cDir.SelectedIndexChanged += (s, e) => { if (!rDir.Checked) rDir.Checked = true; else Info(); };
            bPick.Click += (s, e) => Browse();
            ok.Click += (s, e) =>
            {
                var p = Chosen(); if (p == null && !rNone.Checked) { MessageBox.Show(this, "Hãy chọn tệp thuộc tính, hoặc chọn “Không kết nối”.", Text); return; }
                Result = p; AskNext = cAsk.Checked; Gtp og; if (p != null && opened.TryGetValue(p, out og)) { ResultGtp = og; opened.Remove(p); } DialogResult = DialogResult.OK;
            };
            FormClosed += (s, e) => { foreach (var x in opened.Values) try { x.Dispose(); } catch { } opened.Clear(); };
            Shown += (s, e) => { Info(); if (rPick.Checked && tPick.Text == "") bPick.Focus(); };
            // hộp thoại co giãn được, rộng đủ cho tên tệp dài (tối đa gần bề rộng màn hình)
            MinimumSize = new Size(600, 388); GiaoDien.KhongCat(this);
            try { int can = Math.Max(TextRenderer.MeasureText(rSame.Text, Font).Width, TextRenderer.MeasureText(hd.Text.Split('\n').Last(), hd.Font).Width) + 90; Width = Math.Max(Width, Math.Min(Screen.PrimaryScreen.WorkingArea.Width - 80, can)); } catch { }
        }
        void Pick(string p)
        {
            if (string.Equals(p, same, StringComparison.OrdinalIgnoreCase)) { rSame.Checked = true; return; }
            int i = others.FindIndex(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) { cDir.SelectedIndex = i; rDir.Checked = true; return; }
            tPick.Text = p; rPick.Checked = true;
        }
        string Chosen()
        {
            if (rSame.Checked) return same; if (rDir.Checked && cDir.SelectedIndex >= 0) return others[cDir.SelectedIndex];
            if (rPick.Checked && tPick.Text != "") return tPick.Text; return null;
        }
        void Browse()
        {
            using (var d = new OpenFileDialog { Filter = "Tệp thuộc tính (*.gtp)|*.gtp|Tất cả|*.*", Title = "Chọn tệp thuộc tính cho " + Path.GetFileName(dgn) })
            {
                try { d.InitialDirectory = tPick.Text != "" ? Path.GetDirectoryName(tPick.Text) : dir; } catch { }
                if (d.ShowDialog(this) != DialogResult.OK) return; Pick(d.FileName); Info();
            }
        }
        void Info()
        {
            if (rNone.Checked) { info.Text = "Không kết nối tệp thuộc tính: vẫn sửa lỗi, tạo thửa, vẽ nhãn theo bản đồ; chủ sử dụng và hồ sơ cần tệp thuộc tính."; return; }
            var p = Chosen(); if (p == null) { info.Text = rPick.Checked ? "Bấm “Chọn…” để tìm tệp .gtp ở thư mục khác." : "Chưa chọn tệp."; return; }
            string s;
            if (!cache.TryGetValue(p, out s))
            {
                Cursor = Cursors.WaitCursor; info.Text = "Đang đọc " + Path.GetFileName(p) + " …"; info.Refresh();
                try { Gtp g; if (!opened.TryGetValue(p, out g)) { g = Gtp.Open(p); opened[p] = g; } s = GtpInfo.Summary(g, p); }
                catch (Exception ex) { s = "Không đọc được tệp: " + ex.Message; }
                finally { Cursor = Cursors.Default; }
                cache[p] = s;
            }
            info.Text = Path.GetFileName(p) + ": " + s;
        }
    }

    // ================= THIẾT LẬP ĐƠN VỊ HÀNH CHÍNH =================
    public class DvhcForm : Form
    {
        ComboBox tinh = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill, MaxDropDownItems = 18 };
        TextBox huyen = new TextBox { Dock = DockStyle.Fill }, xa = new TextBox { Dock = DockStyle.Fill }, maxa = new TextBox { Width = 120 }, to = new TextBox { Width = 120 }, ktt = new TextBox { Width = 120 }, coquan = new TextBox { Dock = DockStyle.Fill };
        ComboBox tyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 120 };
        public DvhcForm(Dictionary<string, string> gtpInfo, string gtpName)
        {
            var c = Cfg.I; Text = "Thiết lập đơn vị hành chính"; Font = new Font("Segoe UI", 9f); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen; TopMost = true; Width = 560; Height = 396; GiaoDien.KhongCat(this);
            tinh.Items.AddRange(Dvhc.Tinh); tyle.Items.AddRange(new object[] { "200", "500", "1000", "2000", "5000", "10000" });
            tinh.Text = Dvhc.Pre(c.Tinh, "tỉnh"); huyen.Text = Dvhc.Pre(c.Huyen, "huyện"); xa.Text = Dvhc.Pre(c.Xa, "xã"); maxa.Text = c.MaXa ?? ""; to.Text = Dc.To; tyle.Text = c.TyLe.ToString(CultureInfo.InvariantCulture);
            ktt.Text = c.Ktt.ToString("0.####", CultureInfo.InvariantCulture); coquan.Text = c.CoQuan ?? "";
            var t = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(10, 8, 10, 4) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Action<string, Control> row = (l, ctl) => { t.Controls.Add(new Label { Text = l, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }); t.Controls.Add(ctl); };
            row("Tỉnh / thành phố", tinh); row("Huyện / quận (nếu còn dùng)", huyen); row("Xã / phường / đặc khu", xa); row("Mã đơn vị hành chính cấp xã", maxa);
            row("Tờ bản đồ số (tờ đang mở)", to); row("Tỷ lệ bản đồ 1:", tyle); row("Kinh tuyến trục (độ; 105 = 105°00′, 104,75 = 104°45′)", ktt); row("Cơ quan in trên hồ sơ", coquan);
            var note = new Label { Dock = DockStyle.Top, Height = 58, Padding = new Padding(12, 4, 12, 0), ForeColor = Color.DimGray,
                Text = "Từ 01/7/2025 chính quyền địa phương có 2 cấp: để trống Huyện nếu bản đồ, hồ sơ không ghi cấp huyện. Tên đơn vị dùng để in khung bản đồ, sổ sách và các mẫu trong cửa sổ hồ sơ; gõ thiếu cấp (xã, tỉnh…) công cụ tự thêm." };
            var bp = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var ok = GiaoDien.Nut("Lưu", 90, true); var no = GiaoDien.Nut("Hủy", 80, false); no.DialogResult = DialogResult.Cancel;
            var lay = GiaoDien.Nut("Lấy theo tệp thuộc tính", 170, false); lay.Enabled = gtpInfo != null && gtpInfo.Count > 0;
            if (gtpName != null) lay.Text += " (" + gtpName + ")";
            bp.Controls.Add(no); bp.Controls.Add(ok); bp.Controls.Add(lay); AcceptButton = ok; CancelButton = no;
            Controls.Add(note); Controls.Add(t); Controls.Add(bp);
            lay.Click += (s, e) =>
            {
                var tx = GtpInfo.Get(gtpInfo, "TenXa"); var th = GtpInfo.Get(gtpInfo, "TenHuyen"); var tt = GtpInfo.Get(gtpInfo, "TenTinh"); var mx = GtpInfo.Get(gtpInfo, "MaXa"); var sh = GtpInfo.Get(gtpInfo, "soHieuBanDo");
                if (tt != "") tinh.Text = Dvhc.Pre(tt, "tỉnh"); if (th != "") huyen.Text = Dvhc.Pre(th, "huyện"); if (tx != "") xa.Text = Dvhc.Pre(tx, "xã"); if (mx != "") maxa.Text = mx; if (sh != "") to.Text = sh;
            };
            ok.Click += (s, e) =>
            {
                int tl; if (!int.TryParse(tyle.Text.Replace(".", "").Trim(), out tl) || tl <= 0) { MessageBox.Show(this, "Tỷ lệ không hợp lệ.", Text); return; }
                double k; if (!double.TryParse(ktt.Text.Replace(',', '.').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out k) || k < 100 || k > 112) { MessageBox.Show(this, "Kinh tuyến trục không hợp lệ (VN-2000, múi chiếu 3°: khoảng 102–110 độ; ghi 105 cho 105°00′, 104,75 cho 104°45′).", Text); return; }
                c.Tinh = Dvhc.Pre(tinh.Text, "tỉnh"); c.Huyen = Dvhc.Pre(huyen.Text, "huyện"); c.Xa = Dvhc.Pre(xa.Text, "xã"); c.MaXa = maxa.Text.Trim(); c.TyLe = tl; c.Ktt = k; c.CoQuan = coquan.Text.Trim();
                c.Save(); if ((to.Text ?? "").Trim() != Dc.To) Dc.SetTo(to.Text); DialogResult = DialogResult.OK;
            };
        }
    }

    // ================= MÀN HÌNH CHÍNH: kết nối tệp thuộc tính, đơn vị hành chính =================
    public partial class MainForm
    {
        bool gtpAsking;
        // MicroStation vừa mở tờ bản đồ (hoặc lần đầu kết nối): hỏi kết nối tệp thuộc tính, hoặc tự kết nối theo lựa chọn đã nhớ
        void OnMapOpened(string dgn)
        {
            // tệp thuộc tính đang kết nối độc lập (mọi tờ): giữ nguyên, không hỏi kết nối tệp của tờ vừa mở
            if (Dc.DocLap) { Msg("Đang kết nối độc lập " + Dc.G.Name + " (mọi tờ) – tờ bản đồ " + Path.GetFileName(dgn) + " không gắn với tệp này. Kết nối tệp theo tờ: Hệ thống → Kết nối tệp thuộc tính (.gtp)"); return; }
            var mem = GtpMem.Get(dgn); var same = Dc.GtpFor(dgn);
            // tệp thuộc tính của tờ trước không thuộc tờ này thì đóng (lưu nếu có sửa) để không nối nhầm thửa
            if (Dc.G != null && !string.Equals(Dc.G.Path_, mem, StringComparison.OrdinalIgnoreCase) && !string.Equals(Dc.G.Path_, same, StringComparison.OrdinalIgnoreCase)) CloseGtp(false);
            if (Cfg.I.HoiGtp) { BeginInvoke((Action)(() => KetNoiGtp(true))); return; }
            string f = mem == "-" ? null : mem != null && File.Exists(mem) ? mem : Cfg.I.TuMoGtp ? same : null;
            if (f != null) try { ApplyGtp(f, false); } catch (Exception ex) { Program.Log(ex); Msg("Không mở được " + Path.GetFileName(f) + ": " + ex.Message); }
        }
        public void KetNoiGtp(bool auto)
        {
            if (gtpAsking) return; var dgn = Ms.FullName; if (dgn == "") { Msg("Chưa có tờ bản đồ đang mở trong MicroStation"); return; }
            gtpAsking = true; tm.Stop();
            try
            {
                using (var f = new GtpConnForm(dgn, Dc.G != null ? Dc.G.Path_ : null, GtpMem.Get(dgn), auto))
                {
                    if (f.ShowDialog(this) != DialogResult.OK) { if (auto) Msg("Chưa kết nối tệp thuộc tính – khi cần: Hệ thống → Kết nối tệp thuộc tính"); return; }
                    Cfg.I.HoiGtp = f.AskNext; Cfg.I.Save(); GtpMem.Set(dgn, f.Result);
                    if (f.Result == null) { CloseGtp(true); Msg("Không kết nối tệp thuộc tính – làm việc với bản đồ " + Ms.FileName); return; }
                    ApplyGtp(f.Result, !auto, f.ResultGtp);
                }
            }
            catch (Exception ex) { Program.Log(ex); Msg("Không kết nối được tệp thuộc tính: " + ex.Message); MessageBox.Show(this, ex.Message, Bt.Ten, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { gtpAsking = false; tm.Start(); }
        }
        void ApplyGtp(string path, bool showAttr) { ApplyGtp(path, showAttr, null); }
        void ApplyGtp(string path, bool showAttr, Gtp pre)
        {
            // đang kết nối độc lập (kể cả cùng tệp): mở lại theo tờ bản đồ để liên kết thửa
            if (Dc.G == null || Dc.DocLap || !string.Equals(Dc.G.Path_, path, StringComparison.OrdinalIgnoreCase)) Dc.OpenGtp(path, pre);
            else if (pre != null) try { pre.Dispose(); } catch { }
            var tos = Dc.GRows.Select(x => x.To).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
            if (tos.Length == 1 && tos[0] != Dc.To) { Dc.SetTo(tos[0]); Dc.Link(); }
            else if (tos.Length > 1 && !tos.Contains(Dc.To))
            {
                var r = Ask.Show(this, "Tệp có nhiều tờ bản đồ", new F("to", "Tờ ứng với bản đồ " + Dc.MapName, tos[0]) { Opt = tos.OrderBy(x => { int v; return int.TryParse(x, out v) ? v : 99999; }).ToArray() });
                if (r != null) { Dc.SetTo(r["to"]); Dc.Link(); }
            }
            // chưa thiết lập đơn vị hành chính: lấy theo tệp thuộc tính
            var inf = GtpInfo.Read(Dc.G); var c = Cfg.I;
            if (string.IsNullOrEmpty(c.Xa) && GtpInfo.Get(inf, "TenXa") != "")
            {
                c.Xa = Dvhc.Pre(GtpInfo.Get(inf, "TenXa"), "xã"); var th = GtpInfo.Get(inf, "TenHuyen"); var tt = GtpInfo.Get(inf, "TenTinh");
                if (th != "") c.Huyen = Dvhc.Pre(th, "huyện"); if (tt != "") c.Tinh = Dvhc.Pre(tt, "tỉnh"); if (GtpInfo.Get(inf, "MaXa") != "") c.MaXa = GtpInfo.Get(inf, "MaXa"); c.Save(); RefreshTitle();
            }
            Msg("Đã kết nối " + Dc.G.Name + ": " + Dc.GRows.Count + " thửa, " + Dc.GRows.Count(x => x.Chu != "") + " có chủ (tờ " + Dc.To + ")" + (Cfg.I.TuLuu ? " · sửa đến đâu lưu thẳng vào tệp đến đó" : "") +
                (Dc.P.Count > 0 ? " · liên kết " + Dc.P.Count(p => p.Gid != null) + "/" + Dc.P.Count + " thửa" : " · bấm Bản đồ → Tạo thửa để liên kết với bản đồ"));
            // tệp bị bản trước ghi thiếu: đã sửa để gCadas mở được bảng thuộc tính
            if (Dc.SuaGtp != "")
            {
                var tam = Dc.G.MaTam.Distinct().ToArray(); Dc.G.MaTam.Clear();
                var t = "Tệp " + Dc.G.Name + " có chỗ bị bản trước của công cụ ghi thiếu (gCadas báo “Object cannot be cast from DBNull…” khi mở bảng thuộc tính). Đã sửa: " + Dc.SuaGtp + "." +
                    (Dc.G.Dirty ? "\n\nBấm Lưu tệp thuộc tính để ghi lại." : "\n\nĐã lưu lại tệp, bản trước khi sửa giữ ở " + Dc.G.Name + ".bak. Mở lại bằng gCadas là dùng bình thường.") +
                    (tam.Length > 0 ? "\n\nKý hiệu loại đất chưa có mã số trong tệp: " + string.Join(", ", tam) + " – đã tạm ghi mã loại đất dùng nhiều nhất của tệp, nên kiểm tra lại loại đất chi tiết của các thửa này trong gCadas." : "");
                Msg("Đã sửa tệp thuộc tính cho gCadas: " + Dc.SuaGtp); Dc.SuaGtp = "";
                MessageBox.Show(this, t, Bt.Ten, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Dc.Notify(); if (showAttr) ShowAttr();
        }
        // kết nối độc lập một tệp thuộc tính (thường là tệp toàn xã đã gộp): không cần tờ bản đồ ứng với tệp; bảng thuộc tính hiện đủ mọi tờ, mọi thửa
        public void KetNoiGtpDocLap(string path)
        {
            if (path == null)
            {
                using (var d = new OpenFileDialog { Filter = "Tệp thuộc tính (*.gtp)|*.gtp", Title = "Kết nối tệp thuộc tính độc lập – mọi tờ, mọi thửa, không theo bản đồ" })
                {
                    var cu = !string.IsNullOrEmpty(Cfg.I.GtpTx) && File.Exists(Cfg.I.GtpTx) ? Cfg.I.GtpTx : Dc.G != null ? Dc.G.Path_ : "";
                    try { if (cu != "" && Directory.Exists(Path.GetDirectoryName(cu))) { d.InitialDirectory = Path.GetDirectoryName(cu); d.FileName = Path.GetFileName(cu); } } catch { }
                    if (d.ShowDialog(this) != DialogResult.OK) return; path = d.FileName;
                }
            }
            if (Dc.G != null) CloseGtp(false);
            Cursor = Cursors.WaitCursor;
            try { Dc.OpenGtpDocLap(path); }
            catch (Exception ex) { Program.Log(ex); Msg("Không kết nối được " + Path.GetFileName(path) + ": " + ex.Message); MessageBox.Show(this, ex.Message, Bt.Ten, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            finally { Cursor = Cursors.Default; }
            var tos = Dc.CacTo(); if (tos.Count(kv => kv.Key != "") > 1) { Cfg.I.GtpTx = Path.GetFullPath(path); Cfg.I.Save(); }   // Hồ sơ toàn xã dùng luôn tệp này
            Msg("Đã kết nối độc lập " + Dc.G.Name + ": " + tos.Count(kv => kv.Key != "") + " tờ, " + Dc.GRows.Count + " thửa – bảng thuộc tính hiện đủ mọi tờ, mọi thửa (không liên kết thửa trên bản đồ)" + (Cfg.I.TuLuu ? " · sửa đến đâu lưu thẳng vào tệp đến đó" : ""));
            if (Dc.SuaGtp != "") { var t = "Tệp " + Dc.G.Name + " có chỗ bị bản trước của công cụ ghi thiếu. Đã sửa: " + Dc.SuaGtp + "." + (Dc.G.Dirty ? "\n\nBấm Lưu tệp thuộc tính để ghi lại." : "\n\nĐã lưu lại tệp, bản trước khi sửa giữ ở " + Dc.G.Name + ".bak."); Dc.SuaGtp = ""; MessageBox.Show(this, t, Bt.Ten, MessageBoxButtons.OK, MessageBoxIcon.Information); }
            RefreshTitle(); Dc.Notify(); ShowAttr();
        }
        // ngắt kết nối tệp thuộc tính (lưu thay đổi theo thiết lập; chưa lưu thì hỏi)
        public void CloseGtp(bool say)
        {
            if (Dc.G == null) { if (say) Msg("Chưa kết nối tệp thuộc tính"); return; }
            var n = Dc.G.Name; try { Dc.Commit(); } catch (Exception ex) { Program.Log(ex); }
            if (Dc.G.Dirty && MessageBox.Show(this, "Tệp thuộc tính " + n + " có thay đổi chưa lưu. Lưu trước khi ngắt kết nối?", Bt.Ten, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                try { Dc.G.Save(Dc.G.Path_); } catch (Exception ex) { MessageBox.Show(this, ex.Message, Bt.Ten); }
            Dc.G.Dispose(); Dc.G = null; Dc.GRows = new List<GRow>(); foreach (var p in Dc.P) { p.Gid = null; p.Row = null; } Dc.P.RemoveAll(p => p.FromGtp); Dc.Notify();
            if (say) Msg("Đã ngắt kết nối tệp thuộc tính " + n);
        }
        void DonViHanhChinh()
        {
            using (var f = new DvhcForm(Dc.G != null ? GtpInfo.Read(Dc.G) : null, Dc.G != null ? Dc.G.Name : null))
                if (f.ShowDialog(this) == DialogResult.OK) { RefreshTitle(); Host.MenuRefresh(); if (Dc.G != null) { Dc.Link(); Dc.Notify(); } Msg("Đơn vị hành chính: " + Cfg.I.Place + " · tờ " + Dc.To + " · tỷ lệ 1:" + Cfg.I.TyLe); }
        }
    }
}
