// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – TÁCH THỬA, HỢP THỬA, BẢN VẼ TÁCH THỬA, HỢP THỬA ĐẤT (Mẫu số 22). © Nguyễn Xuân Thủy
//  • Chọn thửa: chạm vào bên trong ranh thửa trên bản vẽ MicroStation (chạm lại để bỏ chọn), chuột phải là xong.
//  • Tách thửa: vẽ ranh tách trên lớp ranh thửa (nối kín từ ranh tới ranh) → lệnh Tách thửa → chạm vào trong từng thửa mới theo thứ tự đánh số.
//    Số thửa mới đánh tiếp theo số thửa lớn nhất của tờ (Điều 17 Thông tư 26/2024/TT-BTNMT), số thửa gốc không dùng lại;
//    thửa gốc (tờ, số, diện tích, loại đất, chủ, hình thửa) lưu vào thửa cũ (ThuaDatCu, DaMucDichSuDungCu) của từng thửa mới;
//    thông tin của thửa gốc (chủ sử dụng, địa chỉ, loại đất…) chép sang thửa mới; nhãn thửa mới vẽ theo quy định.
//  • Hợp thửa: lệnh Hợp thửa → chạm vào trong các thửa cần hợp → đoạn ranh chung chuyển sang lớp ranh cũ (49), thửa sau khi hợp nhận số mới,
//    các thửa cũ lưu vào thửa cũ của thửa mới, loại đất và diện tích gộp tự động, vẽ nhãn thửa mới.
//  • Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22): chạm vào trong thửa mới → lấy thửa cũ đã lưu (tệp thuộc tính, hoặc lịch sử trên máy) → lập bản vẽ.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace M8Pro
{
    // một thửa cũ (thửa gốc khi tách, các thửa trước khi hợp): ghi lại trước khi đổi bản đồ
    public class ThuaCu
    {
        public string So = "", To = "", Ld = "", Chu = "", ChuDc = "", DiaChi = "", Gcs = "", Vs = "";
        public double Dt; public double? DtGtp;
        public List<double[]> Rings; public GRow Row; public object NguoiId; public double[] Lp;
        public List<Dictionary<string, object>> Mds = new List<Dictionary<string, object>>();
    }
    public class NhomTach { public ThuaCu Goc; public List<Parcel> Moi = new List<Parcel>(); public string CanhBao = ""; }
    public class NhomHop { public List<Parcel> Cu; public HashSet<string> Chung = new HashSet<string>(); public string CanhBao = ""; }

    public static class TachHop
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static Gtp G { get { return Dc.G; } }
        public static string Str(object v)
        {
            if (v == null) return ""; if (v is double) { var d = (double)v; if (d == Math.Floor(d) && Math.Abs(d) < 1e15) return ((long)d).ToString(IC); }
            if (v is byte[]) return BitConverter.ToString((byte[])v); return Convert.ToString(v, IC);
        }
        static object Get(Dictionary<string, object> r, string k) { object v; return r != null && r.TryGetValue(k, out v) ? v : null; }
        static double? Num(object v) { if (v == null) return null; double d; if (v is double) return (double)v; if (v is long) return (long)v; return double.TryParse(Convert.ToString(v, IC).Replace(',', '.'), NumberStyles.Float, IC, out d) ? d : (double?)null; }
        public static int Int(string s) { int v; return int.TryParse((s ?? "").Trim(), out v) ? v : 0; }
        // số thửa ghi vào tệp: số nguyên khi là số, chữ khi là số tạm (ví dụ 45-1)
        public static object SoVal(string s) { long v; s = (s ?? "").Trim(); return long.TryParse(s, out v) ? (object)v : (object)s; }
        public static double Area(List<double[]> rings) { if (rings == null || rings.Count == 0) return 0; double a = Math.Abs(Geo.Area(rings[0])); foreach (var h in rings.Skip(1)) a -= Math.Abs(Geo.Area(h)); return a; }
        static bool In(List<double[]> rings, double x, double y) { return rings != null && rings.Count > 0 && Geo.InPoly(x, y, rings[0]) && !rings.Skip(1).Any(h => Geo.InPoly(x, y, h)); }
        static double Tol(double a) { return Math.Max(0.05, a * 0.001); }
        public static string[] Codes(string ld) { return (ld ?? "").Split('+', ',').Select(x => x.Trim().ToUpperInvariant()).Where(x => x != "").ToArray(); }
        // kinh tuyến trục dạng độ, phút: 105 → 105°00′, 104.75 → 104°45′
        public static string DoPhut(double v) { int d = (int)Math.Floor(v + 1e-9); int m = (int)Math.Round((v - d) * 60); if (m == 60) { d++; m = 0; } return d + "°" + m.ToString("00") + "′"; }
        public static string Ten(Parcel p) { return (p.So == "" ? "?" : p.So) + " (" + Dc.FD(p.Dt) + " m²)"; }
        static GRow RowBySo(string so) { if (G == null || string.IsNullOrEmpty(so)) return null; return Dc.SheetRows().FirstOrDefault(r => r.So == so); }
        static List<Dictionary<string, object>> Q(string sql, params object[] ps) { return G.Query(sql, ps); }

        // ================= SỐ THỬA MỚI =================
        // số thửa lớn nhất của tờ: thửa trên bản đồ, trong tệp thuộc tính, số của các thửa cũ đã huỷ (không dùng lại số đã huỷ)
        public static int MaxSo()
        {
            int m = 0; foreach (var p in Dc.P) m = Math.Max(m, Int(p.So));
            if (G != null)
            {
                foreach (var r in Dc.SheetRows()) m = Math.Max(m, Int(r.So));
                if (G.Has("ThuaDatCu", "soHieuToBanDoCu", "soThuTuThuaCu"))
                    foreach (var r in Q("select soHieuToBanDoCu as t, soThuTuThuaCu as s from ThuaDatCu")) { var t = Gtp.Norm(Str(r["t"])); if (t == "" || t == Dc.To) m = Math.Max(m, Int(Gtp.Norm(Str(r["s"])))); }
            }
            foreach (var h in MayDoc()) if (h.To == "" || h.To == Dc.To) foreach (var s in h.SoCu) m = Math.Max(m, Int(s));
            return m;
        }

        // ================= THỬA CŨ =================
        public static ThuaCu Cu(Parcel p, GRow r)
        {
            r = r ?? p.Row ?? RowBySo(p.So);
            var c = new ThuaCu
            {
                So = r != null && r.So != "" ? r.So : p.So, To = r != null && !string.IsNullOrEmpty(r.To) ? r.To : Dc.To,
                Ld = p.Ld != "" ? p.Ld : (r != null ? r.Kh ?? "" : ""), Chu = p.Chu != "" ? p.Chu : (r != null ? r.Chu ?? "" : ""),
                DiaChi = p.DiaChi != "" ? p.DiaChi : (r != null ? r.DiaChi ?? "" : ""), Dt = p.Dt, Rings = p.Rings, Row = r, Lp = p.Lp
            };
            Bo(c); return c;
        }
        // bổ sung từ tệp thuộc tính: diện tích ghi trong tệp, chủ sử dụng, các loại đất chi tiết, số phát hành GCN (sổ địa chính)
        static void Bo(ThuaCu c)
        {
            var r = c.Row; if (G == null || r == null) return;
            c.DtGtp = r.Dt; if (r.OwnerIds.Count > 0) c.NguoiId = r.OwnerIds[0]; c.ChuDc = r.ChuDc ?? "";
            if (G.Has("DaMucDichSuDung", "thuaDatId")) c.Mds = Q("select * from DaMucDichSuDung where thuaDatId=? order by daMucDichSuDungId", r.Id);
            if (G.Has("TbTrangSoDiaChinh", "soHieuToBanDo", "soThuTuThua", "soPhatHanh"))
                try { var t = Q("select soPhatHanh as ph, " + (G.Has("TbTrangSoDiaChinh", "soVaoSo") ? "soVaoSo" : "null") + " as vs from TbTrangSoDiaChinh where soHieuToBanDo=? and soThuTuThua=? limit 1", SoVal(c.To), SoVal(c.So)).FirstOrDefault(); if (t != null) { c.Gcs = Str(t["ph"]).Trim(); c.Vs = Str(t["vs"]).Trim(); } } catch { }
        }
        static string CodeOf(ThuaCu c, Dictionary<string, object> md)
        {
            var id = Str(Get(md, "daMucDichSuDungId")); if (c.Row != null) { var g = c.Row.Mds.FirstOrDefault(x => Str(x.Id) == id); if (g != null && g.Code != null) return g.Code; }
            foreach (var k in new[] { "loaiMucDichSuDungId", "loaiMucDichSuDungKiemKeId" }) { long l; if (long.TryParse(Str(Get(md, k)), out l)) { var s = LoaiDatMa.Get(l); if (s != null) return s; } }
            return null;
        }

        // ================= TÁCH THỬA =================
        // nhóm các thửa mới (đã dựng lại thửa theo ranh hiện tại) theo thửa gốc. cu: thửa trên bản đồ trước khi dựng lại (nếu đã tạo thửa trước khi vẽ ranh tách)
        public static List<NhomTach> NhomTach(List<Parcel> chon, List<Parcel> cu, out string loi)
        {
            loi = ""; var o = new List<NhomTach>(); var con = new List<Parcel>();
            foreach (var p in chon)
            {
                if (o.Any(z => z.Moi.Contains(p))) continue;
                var goc = TimGoc(p, cu ?? new List<Parcel>()); if (goc == null) { con.Add(p); continue; }
                var ins = Dc.P.Where(q => !q.Virtual && !q.FromGtp && In(goc.Rings, q.Lp[0], q.Lp[1])).ToList();
                var g = new NhomTach { Goc = goc }; g.Moi = chon.Where(ins.Contains).ToList();
                g.Moi.AddRange(ins.Where(q => !g.Moi.Contains(q)).OrderByDescending(q => Math.Round(q.Lp[1] / 3.0)).ThenBy(q => q.Lp[0]));
                o.Add(g);
            }
            // chưa có hình thửa gốc (chưa tạo thửa trước khi vẽ ranh tách, tệp thuộc tính không có hình thửa): thửa gốc = các thửa đã chạm hợp lại
            if (con.Count > 0)
            {
                if (con.Count < 2) { loi = "Thửa " + Ten(con[0]) + " chưa bị chia bởi ranh tách nào. Vẽ ranh tách trên level ranh thửa (level " + Cfg.I.LvRanh + ") nối kín từ ranh tới ranh, rồi chọn lại lệnh Tách thửa và chạm vào trong từng thửa mới."; return null; }
                var lab = con.FirstOrDefault(q => q.Row != null) ?? con.FirstOrDefault(q => q.So != "");
                if (lab == null) { loi = "Chưa xác định được thửa gốc của các thửa vừa chạm (không thấy nhãn số thửa gốc, chưa kết nối tệp thuộc tính)."; return null; }
                var rings = Vong(con); if (rings == null) { loi = "Các thửa vừa chạm không liền nhau – không phải các phần của cùng một thửa gốc."; return null; }
                var gc = Cu(lab, lab.Row); gc.Rings = rings; gc.Dt = Area(rings); gc.DtGtp = null; gc.Lp = Geo.LabelPt(rings);
                o.Add(new NhomTach { Goc = gc, Moi = con });
            }
            foreach (var g in o)
            {
                if (g.Moi.Count < 2) { loi = "Thửa " + g.Goc.So + " chưa bị chia: ranh tách phải nối kín từ ranh tới ranh trên level ranh thửa (level " + Cfg.I.LvRanh + ")."; return null; }
                double s = g.Moi.Sum(q => q.Dt), a = Area(g.Goc.Rings);
                if (Math.Abs(s - a) > Math.Max(0.5, a * 0.002)) g.CanhBao = "Tổng diện tích các thửa mới " + Dc.FD(s) + " m² khác diện tích thửa gốc " + Dc.FD(a) + " m² – kiểm tra ranh tách";
            }
            return o;
        }
        static ThuaCu TimGoc(Parcel p, List<Parcel> cu)
        {
            double x = p.Lp[0], y = p.Lp[1];
            // 1) thửa trên bản đồ trước khi vẽ ranh tách, lớn hơn thửa mới và chứa thửa mới
            var o = cu.Where(q => q.Rings != null && In(q.Rings, x, y) && q.Dt > p.Dt + Tol(q.Dt)).OrderBy(q => q.Dt).FirstOrDefault();
            if (o != null) return Cu(o, o.Row);
            // 2) hình thửa trong tệp thuộc tính
            if (G != null)
            {
                var r = Dc.SheetRows().Where(q => q.Rings != null && q.Rings.Count > 0 && q.Rings[0].Length >= 6 && In(q.Rings, x, y)).Select(q => new { q, a = Area(q.Rings) })
                    .Where(z => z.a > p.Dt + Tol(z.a)).OrderBy(z => z.a).Select(z => z.q).FirstOrDefault();
                if (r != null)
                {
                    var c = new ThuaCu { So = r.So, To = !string.IsNullOrEmpty(r.To) ? r.To : Dc.To, Ld = r.Kh ?? "", Chu = r.Chu ?? "", DiaChi = r.DiaChi ?? "", Dt = Area(r.Rings), Rings = r.Rings, Row = r, Lp = Geo.LabelPt(r.Rings) };
                    Bo(c); return c;
                }
            }
            return null;
        }
        // tách: gán số, loại đất cho thửa mới, ghi tệp thuộc tính, vẽ nhãn, lưu lịch sử trên máy
        public static string ApDungTach(NhomTach g, List<string> so, List<string> ld, bool ghiGtp, bool nhan)
        {
            var c = g.Goc; var moi = g.Moi;
            var kq = CatVung(c.Rings);   // đầu ranh tách chạm ranh thửa: cắt ranh đúng tại đó trước khi ghi liên kết thửa – đường ranh
            for (int i = 0; i < moi.Count; i++) { moi[i].So = so[i]; moi[i].Ld = ld[i]; moi[i].Chu = c.Chu; moi[i].DiaChi = c.DiaChi; if (moi[i].Dtpl != null) moi[i].Dtpl = null; }
            string gw = ""; if (ghiGtp && G != null && !Dc.DocLap) { gw = GhiTach(c, moi); var tp = Dc.LamMoiTopo(kq, false); if (tp != "") gw += " · " + tp; }
            var goc = new[] { new KeyValuePair<List<double[]>, string>(c.Rings, c.So) };   // ô nhãn cũ của thửa gốc tìm thêm theo hình thửa gốc
            if (nhan) Dc.DrawLabels(moi, true, goc); else Dc.ChuyenNhanCu(moi, goc);        // nhãn cũ của thửa gốc luôn chuyển sang lớp nhãn cũ
            Dc.SaveLocal(); MayGhi("tach", new List<ThuaCu> { c }, moi); Dc.Notify();
            return "Đã tách thửa " + c.So + " (" + Dc.FD(c.DtGtp ?? c.Dt) + " m²) thành " + moi.Count + " thửa: " + string.Join(", ", moi.Select(q => Ten(q)).ToArray()) + (kq != null && kq.Nut + kq.Khop > 0 ? " · " + kq.Bao() : "") + Dc.NhanCuBao() + (gw != "" ? " · " + gw : "");
        }
        // cắt ranh đúng tại giao điểm trong phạm vi thửa gốc (đầu ranh tách chạm ranh thửa…), trên chính mạng ranh đang dùng:
        // thửa mới giữ nguyên, liên kết thửa – đường ranh ghi theo các đoạn đã cắt
        static CatKq CatVung(List<double[]> rings)
        {
            if (!Cfg.I.CatGiao || Dc.Topo == null || rings == null || rings.Count == 0 || Dc.Ranh.Count == 0 || !Ms.Connected) return null;
            try
            {
                var bb = Geo.BBox(rings[0]); double m = Math.Max(CatRanh.Tol * 10, 0.5);
                var kq = CatRanh.Chay(Dc.Ranh, new[] { bb[0] - m, bb[1] - m, bb[2] + m, bb[3] + m }, true);
                if (kq.Thay.Count > 0) Dc.ThayRanh(kq); return kq;
            }
            catch (Exception ex) { Program.Log(ex); return null; }
        }
        static void KiemTep() { if (G.ChangedOnDisk) throw new Exception("Tệp " + G.Name + " vừa bị chương trình khác sửa – không ghi đè. Đóng chương trình đó rồi kết nối lại tệp thuộc tính."); }
        static string GhiTach(ThuaCu c, List<Parcel> moi)
        {
            KiemTep(); var src = c.Row != null ? Q("select * from ThuaDat where thuaDatId=?", c.Row.Id).FirstOrDefault() : null;
            G.Db.Exec("savepoint m8tach");
            try
            {
                foreach (var p in moi)
                {
                    var nid = ThemThua(src, p); p.Gid = nid;
                    var map = ThemMdTach(nid, c, p); G.CanDienTichMd(nid, Math.Round(p.Dt, Cfg.I.DtLe), false);
                    if (src != null) ChepLienKet(c.Row.Id, nid, null, null, map);
                    LuuThuaCu(nid, c);
                }
                if (src != null) XoaThua(c.Row.Id);
                G.Db.Exec("release m8tach");
            }
            catch { try { G.Db.Exec("rollback to m8tach"); G.Db.Exec("release m8tach"); } catch { } throw; }
            G.Dirty = true; Dc.GRows = G.Rows(); Dc.Link(); foreach (var p in moi) Dc.GhiTopo(p);
            G.Save(G.Path_);
            return "tệp thuộc tính: thêm " + moi.Count + " thửa mới" + (src != null ? ", thửa " + c.So + " lưu vào thửa cũ" : " (thửa gốc " + c.So + " chưa có trong tệp)") + " · đã lưu " + G.Name + Dc.BaoMaTam();
        }

        // ================= HỢP THỬA =================
        static HashSet<string> Canh(Parcel p) { var o = new HashSet<string>(); if (p.RingEdges != null) foreach (var s in p.RingEdges) o.UnionWith(s); else if (p.Edges != null) o.UnionWith(p.Edges); return o; }
        public static NhomHop NhomHop(List<Parcel> chon, out string loi)
        {
            loi = "";
            if (chon.Count < 2) { loi = "Chạm vào trong ít nhất 2 thửa cần hợp."; return null; }
            if (Dc.Topo == null || chon.Any(p => p.FromGtp || p.Virtual)) { loi = "Hợp thửa cần thửa dựng từ ranh trên bản đồ (Bản đồ → Tạo thửa)."; return null; }
            var E = chon.ToDictionary(p => p, Canh);
            var seen = new HashSet<Parcel> { chon[0] }; var q = new Queue<Parcel>(); q.Enqueue(chon[0]);
            while (q.Count > 0) { var a = q.Dequeue(); foreach (var b in chon) if (!seen.Contains(b) && E[a].Overlaps(E[b])) { seen.Add(b); q.Enqueue(b); } }
            if (seen.Count < chon.Count) { loi = "Các thửa " + string.Join(", ", chon.Where(p => !seen.Contains(p)).Select(z => Ten(z)).ToArray()) + " không liền với các thửa còn lại (không có ranh chung) – không hợp được."; return null; }
            var cnt = new Dictionary<string, int>(); foreach (var p in chon) foreach (var k in E[p]) { int c; cnt.TryGetValue(k, out c); cnt[k] = c + 1; }
            var g = new NhomHop { Cu = chon }; foreach (var kv in cnt) if (kv.Value >= 2) g.Chung.Add(kv.Key);
            var chus = chon.Select(p => (p.Chu ?? "").Trim()).Where(s => s != "").Distinct().ToList();
            if (chus.Count > 1) g.CanhBao = "Các thửa có chủ sử dụng khác nhau (" + string.Join("; ", chus.ToArray()) + ") – kiểm tra lại trước khi hợp";
            return g;
        }
        public static string HopLd(List<Parcel> cu)
        {
            var o = new List<string>(); foreach (var p in cu) foreach (var c in Codes(p.Ld)) if (!o.Contains(c)) o.Add(c);
            return string.Join("+", o.ToArray());
        }
        // hợp: ranh chung sang lớp ranh cũ, dựng lại thửa, gán số, ghi tệp thuộc tính, vẽ nhãn
        public static string ApDungHop(NhomHop g, string so, string ld, bool ghiGtp, bool nhan, out Parcel moi, out List<ThuaCu> cuOut)
        {
            moi = null; var cu = g.Cu.Select(p => Cu(p, p.Row)).ToList(); cuOut = cu; var lps = g.Cu.Select(p => p.Lp).ToList();
            int nCanh; var ids = DoiRanhChung(g.Chung, Cfg.I.HopLop, out nCanh);
            if (nCanh == 0) return "Không tìm thấy đoạn ranh chung giữa các thửa trên level ranh thửa (level " + Cfg.I.LvRanh + ") – có thể ranh nằm trong ô (cell)";
            Dc.Build(false);
            Parcel m = null; foreach (var lp in lps) { m = Dc.Find(lp[0], lp[1]); if (m != null) break; }
            if (m == null) return "Đã chuyển ranh chung sang level " + Cfg.I.HopLop + " nhưng chưa dựng được thửa sau khi hợp";
            double sum = cu.Sum(c => c.Dt);
            string cb = Math.Abs(m.Dt - sum) > Math.Max(0.5, sum * 0.002) ? " ⚠ diện tích sau hợp " + Dc.FD(m.Dt) + " m² khác tổng các thửa cũ " + Dc.FD(sum) + " m² – kiểm tra đoạn ranh chung còn sót" : "";
            m.So = so; m.Ld = ld; m.Chu = cu[0].Chu; m.DiaChi = cu[0].DiaChi; m.Dtpl = null;
            string gw = ""; if (ghiGtp && G != null && !Dc.DocLap) gw = GhiHop(cu, m, ids);
            var goc = cu.Select(z => new KeyValuePair<List<double[]>, string>(z.Rings, z.So)).ToList();
            if (nhan) Dc.DrawLabels(new[] { m }, true, goc); else Dc.ChuyenNhanCu(new[] { m }, goc);
            Dc.SaveLocal(); MayGhi("hop", cu, new List<Parcel> { m }); Dc.Notify(); moi = m;
            return "Đã hợp thửa " + string.Join(", ", cu.Select(c => c.So).ToArray()) + " thành thửa " + so + " (" + Dc.FD(m.Dt) + " m²) · ranh chung chuyển sang level " + Cfg.I.HopLop + Dc.NhanCuBao() + (gw != "" ? " · " + gw : "") + cb;
        }
        static string GhiHop(List<ThuaCu> cu, Parcel m, HashSet<long> newIds)
        {
            KiemTep(); var r1 = cu.Select(c => c.Row).FirstOrDefault(r => r != null);
            var src = r1 != null ? Q("select * from ThuaDat where thuaDatId=?", r1.Id).FirstOrDefault() : null;
            G.Db.Exec("savepoint m8hop");
            try
            {
                var nid = ThemThua(src, m); m.Gid = nid;
                var map = ThemMdHop(nid, cu, m); G.CanDienTichMd(nid, Math.Round(m.Dt, Cfg.I.DtLe), false);
                var nguoi = new HashSet<string>(); var dchi = new HashSet<string>();
                foreach (var c in cu) if (c.Row != null) ChepLienKet(c.Row.Id, nid, nguoi, dchi, map);
                foreach (var c in cu) LuuThuaCu(nid, c);
                foreach (var c in cu) if (c.Row != null) XoaThua(c.Row.Id);
                G.Db.Exec("release m8hop");
            }
            catch { try { G.Db.Exec("rollback to m8hop"); G.Db.Exec("release m8hop"); } catch { } throw; }
            G.Dirty = true; Dc.GRows = G.Rows(); Dc.Link();
            // ThuaDatTopo: thửa mới và các thửa có đường ranh vừa vẽ lại
            foreach (var p in Dc.P) if (p == m || (p.Gid != null && Dc.TopoIds(p).Overlaps(newIds))) Dc.GhiTopo(p);
            G.Save(G.Path_);
            return "tệp thuộc tính: thêm thửa " + m.So + ", " + cu.Count + " thửa cũ (" + string.Join(", ", cu.Select(c => c.So).ToArray()) + ") lưu vào thửa cũ · đã lưu " + G.Name + Dc.BaoMaTam();
        }
        // chuyển các đoạn ranh chung (khoá cạnh "i_j" của mạng ranh) sang lớp ranh cũ: phần tử nằm trọn trên ranh chung thì đổi lớp (giữ nguyên phần tử),
        // phần tử chỉ có một phần trên ranh chung thì tách: phần còn lại giữ lớp cũ, phần ranh chung vẽ sang lớp ranh cũ. Trả về mã các phần tử mới vẽ.
        public static HashSet<long> DoiRanhChung(HashSet<string> chung, int lvCu, out int nCanh)
        {
            var R = Dc.Topo; var outIds = new HashSet<long>(); nCanh = 0; if (R == null || chung.Count == 0) return outIds;
            var N = R.Nodes; double tol = Math.Max(Cfg.I.Tol, 0.001) * 1.5, cs = Math.Max(tol * 200, 2);
            var grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < N.Count; i++) { long k = GK(N[i][0], N[i][1], cs); List<int> L; if (!grid.TryGetValue(k, out L)) grid[k] = L = new List<int>(); L.Add(i); }
            Ms.EnsureLevel(lvCu);
            var daVe = new HashSet<string>();
            foreach (var m in Dc.Ranh.ToList())
            {
                if (m.Parent != null || m.El == null) continue;
                var parts = new List<KeyValuePair<bool, List<int>>>(); bool hit = false, all = true;
                foreach (var l in m.Lines)
                {
                    var seq = new List<int>();
                    for (int i = 0; i + 3 < l.Length; i += 2) foreach (var n in NutTren(l[i], l[i + 1], l[i + 2], l[i + 3], N, grid, cs, tol)) if (seq.Count == 0 || seq[seq.Count - 1] != n) seq.Add(n);
                    if (seq.Count < 2) { all = false; continue; }
                    var rs = new List<KeyValuePair<bool, List<int>>>();
                    for (int j = 0; j + 1 < seq.Count; j++)
                    {
                        int a = seq[j], b = seq[j + 1]; bool sh = chung.Contains(EK(a, b)); if (sh) hit = true; else all = false;
                        if (rs.Count == 0 || rs[rs.Count - 1].Key != sh) rs.Add(new KeyValuePair<bool, List<int>>(sh, new List<int> { a }));
                        rs[rs.Count - 1].Value.Add(b);
                    }
                    // đường khép kín: nối đoạn cuối với đoạn đầu khi cùng loại
                    if (rs.Count > 1 && seq[0] == seq[seq.Count - 1] && rs[0].Key == rs[rs.Count - 1].Key)
                    { var last = rs[rs.Count - 1]; last.Value.AddRange(rs[0].Value.Skip(1)); rs[0] = last; rs.RemoveAt(rs.Count - 1); }
                    parts.AddRange(rs);
                }
                if (!hit) continue;
                var keys = parts.Where(x => x.Key).SelectMany(x => Pairs(x.Value)).ToList();
                if (all && !keys.Any(daVe.Contains) && Ms.SetLevel(m.El, lvCu)) { foreach (var k in keys) daVe.Add(k); nCanh += keys.Count; continue; }
                Ms.Delete(m.El);
                foreach (var pr in parts)
                {
                    if (!pr.Key) { var e = Ms.AddLine(Pts(pr.Value, N), m.Lv, m.Color, m.Weight); long id = Ms.Id(e); if (id > 0) outIds.Add(id); continue; }
                    var cur = new List<int>();
                    for (int j = 0; j + 1 < pr.Value.Count; j++)
                    {
                        int a = pr.Value[j], b = pr.Value[j + 1]; nCanh++;
                        if (daVe.Add(EK(a, b))) { if (cur.Count == 0) cur.Add(a); cur.Add(b); }
                        else if (cur.Count > 1) { Ms.AddLine(Pts(cur, N), lvCu, m.Color, m.Weight); cur = new List<int>(); }
                        else cur.Clear();
                    }
                    if (cur.Count > 1) Ms.AddLine(Pts(cur, N), lvCu, m.Color, m.Weight);
                }
            }
            return outIds;
        }
        static string EK(int a, int b) { return Math.Min(a, b) + "_" + Math.Max(a, b); }
        static IEnumerable<string> Pairs(List<int> v) { for (int j = 0; j + 1 < v.Count; j++) yield return EK(v[j], v[j + 1]); }
        static long GK(double x, double y, double cs) { return (long)Math.Floor(x / cs) * 2000003L + (long)Math.Floor(y / cs); }
        static double[] Pts(List<int> v, List<double[]> N) { var o = new double[v.Count * 2]; for (int i = 0; i < v.Count; i++) { o[2 * i] = N[v[i]][0]; o[2 * i + 1] = N[v[i]][1]; } return o; }
        // các nút của mạng ranh nằm trên đoạn a→b (cả hai đầu), xếp theo thứ tự từ a tới b
        static List<int> NutTren(double ax, double ay, double bx, double by, List<double[]> N, Dictionary<long, List<int>> grid, double cs, double tol)
        {
            var res = new List<KeyValuePair<double, int>>(); double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy, len = Math.Sqrt(l2);
            long i0 = (long)Math.Floor((Math.Min(ax, bx) - tol) / cs), i1 = (long)Math.Floor((Math.Max(ax, bx) + tol) / cs), j0 = (long)Math.Floor((Math.Min(ay, by) - tol) / cs), j1 = (long)Math.Floor((Math.Max(ay, by) + tol) / cs);
            for (long i = i0; i <= i1; i++) for (long j = j0; j <= j1; j++)
                {
                    List<int> L; if (!grid.TryGetValue(i * 2000003L + j, out L)) continue;
                    foreach (int n in L)
                    {
                        double px = N[n][0], py = N[n][1], t = l2 > 0 ? ((px - ax) * dx + (py - ay) * dy) / l2 : 0;
                        if (len > 0 && (t * len < -tol || (t - 1) * len > tol)) continue; double tc = t < 0 ? 0 : t > 1 ? 1 : t;
                        if (Geo.Dist(ax + tc * dx, ay + tc * dy, px, py) <= tol) res.Add(new KeyValuePair<double, int>(t, n));
                    }
                }
            return res.OrderBy(z => z.Key).Select(z => z.Value).ToList();
        }

        // ================= GHI TỆP THUỘC TÍNH =================
        // các trường của thửa gốc không chép sang thửa mới (mã, giấy tờ, diện tích pháp lý, số liệu cũ, đăng ký…)
        static readonly string[] CLR = { "maThua", "maDinhDanh", "thuaDatFTS", "saoLuuId", "ghiChu", "dienTichPhapLy", "soHieuToBanDoCu", "soThuTuThuaCu", "dienTichCu", "loaiDatCu", "soHieuThuaTam",
            "nguonGocHinhThanh", "thoiDiemHinhThanh", "dienTichTrenGiayTo", "loaiGiayToHienCo", "loaiDatTrenGiayTo", "tinhHinhThayDoiRanhGioi", "soPhatHanhCu", "soVaoSoCu", "ngayCapGiayCu", "mucDichSuDungCu",
            "nguonGocSuDungCu", "ngayVaoSoDiaChinh", "trangThaiDangKy", "hoSoThuTucSo", "ngayTiepNhan", "loaiDon", "dotDangKyId", "dienTichTrongQuyHoach", "dienTichNgoaiQuyHoach", "dienTichDenBu",
            "dienTichQuyHoachCu", "dienTichQuyHoachTaiSan", "dienTichQuyHoachDuongDien", "dienTichTrongQuyHoachDuongDien", "dienTichNgoaiQuyHoachDuongDien", "dienTichQuyNgoaiHoachDuongDien",
            "giayToNguonGocSuDung", "nghiaVuTaiChinh", "tranhChap", "duDieuKien", "hanCheSuDung", "quyenSuDungHanChe", "loaiHinhBienDong" };
        static object ThemThua(Dictionary<string, object> src, Parcel p)
        {
            var T = G.Cols["ThuaDat"]; double dt = Math.Round(p.Dt, Cfg.I.DtLe); var f = new Dictionary<string, object>();
            foreach (var k in CLR) f[k] = null;
            f["soThuTuThua"] = SoVal(p.So); if (Dc.To != "") f["soHieuToBanDo"] = SoVal(Dc.To); f["dienTich"] = dt;
            foreach (var k in new[] { "dienTichBanDo", "dienTichDaMucDichSuDung", "dienTichChuan" }) if (src != null && Get(src, k) != null) f[k] = dt;
            f["mucDichSuDungGhep"] = p.Ld == "" ? null : p.Ld.Replace("+", " + ");
            if (p.DiaChi != "") f["diaChi"] = p.DiaChi;
            if (T.Contains("TamX")) { f["TamX"] = p.Lp[0]; f["TamY"] = p.Lp[1]; }
            if (T.Contains("geom")) f["geom"] = Gtp.Wkb(p.Rings);
            return src != null ? G.CopyRow("ThuaDat", "thuaDatId", src, f) : G.NewParcel(f);
        }
        static object ChepMd(object nid, Dictionary<string, object> m, double? a, bool chinh, string so)
        {
            return G.CopyRow("DaMucDichSuDung", "daMucDichSuDungId", m, new Dictionary<string, object> { { "thuaDatId", nid }, { "dienTich", a }, { "laMucDichChinh", chinh ? 1L : 0L },
                { "soThuTuThua", SoVal(so) }, { "soHieuToBanDo", Dc.To != "" ? SoVal(Dc.To) : Get(m, "soHieuToBanDo") }, { "dienTichPhapLy", null }, { "saoLuuId", null } });
        }
        static void Ghi(Dictionary<string, object> map, Dictionary<string, object> md, object moi) { var k = md != null ? Str(Get(md, "daMucDichSuDungId")) : ""; if (k != "" && !map.ContainsKey(k)) map[k] = moi; }
        // loại đất chi tiết của thửa mới khi tách: theo loại đất của thửa mới, lấy dòng cùng loại của thửa gốc; một loại thì ghi cả diện tích, nhiều loại thì chia theo tỷ lệ.
        // Trả về: mã loại đất chi tiết của thửa gốc → mã dòng mới tương ứng (để đăng ký, nguồn gốc sử dụng chép theo)
        static Dictionary<string, object> ThemMdTach(object nid, ThuaCu c, Parcel p)
        {
            var map = new Dictionary<string, object>(); if (!G.Has("DaMucDichSuDung", "thuaDatId")) return map;
            var codes = Codes(p.Ld); double dt = Math.Round(p.Dt, Cfg.I.DtLe), dt0 = c.DtGtp ?? c.Dt;
            Func<Dictionary<string, object>, double?> ti = m => { var a = Num(Get(m, "dienTich")); return a.HasValue && dt0 > 0 ? Math.Round(a.Value * dt / dt0, Cfg.I.DtLe) : (double?)null; };
            if (codes.Length == 0) { for (int i = 0; i < c.Mds.Count; i++) Ghi(map, c.Mds[i], ChepMd(nid, c.Mds[i], c.Mds.Count == 1 ? dt : ti(c.Mds[i]), i == 0, p.So)); if (c.Mds.Count == 0) G.EnsureMd(nid); return map; }
            for (int i = 0; i < codes.Length; i++)
            {
                var m = c.Mds.FirstOrDefault(z => CodeOf(c, z) == codes[i]) ?? (c.Mds.Count == 1 && codes.Length == 1 ? c.Mds[0] : null);
                double? a = codes.Length == 1 ? dt : (m != null ? ti(m) : null);
                if (m != null) Ghi(map, m, ChepMd(nid, m, a, i == 0, p.So)); else G.AddMd(nid, LoaiDatMa.Id(codes[i]), a, i == 0, p.So, Dc.To, codes[i]);
            }
            return map;
        }
        // loại đất chi tiết khi hợp: gộp theo loại đất, cộng diện tích từng loại
        static Dictionary<string, object> ThemMdHop(object nid, List<ThuaCu> cu, Parcel m)
        {
            var map = new Dictionary<string, object>(); if (!G.Has("DaMucDichSuDung", "thuaDatId")) return map;
            var codes = Codes(m.Ld); double dt = Math.Round(m.Dt, Cfg.I.DtLe);
            var pick = new Dictionary<string, Dictionary<string, object>>(); var sum = new Dictionary<string, double?>(); var ord = new List<string>(); var nhom = new Dictionary<string, List<Dictionary<string, object>>>();
            foreach (var c in cu) foreach (var md in c.Mds)
                {
                    var k = CodeOf(c, md) ?? ""; if (!pick.ContainsKey(k)) { pick[k] = md; sum[k] = 0; ord.Add(k); nhom[k] = new List<Dictionary<string, object>>(); }
                    nhom[k].Add(md); var a = Num(Get(md, "dienTich")); sum[k] = sum[k].HasValue && a.HasValue ? sum[k] + a : null;
                }
            if (codes.Length == 0) codes = ord.Where(k => k != "").ToArray();
            for (int i = 0; i < codes.Length; i++)
            {
                Dictionary<string, object> md; pick.TryGetValue(codes[i], out md);
                double? a = codes.Length == 1 ? dt : (sum.ContainsKey(codes[i]) && sum[codes[i]].HasValue ? Math.Round(sum[codes[i]].Value, Cfg.I.DtLe) : (double?)null);
                if (md != null) { var moi = ChepMd(nid, md, a, i == 0, m.So); foreach (var x in nhom[codes[i]]) Ghi(map, x, moi); }
                else G.AddMd(nid, LoaiDatMa.Id(codes[i]), a, i == 0, m.So, Dc.To, codes[i]);
            }
            if (codes.Length == 0) G.EnsureMd(nid);
            return map;
        }
        // chủ sử dụng (DangKyThua), nguồn gốc sử dụng (NguonGocGiaoDat), địa chỉ (LienKetDiaChi) của thửa cũ chép sang thửa mới.
        // Đăng ký như gCadas: mỗi chủ một dòng trên từng loại đất chi tiết của thửa mới. Khi hợp không chép trùng người / địa chỉ.
        static void ChepLienKet(object id0, object nid, HashSet<string> nguoi, HashSet<string> dchi, Dictionary<string, object> map)
        {
            if (G.Has("DangKyThua", "thuaDatId", "nguoiId"))
                foreach (var g in Q("select * from DangKyThua where thuaDatId=? order by " + Sqlite.Q(G.Cols["DangKyThua"][0]), id0).GroupBy(r => Str(Get(r, "nguoiId"))))
                {
                    if (g.Key == "" || (nguoi != null && !nguoi.Add(g.Key))) continue;
                    G.DangKy(nid, Get(g.First(), "nguoiId"), g.First());
                }
            if (G.Has("NguonGocGiaoDat", "thuaDatId"))
            {
                var pk = G.Cols["NguonGocGiaoDat"][0]; var mds = G.MdCua(nid, true); var da = new HashSet<string>();
                foreach (var r in Q("select * from NguonGocGiaoDat where thuaDatId=?", id0))
                {
                    object md = null; var k0 = Str(Get(r, "daMucDichSuDungId")); if (k0 != "" && map != null) map.TryGetValue(k0, out md); if (md == null && mds.Count > 0) md = mds[0];
                    if (!da.Add(Str(md) + "|" + Str(Get(r, "loaiNguonGocSuDungDatId")))) continue;
                    G.CopyRow("NguonGocGiaoDat", pk, r, new Dictionary<string, object> { { "thuaDatId", nid }, { "daMucDichSuDungId", md }, { "saoLuuId", null } });
                }
            }
            if (G.Has("LienKetDiaChi", "thuaDatId"))
            {
                var pk = G.Cols["LienKetDiaChi"][0];
                foreach (var r in Q("select * from LienKetDiaChi where thuaDatId=?", id0))
                {
                    if (dchi != null) { var v = Str(Get(r, "diaChiId")); if (v != "" && !dchi.Add(v)) continue; }
                    G.CopyRow("LienKetDiaChi", pk, r, new Dictionary<string, object> { { "thuaDatId", nid }, { "saoLuuId", null } });
                }
            }
        }
        // thửa cũ của thửa mới (bảng ThuaDatCu, như dữ liệu sẵn có): tờ, số, diện tích, chủ, hình thửa; loại đất chi tiết sang DaMucDichSuDungCu
        static void LuuThuaCu(object nid, ThuaCu c)
        {
            if (!G.Has("ThuaDatCu", "thuaDatId", "soThuTuThuaCu")) return;
            var f = new Dictionary<string, object> { { "thuaDatCuId", null }, { "thuaDatId", nid }, { "soHieuToBanDoCu", c.To }, { "soThuTuThuaCu", c.So }, { "dienTichCu", Math.Round(c.DtGtp ?? c.Dt, 2) },
                { "ngayCapCu", "0001-01-01 00:00:00" }, { "tenChuCu", c.Chu == "" ? null : c.Chu }, { "nguoiId", c.NguoiId }, { "diaChiCu", c.DiaChi == "" ? null : c.DiaChi },
                { "soPhatHanhCu", c.Gcs == "" ? null : c.Gcs }, { "soVaoSoCu", c.Vs == "" ? null : c.Vs } };
            if (c.Rings != null) f["geom"] = Gtp.Wkb(c.Rings);
            var cuId = G.CopyRow("ThuaDatCu", "id", null, f);
            if (G.Has("DaMucDichSuDungCu", "thuaDatCuId", "thuaDatId"))
                foreach (var m in c.Mds) G.CopyRow("DaMucDichSuDungCu", "daMucDichSuDungId", m, new Dictionary<string, object> { { "thuaDatId", nid }, { "thuaDatCuId", cuId } });
        }
        static void XoaThua(object id)
        {
            foreach (var t in new[] { "DaMucDichSuDung", "ThuaDatTopo", "DangKyThua", "LienKetDiaChi", "NguonGocGiaoDat", "NhanDiaChinh" }) if (G.Has(t, "thuaDatId")) G.Db.Exec("delete from " + Sqlite.Q(t) + " where thuaDatId=?", id);
            G.Db.Exec("delete from ThuaDat where thuaDatId=?", id);
        }

        // ================= HỢP VÒNG (thửa gốc = các thửa mới ghép lại) =================
        // các thửa cùng mạng ranh (đỉnh chung trùng nhau): bỏ cặp cạnh ngược chiều, nối phần còn lại thành vòng ngoài + lỗ; null nếu không liền nhau
        public static List<double[]> Vong(List<Parcel> ps)
        {
            Func<double, double, string> K = (x, y) => Math.Round(x * 1000).ToString(IC) + "," + Math.Round(y * 1000).ToString(IC);
            var E = new List<double[]>();
            foreach (var p in ps) for (int r = 0; r < p.Rings.Count; r++)
                {
                    var g = p.Rings[r]; int n = g.Length / 2; if (n > 1 && g[0] == g[2 * n - 2] && g[1] == g[2 * n - 1]) n--; if (n < 3) continue;
                    bool ccw = Geo.Area(g) > 0, rev = (r == 0) != ccw;
                    for (int i = 0; i < n; i++) { int a = rev ? (n - i) % n : i, b = rev ? (n - i - 1 + n) % n : (i + 1) % n; E.Add(new[] { g[2 * a], g[2 * a + 1], g[2 * b], g[2 * b + 1] }); }
                }
            var all = new HashSet<string>(E.Select(e => K(e[0], e[1]) + ">" + K(e[2], e[3])));
            var keep = E.Where(e => !all.Contains(K(e[2], e[3]) + ">" + K(e[0], e[1]))).ToList();
            var outE = new Dictionary<string, List<double[]>>(); foreach (var e in keep) { var k = K(e[0], e[1]); List<double[]> L; if (!outE.TryGetValue(k, out L)) outE[k] = L = new List<double[]>(); L.Add(e); }
            var used = new HashSet<double[]>(); var rings = new List<double[]>();
            foreach (var e0 in keep)
            {
                if (used.Contains(e0)) continue; var ring = new List<double>(); var e = e0; int guard = 0;
                while (e != null && !used.Contains(e) && guard++ < 1000000) { used.Add(e); ring.Add(e[0]); ring.Add(e[1]); List<double[]> L; e = outE.TryGetValue(K(e[2], e[3]), out L) ? L.FirstOrDefault(z => !used.Contains(z)) : null; }
                if (ring.Count >= 6) rings.Add(ring.ToArray());
            }
            var outer = rings.Where(r => Geo.Area(r) > 0).ToList(); if (outer.Count != 1) return null;
            var o = new List<double[]> { outer[0] }; o.AddRange(rings.Where(r => Geo.Area(r) < 0)); return o;
        }

        // ================= BẢN VẼ TÁCH THỬA, HỢP THỬA (Mẫu số 22): DỮ LIỆU GỬI BỘ MÁY LẬP MẪU =================
        public static string Json(string loai, List<ThuaCu> cu, List<Parcel> moi)
        {
            var b = new StringBuilder("{\"loai\":" + Web.J(loai) + ",\"cu\":[");
            for (int i = 0; i < cu.Count; i++)
            {
                var c = cu[i]; if (i > 0) b.Append(',');
                b.Append("{\"so\":").Append(Web.J(c.So)).Append(",\"to\":").Append(Web.J(c.To)).Append(",\"dt\":").Append(Math.Round(c.DtGtp ?? c.Dt, 2).ToString("0.##", IC))
                 .Append(",\"ld\":").Append(Web.J(c.Ld)).Append(",\"chu\":").Append(Web.J(c.Chu)).Append(",\"tt\":").Append(Web.J(c.ChuDc)).Append(",\"dc\":").Append(Web.J(c.DiaChi)).Append(",\"gcs\":").Append(Web.J(c.Gcs));
                Vong(b, c.Rings); b.Append('}');
            }
            b.Append("],\"moi\":[");
            for (int i = 0; i < moi.Count; i++)
            {
                var p = moi[i]; if (i > 0) b.Append(',');
                b.Append("{\"so\":").Append(Web.J(p.So)).Append(",\"dt\":").Append(Math.Round(p.Dt, 2).ToString("0.##", IC)).Append(",\"ld\":").Append(Web.J(p.Ld)); Vong(b, p.Rings); b.Append('}');
            }
            return b.Append("]}").ToString();
        }
        static void Vong(StringBuilder b, List<double[]> rs)
        {
            b.Append(",\"o\":"); Arr(b, rs != null && rs.Count > 0 ? rs[0] : new double[0]); b.Append(",\"h\":[");
            for (int i = 1; rs != null && i < rs.Count; i++) { if (i > 1) b.Append(','); Arr(b, rs[i]); }
            b.Append(']');
        }
        static void Arr(StringBuilder b, double[] a) { b.Append('['); for (int i = 0; i < a.Length; i++) { if (i > 0) b.Append(','); b.Append(a[i].ToString("0.####", IC)); } b.Append(']'); }
        // các thửa mới đã chạm → thửa cũ đã lưu: trong tệp thuộc tính (ThuaDatCu có hình thửa), nếu không có thì lịch sử tách / hợp trên máy; null: chưa tách / hợp
        public static string LichSuJson(List<Parcel> chon) { return TuGtp(chon) ?? TuMay(chon); }
        static string TuGtp(List<Parcel> chon)
        {
            if (G == null || !G.Has("ThuaDatCu", "thuaDatId", "soThuTuThuaCu", "soHieuToBanDoCu", "geom")) return null;
            var ids = new HashSet<string>(chon.Where(p => p.Gid != null).Select(p => Str(p.Gid))); if (ids.Count == 0) return null;
            var all = Q("select * from ThuaDatCu where geom is not null");
            Func<Dictionary<string, object>, string> K = r => Gtp.Norm(Str(r["soHieuToBanDoCu"])) + "|" + Gtp.Norm(Str(r["soThuTuThuaCu"]));
            var keys = all.Where(r => ids.Contains(Str(r["thuaDatId"]))).Select(r => K(r)).Distinct().ToList(); if (keys.Count == 0) return null;
            var nhom = all.Where(r => keys.Contains(K(r))).ToList(); var moiIds = new HashSet<string>(nhom.Select(r => Str(r["thuaDatId"])));
            var moi = Dc.P.Where(p => p.Gid != null && moiIds.Contains(Str(p.Gid))).OrderBy(p => Int(p.So)).ThenBy(p => p.So, StringComparer.Ordinal).ToList(); if (moi.Count == 0) return null;
            var cu = keys.Select(k => TuDong(nhom.First(z => K(z) == k))).Where(c => c.Rings != null).OrderBy(c => Int(c.So)).ToList(); if (cu.Count == 0) return null;
            return Json(Loai(cu.Count, moi.Count), cu, moi);
        }
        static string Loai(int nCu, int nMoi) { return nCu == 1 && nMoi > 1 ? "tach" : nCu > 1 && nMoi == 1 ? "hop" : "tachhop"; }
        static ThuaCu TuDong(Dictionary<string, object> r)
        {
            var c = new ThuaCu { So = Gtp.Norm(Str(r["soThuTuThuaCu"])), To = Gtp.Norm(Str(r["soHieuToBanDoCu"])), Chu = Str(Get(r, "tenChuCu")).Trim(), DiaChi = Str(Get(r, "diaChiCu")).Trim(), Gcs = Str(Get(r, "soPhatHanhCu")).Trim(), NguoiId = Get(r, "nguoiId") };
            var d = Num(Get(r, "dienTichCu")); c.DtGtp = d; var g = Get(r, "geom") as byte[]; if (g != null) try { c.Rings = Gtp.WkbRead(g); } catch { }
            c.Dt = d ?? Area(c.Rings);
            if (c.NguoiId != null && G.Has("Nguoi", "nguoiId", "hoTen"))
                try
                {
                    var n = Q("select hoTen as h, " + (G.Has("Nguoi", "hoTen2") ? "hoTen2" : "null") + " as h2, " + (G.Has("Nguoi", "diaChi") ? "diaChi" : "null") + " as dc from Nguoi where nguoiId=?", c.NguoiId).FirstOrDefault();
                    if (n != null) { if (c.Chu == "") c.Chu = string.Join(" và ", new[] { Str(n["h"]), Str(n["h2"]) }.Where(s => s.Trim() != "").ToArray()); c.ChuDc = Str(n["dc"]).Trim(); }
                }
                catch { }
            if (G.Has("DaMucDichSuDungCu", "thuaDatCuId"))
            {
                var codes = new List<string>(); var md = Q("select * from DaMucDichSuDungCu where thuaDatCuId=?", r["id"]);
                foreach (var m in md.OrderByDescending(z => Str(Get(z, "laMucDichChinh")).Contains("1")))
                    foreach (var k in new[] { "loaiMucDichSuDungId", "loaiMucDichSuDungKiemKeId" }) { long l; if (long.TryParse(Str(Get(m, k)), out l)) { var s = LoaiDatMa.Get(l); if (s != null) { if (!codes.Contains(s)) codes.Add(s); break; } } }
                c.Ld = string.Join("+", codes.ToArray());
            }
            return c;
        }

        // ================= LỊCH SỬ TÁCH / HỢP TRÊN MÁY (khi chưa kết nối tệp thuộc tính) =================
        public class MayMuc { public string To = "", Loai = "", Json = ""; public List<string> SoMoi = new List<string>(), SoCu = new List<string>(); }
        static string MayTep { get { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "M8Pro", "tachhop"); Directory.CreateDirectory(d); return Path.Combine(d, Regex.Replace(Path.GetFileName(Dc.MapName ?? "x"), @"[^\w.-]", "_") + ".txt"); } }
        public static List<MayMuc> MayDoc()
        {
            var o = new List<MayMuc>();
            try { if (File.Exists(MayTep)) foreach (var l in File.ReadAllLines(MayTep, Encoding.UTF8)) { var a = l.Split('\t'); if (a.Length >= 5) o.Add(new MayMuc { To = a[0], Loai = a[1], SoMoi = a[2].Split(',').Where(s => s != "").ToList(), SoCu = a[3].Split(',').Where(s => s != "").ToList(), Json = a[4] }); } } catch { }
            return o;
        }
        static void MayGhi(string loai, List<ThuaCu> cu, List<Parcel> moi)
        {
            try { File.AppendAllText(MayTep, string.Join("\t", new[] { Dc.To, loai, string.Join(",", moi.Select(p => p.So).ToArray()), string.Join(",", cu.Select(c => c.So).ToArray()), Json(loai, cu, moi) }) + "\r\n", Encoding.UTF8); } catch (Exception ex) { Program.Log(ex); }
        }
        static string TuMay(List<Parcel> chon)
        {
            var so = new HashSet<string>(chon.Select(p => p.So).Where(s => s != "")); if (so.Count == 0) return null;
            var h = MayDoc().Where(m => (m.To == "" || m.To == Dc.To) && m.SoMoi.Any(so.Contains)).LastOrDefault(); return h != null ? h.Json : null;
        }
    }
}
