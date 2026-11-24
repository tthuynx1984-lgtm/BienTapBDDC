// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – GỘP CƠ SỞ DỮ LIỆU BẢN ĐỒ VÀ THUỘC TÍNH TOÀN XÃ. © Nguyễn Xuân Thủy
// Gộp các tờ bản đồ (.dgn) và tệp thuộc tính (.gtp) của cả xã – chọn tệp ở nhiều thư mục – thành một bản đồ và một tệp thuộc tính toàn xã.
// Tệp gốc chỉ đọc, không sửa. Thuộc tính: tệp đầu tiên làm nền (giữ cấu trúc, danh mục, cách mã hoá như tệp gốc), các tệp sau chép thêm vào:
//  • khoá chính số của bảng dữ liệu được dời (cộng thêm) để không trùng giữa các tờ; cột trỏ tới khoá đó (cùng tên khoá chính: thuaDatId, nguoiId…,
//    cả mã thửa cũ thuaDatCuId) dời theo; giá trị 0 / trống giữ nguyên;
//  • bảng danh mục (không trỏ tới, không được trỏ tới: DM75_…, thông tin mở rộng của tệp) chỉ thêm dòng mới, dòng trùng mã giữ như tệp nền;
//  • liên kết thửa – đường ranh, nhãn – phần tử bản đồ (ThuaDatTopo, NhanDiaChinh) đổi theo mã phần tử mới của bản đồ gộp; không gộp bản đồ thì bỏ
//    (mã phần tử của từng tờ không còn đúng – tạo thửa / vẽ nhãn lại trên bản đồ gộp sẽ ghi lại);
//  • tuỳ chọn đặt số tờ theo tên tờ bản đồ (DC12 → tờ 12) ngay trong tệp gộp; báo thửa trùng số tờ + số thửa.
// Bản đồ (cần đang kết nối bản vẽ): tờ đầu chép nguyên tệp làm nền, các tờ sau chép từng phần tử (ModelReference.CopyElement) vào, nhóm phần tử
// (graphic group) đánh lại theo từng tờ; có thể bỏ một số lớp (khung, lưới…).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using BCOM = Bentley.Interop.MicroStationDGN;

namespace M8Pro
{
    // một tờ trong danh sách gộp: bản đồ và / hoặc tệp thuộc tính cùng tên
    public class GopTo
    {
        public string Dgn = "", Gtp = "", To = "", Loi = "", Ghi = "", KetQua = "", Ma = "";
        public int SoThua = -1; public bool Chon = true, TuBo;   // TuBo: đã tự bỏ chọn một lần (tệp giống hệt tệp khác) – người dùng tích lại thì thôi
        public Dictionary<string, int> ToTrong = new Dictionary<string, int>();   // số tờ ghi trong bảng thửa → số thửa
        public string Ten { get { return Path.GetFileNameWithoutExtension(Dgn != "" ? Dgn : Gtp); } }
        public string ThuMuc { get { return Path.GetDirectoryName(Dgn != "" ? Dgn : Gtp); } }
    }
    public class GopKq { public int To, Thua, Nguoi, PhanTu, LienKet, BoLienKet; public List<string> Trung = new List<string>(), Bao = new List<string>(); public string Tep = ""; }

    public static class Gop
    {
        // ================= DANH SÁCH TỆP =================
        // các tệp .dgn, .gtp trong thư mục và mọi thư mục con
        public static List<string> TrongThuMuc(string dir)
        {
            var o = new List<string>();
            try
            {
                foreach (var f in Directory.GetFiles(dir)) { var e = Path.GetExtension(f).ToLowerInvariant(); if (e == ".dgn" || e == ".gtp") o.Add(f); }
                foreach (var d in Directory.GetDirectories(dir)) o.AddRange(TrongThuMuc(d));
            }
            catch (Exception ex) { Program.Log(ex); }
            return o;
        }
        // thêm tệp vào danh sách: bản đồ và tệp thuộc tính cùng tên ghép vào một dòng (ưu tiên cùng thư mục); trả về số tệp đã thêm
        public static int Them(List<GopTo> ds, IEnumerable<string> tep)
        {
            int n = 0;
            foreach (var p0 in tep)
            {
                string p; try { p = Path.GetFullPath(p0); } catch { continue; }
                var ext = Path.GetExtension(p).ToLowerInvariant(); if (ext != ".dgn" && ext != ".gtp") continue;
                if (ds.Any(t => Cung(t.Dgn, p) || Cung(t.Gtp, p))) continue;
                bool dgn = ext == ".dgn"; var ten = Path.GetFileNameWithoutExtension(p); var dir = Path.GetDirectoryName(p);
                Func<GopTo, bool> trong = t => dgn ? t.Dgn == "" && t.Gtp != "" : t.Gtp == "" && t.Dgn != "";
                var cung = ds.Where(t => trong(t) && string.Equals(t.Ten, ten, StringComparison.OrdinalIgnoreCase)).ToList();
                var r = cung.FirstOrDefault(t => string.Equals(t.ThuMuc, dir, StringComparison.OrdinalIgnoreCase)) ?? (cung.Count == 1 ? cung[0] : null);
                if (r == null) { r = new GopTo(); ds.Add(r); }
                if (dgn) r.Dgn = p; else { r.Gtp = p; Doc(r); }
                if (r.To == "") r.To = SoTo.TheoTen(p);
                n++;
            }
            KiemTra(ds); return n;
        }
        static bool Cung(string a, string b) { return a != "" && string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
        // đọc nhanh tệp thuộc tính: số thửa, các số tờ đang ghi, mã nội dung (nhận ra tệp giống hệt nhau)
        public static void Doc(GopTo t)
        {
            t.Loi = ""; t.SoThua = -1; t.ToTrong.Clear();
            try
            {
                using (var md = System.Security.Cryptography.MD5.Create()) using (var fs = File.OpenRead(t.Gtp)) t.Ma = BitConverter.ToString(md.ComputeHash(fs));
                using (var g = Gtp.Open(t.Gtp))
                {
                    if (!g.Has("ThuaDat", "soThuTuThua")) { t.Loi = "tệp không có bảng thửa đất"; return; }
                    t.SoThua = Convert.ToInt32(g.Query("select count(*) as n from ThuaDat")[0]["n"]);
                    if (g.Has("ThuaDat", "soHieuToBanDo")) foreach (var r in g.Query("select soHieuToBanDo as t, count(*) as n from ThuaDat group by soHieuToBanDo")) t.ToTrong[Gtp.Norm(Convert.ToString(r["t"], CultureInfo.InvariantCulture))] = Convert.ToInt32(r["n"]);
                }
            }
            catch (Exception ex) { t.Loi = ex.Message; }
        }
        // ghi chú từng dòng: thiếu bản đồ / thuộc tính, trùng số tờ, tệp thuộc tính giống hệt tệp khác (bỏ chọn bản sau)
        public static void KiemTra(List<GopTo> ds)
        {
            foreach (var t in ds) t.Ghi = "";
            for (int i = 0; i < ds.Count; i++)
            {
                var t = ds[i]; var g = new List<string>();
                if (t.Dgn == "") g.Add("chưa có bản đồ cùng tên"); if (t.Gtp == "") g.Add("chưa có tệp thuộc tính cùng tên");
                if (t.Ma != "") { var k = ds.Take(i).FirstOrDefault(z => z.Ma == t.Ma); if (k != null) { g.Add("tệp thuộc tính giống hệt " + k.Ten + " (" + k.ThuMuc + ")" + (t.TuBo ? "" : " – đã bỏ chọn")); if (!t.TuBo) { t.Chon = false; t.TuBo = true; } } }
                if (t.To != "") { var k = ds.Take(i).FirstOrDefault(z => z.Chon && z.To == t.To); if (k != null && t.Chon) g.Add("trùng số tờ " + t.To + " với " + k.Ten + " (" + k.ThuMuc + ")"); }
                else g.Add("tên tệp không có số tờ");
                var khac = t.ToTrong.Keys.Where(k => k != t.To && k != "").ToList(); if (t.To != "" && khac.Count > 0) g.Add("trong tệp ghi tờ " + string.Join(", ", khac.ToArray()) + " – tích “Đặt số tờ theo tên” để thành tờ " + t.To + " trong tệp gộp");
                t.Ghi = string.Join(" · ", g.ToArray());
            }
        }

        // ================= CẤU TRÚC TỆP THUỘC TÍNH =================
        class Cot { public string Ten, Kieu, MacDinh; public bool NotNull; }
        class Bang
        {
            public string Ten, Pk; public bool PkSo, DuocTro, Ao; public List<Cot> Cot = new List<Cot>();
            public Dictionary<string, string> Fk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);     // cột → bảng có khoá chính đó
            public bool TenDanhMuc;   // tên kiểu danh mục: DM…, …Loai… (bảng mã dùng chung – không dời mã)
            public bool DanhMuc { get { return TenDanhMuc || Fk.Count == 0 && !DuocTro; } }
            public bool Co(string c) { return Cot.Any(x => string.Equals(x.Ten, c, StringComparison.OrdinalIgnoreCase)); }
        }
        // cột mã phần tử bản đồ (DGN) – đổi theo bản đồ gộp
        static readonly Dictionary<string, string[]> MaPhanTu = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "ThuaDatTopo", new[] { "elementId" } },
            { "NhanDiaChinh", new[] { "nhanId", "loaiDatId", "soThuaId", "dienTichId", "muiTenId", "phanCachId" } }
        };
        static Dictionary<string, Bang> CauTruc(Sqlite db, string sc)
        {
            var B = new Dictionary<string, Bang>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in db.Query("select name, sql from " + sc + ".sqlite_master where type='table' and name not like 'sqlite_%'"))
            {
                var b = new Bang { Ten = Convert.ToString(r["name"]), Ao = (Convert.ToString(r["sql"]) ?? "").TrimStart().StartsWith("CREATE VIRTUAL", StringComparison.OrdinalIgnoreCase) };
                var pk = new List<Cot>(); var pkTy = new List<string>();
                foreach (var c in db.Query("pragma " + sc + ".table_info(" + Sqlite.Q(b.Ten) + ")"))
                {
                    var k = new Cot { Ten = Convert.ToString(c["name"]), Kieu = (Convert.ToString(c["type"]) ?? "").ToLowerInvariant(), NotNull = Convert.ToString(c["notnull"]) == "1", MacDinh = c["dflt_value"] == null ? null : Convert.ToString(c["dflt_value"]) };
                    b.Cot.Add(k); if (Convert.ToString(c["pk"]) != "0") { pk.Add(k); }
                }
                if (pk.Count == 1) { b.Pk = pk[0].Ten; b.PkSo = pk[0].Kieu.Contains("int"); }
                B[b.Ten] = b;
            }
            // khoá ngoại theo tên: cột trùng tên khoá chính số (không phải "id") của bảng khác; cột <tên>CuId: mã cũ của cùng bảng (thuaDatCuId → ThuaDat)
            foreach (var b in B.Values) b.TenDanhMuc = b.Ten.StartsWith("DM", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(b.Ten, @"(^|_)Loai");
            var chu = new Dictionary<string, List<Bang>>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in B.Values) if (!b.Ao && !b.TenDanhMuc && b.PkSo && !string.Equals(b.Pk, "id", StringComparison.OrdinalIgnoreCase)) { List<Bang> L; if (!chu.TryGetValue(b.Pk, out L)) chu[b.Pk] = L = new List<Bang>(); L.Add(b); }
            // nhiều bảng cùng tên khoá chính: ưu tiên bảng cùng nhóm "Tb…" với bảng trỏ tới (TbTrangSoDiaChinh → TbSoDiaChinh), rồi bảng đúng tên khoá, rồi tên ngắn nhất
            Func<string, Bang, Bang> chon = (k, con) =>
            {
                List<Bang> L; if (!chu.TryGetValue(k, out L)) return null; if (L.Count == 1) return L[0];
                bool tb = con.Ten.StartsWith("Tb", StringComparison.Ordinal); var L2 = L.Where(b => b.Ten.StartsWith("Tb", StringComparison.Ordinal) == tb).ToList(); if (L2.Count == 0) L2 = L;
                return L2.FirstOrDefault(b => string.Equals(char.ToLowerInvariant(b.Ten[0]) + b.Ten.Substring(1) + "Id", k, StringComparison.OrdinalIgnoreCase)) ?? L2.OrderBy(b => b.Ten.Length).First();
            };
            foreach (var b in B.Values)
            {
                if (b.Ao) continue; string[] pt; MaPhanTu.TryGetValue(b.Ten, out pt);
                foreach (var c in b.Cot)
                {
                    if (string.Equals(c.Ten, b.Pk, StringComparison.OrdinalIgnoreCase) || (pt != null && pt.Contains(c.Ten, StringComparer.OrdinalIgnoreCase))) continue;
                    var p = chon(c.Ten, b);
                    if (p == null && c.Ten.EndsWith("CuId", StringComparison.OrdinalIgnoreCase)) p = chon(c.Ten.Substring(0, c.Ten.Length - 4) + "Id", b);
                    if (p == null || p == b && string.Equals(c.Ten, b.Pk, StringComparison.OrdinalIgnoreCase)) continue;
                    b.Fk[c.Ten] = p.Ten; p.DuocTro = true;
                }
            }
            return B;
        }
        // mô tả cách gộp từng bảng (chẩn đoán, không có dữ liệu)
        public static string MoTa(Sqlite db)
        {
            var B = CauTruc(db, "main"); var sb = new StringBuilder();
            foreach (var b in B.Values.OrderBy(x => x.Ten))
                sb.AppendLine(b.Ten + " · " + (b.Ao ? "bảng ảo" : b.DanhMuc ? "danh mục" : b.PkSo ? "dữ liệu, khoá " + b.Pk : "dữ liệu, không khoá số") + (b.Fk.Count > 0 ? " · trỏ: " + string.Join(", ", b.Fk.Select(kv => kv.Key + "→" + kv.Value).ToArray()) : "") + (MaPhanTu.ContainsKey(b.Ten) ? " · mã phần tử bản đồ" : ""));
            return sb.ToString();
        }

        // ================= GỘP THUỘC TÍNH =================
        // ds: các tờ đã chọn có tệp thuộc tính (theo thứ tự, tờ đầu làm nền); maPt[i]: mã phần tử cũ → mới của bản đồ tờ i trong bản đồ gộp
        // (null: tờ nền – mã giữ nguyên; thiếu khoá: tờ chưa gộp bản đồ → bỏ liên kết phần tử); soTheoTen: đặt số tờ theo tên tờ
        public static GopKq GopThuocTinh(List<GopTo> ds, string ra, bool soTheoTen, Dictionary<int, Dictionary<long, long>> maPt, bool coBanDo, Action<string> bao)
        {
            // cách đổi mã phần tử của tờ: null = bỏ liên kết (tờ chưa gộp bản đồ); tờ nền bản đồ (chép nguyên tệp): giữ mã
            Func<GopTo, Func<long, long?>> doiMa = t =>
            {
                Dictionary<long, long> mp; if (!coBanDo || maPt == null || !maPt.TryGetValue(ds.IndexOf(t), out mp)) return null;
                if (mp == null) return id => id; return id => { long v; return mp.TryGetValue(id, out v) ? v : (long?)null; };
            };
            var kq = new GopKq { Tep = ra }; var L = ds.Where(t => t.Gtp != "").ToList(); if (L.Count == 0) throw new Exception("Chưa chọn tệp thuộc tính nào");
            bao = bao ?? (s => { });
            Gtp nen = null;
            try
            {
                bao("Đang đọc " + Path.GetFileName(L[0].Gtp) + "…"); nen = Gtp.Open(L[0].Gtp); var db = nen.Db;
                if (soTheoTen) DatSoTo(nen, L[0], kq);
                var B = CauTruc(db, "main");
                LienKetNen(db, B, doiMa(L[0]), kq);
                kq.To = 1;
                // khoảng mã đã dùng của từng bảng (cả mã đã xoá: sqlite_sequence) – tờ sau dời khoá chính ra ngoài khoảng này
                var dung = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                foreach (var b in B.Values) if (b.PkSo && !b.Ao && !b.DanhMuc) dung[b.Ten] = MaLon(db, "main", b);
                for (int i = 1; i < L.Count; i++)
                {
                    var t = L[i]; bao("Đang gộp " + Path.GetFileName(t.Gtp) + " (" + (i + 1) + "/" + L.Count + ")…");
                    Gtp s = null;
                    try
                    {
                        s = Gtp.Open(t.Gtp); if (soTheoTen) DatSoTo(s, t, kq);
                        db.Exec("attach database ? as s", s.Temp);
                        try
                        {
                            var S = CauTruc(db, "s"); var off = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase); var lenS = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                            foreach (var b in B.Values) if (b.PkSo && !b.Ao && !b.DanhMuc && S.ContainsKey(b.Ten)) { off[b.Ten] = dung[b.Ten]; lenS[b.Ten] = MaLon(db, "s", S[b.Ten]); }
                            db.Exec("begin");
                            try
                            {
                                var f = doiMa(t);
                                foreach (var b in B.Values.OrderBy(x => x.Ten))
                                {
                                    Bang sb; if (b.Ao || !S.TryGetValue(b.Ten, out sb) || ChiTepNen.Contains(b.Ten)) continue;
                                    ChepBang(db, b, sb, off, kq, f, MaPhanTu.ContainsKey(b.Ten));
                                }
                                // mã đã dùng: tờ sau dời tiếp; sqlite_sequence giữ đủ khoảng (thửa mới tạo sau này không trùng mã cũ đã dời)
                                foreach (var kv in off) { long moi = kv.Value + lenS[kv.Key]; dung[kv.Key] = Math.Max(MaLon(db, "main", B[kv.Key]), moi); GhiSeq(db, kv.Key, dung[kv.Key]); }
                                db.Exec("commit");
                            }
                            catch { try { db.Exec("rollback"); } catch { } throw; }
                        }
                        finally { db.Exec("detach database s"); }
                        kq.To++; t.KetQua = "✓ đã gộp thuộc tính";
                    }
                    catch (Exception ex) { Program.Log(ex); t.KetQua = "⚠ chưa gộp được thuộc tính: " + ex.Message; kq.Bao.Add(t.Ten + ": " + ex.Message); }
                    finally { if (s != null) s.Dispose(); }
                }
                if (L[0].KetQua == "") L[0].KetQua = "✓ tệp nền của thuộc tính";
                // tổng hợp, kiểm tra
                if (nen.Has("ThuaDat", "thuaDatId")) kq.Thua = Convert.ToInt32(db.Query("select count(*) as n from ThuaDat")[0]["n"]);
                if (nen.Has("Nguoi", "nguoiId")) kq.Nguoi = Convert.ToInt32(db.Query("select count(*) as n from Nguoi")[0]["n"]);
                if (nen.Has("ThuaDat", "soHieuToBanDo", "soThuTuThua"))
                    foreach (var r in db.Query("select soHieuToBanDo as t, soThuTuThua as s, count(*) as n from ThuaDat group by soHieuToBanDo, soThuTuThua having count(*) > 1 order by soHieuToBanDo, soThuTuThua"))
                        kq.Trung.Add("tờ " + Gtp.Norm(Convert.ToString(r["t"], CultureInfo.InvariantCulture)) + " thửa " + Gtp.Norm(Convert.ToString(r["s"], CultureInfo.InvariantCulture)) + " (" + r["n"] + " lần)");
                if (nen.Has("ThuaDat", "xaId")) { var xa = db.Query("select distinct xaId as x from ThuaDat where xaId is not null").Count; if (xa > 1) kq.Bao.Add("các tờ thuộc " + xa + " mã xã khác nhau"); }
                if (nen.Has("ThongTinMoRong", "nhomThongTin", "truong", "giaTri"))
                {
                    db.Exec("update ThongTinMoRong set giaTri=? where nhomThongTin='TopoInfo' and truong='tongSoThua'", kq.Thua.ToString(CultureInfo.InvariantCulture));
                    if (nen.Has("ThuaDat", "dienTich")) { var dt = db.Query("select total(dienTich) as d from ThuaDat")[0]["d"]; db.Exec("update ThongTinMoRong set giaTri=? where nhomThongTin='TopoInfo' and truong='tongDienTich'", Convert.ToDouble(dt, CultureInfo.InvariantCulture).ToString("0.##", CultureInfo.InvariantCulture)); }
                }
                var ok = db.Query("pragma quick_check"); var kt = ok.Count > 0 ? Convert.ToString(ok[0].Values.First()) : "";
                if (kt != "ok") throw new Exception("tệp gộp không đạt kiểm tra (" + kt + ") – chưa lưu");
                bao("Đang lưu " + Path.GetFileName(ra) + "…"); nen.Dirty = true; nen.Save(ra);
                return kq;
            }
            finally { if (nen != null) nen.Dispose(); }
        }
        // số tờ theo tên tờ bản đồ ngay trong bản đọc ra (tệp gốc không đổi)
        static void DatSoTo(Gtp g, GopTo t, GopKq kq)
        {
            var to = SoTo.TheoTen(t.Dgn != "" ? t.Dgn : t.Gtp); if (to == "") return;
            var f = new SoToTep { Path = t.Gtp, ToMoi = to, TuDau = "tên tệp" }; string b;
            try { SoTo.Chuyen(g, f, true, true, out b); if (f.GiuCu > 0) kq.Bao.Add(t.Ten + ": " + f.GiuCu + " thửa giữ số tờ cũ vì trùng số thửa của tờ " + to); }
            catch (Exception ex) { kq.Bao.Add(t.Ten + ": chưa đặt được số tờ (" + ex.Message + ")"); }
        }
        static long MaLon(Sqlite db, string sc, Bang b)
        {
            long m = 0; try { var r = db.Query("select max(" + Sqlite.Q(b.Pk) + ") as m from " + sc + "." + Sqlite.Q(b.Ten))[0]["m"]; if (r != null) m = Convert.ToInt64(r, CultureInfo.InvariantCulture); } catch { }
            try { var q = db.Query("select seq from " + sc + ".sqlite_sequence where name=?", b.Ten); if (q.Count > 0 && q[0]["seq"] != null) m = Math.Max(m, Convert.ToInt64(q[0]["seq"], CultureInfo.InvariantCulture)); } catch { }
            return Math.Max(m, 0);
        }
        static void GhiSeq(Sqlite db, string bang, long v)
        {
            try
            {
                var q = db.Query("select seq from main.sqlite_sequence where name=?", bang);
                if (q.Count == 0) db.Exec("insert into main.sqlite_sequence (name, seq) values (?, ?)", bang, v);
                else if (q[0]["seq"] == null || Convert.ToInt64(q[0]["seq"], CultureInfo.InvariantCulture) < v) db.Exec("update main.sqlite_sequence set seq=? where name=?", v, bang);
            }
            catch { }   // bảng không AUTOINCREMENT: không có sqlite_sequence
        }
        // chép một bảng của tờ s vào tệp nền. mp: mã phần tử cũ → mới (null: bảng có mã phần tử mà tờ chưa gộp bản đồ → không chép)
        static void ChepBang(Sqlite db, Bang b, Bang sb, Dictionary<string, long> off, GopKq kq, Func<long, long?> mp, bool coMaPt)
        {
            if (coMaPt && mp == null) { long n0 = Convert.ToInt64(db.Query("select count(*) as n from s." + Sqlite.Q(b.Ten))[0]["n"]); kq.BoLienKet += (int)n0; return; }
            var chung = b.Cot.Where(c => sb.Co(c.Ten)).ToList(); if (chung.Count == 0) return;
            var cols = new List<string>(); var exps = new List<string>();
            foreach (var c in chung)
            {
                string q = Sqlite.Q(c.Ten), e = q; string p; long o;
                if (b.PkSo && string.Equals(c.Ten, b.Pk, StringComparison.OrdinalIgnoreCase) && !b.DanhMuc && off.TryGetValue(b.Ten, out o)) e = q + " + " + o;
                else if (b.Fk.TryGetValue(c.Ten, out p) && off.TryGetValue(p, out o)) e = "case when typeof(" + q + ") in ('integer','real','text') and " + q + " > 0 then " + q + " + " + o + " else " + q + " end";
                cols.Add(q); exps.Add(e);
            }
            // cột bắt buộc của tệp nền mà tờ này không có (khác phiên bản): giá trị mặc định đúng kiểu
            foreach (var c in b.Cot.Where(c => !sb.Co(c.Ten) && c.NotNull && c.MacDinh == null)) { cols.Add(Sqlite.Q(c.Ten)); exps.Add(c.Kieu.Contains("int") || c.Kieu == "bit" || c.Kieu.Contains("float") || c.Kieu.Contains("double") || c.Kieu.Contains("real") ? "0" : "''"); }
            string tb = Sqlite.Q(b.Ten), ins = "(" + string.Join(",", cols.ToArray()) + ")";
            if (b.DanhMuc)
            {
                // danh mục: thêm dòng có mã mới; dòng cùng mã giữ như tệp nền (bảng không khoá: thêm dòng chưa có y hệt)
                if (b.Pk != null) db.Exec("insert or ignore into main." + tb + ins + " select " + string.Join(",", exps.ToArray()) + " from s." + tb);
                else db.Exec("insert into main." + tb + ins + " select " + string.Join(",", exps.ToArray()) + " from s." + tb + " except select " + string.Join(",", cols.ToArray()) + " from main." + tb);
                return;
            }
            if (!coMaPt) { db.Exec("insert into main." + tb + ins + " select " + string.Join(",", exps.ToArray()) + " from s." + tb); return; }
            // bảng liên kết phần tử bản đồ: đổi mã phần tử theo bản đồ gộp, dòng có mã không tìm thấy thì bỏ
            var pt = MaPhanTu[b.Ten].Where(c => b.Co(c) && sb.Co(c)).ToList();
            var rows = db.Query("select rowid as _r, * from s." + tb);
            foreach (var r in rows)
            {
                bool bo = false; var v = new object[cols.Count];
                for (int k = 0; k < chung.Count; k++)
                {
                    var c = chung[k]; object x = r.ContainsKey(c.Ten) ? r[c.Ten] : null; string p; long o;
                    if (pt.Contains(c.Ten, StringComparer.OrdinalIgnoreCase)) { long id; if (x != null && long.TryParse(Convert.ToString(x, CultureInfo.InvariantCulture), out id) && id > 0) { var nid = mp(id); if (nid.HasValue) x = nid.Value; else bo = true; } }
                    else if (b.PkSo && string.Equals(c.Ten, b.Pk, StringComparison.OrdinalIgnoreCase) && off.TryGetValue(b.Ten, out o)) x = Convert.ToInt64(x, CultureInfo.InvariantCulture) + o;
                    else if (b.Fk.TryGetValue(c.Ten, out p) && off.TryGetValue(p, out o)) { long id; if (x != null && long.TryParse(Convert.ToString(x, CultureInfo.InvariantCulture), out id) && id > 0) x = id + o; }
                    v[k] = x;
                }
                for (int k = chung.Count; k < cols.Count; k++) v[k] = exps[k] == "0" ? (object)0L : "";
                if (bo) { kq.BoLienKet++; continue; }
                db.Exec("insert into main." + tb + ins + " values (" + string.Join(",", cols.Select(c => "?").ToArray()) + ")", v); kq.LienKet++;
            }
        }
        // bảng chỉ lấy của tệp nền (thông tin chung của tệp, cấu hình) – tệp sau không chép
        static readonly HashSet<string> ChiTepNen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ThongTinMoRong", "GPMB_CauHinh" };
        // liên kết phần tử của tệp nền: tờ nền bản đồ – giữ mã; tờ đã gộp bản đồ – đổi theo mã mới (mã không thấy thì bỏ dòng); chưa gộp bản đồ – bỏ hết
        static void LienKetNen(Sqlite db, Dictionary<string, Bang> B, Func<long, long?> mp, GopKq kq)
        {
            foreach (var kv in MaPhanTu)
            {
                Bang b; if (!B.TryGetValue(kv.Key, out b)) continue; var pt = kv.Value.Where(b.Co).ToList(); if (pt.Count == 0) continue;
                if (mp == null) { long n = Convert.ToInt64(db.Query("select count(*) as n from " + Sqlite.Q(b.Ten))[0]["n"]); if (n > 0) { db.Exec("delete from " + Sqlite.Q(b.Ten)); kq.BoLienKet += (int)n; } continue; }
                if (mp(-1) == -1) continue;   // giữ mã (tờ nền bản đồ)
                foreach (var r in db.Query("select rowid as _r, " + string.Join(",", pt.Select(Sqlite.Q).ToArray()) + " from " + Sqlite.Q(b.Ten)))
                {
                    bool bo = false; var v = new List<object>();
                    foreach (var c in pt) { object x = r[c]; long id; if (x != null && long.TryParse(Convert.ToString(x, CultureInfo.InvariantCulture), out id) && id > 0) { var nid = mp(id); if (nid.HasValue) x = nid.Value; else bo = true; } v.Add(x); }
                    if (bo) { db.Exec("delete from " + Sqlite.Q(b.Ten) + " where rowid=?", r["_r"]); kq.BoLienKet++; continue; }
                    v.Add(r["_r"]); db.Exec("update " + Sqlite.Q(b.Ten) + " set " + string.Join(",", pt.Select(c => Sqlite.Q(c) + "=?").ToArray()) + " where rowid=?", v.ToArray()); kq.LienKet++;
                }
            }
        }

        // ================= GỘP BẢN ĐỒ (qua kết nối bản vẽ) =================
        // các lớp viết dạng 1-5, 8, 63
        public static HashSet<int> Lop(string s)
        {
            var o = new HashSet<int>();
            foreach (var x in (s ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var m = Regex.Match(x, @"^(\d+)\s*-\s*(\d+)$"); int a, b;
                if (m.Success && int.TryParse(m.Groups[1].Value, out a) && int.TryParse(m.Groups[2].Value, out b)) { for (int i = Math.Min(a, b); i <= Math.Max(a, b) && i - Math.Min(a, b) < 10000; i++) o.Add(i); }
                else if (int.TryParse(x, out a)) o.Add(a);
            }
            return o;
        }
        // tờ đầu: chép nguyên tệp; các tờ sau: chép từng phần tử đồ hoạ vào mô hình mặc định của bản đồ gộp. maPt[i]: mã phần tử cũ → mới của tờ i
        public static GopKq GopBanDo(List<GopTo> ds, string ra, HashSet<int> boLop, Dictionary<int, Dictionary<long, long>> maPt, Action<string> bao)
        {
            var kq = new GopKq { Tep = ra }; var L = ds.Where(t => t.Dgn != "").ToList(); if (L.Count == 0) throw new Exception("Chưa chọn tờ bản đồ nào");
            bao = bao ?? (s => { }); if (!Ms.Connected) throw new Exception("Gộp bản đồ cần đang kết nối bản vẽ (Hệ thống → Kết nối)");
            dynamic app = Ms.App; string dang = ""; try { dang = Path.GetFullPath(Ms.FullName); } catch { }
            if (string.Equals(dang, Path.GetFullPath(ra), StringComparison.OrdinalIgnoreCase)) throw new Exception("Tệp bản đồ gộp đang mở – chọn tên khác");
            File.Copy(L[0].Dgn, ra, true); try { File.SetAttributes(ra, FileAttributes.Normal); } catch { }
            dynamic dst = app.OpenDesignFileForProgram(ra, false); dynamic dm = dst.DefaultModelReference;
            try
            {
                if (boLop != null && boLop.Count > 0) kq.PhanTu -= BoLopNen(app, dm, boLop);
                kq.PhanTu += DemPhanTu(app, dm); maPt[ds.IndexOf(L[0])] = null; kq.To = 1; L[0].KetQua = "✓ tờ nền của bản đồ";
                for (int i = 1; i < L.Count; i++)
                {
                    var t = L[i]; bao("Đang gộp bản đồ " + Path.GetFileName(t.Dgn) + " (" + (i + 1) + "/" + L.Count + ")…");
                    bool laDang = string.Equals(dang, Path.GetFullPath(t.Dgn), StringComparison.OrdinalIgnoreCase); dynamic src = null;
                    try
                    {
                        src = laDang ? app.ActiveDesignFile : app.OpenDesignFileForProgram(t.Dgn, true);
                        var mp = new Dictionary<long, long>(); var gg = new Dictionary<int, int>(); int n = 0, hong = 0; string loi1 = "";
                        dynamic sc = app.CreateObjectInMicroStation("MicroStationDGN.ElementScanCriteria"); sc.ExcludeNonGraphical();
                        dynamic en = src.DefaultModelReference.Scan(sc);
                        while ((bool)en.MoveNext())
                        {
                            BCOM.Element el = en.Current; int lv = -1; try { lv = Ms.LvNum(el); } catch { }
                            if (boLop != null && boLop.Contains(lv)) continue;
                            BCOM.Element nw; try { nw = dm.CopyElement(el); } catch (Exception ex) { hong++; if (loi1 == "") loi1 = ex.Message; continue; }
                            if (nw == null) { hong++; continue; } n++;
                            Map(el, nw, mp); NhomMoi(dst, el, nw, gg);
                            if (n % 500 == 0) bao("Đang gộp bản đồ " + Path.GetFileName(t.Dgn) + ": " + n + " phần tử…");
                        }
                        maPt[ds.IndexOf(t)] = mp; kq.PhanTu += n; kq.To++; t.KetQua = "✓ đã gộp " + n + " phần tử bản đồ" + (hong > 0 ? " · ⚠ " + hong + " phần tử không chép được" : "");
                        if (hong > 0) kq.Bao.Add(t.Ten + ": " + hong + " phần tử bản đồ không chép được" + (loi1 != "" ? " (" + loi1 + ")" : ""));
                    }
                    catch (Exception ex) { Program.Log(ex); t.KetQua = "⚠ chưa gộp được bản đồ: " + ex.Message; kq.Bao.Add(t.Ten + ": " + ex.Message); }
                    finally { if (src != null && !laDang) try { src.Close(); } catch { } }
                }
                bao("Đang lưu " + Path.GetFileName(ra) + "…"); dst.Save();
            }
            finally { try { dst.Close(); } catch { } }
            return kq;
        }
        // mã phần tử cũ → mới, cả thành phần của ô / khối chữ (theo đúng thứ tự)
        static void Map(BCOM.Element a, BCOM.Element b, Dictionary<long, long> mp)
        {
            long x = Ms.Id(a), y = Ms.Id(b); if (x != 0 && y != 0) mp[x] = y;
            int t = -1; try { t = (int)a.Type; } catch { }
            if (t != 2 && t != 7 && t != 12 && t != 14 && t != 18 && t != 19) return;     // ô, khối chữ, chuỗi / vùng phức, khối 3D: có thành phần
            try
            {
                dynamic ea = ((dynamic)a).AsComplexElement.GetSubElements(), eb = ((dynamic)b).AsComplexElement.GetSubElements();
                while ((bool)ea.MoveNext() && (bool)eb.MoveNext()) Map((BCOM.Element)ea.Current, (BCOM.Element)eb.Current, mp);
            }
            catch { }
        }
        // nhóm phần tử (graphic group) của từng tờ đánh số mới trong bản đồ gộp – không dính nhóm với tờ khác
        static bool coNhom = true;
        static void NhomMoi(dynamic dst, BCOM.Element el, BCOM.Element nw, Dictionary<int, int> gg)
        {
            if (!coNhom) return;
            try
            {
                int g = (int)((dynamic)el).GraphicGroup; if (g == 0) return; int m;
                if (!gg.TryGetValue(g, out m)) gg[g] = m = (int)dst.GetNextGraphicGroup();
                ((dynamic)nw).GraphicGroup = m; nw.Rewrite();
            }
            catch { coNhom = false; }
        }
        static int DemPhanTu(dynamic app, dynamic dm)
        {
            int n = 0; try { dynamic sc = app.CreateObjectInMicroStation("MicroStationDGN.ElementScanCriteria"); sc.ExcludeNonGraphical(); dynamic en = dm.Scan(sc); while ((bool)en.MoveNext()) n++; } catch { }
            return n;
        }
        // bỏ các lớp không gộp khỏi tờ nền (bản chép)
        static int BoLopNen(dynamic app, dynamic dm, HashSet<int> boLop)
        {
            var xoa = new List<BCOM.Element>();
            try { dynamic sc = app.CreateObjectInMicroStation("MicroStationDGN.ElementScanCriteria"); sc.ExcludeNonGraphical(); dynamic en = dm.Scan(sc); while ((bool)en.MoveNext()) { BCOM.Element e = en.Current; int lv = -1; try { lv = Ms.LvNum(e); } catch { } if (boLop.Contains(lv)) xoa.Add(e); } } catch { }
            foreach (var e in xoa) try { dm.RemoveElement(e); } catch { }
            return 0;
        }
    }

    // ================= CỬA SỔ GỘP CSDL TOÀN XÃ =================
    public class GopForm : Form
    {
        ListView L = new ListView { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false, GridLines = true };
        TextBox tDir = new TextBox { Width = 420 }, tTen = new TextBox { Width = 200 }, tBo = new TextBox { Width = 120 };
        CheckBox cBd = new OTich { Text = "Gộp bản đồ (.dgn) – cần đang kết nối bản vẽ", Checked = true, AutoSize = true };
        CheckBox cTt = new OTich { Text = "Gộp thuộc tính (.gtp)", Checked = true, AutoSize = true };
        CheckBox cSo = new OTich { Text = "Đặt số tờ theo tên tờ bản đồ trong tệp gộp (DC12 → tờ 12; tệp gốc không đổi)", Checked = false, AutoSize = true };
        Label st = new Label { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6, 2, 6, 2) };
        List<GopTo> ds = new List<GopTo>(); bool nap;
        public string BanDoGop = "", ThuocTinhGop = "";
        public GopForm()
        {
            Text = "Gộp CSDL bản đồ và thuộc tính toàn xã"; Font = new Font("Segoe UI", 9f); FormBorderStyle = FormBorderStyle.Sizable; MinimizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; TopMost = true;
            Width = 1060; Height = 660; MinimumSize = new Size(820, 480); GiaoDien.KhongCat(this);
            var top = new Label { Dock = DockStyle.Top, Height = 62, Padding = new Padding(6, 6, 6, 0), Text =
                "Gộp các tờ bản đồ (.dgn) và tệp thuộc tính (.gtp) của cả xã thành một bản đồ và một tệp thuộc tính toàn xã. Tệp ở nhiều thư mục: bấm Thêm tệp… (chọn nhiều tệp một lần, bấm nhiều lần cho từng thư mục) " +
                "hoặc Thêm thư mục… (lấy cả thư mục con). Bản đồ và tệp thuộc tính cùng tên ghép vào một dòng. Tệp gốc chỉ đọc, không sửa. Tờ đầu danh sách làm nền (giữ thiết lập, danh mục của tờ đó)." };
            L.Columns.Add("Tờ", 50); L.Columns.Add("Bản đồ (.dgn)", 190); L.Columns.Add("Thuộc tính (.gtp)", 190); L.Columns.Add("Số thửa", 65, HorizontalAlignment.Right); L.Columns.Add("Thư mục", 230); L.Columns.Add("Ghi chú", 330);
            var bt = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(4), FlowDirection = FlowDirection.LeftToRight };
            Func<string, int, Button> nut = (s, w) => { var b = new Button { Text = s, Width = w, Height = 28 }; bt.Controls.Add(b); return b; };
            var bTep = nut("Thêm tệp…", 100); var bThu = nut("Thêm thư mục…", 120); var bBo = nut("Bỏ khỏi danh sách", 130); var bLen = nut("▲ Lên", 70); var bXuong = nut("▼ Xuống", 80); var bXep = nut("Xếp theo số tờ", 120);
            var op = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 118, ColumnCount = 4, Padding = new Padding(6, 2, 4, 0) };
            var bDir = new Button { Text = "Chọn…", Width = 70, Height = 24 };
            op.Controls.Add(new Label { Text = "Lưu vào thư mục", AutoSize = true, Margin = new Padding(3, 6, 3, 3) }); op.Controls.Add(tDir); op.Controls.Add(bDir); op.Controls.Add(new Label { Text = "", AutoSize = true });
            op.Controls.Add(new Label { Text = "Tên CSDL gộp", AutoSize = true, Margin = new Padding(3, 6, 3, 3) }); op.Controls.Add(tTen); op.Controls.Add(new Label { Text = "Bỏ các level (không gộp)", AutoSize = true, Margin = new Padding(3, 6, 3, 3) }); op.Controls.Add(tBo);
            op.Controls.Add(cBd); op.SetColumnSpan(cBd, 2); op.Controls.Add(cTt); op.SetColumnSpan(cTt, 2);
            op.Controls.Add(cSo); op.SetColumnSpan(cSo, 4);
            var bp = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 42, Padding = new Padding(4) };
            var dong = new Button { Text = "Đóng", Width = 90, Height = 30, DialogResult = DialogResult.Cancel }; var ok = new Button { Text = "⧉ Gộp CSDL toàn xã", Width = 160, Height = 30 };
            bp.Controls.Add(dong); bp.Controls.Add(ok); CancelButton = dong;
            Controls.Add(L); Controls.Add(top); Controls.Add(bt); Controls.Add(op); Controls.Add(st); Controls.Add(bp);
            var xa = (Cfg.I.Xa ?? "").Trim(); tTen.Text = "CSDL_toan_xa" + (xa != "" ? "_" + Regex.Replace(Bo(xa), @"[^\w]+", "_") : "");
            cBd.Enabled = Ms.Connected; if (!Ms.Connected) { cBd.Checked = false; cBd.Text = "Gộp bản đồ (.dgn) – chưa kết nối (Hệ thống → Kết nối)"; }
            bTep.Click += (s, e) => { using (var d = new OpenFileDialog { Filter = "Bản đồ và thuộc tính (*.dgn;*.gtp)|*.dgn;*.gtp|Bản đồ (*.dgn)|*.dgn|Tệp thuộc tính (*.gtp)|*.gtp", Multiselect = true, Title = "Chọn các tờ bản đồ và tệp thuộc tính (bấm lại để chọn ở thư mục khác)" }) if (d.ShowDialog(this) == DialogResult.OK) Them(d.FileNames); };
            bThu.Click += (s, e) => { using (var d = new FolderBrowserDialog { Description = "Chọn thư mục có các tờ bản đồ (.dgn) và tệp thuộc tính (.gtp) – lấy cả thư mục con" }) if (d.ShowDialog(this) == DialogResult.OK) { var f = Gop.TrongThuMuc(d.SelectedPath); if (f.Count == 0) Msg("Thư mục không có tệp .dgn hay .gtp"); else Them(f); } };
            bBo.Click += (s, e) => { foreach (ListViewItem it in L.SelectedItems) ds.Remove((GopTo)it.Tag); Gop.KiemTra(ds); Ve(); };
            bLen.Click += (s, e) => Doi(-1); bXuong.Click += (s, e) => Doi(1);
            bXep.Click += (s, e) => { ds = ds.OrderBy(t => { int v; return int.TryParse(t.To, out v) ? v : int.MaxValue; }).ThenBy(t => t.Ten, StringComparer.OrdinalIgnoreCase).ToList(); Gop.KiemTra(ds); Ve(); };
            bDir.Click += (s, e) => { using (var d = new FolderBrowserDialog { Description = "Thư mục lưu bản đồ và tệp thuộc tính toàn xã" }) { if (tDir.Text != "" && Directory.Exists(tDir.Text)) d.SelectedPath = tDir.Text; if (d.ShowDialog(this) == DialogResult.OK) tDir.Text = d.SelectedPath; } };
            L.ItemChecked += (s, e) => { if (nap) return; ((GopTo)e.Item.Tag).Chon = e.Item.Checked; Tong(); BeginInvoke((Action)(() => { Gop.KiemTra(ds); Ve(); })); };
            L.SelectedIndexChanged += (s, e) => { if (L.SelectedItems.Count == 1) { var t = (GopTo)L.SelectedItems[0].Tag; Msg((t.Dgn != "" ? t.Dgn : "") + (t.Gtp != "" ? (t.Dgn != "" ? "  +  " : "") + t.Gtp : "") + (t.Ghi != "" ? " — " + t.Ghi : "")); } };
            ok.Click += (s, e) => Chay();
            Tong();
        }
        static string Bo(string s) { var n = s.Normalize(NormalizationForm.FormD); var sb = new StringBuilder(); foreach (var c in n) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c); return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D'); }
        void Msg(string s) { st.Text = s; }
        void Them(IEnumerable<string> tep)
        {
            var c0 = Cursor; Cursor = Cursors.WaitCursor;
            try { var l = tep.ToList(); Msg("Đang đọc " + l.Count + " tệp…"); Application.DoEvents(); int n = Gop.Them(ds, l); if (tDir.Text == "" && ds.Count > 0) tDir.Text = Path.Combine(ds[0].ThuMuc, "ToanXa"); Ve(); if (n < l.Count) Msg("Đã thêm " + n + " tệp (" + (l.Count - n) + " tệp đã có trong danh sách hoặc không phải .dgn / .gtp)"); }
            finally { Cursor = c0; }
        }
        void Doi(int d)
        {
            if (L.SelectedItems.Count != 1) return; var t = (GopTo)L.SelectedItems[0].Tag; int i = ds.IndexOf(t), j = i + d; if (j < 0 || j >= ds.Count) return;
            ds[i] = ds[j]; ds[j] = t; Gop.KiemTra(ds); Ve(); foreach (ListViewItem it in L.Items) if (it.Tag == t) { it.Selected = true; it.EnsureVisible(); }
        }
        void Ve()
        {
            nap = true; L.BeginUpdate(); L.Items.Clear();
            foreach (var t in ds)
            {
                var it = new ListViewItem(new[] { t.To, t.Dgn != "" ? Path.GetFileName(t.Dgn) : "—", t.Gtp != "" ? Path.GetFileName(t.Gtp) : "—", t.SoThua >= 0 ? t.SoThua.ToString() : "", t.ThuMuc, t.KetQua != "" ? t.KetQua + (t.Ghi != "" ? " · " + t.Ghi : "") : t.Loi != "" ? "⚠ " + t.Loi : t.Ghi }) { Tag = t, Checked = t.Chon, ToolTipText = (t.Dgn + " " + t.Gtp).Trim() };
                if (t.Loi != "") it.ForeColor = Color.Firebrick; else if (t.KetQua.StartsWith("✓")) it.ForeColor = Color.DarkGreen; else if (t.Ghi.Contains("trùng") || t.Ghi.Contains("giống hệt")) it.ForeColor = Color.DarkOrange;
                L.Items.Add(it);
            }
            L.EndUpdate(); nap = false; GiaoDien.VuaCot(L, 520); Tong();
        }
        void Tong()
        {
            var c = ds.Where(t => t.Chon).ToList();
            Msg(ds.Count == 0 ? "Thêm các tờ bản đồ và tệp thuộc tính của xã (nhiều tệp, nhiều thư mục)." : c.Count + " / " + ds.Count + " tờ được chọn · " + c.Count(t => t.Dgn != "") + " bản đồ, " + c.Count(t => t.Gtp != "") + " tệp thuộc tính · " + c.Where(t => t.SoThua > 0).Sum(t => t.SoThua) + " thửa");
        }
        void Chay()
        {
            var c = ds.Where(t => t.Chon && t.Loi == "").ToList(); bool bd = cBd.Checked && cBd.Enabled, tt = cTt.Checked;
            if (!bd && !tt) { Msg("Chưa chọn gộp bản đồ hay thuộc tính"); return; }
            if (bd && c.Count(t => t.Dgn != "") == 0 || !bd && tt && c.Count(t => t.Gtp != "") == 0 || bd && !tt && c.Count(t => t.Dgn != "") == 0) { Msg("Chưa chọn tờ nào có " + (bd ? "bản đồ" : "tệp thuộc tính")); return; }
            var ten = Regex.Replace(tTen.Text.Trim(), @"[\\/:*?""<>|]+", "_"); if (ten == "") { Msg("Chưa ghi tên CSDL gộp"); return; }
            var dir = tDir.Text.Trim(); if (dir == "") { Msg("Chưa chọn thư mục lưu"); return; }
            string fDgn = Path.Combine(dir, ten + ".dgn"), fGtp = Path.Combine(dir, ten + ".gtp");
            var nguon = c.SelectMany(t => new[] { t.Dgn, t.Gtp }).Where(x => x != "").Select(x => Path.GetFullPath(x)).ToList();
            if ((bd && nguon.Contains(Path.GetFullPath(fDgn), StringComparer.OrdinalIgnoreCase)) || (tt && nguon.Contains(Path.GetFullPath(fGtp), StringComparer.OrdinalIgnoreCase))) { Msg("Tệp gộp trùng một tệp gốc – chọn thư mục hoặc tên khác"); return; }
            var de = new List<string>(); if (bd && File.Exists(fDgn)) de.Add(Path.GetFileName(fDgn)); if (tt && File.Exists(fGtp)) de.Add(Path.GetFileName(fGtp));
            var hoi = "Gộp " + c.Count + " tờ thành CSDL toàn xã:\r\n" + (bd ? "  • bản đồ " + fDgn + "\r\n" : "") + (tt ? "  • tệp thuộc tính " + fGtp + "\r\n" : "") + (de.Count > 0 ? "\r\nĐã có " + string.Join(", ", de.ToArray()) + " – sẽ ghi đè (tệp thuộc tính cũ giữ bản .bak).\r\n" : "") + "\r\nTệp gốc không bị sửa.";
            if (MessageBox.Show(this, hoi, Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            var c0 = Cursor; Cursor = Cursors.WaitCursor; var maPt = new Dictionary<int, Dictionary<long, long>>(); var bao = new List<string>(); foreach (var t in ds) t.KetQua = "";
            try
            {
                Directory.CreateDirectory(dir); Action<string> b = s => { Msg(s); Application.DoEvents(); };
                if (bd)
                {
                    try { var k = Gop.GopBanDo(c, fDgn, Gop.Lop(tBo.Text), maPt, b); BanDoGop = fDgn; bao.Add("Bản đồ: " + k.To + " tờ, " + k.PhanTu + " phần tử → " + Path.GetFileName(fDgn)); bao.AddRange(k.Bao); }
                    catch (Exception ex) { Program.Log(ex); bao.Add("⚠ Chưa gộp được bản đồ: " + ex.Message); bd = false; }
                }
                if (tt)
                {
                    try
                    {
                        var k = Gop.GopThuocTinh(c, fGtp, cSo.Checked, maPt, bd, b); ThuocTinhGop = fGtp;
                        bao.Add("Thuộc tính: " + k.To + " tệp, " + k.Thua + " thửa, " + k.Nguoi + " người sử dụng → " + Path.GetFileName(fGtp) + (k.LienKet > 0 ? " · " + k.LienKet + " liên kết thửa / nhãn đổi theo bản đồ gộp" : "") + (k.BoLienKet > 0 ? " · bỏ " + k.BoLienKet + " liên kết phần tử bản đồ" + (bd ? " không còn" : " (chưa gộp bản đồ)") : ""));
                        if (k.Trung.Count > 0) bao.Add("⚠ " + k.Trung.Count + " thửa trùng số tờ + số thửa: " + string.Join("; ", k.Trung.Take(15).ToArray()) + (k.Trung.Count > 15 ? "…" : "") + (cSo.Checked ? "" : " – thử tích “Đặt số tờ theo tên tờ bản đồ”"));
                        bao.AddRange(k.Bao);
                    }
                    catch (Exception ex) { Program.Log(ex); bao.Add("⚠ Chưa gộp được thuộc tính: " + ex.Message); }
                }
            }
            finally { Cursor = c0; }
            Ve(); Msg(string.Join(" · ", bao.Take(2).ToArray()));
            MessageBox.Show(this, string.Join("\r\n", bao.ToArray()), Text, MessageBoxButtons.OK, bao.Any(x => x.StartsWith("⚠")) ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
    }
}
