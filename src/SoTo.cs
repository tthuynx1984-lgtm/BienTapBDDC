// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – CHUYỂN SỐ TỜ TRONG TỆP THUỘC TÍNH THEO TÊN TỜ BẢN ĐỒ. © Nguyễn Xuân Thủy
// Sau khi sắp xếp lại đơn vị hành chính, tờ bản đồ mang số tờ mới trong tên tệp (DC226.dgn; dc101(dc07).dgn – số trong ngoặc là số tờ cũ)
// còn tệp thuộc tính (.gtp) vẫn ghi số tờ cũ; thửa tạo mới lại mang số tờ mới nên một tệp lẫn hai số tờ. Chức năng này chuyển số tờ trong
// tệp thuộc tính về đúng số tờ theo tên – từng tờ (tệp đang kết nối) hoặc nhiều tờ một lúc (chọn nhiều tệp, cả thư mục):
//  • luôn đổi: số tờ của thửa đất, của mục đích sử dụng, của hạn chế sử dụng; số hiệu bản đồ của tệp;
//  • tuỳ chọn: số tờ trên trang sổ địa chính, sổ mục kê; ghi số tờ cũ vào cột số tờ cũ của thửa (cột còn trống);
//  • không đổi: số tờ cũ / gốc, bản sao lưu, lịch sử thửa, giấy chứng nhận đã cấp, hồ sơ quét, quá trình xử lý, biến động.
// Thửa mà đổi sang số tờ mới sẽ trùng số thửa đã có của tờ mới: giữ số tờ cũ, báo để xử lý tay. Mỗi tệp giữ bản gốc <tên>.gtp.bak.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace M8Pro
{
    // một tệp thuộc tính trong danh sách chuyển số tờ
    public class SoToTep
    {
        public string Path, ToMoi = "", ToTep = "", Dgn = "", Loi = "", KetQua = "", TuDau = "";   // TuDau: số tờ mới lấy từ đâu
        public Dictionary<string, int> Co = new Dictionary<string, int>();     // số tờ đang có trong bảng thửa → số thửa ("" = chưa ghi số tờ)
        public HashSet<string> Doi;                                            // các số tờ sẽ chuyển (null: mọi số tờ khác số tờ mới)
        public List<string> Trung = new List<string>();                        // số thửa sẽ trùng sau khi chuyển (giữ số tờ cũ)
        public int GiuCu;                                                      // số thửa giữ số tờ cũ vì trùng
        public bool Chon = true, DangMo;
        public string Ten { get { return System.IO.Path.GetFileName(Path); } }
        public IEnumerable<string> CanDoi { get { return Co.Keys.Where(k => k != ToMoi && (Doi == null || Doi.Contains(k))); } }
        public int SoThuaDoi { get { return CanDoi.Sum(k => Co[k]) - GiuCu; } }
        public bool CoViec { get { return Loi == "" && ToMoi != "" && (SoThuaDoi > 0 || (ToTep != "" && ToTep != ToMoi)); } }
    }

    public static class SoTo
    {
        // số tờ theo tên tệp: số cuối trong tên, bỏ phần trong ngoặc (số tờ cũ): DC226 → 226, dc101(dc07) → 101, Ban_do_to_12 → 12
        public static string TheoTen(string path)
        {
            var n = Regex.Replace(System.IO.Path.GetFileNameWithoutExtension(path ?? ""), @"[(\[{][^)\]}]*[)\]}]", " ");
            var m = Regex.Match(n, @"(\d+)\D*$"); if (!m.Success) return ""; var d = m.Groups[1].Value.TrimStart('0'); return d.Length > 6 ? "" : d == "" ? "0" : d;
        }
        // số tờ cũ ghi trong ngoặc của tên tệp: dc101(dc07) → 7; không có: ""
        public static string CuTrongTen(string path)
        {
            var m = Regex.Match(System.IO.Path.GetFileNameWithoutExtension(path ?? ""), @"[(\[{][^)\]}\d]*(\d+)[^)\]}]*[)\]}]"); if (!m.Success) return ""; var d = m.Groups[1].Value.TrimStart('0'); return d == "" ? "0" : d;
        }
        static string N(object v) { return Gtp.Norm(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)); }

        // đọc số tờ đang có trong tệp (không sửa gì); g: tệp đang mở (null: tự mở rồi đóng)
        public static SoToTep Doc(string path, Gtp g, string toMoi, string tuDau)
        {
            var f = new SoToTep { Path = path, ToMoi = toMoi ?? "", TuDau = tuDau ?? "" };
            var dgn = System.IO.Path.ChangeExtension(path, ".dgn"); if (File.Exists(dgn)) f.Dgn = System.IO.Path.GetFileName(dgn);
            bool mo = g == null;
            try
            {
                if (mo) g = Gtp.Open(path); else f.DangMo = true;
                PhanTich(g, f);
            }
            catch (Exception ex) { f.Loi = ex.Message; f.Chon = false; }
            finally { if (mo && g != null) g.Dispose(); }
            return f;
        }
        public static void PhanTich(Gtp g, SoToTep f)
        {
            f.Co.Clear(); f.Trung.Clear();
            if (!g.Has("ThuaDat", "soHieuToBanDo")) { f.Loi = "tệp không có bảng thửa đất có số tờ"; f.Chon = false; return; }
            foreach (var r in g.Query("select soHieuToBanDo as t, count(*) as n from ThuaDat group by soHieuToBanDo")) { var k = N(r["t"]); int c; f.Co.TryGetValue(k, out c); f.Co[k] = c + Convert.ToInt32(r["n"]); }
            if (g.Has("ThongTinMoRong", "nhomThongTin", "truong", "giaTri")) { var q = g.Query("select giaTri as v from ThongTinMoRong where nhomThongTin='TopoInfo' and truong='soHieuBanDo'"); if (q.Count > 0) f.ToTep = N(q[0]["v"]); }
            int giu; f.Trung = Trung(g, f, out giu); f.GiuCu = giu;
            if (!f.CoViec) f.Chon = false;
        }
        // số thửa sẽ trùng nếu chuyển: đã có trên tờ mới, hoặc trùng giữa các tờ cùng chuyển sang
        static List<string> Trung(Gtp g, SoToTep f, out int giu)
        {
            giu = 0; f.GiuCu = 0; var doi = new HashSet<string>(f.CanDoi); if (f.ToMoi == "" || doi.Count == 0 || !g.Has("ThuaDat", "soThuTuThua")) return new List<string>();
            var dem = new Dictionary<string, int>(); var ung = new List<string>();
            foreach (var r in g.Query("select soHieuToBanDo as t, soThuTuThua as s from ThuaDat"))
            {
                var t = N(r["t"]); var s = N(r["s"]); if (s == "" || (t != f.ToMoi && !doi.Contains(t))) continue;
                int c; dem.TryGetValue(s, out c); dem[s] = c + 1; if (t != f.ToMoi) ung.Add(s);
            }
            var o = dem.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList(); var h = new HashSet<string>(o); giu = ung.Count(h.Contains);
            return o.OrderBy(s => { int v; return int.TryParse(s, out v) ? v : int.MaxValue; }).ThenBy(s => s).ToList();
        }

        // bảng / cột số tờ hiện tại được chuyển; soSach: cả trang sổ địa chính, sổ mục kê
        static readonly string[][] LuonDoi = { new[] { "HanChe", "SoHieuTo" } };
        static readonly string[][] SoSach = { new[] { "TbTrangSoDiaChinh", "soHieuToBanDo" }, new[] { "DongSoDiaChinh", "soHieuToBanDo" }, new[] { "SoMucKe", "soHieuToBanDo" }, new[] { "ChiTietSoMucKe", "soHieuToBanDo" }, new[] { "TBSoMucKe", "soHieuToBanDo" }, new[] { "TBChiTietSoMucKe", "soHieuToBanDo" } };

        // chuyển trong tệp đang mở (chưa lưu); trả về số thửa đã chuyển, bao gồm các dòng của các bảng khác trong báo cáo
        public static int Chuyen(Gtp g, SoToTep f, bool soSach, bool ghiCu, out string bao)
        {
            bao = ""; PhanTich(g, f); if (f.Loi != "") throw new Exception(f.Loi);
            if (f.ToMoi == "") throw new Exception("chưa có số tờ mới");
            var doi = new HashSet<string>(f.CanDoi); var trung = new HashSet<string>(f.Trung);
            if (doi.Count == 0 && f.ToTep == f.ToMoi) { bao = "đã đúng số tờ " + f.ToMoi + " – không phải chuyển"; return 0; }
            bool coSo = g.Has("ThuaDat", "soThuTuThua"), coCu = ghiCu && g.Has("ThuaDat", "soHieuToBanDoCu");
            var ids = new List<object>(); var cuCua = new Dictionary<string, string>(); int giu = 0, nCu = 0;
            foreach (var r in g.Query("select rowid as rid, soHieuToBanDo as t" + (coSo ? ", soThuTuThua as s" : "") + (coCu ? ", soHieuToBanDoCu as c" : "") + (g.Has("ThuaDat", "thuaDatId") ? ", thuaDatId as id" : "") + " from ThuaDat"))
            {
                var t = N(r["t"]); if (!doi.Contains(t)) continue;
                if (coSo && trung.Contains(N(r["s"]))) { giu++; continue; }
                ids.Add(r["rid"]); if (r.ContainsKey("id")) cuCua[N(r["id"])] = t;
                if (coCu && t != "" && N(r["c"]) == "") { g.Db.Exec("update ThuaDat set soHieuToBanDoCu=? where rowid=?", t, r["rid"]); nCu++; }
            }
            object moi = GiaTri(g, "ThuaDat", "soHieuToBanDo", f.ToMoi);
            foreach (var rid in ids) g.Db.Exec("update ThuaDat set soHieuToBanDo=? where rowid=?", moi, rid);
            var them = new List<string>(); int n;
            // mục đích sử dụng của thửa: theo đúng các thửa vừa chuyển (cột số tờ có ghi)
            if (g.Has("DaMucDichSuDung", "soHieuToBanDo", "thuaDatId"))
            {
                n = 0; object v = GiaTri(g, "DaMucDichSuDung", "soHieuToBanDo", f.ToMoi);
                foreach (var r in g.Query("select rowid as rid, thuaDatId as id, soHieuToBanDo as t from DaMucDichSuDung"))
                { string t0; var t = N(r["t"]); if (t == "" || !cuCua.TryGetValue(N(r["id"]), out t0)) continue; g.Db.Exec("update DaMucDichSuDung set soHieuToBanDo=? where rowid=?", v, r["rid"]); n++; }
                if (n > 0) them.Add(n + " dòng mục đích sử dụng");
            }
            var bang = LuonDoi.Concat(soSach ? SoSach : new string[0][]).ToList();
            foreach (var b in bang)
            {
                if (!g.Has(b[0], b[1])) continue; n = 0; object v = GiaTri(g, b[0], b[1], f.ToMoi); bool coThua = g.Has(b[0], "soThuTuThua");
                foreach (var r in g.Query("select rowid as rid, " + b[1] + " as t" + (coThua ? ", soThuTuThua as s" : "") + " from " + b[0]))
                {
                    if (!doi.Contains(N(r["t"])) || N(r["t"]) == "") continue; if (coThua && trung.Contains(N(r["s"]))) continue;
                    g.Db.Exec("update " + b[0] + " set " + b[1] + "=? where rowid=?", v, r["rid"]); n++;
                }
                if (n > 0) them.Add(n + " dòng " + TenBang(b[0]));
            }
            // số hiệu bản đồ của tệp
            if (g.Has("ThongTinMoRong", "nhomThongTin", "truong", "giaTri") && f.ToTep != "" && f.ToTep != f.ToMoi)
            {
                g.Db.Exec("update ThongTinMoRong set giaTri=? where nhomThongTin='TopoInfo' and truong='soHieuBanDo'", f.ToMoi);
                them.Add("số hiệu bản đồ của tệp " + f.ToTep + " → " + f.ToMoi);
            }
            if (ids.Count > 0 || them.Count > 0 || nCu > 0) g.Dirty = true;
            bao = ids.Count + " thửa " + string.Join(", ", doi.Select(t => (t == "" ? "(chưa ghi số tờ)" : "tờ " + t)).ToArray()) + " → tờ " + f.ToMoi + (them.Count > 0 ? " · " + string.Join(", ", them.ToArray()) : "") +
                (nCu > 0 ? " · số tờ cũ ghi vào cột số tờ cũ của " + nCu + " thửa" : "") + (giu > 0 ? " · ⚠ " + giu + " thửa giữ số tờ cũ vì trùng số thửa đã có của tờ " + f.ToMoi + ": " + string.Join(", ", f.Trung.Take(12).ToArray()) + (f.Trung.Count > 12 ? "…" : "") : "");
            return ids.Count;
        }
        // giá trị số tờ đúng kiểu cột (cột số nguyên: ghi số)
        static object GiaTri(Gtp g, string bang, string cot, string to)
        {
            Dictionary<string, string> T; string ty; long v;
            if (g.Types.TryGetValue(bang, out T) && T.TryGetValue(cot, out ty) && ty.Contains("int") && long.TryParse(to, out v)) return v;
            return to;
        }
        static string TenBang(string b)
        {
            switch (b)
            {
                case "TbTrangSoDiaChinh": return "trang sổ địa chính";
                case "DongSoDiaChinh": return "dòng sổ địa chính";
                case "SoMucKe": case "TBSoMucKe": return "sổ mục kê";
                case "ChiTietSoMucKe": case "TBChiTietSoMucKe": return "chi tiết sổ mục kê";
                case "HanChe": return "hạn chế sử dụng";
                default: return b;
            }
        }

        // chuyển một tệp trên đĩa (hoặc tệp đang kết nối): ghi lại tệp, giữ bản gốc .bak
        public static string ChuyenTep(SoToTep f, bool soSach, bool ghiCu)
        {
            bool dangKetNoi = Dc.G != null && string.Equals(System.IO.Path.GetFullPath(Dc.G.Path_), System.IO.Path.GetFullPath(f.Path), StringComparison.OrdinalIgnoreCase);
            var g = dangKetNoi ? Dc.G : Gtp.Open(f.Path); string bao;
            try
            {
                if (g.ChangedOnDisk) throw new Exception("tệp vừa bị chương trình khác sửa – kết nối lại tệp rồi chuyển");
                if (dangKetNoi) { try { Dc.Commit(); } catch (Exception ex) { Program.Log(ex); } }
                g.Db.Exec("savepoint m8soto");
                int n;
                try { n = Chuyen(g, f, soSach, ghiCu, out bao); g.Db.Exec("release m8soto"); }
                catch { try { g.Db.Exec("rollback to m8soto"); g.Db.Exec("release m8soto"); } catch { } throw; }
                bool luu = g.Dirty; if (luu) g.Save(g.Path_);
                if (dangKetNoi)
                {
                    Dc.GRows = g.Rows();
                    if (Dc.To != f.ToMoi && TheoTen(Dc.MapName) == f.ToMoi) Dc.SetTo(f.ToMoi);     // tờ đang mở: dùng số tờ mới (chữ “Tờ số” cũ trên bản đồ không còn đúng)
                    Dc.Link(); Dc.Notify();
                }
                PhanTich(g, f);
                return bao + (luu ? " · đã lưu " + g.Name + " (bản gốc " + g.Name + ".bak)" : "");
            }
            finally { if (!dangKetNoi) g.Dispose(); }
        }
        // các tệp .gtp trong thư mục và các thư mục con một cấp
        public static List<string> TrongThuMuc(string dir)
        {
            var o = new List<string>();
            try { o.AddRange(Directory.GetFiles(dir, "*.gtp")); foreach (var d in Directory.GetDirectories(dir)) try { o.AddRange(Directory.GetFiles(d, "*.gtp")); } catch { } } catch (Exception ex) { Program.Log(ex); }
            return o.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        }
        // số tờ mới của một tệp: tệp đang kết nối theo tờ bản đồ đang mở; tệp khác theo tên tệp, tên tệp không có số mà thư mục chỉ có một tờ bản đồ thì theo tờ đó
        public static string ToCua(string path, out string tuDau)
        {
            if (Dc.G != null && string.Equals(System.IO.Path.GetFullPath(Dc.G.Path_), System.IO.Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(Dc.MapName) && TheoTen(Dc.MapName) != "")
            { tuDau = "tên tờ bản đồ đang mở " + System.IO.Path.GetFileName(Dc.MapName); return TheoTen(Dc.MapName); }
            var t = TheoTen(path); if (t != "") { tuDau = "tên tệp"; return t; }
            try
            {
                var ds = Directory.GetFiles(System.IO.Path.GetDirectoryName(path), "*.dgn");
                if (ds.Length == 1 && TheoTen(ds[0]) != "") { tuDau = "tờ bản đồ " + System.IO.Path.GetFileName(ds[0]); return TheoTen(ds[0]); }
            }
            catch { }
            tuDau = ""; return "";
        }
    }

    // ================= CỬA SỔ CHUYỂN SỐ TỜ (một tệp hoặc nhiều tệp) =================
    public class SoToForm : Form
    {
        ListView L = new ListView { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false, GridLines = true };
        CheckBox cSo = new OTich { Text = "Cả số tờ trên trang sổ địa chính, sổ mục kê của thửa", Checked = true, AutoSize = true };
        CheckBox cCu = new OTich { Text = "Ghi số tờ cũ vào cột số tờ cũ của thửa (khi cột còn trống)", Checked = true, AutoSize = true };
        Label st = new Label { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(6, 2, 6, 2) };
        List<SoToTep> ds = new List<SoToTep>(); bool nap;
        public bool DaChuyen;
        public SoToForm(IEnumerable<string> tep)
        {
            Text = "Chuyển số tờ trong tệp thuộc tính theo tên tờ bản đồ"; Font = new Font("Segoe UI", 9f); FormBorderStyle = FormBorderStyle.Sizable; MinimizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; TopMost = true;
            Width = 1000; Height = 600; MinimumSize = new Size(760, 420); GiaoDien.KhongCat(this);
            var top = new Label { Dock = DockStyle.Top, Height = 64, Padding = new Padding(6, 6, 6, 0), Text =
                "Đổi số tờ ghi trong tệp thuộc tính (.gtp) về đúng số tờ theo tên tờ bản đồ: DC226 → tờ 226; dc101(dc07) → tờ 101 (số trong ngoặc là số tờ cũ). " +
                "Đổi số tờ của thửa đất, mục đích sử dụng, hạn chế sử dụng và số hiệu bản đồ của tệp; giữ nguyên số tờ cũ, bản sao lưu, lịch sử thửa, giấy chứng nhận đã cấp. " +
                "Thửa sẽ trùng số thửa đã có của tờ mới thì giữ số tờ cũ và được báo. Mỗi tệp giữ bản gốc <tên>.gtp.bak. Nên đóng tệp ở phần mềm khác trước khi chuyển." };
            L.Columns.Add("Tệp thuộc tính", 230); L.Columns.Add("Số tờ mới", 80); L.Columns.Add("Số tờ đang có trong tệp (số thửa)", 230); L.Columns.Add("Sẽ chuyển", 170); L.Columns.Add("Ghi chú", 360);
            var bt = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(4), FlowDirection = FlowDirection.LeftToRight };
            Func<string, int, Button> nut = (s, w) => { var b = GiaoDien.Nut(s, w, false); bt.Controls.Add(b); return b; };
            var bTep = nut("Thêm tệp…", 100); var bThu = nut("Thêm thư mục…", 120); var bSua = nut("Sửa số tờ…", 100); var bBo = nut("Bỏ khỏi danh sách", 130);
            var op = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(6, 4, 4, 0) }; op.Controls.Add(cSo); op.Controls.Add(cCu);
            var bp = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 42, Padding = new Padding(4) };
            var dong = GiaoDien.Nut("Đóng", 90, false); dong.DialogResult = DialogResult.Cancel; var ok = GiaoDien.Nut("⇄ Chuyển số tờ", 140, true);
            bp.Controls.Add(dong); bp.Controls.Add(ok); CancelButton = dong;
            Controls.Add(L); Controls.Add(top); Controls.Add(bt); Controls.Add(op); Controls.Add(st); Controls.Add(bp);
            bTep.Click += (s, e) => { using (var d = new OpenFileDialog { Filter = "Tệp thuộc tính (*.gtp)|*.gtp", Multiselect = true, Title = "Chọn một hoặc nhiều tệp thuộc tính" }) if (d.ShowDialog(this) == DialogResult.OK) Them(d.FileNames); };
            bThu.Click += (s, e) => { using (var d = new FolderBrowserDialog { Description = "Chọn thư mục có các tệp thuộc tính (.gtp) – lấy cả các thư mục con một cấp" }) if (d.ShowDialog(this) == DialogResult.OK) { var f = SoTo.TrongThuMuc(d.SelectedPath); if (f.Count == 0) Msg("Không có tệp .gtp nào trong thư mục đã chọn"); else Them(f); } };
            bBo.Click += (s, e) => { foreach (ListViewItem it in L.SelectedItems) ds.Remove((SoToTep)it.Tag); Ve(); };
            bSua.Click += (s, e) => Sua();
            L.DoubleClick += (s, e) => Sua();
            L.SelectedIndexChanged += (s, e) => { if (L.SelectedItems.Count == 1) Msg(L.SelectedItems[0].Text + ": " + L.SelectedItems[0].SubItems[4].Text); };
            L.ItemChecked += (s, e) => { if (nap) return; var f = (SoToTep)e.Item.Tag; f.Chon = e.Item.Checked; Tong(); };
            ok.Click += (s, e) => Chay();
            Them(tep ?? new string[0]);
        }
        void Msg(string s) { st.Text = s; }
        void Them(IEnumerable<string> tep)
        {
            var c0 = Cursor; Cursor = Cursors.WaitCursor;
            try
            {
                foreach (var p in tep)
                {
                    if (ds.Any(f => string.Equals(f.Path, p, StringComparison.OrdinalIgnoreCase))) continue;
                    Msg("Đang đọc " + Path.GetFileName(p) + "…"); Application.DoEvents();
                    string tu; var to = SoTo.ToCua(p, out tu);
                    bool dk = Dc.G != null && string.Equals(Path.GetFullPath(Dc.G.Path_), Path.GetFullPath(p), StringComparison.OrdinalIgnoreCase);
                    ds.Add(SoTo.Doc(p, dk ? Dc.G : null, to, tu));
                }
            }
            finally { Cursor = c0; }
            Ve();
        }
        void Ve()
        {
            nap = true; L.BeginUpdate(); L.Items.Clear();
            foreach (var f in ds)
            {
                var co = string.Join("; ", f.Co.OrderBy(kv => { int v; return int.TryParse(kv.Key, out v) ? v : -1; }).Select(kv => (kv.Key == "" ? "(chưa ghi)" : kv.Key) + " · " + kv.Value).ToArray());
                var doi = f.CanDoi.ToList();
                var se = f.Loi != "" ? "" : f.ToMoi == "" ? "chưa có số tờ mới" : doi.Count == 0 ? (f.ToTep != "" && f.ToTep != f.ToMoi ? "số hiệu bản đồ của tệp" : "đã đúng tờ " + f.ToMoi) : string.Join(", ", doi.Select(t => t == "" ? "(chưa ghi)" : t).ToArray()) + " → " + f.ToMoi + " (" + f.SoThuaDoi + " thửa" + (f.GiuCu > 0 ? "; " + f.GiuCu + " giữ tờ cũ" : "") + ")";
                var gc = f.KetQua != "" ? f.KetQua : f.Loi != "" ? "⚠ " + f.Loi :
                    (f.DangMo ? "đang kết nối · " : "") + (f.TuDau != "" ? "số tờ theo " + f.TuDau : "tên tệp không có số tờ – bấm Sửa số tờ") + (f.Dgn != "" && !f.DangMo ? " · có " + f.Dgn : "") +
                    (f.ToTep != "" ? " · số hiệu bản đồ trong tệp: " + f.ToTep : "") + (f.Trung.Count > 0 ? " · ⚠ " + f.Trung.Count + " số thửa sẽ trùng (giữ số tờ cũ): " + string.Join(", ", f.Trung.Take(8).ToArray()) + (f.Trung.Count > 8 ? "…" : "") : "");
                var it = new ListViewItem(new[] { f.Ten, f.ToMoi, co, se, gc }) { Tag = f, Checked = f.Chon && f.CoViec, ToolTipText = f.Path };
                if (f.Loi != "") it.ForeColor = Color.Firebrick; else if (f.KetQua != "") it.ForeColor = Color.DarkGreen;
                L.Items.Add(it);
            }
            L.EndUpdate(); GiaoDien.VuaCot(L, 520); nap = false; Tong();
        }
        void Tong()
        {
            var c = ds.Where(f => f.Chon && f.CoViec).ToList();
            Msg(ds.Count == 0 ? "Thêm tệp thuộc tính cần chuyển số tờ (một tệp, nhiều tệp hoặc cả thư mục)." : c.Count + " / " + ds.Count + " tệp được chọn · " + c.Sum(f => f.SoThuaDoi) + " thửa sẽ chuyển sang số tờ mới");
        }
        void Sua()
        {
            if (L.SelectedItems.Count == 0) { Msg("Chọn một tệp trong danh sách rồi bấm Sửa số tờ"); return; }
            var f = (SoToTep)L.SelectedItems[0].Tag; if (f.Loi != "") return;
            var fs = new List<F> { new F("moi", "Số tờ mới (theo tên tờ bản đồ)", f.ToMoi) };
            var cu = SoTo.CuTrongTen(f.Path); if (cu != "") fs.Add(new F("_c", "Tên tệp ghi số tờ cũ: " + cu, "") { Info = true });
            var keys = f.Co.Keys.OrderBy(k => { int v; return int.TryParse(k, out v) ? v : -1; }).ToList();
            for (int i = 0; i < keys.Count; i++) fs.Add(new F("k" + i, "Chuyển " + (keys[i] == "" ? "các thửa chưa ghi số tờ" : "tờ " + keys[i]) + " (" + f.Co[keys[i]] + " thửa)", f.Doi == null || f.Doi.Contains(keys[i]) ? "1" : "0") { Check = true });
            var r = Ask.Show(this, "Số tờ của " + f.Ten, fs.ToArray()); if (r == null) return;
            var moi = Gtp.Norm(r["moi"]); if (moi != "" && !Regex.IsMatch(moi, @"^[0-9A-Za-z.\-]{1,12}$")) { MessageBox.Show(this, "Số tờ mới không hợp lệ.", Text); return; }
            f.ToMoi = moi; f.TuDau = moi != "" ? "số tờ đã sửa" : ""; f.Doi = new HashSet<string>(); for (int i = 0; i < keys.Count; i++) if (r["k" + i] == "1") f.Doi.Add(keys[i]);
            f.KetQua = ""; f.Chon = true;
            try { var g = f.DangMo ? Dc.G : Gtp.Open(f.Path); try { SoTo.PhanTich(g, f); } finally { if (!f.DangMo) g.Dispose(); } } catch (Exception ex) { f.Loi = ex.Message; }
            Ve();
        }
        void Chay()
        {
            var c = ds.Where(f => f.Chon && f.CoViec).ToList(); if (c.Count == 0) { Msg("Chưa chọn tệp nào cần chuyển số tờ"); return; }
            if (MessageBox.Show(this, "Chuyển số tờ trong " + c.Count + " tệp thuộc tính (" + c.Sum(f => f.SoThuaDoi) + " thửa)?\r\n\r\nMỗi tệp giữ bản gốc <tên>.gtp.bak.", Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            var c0 = Cursor; Cursor = Cursors.WaitCursor; int ok = 0, loi = 0;
            try
            {
                foreach (var f in c)
                {
                    Msg("Đang chuyển " + f.Ten + "…"); Application.DoEvents();
                    try { f.KetQua = "✓ " + SoTo.ChuyenTep(f, cSo.Checked, cCu.Checked); ok++; DaChuyen = true; f.Chon = false; }
                    catch (Exception ex) { Program.Log(ex); f.KetQua = "⚠ chưa chuyển: " + ex.Message; loi++; }
                }
            }
            finally { Cursor = c0; }
            Ve(); Msg("Đã chuyển " + ok + " tệp" + (loi > 0 ? ", " + loi + " tệp chưa chuyển được (xem ghi chú)" : "") + ".");
        }
    }
}
