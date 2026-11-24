// M8 Pro Địa chính – xem và sửa TOÀN BỘ thông tin một thửa trong tệp thuộc tính (.gtp). Sửa ô nào lưu thẳng vào tệp ô đó. © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace M8Pro
{
    public class GtpForm : FormBase
    {
        static readonly Dictionary<string, string> TEN = new Dictionary<string, string> {
            { "soHieuToBanDo", "Số tờ bản đồ" }, { "soThuTuThua", "Số thửa" }, { "maThua", "Mã thửa" }, { "dienTich", "Diện tích (m²)" }, { "dienTichPhapLy", "Diện tích pháp lý (m²)" }, { "dienTichBanDo", "Diện tích bản đồ (m²)" },
            { "mucDichSuDungGhep", "Mục đích sử dụng (ký hiệu)" }, { "diaChi", "Địa chỉ thửa đất" }, { "ghiChu", "Ghi chú" }, { "soHieuToBanDoCu", "Số tờ cũ" }, { "soThuTuThuaCu", "Số thửa cũ" }, { "dienTichCu", "Diện tích cũ" },
            { "loaiDatCu", "Loại đất cũ" }, { "mucDichSuDungCu", "Mục đích sử dụng cũ" }, { "nguonGocSuDungCu", "Nguồn gốc sử dụng cũ" }, { "soPhatHanhCu", "Số phát hành GCN cũ" }, { "soVaoSoCu", "Số vào sổ cũ" }, { "ngayCapGiayCu", "Ngày cấp giấy cũ" },
            { "TamX", "Toạ độ tâm – Đông (Y)" }, { "TamY", "Toạ độ tâm – Bắc (X)" }, { "nguonGocHinhThanh", "Nguồn gốc hình thành" }, { "thoiDiemHinhThanh", "Thời điểm hình thành" }, { "tranhChap", "Tranh chấp" },
            { "loaiGiayToHienCo", "Loại giấy tờ hiện có" }, { "dienTichTrenGiayTo", "Diện tích trên giấy tờ" }, { "loaiDatTrenGiayTo", "Loại đất trên giấy tờ" }, { "giayToNguonGocSuDung", "Giấy tờ nguồn gốc sử dụng" },
            { "hanCheSuDung", "Hạn chế sử dụng" }, { "nghiaVuTaiChinh", "Nghĩa vụ tài chính" }, { "dienTichTrongQuyHoach", "Diện tích trong quy hoạch" }, { "dienTichNgoaiQuyHoach", "Diện tích ngoài quy hoạch" },
            { "trangThaiDangKy", "Trạng thái đăng ký" }, { "maDinhDanh", "Mã định danh" }, { "taiLieuDoDac", "Tài liệu đo đạc" }, { "ngayVaoSoDiaChinh", "Ngày vào sổ địa chính" }, { "tinhHinhThayDoiRanhGioi", "Tình hình thay đổi ranh giới" },
            { "hoTen", "Họ tên chủ" }, { "namSinh", "Năm sinh" }, { "ngaySinh", "Ngày sinh" }, { "gioiTinh", "Giới tính (1 nam, 0 nữ)" }, { "hoTen2", "Họ tên vợ/chồng" }, { "namSinh2", "Năm sinh vợ/chồng" }, { "gioiTinh2", "Giới tính vợ/chồng" },
            { "diaChi2", "Địa chỉ vợ/chồng" }, { "soDienThoai", "Điện thoại" }, { "soDiDong", "Di động" }, { "maSoThue", "Mã số thuế" }, { "maSoThue2", "Mã số thuế vợ/chồng" }, { "hoGiaDinh", "Hộ gia đình (1/0)" }, { "daChet", "Đã chết (1/0)" },
            { "soNha", "Số nhà" }, { "tenDuong", "Tên đường" }, { "tenToDanPho", "Tổ dân phố / thôn" }, { "loaiMucDichSuDungId", "Mã số loại đất" }, { "laMucDichChinh", "Mục đích chính (1/0)" }, { "thoiHanSuDung", "Thời hạn sử dụng" } };
        static readonly HashSet<string> AN = new HashSet<string> { "geom", "uid", "thuaDatFTS", "nguoiFTS", "saoLuuId", "trangThai" };
        GRow R; Action after; CheckBox all = new OTich { Text = "Hiện cả trường chưa có dữ liệu", AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(6, 2, 0, 2) };
        TabControl tabs = new TabControl { Dock = DockStyle.Fill }; Label st = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.DimGray, Padding = new Padding(6, 4, 0, 0) };
        public GtpForm(GRow r, Action onChange)
        {
            R = r; after = onChange; Text = "Thửa " + r.So + " – tờ " + r.To + " – " + (Dc.G != null ? Dc.G.Name : ""); Width = 620; Height = 640; GiaoDien.KhongCat(this); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9f); ShowInTaskbar = false;
            Controls.Add(tabs); Controls.Add(all); Controls.Add(st); all.CheckedChanged += (s, e) => Fill(); Fill();
        }
        void Fill()
        {
            tabs.TabPages.Clear(); var G = Dc.G; if (G == null) return;
            tabs.TabPages.Add(Page("Thửa đất", "ThuaDat", "thuaDatId", R.Id));
            int k = 0; foreach (var nid in R.OwnerIds) tabs.TabPages.Add(Page(R.OwnerIds.Count > 1 ? "Chủ sử dụng " + (++k) : "Chủ sử dụng", "Nguoi", "nguoiId", nid));
            foreach (var m in R.Mds.OrderByDescending(x => x.Chinh)) tabs.TabPages.Add(Page("Mục đích " + (m.Code ?? "#" + m.Loai), "DaMucDichSuDung", "daMucDichSuDungId", m.Id));
            st.Text = "Sửa ô Giá trị rồi Enter – " + (Cfg.I.TuLuu ? "lưu thẳng vào " + G.Name : "nhớ bấm Lưu tệp thuộc tính");
        }
        TabPage Page(string title, string table, string key, object id)
        {
            var pg = new TabPage(title); var G = Dc.G;
            var g = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, BackgroundColor = Color.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.CellSelect, EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2 };
            g.Columns.Add("t", "Trường"); g.Columns.Add("v", "Giá trị"); g.Columns.Add("c", "Tên cột"); g.Columns[0].ReadOnly = true; g.Columns[2].ReadOnly = true; g.Columns[0].FillWeight = 38; g.Columns[1].FillWeight = 50; g.Columns[2].FillWeight = 22; g.Columns[2].DefaultCellStyle.ForeColor = Color.Gray;
            var row = G.Query("select * from " + Sqlite.Q(table) + " where " + Sqlite.Q(key) + "=?", id).FirstOrDefault();
            if (row == null) { pg.Controls.Add(new Label { Text = "Không có dữ liệu", Dock = DockStyle.Fill }); return pg; }
            var items = row.Where(kv => !AN.Contains(kv.Key) && kv.Key != key && !(kv.Value is byte[])).Select(kv => new { k = kv.Key, v = Show(kv.Value) }).ToList();
            foreach (var it in items.Where(x => all.Checked || x.v != "").OrderBy(x => TEN.ContainsKey(x.k) ? 0 : 1).ThenBy(x => x.v == "" ? 1 : 0))
                g.Rows.Add(TEN.ContainsKey(it.k) ? TEN[it.k] : it.k, it.v, it.k);
            g.CellEndEdit += (s, e) =>
            {
                var col = Convert.ToString(g.Rows[e.RowIndex].Cells[2].Value); var v = Convert.ToString(g.Rows[e.RowIndex].Cells[1].Value) ?? "";
                try
                {
                    G.Set(table, key, id, col, v);
                    // giữ bảng tổng hợp đồng bộ
                    if (table == "ThuaDat") { if (col == "soThuTuThua") R.So = Gtp.Norm(v); else if (col == "diaChi") R.DiaChi = v; else if (col == "ghiChu") R.GhiChu = v; else if (col == "dienTich") R.Dt = N(v); else if (col == "dienTichPhapLy") R.Dtpl = N(v); else if (col == "mucDichSuDungGhep") { R.Ghep = v.Replace(" ", ""); Gtp.Refresh(R); } else if (col == "soHieuToBanDo") R.To = Gtp.Norm(v); }
                    if (table == "Nguoi" && R.OwnerIds.Count > 0 && Equals(Convert.ToString(id), Convert.ToString(R.OwnerIds[0]))) { if (col == "hoTen") R.Chu = v; else if (col == "diaChi") R.ChuDc = v; else if (col == "namSinh") R.NamSinh = v; }
                    if (table == "DaMucDichSuDung") { var m = R.Mds.FirstOrDefault(x => Convert.ToString(x.Id) == Convert.ToString(id)); if (m != null) { if (col == "dienTich") m.Dt = N(v); if (col.StartsWith("loaiMucDichSuDung")) { long l; if (long.TryParse(v, out l)) m.Loai = l; Gtp.Refresh(R); } } }
                    foreach (var p in Dc.P.Where(p => p.Row == R)) { p.So = R.So; p.DiaChi = R.DiaChi; p.GhiChu = R.GhiChu; p.Dtpl = R.Dtpl; p.Chu = R.Chu; if (R.Kh != "") p.Ld = R.Kh; }
                    var sv = Dc.Commit(); st.Text = "Đã ghi “" + Convert.ToString(g.Rows[e.RowIndex].Cells[0].Value) + "” = " + v + (sv != "" ? " – " + sv : " – nhớ bấm Lưu tệp thuộc tính");
                    if (after != null) after();
                }
                catch (Exception ex) { st.Text = "Lỗi: " + ex.Message; Program.Log(ex); }
            };
            pg.Controls.Add(g); return pg;
        }
        static double? N(string v) { double d; return double.TryParse((v ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) ? d : (double?)null; }
        static string Show(object v)
        {
            if (v == null) return ""; if (v is double) return ((double)v).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var s = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture).Trim(); return s.Trim('\'');
        }
    }
}
