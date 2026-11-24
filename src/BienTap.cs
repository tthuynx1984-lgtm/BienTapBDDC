// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH (nền M8 Pro Địa chính) – các chức năng menu Bản đồ theo nghiệp vụ biên tập bản đồ địa chính, kế thừa M8 Mobile:
//   Sửa lỗi tự động (chọn lớp tham gia sửa), Tìm và sửa lỗi (◀ ▶ đưa MicroStation tới từng lỗi, tự dò lại),
//   Tạo thửa (chọn lớp ranh, chỉ tạo khi hết lỗi treo đỉnh, bảng tổng hợp), Vẽ nhãn chuẩn 3 lớp 3 màu,
//   Đăng ký / Hồ sơ / Kết xuất: lập bằng bộ mẫu của M8 Mobile chạy ngầm trong công cụ (Edge / Chrome không cửa sổ, máy chủ cục bộ 127.0.0.1).
// © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using BCOM = Bentley.Interop.MicroStationDGN;

namespace M8Pro
{
    // một lỗi tìm được (kiểu: ho đầu hở, cat chồng ranh, nho vùng rất nhỏ, so chưa có số, trung trùng số, lech lệch diện tích)
    public class ErrX { public string K, Text; public double X, Y; public Parcel P; public BCOM.Element El; }

    public static class Bt
    {
        public const string Ten = "BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH";
        public static readonly int[] LineTypes = { 3, 4, 6, 12, 14, 15, 16 };
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public static readonly string[] KOrd = { "ho", "giao", "khop", "cong", "cat", "nho", "so", "trung", "lech" };
        public static readonly Dictionary<string, string> KTen = new Dictionary<string, string> {
            { "ho", "Đầu đường hở (treo đỉnh)" }, { "giao", "Ranh chưa cắt tại giao điểm" }, { "khop", "Đầu đường chưa khớp đúng điểm nối" }, { "cong", "Chỗ cần cắt / nối tay (cung tròn, vùng, chuỗi phức…)" },
            { "cat", "Đoạn ranh vẽ chồng lên nhau" }, { "nho", "Vùng rất nhỏ" }, { "so", "Thửa chưa có số" }, { "trung", "Trùng số thửa" }, { "lech", "Lệch diện tích nhãn" } };
        public static string Lvs(IEnumerable<int> lv) { return string.Join(", ", lv.Select(x => x.ToString()).ToArray()); }

        // số đường nét (line, line string, shape, complex…) trên từng lớp
        public static SortedDictionary<int, int> LevelLines()
        {
            var o = new SortedDictionary<int, int>(); var rx = new Regex(@"^Level (-?\d+) · kiểu (\d+)$");
            foreach (var kv in Ms.Census())
            {
                var m = rx.Match(kv.Key); if (!m.Success) continue; int lv = int.Parse(m.Groups[1].Value), t = int.Parse(m.Groups[2].Value); if (!LineTypes.Contains(t)) continue;
                int c; o.TryGetValue(lv, out c); o[lv] = c + kv.Value;
            }
            return o;
        }
        // dựng mạng ranh từ các lớp chọn (không đụng tới thửa đã tạo) — tính cả đường vừa vẽ
        public static Planar Probe(ICollection<int> lv, out List<MEl> ranh)
        {
            ranh = Ms.Read(lv, LineTypes); var segs = new List<Seg>();
            foreach (var m in ranh) foreach (var l in m.Lines) for (int i = 2; i < l.Length; i += 2) if (l[i - 2] != l[i] || l[i - 1] != l[i + 1]) segs.Add(new Seg(l[i - 2], l[i - 1], l[i], l[i + 1], m.Idx));
            return segs.Count == 0 ? null : Planar.Build(segs, Cfg.I.Tol);
        }
        static BCOM.Element ElAt(List<MEl> ranh, double[] n)
        {
            double t2 = Math.Max(Cfg.I.Tol * Cfg.I.Tol * 4, 1e-6);
            foreach (var m in ranh) foreach (var l in m.Lines) { int c = l.Length; if (c < 4) continue; if (Geo.D2(l[0], l[1], n[0], n[1]) <= t2 || Geo.D2(l[c - 2], l[c - 1], n[0], n[1]) <= t2) return m.El; }
            return null;
        }
        // tìm lỗi theo dữ liệu hiện tại: lỗi đường ranh trên các lớp lv (cả đường mới vẽ) + lỗi thửa, nhãn theo thửa đã tạo
        public static List<ErrX> Errors(ICollection<int> lv, bool parcels, out int nHo) { return Errors(lv, parcels, out nHo, null, 1); }
        // kinds: các loại lỗi cần tìm (null: tất cả); nho: vùng rất nhỏ khi diện tích dưới nho m²
        public static List<ErrX> Errors(ICollection<int> lv, bool parcels, out int nHo, ICollection<string> kinds, double nho)
        {
            var o = new List<ErrX>(); nHo = 0; List<MEl> ranh; var R = Probe(lv, out ranh);
            Func<string, bool> on = k => kinds == null || kinds.Contains(k);
            if (R != null)
            {
                if (on("ho")) foreach (var d in R.Dangles) { o.Add(new ErrX { K = "ho", Text = "Đầu đường hở — không nối vào đường ranh nào", X = d[0], Y = d[1], El = ElAt(ranh, d) }); nHo++; }
                if (on("cat")) foreach (var v in R.Overlaps) o.Add(new ErrX { K = "cat", Text = "Đoạn ranh vẽ chồng lên nhau", X = v[0], Y = v[1] });
                // chỗ các đường ranh gặp nhau mà chưa cắt đúng tại giao điểm (phần mềm tạo thửa khác sẽ phải cắt lại)
                if (on("giao") || on("khop") || on("cong"))
                    try
                    {
                        var kq = CatRanh.Chay(ranh, null, false);
                        if (on("giao")) foreach (var d in kq.DiemCat) o.Add(new ErrX { K = "giao", Text = "Ranh chưa cắt tại giao điểm — " + d.N + " đường đi qua điểm này chưa được cắt", X = d.X, Y = d.Y, El = d.M != null ? d.M.El : null });
                        if (on("khop")) foreach (var d in kq.DiemKhop) o.Add(new ErrX { K = "khop", Text = "Đầu đường lệch " + Dc.F(d.D * 1000, 1) + " mm khỏi điểm nối — chưa khớp đúng điểm", X = d.X, Y = d.Y, El = d.M != null ? d.M.El : null });
                        if (on("cong")) foreach (var d in kq.DiemCong) o.Add(new ErrX { K = "cong", Text = d.Ly ?? "Chỗ giao cần cắt tay tại điểm này", X = d.X, Y = d.Y, El = d.M != null ? d.M.El : null });
                    }
                    catch (Exception ex) { Program.Log(ex); }
            }
            if (parcels)
            {
                var cnt = Dc.P.Where(p => p.So != "").GroupBy(p => p.So).ToDictionary(g => g.Key, g => g.Count());
                foreach (var p in Dc.P)
                {
                    if (p.Virtual) continue; double x = p.Lp[0], y = p.Lp[1];
                    if (on("nho") && p.Dt < nho) o.Add(new ErrX { K = "nho", Text = "Vùng rất nhỏ (" + Dc.FD(p.Dt) + " m²) — có thể là lỗi ranh", X = x, Y = y, P = p });
                    if (p.So == "") { if (on("so")) o.Add(new ErrX { K = "so", Text = "Thửa chưa có số thửa (" + Dc.FD(p.Dt) + " m²)", X = x, Y = y, P = p }); }
                    else if (cnt[p.So] > 1 && on("trung")) o.Add(new ErrX { K = "trung", Text = "Trùng số thửa " + p.So, X = x, Y = y, P = p });
                    if (on("lech") && p.DtNhan != null && Math.Abs(p.DtNhan.Value - Math.Round(p.Dt, Cfg.I.DtLe)) > 0.5 * Math.Pow(10, -Cfg.I.DtLe) + 1e-6)
                        o.Add(new ErrX { K = "lech", Text = "Thửa " + (p.So == "" ? "?" : p.So) + ": diện tích nhãn " + Dc.FD(p.DtNhan.Value) + " khác bản đồ " + Dc.FD(p.Dt) + " m²", X = x, Y = y, P = p });
                }
            }
            // theo kiểu lỗi, rồi từ Bắc xuống Nam, từ Tây sang Đông
            return o.OrderBy(e => Array.IndexOf(KOrd, e.K)).ThenByDescending(e => Math.Round(e.Y / 5.0)).ThenBy(e => e.X).ToList();
        }
        // thửa mới sau khi tạo thửa: vùng chưa có trước đó (khác diện tích hoặc nằm ngoài thửa cũ); lần đầu: thửa chưa có số
        public static List<Parcel> NewParcels(List<Parcel> old)
        {
            var real = old.Where(p => !p.Virtual && !p.FromGtp).ToList();
            if (real.Count == 0) return Dc.P.Where(p => p.So == "").ToList();
            return Dc.P.Where(q =>
            {
                var o = real.FirstOrDefault(p => Geo.InPoly(q.Lp[0], q.Lp[1], p.Rings[0]));
                return o == null || Math.Abs(o.Dt - q.Dt) > Math.Max(0.05, q.Dt * 0.001);
            }).ToList();
        }

        // ================= NHÃN CHUẨN 3 LỚP 3 MÀU – bố cục như ô nhãn N_THUA của phần mềm địa chính (đo trên bản đồ thật) =================
        // đơn vị = cao chữ h, gạch ngang ở v = 0. Số thửa canh giữa – dưới, đáy chữ cao hơn gạch 2/9 h; diện tích canh giữa – trên, đỉnh chữ thấp hơn
        // gạch 2/9 h; hai chữ canh giữa theo gạch. Gạch dài bằng chữ diện tích (diện tích dài hơn 2,25 h: 80% chữ, không ngắn hơn 2,25 h).
        // Loại đất canh phải – giữa, ngang giữa gạch, mép phải cách mép trái khối chữ (số thửa / diện tích, chữ rộng hơn) 0,1 h.
        const double G_k = 2.0 / 9, G_gk = .1, G_L = 2.25, G_Lt = .8;
        static double BarL(double wd, double h) { return wd <= G_L * h ? wd : Math.Max(G_L * h, G_Lt * wd); }
        // Wb: gạch; Wt: khối chữ (số thửa, diện tích, gạch – rộng nhất); Wl: loại đất + khoảng cách; W, H: khung cả nhãn
        class Dim { public double Wb, Wt, Wl, W, H; }
        static Dim Dm(double ws, double wk, double wd, double h, bool kh)
        {
            var d = new Dim(); d.Wb = BarL(wd, h); d.Wt = Math.Max(Math.Max(ws, wd), d.Wb); d.Wl = kh ? wk + G_gk * h : 0; d.W = d.Wl + d.Wt; d.H = 2 * (1 + G_k) * h + h * .1; return d;
        }
        class Pl { public double X, Y, R, H; public bool One; }
        static double TW(string s, double h) { double w = 0; try { w = Ms.TextW(s, h); } catch { } return w > 0 ? w : Ms.UocRong(s, h); }
        static bool InR(double[] o, List<double[]> hs, double x, double y) { return Geo.InPoly(x, y, o) && !hs.Any(q => Geo.InPoly(x, y, q)); }
        static bool Inside(double[] o, List<double[]> hs, double cx, double cy, double r, double W, double H)
        {
            double c = Math.Cos(r), s = Math.Sin(r);
            foreach (var uv in new[] { new[] { -W / 2, -H / 2 }, new[] { W / 2, -H / 2 }, new[] { W / 2, H / 2 }, new[] { -W / 2, H / 2 } })
                if (!InR(o, hs, cx + uv[0] * c - uv[1] * s, cy + uv[0] * s + uv[1] * c)) return false;
            return true;
        }
        static double DistB(double[] o, List<double[]> hs, double x, double y)
        {
            double m = double.MaxValue, t, qx, qy;
            foreach (var R in new[] { o }.Concat(hs)) { int n = R.Length; for (int i = 0; i + 3 < n; i += 2) { var d = Geo.SegPt(x, y, R[i], R[i + 1], R[i + 2], R[i + 3], out t, out qx, out qy); if (d < m) m = d; } }
            return Math.Sqrt(m);
        }
        // hướng cạnh dài nhất của thửa (để xoay nhãn khi thửa hẹp và xiên)
        static double Axis(double[] o)
        {
            double best = 0, ang = double.NaN; int n = o.Length;
            for (int i = 0; i + 3 < n; i += 2) { double dx = o[i + 2] - o[i], dy = o[i + 3] - o[i + 1], L = dx * dx + dy * dy; if (L > best) { best = L; ang = Math.Atan2(dy, dx); } }
            if (double.IsNaN(ang)) return ang; while (ang > Math.PI / 2) ang -= Math.PI; while (ang <= -Math.PI / 2) ang += Math.PI;
            return Math.Abs(ang) < 0.05 ? double.NaN : ang;
        }
        static Pl Place(double[] o, List<double[]> hs, double[] ip, double ws0, double wk0, double wd0, double h0, bool kh)
        {
            var angs = new List<double> { 0 }; var ax = Axis(o); if (!double.IsNaN(ax)) angs.Add(ax);
            var bb = Geo.BBox(o); var cand = new List<double[]> { ip };
            foreach (int N in new[] { 9, 17 }) for (int i = 1; i < N; i++) for (int j = 1; j < N; j++) { double x = bb[0] + (bb[2] - bb[0]) * i / N, y = bb[1] + (bb[3] - bb[1]) * j / N; if (InR(o, hs, x, y)) cand.Add(new[] { x, y }); }
            var top = new List<double[]> { ip }; top.AddRange(cand.Skip(1).Select(p => new { p, d = DistB(o, hs, p[0], p[1]) }).OrderByDescending(z => z.d).Take(40).Select(z => z.p));
            double hmin = Math.Max(0.2, h0 * .3);
            for (double h = h0; h >= hmin; h *= .85)
            {
                double k = h / h0; var D = Dm(ws0 * k, wk0 * k, wd0 * k, h, kh);
                foreach (var q in top) foreach (var r in angs) if (Inside(o, hs, q[0], q[1], r, D.W, D.H)) return new Pl { X = q[0], Y = q[1], R = r, H = h };
            }
            for (double h = h0; h >= Math.Max(0.15, h0 * .2); h *= .85)
            {
                double k = h / h0, W = ws0 * k + h * .5, H = h * 1.3;
                foreach (var q in top) foreach (var r in angs) if (Inside(o, hs, q[0], q[1], r, W, H)) return new Pl { X = q[0], Y = q[1], R = r, H = h, One = true };
            }
            return new Pl { X = ip[0], Y = ip[1], R = 0, H = Math.Max(0.15, h0 * .2), One = true };
        }
        static void Add(Parcel p, BCOM.Element e, int kind) { if (e != null) p.Labels.Add(new MEl { El = e, Kind = kind }); }
        // vẽ nhãn một thửa: số thửa + gạch ngang (lớp, màu số thửa), loại đất (lớp, màu loại đất), diện tích (lớp, màu diện tích)
        public static void Lab3(Parcel p)
        {
            var c = Cfg.I; double h0 = c.H; string so = p.So == "" ? "?" : p.So, kh = (p.Ld ?? "").Trim(), dt = DtStr(p.Dt);
            double ws0 = TW(so, h0), wk0 = kh != "" ? TW(kh, h0) : 0, wd0 = TW(dt, h0);
            var o = p.Rings[0]; var hs = p.Rings.Skip(1).ToList();
            var pl = Place(o, hs, p.Lp, ws0, wk0, wd0, h0, kh != "");
            double h = pl.H, k = h / h0, ws = ws0 * k, wk = wk0 * k, wd = wd0 * k, cs = Math.Cos(pl.R), sn = Math.Sin(pl.R);
            Func<double, double, double[]> at = (u, v) => new[] { pl.X + u * cs - v * sn, pl.Y + u * sn + v * cs };
            var els = new List<BCOM.Element>(); var txt = new List<string>(); var pos = new List<double[]>();
            Action<BCOM.Element, string, double[]> put = (e, s1, q1) => { els.Add(e); txt.Add(s1); pos.Add(q1); };
            if (pl.One) { var q = at(0, 0); put(Ms.MakeText(so, q[0], q[1], h, pl.R, c.SoLop, c.SoMau), so, q); PutCell(p, els, txt, pos, pl.X, pl.Y); return; }
            var D = Dm(ws, wk, wd, h, kh != ""); double uc = -D.W / 2 + D.Wl + D.Wt / 2, u0 = uc - D.Wb / 2, u1 = uc + D.Wb / 2;
            // thứ tự thành phần như ô nhãn N_THUA: loại đất, gạch ngang, số thửa, diện tích; mỗi chữ đặt theo điểm canh (pos: tâm chữ)
            if (kh != "") { var q = at(uc - D.Wt / 2 - G_gk * h, 0); put(Ms.MakeText(kh, q[0], q[1], h, pl.R, c.LdLop, c.LdMau, Ms.CanhPhaiGiua), kh, at(uc - D.Wt / 2 - G_gk * h - wk / 2, 0)); }
            var b0 = at(u0, 0); var b1 = at(u1, 0); put(Ms.MakeLine(new[] { b0[0], b0[1], b1[0], b1[1] }, c.SoLop, c.SoMau, 0), null, new[] { (b0[0] + b1[0]) / 2, (b0[1] + b1[1]) / 2 });
            var a = at(uc, G_k * h); put(Ms.MakeText(so, a[0], a[1], h, pl.R, c.SoLop, c.SoMau, Ms.CanhGiuaDuoi), so, at(uc, (G_k + .5) * h));
            var d = at(uc, -G_k * h); put(Ms.MakeText(dt, d[0], d[1], h, pl.R, c.DtLop, c.DtMau, Ms.CanhGiuaTren), dt, at(uc, -(G_k + .5) * h));
            PutCell(p, els, txt, pos, pl.X, pl.Y);
        }
        // gom các phần của nhãn thành ô N_THUA (nhãn thửa dạng cell) rồi ghi nhận vào thửa; txt[i] = null: gạch ngang
        public static void PutCell(Parcel p, List<BCOM.Element> els, List<string> txt, List<double[]> pos, double ox, double oy)
        {
            BCOM.Element cell = null; try { cell = Ms.AddCell("N_THUA", els, ox, oy); } catch (Exception ex) { Program.Log(ex); }
            var parts = cell != null ? Ms.CellParts(cell) : els; if (parts.Count != els.Count) parts = els;
            for (int i = 0; i < els.Count; i++)
                p.Labels.Add(new MEl { El = parts[i], Parent = cell, Kind = txt[i] == null ? 3 : 17, Text = txt[i], Cx = pos[i][0], Cy = pos[i][1], H = Cfg.I.H });
        }
        // diện tích trên nhãn: số lẻ theo thiết lập; dấu thập phân theo đa số nhãn đang có trên tờ (796,1 hay 796.1)
        public static string DtStr(double v)
        {
            var s = v.ToString("F" + Cfg.I.DtLe, IC); bool comma = Cfg.I.DauPhay;
            if (Cfg.I.TpTheoNhan)
            {
                int pc = 0, pd = 0; var rc = new Regex(@"^\d+,\d+$"); var rd = new Regex(@"^\d+\.\d+$");
                foreach (var p in Dc.P) foreach (var l in p.Labels) { var t = (l.Text ?? "").Trim(); if (t == "") continue; if (rc.IsMatch(t)) pc++; else if (rd.IsMatch(t)) pd++; }
                if (pc + pd > 0) comma = pc >= pd;
            }
            return comma ? s.Replace('.', ',') : s;
        }
        // hình và số thửa (cả số đọc từ nhãn) của các thửa vẽ lại nhãn: ô nhãn cũ trong thửa tìm thêm theo hình
        public static List<KeyValuePair<List<double[]>, string>> GocNhan(IEnumerable<Parcel> ds)
        {
            var o = new List<KeyValuePair<List<double[]>, string>>();
            foreach (var p in ds) { if (p.Rings == null || p.Rings.Count == 0) continue; foreach (var s in new[] { p.So, p.NhanSo }.Where(z => !string.IsNullOrEmpty(z)).Distinct()) o.Add(new KeyValuePair<List<double[]>, string>(p.Rings, s)); }
            return o;
        }
        // gạch ngang của nhãn cũ (đoạn thẳng ngắn trên lớp số thửa, sát chữ nhãn của thửa): đưa vào nhãn của thửa để vẽ lại thì xoá được
        public static void ReadBars(Dc.PGrid g)
        {
            var lv = new HashSet<int> { Cfg.I.SoLop }; foreach (var x in Cfg.Lvs(Cfg.I.LvNhan)) lv.Add(x); foreach (var r in Cfg.Lvs(Cfg.I.LvRanh)) lv.Remove(r);
            if (lv.Count == 0) return; double maxL = Math.Max(Cfg.I.H * 25, 5);
            foreach (var m in Ms.Read(lv, new[] { 3 }))
            {
                if (m.Lines.Count != 1 || m.Lines[0].Length != 4) continue; var l = m.Lines[0]; double L = Geo.Dist(l[0], l[1], l[2], l[3]); if (L <= 0 || L > maxL) continue;
                double mx = (l[0] + l[2]) / 2, my = (l[1] + l[3]) / 2; var p = g.Find(mx, my); if (p == null) continue;
                double near = Math.Max(L, Cfg.I.H * 3);
                if (!p.Labels.Any(t => t.Kind != 3 && Geo.Dist(t.Cx, t.Cy, mx, my) < near)) continue;
                m.Kind = 3; p.Labels.Add(m);
            }
        }
    }

    // ================= DUYỆT VÀ SỬA LỖI: lọc loại lỗi, danh sách lỗi, thanh chuyển ⏮ ◀ i / n ▶ ⏭ đưa khung nhìn tới từng lỗi =================
    public class NavForm : FormBase
    {
        MainForm M; List<ErrX> All = new List<ErrX>(), E = new List<ErrX>(); int I = -1; ErrX Cur; bool Fixed, Stale; List<int> Lv = new List<int>();
        Nhom nk = new Nhom("Loại lỗi đang duyệt (bỏ tích để ẩn)") { Dock = DockStyle.Top, Height = 62 }; int rongLoi = 160;
        ICollection<string> Kinds; double Nho = 1; HashSet<string> An = new HashSet<string>();
        ThanhChuyen nav = new ThanhChuyen("lỗi"); FlowLayoutPanel loai = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
        Label cur = new Label { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6, 5, 6, 2) }; Font fDam;
        ListView L = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
        public NavForm(MainForm m)
        {
            M = m; Text = "Duyệt và sửa lỗi"; Font = m.Font; fDam = new Font(Font, FontStyle.Bold); Width = 680; Height = 500; MinimumSize = new Size(520, 380); ShowInTaskbar = false; try { Icon = m.Icon; } catch { } GiaoDien.KhongCat(this);
            nk.Than.Controls.Add(loai);
            L.Columns.Add("Lỗi", 430); L.Columns.Add("X (Bắc)", 92); L.Columns.Add("Y (Đông)", 92);
            // cột Lỗi chiếm phần rộng còn lại
            L.ShowItemToolTips = true; L.Tag = "tucot"; Resize += (s, e) => CaoNhom();
            // cột Lỗi: vừa nội dung dài nhất (không cắt chữ), rộng ra theo hộp thoại
            L.Resize += (s, e) => { int w = L.ClientSize.Width - L.Columns[1].Width - L.Columns[2].Width - SystemInformation.VerticalScrollBarWidth - 4; L.Columns[0].Width = Math.Max(rongLoi, w); };
            nav.Chuyen += d => Run(() => { if (d == -2) Dau(); else if (d == -1) Prev(); else if (d == 1) Next(); else Cuoi(); });
            nav.Items.Add(new ToolStripSeparator());
            nav.Them("Dò lại", GiaoDien.MuiTen(6, 16, GiaoDien.Nhan), "Dò lại lỗi theo bản vẽ hiện tại", () => Run(() => { Recheck(true); M.Msg(E.Count == 0 ? "✓ Không còn lỗi" : "Còn " + All.Count + " lỗi"); }));
            nav.Them("Phóng tới", Icons.Get("zin", 16), "Đưa khung nhìn tới lỗi đang chọn", () => Run(() => { if (Cur != null) Go(I); }));
            Controls.Add(L); Controls.Add(cur); Controls.Add(nk); Controls.Add(nav);
            L.ItemActivate += (s, e) => { if (L.SelectedItems.Count > 0) Run(() => Go((int)L.SelectedItems[0].Tag)); };
            L.Click += (s, e) => { if (L.SelectedItems.Count > 0) Run(() => Go((int)L.SelectedItems[0].Tag)); };
            // sang bản vẽ sửa rồi quay lại: lần chuyển tới sẽ dò lại trước
            Deactivate += (s, e) => Stale = true;
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Right || e.KeyCode == Keys.PageDown) { Run(Next); e.Handled = true; } else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.PageUp) { Run(Prev); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.Home) { Run(Dau); e.Handled = true; } else if (e.Control && e.KeyCode == Keys.End) { Run(Cuoi); e.Handled = true; }
            };
        }
        void Run(Action a) { Cursor = Cursors.WaitCursor; try { a(); } catch (Exception ex) { Program.Log(ex); M.Msg("Lỗi: " + ex.Message); } finally { Cursor = Cursors.Default; } }
        public void Load_(List<ErrX> e, ICollection<int> lv, int start) { Load_(e, lv, start, null, 1); }
        public void Load_(List<ErrX> e, ICollection<int> lv, int start, ICollection<string> kinds, double nho)
        {
            Lv = lv.ToList(); Kinds = kinds; Nho = nho; An.Clear(); All = new List<ErrX>(); E = new List<ErrX>(); I = -1; Cur = null; Fixed = false;
            Set(e, false); Stale = false; if (start >= 0) { int j = E.IndexOf(e[start]); if (j >= 0) Go(j); }
        }
        static bool Same(ErrX a, ErrX b) { return a.K == b.K && Geo.Dist(a.X, a.Y, b.X, b.Y) < 0.001; }
        public void Recheck(bool keep)
        {
            if (Dc.Dirty || Dc.P.Count == 0) { try { Dc.Build(false); } catch (Exception ex) { Program.Log(ex); } }
            int nh; Set(Bt.Errors(Lv, true, out nh, Kinds, Nho), keep); Stale = false;
        }
        // giữ chỗ đang xem; lỗi đang xem đã sửa xong → đứng trước lỗi kế tiếp (của danh sách cũ) còn lại, ▶ tới đúng lỗi đó
        void Set(List<ErrX> e, bool keep)
        {
            var prev = E; var pc = Cur; int pi = I; bool pf = Fixed; All = e; E = All.Where(x => !An.Contains(x.K)).ToList(); I = -1; Cur = null; Fixed = false;
            if (keep && (pc != null || pf))
            {
                int j = pc != null ? E.FindIndex(x => Same(x, pc)) : -1;
                if (j >= 0) { I = j; Cur = E[j]; }
                else { int nx = -1; for (int q = pi + 1; q < prev.Count && nx < 0; q++) { var pq = prev[q]; nx = E.FindIndex(x => Same(x, pq)); } I = (nx < 0 ? E.Count : nx) - 1; Fixed = true; }
            }
            Loai(); Fill(); Ui();
        }
        // ô tích theo loại lỗi có trong danh sách (kèm số lỗi)
        void Loai()
        {
            loai.SuspendLayout(); loai.Controls.Clear();
            foreach (var k in Bt.KOrd)
            {
                int n = All.Count(x => x.K == k); if (n == 0) continue; var kk = k;
                var c = new OTich { Text = Bt.KTen[k] + " (" + n + ")", Checked = !An.Contains(k), AutoSize = true, Margin = new Padding(3, 2, 12, 2) };
                c.CheckedChanged += (s, e) => { if (((CheckBox)s).Checked) An.Remove(kk); else An.Add(kk); Loc(); };
                loai.Controls.Add(c);
            }
            if (loai.Controls.Count == 0) loai.Controls.Add(new Label { Text = "Không phát hiện lỗi", AutoSize = true, ForeColor = Color.ForestGreen, Margin = new Padding(3, 4, 3, 3) });
            loai.ResumeLayout(); CaoNhom();
        }
        // nhóm loại lỗi cao vừa số hàng ô tích (tối đa 3 hàng, nhiều hơn thì cuộn)
        void CaoNhom()
        {
            int rong = Math.Max(200, ClientSize.Width - 30), x0 = 0, hang = 1;
            foreach (Control c in loai.Controls) { int w = c.PreferredSize.Width + c.Margin.Horizontal; if (x0 > 0 && x0 + w > rong) { hang++; x0 = 0; } x0 += w; }
            nk.Height = Nhom.CAO + 12 + Math.Min(3, hang) * 24;
        }
        // ẩn / hiện loại lỗi: giữ lỗi đang xem nếu còn hiện
        void Loc()
        {
            var pc = Cur; E = All.Where(x => !An.Contains(x.K)).ToList(); I = -1; Cur = null; Fixed = false;
            if (pc != null) { int j = E.FindIndex(x => Same(x, pc)); if (j >= 0) { I = j; Cur = E[j]; } }
            Fill(); Ui();
        }
        void Fill()
        {
            L.BeginUpdate(); L.Items.Clear(); L.Groups.Clear(); var G = new Dictionary<string, ListViewGroup>();
            foreach (var k in Bt.KOrd) { int n = E.Count(x => x.K == k); if (n == 0) continue; var g = new ListViewGroup(k, Bt.KTen[k] + " (" + n + ")"); L.Groups.Add(g); G[k] = g; }
            for (int i = 0; i < E.Count && i < 5000; i++) { var x = E[i]; var it = new ListViewItem(new[] { Gon(x), Dc.F(x.Y, 2), Dc.F(x.X, 2) }, G[x.K]) { Tag = i, ToolTipText = x.Text }; L.Items.Add(it); }
            L.EndUpdate();
            // cột vừa chữ dài nhất và tiêu đề; cột Lỗi không hẹp hơn nội dung, có thừa chỗ thì rộng ra
            GiaoDien.VuaCot(L, 900); rongLoi = Math.Max(160, L.Columns[0].Width + 20); int con = L.ClientSize.Width - L.Columns[1].Width - L.Columns[2].Width - SystemInformation.VerticalScrollBarWidth - 4; L.Columns[0].Width = Math.Max(rongLoi, con);
        }
        void Ui()
        {
            int n = E.Count, tong = All.Count; nav.Dat(Cur != null ? I : -1, n);
            cur.Font = Cur != null ? fDam : Font;
            cur.Text = Cur != null ? (Gon(Cur) != Cur.Text ? Bt.KTen[Cur.K] + " — " + Gon(Cur).Substring(0, 1).ToLower() + Gon(Cur).Substring(1) : Bt.KTen[Cur.K] + ": " + Cur.Text) : Fixed && n > 0 ? "Lỗi vừa xem đã hết — bấm ▶ để tới lỗi kế tiếp" : n > 0 ? "Bấm ▶ (hoặc PgDn) để đưa khung nhìn tới từng lỗi, hoặc nhấp một dòng" : tong > 0 ? "Các lỗi còn lại đang ẩn – tích loại lỗi ở trên để duyệt" : "✓ Không phát hiện lỗi";
            cur.ForeColor = Cur != null ? Color.FromArgb(150, 40, 0) : n == 0 && tong == 0 ? Color.ForestGreen : Color.FromArgb(40, 60, 90);
            Text = "Duyệt và sửa lỗi — " + (tong == 0 ? "không còn lỗi" : tong + " lỗi" + (n < tong ? " (đang duyệt " + n + ")" : "")) + " · level ranh " + Bt.Lvs(Lv);
            foreach (ListViewItem it in L.Items) { bool on = Cur != null && (int)it.Tag == I; it.Font = on ? fDam : L.Font; if (on) { it.Selected = true; it.EnsureVisible(); } }
        }
        // nội dung lỗi gọn khi đã xếp theo nhóm: bỏ phần đầu trùng tên nhóm ("Ranh chưa cắt tại giao điểm — 2 đường…" → "2 đường…")
        static string Gon(ErrX x)
        {
            var t = x.Text ?? ""; int i = t.IndexOf(" — "); string ten;
            if (i <= 0 || !Bt.KTen.TryGetValue(x.K, out ten) || !ten.StartsWith(t.Substring(0, i), StringComparison.OrdinalIgnoreCase)) return t;
            var r = t.Substring(i + 3).Trim(); return r == "" ? t : char.ToUpper(r[0]) + r.Substring(1);
        }
        void Dau() { if (Stale) Recheck(true); if (E.Count == 0) { Ui(); return; } Go(0); }
        void Cuoi() { if (Stale) Recheck(true); if (E.Count == 0) { Ui(); return; } Go(E.Count - 1); }
        void Next() { if (Stale) Recheck(true); if (E.Count == 0) { Ui(); return; } Go(I + 1); }
        void Prev() { if (Stale) Recheck(true); if (E.Count == 0) { Ui(); return; } Go(Cur != null ? I - 1 : I >= 0 ? I : E.Count - 1); }
        void Go(int i)
        {
            int n = E.Count; if (n == 0) return; I = ((i % n) + n) % n; Cur = E[I]; Fixed = false; var e = Cur;
            if (e.P != null && e.K != "ho" && e.K != "cat") { M.Cur = e.P; Dc.Zoom(e.P, .6); }
            else { double d = Math.Max(12, Cfg.I.H * 8); Ms.ZoomTo(e.X - d, e.Y - d, e.X + d, e.Y + d); if (e.El != null) Ms.Select(new[] { e.El }); }
            Ui(); M.Msg("Lỗi " + (I + 1) + "/" + n + ": " + e.Text + " — sửa trên bản vẽ rồi bấm ▶ (công cụ tự dò lại)");
        }
    }

    // ================= BẢNG TỔNG HỢP SƠ BỘ SAU KHI TẠO THỬA =================
    public class SumForm : Form
    {
        public Parcel Edit;   // thửa chọn để sửa thông tin sau khi đóng
        public SumForm(List<Parcel> nw, string lv, string gw)
        {
            Text = "⬡ Tạo thửa xong — tổng hợp sơ bộ"; Font = new Font("Segoe UI", 9f); Width = 560; Height = 560; GiaoDien.KhongCat(this); StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; TopMost = true; MinimizeBox = false;
            double tot = Dc.P.Where(p => !p.Virtual).Sum(p => p.Dt), nn = nw.Sum(p => p.Dt); int all = Dc.P.Count(p => !p.Virtual), nso = Dc.P.Count(p => !p.Virtual && p.So == "");
            var t = new Label { Dock = DockStyle.Top, Height = 54, Padding = new Padding(6, 6, 6, 0), Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Text = "Tổng số thửa: " + all + (nso > 0 ? " (chưa có số: " + nso + ")" : "") + " · level ranh " + lv + "\r\nThửa mới: " + nw.Count + (nw.Count > 0 ? " — tổng diện tích " + Dc.F(nn, 1) + " m²" : "") };
            var L = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
            L.Columns.Add("Số thửa", 90); L.Columns.Add("Loại đất", 150); L.Columns.Add("Diện tích (m²)", 120, HorizontalAlignment.Right);
            foreach (var p in nw.OrderBy(p => { int v; return int.TryParse(p.So, out v) ? v : int.MaxValue; })) L.Items.Add(new ListViewItem(new[] { p.So == "" ? "chưa có" : p.So, p.Ld, Dc.F(p.Dt, 1) }) { Tag = p });
            if (nw.Count > 0) L.Items.Add(new ListViewItem(new[] { "Cộng thửa mới", "", Dc.F(nn, 1) }) { Font = new Font(Font, FontStyle.Bold) });
            var S = new ListView { Dock = DockStyle.Bottom, Height = 150, View = View.Details, FullRowSelect = true, GridLines = true };
            S.Columns.Add("Loại đất", 150); S.Columns.Add("Số thửa", 90, HorizontalAlignment.Right); S.Columns.Add("Diện tích (m²)", 120, HorizontalAlignment.Right);
            foreach (var kv in Dc.StatLD()) S.Items.Add(new ListViewItem(new[] { kv.Key, kv.Value[0].ToString(CultureInfo.InvariantCulture), Dc.F(kv.Value[1], 1) }));
            var b = new Label { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(6, 4, 6, 0), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Text = "Tổng diện tích cả tờ: " + Dc.F(tot, 1) + " m² = " + Dc.F(tot / 1e4, 4) + " ha\r\nTheo loại đất:" };
            var n = new Label { Dock = DockStyle.Bottom, Height = 22, Padding = new Padding(6, 2, 6, 0), ForeColor = Color.DimGray, Text = "Diện tích tính từ đường ranh vừa dựng (sơ bộ). Số liệu chính thức theo .gtp và nhãn thửa." };
            var bp = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
            var ok = new Button { Text = "Đóng", Width = 90, DialogResult = DialogResult.OK }; bp.Controls.Add(ok); AcceptButton = ok; CancelButton = ok;
            // sửa thông tin thửa (cả loại đất chi tiết) rồi ghi ngay vào tệp thuộc tính
            var ed = new Button { Text = "Sửa thông tin thửa…", AutoSize = true }; bp.Controls.Add(ed);
            Func<Parcel> sel = () => L.SelectedItems.Count > 0 ? L.SelectedItems[0].Tag as Parcel : nw.FirstOrDefault();
            ed.Click += (s, e) => { Edit = sel(); if (Edit != null) { DialogResult = DialogResult.OK; } };
            L.DoubleClick += (s, e) => { Edit = sel(); if (Edit != null) DialogResult = DialogResult.OK; };
            ed.Enabled = nw.Count > 0;
            var gl = new Label { Dock = DockStyle.Bottom, Height = gw != "" ? 36 : 0, Padding = new Padding(6, 2, 6, 0), ForeColor = gw.StartsWith("Chưa") ? Color.Firebrick : Color.ForestGreen, Text = gw };
            Controls.Add(L); Controls.Add(t); Controls.Add(b); Controls.Add(S); Controls.Add(n); Controls.Add(gl); Controls.Add(bp);
        }
    }

    // ================= CHỨC NĂNG MENU (thêm vào cửa sổ chính) =================
    public partial class MainForm
    {
        NavForm nav;
        public void ShowNav(List<ErrX> e, ICollection<int> lv, int start) { ShowNav(e, lv, start, null, 1); }
        public void ShowNav(List<ErrX> e, ICollection<int> lv, int start, ICollection<string> kinds, double nho)
        {
            if (nav == null || nav.IsDisposed) { nav = new NavForm(this); if (!(Host.InMs && nav.AttachMs())) nav.Show(this); else nav.Show(); }
            nav.Load_(e, lv, start, kinds, nho); nav.Show(); nav.Activate();
        }
        // 🧹 Sửa lỗi tự động: chọn lớp tham gia sửa, dung sai; xong dò lại và báo số đầu hở còn lại
        void BtFix()
        {
            var sel = Cfg.Lvs(string.IsNullOrEmpty(Cfg.I.FixLv) ? Cfg.I.LvRanh : Cfg.I.FixLv);
            var f = new SuaLoiForm(sel);
            if (f.ShowDialog(this) != DialogResult.OK) return;
            Cfg.I.FixLv = string.Join(",", f.Levels.Select(x => x.ToString()).ToArray()); Cfg.I.FixTol = f.Tol; Cfg.I.CatGiao = f.Cat; Cfg.I.Save();
            if (!f.Cat && !f.Under && !f.Over && !f.Dup) { Msg("Chưa chọn việc sửa nào"); return; }
            List<MEl> ranh; var R = Bt.Probe(f.Levels, out ranh); if (R == null) { Msg("Không có đường nét nào trên level " + Bt.Lvs(f.Levels) + ". " + Ms.LastRead); return; }
            var s = f.Under || f.Over || f.Dup ? Dc.FixOn(ranh, R, f.Under, f.Over, f.Dup) : ""; Dc.Dirty = true;
            if (f.Cat) s += (s != "" ? "; " : "") + CatGiao(f.Levels);
            int nh; var errs = Bt.Errors(f.Levels, false, out nh); int ng = errs.Count(x => x.K == "giao" || x.K == "khop"), nc = errs.Count(x => x.K == "cong");
            Msg(s + " trên level " + Bt.Lvs(f.Levels) + " · còn " + nh + " đầu hở" + (ng > 0 ? ", " + ng + " chỗ chưa cắt" : "") + (nc > 0 ? ", " + nc + " chỗ cần cắt / nối tay" : ""));
            if (nh > 0 && Confirm(s + ".\r\nCòn " + nh + " đầu hở chưa sửa được trong khoảng " + Dc.F(f.Tol, 2) + " m. Mở “Duyệt và sửa lỗi” để tới từng chỗ?")) ShowNav(errs, f.Levels, errs.FindIndex(x => x.K == "ho"));
            else if (nh == 0 && ng + nc > 0 && Confirm(s + ".\r\nCòn " + (ng + nc) + " chỗ chưa cắt được tự động" + (nc > 0 ? " (" + nc + " chỗ giao với cung tròn, vùng, chuỗi phức hoặc đầu đường gần song song – cắt / nối tay)" : "") + ". Mở “Duyệt và sửa lỗi” để tới từng chỗ?")) ShowNav(errs, f.Levels, errs.FindIndex(x => x.K == "giao" || x.K == "khop" || x.K == "cong"));
            else if (nav != null && !nav.IsDisposed && nav.Visible) nav.Recheck(true);
        }
        // phá đường gấp khúc thành đoạn thẳng, cắt ranh đúng tại giao điểm trên các lớp (thay đường bằng các đoạn mới),
        // rồi ghi lại liên kết thửa – đường ranh trong tệp thuộc tính
        string CatGiao(ICollection<int> lv)
        {
            var c0 = Cursor; Cursor = Cursors.WaitCursor;
            try { string s; var kq = PhaCat(lv, out s); var tp = Dc.LamMoiTopo(kq, true); return s + (tp != "" ? " · " + tp : ""); }
            catch (Exception ex) { Program.Log(ex); return "chưa cắt được ranh tại giao điểm: " + ex.Message; }
            finally { Cursor = c0; Ms.Status(""); }
        }
        // phá đường gấp khúc (line string) thành đoạn thẳng (line) rồi cắt đúng tại giao điểm; bao: câu báo kết quả
        static CatKq PhaCat(ICollection<int> lv, out string bao)
        {
            int nd, nn; NoiTach.Bao = Ms.Status; Ms.Status("Đang phá đường gấp khúc thành đoạn thẳng…");
            var kp = NoiTach.Pha(lv, out nd, out nn); if (nd > 0 || kp.Loi > 0) Dc.Dirty = true;
            Ms.Status("Đang cắt ranh tại giao điểm…");
            var kq = CatRanh.Chay(lv, null, true); Dc.Dirty = true; foreach (var id in kp.IdCu) kq.IdCu.Add(id); kq.Loi += kp.Loi;
            bao = (nd > 0 ? "phá " + nd + " đường gấp khúc thành " + nn + " đoạn thẳng; " : "") + kq.Bao();
            return kq;
        }
        // ⚠ Tìm và sửa lỗi: chọn lớp ranh, loại lỗi, ngưỡng; dò theo dữ liệu hiện tại (cả đường vừa vẽ), ◀ ▶ đưa khung nhìn tới từng lỗi
        void BtCheck()
        {
            var f = new TimLoiForm(Cfg.Lvs(Cfg.I.LvRanh)); if (f.ShowDialog(this) != DialogResult.OK) return;
            var c = Cfg.I; var lr = string.Join(",", f.Levels.Select(x => x.ToString()).ToArray());
            if (lr != c.LvRanh || Math.Abs(c.Tol - f.Tol) > 1e-12) Dc.Dirty = true;
            c.LvRanh = lr; c.Tol = f.Tol; c.VungNho = f.Nho; c.TimLoai = f.Kinds.Count == TimLoiForm.MAC_DINH.Length ? "" : string.Join(",", f.Kinds.ToArray()); c.Save();
            var lv = f.Levels; if (Dc.P.Count == 0 || Dc.Dirty) Msg(Dc.Build(false));
            int nh; var e = Bt.Errors(lv, true, out nh, f.Kinds, f.Nho); ShowNav(e, lv, -1, f.Kinds, f.Nho);
            Msg(e.Count == 0 ? "Không phát hiện lỗi trên level " + Bt.Lvs(lv) : "Tìm thấy " + e.Count + " lỗi" + (f.Kinds.Contains("ho") ? " (" + nh + " đầu hở)" : "") + " — bấm ▶ để tới từng lỗi");
        }
        // ⬡ Tạo thửa: chọn lớp ranh; chỉ tạo khi không còn lỗi treo đỉnh; xong hiện bảng tổng hợp
        void BtCreate()
        {
            var f = new TaoThuaForm(Cfg.Lvs(Cfg.I.LvRanh));
            if (f.ShowDialog(this) != DialogResult.OK) return;
            Dc.Dirty = true;   // lớp ranh, dung sai, lớp nhãn, số tờ có thể vừa đổi
            int nh; var e = Bt.Errors(f.Levels, false, out nh);
            if (nh > 0)
            {
                Msg("⛔ Chưa tạo thửa: còn " + nh + " lỗi treo đỉnh (đầu đường hở) trên level " + Bt.Lvs(f.Levels) + ". Bấm ▶ để tới từng lỗi; sửa bằng “Sửa lỗi tự động” hoặc sửa tay rồi tạo thửa lại");
                ShowNav(e, f.Levels, e.FindIndex(x => x.K == "ho")); return;
            }
            // ranh chưa cắt tại giao điểm: hỏi cắt trước (tạo thửa bằng phần mềm khác sẽ không phải cắt lại, liên kết thửa – đường ranh ghi theo đoạn đã cắt)
            CatKq kq = null; string sc = ""; int ng = e.Count(x => x.K == "giao"), nk = e.Count(x => x.K == "khop");
            if (ng + nk > 0 && Cfg.I.HoiCatTao)
            {
                var hoi = MessageBox.Show(this, "Trên level " + Bt.Lvs(f.Levels) + " còn " + (ng > 0 ? ng + " chỗ đường ranh gặp nhau chưa cắt tại giao điểm" : "") + (ng > 0 && nk > 0 ? " và " : "") + (nk > 0 ? nk + " đầu đường chưa khớp đúng điểm nối" : "") + ".\r\n\r\n" +
                    "Cắt ranh đúng tại giao điểm trước khi tạo thửa? (nên làm: tạo thửa bằng phần mềm khác sẽ không phải cắt lại ranh, không mất liên kết thửa – đường ranh)\r\n\r\nCó: cắt ranh rồi tạo thửa · Không: tạo thửa luôn · Huỷ: thôi", "⬡ Tạo thửa", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (hoi == DialogResult.Cancel) return;
                if (hoi == DialogResult.Yes)
                {
                    var c0 = Cursor; Cursor = Cursors.WaitCursor;
                    try { kq = PhaCat(f.Levels, out sc); Msg(sc); } catch (Exception ex) { Program.Log(ex); Msg("Chưa cắt được ranh tại giao điểm: " + ex.Message); } finally { Cursor = c0; Ms.Status(""); }
                }
            }
            var old = Dc.P.ToList(); bool had = old.Any(p => !p.Virtual && !p.FromGtp);
            var msg = Dc.Build(had); if (sc != "") msg = sc + " · " + msg; Msg(msg); if (Dc.P.Count == 0) return;
            // ghi ngay vào tệp thuộc tính: diện tích, tâm, hình thửa, đường ranh tạo thành thửa, thửa mới
            string gw = "";
            if (Dc.G != null && Cfg.I.GhiKhiTaoThua) { try { gw = Dc.SyncGtp(); Msg(msg + " · " + gw); } catch (Exception ex) { Program.Log(ex); gw = "Chưa ghi được vào tệp thuộc tính: " + ex.Message; Msg(gw); } }
            else if (kq != null) { try { gw = Dc.LamMoiTopo(kq, false); if (gw != "") Msg(msg + " · " + gw); } catch (Exception ex) { Program.Log(ex); } }
            var nw = Bt.NewParcels(old); using (var s = new SumForm(nw, Bt.Lvs(f.Levels), gw)) { s.ShowDialog(this); if (s.Edit != null) ShowParcel(s.Edit); }
        }
        // 🏷 Vẽ nhãn: nhãn chuẩn 3 lớp 3 màu (hoặc kiểu khác trong Thiết lập)
        void BtLabels()
        {
            if (!NeedTopo()) return;
            string[] kieu = { "3 level 3 màu (chuẩn)", "3 dòng", "Phân số" };
            var r = Ask.Show(this, "🏷 Vẽ nhãn thửa", new F("pv", "Phạm vi", "Tất cả thửa") { Opt = new[] { "Tất cả thửa", "Thửa chọn trên bản đồ", "Thửa chưa có nhãn" } },
                new F("k", "Kiểu nhãn", kieu[(int)Cfg.I.Kieu]) { Opt = kieu }, new F("x", (Cfg.I.NhanCuLop > 0 ? "Nhãn cũ trong thửa chuyển sang level " + Cfg.I.NhanCuLop + " (không xoá" : "Xoá nhãn cũ trong thửa (") + "; chỉ chữ nhãn và gạch ngang của nhãn)", "1") { Check = true });
            if (r == null) return; Cfg.I.Kieu = (KieuNhan)Array.IndexOf(kieu, r["k"]); Cfg.I.Save(); bool x = r["x"] == "1";
            string how = Cfg.I.Kieu == KieuNhan.Ba_lop_chuan ? " — số thửa level " + Cfg.I.SoLop + " màu " + Cfg.I.SoMau + ", loại đất level " + Cfg.I.LdLop + " màu " + Cfg.I.LdMau + ", diện tích level " + Cfg.I.DtLop + " màu " + Cfg.I.DtMau : "";
            if (r["pv"].StartsWith("Thửa chọn")) { Pick("Chạm vào trong thửa cần vẽ nhãn", p => { Dc.DrawLabels(new[] { p }, x, Bt.GocNhan(new[] { p })); Msg("Đã vẽ nhãn thửa " + p.So + how + Dc.NhanCuBao()); }); return; }
            var list = r["pv"].StartsWith("Thửa chưa") ? Dc.P.Where(p => !p.Virtual && p.Labels.Count == 0).ToList() : Dc.P.Where(p => !p.Virtual).ToList();
            if (list.Count == 0) { Msg("Không có thửa nào cần vẽ nhãn"); return; }
            if (!Confirm("Vẽ nhãn cho " + list.Count + " thửa" + (x ? " (" + Dc.NhanCuMoTa + " trong thửa)" : "") + "?")) return;
            Dc.DrawLabels(list, x, list.Count <= 300 ? Bt.GocNhan(list) : null); Msg("Đã vẽ nhãn " + list.Count + " thửa" + how + Dc.NhanCuBao());
        }
        // Đăng ký / Hồ sơ / Kết xuất: lập bằng bộ mẫu M8 Mobile – bộ máy lập mẫu chạy ngầm trong công cụ, tệp lưu vào thư mục hồ sơ và mở ngay
        static readonly string[] DdTen = { "PDF – in, ký", "DGN V8 – mở bằng MicroStation, đúng toạ độ", "DGN V7", "DXF" }, DdMa = { "pdf", "dgn8", "dgn", "dxf" };
        void BtForm(string id, string name, bool parcel) { BtForm(id, name, parcel, null, true); }
        // chon: thửa đã chọn sẵn (bảng thuộc tính, bảng hồ sơ) – không phải chạm lại trên bản đồ; hoi: hỏi định dạng tệp (bảng hồ sơ đã có ô chọn định dạng)
        public void BtForm(string id, string name, bool parcel, Parcel chon, bool hoi)
        {
            var extra = new StringBuilder();
            // phiếu đo đạc, trích lục, bản vẽ tách thửa: chọn định dạng như M8 Mobile
            if (id == "thp" || id == "ttl" || id == "t2b")
            {
                int k = Math.Max(0, Array.IndexOf(DdMa, Cfg.I.DinhDang ?? "pdf"));
                if (hoi)
                {
                    var r = Ask.Show(this, name, new F("f", "Định dạng tệp", DdTen[k]) { Opt = DdTen }); if (r == null) return;
                    k = Math.Max(0, Array.IndexOf(DdTen, r["f"])); Cfg.I.DinhDang = DdMa[k]; Cfg.I.Save();
                }
                extra.Append(",\"fmt\":" + Web.J(DdMa[k]));
            }
            // Mẫu số 22: chạm vào trong thửa mới (sau Tách thửa / Hợp thửa) hoặc lập phương án tách, hợp
            if (id == "t2b") { BanVeTachHop(name, extra.ToString()); return; }
            // lưu .gtp và tờ bản đồ trước khi gửi: bộ máy lập mẫu đọc lại tệp trên đĩa nên hồ sơ theo đúng bản mới nhất
            Action<Parcel> go = p =>
            {
                if (Dc.G != null) try { Dc.Commit(); } catch (Exception ex) { Program.Log(ex); }
                if (Dc.G != null && Dc.G.Dirty && Confirm("Tệp thuộc tính " + Dc.G.Name + " có thay đổi chưa lưu. Hồ sơ lập theo tệp đã lưu trên đĩa.\n\nLưu tệp thuộc tính ngay?"))
                    try { Dc.G.Save(Dc.G.Path_); } catch (Exception ex) { Msg("Không lưu được " + Dc.G.Name + ": " + ex.Message); }
                Ms.Save(); Web.Send(id, name, p, extra.ToString());
                Msg("Đang lập " + name + (p != null ? " cho thửa " + p.So : "") + " … (tệp lưu vào " + Web.OutDir() + ")");
            };
            if (parcel)
            {
                if (chon != null && !chon.Virtual && Dc.P.Contains(chon)) { Cur = chon; Ms.Hilite(chon.Rings); go(chon); return; }
                Pick("Chạm vào thửa cần lập " + name, p => go(p)); return;
            }
            go(null);
        }
        // chạm thửa cho bộ máy lập mẫu (lệnh “một thửa” trong hộp thoại của mẫu); huỷ / chạm ra ngoài thửa thì báo huỷ
        public void PickFor(string prompt, Action<Parcel> done, Action cancel)
        {
            if (!NeedTopo()) { cancel(); return; }
            Msg(prompt + " (trong cửa sổ MicroStation; chuột phải để huỷ)");
            Ms.PickPoint(prompt, (x, y) =>
            {
                var p = Dc.Find(x, y); if (p == null) { Msg("Không có thửa tại điểm vừa chọn"); cancel(); return; }
                Cur = p; Ms.Hilite(p.Rings); if (attr != null && !attr.IsDisposed) attr.SelectParcel(p); done(p);
            }, () => { Msg("Đã huỷ chọn"); cancel(); });
        }
        It F_(string id, string t, string ic, bool parcel) { return new It { T = t, Ic = ic, A = () => BtForm(id, t, parcel), Map = true }; }
        // ================= HỒ SƠ TOÀN XÃ =================
        // sổ sách, bảng biểu, Excel thống kê lập từ tệp thuộc tính toàn xã (.gtp – thường là tệp đã gộp các tờ) – chỉ theo .gtp, không cần bản đồ
        It TX_(string id, string t, string ic) { return new It { T = t, Ic = ic, A = () => BtHoSoTx(id, t), Map = false }; }
        void BtHoSoTx(string id, string name)
        {
            var p = GtpTx(false); if (p == null) return;
            if (Dc.G != null && string.Equals(SafeFull(Dc.G.Path_), p, StringComparison.OrdinalIgnoreCase))
            {
                try { Dc.Commit(); } catch (Exception ex) { Program.Log(ex); }
                if (Dc.G.Dirty && Confirm("Tệp thuộc tính " + Dc.G.Name + " có thay đổi chưa lưu. Hồ sơ toàn xã lập theo tệp đã lưu trên đĩa.\n\nLưu tệp thuộc tính ngay?"))
                    try { Dc.G.Save(Dc.G.Path_); } catch (Exception ex) { Msg("Không lưu được " + Dc.G.Name + ": " + ex.Message); }
            }
            Web.TepTx = p;
            Web.Send(id, name, null, ",\"tx\":1,\"gtp\":" + Web.J(p) + ",\"gtpTen\":" + Web.J(Path.GetFileName(p)));
            Msg("Đang lập " + name + " từ " + Path.GetFileName(p) + " … (tệp lưu vào " + Web.OutDir() + ")");
        }
        static string SafeFull(string p) { try { return Path.GetFullPath(p); } catch { return p ?? ""; } }
        // tệp thuộc tính toàn xã: tệp đang kết nối có từ 2 tờ trở lên → tệp đó; không thì tệp chọn lần trước; chưa có (hoặc chọn lại) thì hỏi
        string GtpTx(bool chonLai)
        {
            var c = Cfg.I; string p = null;
            if (!chonLai && Dc.G != null && Dc.GRows.Select(r => r.To).Where(t => t != "").Distinct().Count() > 1 && File.Exists(Dc.G.Path_)) p = Dc.G.Path_;
            if (!chonLai && p == null && !string.IsNullOrEmpty(c.GtpTx) && File.Exists(c.GtpTx)) p = c.GtpTx;
            if (p == null)
            {
                using (var d = new OpenFileDialog { Filter = "Tệp thuộc tính (*.gtp)|*.gtp", Title = "Chọn tệp thuộc tính toàn xã (.gtp) – tệp đã gộp các tờ của xã (Hồ sơ toàn xã → Gộp CSDL…)" })
                {
                    var cu = !string.IsNullOrEmpty(c.GtpTx) ? c.GtpTx : Dc.G != null ? Dc.G.Path_ : "";
                    try { if (cu != "" && Directory.Exists(Path.GetDirectoryName(cu))) { d.InitialDirectory = Path.GetDirectoryName(cu); d.FileName = Path.GetFileName(cu); } } catch { }
                    if (d.ShowDialog(this) != DialogResult.OK) return null; p = d.FileName;
                }
            }
            p = SafeFull(p); if (c.GtpTx != p) { c.GtpTx = p; c.Save(); }
            return p;
        }
        public void ChonGtpTx() { var p = GtpTx(true); if (p != null) Msg("Hồ sơ toàn xã lập từ " + p); }
    }

    // ================= MÁY CHỦ CỤC BỘ CHO CỬA SỔ HỒ SƠ (bộ máy M8 Mobile: BienTapBDDC.html) =================
    // chỉ nhận kết nối từ chính máy này (127.0.0.1), có mã phiên; cửa sổ hồ sơ chỉ đọc bản đồ và .gtp, không ghi
    public static partial class Web
    {
        public const int Cong0 = 27808; public static int Cong = Cong0; public const string Ban = "2026-11-20-5";
        static TcpListener Lis; static readonly string Token = Guid.NewGuid().ToString("N");
        public static volatile string Tep = "";
        static readonly object lk = new object(); static readonly Queue<string> Q = new Queue<string>(); static int seq; static DateTime lastPoll = DateTime.MinValue;
        static readonly CultureInfo IC = CultureInfo.InvariantCulture; static readonly Encoding U8 = new UTF8Encoding(false);
        public static string Html
        {
            get
            {
                var ds = new List<string>(); try { var a = Path.GetDirectoryName(typeof(Web).Assembly.Location); if (!string.IsNullOrEmpty(a)) { ds.Add(Path.Combine(a, "BienTapBDDC")); ds.Add(a); } } catch { }
                ds.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "M8Pro"));
                foreach (var d in ds) { var f = Path.Combine(d, "BienTapBDDC.html"); if (File.Exists(f)) return f; } return null;
            }
        }
        public static bool Start()
        {
            if (Lis != null) return true;
            for (int i = 0; i < 4 && Lis == null; i++) { try { var l = new TcpListener(IPAddress.Loopback, Cong0 + i); l.Start(); Lis = l; Cong = Cong0 + i; } catch { } }
            if (Lis == null) return false;
            var t = new Thread(Loop) { IsBackground = true, Name = "BTBDDC" }; t.Start(Lis); return true;
        }
        public static void Stop() { KillEngine(); var l = Lis; Lis = null; try { if (l != null) l.Stop(); } catch { } }
        static void Loop(object o)
        {
            var l = (TcpListener)o;
            while (Lis == l)
            {
                TcpClient c; try { c = l.AcceptTcpClient(); } catch { break; }
                ThreadPool.QueueUserWorkItem(x => { var cc = (TcpClient)x; try { Handle(cc); } catch (Exception ex) { Program.Log(ex); } finally { try { cc.Close(); } catch { } } }, c);
            }
        }
        public static string J(string s)
        {
            var b = new StringBuilder("\""); foreach (var ch in s ?? "") { if (ch == '"' || ch == '\\') b.Append('\\').Append(ch); else if (ch < ' ') b.Append("\\u").Append(((int)ch).ToString("x4")); else b.Append(ch); }
            return b.Append('"').ToString();
        }
        static string N(double v) { return v.ToString("0.####", IC); }
        static long Mt(string p) { return (long)(File.GetLastWriteTimeUtc(p) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds; }
        // lệnh lập hồ sơ: bộ máy lập mẫu chạy ngầm nhận lệnh (máy không có Edge / Chrome thì mở cửa sổ hồ sơ)
        public static void Send(string cmd, string name, Parcel p) { Send(cmd, name, p, null); }
        // extra: các trường JSON thêm (bắt đầu bằng dấu phẩy), ví dụ định dạng, đường tách
        public static void Send(string cmd, string name, Parcel p, string extra)
        {
            if (!Start()) { Warn("Không mở được máy chủ cục bộ (cổng " + Cong0 + "–" + (Cong0 + 3) + " đều bận)."); return; }
            if (Html == null) { Warn("Thiếu tệp BienTapBDDC.html (bộ mẫu hồ sơ) cạnh M8ProDiaChinh.dll. Hãy chạy lại cai-dat.bat."); return; }
            var j = "{\"id\":" + Interlocked.Increment(ref seq) + ",\"cmd\":" + J(cmd) + ",\"ten\":" + J(name) + ",\"to\":" + J(Dc.To) + ",\"tep\":" + J(Tep) +
                (p != null ? ",\"x\":" + N(p.Lp[0]) + ",\"y\":" + N(p.Lp[1]) + ",\"so\":" + J(p.So) : "") + (extra ?? "") + "}";
            lock (lk) { Q.Clear(); Q.Enqueue(j); }
            lastCmd = DateTime.UtcNow;
            if (NgamOk && Engine()) { WatchEngine(j); return; }
            if (!Cfg.I.ChayNgam) KillEngine();
            Show(false);
        }
        public static void Show(bool force)
        {
            if (!Start()) { Warn("Không mở được máy chủ cục bộ (cổng " + Cong0 + "–" + (Cong0 + 3) + " đều bận)."); return; }
            if (Html == null) { Warn("Thiếu tệp BienTapBDDC.html (bộ mẫu hồ sơ) cạnh M8ProDiaChinh.dll. Hãy chạy lại cai-dat.bat."); return; }
            if (force || (DateTime.UtcNow - lastPoll).TotalSeconds > 4) { if (!Front()) Open(); } else Front();
        }
        // hộp thông báo tách riêng: Send/Show không phụ thuộc WinForms khi không cần báo lỗi
        static void Warn(string m) { MessageBox.Show(m, Bt.Ten, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        static void Open()
        {
            var u = "http://127.0.0.1:" + Cong + "/";
            var ds = new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("LOCALAPPDATA") };
            foreach (var rel in new[] { @"Microsoft\Edge\Application\msedge.exe", @"Google\Chrome\Application\chrome.exe" })
                foreach (var d in ds)
                {
                    if (string.IsNullOrEmpty(d)) continue; var f = Path.Combine(d, rel); if (!File.Exists(f)) continue;
                    try { Process.Start(new ProcessStartInfo(f, "--app=" + u + " --window-size=1200,860") { UseShellExecute = false }); return; } catch (Exception ex) { Program.Log(ex); }
                }
            try { Process.Start(u); } catch (Exception ex) { Program.Log(ex); }
        }
        [DllImport("user32")] static extern bool EnumWindows(EnumProc f, IntPtr l);
        [DllImport("user32", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
        [DllImport("user32")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32")] static extern bool ShowWindow(IntPtr h, int c);
        [DllImport("user32")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
        delegate bool EnumProc(IntPtr h, IntPtr l);
        // đưa cửa sổ hồ sơ lên trước: cửa sổ trình duyệt (Edge, Chrome…) có tiêu đề mang tên công cụ;
        // bỏ qua cửa sổ của chính MicroStation (cửa sổ công cụ cũng mang tên này)
        static bool Front()
        {
            IntPtr hit = IntPtr.Zero; uint me = 0; try { me = (uint)Process.GetCurrentProcess().Id; } catch { }
            try
            {
                EnumWindows((h, l) =>
                {
                    if (!IsWindowVisible(h)) return true; uint pid; GetWindowThreadProcessId(h, out pid); if (pid == me) return true;
                    var cn = new StringBuilder(128); GetClassNameW(h, cn, 128); var k = cn.ToString();
                    if (!k.StartsWith("Chrome_WidgetWin", StringComparison.Ordinal) && k != "MozillaWindowClass") return true;
                    var sb = new StringBuilder(512); GetWindowTextW(h, sb, 512); if (sb.ToString().IndexOf(Bt.Ten, StringComparison.OrdinalIgnoreCase) >= 0) { hit = h; return false; }
                    return true;
                }, IntPtr.Zero);
                if (hit == IntPtr.Zero) return false; if (IsIconic(hit)) ShowWindow(hit, 9); SetForegroundWindow(hit); return true;
            }
            catch { return false; }
        }

        // ---------- HTTP tối giản ----------
        class Req { public string Method, Path; public Dictionary<string, string> Q = new Dictionary<string, string>(), H = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); public byte[] Body; }
        static Req Read(NetworkStream s)
        {
            var buf = new byte[65536]; var acc = new MemoryStream(); int he = -1; string txt = "";
            while (he < 0)
            {
                int n = s.Read(buf, 0, buf.Length); if (n <= 0) return null; acc.Write(buf, 0, n); if (acc.Length > 262144) return null;
                txt = Encoding.ASCII.GetString(acc.GetBuffer(), 0, (int)acc.Length); he = txt.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            }
            var lines = txt.Substring(0, he).Split(new[] { "\r\n" }, StringSplitOptions.None); var rl = lines[0].Split(' '); if (rl.Length < 2) return null;
            var r = new Req { Method = rl[0].ToUpperInvariant() };
            for (int i = 1; i < lines.Length; i++) { int k = lines[i].IndexOf(':'); if (k > 0) r.H[lines[i].Substring(0, k).Trim()] = lines[i].Substring(k + 1).Trim(); }
            var u = rl[1]; int qi = u.IndexOf('?'); r.Path = Uri.UnescapeDataString(qi >= 0 ? u.Substring(0, qi) : u);
            if (qi >= 0) foreach (var kv in u.Substring(qi + 1).Split('&')) { if (kv == "") continue; int e = kv.IndexOf('='); string a = e >= 0 ? kv.Substring(0, e) : kv, b = e >= 0 ? kv.Substring(e + 1) : ""; r.Q[Uri.UnescapeDataString(a.Replace('+', ' '))] = Uri.UnescapeDataString(b.Replace('+', ' ')); }
            int len = 0; string cl; if (r.H.TryGetValue("content-length", out cl)) int.TryParse(cl, out len);
            r.Body = new byte[Math.Max(0, len)]; int have = (int)acc.Length - (he + 4), o = Math.Min(Math.Max(have, 0), len);
            if (o > 0) Array.Copy(acc.GetBuffer(), he + 4, r.Body, 0, o);
            while (o < len) { int n = s.Read(r.Body, o, len - o); if (n <= 0) break; o += n; }
            return r;
        }
        static void Send(NetworkStream s, int code, string type, byte[] body, string extra)
        {
            var st = code == 200 ? "OK" : code == 403 ? "Forbidden" : code == 404 ? "Not Found" : code == 405 ? "Method Not Allowed" : "Internal Server Error";
            var h = "HTTP/1.1 " + code + " " + st + "\r\nContent-Type: " + type + "\r\nContent-Length: " + body.Length + "\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n" + (extra ?? "") + "\r\n";
            var hb = Encoding.ASCII.GetBytes(h); s.Write(hb, 0, hb.Length); if (body.Length > 0) s.Write(body, 0, body.Length); s.Flush();
        }
        static void Text(NetworkStream s, int code, string t) { Send(s, code, "text/plain; charset=utf-8", U8.GetBytes(t), null); }
        static void Json(NetworkStream s, string j) { Send(s, 200, "application/json; charset=utf-8", U8.GetBytes(j), null); }
        static byte[] ReadAll(string p)
        {
            using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            { var b = new byte[fs.Length]; int o = 0; while (o < b.Length) { int n = fs.Read(b, o, b.Length - o); if (n <= 0) break; o += n; } return b; }
        }
        // chỉ tệp bản đồ đang mở và tệp .dgn / .gtp cùng thư mục
        // tệp thuộc tính toàn xã người dùng chọn cho Hồ sơ toàn xã (đọc được dù ở thư mục khác bản đồ đang mở)
        public static string TepTx = "";
        static string Allowed(string p)
        {
            var act = Tep; if (string.IsNullOrEmpty(p)) return null; string f;
            try { f = Path.GetFullPath(p); } catch { return null; }
            var ext = Path.GetExtension(f).ToLowerInvariant(); if (ext != ".dgn" && ext != ".gtp") return null;
            if (ext == ".gtp" && !string.IsNullOrEmpty(TepTx) && string.Equals(f, TepTx, StringComparison.OrdinalIgnoreCase)) return f;
            if (string.IsNullOrEmpty(act)) return null;
            if (string.Equals(f, act, StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetDirectoryName(f), Path.GetDirectoryName(act), StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }
        // thư viện đi kèm (đọc .gtp: sql.js – MIT; xuất Excel: SheetJS – Apache-2.0) để cửa sổ hồ sơ chạy cả khi máy không có mạng
        static readonly Dictionary<string, string> Libs = new Dictionary<string, string> { { "sql-wasm.js", "application/javascript" }, { "sql-wasm.wasm", "application/wasm" }, { "xlsx.full.min.js", "application/javascript" } };
        static void Lib(NetworkStream s, string name)
        {
            string type, h = Html; if (h == null || !Libs.TryGetValue(name, out type)) { Text(s, 404, "Không có"); return; }
            var f = Path.Combine(Path.Combine(Path.GetDirectoryName(h), "lib"), name); if (!File.Exists(f)) { Text(s, 404, "Không có"); return; }
            Send(s, 200, type, ReadAll(f), null);
        }
        static void Handle(TcpClient c)
        {
            c.ReceiveTimeout = 20000; c.SendTimeout = 120000; var s = c.GetStream(); var r = Read(s); if (r == null) return;
            string host, org, v;
            if (!r.H.TryGetValue("host", out host) || (host != "127.0.0.1:" + Cong && host != "localhost:" + Cong)) { Text(s, 403, "Sai địa chỉ"); return; }
            if (r.H.TryGetValue("origin", out org) && org != "http://127.0.0.1:" + Cong && org != "http://localhost:" + Cong) { Text(s, 403, "Không cho phép"); return; }
            var p = r.Path;
            if (r.Method == "GET" && (p == "/" || p == "/index.html" || p == "/BienTapBDDC.html")) { var h = Html; if (h == null) { Text(s, 404, "Thiếu BienTapBDDC.html"); return; } Send(s, 200, "text/html; charset=utf-8", ReadAll(h), null); return; }
            if (p == "/api/ping") { Text(s, 200, "BTBDDC addin " + Ban); return; }
            if (p == "/api/token")
            {
                if (r.H.TryGetValue("sec-fetch-site", out v) && v != "same-origin" && v != "none") { Text(s, 403, "Không cho phép"); return; }
                Json(s, "{\"token\":" + J(Token) + ",\"ban\":" + J(Ban) + ",\"addin\":true}"); return;
            }
            if (p == "/api/bye") { Text(s, 200, "ok"); return; }
            if (r.Method == "GET" && p.StartsWith("/lib/")) { Lib(s, p.Substring(5)); return; }
            if (!p.StartsWith("/api/")) { Text(s, 404, "Không có"); return; }
            if (!r.H.TryGetValue("x-btb", out v) || v != Token) { Text(s, 403, "Thiếu mã phiên — tải lại cửa sổ hồ sơ"); return; }
            // yêu cầu từ bộ máy lập mẫu chạy ngầm (trang mở với ?may=1)
            string rf; bool may = r.H.TryGetValue("referer", out rf) && rf.IndexOf("may=1", StringComparison.Ordinal) >= 0; if (may) lastSeen = DateTime.UtcNow;
            try
            {
                if (EngineApi(p, r, s)) return;
                switch (p)
                {
                    case "/api/trangthai":
                        {
                            var f = Tep; bool ok = !string.IsNullOrEmpty(f) && File.Exists(f);
                            Json(s, "{\"ms\":" + (ok ? "true" : "false") + ",\"tep\":" + J(ok ? f : "") + ",\"ten\":" + J(ok ? Path.GetFileName(f) : "") + ",\"mt\":" + (ok ? Mt(f) : 0) + ",\"sz\":" + (ok ? new FileInfo(f).Length : 0) +
                                ",\"loi\":" + J(ok ? "" : "Chưa kết nối bản đồ trong MicroStation") + ",\"ban\":" + J(Ban) + ",\"addin\":true}");
                            return;
                        }
                    case "/api/doc":
                        {
                            string q; r.Q.TryGetValue("p", out q); var f = Allowed(q); if (f == null) { Text(s, 403, "Chỉ đọc được tệp bản đồ đang mở và tệp .dgn, .gtp cùng thư mục"); return; }
                            if (!File.Exists(f)) { Text(s, 404, "Không thấy tệp"); return; }
                            Send(s, 200, "application/octet-stream", ReadAll(f), "X-MTime: " + Mt(f) + "\r\n"); return;
                        }
                    case "/api/thumuc":
                        {
                            var act = Tep; if (string.IsNullOrEmpty(act)) { Json(s, "{\"thumuc\":\"\",\"tep\":[]}"); return; }
                            var d = Path.GetDirectoryName(act); var sb = new StringBuilder();
                            foreach (var f in Directory.GetFiles(d)) { var e = Path.GetExtension(f).ToLowerInvariant(); if (e != ".dgn" && e != ".gtp") continue; if (sb.Length > 0) sb.Append(','); sb.Append("{\"ten\":" + J(Path.GetFileName(f)) + ",\"p\":" + J(f) + ",\"sz\":" + new FileInfo(f).Length + ",\"mt\":" + Mt(f) + "}"); }
                            Json(s, "{\"thumuc\":" + J(d) + ",\"tep\":[" + sb + "]}"); return;
                        }
                    case "/api/ghi": Text(s, 403, "Cửa sổ hồ sơ chỉ lập hồ sơ. Ranh, nhãn và thuộc tính được ghi bằng các lệnh của " + Bt.Ten + " trong MicroStation (menu Bản đồ, Bảng thuộc tính)."); return;
                    case "/api/xem":
                        {
                            double x1, y1, x2, y2; string a, b, cc, d;
                            if (r.Q.TryGetValue("x1", out a) && r.Q.TryGetValue("y1", out b) && r.Q.TryGetValue("x2", out cc) && r.Q.TryGetValue("y2", out d) &&
                                double.TryParse(a, NumberStyles.Float, IC, out x1) && double.TryParse(b, NumberStyles.Float, IC, out y1) && double.TryParse(cc, NumberStyles.Float, IC, out x2) && double.TryParse(d, NumberStyles.Float, IC, out y2))
                                Program.Ui(() => Ms.ZoomTo(x1, y1, x2, y2));
                            Json(s, "{\"ok\":true}"); return;
                        }
                    case "/api/lenh":
                        {
                            // bộ máy lập mẫu đang chạy: lệnh chỉ giao cho bộ máy, cửa sổ hồ sơ (nếu có mở) không nhận
                            if (!may && NgamOk && EngineRunning) { Json(s, "{}"); return; }
                            if (!may) lastPoll = DateTime.UtcNow; string j = null; lock (lk) { if (Q.Count > 0) j = Q.Dequeue(); }
                            Json(s, j ?? "{}"); return;
                        }
                    case "/api/ngat": Json(s, "{\"ok\":true}"); return;
                    case "/api/dvhc":
                        {
                            // đơn vị hành chính, cơ quan thiết lập trong công cụ → cửa sổ hồ sơ in đúng trên các mẫu
                            var cf = Cfg.I; Json(s, "{\"dv\":" + J(cf.Place) + ",\"vpdk\":" + J(cf.CoQuan ?? "") + ",\"maxa\":" + J(cf.MaXa ?? "") + ",\"to\":" + J(Dc.To) + ",\"tyle\":" + cf.TyLe + "}"); return;
                        }
                    default: Text(s, 404, "Không có"); return;
                }
            }
            catch (Exception ex) { Program.Log(ex); try { Text(s, 500, ex.Message); } catch { } }
        }
    }
}
