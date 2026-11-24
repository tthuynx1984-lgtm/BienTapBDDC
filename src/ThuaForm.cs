// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – thông tin thửa đất: thông tin thửa ở trên; mục đích sử dụng (danh sách bên trái, chi tiết mục đang chọn bên phải);
// chủ sử dụng; nút Chọn trên bản đồ / Phóng tới / Ghi lại / Đóng. Ghi lại là cập nhật ngay vào tệp .gtp. © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace M8Pro
{
    public class ThuaForm : FormBase
    {
        readonly ToolTip tip = new ToolTip();
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        MainForm M; Parcel P;
        TextBox tTo = new TextBox { ReadOnly = true, Dock = DockStyle.Fill }, tSo = new TextBox { Dock = DockStyle.Fill }, tDt = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Right },
            tDtpl = new TextBox { Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Right }, tMa = new TextBox { ReadOnly = true, Dock = DockStyle.Fill }, tLk = new TextBox { ReadOnly = true, Dock = DockStyle.Fill },
            tDc = new TextBox { Dock = DockStyle.Fill }, tGc = new TextBox { Dock = DockStyle.Fill };
        // hình thức sử dụng đất (Thông tư 10/2024/TT-BTNMT): chọn là tự thêm người sử dụng theo hình thức
        ComboBox cbHt = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        // mục đích sử dụng: bảng (lưu dữ liệu, chỉ hiện loại đất và diện tích) + ô chi tiết của dòng đang chọn
        DataGridView gMd = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, ReadOnly = true, RowHeadersVisible = false, BackgroundColor = Color.White, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
        ComboBox cbKh = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
        Label lbTen = new Label { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(40, 60, 90), TextAlign = ContentAlignment.MiddleLeft };
        TextBox tMdDt = new TextBox { Width = 110, TextAlign = HorizontalAlignment.Right }, tTh = new TextBox { Width = 110 };
        CheckBox cChinh = new OTich { Text = "Là mục đích chính", AutoSize = true }, cChung = new OTich { Text = "Sử dụng chung", AutoSize = true };
        Label lTh = GiaoDien.Nhan_("Thời hạn sử dụng"); ThanhChuyen navMd = new ThanhChuyen("mục đích"); bool napMd;
        // người sử dụng đất: bảng (quan hệ, họ tên, ngày sinh, số định danh) + ô chi tiết đủ trường căn cước của người đang chọn
        DataGridView gChu = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, ReadOnly = true, RowHeadersVisible = false, BackgroundColor = Color.White, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
        ThanhChuyen navChu = new ThanhChuyen("người"); Dictionary<string, Control> oChu = new Dictionary<string, Control>(); bool napChu, napHt; List<object> chuDel = new List<object>();
        Label sChu = new Label { Dock = DockStyle.Bottom, Height = 20, ForeColor = Color.DimGray, Padding = new Padding(2, 3, 2, 0) };
        DataGridView gV = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, BackgroundColor = Color.White };
        Label sMd = new Label { Dock = DockStyle.Bottom, Height = 20, ForeColor = Color.DimGray, Padding = new Padding(2, 3, 2, 0) }, st = new Label { Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft };
        CheckBox cLab = new OTich { Text = "Vẽ lại nhãn sau khi ghi (" + Dc.NhanCuMoTa + ")", AutoSize = true, Checked = true, Anchor = AnchorStyles.Left };
        // bật: nhấp vào trong thửa khác trên bản đồ là hộp này chuyển sang thửa đó (chuột phải trên bản đồ: thôi)
        CheckBox bChon = new CheckBox { Text = "Chọn trên bản đồ", Appearance = Appearance.Button, AutoCheck = false, AutoSize = true, TextImageRelation = TextImageRelation.ImageBeforeText, Anchor = AnchorStyles.Left };
        List<object> mdDel = new List<object>(); bool hasTh, hasChung, chon; string snap = "", soCu = "";
        public bool DangChon { get { return chon; } }
        // diện tích trong hộp thoại: dạng số, dấu chấm thập phân
        static string SoDT(double v) { return GiaoDien.SoDT(v); }
        public ThuaForm(MainForm m)
        {
            M = m; Text = "Thông tin thửa đất"; Font = m.Font; Width = 1180; Height = 720; ShowInTaskbar = false; Icon = m.Icon; StartPosition = FormStartPosition.Manual; MinimumSize = new Size(1040, 640);
            var wa = Screen.PrimaryScreen.WorkingArea; Location = new Point(Math.Max(wa.Left, wa.Right - Width - 20), Math.Max(wa.Top, Math.Min(wa.Top + 120, wa.Bottom - Height)));
            var tabs = new TabControl { Dock = DockStyle.Fill }; var p1 = new TabPage("Thửa đất") { Padding = new Padding(6) }; var p2 = new TabPage("Toạ độ đỉnh, cạnh"); tabs.TabPages.Add(p1); tabs.TabPages.Add(p2);
            Func<string, Label> L = s => GiaoDien.Nhan_(s);
            // tab Thửa đất chia đôi: trái – thông tin thửa, hình thức và mục đích sử dụng; phải – người sử dụng đất
            var hai = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0), Padding = new Padding(0) };
            hai.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48)); hai.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 6)); hai.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); hai.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var trai0 = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) }; var phai0 = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            hai.Controls.Add(trai0, 0, 0); hai.Controls.Add(phai0, 2, 0); p1.Controls.Add(hai);
            // ----- thông tin thửa đất -----
            var nT = new Nhom("Thông tin thửa đất") { Dock = DockStyle.Top, Height = 6 * 29 + 38 };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 7 };
            // cột tên ô vừa chữ (không cắt chữ), cột ô nhập chia phần còn lại
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 6; i++) t.RowStyles.Add(new RowStyle(SizeType.Absolute, 29)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // hàng đệm: phần cao thừa không kéo lệch nhãn hàng cuối
            t.Controls.Add(L("Tờ bản đồ số"), 0, 0); t.Controls.Add(tTo, 1, 0); t.Controls.Add(L("Số thửa"), 2, 0); t.Controls.Add(tSo, 3, 0);
            t.Controls.Add(L("Diện tích (m²)"), 0, 1); t.Controls.Add(tDt, 1, 1); t.Controls.Add(L("Diện tích pháp lý"), 2, 1); t.Controls.Add(tDtpl, 3, 1);
            t.Controls.Add(L("Mã định danh"), 0, 2); t.Controls.Add(tMa, 1, 2); t.Controls.Add(L("Liên kết"), 2, 2); t.Controls.Add(tLk, 3, 2);
            t.Controls.Add(L("Hình thức sử dụng"), 0, 3); t.Controls.Add(cbHt, 1, 3); t.SetColumnSpan(cbHt, 3);
            t.Controls.Add(L("Địa chỉ thửa đất"), 0, 4); t.Controls.Add(tDc, 1, 4); t.SetColumnSpan(tDc, 3);
            t.Controls.Add(L("Ghi chú"), 0, 5); t.Controls.Add(tGc, 1, 5); t.SetColumnSpan(tGc, 3);
            cbHt.Items.Add(""); foreach (var h in ChuSD.TenHinhThuc) cbHt.Items.Add(h);
            tip.SetToolTip(cbHt, "Hình thức sử dụng đất (khoản 5 Điều 8 Thông tư 10/2024/TT-BTNMT).\r\nChọn hình thức là tự thêm người sử dụng đất bên phải: vợ và chồng – 2 người; hộ gia đình – chủ hộ và thành viên; đồng sử dụng – từ 2 người.");
            nT.Than.Controls.Add(t);
            // ----- mục đích sử dụng -----
            var nM = new Nhom("Mục đích sử dụng (loại đất chi tiết)") { Dock = DockStyle.Fill };
            var trai = new Panel { Dock = DockStyle.Left, Width = 300 }; navMd.ViTri.Width = 52; trai.Controls.Add(gMd); trai.Controls.Add(navMd); navMd.Dock = DockStyle.Bottom;
            var bAdd = navMd.Them(null, GiaoDien.MuiTen(4, 16, GiaoDien.Nhan), "Thêm mục đích sử dụng", () => ThemMd()); var bDel = navMd.Them(null, GiaoDien.MuiTen(5, 16, GiaoDien.Nhan), "Xoá mục đích đang chọn", () => XoaMd());
            navMd.Items.Remove(bAdd); navMd.Items.Remove(bDel); navMd.Items.Insert(0, bDel); navMd.Items.Insert(0, bAdd); navMd.Items.Insert(2, new ToolStripSeparator());
            navMd.Items.Add(new ToolStripSeparator()); navMd.Them("Chia đủ DT", null, "Một loại đất: nhận cả diện tích thửa; nhiều loại: mục đích chính nhận phần còn lại", () => ChiaDu());
            var phai = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(10, 0, 0, 0) };
            phai.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); phai.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 6; i++) phai.RowStyles.Add(i == 1 ? new RowStyle(SizeType.AutoSize) : new RowStyle(SizeType.Absolute, 28)); phai.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // tên loại đất dài: xuống dòng
            GiaoDien.Gian(lbTen, 24);
            phai.Controls.Add(L("Ký hiệu loại đất"), 0, 0); phai.Controls.Add(cbKh, 1, 0);
            phai.Controls.Add(L("Tên loại đất"), 0, 1); phai.Controls.Add(lbTen, 1, 1);
            phai.Controls.Add(L("Diện tích (m²)"), 0, 2); phai.Controls.Add(tMdDt, 1, 2);
            phai.Controls.Add(cChinh, 1, 3); phai.Controls.Add(cChung, 1, 4); phai.Controls.Add(lTh, 0, 5); phai.Controls.Add(tTh, 1, 5);
            foreach (var k in Dc.LoaiDat.Keys.OrderBy(x => x)) cbKh.Items.Add(k);
            var bGhiT = GiaoDien.Nut("Ghi thửa đất", 110, false); var dM = DongGhi(sMd, bGhiT);
            tip.SetToolTip(bGhiT, "Chỉ ghi thông tin thửa và mục đích sử dụng vào tệp (người sử dụng đang sửa dở vẫn giữ để ghi sau)"); bGhiT.Click += (s, e) => Ghi(true, false);
            nM.Than.Controls.Add(phai); nM.Than.Controls.Add(trai); nM.Than.Controls.Add(dM);
            trai0.Controls.Add(nM); trai0.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6 }); trai0.Controls.Add(nT);
            // ----- người sử dụng đất -----
            var nC = new Nhom("Người sử dụng đất") { Dock = DockStyle.Fill };
            var tren = new Panel { Dock = DockStyle.Top, Height = 200 }; navChu.ViTri.Width = 52; tren.Controls.Add(gChu); tren.Controls.Add(navChu); navChu.Dock = DockStyle.Bottom;
            var cAdd = navChu.Them(null, GiaoDien.MuiTen(4, 16, GiaoDien.Nhan), "Thêm người sử dụng đất (vai trò theo hình thức sử dụng)", () => ThemChu(null)); var cDel = navChu.Them(null, GiaoDien.MuiTen(5, 16, GiaoDien.Nhan), "Bỏ người đang chọn khỏi thửa", () => XoaChu());
            navChu.Items.Remove(cAdd); navChu.Items.Remove(cDel); navChu.Items.Insert(0, cDel); navChu.Items.Insert(0, cAdd); navChu.Items.Insert(2, new ToolStripSeparator());
            navChu.Items.Add(new ToolStripSeparator()); navChu.Them("Theo hình thức", null, "Thêm đủ người theo hình thức sử dụng đang chọn và điền vai trò còn trống", () => TheoHinhThuc(true));
            // ô chi tiết: hai cặp tên – ô mỗi hàng; trường dài chiếm cả hàng
            var ct = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 7, Padding = new Padding(0, 6, 0, 0) };
            ct.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); ct.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); ct.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); ct.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 6; i++) ct.RowStyles.Add(new RowStyle(SizeType.Absolute, 29)); ct.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Func<string, Control> O = k =>
            {
                var tr = ChuSD.T(k); Control c;
                if (tr.Ds != null) { var cb = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems }; cb.Items.AddRange(tr.Ds); c = cb; }
                else c = new TextBox { Dock = DockStyle.Fill };
                c.TextChanged += (s, e) => GhiChu(k, c.Text); oChu[k] = c; return c;
            };
            Action<string, int, int, int> D = (k, cot, hang, span) => { ct.Controls.Add(L(ChuSD.T(k).L), cot, hang); var c = O(k); ct.Controls.Add(c, cot + 1, hang); if (span > 1) ct.SetColumnSpan(c, span); };
            D("qh", 0, 0, 1); D("gt", 2, 0, 1); D("ten", 0, 1, 3); D("ns", 0, 2, 1); D("qt", 2, 2, 1); D("lgt", 0, 3, 1); D("so", 2, 3, 1);
            D("nc", 0, 4, 1); D("noic", 2, 4, 1); D("ct", 0, 5, 3);
            tip.SetToolTip(oChu["ns"], "Ngày sinh dd/mm/yyyy (chỉ biết năm thì nhập năm)"); tip.SetToolTip(oChu["so"], "Số định danh cá nhân (12 số trên thẻ căn cước) hoặc số giấy tờ");
            tip.SetToolTip(oChu["nc"], "dd/mm/yyyy");
            var bGhiC = GiaoDien.Nut("Ghi người sử dụng", 140, false); var dC = DongGhi(sChu, bGhiC);
            tip.SetToolTip(bGhiC, "Chỉ ghi người sử dụng đất và hình thức sử dụng vào tệp (thông tin thửa đang sửa dở vẫn giữ để ghi sau)"); bGhiC.Click += (s, e) => Ghi(false, true);
            nC.Than.Controls.Add(ct); nC.Than.Controls.Add(tren); nC.Than.Controls.Add(dC);
            phai0.Controls.Add(nC);
            p2.Controls.Add(gV); gV.Columns.Add("i", "Đỉnh"); gV.Columns.Add("x", "X (m)"); gV.Columns.Add("y", "Y (m)"); gV.Columns.Add("c", "Cạnh"); gV.Columns.Add("l", "Dài (m)");
            // ----- nút dưới -----
            var bp = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 72, ColumnCount = 6, RowCount = 2, Padding = new Padding(6, 2, 6, 4) };
            bp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); bp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); bp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); for (int i = 0; i < 3; i++) bp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bp.RowStyles.Add(new RowStyle(SizeType.AutoSize)); bp.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var bGhi = GiaoDien.Nut("Ghi lại", 100, true); var bZoom = GiaoDien.Nut("Phóng tới", 90, false); var bClose = GiaoDien.Nut("Đóng", 84, false);
            try { bChon.Image = Icons.Get("cursor", 16); bGhi.Image = Icons.Get("save", 16); bGhi.TextImageRelation = TextImageRelation.ImageBeforeText; bZoom.Image = Icons.Get("zin", 16); bZoom.TextImageRelation = TextImageRelation.ImageBeforeText; } catch { }
            new ToolTip().SetToolTip(bChon, "Bật: nhấp vào trong thửa khác trên bản đồ là hộp này chuyển sang thửa đó (đang sửa dở thì hỏi ghi trước).\r\nChuột phải trên bản đồ hoặc bấm lại nút này để thôi.");
            bp.Controls.Add(st, 0, 0); bp.SetColumnSpan(st, 6);
            bp.Controls.Add(bChon, 0, 1); bp.Controls.Add(cLab, 1, 1); bp.Controls.Add(bZoom, 3, 1); bp.Controls.Add(bGhi, 4, 1); bp.Controls.Add(bClose, 5, 1);
            Controls.Add(tabs); Controls.Add(bp); AcceptButton = null; CancelButton = bClose;
            Shown += (s, e) => { navMd.PerformLayout(); navChu.PerformLayout(); };   // thanh nút xếp lại sau khi khung có kích thước thật
            // dòng trạng thái dài: xuống dòng, khung nút cao theo
            GiaoDien.Gian(st, 24); st.SizeChanged += (s, e) => bp.Height = Math.Max(72, st.Height + 48); GiaoDien.KhongCat(this);
            // cột mục đích sử dụng: hiện loại đất, diện tích; các cột còn lại giữ dữ liệu cho ô chi tiết
            gMd.Columns.Add(new DataGridViewTextBoxColumn { Name = "kh", HeaderText = "Loại đất", Width = 70, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 64 }); gMd.Columns.Add(new DataGridViewTextBoxColumn { Name = "ten", HeaderText = "Tên loại đất", Visible = false });
            gMd.Columns.Add(new DataGridViewTextBoxColumn { Name = "dt", HeaderText = "Diện tích (m²)", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            gMd.Columns.Add(new DataGridViewCheckBoxColumn { Name = "chinh", HeaderText = "Chính", Width = 46 });
            gMd.Columns.Add(new DataGridViewTextBoxColumn { Name = "th", HeaderText = "Thời hạn", Visible = false }); gMd.Columns.Add(new DataGridViewCheckBoxColumn { Name = "chung", HeaderText = "Chung", Visible = false });
            gMd.Columns.Add(new DataGridViewTextBoxColumn { Name = "id", Visible = false });
            gMd.Columns["dt"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; gMd.AlternatingRowsDefaultCellStyle.BackColor = GiaoDien.NenXen;
            gMd.DataError += (s, e) => { e.ThrowException = false; };
            gMd.CellValueChanged += (s, e) => { if (e.RowIndex < 0) return; if (gMd.Columns[e.ColumnIndex].Name == "kh") { var k = Convert.ToString(gMd.Rows[e.RowIndex].Cells["kh"].Value); gMd.Rows[e.RowIndex].Cells["ten"].Value = k != null && Dc.LoaiDat.ContainsKey(k) ? Dc.LoaiDat[k] : ""; } SumMd(); };
            gMd.SelectionChanged += (s, e) => NapMd();
            navMd.Chuyen += d => { int n = gMd.Rows.Count; if (n == 0) return; int i0 = gMd.CurrentRow != null ? gMd.CurrentRow.Index : -1; int j = d == -2 ? 0 : d == 2 ? n - 1 : Math.Max(0, Math.Min(n - 1, i0 + d)); gMd.CurrentCell = gMd.Rows[j].Cells["kh"]; };
            // ô chi tiết → dòng đang chọn
            cbKh.TextChanged += (s, e) => GhiMd("kh", cbKh.Text.Trim().ToUpperInvariant());
            tMdDt.TextChanged += (s, e) => GhiMd("dt", tMdDt.Text.Trim());
            tTh.TextChanged += (s, e) => GhiMd("th", tTh.Text);
            cChung.CheckedChanged += (s, e) => GhiMd("chung", cChung.Checked);
            // một mục đích chính: tích dòng này thì bỏ tích các dòng khác
            cChinh.CheckedChanged += (s, e) =>
            {
                if (napMd || gMd.CurrentRow == null) return; GhiMd("chinh", cChinh.Checked);
                if (cChinh.Checked) foreach (DataGridViewRow r in gMd.Rows) if (r != gMd.CurrentRow && Convert.ToBoolean(r.Cells["chinh"].Value ?? false)) r.Cells["chinh"].Value = false;
            };
            // cột người sử dụng: quan hệ, họ tên, ngày sinh, số định danh (đủ trường ở ô chi tiết bên dưới)
            gChu.Columns.Add(new DataGridViewTextBoxColumn { Name = "qh", HeaderText = "Vai trò", Width = 130 });
            gChu.Columns.Add(new DataGridViewTextBoxColumn { Name = "ten", HeaderText = "Họ, chữ đệm và tên", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            gChu.Columns.Add(new DataGridViewTextBoxColumn { Name = "ns", HeaderText = "Ngày sinh", Width = 84 }); gChu.Columns.Add(new DataGridViewTextBoxColumn { Name = "so", HeaderText = "Số định danh", Width = 110 });
            gChu.AlternatingRowsDefaultCellStyle.BackColor = GiaoDien.NenXen; gChu.DataError += (s, e) => { e.ThrowException = false; };
            gChu.SelectionChanged += (s, e) => NapChu();
            navChu.Chuyen += d => { int n = gChu.Rows.Count; if (n == 0) return; int i0 = gChu.CurrentRow != null ? gChu.CurrentRow.Index : -1; int j = d == -2 ? 0 : d == 2 ? n - 1 : Math.Max(0, Math.Min(n - 1, i0 + d)); gChu.CurrentCell = gChu.Rows[j].Cells["ten"]; };
            cbHt.SelectedIndexChanged += (s, e) => { if (!napHt) TheoHinhThuc(false); };
            bZoom.Click += (s, e) => { if (P != null && P.Rings != null && (!P.Virtual || Dc.DocLap)) { Dc.Zoom(P, .6); Ms.Hilite(P.Rings); } };
            bGhi.Click += (s, e) => Ghi(); tip.SetToolTip(bGhi, "Ghi cả thông tin thửa đất và người sử dụng đất");
            bClose.Click += (s, e) => Close();
            bChon.Click += (s, e) => { if (chon) ThoiChon(); else ChonTren(); };
            FormClosed += (s, e) => { Ms.Hilite(null); ThoiChon(); };
            NapMd(); NapChu();
        }
        // ---------- người sử dụng đất: danh sách ↔ ô chi tiết ----------
        NguoiSD ChuDang { get { return gChu.CurrentRow != null ? gChu.CurrentRow.Tag as NguoiSD : null; } }
        List<NguoiSD> DsChu { get { return gChu.Rows.Cast<DataGridViewRow>().Select(r => (NguoiSD)r.Tag).ToList(); } }
        string HinhThuc { get { return Convert.ToString(cbHt.SelectedItem) ?? ""; } }
        void DongChu(DataGridViewRow r) { var n = (NguoiSD)r.Tag; r.Cells["qh"].Value = n["qh"]; r.Cells["ten"].Value = n["ten"]; r.Cells["ns"].Value = n["ns"]; r.Cells["so"].Value = n["so"]; }
        int ThemDong(NguoiSD n) { int i = gChu.Rows.Add(); var r = gChu.Rows[i]; r.Tag = n; DongChu(r); return i; }
        void NapChu()
        {
            napChu = true;
            try
            {
                var n = ChuDang; bool co = n != null;
                foreach (var kv in oChu) { kv.Value.Enabled = co; kv.Value.Text = co ? n[kv.Key] : ""; }
                navChu.Dat(co ? gChu.CurrentRow.Index : -1, gChu.Rows.Count); SumChu();
            }
            finally { napChu = false; }
        }
        void GhiChu(string k, string v)
        {
            if (napChu) return; var n = ChuDang; if (n == null) return; n[k] = v;
            if (k == "qh" || k == "ten" || k == "ns" || k == "so") DongChu(gChu.CurrentRow); SumChu();
        }
        void ThemChu(string qh)
        {
            var n = new NguoiSD(); n["qh"] = qh ?? ChuSD.VaiTroThem(HinhThuc, DsChu); n["qt"] = "Việt Nam"; n["lgt"] = "Thẻ căn cước";
            int i = ThemDong(n); gChu.CurrentCell = gChu.Rows[i].Cells["ten"]; NapChu(); if (qh == null) oChu["ten"].Focus();
        }
        void XoaChu()
        {
            var n = ChuDang; if (n == null) return;
            if (n.Id != null && Convert.ToString(n.Id, IC) != "") { if (MessageBox.Show(this, "Bỏ " + (n.Ten != "" ? n.Ten : "người đang chọn") + " khỏi thửa này?\r\n(Ghi lại mới lưu vào tệp; người không còn đăng ký thửa nào thì xoá khỏi tệp)", Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return; chuDel.Add(n.Id); }
            gChu.Rows.Remove(gChu.CurrentRow); NapChu();
        }
        // theo hình thức sử dụng: thêm đủ người (vợ và chồng: 2; hộ gia đình: chủ hộ và thành viên; đồng sử dụng: từ 2) và điền vai trò còn trống
        void TheoHinhThuc(bool bao)
        {
            var ht = HinhThuc; var vt = ChuSD.VaiTro(ht); if (vt.Length == 0) { if (bao) M.Msg("Chọn hình thức sử dụng trước"); SumChu(); return; }
            var ds = DsChu; int them = 0;
            for (int i = 0; i < ds.Count && i < vt.Length; i++) if (ds[i]["qh"] == "" || (i > 0 && ds[i]["qh"] == "Người sử dụng đất")) { ds[i]["qh"] = vt[i]; DongChu(gChu.Rows[i]); }
            while (gChu.Rows.Count < vt.Length) { ThemChu(vt[gChu.Rows.Count]); them++; }
            if (gChu.Rows.Count > 0 && gChu.CurrentRow == null) gChu.CurrentCell = gChu.Rows[0].Cells["ten"];
            NapChu();
            if (them > 0) { int j = gChu.Rows.Count - them; gChu.CurrentCell = gChu.Rows[j].Cells["ten"]; NapChu(); }
        }
        void SumChu()
        {
            var ds = DsChu; var ht = HinhThuc; int n = ds.Count(x => !x.Trong); string bao = "";
            if (ht != "" && !ChuSD.Chung(ht) && n > 1) bao = "  — sử dụng riêng chỉ một người sử dụng đất";
            else if (ChuSD.Chung(ht) && n < 2) bao = "  — sử dụng chung cần từ 2 người";
            sChu.Text = n + " người sử dụng đất" + (ht == "" ? " · chưa chọn hình thức sử dụng" : "") + bao; tip.SetToolTip(sChu, sChu.Text); sChu.ForeColor = bao != "" ? Color.Firebrick : Color.DimGray;
        }
        // ---------- mục đích sử dụng: danh sách ↔ ô chi tiết ----------
        void NapMd()
        {
            napMd = true;
            try
            {
                var r = gMd.CurrentRow; bool co = r != null; cbKh.Enabled = tMdDt.Enabled = cChinh.Enabled = cChung.Enabled = tTh.Enabled = co && gMd.Enabled;
                cbKh.Text = co ? Convert.ToString(r.Cells["kh"].Value, IC) ?? "" : ""; lbTen.Text = co ? Convert.ToString(r.Cells["ten"].Value, IC) ?? "" : "";
                tMdDt.Text = co ? Convert.ToString(r.Cells["dt"].Value, IC) ?? "" : ""; cChinh.Checked = co && Convert.ToBoolean(r.Cells["chinh"].Value ?? false);
                cChung.Checked = co && Convert.ToBoolean(r.Cells["chung"].Value ?? false); tTh.Text = co ? Convert.ToString(r.Cells["th"].Value, IC) ?? "" : "";
                cChung.Visible = hasChung; tTh.Visible = lTh.Visible = hasTh;
                navMd.Dat(co ? r.Index : -1, gMd.Rows.Count);
            }
            finally { napMd = false; }
        }
        void GhiMd(string cot, object v)
        {
            if (napMd || gMd.CurrentRow == null) return; gMd.CurrentRow.Cells[cot].Value = v;
            if (cot == "kh") lbTen.Text = Convert.ToString(gMd.CurrentRow.Cells["ten"].Value, IC) ?? "";
        }
        void ThemMd()
        {
            if (!gMd.Enabled) return; int i = gMd.Rows.Add(); var r = gMd.Rows[i];
            r.Cells["chinh"].Value = gMd.Rows.Count == 1; r.Cells["dt"].Value = gMd.Rows.Count == 1 ? SoDT(Area()) : ""; gMd.CurrentCell = r.Cells["kh"]; SumMd(); NapMd(); cbKh.Focus();
        }
        void XoaMd()
        {
            if (!gMd.Enabled || gMd.CurrentRow == null) return; var id = gMd.CurrentRow.Cells["id"].Value; if (id != null && Convert.ToString(id) != "") mdDel.Add(id);
            gMd.Rows.Remove(gMd.CurrentRow); SumMd(); NapMd();
        }
        // một loại đất: nhận cả diện tích thửa; nhiều loại: loại chính nhận phần còn lại
        void ChiaDu()
        {
            if (gMd.Rows.Count == 0) return; double a = Area();
            if (gMd.Rows.Count == 1) { gMd.Rows[0].Cells["dt"].Value = SoDT(a); SumMd(); NapMd(); return; }
            double other = 0; DataGridViewRow main = null;
            foreach (DataGridViewRow r in gMd.Rows) { if (main == null && Convert.ToBoolean(r.Cells["chinh"].Value ?? false)) { main = r; continue; } other += Num(r.Cells["dt"].Value) ?? 0; }
            if (main == null) main = gMd.Rows[0]; main.Cells["dt"].Value = SoDT(Math.Max(0, a - other)); SumMd(); NapMd();
        }
        // ---------- chọn thửa tiếp theo bằng cách nhấp trên bản đồ ----------
        // lệnh chọn của công cụ chạy trong chương trình bản vẽ: mỗi lần nhấp vào trong một thửa là hộp chuyển sang thửa đó; chuột phải, chọn lệnh khác
        // hoặc bấm lại nút là thôi
        public void ChonTren()
        {
            if (IsDisposed || !Ms.Connected) return;
            if (Dc.P.Count == 0 || Dc.Dirty) try { Dc.Build(false); } catch (Exception ex) { Program.Log(ex); }
            if (Dc.P.Count(q => !q.Virtual) == 0) { M.Msg("Chưa có thửa trên bản đồ (Bản đồ → Tạo thửa) – chưa chọn thửa trên bản đồ được"); return; }
            chon = true; bChon.Checked = true;
            try
            {
                Ms.PickMany("Thông tin thửa đất › nhấp vào trong thửa để xem, sửa · chuột phải: thôi", (x, y) =>
                {
                    if (IsDisposed) return; var p = Dc.Find(x, y);
                    if (p == null) { M.Msg("Không có thửa tại điểm vừa nhấp – nhấp vào bên trong ranh thửa"); return; }
                    Chuyen(p);
                }, Thoi, Thoi);
                M.Msg("Nhấp vào trong thửa khác trên bản đồ để chuyển hộp Thông tin thửa đất sang thửa đó · chuột phải để thôi");
            }
            catch (Exception ex) { Program.Log(ex); Thoi(); }
        }
        void Thoi() { chon = false; if (!IsDisposed) bChon.Checked = false; }
        void ThoiChon() { if (chon) { chon = false; try { Ms.App.CommandState.StartDefaultCommand(); } catch { } } if (!IsDisposed) bChon.Checked = false; }
        // số tờ của thửa: kết nối độc lập theo dòng của tệp, không thì theo tờ bản đồ đang mở
        static string ToCua(Parcel p) { return Dc.DocLap && p != null && p.Row != null && !string.IsNullOrEmpty(p.Row.To) ? p.Row.To : Dc.To; }
        // các dòng cùng tờ với thửa (kiểm tra trùng số thửa)
        static IEnumerable<GRow> RowsCung(Parcel p) { if (!Dc.DocLap) return Dc.SheetRows(); var to = ToCua(p); return Dc.GRows.Where(q => q.To == to); }
        static string Ten(Parcel p) { return (p.So == "" ? "(chưa có số)" : p.So) + " (" + Dc.FD(p.Dt) + " m²)"; }
        // chuyển sang thửa khác: đang sửa dở thì hỏi ghi trước
        void Chuyen(Parcel p)
        {
            if (p == null || IsDisposed) return; if (p == P) { M.Msg("Đang ở thửa " + Ten(p)); return; }
            if (!ChoChuyen(p)) return;
            Show_(p); M.ChonThua(p); M.Msg("Thông tin thửa đất: thửa " + Ten(p) + (p.Ld != "" ? ", " + p.Ld : "") + " · nhấp thửa khác để chuyển tiếp, chuột phải để thôi");
        }
        // trước khi hộp chuyển sang thửa khác: thửa đang sửa dở chưa ghi thì hỏi (false: ở lại thửa đang sửa)
        public bool ChoChuyen(Parcel p)
        {
            if (IsDisposed || P == null || p == P || !Doi()) return true;
            var r = MessageBox.Show(this, "Thửa " + Ten(P) + " có thay đổi chưa ghi.\r\n\r\nGhi trước khi chuyển sang thửa " + (p != null ? Ten(p) : "khác") + "?\r\n\r\nCó: ghi rồi chuyển · Không: bỏ thay đổi, chuyển luôn · Huỷ: ở lại thửa đang sửa", Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.No) { try { gMd.CancelEdit(); gChu.CancelEdit(); } catch { } return true; }
            return Ghi();
        }
        // ảnh chụp các ô sửa được (biết thửa đang sửa dở chưa ghi)
        string Chup()
        {
            var sb = new System.Text.StringBuilder(); sb.Append(tSo.Text).Append('|').Append(tDtpl.Text).Append('|').Append(tDc.Text).Append('|').Append(tGc.Text).Append('|').Append(mdDel.Count);
            foreach (DataGridViewRow r in gMd.Rows) foreach (var c in new[] { "kh", "dt", "chinh", "th", "chung", "id" }) sb.Append('|').Append(Convert.ToString(r.Cells[c].Value, IC));
            sb.Append('#').Append(HinhThuc).Append('|').Append(chuDel.Count); foreach (var n in DsChu) sb.Append('#').Append(n.Chup());
            return sb.ToString();
        }
        // ô đang gõ dở (chưa xác nhận) cũng tính là thay đổi
        bool Doi() { return P != null && (gMd.IsCurrentCellDirty || gChu.IsCurrentCellDirty || Chup() != snap); }
        double Area() { return P == null ? 0 : Math.Round(P.Dt, Cfg.I.DtLe); }
        static double? Num(object v) { double d; var s = Convert.ToString(v, IC) ?? ""; s = s.Trim().Replace(" ", ""); if (s.Contains(",") && !s.Contains(".")) s = s.Replace(',', '.'); else s = s.Replace(",", ""); return double.TryParse(s, NumberStyles.Float, IC, out d) ? d : (double?)null; }
        void SumMd()
        {
            double sum = 0; foreach (DataGridViewRow r in gMd.Rows) sum += Num(r.Cells["dt"].Value) ?? 0; double a = Area();
            sMd.Text = "Cộng mục đích sử dụng: " + SoDT(sum) + " m² · diện tích thửa " + SoDT(a) + " m²" + (gMd.Rows.Count > 0 && Math.Abs(sum - a) > Math.Pow(10, -Cfg.I.DtLe) * 1.5 ? "  — chưa khớp" : "");
            sMd.ForeColor = gMd.Rows.Count > 0 && Math.Abs(sum - a) > Math.Pow(10, -Cfg.I.DtLe) * 1.5 ? Color.Firebrick : Color.DimGray;
        }
        public void Show_(Parcel p)
        {
            P = p; mdDel.Clear(); chuDel.Clear(); gMd.Rows.Clear(); gChu.Rows.Clear(); gV.Rows.Clear(); snap = ""; if (p == null) { Text = "Thông tin thửa đất – chưa chọn thửa"; NapMd(); return; }
            Text = "Thông tin thửa đất – tờ " + ToCua(p) + ", thửa " + (p.So == "" ? "(chưa có số)" : p.So); var r = p.Row;
            tTo.Text = ToCua(p); tSo.Text = p.So; tDt.Text = SoDT(p.Dt); tDtpl.Text = p.Dtpl.HasValue ? SoDT(p.Dtpl.Value) : ""; tDc.Text = p.DiaChi; tGc.Text = p.GhiChu;
            var G = Dc.G; bool g = G != null; hasTh = g && G.Has("DaMucDichSuDung", "thoiHanSuDung"); hasChung = g && G.Has("DaMucDichSuDung", "suDungChung");
            gMd.Enabled = g; gChu.Enabled = true;
            tLk.Text = g ? (p.Gid != null ? (p.FromGtp ? "Hình trong tệp thuộc tính" : "Đã liên kết tệp thuộc tính") : "Chưa có trong tệp thuộc tính") : "Chưa kết nối tệp thuộc tính";
            tip.SetToolTip(tLk, tLk.Text); tip.SetToolTip(tMa, tMa.Text);   // ô chỉ xem: rê chuột xem đủ chữ
            tMa.Text = "";
            if (g && p.Gid != null && G.Has("ThuaDat", "maDinhDanh", "thuaDatId"))
                try { var d0 = G.Query("select maDinhDanh from ThuaDat where thuaDatId=?", p.Gid).FirstOrDefault(); if (d0 != null && d0["maDinhDanh"] != null) tMa.Text = Convert.ToString(d0["maDinhDanh"], IC); } catch (Exception ex) { Program.Log(ex); }
            Action<string> need = k => { if (!string.IsNullOrEmpty(k) && !cbKh.Items.Contains(k)) cbKh.Items.Add(k); };
            if (g && p.Gid != null && G.Has("DaMucDichSuDung", "daMucDichSuDungId", "thuaDatId"))
            {
                foreach (var d in G.Query("select * from DaMucDichSuDung where thuaDatId=? order by daMucDichSuDungId", p.Gid))
                {
                    object lo = null; foreach (var c in new[] { "loaiMucDichSuDungId", "loaiMucDichSuDungKiemKeId", "loaiMucDichSuDungQuyHoachId" }) if (lo == null && d.ContainsKey(c) && d[c] != null) lo = d[c];
                    string code = null; if (lo != null) { long v; if (long.TryParse(Convert.ToString(lo, IC), out v)) code = LoaiDatMa.Get(v) ?? "#" + v; }
                    need(code); int i = gMd.Rows.Add(); var rw = gMd.Rows[i];
                    rw.Cells["kh"].Value = code; rw.Cells["ten"].Value = code != null && Dc.LoaiDat.ContainsKey(code) ? Dc.LoaiDat[code] : "";
                    rw.Cells["dt"].Value = d.ContainsKey("dienTich") && d["dienTich"] != null ? SoDT(Convert.ToDouble(d["dienTich"], IC)) : "";
                    rw.Cells["chinh"].Value = d.ContainsKey("laMucDichChinh") && Convert.ToString(d["laMucDichChinh"], IC).Contains("1");
                    if (hasTh) rw.Cells["th"].Value = Convert.ToString(d["thoiHanSuDung"], IC); if (hasChung) rw.Cells["chung"].Value = Convert.ToString(d["suDungChung"], IC).Contains("1");
                    rw.Cells["id"].Value = d["daMucDichSuDungId"];
                }
            }
            else if (p.Ld != "")
            {
                var codes = p.Ld.Split('+', ',').Select(x => x.Trim()).Where(x => x != "").ToArray();
                for (int i = 0; i < codes.Length; i++) { need(codes[i]); int k = gMd.Rows.Add(); var rw = gMd.Rows[k]; rw.Cells["kh"].Value = codes[i]; rw.Cells["ten"].Value = Dc.LoaiDat.ContainsKey(codes[i]) ? Dc.LoaiDat[codes[i]] : ""; rw.Cells["dt"].Value = codes.Length == 1 ? SoDT(Area()) : ""; rw.Cells["chinh"].Value = i == 0; }
            }
            if (gMd.Rows.Count > 0) gMd.CurrentCell = gMd.Rows[0].Cells["kh"];
            // chủ sử dụng
            List<NguoiSD> ds; string ht = "";
            if (g && p.Gid != null && ChuSD.CoBang(G)) { ds = ChuSD.Doc(G, p.Gid); ht = ChuSD.DocHT(G, p.Gid); }
            else
            {
                ds = ChuSD.DocMay(p.So, out ht);
                if (ds.Count == 0 && p.Chu != "") { var n0 = new NguoiSD(); n0["qh"] = "Người sử dụng đất"; n0["ten"] = p.Chu; n0["ns"] = r != null ? r.NamSinh : ""; n0["ct"] = r != null ? r.ChuDc : ""; ds.Add(n0); }
            }
            napHt = true; try { cbHt.SelectedIndex = cbHt.Items.IndexOf(ht ?? ""); } finally { napHt = false; }
            foreach (var n0 in ds) ThemDong(n0);
            if (gChu.Rows.Count > 0) gChu.CurrentCell = gChu.Rows[0].Cells[1];
            NapChu(); soCu = p.So;
            // tọa độ đỉnh, cạnh
            var vt = p.Rings != null && p.Rings.Count > 0 && p.Rings[0].Length >= 6 ? Dc.Verts(p) : new double[0]; int n = vt.Length / 2;
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; gV.Rows.Add(i + 1, Dc.F(vt[2 * i + 1], 3), Dc.F(vt[2 * i], 3), (i + 1) + "-" + (j + 1), Dc.F(Geo.Dist(vt[2 * i], vt[2 * i + 1], vt[2 * j], vt[2 * j + 1]), 2)); }
            SumMd(); NapMd();
            st.Text = g ? (p.Gid != null ? "Đã liên kết " + G.Name + " · Ghi lại là lưu ngay vào tệp" : "Thửa chưa có trong " + G.Name + " · Ghi lại sẽ thêm thửa vào tệp") : "Chưa kết nối tệp thuộc tính: chỉ lưu số thửa, loại đất, chủ… trên máy";
            if (!p.Virtual) Ms.Hilite(p.Rings);
            snap = Chup();
        }
        // ghi tất cả thay đổi vào tệp thuộc tính rồi lưu tệp ngay
        // dòng cuối nhóm: dòng tổng/cảnh báo bên trái, nút ghi riêng bên phải
        static Panel DongGhi(Label l, Button b)
        {
            var d = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(0, 4, 0, 2) };
            l.Dock = DockStyle.Fill; l.Padding = new Padding(2, 0, 6, 0); l.TextAlign = ContentAlignment.MiddleLeft; b.Dock = DockStyle.Right;
            try { b.Image = Icons.Get("save", 16); b.TextImageRelation = TextImageRelation.ImageBeforeText; } catch { }
            d.Controls.Add(l); d.Controls.Add(b); return d;
        }
        // giữ phần chưa ghi của nửa kia khi chỉ ghi một nửa
        class GiuLai { public bool T, C; public string So, Dtpl, DiaChi, Gc, Ht; public List<object[]> Md = new List<object[]>(); public List<object> MdDel, ChuDel; public List<NguoiSD> Chu; }
        GiuLai Giu(bool t, bool c)
        {
            var g = new GiuLai { T = t, C = c };
            if (t) { g.So = tSo.Text; g.Dtpl = tDtpl.Text; g.DiaChi = tDc.Text; g.Gc = tGc.Text; g.MdDel = new List<object>(mdDel); foreach (DataGridViewRow r in gMd.Rows) g.Md.Add(r.Cells.Cast<DataGridViewCell>().Select(x => x.Value).ToArray()); }
            if (c) { g.Chu = DsChu; g.Ht = HinhThuc; g.ChuDel = new List<object>(chuDel); }
            return g;
        }
        void TraLai(GiuLai g)
        {
            if (g.T)
            {
                tSo.Text = g.So; tDtpl.Text = g.Dtpl; tDc.Text = g.DiaChi; tGc.Text = g.Gc; gMd.Rows.Clear(); mdDel.Clear(); mdDel.AddRange(g.MdDel);
                foreach (var v in g.Md) { var k = Convert.ToString(v[0], IC); if (!string.IsNullOrEmpty(k) && !cbKh.Items.Contains(k)) cbKh.Items.Add(k); gMd.Rows.Add(v); }
                if (gMd.Rows.Count > 0) gMd.CurrentCell = gMd.Rows[0].Cells["kh"]; SumMd(); NapMd();
            }
            if (g.C)
            {
                gChu.Rows.Clear(); chuDel.Clear(); chuDel.AddRange(g.ChuDel);
                napHt = true; try { cbHt.SelectedIndex = cbHt.Items.IndexOf(g.Ht ?? ""); } finally { napHt = false; }
                foreach (var n in g.Chu) ThemDong(n); if (gChu.Rows.Count > 0) gChu.CurrentCell = gChu.Rows[0].Cells[1]; NapChu();
            }
        }
        bool Ghi() { return Ghi(true, true); }
        // thua: ghi thông tin thửa + mục đích sử dụng; chu: ghi người sử dụng + hình thức sử dụng
        bool Ghi(bool thua, bool chu)
        {
            if (P == null) return false; gMd.EndEdit(); gChu.EndEdit(); var p = P;
            var G = Dc.DocLap && p.Gid == null ? null : Dc.G;   // kết nối độc lập: thửa trên bản đồ không có trong tệp thì không thêm vào tệp
            if (!thua && G != null && p.Gid == null)
            {
                if (MessageBox.Show(this, "Thửa chưa có trong tệp thuộc tính: phải ghi thông tin thửa trước.\r\nGhi cả thông tin thửa và người sử dụng?", Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return false;
                thua = true;
            }
            string so = thua ? tSo.Text.Trim() : p.So; if (thua && so != "" && !System.Text.RegularExpressions.Regex.IsMatch(so, @"^\d+$")) { MessageBox.Show(this, "Số thửa phải là số.", Text); return false; }
            if (thua && so != "" && so != p.So && ((!Dc.DocLap && Dc.P.Any(q => q != p && q.So == so)) || (G != null && RowsCung(p).Any(q => q != p.Row && q.So == so)))) { MessageBox.Show(this, "Số thửa " + so + " đã có trong tờ " + ToCua(p) + ".", Text); return false; }
            var mds = new List<object[]>();   // [id, code, dt, chinh, th, chung]
            foreach (DataGridViewRow r in gMd.Rows)
            {
                var code = (Convert.ToString(r.Cells["kh"].Value, IC) ?? "").Trim().ToUpperInvariant(); if (code == "") continue;
                mds.Add(new object[] { r.Cells["id"].Value, code, Num(r.Cells["dt"].Value), Convert.ToBoolean(r.Cells["chinh"].Value ?? false), Convert.ToString(r.Cells["th"].Value, IC), Convert.ToBoolean(r.Cells["chung"].Value ?? false) });
            }
            if (mds.Count > 0 && !mds.Any(x => (bool)x[3])) mds[0][3] = true;
            string ld = string.Join("+", mds.OrderByDescending(x => (bool)x[3]).Select(x => (string)x[1]).Distinct().ToArray());
            bool relabel = thua && (so != p.So || ld != p.Ld); var unknown = new List<string>();
            try
            {
                Dc.Hold++;
                try
                {
                    if (G != null && p.Gid == null)
                    {
                        if (so == "") { MessageBox.Show(this, "Nhập số thửa trước khi thêm thửa vào tệp thuộc tính.", Text); return false; }
                        var T = G.Cols["ThuaDat"]; var f = new Dictionary<string, object> { { "soThuTuThua", long.Parse(so) }, { "dienTich", Area() } };
                        long tv; if (long.TryParse(Dc.To, out tv)) f["soHieuToBanDo"] = tv;
                        if (!p.Virtual && T.Contains("TamX")) { f["TamX"] = p.Lp[0]; f["TamY"] = p.Lp[1]; }
                        if (!p.Virtual && T.Contains("geom")) f["geom"] = Gtp.Wkb(p.Rings);
                        p.Gid = G.NewParcel(f);
                    }
                    if (thua) { Dc.SetField(p, "so", so); Dc.SetField(p, "dtpl", tDtpl.Text); Dc.SetField(p, "diachi", tDc.Text); Dc.SetField(p, "ghichu", tGc.Text); }
                    if (G != null && p.Gid != null)
                    {
                        if (thua)
                        {
                            G.Update(p.Gid, "mucDichSuDungGhep", ld == "" ? null : ld.Replace("+", " + ")); p.Ld = ld;
                            foreach (var id in mdDel) G.DelMd(id); mdDel.Clear();
                            foreach (var x in mds)
                            {
                                long? loai = LoaiDatMa.Id((string)x[1]); if (!loai.HasValue && ((string)x[1]).StartsWith("#")) { long v; if (long.TryParse(((string)x[1]).Substring(1), out v)) loai = v; }
                                if (!loai.HasValue) unknown.Add((string)x[1]);
                                object id = x[0] != null && Convert.ToString(x[0], IC) != "" ? x[0] : G.AddMd(p.Gid, loai, (double?)x[2], (bool)x[3], so, ToCua(p), (string)x[1]);
                                if (x[0] != null && Convert.ToString(x[0], IC) != "")
                                {
                                    foreach (var c in new[] { "loaiMucDichSuDungId", "loaiMucDichSuDungKiemKeId" }) if (loai.HasValue) G.Set("DaMucDichSuDung", "daMucDichSuDungId", id, c, loai.Value);
                                    G.Set("DaMucDichSuDung", "daMucDichSuDungId", id, "dienTich", x[2]); G.Set("DaMucDichSuDung", "daMucDichSuDungId", id, "laMucDichChinh", (bool)x[3] ? 1L : 0L);
                                }
                                if (hasTh) G.Set("DaMucDichSuDung", "daMucDichSuDungId", id, "thoiHanSuDung", (string)x[4] ?? "");
                                if (hasChung) G.Set("DaMucDichSuDung", "daMucDichSuDungId", id, "suDungChung", (bool)x[5] ? 1L : 0L);
                            }
                        }
                        // người sử dụng đất (đủ trường căn cước) và hình thức sử dụng
                        if (chu) { ChuSD.Ghi(G, p.Gid, DsChu, chuDel); chuDel.Clear(); ChuSD.GhiHT(G, p.Gid, HinhThuc); }
                        // thửa luôn có loại đất chi tiết, mỗi chủ có đăng ký trên từng loại đất chi tiết (như tệp thuộc tính gốc)
                        G.EnsureMd(p.Gid); G.DongBoDangKy(p.Gid);
                    }
                    else
                    {
                        if (thua) Dc.SetField(p, "ld", ld);
                        if (chu) { var first = DsChu.FirstOrDefault(x => !x.Trong); Dc.SetField(p, "chu", first != null ? first.Ten : ""); ChuSD.GhiMay(so, soCu, HinhThuc, DsChu); }
                        else if (so != soCu) { string htc; var dsc = ChuSD.DocMay(soCu, out htc); if (dsc.Count > 0 || htc != "") ChuSD.GhiMay(so, soCu, htc, dsc); }   // đổi số thửa: chuyển theo người sử dụng đã lưu
                        soCu = so;
                    }
                }
                finally { Dc.Hold--; }
                string sv = "";
                if (G != null && G.Dirty) { if (G.ChangedOnDisk) throw new Exception("Tệp " + G.Name + " vừa bị chương trình khác sửa – không ghi đè."); G.Save(G.Path_); sv = "Đã ghi vào " + G.Name; Dc.GRows = G.Rows(); Dc.Link(); }
                bool ve = relabel && cLab.Checked && !p.Virtual; if (ve) Dc.DrawLabels(new[] { p }, true);
                Dc.Notify(); var giu = Giu(!thua, !chu); Show_(p); TraLai(giu);
                if (G != null) G.MaTam.Clear();
                var msg = (sv != "" ? sv : "Đã lưu trên máy") + (thua && chu ? "" : thua ? " thông tin thửa" : " người sử dụng đất") + " – thửa " + p.So + (ve ? Dc.NhanCuBao() : "") + (unknown.Count > 0 ? " · ký hiệu " + string.Join(", ", unknown.Distinct().ToArray()) + " chưa có mã số loại đất trong tệp: tạm ghi mã loại đất dùng nhiều nhất, kiểm tra lại mục đích sử dụng của thửa" : "");
                st.Text = msg; M.Msg(msg); return true;
            }
            catch (Exception ex) { Program.Log(ex); MessageBox.Show(this, "Không ghi được: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        }
    }
}
