// M8 Pro Địa chính – hình học và dựng thửa (đồ thị phẳng). © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Linq;

namespace M8Pro
{
    public static class Geo
    {
        public static double D2(double ax, double ay, double bx, double by) { return (ax - bx) * (ax - bx) + (ay - by) * (ay - by); }
        public static double Dist(double ax, double ay, double bx, double by) { return Math.Sqrt(D2(ax, ay, bx, by)); }
        // khoảng cách bình phương điểm → đoạn; t: tham số chiếu; qx,qy: chân
        public static double SegPt(double px, double py, double ax, double ay, double bx, double by, out double t, out double qx, out double qy)
        {
            double dx = bx - ax, dy = by - ay, L = dx * dx + dy * dy;
            t = L > 0 ? ((px - ax) * dx + (py - ay) * dy) / L : 0; if (t < 0) t = 0; if (t > 1) t = 1;
            qx = ax + t * dx; qy = ay + t * dy; return (px - qx) * (px - qx) + (py - qy) * (py - qy);
        }
        // giao 2 đoạn (inf: giao 2 đường thẳng). Trả false nếu không giao
        public static bool SegX(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy, bool inf, out double x, out double y, out double t, out double u)
        {
            x = y = t = u = 0; double r1 = bx - ax, r2 = by - ay, s1 = dx - cx, s2 = dy - cy, den = r1 * s2 - r2 * s1;
            if (Math.Abs(den) < 1e-15) return false;
            t = ((cx - ax) * s2 - (cy - ay) * s1) / den; u = ((cx - ax) * r2 - (cy - ay) * r1) / den;
            if (!inf && (t < -1e-9 || t > 1 + 1e-9 || u < -1e-9 || u > 1 + 1e-9)) return false;
            x = ax + t * r1; y = ay + t * r2; return true;
        }
        // diện tích có dấu (>0: ngược chiều kim đồng hồ)
        public static double Area(double[] p)
        {
            double a = 0; int n = p.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++) a += (p[2 * j] + p[2 * i]) * (p[2 * j + 1] - p[2 * i + 1]);
            return -a / 2;
        }
        public static double Len(double[] p, bool closed)
        {
            double s = 0; int n = p.Length / 2;
            for (int i = 1; i < n; i++) s += Dist(p[2 * i - 2], p[2 * i - 1], p[2 * i], p[2 * i + 1]);
            if (closed && n > 2) s += Dist(p[0], p[1], p[2 * n - 2], p[2 * n - 1]);
            return s;
        }
        public static bool InPoly(double x, double y, double[] p)
        {
            bool c = false; int n = p.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = p[2 * i], yi = p[2 * i + 1], xj = p[2 * j], yj = p[2 * j + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) c = !c;
            }
            return c;
        }
        public static double[] Centroid(double[] p)
        {
            double a = 0, cx = 0, cy = 0; int n = p.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++) { double f = p[2 * j] * p[2 * i + 1] - p[2 * i] * p[2 * j + 1]; a += f; cx += (p[2 * j] + p[2 * i]) * f; cy += (p[2 * j + 1] + p[2 * i + 1]) * f; }
            if (Math.Abs(a) < 1e-12) { double sx = 0, sy = 0; for (int i = 0; i < n; i++) { sx += p[2 * i]; sy += p[2 * i + 1]; } return new[] { sx / n, sy / n }; }
            return new[] { cx / (3 * a), cy / (3 * a) };
        }
        public static double[] BBox(double[] p)
        {
            double a = double.MaxValue, b = double.MaxValue, c = double.MinValue, d = double.MinValue;
            for (int i = 0; i < p.Length; i += 2) { a = Math.Min(a, p[i]); c = Math.Max(c, p[i]); b = Math.Min(b, p[i + 1]); d = Math.Max(d, p[i + 1]); }
            return new[] { a, b, c, d };
        }
        // điểm đặt nhãn: điểm trong thửa xa ranh nhất (lưới thô rồi tinh chỉnh)
        public static double[] LabelPt(List<double[]> rings)
        {
            double[] o = rings[0], bb = BBox(o);
            Func<double, double, bool> inside = (x, y) => InPoly(x, y, o) && !rings.Skip(1).Any(h => InPoly(x, y, h));
            Func<double, double, double> dEdge = (x, y) =>
            {
                double m = double.MaxValue, t, qx, qy;
                foreach (var r in rings) { int n = r.Length / 2; for (int i = 0, j = n - 1; i < n; j = i++) { double d = SegPt(x, y, r[2 * j], r[2 * j + 1], r[2 * i], r[2 * i + 1], out t, out qx, out qy); if (d < m) m = d; } }
                return Math.Sqrt(m);
            };
            double[] best = null; double bd = -1; var c = Centroid(o);
            if (inside(c[0], c[1])) { best = c; bd = dEdge(c[0], c[1]); }
            int N = 14;
            for (int gx = 0; gx <= N; gx++) for (int gy = 0; gy <= N; gy++)
                {
                    double x = bb[0] + (bb[2] - bb[0]) * (gx + .5) / (N + 1), y = bb[1] + (bb[3] - bb[1]) * (gy + .5) / (N + 1);
                    if (!inside(x, y)) continue; double d = dEdge(x, y); if (d > bd) { bd = d; best = new[] { x, y }; }
                }
            if (best == null) return c;
            double st = Math.Max(bb[2] - bb[0], bb[3] - bb[1]) / (N + 1) / 2;
            int[,] dir = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 }, { 1, 1 }, { -1, -1 }, { 1, -1 }, { -1, 1 } };
            for (int k = 0; k < 14; k++)
            {
                bool imp = false;
                for (int q = 0; q < 8; q++) { double x = best[0] + dir[q, 0] * st, y = best[1] + dir[q, 1] * st; if (!inside(x, y)) continue; double d = dEdge(x, y); if (d > bd) { bd = d; best = new[] { x, y }; imp = true; } }
                if (!imp) st /= 2;
            }
            return best;
        }
        // rút gọn đỉnh thẳng hàng (Douglas–Peucker)
        // ---------- VN-2000 → WGS84 (như M8 Mobile): chiếu ngược TM múi 3° (k0 = 0,9999), 7 tham số theo QĐ 05/2007/QĐ-BTNMT ----------
        const double WA = 6378137, WF = 1 / 298.257223563, E2 = WF * (2 - WF), EP = E2 / (1 - E2), RAD = Math.PI / 180;
        static readonly double[] HT = { -191.90441429, -39.30318279, -111.45032835 }, HR = { -0.00928836 * Math.PI / 648000, 0.01975479 * Math.PI / 648000, -0.00427372 * Math.PI / 648000 };
        const double HS = 0.252906278e-6;
        public static double[] TmInv(double E, double N, double l0, double k0)
        {
            double x = E - 500000, mu = N / k0 / (WA * (1 - E2 / 4 - 3 * E2 * E2 / 64 - 5 * E2 * E2 * E2 / 256)), s = Math.Sqrt(1 - E2), e1 = (1 - s) / (1 + s);
            double f1 = mu + (3 * e1 / 2 - 27 * Math.Pow(e1, 3) / 32) * Math.Sin(2 * mu) + (21 * e1 * e1 / 16 - 55 * Math.Pow(e1, 4) / 32) * Math.Sin(4 * mu) + 151 * Math.Pow(e1, 3) / 96 * Math.Sin(6 * mu) + 1097 * Math.Pow(e1, 4) / 512 * Math.Sin(8 * mu);
            double C = EP * Math.Pow(Math.Cos(f1), 2), T = Math.Pow(Math.Tan(f1), 2), sf = Math.Sin(f1), N1 = WA / Math.Sqrt(1 - E2 * sf * sf), R1 = WA * (1 - E2) / Math.Pow(1 - E2 * sf * sf, 1.5), D = x / (N1 * k0);
            double lat = f1 - N1 * Math.Tan(f1) / R1 * (D * D / 2 - (5 + 3 * T + 10 * C - 4 * C * C - 9 * EP) * Math.Pow(D, 4) / 24 + (61 + 90 * T + 298 * C + 45 * T * T - 252 * EP - 3 * C * C) * Math.Pow(D, 6) / 720);
            double lon = l0 * RAD + (D - (1 + 2 * T + C) * Math.Pow(D, 3) / 6 + (5 - 2 * C + 28 * T - 3 * C * C + 8 * EP + 24 * T * T) * Math.Pow(D, 5) / 120) / Math.Cos(f1);
            return new[] { lat / RAD, lon / RAD };
        }
        static double[] Ecef(double la, double lo, double h) { double p = la * RAD, l = lo * RAD, N = WA / Math.Sqrt(1 - E2 * Math.Sin(p) * Math.Sin(p)); return new[] { (N + h) * Math.Cos(p) * Math.Cos(l), (N + h) * Math.Cos(p) * Math.Sin(l), (N * (1 - E2) + h) * Math.Sin(p) }; }
        static double[] FromEcef(double X, double Y, double Z)
        {
            double lo = Math.Atan2(Y, X), q = Math.Sqrt(X * X + Y * Y), p = Math.Atan2(Z, q * (1 - E2)), h = 0;
            for (int i = 0; i < 6; i++) { double N = WA / Math.Sqrt(1 - E2 * Math.Sin(p) * Math.Sin(p)); h = q / Math.Cos(p) - N; p = Math.Atan2(Z, q * (1 - E2 * N / (N + h))); }
            return new[] { p / RAD, lo / RAD, h };
        }
        // toạ độ VN-2000 (E = X đông, N = Y bắc, mét) → vĩ độ, kinh độ WGS84
        public static double[] Vn2000ToWgs(double E, double N, double ktt)
        {
            var l = TmInv(E, N, ktt, 0.9999); var c = Ecef(l[0], l[1], 0); double k = 1 + HS, X = c[0], Y = c[1], Z = c[2];
            var w = new[] { HT[0] + k * (X + HR[2] * Y - HR[1] * Z), HT[1] + k * (-HR[2] * X + Y + HR[0] * Z), HT[2] + k * (HR[1] * X - HR[0] * Y + Z) };
            var g = FromEcef(w[0], w[1], w[2]); return new[] { g[0], g[1] };
        }
        public static double[] Simplify(double[] p, double tol)
        {
            int n = p.Length / 2; if (n <= 2) return (double[])p.Clone();
            var keep = new bool[n]; keep[0] = keep[n - 1] = true; var st = new Stack<int[]>(); st.Push(new[] { 0, n - 1 });
            double t, qx, qy;
            while (st.Count > 0)
            {
                var ab = st.Pop(); int a = ab[0], b = ab[1], m = -1; double md = tol * tol;
                for (int i = a + 1; i < b; i++) { double d = SegPt(p[2 * i], p[2 * i + 1], p[2 * a], p[2 * a + 1], p[2 * b], p[2 * b + 1], out t, out qx, out qy); if (d > md) { md = d; m = i; } }
                if (m >= 0) { keep[m] = true; st.Push(new[] { a, m }); st.Push(new[] { m, b }); }
            }
            var o = new List<double>(); for (int i = 0; i < n; i++) if (keep[i]) { o.Add(p[2 * i]); o.Add(p[2 * i + 1]); }
            return o.ToArray();
        }
    }

    public class Seg { public double X1, Y1, X2, Y2; public int Tag; public Seg(double a, double b, double c, double d, int tag) { X1 = a; Y1 = b; X2 = c; Y2 = d; Tag = tag; } }
    public class Face { public double[] Ring; public List<double[]> Holes = new List<double[]>(); public List<HashSet<string>> HoleEdges = new List<HashSet<string>>(); public double Area; public HashSet<string> Edges; public double[] BB; }
    public class Planar
    {
        public List<double[]> Nodes = new List<double[]>();
        public List<int[]> Edges = new List<int[]>();          // [i, j, tag] với i < j
        public Dictionary<string, int[]> EdgeByKey = new Dictionary<string, int[]>();
        public List<Face> Faces = new List<Face>();
        public List<double[]> Dangles = new List<double[]>(), Overlaps = new List<double[]>();
        public int[] Degree;

        // segs: các đoạn ranh; tol: dung sai bắt nối (m)
        public static Planar Build(List<Seg> segs, double tol)
        {
            var R = new Planar(); if (segs.Count == 0) return R; if (tol <= 0) tol = 1e-4;
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var s in segs) { x0 = Math.Min(x0, Math.Min(s.X1, s.X2)); y0 = Math.Min(y0, Math.Min(s.Y1, s.Y2)); x1 = Math.Max(x1, Math.Max(s.X1, s.X2)); y1 = Math.Max(y1, Math.Max(s.Y1, s.Y2)); }
            double W = Math.Max(Math.Max(x1 - x0, y1 - y0), 1), cs = Math.Max(W / Math.Max(16, Math.Sqrt(segs.Count) * 1.5), tol * 4);
            var grid = new Dictionary<long, List<int>>();
            for (int k = 0; k < segs.Count; k++)
            {
                var s = segs[k];
                int i0 = (int)Math.Floor((Math.Min(s.X1, s.X2) - tol - x0) / cs), i1 = (int)Math.Floor((Math.Max(s.X1, s.X2) + tol - x0) / cs);
                int j0 = (int)Math.Floor((Math.Min(s.Y1, s.Y2) - tol - y0) / cs), j1 = (int)Math.Floor((Math.Max(s.Y1, s.Y2) + tol - y0) / cs);
                for (int i = i0; i <= i1; i++) for (int j = j0; j <= j1; j++) { long key = (long)i * 1000003L + j; List<int> L; if (!grid.TryGetValue(key, out L)) grid[key] = L = new List<int>(); L.Add(k); }
            }
            // điểm cắt trên mỗi đoạn
            var cuts = new List<double>[segs.Count]; for (int k = 0; k < segs.Count; k++) cuts[k] = new List<double>();
            var seen = new HashSet<long>(); double tt2 = tol * tol;
            foreach (var L in grid.Values)
                for (int a = 0; a < L.Count; a++) for (int b = a + 1; b < L.Count; b++)
                    {
                        int i = L[a], j = L[b]; long pk = i < j ? (long)i * 10000000L + j : (long)j * 10000000L + i; if (!seen.Add(pk)) continue;
                        Seg A = segs[i], B = segs[j]; double x, y, t, u, qx, qy;
                        if (Geo.SegX(A.X1, A.Y1, A.X2, A.Y2, B.X1, B.Y1, B.X2, B.Y2, false, out x, out y, out t, out u)) { cuts[i].Add(t); cuts[j].Add(u); }
                        // đầu mút đoạn này chạm (trong dung sai) vào giữa đoạn kia
                        if (Geo.SegPt(B.X1, B.Y1, A.X1, A.Y1, A.X2, A.Y2, out t, out qx, out qy) <= tt2 && t > 1e-6 && t < 1 - 1e-6) cuts[i].Add(t);
                        if (Geo.SegPt(B.X2, B.Y2, A.X1, A.Y1, A.X2, A.Y2, out t, out qx, out qy) <= tt2 && t > 1e-6 && t < 1 - 1e-6) cuts[i].Add(t);
                        if (Geo.SegPt(A.X1, A.Y1, B.X1, B.Y1, B.X2, B.Y2, out t, out qx, out qy) <= tt2 && t > 1e-6 && t < 1 - 1e-6) cuts[j].Add(t);
                        if (Geo.SegPt(A.X2, A.Y2, B.X1, B.Y1, B.X2, B.Y2, out t, out qx, out qy) <= tt2 && t > 1e-6 && t < 1 - 1e-6) cuts[j].Add(t);
                    }
            // nút: gộp điểm gần nhau
            var ng = new Dictionary<long, List<int>>(); double nc = Math.Max(tol * 2, 1e-6);
            Func<double, double, int> node = (x, y) =>
            {
                long gi = (long)Math.Floor(x / nc), gj = (long)Math.Floor(y / nc);
                for (long di = -1; di <= 1; di++) for (long dj = -1; dj <= 1; dj++)
                    {
                        List<int> L; if (!ng.TryGetValue((gi + di) * 1000003L + (gj + dj), out L)) continue;
                        foreach (int n in L) { var p = R.Nodes[n]; if (Geo.D2(p[0], p[1], x, y) <= tt2) return n; }
                    }
                int idx = R.Nodes.Count; R.Nodes.Add(new[] { x, y }); long kk = gi * 1000003L + gj; List<int> LL; if (!ng.TryGetValue(kk, out LL)) ng[kk] = LL = new List<int>(); LL.Add(idx); return idx;
            };
            var E = new Dictionary<string, int[]>(); var ovk = new HashSet<string>();
            for (int k = 0; k < segs.Count; k++)
            {
                var s = segs[k]; var ts = new List<double> { 0, 1 }; ts.AddRange(cuts[k]); ts.Sort();
                int prev = node(s.X1, s.Y1);
                for (int q = 1; q < ts.Count; q++)
                {
                    if (ts[q] - ts[q - 1] < 1e-9 && q < ts.Count - 1) continue;
                    int n = q == ts.Count - 1 ? node(s.X2, s.Y2) : node(s.X1 + (s.X2 - s.X1) * ts[q], s.Y1 + (s.Y2 - s.Y1) * ts[q]);
                    if (n != prev)
                    {
                        int a = Math.Min(prev, n), b = Math.Max(prev, n); string key = a + "_" + b; int[] ex;
                        if (!E.TryGetValue(key, out ex)) E[key] = new[] { a, b, s.Tag };
                        else if (ex[2] != s.Tag && ovk.Add(key)) R.Overlaps.Add(new[] { (R.Nodes[a][0] + R.Nodes[b][0]) / 2, (R.Nodes[a][1] + R.Nodes[b][1]) / 2 });   // đoạn ranh vẽ chồng (trùng toàn bộ hoặc một phần)
                    }
                    prev = n;
                }
            }
            R.Edges = E.Values.ToList(); R.EdgeByKey = E;
            int NN = R.Nodes.Count; var dg = new int[NN]; foreach (var e in R.Edges) { dg[e[0]]++; dg[e[1]]++; }
            R.Degree = (int[])dg.Clone();
            for (int n = 0; n < NN; n++) if (dg[n] == 1) R.Dangles.Add(R.Nodes[n]);
            // bỏ đoạn cụt trước khi tìm mặt
            var inc = new List<int>[NN]; for (int n = 0; n < NN; n++) inc[n] = new List<int>();
            for (int k = 0; k < R.Edges.Count; k++) { inc[R.Edges[k][0]].Add(k); inc[R.Edges[k][1]].Add(k); }
            var dead = new bool[R.Edges.Count]; var qd = new Stack<int>(); for (int n = 0; n < NN; n++) if (dg[n] == 1) qd.Push(n);
            while (qd.Count > 0)
            {
                int n = qd.Pop(); if (dg[n] != 1) continue; int k = -1; foreach (int c in inc[n]) if (!dead[c]) { k = c; break; }
                if (k < 0) continue; dead[k] = true; dg[n]--; int m = R.Edges[k][0] == n ? R.Edges[k][1] : R.Edges[k][0]; if (--dg[m] == 1) qd.Push(m);
            }
            // bán cạnh: sắp theo góc quanh nút
            var outs = new List<int[]>[NN]; for (int n = 0; n < NN; n++) outs[n] = new List<int[]>();
            for (int k = 0; k < R.Edges.Count; k++) { if (dead[k]) continue; var e = R.Edges[k]; outs[e[0]].Add(new[] { k, e[1] }); outs[e[1]].Add(new[] { k, e[0] }); }
            for (int n = 0; n < NN; n++) { int nn = n; outs[n].Sort((p, q) => Ang(R, nn, p[1]).CompareTo(Ang(R, nn, q[1]))); }
            var used = new HashSet<long>(); var holes = new List<Face>();
            for (int n = 0; n < NN; n++) foreach (var hm in outs[n])
                {
                    int m0 = hm[1]; if (used.Contains((long)n * NN + m0)) continue;
                    var ring = new List<double>(); var es = new HashSet<string>(); int a = n, b = m0, guard = 0; bool ok = true;
                    while (true)
                    {
                        used.Add((long)a * NN + b); ring.Add(R.Nodes[a][0]); ring.Add(R.Nodes[a][1]); es.Add(Math.Min(a, b) + "_" + Math.Max(a, b));
                        var L = outs[b]; int idx = L.FindIndex(x => x[1] == a); var nx = L[(idx - 1 + L.Count) % L.Count];
                        a = b; b = nx[1]; if (a == n && b == m0) break; if (++guard > 500000) { ok = false; break; }
                    }
                    if (!ok || ring.Count < 6) continue; var arr = ring.ToArray(); double ar = Geo.Area(arr);
                    var f = new Face { Ring = arr, Area = ar, Edges = es, BB = Geo.BBox(arr) };
                    if (ar > 1e-9) R.Faces.Add(f); else holes.Add(f);
                }
            // lỗ: vòng âm của một cụm nằm trong mặt dương nhỏ nhất chứa nó
            foreach (var h in holes)
            {
                Face best = null; double mx = (h.Ring[0] + h.Ring[2]) / 2, my = (h.Ring[1] + h.Ring[3]) / 2;
                foreach (var f in R.Faces)
                {
                    if (f.Area <= Math.Abs(h.Area) + 1e-9) continue;
                    if (h.BB[0] < f.BB[0] || h.BB[2] > f.BB[2] || h.BB[1] < f.BB[1] || h.BB[3] > f.BB[3]) continue;
                    if (!Geo.InPoly(mx, my, f.Ring)) continue; if (f.Edges.Overlaps(h.Edges)) continue;
                    if (best == null || f.Area < best.Area) best = f;
                }
                if (best != null) { best.Holes.Add(h.Ring); best.HoleEdges.Add(h.Edges); }
            }
            return R;
        }
        static double Ang(Planar R, int a, int b) { return Math.Atan2(R.Nodes[b][1] - R.Nodes[a][1], R.Nodes[b][0] - R.Nodes[a][0]); }

        // nối các cạnh thành đường gấp khúc
        public static List<double[]> Chain(List<int[]> edges, List<double[]> nodes)
        {
            var adj = new Dictionary<int, List<int[]>>(); var usedE = new bool[edges.Count]; var outp = new List<double[]>();
            for (int k = 0; k < edges.Count; k++) { var e = edges[k]; if (!adj.ContainsKey(e[0])) adj[e[0]] = new List<int[]>(); if (!adj.ContainsKey(e[1])) adj[e[1]] = new List<int[]>(); adj[e[0]].Add(new[] { e[1], k }); adj[e[1]].Add(new[] { e[0], k }); }
            Func<int, List<int>> walk = s =>
            {
                var o = new List<int> { s }; int c = s;
                while (true) { int[] nx = null; foreach (var z in adj[c]) if (!usedE[z[1]]) { nx = z; break; } if (nx == null) break; usedE[nx[1]] = true; c = nx[0]; o.Add(c); if (c == s) break; }
                return o;
            };
            foreach (var kv in adj) if (kv.Value.Count != 2) while (kv.Value.Any(z => !usedE[z[1]])) outp.Add(Flat(walk(kv.Key), nodes));
            for (int k = 0; k < edges.Count; k++) if (!usedE[k]) outp.Add(Flat(walk(edges[k][0]), nodes));
            return outp;
        }
        static double[] Flat(List<int> w, List<double[]> nodes) { var o = new double[w.Count * 2]; for (int i = 0; i < w.Count; i++) { o[2 * i] = nodes[w[i]][0]; o[2 * i + 1] = nodes[w[i]][1]; } return o; }
    }
}
