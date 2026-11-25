// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – hộp thoại menu Bản đồ: Sửa lỗi tự động, Tìm lỗi dữ liệu, Tạo thửa đất. © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace M8Pro
{
    // ================= SỬA LỖI TỰ ĐỘNG: thẻ Lớp ranh (bảng chọn lớp) + thẻ Tuỳ chọn sửa; khoảng sửa tối đa và nút ở dưới =================
    public class SuaLoiForm : Form
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        DsLop L = new DsLop();   // danh sách level dùng chung với Tìm lỗi, Tạo thửa: ô tích và chữ cùng hàng
        CheckBox cChiCo = new OTich { Text = "Chỉ hiện level có đối tượng đường", AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(2, 4, 0, 0) };
        ComboBox cTol = new ComboBox { Width = 80, DropDownStyle = ComboBoxStyle.DropDown };
        CheckBox cc = new OTich { AutoSize = true, Text = "Cắt ranh tại giao điểm" }, cu = new OTich { AutoSize = true, Text = "Nối đầu hở: kéo dài theo hướng của đường tới đường khác (bắt chưa tới)" },
            co = new OTich { AutoSize = true, Text = "Cắt phần thừa: đoạn cuối cắt qua đường khác rồi thò ra (bắt quá)" }, cd = new OTich { AutoSize = true, Text = "Xoá đoạn thẳng trùng hệt nhau (cùng level)" };
        SortedDictionary<int, int> dem = new SortedDictionary<int, int>(); HashSet<int> chon;
        public List<int> Levels = new List<int>(); public double Tol; public bool Under, Over, Dup, Cat;
        public SuaLoiForm(ICollection<int> sel)
        {
            Text = "Sửa lỗi tự động"; Font = new Font("Segoe UI", 9f); FormBorderStyle = FormBorderStyle.Sizable; MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; TopMost = true;
            Width = 560; Height = 560; MinimumSize = new Size(480, 420); chon = new HashSet<int>(sel);
            try { dem = Bt.LevelLines(); } catch (Exception ex) { Program.Log(ex); }
            var tabs = new TabControl { Dock = DockStyle.Fill }; var p1 = new TabPage("Level ranh thửa") { Padding = new Padding(6) }; var p2 = new TabPage("Tuỳ chọn sửa") { Padding = new Padding(6) };
            tabs.TabPages.Add(p1); tabs.TabPages.Add(p2);
            L.ItemCheck += (s, e) => { chon = new HashSet<int>(L.Chon); };
            cChiCo.Checked = Cfg.I.ChiLopCo; cChiCo.CheckedChanged += (s, e) => { Cfg.I.ChiLopCo = cChiCo.Checked; NapLop(); };
            var hd = new Label { Dock = DockStyle.Top, Height = 34, ForeColor = Color.FromArgb(40, 60, 90), Text = "Tích các level có đường ranh cần sửa (level có đối tượng in đậm, rê chuột thấy số đối tượng đường). Sửa ngay trên bản vẽ – dùng Undo của chương trình bản vẽ nếu cần hoàn tác." };
            p1.Controls.Add(L); p1.Controls.Add(cChiCo); p1.Controls.Add(hd);
            // tuỳ chọn sửa
            cc.Checked = Cfg.I.CatGiao;
            // nhóm cao vừa nội dung, chữ dài tự xuống dòng (không cắt chữ khi thu hẹp hộp thoại)
            var n1 = new Nhom("Cắt ranh tại giao điểm") { Dock = DockStyle.Top, Vua = true };
            var t1 = GiaoDien.Gian(new Label { Dock = DockStyle.Top, ForeColor = Color.DimGray, Padding = new Padding(0, 2, 0, 2), Text = "Phá đường gấp khúc (line string) thành đoạn thẳng (line), rồi ngắt mỗi đoạn đúng tại điểm cắt, khớp đúng điểm nối. Giữ nguyên mọi đỉnh: không kéo, không xén, không xoá đoạn nào." });
            cc.Dock = DockStyle.Top; GiaoDien.Gian(cc); n1.Than.Controls.Add(t1); n1.Than.Controls.Add(cc);
            var n2 = new Nhom("Sửa thêm đầu đường (chỉ đỉnh đầu / cuối đang hở, trong khoảng sửa tối đa)") { Dock = DockStyle.Top, Vua = true };
            foreach (var x in new[] { cd, co, cu }) { x.Dock = DockStyle.Top; x.Margin = new Padding(0); GiaoDien.Gian(x); n2.Than.Controls.Add(x); }
            var gap = new Panel { Dock = DockStyle.Top, Height = 8 };
            p2.Controls.Add(n2); p2.Controls.Add(gap); p2.Controls.Add(n1);
            // dưới: khoảng sửa tối đa, nút
            foreach (var v in new[] { 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1 }) cTol.Items.Add(GiaoDien.ChuSo(v)); cTol.Text = GiaoDien.ChuSo(Cfg.I.FixTol);
            var bp = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 42, ColumnCount = 5, Padding = new Padding(6, 4, 6, 4) };
            bp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); bp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); bp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); bp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var ok = GiaoDien.Nut("Thực hiện", 100, true); var no = GiaoDien.Nut("Đóng", 84, false); no.DialogResult = DialogResult.Cancel;
            bp.Controls.Add(GiaoDien.Nhan_("Khoảng sửa tối đa (m):"), 0, 0); bp.Controls.Add(cTol, 1, 0); bp.Controls.Add(ok, 3, 0); bp.Controls.Add(no, 4, 0);
            Controls.Add(tabs); Controls.Add(bp); AcceptButton = ok; CancelButton = no; GiaoDien.KhongCat(this);
            ok.Click += (s, e) =>
            {
                Levels = L.Chon.OrderBy(x => x).ToList();
                if (Levels.Count == 0) { MessageBox.Show(this, "Hãy tích ít nhất một level.", Text); return; }
                double d; if (!GiaoDien.So(cTol.Text, out d) || d <= 0) { MessageBox.Show(this, "Khoảng sửa tối đa không hợp lệ.", Text); tabs.SelectedIndex = 0; cTol.Focus(); return; }
                Tol = d; Under = cu.Checked; Over = co.Checked; Dup = cd.Checked; Cat = cc.Checked; DialogResult = DialogResult.OK;
            };
            NapLop();
        }
        void NapLop() { L.Nap(dem, chon, cChiCo.Checked); }
    }

    // ================= TÌM LỖI DỮ LIỆU: lớp ranh (danh sách nhiều cột) bên trái; loại lỗi cần tìm, ngưỡng kiểm tra bên phải =================
    public class TimLoiForm : Form
    {
        public static readonly string[] MAC_DINH = { "ho", "giao", "khop", "cong", "cat", "nho", "so", "trung", "lech" };
        DsLop L = new DsLop(); Dictionary<string, CheckBox> K = new Dictionary<string, CheckBox>(); TextBox tNho = new TextBox { Width = 70 }, tTol = new TextBox { Width = 70 };
        public List<int> Levels = new List<int>(); public HashSet<string> Kinds = new HashSet<string>(); public double Nho, Tol;
        public TimLoiForm(ICollection<int> sel)
        {
            Text = "Tìm lỗi dữ liệu"; Font = new Font("Segoe UI", 9f); FormBorderStyle = FormBorderStyle.Sizable; MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; TopMost = true;
            Width = 720; Height = 500; MinimumSize = new Size(620, 460);
            var dem = new SortedDictionary<int, int>(); try { dem = Bt.LevelLines(); } catch (Exception ex) { Program.Log(ex); }
            L.Nap(dem, sel);
            var nL = new Nhom("Level ranh thửa") { Dock = DockStyle.Fill }; nL.Than.Controls.Add(L);
            var phai = new Panel { Dock = DockStyle.Right, Width = 318, Padding = new Padding(6, 0, 0, 0) };
            // loại lỗi
            var nK = new Nhom("Loại lỗi cần tìm") { Dock = DockStyle.Fill };
            var fk = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var co = Cfg.I.TimLoai ?? ""; var bat = co.Trim() == "" ? new HashSet<string>(MAC_DINH) : new HashSet<string>(co.Split(',').Select(x => x.Trim()));
            // tên loại lỗi đầy đủ, dài thì xuống dòng (neo trên: thêm từ dưới lên)
            foreach (var k in MAC_DINH.Reverse()) { var c = GiaoDien.Gian(new OTich { Text = Bt.KTen[k], Dock = DockStyle.Top, Checked = bat.Contains(k), Padding = new Padding(2, 1, 0, 1) }); K[k] = c; fk.Controls.Add(c); }
            nK.Than.Controls.Add(fk);
            // ngưỡng
            var nN = new Nhom("Ngưỡng kiểm tra") { Dock = DockStyle.Bottom, Vua = true };
            var tn = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            tn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); tn.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); tn.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tNho.Text = GiaoDien.ChuSo(Cfg.I.VungNho); tTol.Text = GiaoDien.ChuSo(Cfg.I.Tol);
            Func<string, Label> LN = t0 => GiaoDien.Gian(new Label { Text = t0, Dock = DockStyle.Fill, Margin = new Padding(3, 6, 3, 2) });
            tn.Controls.Add(LN("Vùng rất nhỏ khi diện tích dưới"), 0, 0); tn.Controls.Add(tNho, 1, 0); tn.Controls.Add(GiaoDien.Nhan_("m²"), 2, 0);
            tn.Controls.Add(LN("Dung sai bắt nối ranh"), 0, 1); tn.Controls.Add(tTol, 1, 1); tn.Controls.Add(GiaoDien.Nhan_("m"), 2, 1);
            nN.Than.Controls.Add(tn);
            phai.Controls.Add(nK); phai.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 6 }); phai.Controls.Add(nN);
            // dưới
            var bp = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 42, ColumnCount = 4, Padding = new Padding(6, 4, 6, 4) };
            bp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); bp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); bp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var md = new LinkLabel { Text = "Đặt lại mặc định", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) };
            md.LinkClicked += (s, e) => { foreach (var c in K.Values) c.Checked = true; tNho.Text = GiaoDien.ChuSo(1); tTol.Text = GiaoDien.ChuSo(0.02); };
            var ok = GiaoDien.Nut("Thực hiện", 100, true); var no = GiaoDien.Nut("Đóng", 84, false); no.DialogResult = DialogResult.Cancel;
            bp.Controls.Add(md, 0, 0); bp.Controls.Add(ok, 2, 0); bp.Controls.Add(no, 3, 0);
            var giua = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 6, 6, 0) }; giua.Controls.Add(nL); giua.Controls.Add(phai);
            Controls.Add(giua); Controls.Add(bp); AcceptButton = ok; CancelButton = no; GiaoDien.KhongCat(this);
            Resize += (s, e) => phai.Width = Math.Max(318, ClientSize.Width * 46 / 100);   // cột phải giãn theo hộp thoại
            ok.Click += (s, e) =>
            {
                Levels = L.Chon; if (Levels.Count == 0) { MessageBox.Show(this, "Hãy tích ít nhất một level ranh thửa.", Text); return; }
                Kinds = new HashSet<string>(K.Where(kv => kv.Value.Checked).Select(kv => kv.Key)); if (Kinds.Count == 0) { MessageBox.Show(this, "Hãy chọn ít nhất một loại lỗi cần tìm.", Text); return; }
                double a, b; if (!GiaoDien.So(tNho.Text, out a) || a < 0) { MessageBox.Show(this, "Ngưỡng vùng rất nhỏ không hợp lệ.", Text); tNho.Focus(); return; }
                if (!GiaoDien.So(tTol.Text, out b) || b <= 0 || b > 5) { MessageBox.Show(this, "Dung sai bắt nối ranh không hợp lệ (0 – 5 m).", Text); tTol.Focus(); return; }
                Nho = a; Tol = b; DialogResult = DialogResult.OK;
            };
        }
    }

    // ================= TẠO THỬA ĐẤT: lớp ranh tạo thửa bên trái; thông tin gán cho thửa và đọc nhãn bên phải =================
    public class TaoThuaForm : Form
    {
        DsLop L = new DsLop(); TextBox tTo = new TextBox { Width = 90 }, tBo = new TextBox { Width = 90 }, tTol = new TextBox { Width = 90 }, tNhan = new TextBox { Width = 90 }, tTamLv = new TextBox { Width = 70 }, tTamMau = new TextBox { Width = 70 }, tTamTl = new TextBox { Width = 70 };
        ComboBox cLe = new ComboBox { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList }, cLd = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDown };
        CheckBox cGhi = new OTich { AutoSize = true, Text = "Ghi thông tin thửa vào tệp thuộc tính" }, cCat = new OTich { AutoSize = true, Text = "Hỏi cắt ranh tại giao điểm trước khi tạo thửa" }, cPhap = new OTich { AutoSize = true, Text = "Lưu thông tin pháp lý" }, cDt = new OTich { AutoSize = true, Text = "Lưu diện tích và tâm thửa" }, cMdsd = new OTich { AutoSize = true, Text = "Cập nhật mục đích sử dụng đất" }, cChu = new OTich { AutoSize = true, Text = "Chuyển thửa thành chủ sử dụng" }, cTam = new OTich { AutoSize = true, Text = "Vẽ tâm thửa sau khi tạo" };
        public List<int> Levels = new List<int>();
        public TaoThuaForm(ICollection<int> sel)
        {
            Text = "Tạo thửa đất"; Font = new Font("Segoe UI", 9f); FormBorderStyle = FormBorderStyle.Sizable; MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; TopMost = true;
            Width = 820; Height = 620; MinimumSize = new Size(700, 560);
            var dem = new SortedDictionary<int, int>(); try { dem = Bt.LevelLines(); } catch (Exception ex) { Program.Log(ex); }
            L.Nap(dem, sel);
            var nL = new Nhom("Các level ranh tạo thửa") { Dock = DockStyle.Fill }; nL.Than.Controls.Add(L);
            var phai = new Panel { Dock = DockStyle.Right, Width = 340, Padding = new Padding(6, 0, 0, 0) };
            // thông tin gán cho thửa
            var nG = new Nhom("Thông tin gán cho thửa") { Dock = DockStyle.Top, Vua = true };
            var tg = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            tg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); tg.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tTo.Text = Dc.To; for (int i = 0; i <= 3; i++) cLe.Items.Add(i.ToString(CultureInfo.InvariantCulture)); cLe.SelectedIndex = Math.Max(0, Math.Min(3, Cfg.I.DtLe));
            cLd.Items.AddRange(new object[] { "", "ONT", "ODT", "CLN", "LUC", "HNK", "TMD", "DTS" }); cLd.Text = Cfg.I.LdMacDinh ?? "";
            tBo.Text = GiaoDien.ChuSo(Cfg.I.BoVung); tTol.Text = GiaoDien.ChuSo(Cfg.I.Tol); cGhi.Checked = Cfg.I.GhiKhiTaoThua; cCat.Checked = Cfg.I.HoiCatTao; cPhap.Checked = Cfg.I.TaoLuuPhapLy; cDt.Checked = Cfg.I.TaoLuuDienTich; cMdsd.Checked = Cfg.I.TaoCapNhatMdsd; cChu.Checked = Cfg.I.TaoChuyenChu; tTamLv.Text = Cfg.I.TamLop.ToString(); tTamMau.Text = Cfg.I.TamMau.ToString(); tTamTl.Text = GiaoDien.ChuSo(Cfg.I.TamTyLe); cTam.Checked = Cfg.I.TamLop > 0;
            Func<string, Label> LN = t0 => GiaoDien.Gian(new Label { Text = t0, Dock = DockStyle.Fill, Margin = new Padding(3, 6, 3, 2) });
            tg.Controls.Add(LN("Tờ bản đồ số"), 0, 0); tg.Controls.Add(tTo, 1, 0);
            tg.Controls.Add(LN("Loại đất mặc định"), 0, 1); tg.Controls.Add(cLd, 1, 1);
            tg.Controls.Add(LN("Số lẻ diện tích"), 0, 2); tg.Controls.Add(cLe, 1, 2);
            tg.Controls.Add(LN("Bỏ vùng có diện tích nhỏ hơn (m²)"), 0, 3); tg.Controls.Add(tBo, 1, 3);
            tg.Controls.Add(LN("Dung sai bắt nối ranh (m)"), 0, 4); tg.Controls.Add(tTol, 1, 4);
            cGhi.Dock = DockStyle.Fill; cCat.Dock = DockStyle.Fill; GiaoDien.Gian(cGhi); GiaoDien.Gian(cCat);
            int rr = 5; foreach (var q in new[] { cGhi, cPhap, cDt, cMdsd, cChu, cCat }) { tg.Controls.Add(q, 0, rr++); tg.SetColumnSpan(q, 2); }
            new ToolTip().SetToolTip(tTo, "Số tờ dùng để liên kết thửa với tệp thuộc tính. Để trống: theo tên tờ bản đồ (" + (Dc.ToTheoTen() != "" ? "tờ " + Dc.ToTheoTen() : "tên tệp không có số tờ") + ").");
            nG.Than.Controls.Add(tg);
            // đọc nhãn
            var nN = new Nhom("Đọc nhãn thửa") { Dock = DockStyle.Fill };
            var tn = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            tn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); tn.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tNhan.Text = Cfg.I.LvNhan;
            tn.Controls.Add(LN("Level chữ nhãn (cách nhau dấu phẩy)"), 0, 0); tn.Controls.Add(tNhan, 1, 0);
            var gc = GiaoDien.Gian(new Label { Dock = DockStyle.Fill, ForeColor = Color.DimGray, Margin = new Padding(3, 6, 3, 3), Text = "Số thửa, loại đất, diện tích đọc từ nhãn nằm trong thửa (nhãn ô N_THUA hoặc chữ rời). Thửa không có nhãn để số thửa trống." });
            tn.Controls.Add(gc, 0, 1); tn.SetColumnSpan(gc, 2); nN.Than.Controls.Add(tn);
            var nT = new Nhom("Vẽ tâm thửa") { Dock = DockStyle.Top, Vua = true }; var tt = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true }; tt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); tt.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tt.Controls.Add(LN("Level"), 0, 0); tt.Controls.Add(tTamLv, 1, 0); tt.Controls.Add(LN("Màu"), 0, 1); tt.Controls.Add(tTamMau, 1, 1); tt.Controls.Add(LN("Tỷ lệ cell"), 0, 2); tt.Controls.Add(tTamTl, 1, 2); tt.Controls.Add(cTam, 0, 3); tt.SetColumnSpan(cTam, 2); nT.Than.Controls.Add(tt);
            phai.Controls.Add(nN); phai.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6 }); phai.Controls.Add(nT); phai.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6 }); phai.Controls.Add(nG);
            // dưới
            var bp = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6, 4, 6, 4) };
            var ok = GiaoDien.Nut("Thực hiện", 100, true); var no = GiaoDien.Nut("Đóng", 84, false); no.DialogResult = DialogResult.Cancel;
            bp.Controls.Add(no); bp.Controls.Add(ok);
            var giua = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 6, 6, 0) }; giua.Controls.Add(nL); giua.Controls.Add(phai);
            Controls.Add(giua); Controls.Add(bp); AcceptButton = ok; CancelButton = no; GiaoDien.KhongCat(this);
            Resize += (s, e) => phai.Width = Math.Max(340, ClientSize.Width * 48 / 100);   // cột phải giãn theo hộp thoại
            ok.Click += (s, e) =>
            {
                Levels = L.Chon; if (Levels.Count == 0) { MessageBox.Show(this, "Hãy tích ít nhất một level ranh thửa.", Text); return; }
                double bo, tol; if (!GiaoDien.So(tBo.Text == "" ? "0" : tBo.Text, out bo) || bo < 0) { MessageBox.Show(this, "Diện tích vùng bỏ qua không hợp lệ.", Text); tBo.Focus(); return; }
                if (!GiaoDien.So(tTol.Text, out tol) || tol <= 0 || tol > 5) { MessageBox.Show(this, "Dung sai bắt nối ranh không hợp lệ (0 – 5 m).", Text); tTol.Focus(); return; }
                var nhan = Cfg.Lvs(tNhan.Text); if (nhan.Count == 0) { MessageBox.Show(this, "Hãy nhập level chữ nhãn (ví dụ 13).", Text); tNhan.Focus(); return; }
                var to = tTo.Text.Trim(); if (to != "" && !System.Text.RegularExpressions.Regex.IsMatch(to, @"^\d+$")) { MessageBox.Show(this, "Số tờ bản đồ phải là số.", Text); tTo.Focus(); return; }
                int tamLv, tamMau; double tamTl; if (!int.TryParse(tTamLv.Text, out tamLv) || tamLv < 0 || !int.TryParse(tTamMau.Text, out tamMau) || tamMau < 0 || !GiaoDien.So(tTamTl.Text, out tamTl) || tamTl <= 0) { MessageBox.Show(this, "Thông số vẽ tâm thửa không hợp lệ.", Text); return; }
                var c = Cfg.I; c.LvRanh = string.Join(",", Levels.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray()); c.DtLe = cLe.SelectedIndex; c.BoVung = bo; c.Tol = tol; c.LdMacDinh = cLd.Text.Trim().ToUpperInvariant();
                c.GhiKhiTaoThua = cGhi.Checked; c.HoiCatTao = cCat.Checked; c.TaoLuuPhapLy = cPhap.Checked; c.TaoLuuDienTich = cDt.Checked; c.TaoCapNhatMdsd = cMdsd.Checked; c.TaoChuyenChu = cChu.Checked; c.TamLop = tamLv; c.TamMau = tamMau; c.TamTyLe = tamTl; c.LvNhan = string.Join(",", nhan.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray()); c.Save();
                // số tờ: trùng số theo tên tờ bản đồ thì để tự động theo tên
                if (to != Dc.To) Dc.SetTo(to == Dc.ToTheoTen() ? "" : to);
                DialogResult = DialogResult.OK;
            };
        }
    }
}
