// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – bảng Hồ sơ thửa đất: gom các lệnh lập hồ sơ theo nhóm – thửa lập hồ sơ, định dạng tệp, hồ sơ của thửa,
// hồ sơ cả tờ bản đồ, thư mục lưu hồ sơ. Các mẫu lập bằng bộ mẫu chạy ngầm như menu Đăng ký / Hồ sơ / Kết xuất. © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace M8Pro
{
    public class HoSoForm : FormBase
    {
        MainForm M; Parcel P; Label lThua = new Label { Dock = DockStyle.Top, Height = 40, Padding = new Padding(2, 2, 2, 0) };
        Label lThuMuc = new Label { Dock = DockStyle.Top, Height = 34, ForeColor = Color.FromArgb(40, 60, 90), AutoEllipsis = true };
        CheckBox cMo = new OTich { Text = "Mở tệp hồ sơ ngay sau khi lập", AutoSize = true, Dock = DockStyle.Top };
        static readonly string[] DD_TEN = { "PDF", "DGN V8", "DGN V7", "DXF" }, DD_MA = { "pdf", "dgn8", "dgn", "dxf" };
        List<RadioButton> dd = new List<RadioButton>(); ToolTip tip = new ToolTip();
        // mã lệnh, tên đầy đủ (gửi bộ mẫu), chữ trên nút
        static readonly string[][] THUA = {
            new[] { "thp", "Phiếu đo đạc chỉnh lý thửa đất (Phụ lục 17)", "Phiếu đo đạc chỉnh lý" }, new[] { "ttl", "Trích lục bản đồ địa chính (Phụ lục 19)", "Trích lục bản đồ" },
            new[] { "hsht", "Phiếu xác nhận kết quả đo đạc hiện trạng thửa đất (Phụ lục 14)", "Xác nhận hiện trạng" }, new[] { "hsmt", "Bản mô tả ranh giới, mốc giới thửa đất (Phụ lục 12)", "Mô tả ranh giới, mốc" },
            new[] { "hsgc", "Sơ đồ thửa đất in trên Giấy chứng nhận", "Sơ đồ in trên GCN" }, new[] { "hstd", "Bảng thống kê tọa độ điểm mốc ranh giới (Phụ lục 14a)", "Bảng toạ độ mốc" },
            new[] { "hsdn", "Đơn đăng ký đất đai, tài sản gắn liền với đất (Mẫu số 15)", "Đơn đăng ký (Mẫu 15)" }, new[] { "t2b", "Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22)", "Bản vẽ tách, hợp thửa" } };
        static readonly string[][] TO = {
            new[] { "hshh", "Sơ đồ thửa đất (DGN): ranh thửa level 10, số thửa level 13, loại đất level 2, diện tích level 4", "Sơ đồ thửa đất (DGN)" }, new[] { "hsmk", "Sổ mục kê đất đai (Phụ lục 18)", "Sổ mục kê" },
            new[] { "hsdc", "Sổ địa chính (Mẫu số 01/ĐK)", "Sổ địa chính" }, new[] { "hsck", "Danh sách công khai kết quả kiểm tra hồ sơ (Mẫu số 06/ĐK)", "Danh sách công khai" },
            new[] { "hsshp", "Shapefile (VN-2000)", "Shapefile (VN-2000)" }, new[] { "hsgj", "GeoJSON (WGS84)", "GeoJSON (WGS84)" } };
        public HoSoForm(MainForm m)
        {
            M = m; Text = "Hồ sơ thửa đất"; Font = m.Font; Width = 430; Height = 760; ShowInTaskbar = false; Icon = m.Icon; StartPosition = FormStartPosition.Manual; MinimumSize = new Size(340, 420);
            var wa = Screen.PrimaryScreen.WorkingArea; Location = new Point(wa.Left + 20, Math.Max(wa.Top, wa.Top + 110));
            var cuon = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6, 6, 6, 6) };
            // 1. thửa lập hồ sơ
            var n1 = new Nhom("Thửa lập hồ sơ") { Dock = DockStyle.Top, Vua = true };
            lThua.Font = new Font(Font, FontStyle.Bold);
            var h1 = new FlowLayoutPanel { Dock = DockStyle.Top, WrapContents = true, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            var bChon = new Button { Text = "Chọn thửa trên bản đồ", AutoSize = true, Height = 28 };
            try { bChon.Image = Icons.Get("cursor", 16); bChon.TextImageRelation = TextImageRelation.ImageBeforeText; } catch { }
            bChon.Click += (s, e) => Run(() => M.Pick("Hồ sơ thửa đất › chạm vào thửa cần lập hồ sơ", p => DatThua(p)));
            h1.Controls.Add(bChon); n1.Than.Controls.Add(h1); n1.Than.Controls.Add(lThua);
            // 2. định dạng tệp
            var n2 = new Nhom("Định dạng tệp") { Dock = DockStyle.Top, Vua = true };
            var f2 = new FlowLayoutPanel { Dock = DockStyle.Top, WrapContents = true, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            int k0 = Math.Max(0, Array.IndexOf(DD_MA, Cfg.I.DinhDang ?? "pdf"));
            for (int i = 0; i < DD_TEN.Length; i++) { var r = new RadioButton { Text = DD_TEN[i], AutoSize = true, Checked = i == k0, Margin = new Padding(3, 4, 10, 0) }; var ma = DD_MA[i]; r.CheckedChanged += (s, e) => { if (((RadioButton)s).Checked) { Cfg.I.DinhDang = ma; Cfg.I.Save(); } }; dd.Add(r); f2.Controls.Add(r); }
            var g2 = new Label { Dock = DockStyle.Top, Height = 20, ForeColor = Color.DimGray, Text = "Áp dụng cho phiếu đo đạc, trích lục, bản vẽ tách hợp thửa" };
            n2.Than.Controls.Add(g2); n2.Than.Controls.Add(f2);
            // 3. hồ sơ của thửa
            var n3 = new Nhom("Hồ sơ của thửa") { Dock = DockStyle.Top, Vua = true }; Luoi(n3, THUA, true);
            // 4. cả tờ bản đồ
            var n4 = new Nhom("Cả tờ bản đồ") { Dock = DockStyle.Top, Vua = true }; Luoi(n4, TO, false);
            // 5. lưu hồ sơ
            var n5 = new Nhom("Lưu hồ sơ") { Dock = DockStyle.Top, Vua = true };
            var h5 = new FlowLayoutPanel { Dock = DockStyle.Top, WrapContents = true, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            var bMo = new Button { Text = "Mở thư mục", AutoSize = true, Height = 28 }; var bDoi = new Button { Text = "Đổi thư mục…", AutoSize = true, Height = 28 };
            try { bMo.Image = Icons.Get("folder", 16); bMo.TextImageRelation = TextImageRelation.ImageBeforeText; } catch { }
            bMo.Click += (s, e) => Run(Web.OpenOutDir);
            bDoi.Click += (s, e) => Run(() =>
            {
                using (var d = new FolderBrowserDialog { Description = "Chọn thư mục lưu hồ sơ (để trống trong Thiết lập: thư mục HoSo cạnh tệp bản đồ)", ShowNewFolderButton = true })
                {
                    try { var cu = Web.OutDir(); if (Directory.Exists(cu)) d.SelectedPath = cu; } catch { }
                    if (d.ShowDialog(this) == DialogResult.OK) { Cfg.I.ThuMucHoSo = d.SelectedPath; Cfg.I.Save(); NapThuMuc(); M.Msg("Hồ sơ lưu vào " + d.SelectedPath); }
                }
            });
            cMo.Checked = Cfg.I.MoHoSo; cMo.CheckedChanged += (s, e) => { Cfg.I.MoHoSo = cMo.Checked; Cfg.I.Save(); };
            n5.Than.Controls.Add(cMo); n5.Than.Controls.Add(h5); n5.Than.Controls.Add(lThuMuc); h5.Controls.Add(bMo); h5.Controls.Add(bDoi);
            // xếp từ dưới lên (cùng neo trên)
            foreach (var c in new Control[] { n5, Gap(), n4, Gap(), n3, Gap(), n2, Gap(), n1 }) cuon.Controls.Add(c);
            GiaoDien.Gian(lThua, 24); GiaoDien.Gian(lThuMuc, 20); GiaoDien.Gian(g2, 18); GiaoDien.Gian(cMo, 22);
            Controls.Add(cuon); DatThua(null); NapThuMuc(); GiaoDien.KhongCat(this);
            Activated += (s, e) => NapThuMuc();
        }
        static Panel Gap() { return new Panel { Dock = DockStyle.Top, Height = 6 }; }
        // danh sách nút một cột, tên mẫu đầy đủ (dài thì xuống dòng, nút cao theo chữ) – neo trên: thêm từ dưới lên
        void Luoi(Nhom n, string[][] ds, bool thua)
        {
            for (int i = ds.Length - 1; i >= 0; i--)
            {
                var x = ds[i]; var id = x[0]; var ten = x[1];
                Image hb = null; try { hb = Icons.Get(Icons.For(ten, thua ? "doc" : "list"), 16); } catch { }
                var b = new NutDai(ten, hb) { Dock = DockStyle.Top };
                tip.SetToolTip(b, ten); b.Click += (s, e) => Lap(id, ten, thua);
                n.Than.Controls.Add(b); if (i > 0) n.Than.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 3 });
            }
        }
        void Run(Action a) { try { a(); } catch (Exception ex) { Program.Log(ex); M.Msg("Lỗi: " + ex.Message); } }
        public void DatThua(Parcel p)
        {
            if (IsDisposed) return; P = p;
            lThua.Text = p == null ? "Chưa chọn thửa – bấm “Chọn thửa trên bản đồ”" : "Thửa " + (p.So == "" ? "(chưa có số)" : p.So) + " · tờ " + Dc.To + " · " + Dc.FD(p.Dt) + " m²" + (p.Ld != "" ? " · " + p.Ld : "");
            lThua.ForeColor = p == null ? Color.DimGray : GiaoDien.ChuNhom;
        }
        void NapThuMuc() { try { lThuMuc.Text = "Thư mục hồ sơ: " + Web.OutDir(); } catch (Exception ex) { lThuMuc.Text = "Thư mục hồ sơ: " + ex.Message; } }
        // lập mẫu: hồ sơ của thửa dùng thửa đang ghi ở trên (chưa có thì chạm chọn); định dạng theo ô Định dạng tệp
        void Lap(string id, string ten, bool thua)
        {
            Run(() =>
            {
                if (!thua) { M.BtForm(id, ten, false, null, false); return; }
                if (id == "t2b") { M.BtForm(id, ten, true, null, false); return; }   // bản vẽ tách, hợp: chọn thửa mới hoặc lập phương án theo lệnh riêng
                if (P != null && Dc.P.Contains(P) && !P.Virtual) { M.BtForm(id, ten, true, P, false); return; }
                M.Pick("Chạm vào thửa cần lập " + ten, p => { DatThua(p); M.BtForm(id, ten, true, p, false); });
            });
        }
    }
}
