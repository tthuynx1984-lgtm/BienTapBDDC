// M8 Pro Địa chính – nhóm chức năng nghiệp vụ bổ sung (viết mới, độc lập). © Nguyễn Xuân Thủy
//  triển điểm đo · nối điểm thành ranh · chia thửa theo diện tích · đối chiếu nhãn với tệp thuộc tính
//  ghi tên chủ · diện tích trong/ngoài quy hoạch · đổi phông TCVN3/Unicode · tìm – thay chữ · danh sách công khai
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace M8Pro
{
    public static class Nv
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // ================= ĐIỂM ĐO =================
        public static Dictionary<string, double[]> Diem = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);   // tên → x(Đông), y(Bắc), h
        // đọc tệp toạ độ: mỗi dòng "tên X Y [H] [mã]" (cách nhau dấu cách, tab, phẩy, chấm phẩy). xBac: cột X là toạ độ Bắc (VN-2000)
        public static List<string[]> ReadPoints(string file, string order, bool xBac)
        {
            var cols = order.ToLowerInvariant().Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            int iTen = cols.IndexOf("tên"), iX = cols.IndexOf("x"), iY = cols.IndexOf("y"), iH = cols.IndexOf("h"), iMa = cols.IndexOf("mã");
            if (iX < 0 || iY < 0) throw new Exception("Thứ tự cột phải có X và Y");
            var o = new List<string[]>(); int n = 0;
            foreach (var raw in File.ReadAllLines(file))
            {
                var l = raw.Trim(); n++; if (l == "" || l.StartsWith("#") || l.StartsWith("//")) continue;
                var a = Regex.Split(l, @"[\t;]+|\s*,\s*|\s+").Where(z => z != "").ToArray(); if (a.Length <= Math.Max(iX, iY)) continue;
                double X, Y; if (!Num(a[iX], out X) || !Num(a[iY], out Y)) continue;   // bỏ dòng tiêu đề
                double x = xBac ? Y : X, y = xBac ? X : Y; string ten = iTen >= 0 && iTen < a.Length ? a[iTen] : n.ToString(); double h = 0; if (iH >= 0 && iH < a.Length) Num(a[iH], out h);
                Diem[ten] = new[] { x, y, h }; o.Add(new[] { ten, x.ToString("R", IC), y.ToString("R", IC), h.ToString("R", IC), iMa >= 0 && iMa < a.Length ? a[iMa] : "" });
            }
            return o;
        }
        static bool Num(string s, out double v) { return double.TryParse(s.Replace(',', '.'), NumberStyles.Float, IC, out v); }
        public static int DrawPoints(List<string[]> pts, bool ghiDoCao)
        {
            var c = Cfg.I; double h = c.H * .8, r = c.TyLe / 1000.0 * .4;
            foreach (var p in pts)
            {
                double x = double.Parse(p[1], IC), y = double.Parse(p[2], IC);
                Ms.AddCircle(x, y, r, c.DiemLop, c.DiemMau); Ms.AddText(p[0], x + h * .9, y + h * .6, h, 0, c.DiemLop, c.DiemMau);
                if (ghiDoCao && p[3] != "0") Ms.AddText(double.Parse(p[3], IC).ToString("0.00", IC), x + h * .9, y - h * .8, h * .8, 0, c.DiemLop, c.DiemMau);
            }
            return pts.Count;
        }
        // "1-2-3-4-1; 5-6-7" → các đường gấp khúc theo tên điểm
        public static List<double[]> Chains(string s)
        {
            var o = new List<double[]>();
            foreach (var ch in s.Split(new[] { ';', '\n', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var names = ch.Split(new[] { '-', ',', ' ', '>' }, StringSplitOptions.RemoveEmptyEntries); var L = new List<double>();
                foreach (var nm in names) { double[] p; if (!Diem.TryGetValue(nm.Trim(), out p)) throw new Exception("Không có điểm “" + nm + "” – hãy triển điểm từ tệp toạ độ trước"); L.Add(p[0]); L.Add(p[1]); }
                if (L.Count >= 4) o.Add(L.ToArray());
            }
            return o;
        }
        public static int Connect(string s)
        {
            var lr = Cfg.Lvs(Cfg.I.LvRanh); int lv = lr.Count > 0 ? lr[0] : 10; var ch = Chains(s);
            foreach (var l in ch) Ms.AddLine(l, lv, -1, -1); Dc.Dirty = true; return ch.Count;
        }

        // ================= CHIA THỬA THEO DIỆN TÍCH =================
        // cắt đa giác bởi nửa mặt phẳng n·q <= t (le=true) hoặc >= t
        public static double[] Half(double[] ring, double nx, double ny, double t, bool le)
        {
            var o = new List<double>(); int n = ring.Length / 2;
            Func<int, double> f = i => (nx * ring[2 * i] + ny * ring[2 * i + 1] - t) * (le ? 1 : -1);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n; double fi = f(i), fj = f(j);
                if (fi <= 0) { o.Add(ring[2 * i]); o.Add(ring[2 * i + 1]); }
                if ((fi < 0 && fj > 0) || (fi > 0 && fj < 0)) { double k = fi / (fi - fj); o.Add(ring[2 * i] + (ring[2 * j] - ring[2 * i]) * k); o.Add(ring[2 * i + 1] + (ring[2 * j + 1] - ring[2 * i + 1]) * k); }
            }
            return o.ToArray();
        }
        static double AreaOf(List<double[]> rings, double nx, double ny, double t, bool le)
        {
            double a = 0; for (int i = 0; i < rings.Count; i++) { var h = Half(rings[i], nx, ny, t, le); var v = h.Length >= 6 ? Math.Abs(Geo.Area(h)) : 0; a += i == 0 ? v : -v; }
            return a;
        }
        // đường chia song song hướng (a→b), phần chứa điểm (px,py) có diện tích = target. Trả đoạn thẳng dài cắt qua thửa
        public static double[] SplitLine(List<double[]> rings, double ax, double ay, double bx, double by, double px, double py, double target)
        {
            double dx = bx - ax, dy = by - ay, L = Math.Sqrt(dx * dx + dy * dy); if (L < 1e-9) throw new Exception("Hai điểm chỉ hướng trùng nhau");
            dx /= L; dy /= L; double nx = -dy, ny = dx; var o = rings[0];
            double lo = double.MaxValue, hi = double.MinValue; for (int i = 0; i < o.Length; i += 2) { double v = nx * o[i] + ny * o[i + 1]; lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            double tot = AreaOf(rings, nx, ny, hi + 1, true); if (target <= 0 || target >= tot) throw new Exception("Diện tích cần tách phải nhỏ hơn diện tích thửa (" + Dc.FD(tot) + " m²)");
            bool le = nx * px + ny * py <= (lo + hi) / 2;   // phần cần tách nằm phía nào
            double a = lo, b = hi;
            for (int k = 0; k < 80; k++) { double m = (a + b) / 2, v = AreaOf(rings, nx, ny, m, le); if ((v < target) == le) a = m; else b = m; }
            double tt = (a + b) / 2, big = (hi - lo) + 1e4; var c = Geo.Centroid(o); double s = nx * c[0] + ny * c[1], cx = c[0] + nx * (tt - s), cy = c[1] + ny * (tt - s);
            // đoạn chia đủ dài: giới hạn theo khung thửa
            double w = 0; for (int i = 0; i < o.Length; i += 2) w = Math.Max(w, Math.Abs(dx * (o[i] - cx) + dy * (o[i + 1] - cy)));
            w += 1; return new[] { cx - dx * w, cy - dy * w, cx + dx * w, cy + dy * w };
        }
        public static double AreaSide(List<double[]> rings, double[] line, double px, double py)
        {
            double dx = line[2] - line[0], dy = line[3] - line[1], L = Math.Sqrt(dx * dx + dy * dy); double nx = -dy / L, ny = dx / L, t = nx * line[0] + ny * line[1];
            return AreaOf(rings, nx, ny, t, nx * px + ny * py <= t);
        }

        // ================= ĐỐI CHIẾU NHÃN VỚI TỆP THUỘC TÍNH =================
        static string NormLd(string s) { return string.Join("+", (s ?? "").ToUpperInvariant().Replace(" ", "").Split('+', ',').Where(x => x != "").OrderBy(x => x).ToArray()); }
        public static List<Err> CompareLabels(out List<Parcel> wrong)
        {
            var o = new List<Err>(); wrong = new List<Parcel>(); double tol = 0.5 * Math.Pow(10, -Cfg.I.DtLe) + 1e-6;
            foreach (var p in Dc.P)
            {
                if (p.FromGtp) continue; var r = p.Row; var msg = new List<string>();
                if (p.NhanSo == "" && p.DtNhan == null) msg.Add("chưa có nhãn");
                if (r == null) { if (Dc.G != null && !Dc.DocLap) msg.Add("chưa liên kết tệp thuộc tính"); }
                else
                {
                    if (p.NhanSo != "" && p.NhanSo != r.So) msg.Add("số thửa nhãn " + p.NhanSo + " ≠ tệp " + r.So);
                    if (p.NhanLd != "" && r.Kh != "" && !r.Kh.Contains("#") && NormLd(p.NhanLd) != NormLd(r.Kh)) msg.Add("loại đất nhãn " + p.NhanLd + " ≠ tệp " + r.Kh);
                    if (p.DtNhan.HasValue && r.Dt.HasValue && Math.Abs(p.DtNhan.Value - r.Dt.Value) > tol) msg.Add("diện tích nhãn " + Dc.FD(p.DtNhan.Value) + " ≠ tệp " + Dc.FD(r.Dt.Value));
                }
                if (r != null && r.Dt.HasValue && Math.Abs(Math.Round(p.Dt, Cfg.I.DtLe) - r.Dt.Value) > tol) msg.Add("diện tích bản đồ " + Dc.FD(p.Dt) + " ≠ tệp " + Dc.FD(r.Dt.Value));
                if (msg.Count > 0) { o.Add(new Err { Text = "Thửa " + (p.So == "" ? "?" : p.So) + ": " + string.Join("; ", msg.ToArray()), X = p.Lp[0], Y = p.Lp[1], P = p }); if (r != null && msg.Any(m => m.StartsWith("số") || m.StartsWith("loại") || m.StartsWith("diện tích nhãn"))) wrong.Add(p); }
            }
            if (Dc.G != null)
            {
                var used = new HashSet<GRow>(Dc.P.Where(p => p.Row != null).Select(p => p.Row));
                foreach (var r in Dc.SheetRows().Where(r => !used.Contains(r)))
                    o.Add(new Err { Text = "Thửa " + r.So + " có trong tệp thuộc tính nhưng không có trên bản đồ", X = r.TamX ?? 0, Y = r.TamY ?? 0, P = Dc.Virtual(r) });
            }
            return o;
        }

        // ================= GHI TÊN CHỦ LÊN BẢN ĐỒ =================
        public static int DrawOwners(IEnumerable<Parcel> list)
        {
            var c = Cfg.I; double h = c.H * .8; int n = 0;
            // xoá tên chủ cũ trong các thửa
            var old = Ms.Read(new[] { c.ChuLop }, new[] { 17 }); var set = new HashSet<Parcel>(list); var pg = new Dc.PGrid(Dc.P);
            foreach (var t in old) { var p = pg.Find(t.Cx, t.Cy); if (p != null && set.Contains(p)) Ms.Delete(t.El); }
            foreach (var p in set) { if (string.IsNullOrEmpty(p.Chu)) continue; Ms.AddText(p.Chu, p.Lp[0], p.Lp[1] - c.H * 3.2, h, 0, c.ChuLop, c.ChuMau); n++; }
            return n;
        }

        // ================= DIỆN TÍCH TRONG / NGOÀI QUY HOẠCH =================
        public class QhKq { public Parcel P; public double Trong, Ngoai; }
        public static List<double[]> QhRings(List<MEl> els)
        {
            var o = new List<double[]>();
            foreach (var m in els) foreach (var l in m.Lines) { int n = l.Length / 2; if (n < 3) continue; bool closed = Math.Abs(l[0] - l[2 * n - 2]) < 1e-6 && Math.Abs(l[1] - l[2 * n - 1]) < 1e-6; if (!closed && m.Kind != 6 && m.Kind != 14) continue; var r = closed ? l.Take(2 * n - 2).ToArray() : l; if (r.Length >= 6 && Math.Abs(Geo.Area(r)) > 1e-6) o.Add(r); }
            return o;
        }
        public static List<QhKq> Planning(List<Parcel> ps, List<double[]> qh)
        {
            var o = new List<QhKq>(); var qbb = qh.Select(Geo.BBox).ToList();
            foreach (var p in ps)
            {
                var b = p.BB; var near = new List<double[]>(); for (int i = 0; i < qh.Count; i++) { var q = qbb[i]; if (q[0] <= b[2] && q[2] >= b[0] && q[1] <= b[3] && q[3] >= b[1]) near.Add(qh[i]); }
                double trong = 0;
                if (near.Count > 0)
                {
                    var segs = new List<Seg>(); int tag = 0;
                    foreach (var r in p.Rings.Concat(near)) { int n = r.Length / 2; for (int i = 0; i < n; i++) { int j = (i + 1) % n; segs.Add(new Seg(r[2 * i], r[2 * i + 1], r[2 * j], r[2 * j + 1], tag)); } tag++; }
                    var T = Planar.Build(segs, 0.001);
                    foreach (var f in T.Faces)
                    {
                        var rings = new List<double[]> { f.Ring }; rings.AddRange(f.Holes); var lp = Geo.LabelPt(rings);
                        bool inP = Geo.InPoly(lp[0], lp[1], p.Rings[0]) && !p.Rings.Skip(1).Any(h => Geo.InPoly(lp[0], lp[1], h));
                        if (!inP || !near.Any(q => Geo.InPoly(lp[0], lp[1], q))) continue;
                        double a = Math.Abs(Geo.Area(f.Ring)); foreach (var h in f.Holes) a -= Math.Abs(Geo.Area(h)); trong += a;
                    }
                }
                if (trong > 1e-4) o.Add(new QhKq { P = p, Trong = Math.Min(trong, p.Dt), Ngoai = Math.Max(0, p.Dt - trong) });
            }
            return o;
        }

        // ================= CHỮ TRÊN BẢN ĐỒ: ĐỔI PHÔNG, TÌM – THAY =================
        public static int ReplaceTexts(Func<string, string> f, out int skipped)
        {
            int n = 0; skipped = 0;
            foreach (var t in Ms.Read(null, new[] { 17, 7 }))
            {
                if (t.Text == null) continue; var s2 = f(t.Text); if (s2 == t.Text) continue;
                if (t.Kind != 17) { skipped++; continue; }
                if (Ms.SetText(t.El, s2)) n++; else skipped++;
            }
            return n;
        }

        // ================= DANH SÁCH THỬA THEO CHUỖI "12, 15-20" =================
        public static List<Parcel> Pick(string s)
        {
            s = (s ?? "").Trim(); if (s == "" || s == "*" || s.ToLower().StartsWith("tất")) return Dc.P.OrderBy(p => Key(p.So)).ToList();
            var want = new HashSet<string>(); var rng = new List<double[]>();
            foreach (var part in s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var m = Regex.Match(part, @"^(\d+)\s*-\s*(\d+)$"); if (m.Success) rng.Add(new[] { double.Parse(m.Groups[1].Value), double.Parse(m.Groups[2].Value) }); else want.Add(part.Trim());
            }
            return Dc.P.Where(p => want.Contains(p.So) || rng.Any(r => { double v; return double.TryParse(p.So, out v) && v >= r[0] && v <= r[1]; })).OrderBy(p => Key(p.So)).ToList();
        }
        static double Key(string so) { double v; return double.TryParse(so, NumberStyles.Float, IC, out v) ? v : 1e12; }
    }
}
