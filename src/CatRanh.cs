// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – CẮT RANH TẠI GIAO ĐIỂM. © Nguyễn Xuân Thủy
// Chỉ cắt đường thẳng (line) và đường gấp khúc (line string), và chỉ cắt đúng tại chỗ các đường ranh gặp nhau:
//  • hai đoạn cắt chéo nhau: cắt cả hai đúng tại giao điểm;
//  • đầu đường chạm giữa đường khác (chữ T): cắt đường đi qua đúng tại điểm chạm;
//  • đỉnh của đường gấp khúc nằm đúng trên đường khác (lệch không quá 1 mm): cắt cả hai tại đỉnh đó.
// Phần còn lại giữ nguyên: mọi đỉnh của đường gấp khúc giữ đúng toạ độ (cả cao độ), không bỏ đỉnh, không xoá đoạn nào – các đoạn mới nối
// liền nhau đúng theo đường cũ. Từng đường được kiểm tra lại trước khi ghi; sai khác thì giữ nguyên đường đó và báo để cắt tay.
// Đầu đường hụt / thừa trong dung sai bắt nối được đưa đúng vào điểm nối (chỉ dời đỉnh đầu): đầu thừa cắt về giao điểm, đầu hụt kéo dài
// theo hướng của nó tới đường kia, hoặc vào đỉnh / nút gần nhất, hoặc chân vuông góc. Đường đi qua không đổi hình.
// Không cắt, không sửa: vùng (shape), chuỗi phức, cung tròn, đường cong – chỗ các phần tử này cần cắt được báo để cắt tay (đầu cung tròn,
// đầu chuỗi phức chạm đường thẳng thì đường thẳng vẫn được cắt đúng tại đó). Đoạn vẽ chồng lên nhau không cắt (báo ở nhóm
// “Đoạn ranh vẽ chồng lên nhau”).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace M8Pro
{
    // một chỗ cần cắt / cần khớp / cần cắt tay (để báo lỗi, phóng tới)
    public class CatDiem { public double X, Y, D; public int N; public MEl M; public string Ly; }
    public class CatKq
    {
        public int Nut, Duong, Doan, Khop, Loi;
        public List<CatDiem> DiemCat = new List<CatDiem>(), DiemKhop = new List<CatDiem>(), DiemCong = new List<CatDiem>();
        public HashSet<long> IdCu = new HashSet<long>();                          // mã phần tử cũ đã thay bằng các đoạn mới
        public Dictionary<int, List<MEl>> Thay = new Dictionary<int, List<MEl>>(); // chỉ số trong danh sách vào → các đoạn mới
        public bool Sua;
        public string Bao()
        {
            if (!Sua) return DiemCat.Count + " chỗ chưa cắt tại giao điểm, " + DiemKhop.Count + " đầu đường chưa khớp đúng điểm" + (DiemCong.Count > 0 ? ", " + DiemCong.Count + " chỗ cần cắt / nối tay" : "");
            var o = new List<string>();
            if (Nut > 0) o.Add("cắt ranh tại " + Nut + " giao điểm (" + Duong + " đường tách thành " + Doan + " đoạn, giữ nguyên mọi đỉnh)");
            if (Khop > 0) o.Add("khớp đúng điểm " + Khop + " đầu đường");
            if (DiemCong.Count > 0) o.Add(DiemCong.Count + " chỗ cần cắt / nối tay");
            if (Loi > 0) o.Add(Loi + " đường chưa sửa được (giữ nguyên)");
            return o.Count == 0 ? "ranh đã cắt đúng tại các giao điểm" : string.Join(", ", o.ToArray());
        }
    }

    public static class CatRanh
    {
        const double EPS = 1e-6;          // hai toạ độ cách nhau dưới 1 µm coi là trùng
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public static double Tol { get { return Math.Max(Cfg.I.Tol, 1e-4); } }       // dung sai bắt nối: đầu đường hụt / thừa
        public static double TolDinh { get { return Math.Min(Tol, 0.001); } }         // đỉnh nằm trên đường khác, hai điểm coi là một: 1 mm

        class Ln
        {
            public MEl M; public int Src; public double[] P; public int N; public bool Kin, Cat, Thang;   // Cat: line / line string; Thang: đoạn thẳng thật
            public double[] Cum; public double Len; public int VB; public int[] VN;                       // VN: nút của từng đỉnh (−1: chưa)
            public List<Su> Su = new List<Su>();
        }
        class Su { public double S; public int Nut; }                                       // điểm cắt trên đường: trạm (chỉ số đoạn + t), nút
        class Ct { public int A, V, B, J; public double U, D2, Qx, Qy; }                    // đỉnh V của A gần đoạn J của B (chân Qx,Qy; khoảng cách²)
        class Cx { public int A, Ja, B, Jb; public double X, Y; public int Nut = -1; }      // đoạn Ja của A cắt đoạn Jb của B
        class Ov { public double X0, Y0, X1, Y1; }                                          // phần hai đoạn vẽ chồng lên nhau
        class Viec { public Ln Ln; public double[] Q; public List<Su> Cat; public List<double[]> Doan = new List<double[]>(), Tram = new List<double[]>(); public List<int> Doi; public List<CatDiem> Kh = new List<CatDiem>(); }

        // đọc các đường trên các lớp rồi cắt (vung: chỉ cắt các chỗ nằm trong khung x0,y0,x1,y1; null: cả tờ)
        public static CatKq Chay(ICollection<int> lv, double[] vung, bool sua) { return Chay(Ms.Read(lv, Bt.LineTypes), vung, sua); }

        public static CatKq Chay(List<MEl> els, double[] vung, bool sua)
        {
            var kq = new CatKq { Sua = sua }; double tol = Tol, td = TolDinh, t2 = tol * tol, d2 = td * td;
            // ---- 1. các đường ----
            var L = new List<Ln>(); int vb = 0;
            for (int i = 0; i < els.Count; i++) foreach (var ln in Doc(els[i], i)) { ln.VB = vb; vb += ln.N; L.Add(ln); }
            if (!L.Any(x => x.Cat)) return kq;
            var sL = new List<int>(); var sJ = new List<int>();
            for (int a = 0; a < L.Count; a++) { var p = L[a].P; for (int j = 0; j + 1 < L[a].N; j++) if (p[2 * j] != p[2 * j + 2] || p[2 * j + 1] != p[2 * j + 3]) { sL.Add(a); sJ.Add(j); } }
            int S = sL.Count; if (S == 0) return kq;
            // ---- 2. lưới đoạn, dò các cặp đoạn gần nhau (mỗi cặp xét đúng một lần ở ô đầu tiên chung của hai đoạn) ----
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var ln in L) for (int k = 0; k < ln.N; k++) { x0 = Math.Min(x0, ln.P[2 * k]); x1 = Math.Max(x1, ln.P[2 * k]); y0 = Math.Min(y0, ln.P[2 * k + 1]); y1 = Math.Max(y1, ln.P[2 * k + 1]); }
            double W = Math.Max(Math.Max(x1 - x0, y1 - y0), 1), cs = Math.Max(W / Math.Max(16, Math.Sqrt(S) * 1.5), tol * 4);
            var gi0 = new int[S]; var gj0 = new int[S]; var bb = new double[S * 4]; var grid = new Dictionary<long, List<int>>();
            for (int s = 0; s < S; s++)
            {
                var P = L[sL[s]].P; int j = sJ[s]; double ax = P[2 * j], ay = P[2 * j + 1], bx = P[2 * j + 2], by = P[2 * j + 3];
                bb[4 * s] = Math.Min(ax, bx) - tol; bb[4 * s + 1] = Math.Min(ay, by) - tol; bb[4 * s + 2] = Math.Max(ax, bx) + tol; bb[4 * s + 3] = Math.Max(ay, by) + tol;
                int i0 = (int)Math.Floor((bb[4 * s] - x0) / cs) + 1, i1 = (int)Math.Floor((bb[4 * s + 2] - x0) / cs) + 1, j0 = (int)Math.Floor((bb[4 * s + 1] - y0) / cs) + 1, j1 = (int)Math.Floor((bb[4 * s + 3] - y0) / cs) + 1;
                gi0[s] = i0; gj0[s] = j0;
                for (int ii = i0; ii <= i1; ii++) for (int jj = j0; jj <= j1; jj++) { long key = (long)ii * 1000003L + jj; List<int> C; if (!grid.TryGetValue(key, out C)) grid[key] = C = new List<int>(); C.Add(s); }
            }
            var cts = new List<Ct>(); var cxs = new List<Cx>(); var ovs = new Dictionary<long, List<Ov>>(); var seen = new HashSet<long>();
            foreach (var kv in grid)
            {
                int ci = (int)(kv.Key / 1000003L), cj = (int)(kv.Key % 1000003L); var C = kv.Value;
                for (int u = 0; u < C.Count; u++) for (int w = u + 1; w < C.Count; w++)
                    {
                        int sa = C[u], sb = C[w];
                        if (Math.Max(gi0[sa], gi0[sb]) != ci || Math.Max(gj0[sa], gj0[sb]) != cj) continue;
                        if (bb[4 * sa] > bb[4 * sb + 2] || bb[4 * sb] > bb[4 * sa + 2] || bb[4 * sa + 1] > bb[4 * sb + 3] || bb[4 * sb + 1] > bb[4 * sa + 3]) continue;
                        Cap(L, sL, sJ, sa, sb, tol, td, S, cts, cxs, ovs, seen);
                    }
            }
            // ---- 3. lọc: trong khung, không thuộc phần vẽ chồng; chỗ chạm phần tử không cắt được (vùng, chuỗi phức, cung tròn) → báo cắt tay ----
            Func<double, double, bool> trong = (x, y) => vung == null || (x >= vung[0] && x <= vung[2] && y >= vung[1] && y <= vung[3]);
            Func<int, int, double, double, bool> chong = (a, b, x, y) =>
            {
                List<Ov> l; if (!ovs.TryGetValue(K2(a, b), out l)) return false; double t, qx, qy;
                foreach (var o in l) if (Geo.SegPt(x, y, o.X0, o.Y0, o.X1, o.Y1, out t, out qx, out qy) <= d2) return true;
                return false;
            };
            var tay = new List<CatDiem>(); var ct2 = new List<Ct>();
            foreach (var c in cts)
            {
                Ln A = L[c.A], B = L[c.B]; double px = A.P[2 * c.V], py = A.P[2 * c.V + 1];
                if (!trong(px, py) || chong(c.A, c.B, px, py)) continue;
                if (A.Cat && B.Cat) { ct2.Add(c); continue; }
                if (A.Cat)
                {
                    // đỉnh của đường cắt được chạm phần tử không cắt được: chỉ nối vào đầu thật của phần tử đó (đầu cung tròn, đầu chuỗi phức)
                    if (DauGan(B, px, py, Dau(A, c.V) ? t2 : d2) >= 0) ct2.Add(c); else tay.Add(new CatDiem { X = px, Y = py, M = B.M, Ly = LyTay(B) });
                    continue;
                }
                // đỉnh của phần tử không cắt được chạm đường cắt được: đầu thật → cắt đường kia tại đó; đỉnh khác (đỉnh vùng…) → báo
                if (Dau(A, c.V)) ct2.Add(c); else tay.Add(new CatDiem { X = px, Y = py, M = A.M, Ly = LyTay(A) });
            }
            var cx2 = new List<Cx>();
            foreach (var c in cxs)
            {
                if (!trong(c.X, c.Y) || chong(c.A, c.B, c.X, c.Y)) continue;
                if (L[c.A].Cat && L[c.B].Cat) cx2.Add(c);
                else { var o = L[c.A].Cat ? L[c.B] : L[c.A]; tay.Add(new CatDiem { X = c.X, Y = c.Y, M = o.M, Ly = LyTay(o) }); }
            }
            // thứ tự cố định (kết quả như nhau mỗi lần chạy)
            ct2.Sort((p, q) => p.A != q.A ? p.A.CompareTo(q.A) : p.V != q.V ? p.V.CompareTo(q.V) : p.B != q.B ? p.B.CompareTo(q.B) : p.J.CompareTo(q.J));
            cx2.Sort((p, q) => p.A != q.A ? p.A.CompareTo(q.A) : p.Ja != q.Ja ? p.Ja.CompareTo(q.Ja) : p.B != q.B ? p.B.CompareTo(q.B) : p.Jb.CompareTo(q.Jb));
            // ---- 4. nút cố định: đầu thật của phần tử không cắt được, giao điểm của hai đoạn (đúng điểm, nằm trên cả hai đường) ----
            var R = new Nut(tol, td);
            foreach (var c in ct2)
            {
                Ln A = L[c.A], B = L[c.B];
                if (!A.Cat) { if (A.VN[c.V] < 0) A.VN[c.V] = R.Get(A.P[2 * c.V], A.P[2 * c.V + 1]); }
                else if (!B.Cat) { int e = DauGan(B, A.P[2 * c.V], A.P[2 * c.V + 1], Dau(A, c.V) ? t2 : d2); if (e >= 0 && B.VN[e] < 0) B.VN[e] = R.Get(B.P[2 * e], B.P[2 * e + 1]); }
            }
            foreach (var c in cx2) c.Nut = R.Get(c.X, c.Y);
            foreach (var c in ct2) { Ln A = L[c.A], B = L[c.B]; if (A.Cat || A.VN[c.V] < 0) continue; int n = A.VN[c.V]; if (B.Cat && NamTren(B, c.J, R.X[n], R.Y[n], d2)) Tram(B, c.J, n, R, d2); }
            foreach (var c in cx2) { Tram(L[c.A], c.Ja, c.Nut, R, d2); Tram(L[c.B], c.Jb, c.Nut, R, d2); }
            // ---- 4b. đỉnh giữa của đường gấp khúc nằm trên đường khác (≤ 1 mm): điểm nối nằm đúng trên đường đi qua (đường đi qua không gãy),
            //      đỉnh chỉ dời tới đó (không quá 1 mm). Chạm đúng đầu của đường kia thì để bước 5 (đầu đường kia nối vào đỉnh này)
            var dinhCt = new SortedDictionary<long, List<Ct>>();
            foreach (var c in ct2)
            {
                Ln A = L[c.A], B = L[c.B]; if (!A.Cat || Dau(A, c.V)) continue;
                if (B.Cat && ((c.J == 0 && c.U <= 0) || (c.J + 1 == B.N - 1 && c.U >= 1)) && DauGan(B, A.P[2 * c.V], A.P[2 * c.V + 1], d2) >= 0) continue;
                long k = (long)(A.VB + c.V); List<Ct> l; if (!dinhCt.TryGetValue(k, out l)) dinhCt[k] = l = new List<Ct>(); l.Add(c);
            }
            foreach (var kv in dinhCt)
            {
                var cs1 = kv.Value; var A = L[cs1[0].A]; int v = cs1[0].V; double px = A.P[2 * v], py = A.P[2 * v + 1]; int n = A.VN[v];
                if (n < 0) n = R.Find(px, py);
                if (n < 0)
                {
                    double bd = d2 * 1.0001, bx = 0, by = 0; bool co = false;
                    foreach (var c in cs1)
                    {
                        var B = L[c.B]; if (!B.Cat) continue;
                        for (int q = 0; q < 2; q++) { int k = c.J + q; if (Dau(B, k)) continue; double x = Vx(B, k, R), y = Vy(B, k, R), d = Geo.D2(x, y, px, py); if (d <= bd) { bd = d; bx = x; by = y; co = true; } }
                    }
                    if (!co) { Ct m = null; foreach (var c in cs1) if (L[c.B].Cat && (m == null || c.D2 < m.D2)) m = c; if (m != null) { bx = m.Qx; by = m.Qy; co = true; } }
                    if (!co) continue;
                    n = R.Get(bx, by);
                }
                A.VN[v] = n; A.Su.Add(new Su { S = v, Nut = n });
                foreach (var c in cs1) { var B = L[c.B]; if (B.Cat && NamTren(B, c.J, R.X[n], R.Y[n], d2)) Tram(B, c.J, n, R, d2); }
            }
            // ---- 5. đầu đường: mỗi đầu chọn đúng một điểm nối (thứ tự cố định), rồi cắt các đường đi qua đúng điểm đó ----
            var dauCt = new SortedDictionary<long, List<Ct>>();
            foreach (var c in ct2) { var A = L[c.A]; if (!A.Cat || !Dau(A, c.V)) continue; long k = 2L * c.A + (c.V == 0 ? 0 : 1); List<Ct> l; if (!dauCt.TryGetValue(k, out l)) dauCt[k] = l = new List<Ct>(); l.Add(c); }
            var xDoan = new Dictionary<long, List<Cx>>();
            Action<int, int, Cx> themX = (a, j, c) => { long k = K2d(a, j); List<Cx> l; if (!xDoan.TryGetValue(k, out l)) xDoan[k] = l = new List<Cx>(); l.Add(c); };
            foreach (var c in cx2) { themX(c.A, c.Ja, c); themX(c.B, c.Jb, c); }
            foreach (var kv in dauCt)
            {
                int a = (int)(kv.Key / 2); var A = L[a]; int v = kv.Key % 2 == 0 ? 0 : A.N - 1; var cs1 = kv.Value; int n = A.VN[v];
                if (n < 0)
                {
                    bool songSong; n = Chon(L, a, v, cs1, R, xDoan, tol, td, out songSong);
                    if (n < 0)
                    {
                        if (songSong) { double dm = Math.Sqrt(cs1.Where(c => L[c.B].Cat).Min(c => c.D2)); tay.Add(new CatDiem { X = A.P[2 * v], Y = A.P[2 * v + 1], D = dm, M = A.M, Ly = "Đầu đường cách đường ranh khác " + Dc.F(dm * 1000, 1) + " mm, chạy gần song song – kiểm tra, nối tay" }); }
                        continue;
                    }
                    A.VN[v] = n;
                }
                foreach (var c in cs1) { var B = L[c.B]; if (B.Cat && NamTren(B, c.J, R.X[n], R.Y[n], d2)) Tram(B, c.J, n, R, d2); }
            }
            // ---- 6. dựng các đoạn mới của từng đường, kiểm tra lại: nối liền, đi qua đúng mọi đỉnh cũ, điểm cắt nằm trên đường cũ ----
            var viec = new List<Viec>();
            foreach (var ln in L)
            {
                if (!ln.Cat) continue;
                var cat = Gon(ln, tol); var Q = (double[])ln.P.Clone();
                for (int v = 0; v < ln.N; v++) if (ln.VN[v] >= 0) { Q[2 * v] = R.X[ln.VN[v]]; Q[2 * v + 1] = R.Y[ln.VN[v]]; }
                var doi = new List<int>(); for (int v = 0; v < ln.N; v++) if (Q[2 * v] != ln.P[2 * v] || Q[2 * v + 1] != ln.P[2 * v + 1]) doi.Add(v);   // dời dù rất nhỏ: nút dùng chung đúng một toạ độ
                if (cat.Count == 0 && doi.Count == 0) continue;
                var w = new Viec { Ln = ln, Q = Q, Cat = cat, Doi = doi }; Doan(ln, Q, cat, R, w.Doan, w.Tram);
                foreach (var v in doi) { if (!Dau(ln, v)) continue; double d = Math.Sqrt(Geo.D2(Q[2 * v], Q[2 * v + 1], ln.P[2 * v], ln.P[2 * v + 1])); if (d > EPS) w.Kh.Add(new CatDiem { X = Q[2 * v], Y = Q[2 * v + 1], D = d, M = ln.M }); }
                if (!Dung(ln, w.Doan, tol, td))
                {
                    kq.Loi++; double ex = cat.Count > 0 ? R.X[cat[0].Nut] : Q[2 * doi[0]], ey = cat.Count > 0 ? R.Y[cat[0].Nut] : Q[2 * doi[0] + 1];
                    double lq = 0; for (int v = 1; v < ln.N; v++) lq += Geo.Dist(Q[2 * v - 2], Q[2 * v - 1], Q[2 * v], Q[2 * v + 1]);
                    tay.Add(new CatDiem { X = ex, Y = ey, M = ln.M, D = ln.Len, Ly = lq <= 2 * td ? "Đoạn ranh rất ngắn (" + Dc.F(ln.Len * 1000, 1) + " mm): khớp đầu đường sẽ co thành một điểm – kiểm tra, xoá tay nếu là đoạn thừa" : "Đường này chưa cắt tự động được (kiểm tra không khớp) – cắt tay tại đây" }); continue;
                }
                viec.Add(w);
            }
            kq.DiemCong = Gom(tay, tol);
            if (!sua) { Dem(kq, viec, R); kq.Duong = viec.Count(w => w.Cat.Count > 0); kq.Doan = viec.Where(w => w.Cat.Count > 0).Sum(w => w.Doan.Count); kq.Khop = kq.DiemKhop.Count; return kq; }
            // ---- 7. ghi vào bản vẽ ----
            var xong = new List<Viec>();
            foreach (var w in viec)
            {
                var ln = w.Ln; double[] zs = Ms.Zs(ln.M.El);
                if (w.Cat.Count == 0)
                {
                    // chỉ dời đầu đường (không phải cắt): sửa đỉnh, giữ nguyên phần tử
                    var da = new List<int>(); bool ok = true;
                    foreach (var v in w.Doi) { if (Ms.MoveVertex(ln.M.El, v, w.Q[2 * v], w.Q[2 * v + 1])) da.Add(v); else { ok = false; break; } }
                    if (!ok) { foreach (var v in da) Ms.MoveVertex(ln.M.El, v, ln.P[2 * v], ln.P[2 * v + 1]); kq.Loi++; continue; }
                    foreach (var v in w.Doi) { ln.M.Lines[0][2 * v] = w.Q[2 * v]; ln.M.Lines[0][2 * v + 1] = w.Q[2 * v + 1]; }
                    xong.Add(w); continue;
                }
                var moi = new List<MEl>(); bool ok2 = true; long id = Ms.Id(ln.M.El);
                for (int i = 0; i < w.Doan.Count; i++)
                {
                    var d = w.Doan[i];
                    try
                    {
                        var e = Ms.ThemTheoMau(ln.M.El, d, zs == null ? null : Cao(zs, w.Tram[i])); if (e == null) { ok2 = false; break; }
                        var m = new MEl { El = e, Kind = d.Length == 4 ? 3 : 4, Lv = ln.M.Lv, Color = ln.M.Color, Weight = ln.M.Weight }; m.Lines.Add(d); moi.Add(m);
                    }
                    catch (Exception ex) { Program.Log(ex); ok2 = false; break; }
                }
                if (ok2 && !Ms.Xoa(ln.M.El)) ok2 = false;
                if (!ok2) { foreach (var m in moi) Ms.Xoa(m.El); kq.Loi++; continue; }
                kq.Duong++; kq.Doan += moi.Count; if (id != 0) kq.IdCu.Add(id); kq.Thay[ln.Src] = moi; xong.Add(w);
            }
            Dem(kq, xong, R); kq.Khop = kq.DiemKhop.Count;
            return kq;
        }
        // đếm nút cắt (mỗi nút: số đường cắt tại đó) và các đầu đường dời
        static void Dem(CatKq kq, List<Viec> ds, Nut R)
        {
            var nutCat = new Dictionary<int, int>(); var nutEl = new Dictionary<int, MEl>();
            foreach (var w in ds)
            {
                foreach (var n in w.Cat.Select(x => x.Nut).Distinct()) { int c; nutCat.TryGetValue(n, out c); nutCat[n] = c + 1; if (!nutEl.ContainsKey(n)) nutEl[n] = w.Ln.M; }
                kq.DiemKhop.AddRange(w.Kh);
            }
            kq.Nut = nutCat.Count;
            foreach (var kv in nutCat.OrderBy(k => k.Key)) kq.DiemCat.Add(new CatDiem { X = R.X[kv.Key], Y = R.Y[kv.Key], N = kv.Value, M = nutEl[kv.Key] });
        }

        // ---------- đọc một phần tử thành các đường (đường cong: chỉ để dò chỗ chạm, không cắt) ----------
        static IEnumerable<Ln> Doc(MEl m, int i)
        {
            var o = new List<Ln>(); if (m == null || m.El == null || m.Parent != null || m.Lines.Count == 0) return o;
            Func<double[], bool, bool, bool, Ln> mk = (p, kin, cat, thang) =>
            {
                if (p == null || p.Length < 4) return null;
                if (kin && (p[0] != p[p.Length - 2] || p[1] != p[p.Length - 1])) { var q = new double[p.Length + 2]; p.CopyTo(q, 0); q[p.Length] = p[0]; q[p.Length + 1] = p[1]; p = q; }
                int n = p.Length / 2; if (kin && n < 4) return null;
                var ln = new Ln { M = m, Src = i, P = p, N = n, Kin = kin, Cat = cat, Thang = thang, VN = Enumerable.Repeat(-1, n).ToArray(), Cum = new double[n] };
                for (int k = 1; k < n; k++) ln.Cum[k] = ln.Cum[k - 1] + Geo.Dist(p[2 * k - 2], p[2 * k - 1], p[2 * k], p[2 * k + 1]);
                ln.Len = ln.Cum[n - 1]; return ln;
            };
            Action<Ln> them = x => { if (x != null) o.Add(x); };
            switch (m.Kind)
            {
                case Ms.T_LINE: case Ms.T_LSTR: if (m.Lines.Count == 1) them(mk(m.Lines[0], false, true, true)); break;
                case Ms.T_SHAPE: if (m.Lines.Count == 1) them(mk(m.Lines[0], true, false, true)); break;
                case Ms.T_CSTR: case Ms.T_CSHAPE:
                    {
                        var p = Noi(m.Lines); bool kin = m.Kind == Ms.T_CSHAPE;
                        if (p != null) them(mk(p, kin, false, Ms.ChiDoanThang(m.El)));
                        else foreach (var l in m.Lines) them(mk(l, false, false, false));
                        break;
                    }
                default: foreach (var l in m.Lines) them(mk(l, m.Kind == Ms.T_ELL, false, false)); break;
            }
            return o;
        }
        // các thành phần của chuỗi phức nối liền thành một đường gấp khúc (không liền: null)
        static double[] Noi(List<double[]> ls)
        {
            var o = new List<double>();
            foreach (var l in ls)
            {
                if (l.Length < 4) continue;
                if (o.Count == 0) { o.AddRange(l); continue; }
                if (Geo.D2(o[o.Count - 2], o[o.Count - 1], l[0], l[1]) > EPS * EPS) return null;
                for (int k = 2; k < l.Length; k++) o.Add(l[k]);
            }
            return o.Count >= 4 ? o.ToArray() : null;
        }
        static bool Dau(Ln A, int v) { return !A.Kin && (v == 0 || v == A.N - 1); }
        static bool That(Ln A, int v) { return A.Thang || Dau(A, v); }      // đỉnh thật (không phải điểm chia nhỏ cung tròn)
        // đầu thật của phần tử B trong bán kính (bình phương r2) quanh điểm: chỉ số đỉnh, −1 nếu không có
        static int DauGan(Ln B, double x, double y, double r2)
        {
            if (B.Kin) return -1; double a = Geo.D2(x, y, B.P[0], B.P[1]), b = Geo.D2(x, y, B.P[2 * B.N - 2], B.P[2 * B.N - 1]);
            if (a <= r2 && a <= b) return 0; return b <= r2 ? B.N - 1 : -1;
        }
        static string LyTay(Ln B)
        {
            switch (B.M.Kind)
            {
                case Ms.T_SHAPE: case Ms.T_CSHAPE: return "Chỗ giao với vùng (shape) – cắt tay tại điểm này";
                case Ms.T_CSTR: return B.Thang ? "Chỗ giao với chuỗi phức (complex chain) – cắt tay tại điểm này" : "Chỗ giao với chuỗi phức có cung tròn – cắt tay tại điểm này";
                case Ms.T_ARC: return "Chỗ giao với cung tròn – cắt tay tại điểm này";
                case Ms.T_ELL: return "Chỗ giao với đường tròn / ellipse – cắt tay tại điểm này";
                default: return "Chỗ giao với đường cong – cắt tay tại điểm này";
            }
        }
        static long K2(int a, int b) { return (long)Math.Min(a, b) * 4000037L + Math.Max(a, b); }
        static long K2d(int a, int j) { return ((long)a << 32) | (uint)j; }
        static double Vx(Ln L, int k, Nut R) { return L.VN[k] >= 0 ? R.X[L.VN[k]] : L.P[2 * k]; }
        static double Vy(Ln L, int k, Nut R) { return L.VN[k] >= 0 ? R.Y[L.VN[k]] : L.P[2 * k + 1]; }
        static bool NamTren(Ln B, int j, double x, double y, double d2) { double t, qx, qy; return Geo.SegPt(x, y, B.P[2 * j], B.P[2 * j + 1], B.P[2 * j + 2], B.P[2 * j + 3], out t, out qx, out qy) <= d2; }
        // vị trí dọc theo đường (m) của trạm s; khoảng cách dọc đường giữa hai trạm (đường khép kín: lấy đường ngắn hơn)
        static double Pos(Ln A, double s) { int j = (int)Math.Floor(s); if (j >= A.N - 1) return A.Len; if (j < 0) return 0; return A.Cum[j] + (s - j) * (A.Cum[j + 1] - A.Cum[j]); }
        static double Doc2(Ln A, double s1, double s2) { double d = Math.Abs(Pos(A, s1) - Pos(A, s2)); return A.Kin ? Math.Min(d, A.Len - d) : d; }
        static bool KeNhau(Ln A, int i, int j) { if (Math.Abs(i - j) == 1) return true; return A.Kin && ((i == 0 && j == A.N - 2) || (j == 0 && i == A.N - 2)); }
        // đoạn đầu (v = 0) / đoạn cuối (v = N−1) có độ dài khác 0; hướng ra ngoài tại đầu đường (vectơ đơn vị)
        static int DoanDau(Ln A, int v)
        {
            var p = A.P;
            if (v == 0) { for (int j = 0; j + 1 < A.N; j++) if (p[2 * j] != p[2 * j + 2] || p[2 * j + 1] != p[2 * j + 3]) return j; }
            else for (int j = A.N - 2; j >= 0; j--) if (p[2 * j] != p[2 * j + 2] || p[2 * j + 1] != p[2 * j + 3]) return j;
            return -1;
        }
        static bool Huong(Ln A, int v, out double ex, out double ey)
        {
            ex = ey = 0; int j = DoanDau(A, v); if (j < 0) return false; var p = A.P;
            int k = v == 0 ? j + 1 : j; double dx = p[2 * v] - p[2 * k], dy = p[2 * v + 1] - p[2 * k + 1], l = Math.Sqrt(dx * dx + dy * dy);
            if (l <= 0) return false; ex = dx / l; ey = dy / l; return true;
        }

        // ---------- một cặp đoạn: vẽ chồng, đỉnh chạm đoạn (4 phía), hai đoạn cắt nhau ----------
        static void Cap(List<Ln> L, List<int> sL, List<int> sJ, int sa, int sb, double tol, double td, int S, List<Ct> cts, List<Cx> cxs, Dictionary<long, List<Ov>> ovs, HashSet<long> seen)
        {
            int a = sL[sa], ka = sJ[sa], b = sL[sb], kb = sJ[sb]; Ln A = L[a], B = L[b]; bool cung = a == b;
            if (cung && (ka == kb || KeNhau(A, ka, kb))) return;
            if (!A.Cat && !B.Cat) return;
            var P = A.P; var Q = B.P; double ax = P[2 * ka], ay = P[2 * ka + 1], bx = P[2 * ka + 2], by = P[2 * ka + 3], cx = Q[2 * kb], cy = Q[2 * kb + 1], dx = Q[2 * kb + 2], dy = Q[2 * kb + 3];
            Ov ov;
            if (A.Thang && B.Thang && Chong(ax, ay, bx, by, cx, cy, dx, dy, td, out ov)) { long k = K2(a, b); List<Ov> l; if (!ovs.TryGetValue(k, out l)) ovs[k] = l = new List<Ov>(); l.Add(ov); return; }
            Dinh(L, a, ka, b, kb, sb, tol, td, S, cts, seen); Dinh(L, a, ka + 1, b, kb, sb, tol, td, S, cts, seen);
            Dinh(L, b, kb, a, ka, sa, tol, td, S, cts, seen); Dinh(L, b, kb + 1, a, ka, sa, tol, td, S, cts, seen);
            double x, y, t, u, d2 = td * td;
            if (!Geo.SegX(ax, ay, bx, by, cx, cy, dx, dy, false, out x, out y, out t, out u)) return;
            if (t <= 1e-9 || t >= 1 - 1e-9 || u <= 1e-9 || u >= 1 - 1e-9) return;
            // sát một đỉnh thật (≤ 1 mm): theo đỉnh đó
            if ((That(A, ka) && Geo.D2(x, y, ax, ay) <= d2) || (That(A, ka + 1) && Geo.D2(x, y, bx, by) <= d2) || (That(B, kb) && Geo.D2(x, y, cx, cy) <= d2) || (That(B, kb + 1) && Geo.D2(x, y, dx, dy) <= d2)) return;
            if (cung && Doc2(A, ka + t, kb + u) <= 3 * tol) return;
            cxs.Add(new Cx { A = a, Ja = ka, B = b, Jb = kb, X = x, Y = y });
        }
        static void Dinh(List<Ln> L, int a, int v, int b, int j, int sg, double tol, double td, int S, List<Ct> cts, HashSet<long> seen)
        {
            Ln A = L[a], B = L[b]; if (A.Kin && v == A.N - 1) v = 0;
            if (!That(A, v)) return;
            double r = A.Cat && Dau(A, v) ? tol : td;                  // đầu đường: dung sai bắt nối; đỉnh giữa, đỉnh vùng…: 1 mm
            if (a == b) { int j1 = B.Kin && j + 1 == B.N - 1 ? 0 : j + 1; if (v == j || v == j1) return; }
            double t, qx, qy; double d = Geo.SegPt(A.P[2 * v], A.P[2 * v + 1], B.P[2 * j], B.P[2 * j + 1], B.P[2 * j + 2], B.P[2 * j + 3], out t, out qx, out qy);
            if (d > r * r) return;
            if (a == b && Doc2(A, v, j + t) <= 3 * tol) return;
            if (!seen.Add((long)(A.VB + v) * S + sg)) return;
            cts.Add(new Ct { A = a, V = v, B = b, J = j, U = t, D2 = d, Qx = qx, Qy = qy });
        }
        // hai đoạn thẳng hàng (lệch ≤ td) chồng lên nhau một khúc dài hơn td: phần chồng (trên đoạn thứ hai)
        static bool Chong(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy, double td, out Ov ov)
        {
            ov = null; double lb = Geo.Dist(cx, cy, dx, dy), la = Geo.Dist(ax, ay, bx, by); if (lb < EPS || la < EPS) return false;
            double ux = (dx - cx) / lb, uy = (dy - cy) / lb, vx = (bx - ax) / la, vy = (by - ay) / la;
            bool t1 = Math.Abs((ax - cx) * uy - (ay - cy) * ux) <= td && Math.Abs((bx - cx) * uy - (by - cy) * ux) <= td;
            bool t2 = Math.Abs((cx - ax) * vy - (cy - ay) * vx) <= td && Math.Abs((dx - ax) * vy - (dy - ay) * vx) <= td;
            if (!t1 && !t2) return false;
            double ta = (ax - cx) * ux + (ay - cy) * uy, tb = (bx - cx) * ux + (by - cy) * uy;
            double lo = Math.Max(0, Math.Min(ta, tb)), hi = Math.Min(lb, Math.Max(ta, tb));
            if (hi - lo <= td) return false;
            ov = new Ov { X0 = cx + ux * lo, Y0 = cy + uy * lo, X1 = cx + ux * hi, Y1 = cy + uy * hi }; return true;
        }

        // ---------- trạm cắt trên đoạn j của đường L tại nút n: trùng đỉnh (≤ 1 mm) thì cắt tại đỉnh, không thì giữa đoạn ----------
        static void Tram(Ln L, int j, int n, Nut R, double d2)
        {
            if (!L.Cat) return;
            double x = R.X[n], y = R.Y[n]; int k0 = j, k1 = j + 1;
            if (Geo.D2(x, y, Vx(L, k1, R), Vy(L, k1, R)) < Geo.D2(x, y, Vx(L, k0, R), Vy(L, k0, R))) { k0 = j + 1; k1 = j; }
            foreach (int k in new[] { k0, k1 })
            {
                if (Geo.D2(x, y, Vx(L, k, R), Vy(L, k, R)) > d2 || (L.VN[k] >= 0 && L.VN[k] != n)) continue;
                L.VN[k] = n; if (!Dau(L, k)) L.Su.Add(new Su { S = k, Nut = n }); return;
            }
            double t, qx, qy; Geo.SegPt(x, y, L.P[2 * j], L.P[2 * j + 1], L.P[2 * j + 2], L.P[2 * j + 3], out t, out qx, out qy);
            L.Su.Add(new Su { S = j + Math.Max(1e-9, Math.Min(1 - 1e-9, t)), Nut = n });
        }

        // ---------- điểm nối của đầu đường v (đường A): nút; −1 nếu không nối được ----------
        static int Chon(List<Ln> L, int a, int v, List<Ct> cs, Nut R, Dictionary<long, List<Cx>> xDoan, double tol, double td, out bool songSong)
        {
            var A = L[a]; double px = A.P[2 * v], py = A.P[2 * v + 1], t2 = tol * tol, d2 = td * td; songSong = false;
            // (1) đã có nút trong 1 mm
            int n = R.Find(px, py); if (n >= 0) return n;
            // (2) đầu đường nằm đúng trên đường khác: vào đỉnh của đường đó nếu trong 1 mm, không thì giữ nguyên điểm đầu
            bool tren = false, co = false; double bd = d2, bx = 0, by = 0;
            foreach (var c in cs)
            {
                if (c.D2 > d2) continue; var B = L[c.B]; if (B.Cat) tren = true;
                for (int q = 0; q < 2; q++)
                {
                    int k = c.J + q; if (B.Kin && k == B.N - 1) k = 0; if (!That(B, k) || (!B.Cat && !Dau(B, k))) continue;
                    double x = Vx(B, k, R), y = Vy(B, k, R), d = Geo.D2(x, y, px, py); if (d <= bd) { bd = d; bx = x; by = y; co = true; }
                }
            }
            if (co) return R.Get(bx, by);
            // nằm đúng trên đường (lệch ≤ 1 µm – chữ T vẽ chính xác): chân trên đường đi qua; lệch 1 µm … 1 mm: xét tiếp, nút / đỉnh có sẵn gần đó được ưu tiên
            if (tren) { Ct m = null; foreach (var c in cs) if (c.D2 <= d2 && L[c.B].Cat && (m == null || c.D2 < m.D2)) m = c; if (m.D2 <= EPS * EPS) return R.Get(m.Qx, m.Qy); }
            // (3) đầu thừa: giao điểm trên chính đoạn đầu / đoạn cuối của đường, gần đầu nhất (bỏ phần thừa, giữ hướng đường)
            int jd = DoanDau(A, v); List<Cx> xl;
            if (jd >= 0 && xDoan.TryGetValue(K2d(a, jd), out xl))
            {
                int bn = -1; double bdd = t2;
                foreach (var x in xl) { if (x.Nut < 0) continue; double d = Geo.D2(R.X[x.Nut], R.Y[x.Nut], px, py); if (d <= bdd) { bdd = d; bn = x.Nut; } }
                if (bn >= 0) return bn;
            }
            // (4) trong dung sai, theo thứ tự ưu tiên: nút có sẵn trên đường chạm (chỗ giao, điểm nối đã có – không tạo khúc ranh vụn), đỉnh cố định
            //     của đường chạm, điểm kéo dài theo hướng của chính đường này (hoặc chân vuông góc khi đầu đường hướng vào đường kia), cuối cùng là
            //     đầu của đường khác chưa nối (đầu đó còn có thể dời về đúng chỗ giao)
            var ung = new List<double[]>(); double ex, ey; bool huong = Huong(A, v, out ex, out ey);
            foreach (var c in cs)
            {
                var B = L[c.B];
                for (int q = 0; q < 2; q++)
                {
                    int k = c.J + q; if (B.Kin && k == B.N - 1) k = 0; if (!That(B, k) || (!B.Cat && !Dau(B, k))) continue;
                    double x = Vx(B, k, R), y = Vy(B, k, R), d = Geo.D2(x, y, px, py);
                    if (d <= t2) ung.Add(new[] { x, y, Math.Sqrt(d), B.Cat && Dau(B, k) && B.VN[k] < 0 ? 3 : 1, -1 });     // đầu đường khác chưa nối (còn dời được): ưu tiên sau cùng
                }
                if (!B.Cat) continue;
                foreach (int m in R.Gan(px, py, tol)) if (NamTren(B, c.J, R.X[m], R.Y[m], d2)) ung.Add(new[] { R.X[m], R.Y[m], Math.Sqrt(Geo.D2(R.X[m], R.Y[m], px, py)), 0, m });
                if (c.D2 <= d2) ung.Add(new[] { c.Qx, c.Qy, Math.Sqrt(c.D2), 2, -1 });                    // đầu đường sát đường kia (≤ 1 mm): chân vuông góc
                if (!huong) continue;
                double xx, yy, t, u; var Q = B.P; int j = c.J;
                if (Geo.SegX(px, py, px + ex * 3 * tol, py + ey * 3 * tol, Q[2 * j], Q[2 * j + 1], Q[2 * j + 2], Q[2 * j + 3], false, out xx, out yy, out t, out u))
                {
                    double s = t * 3 * tol;
                    if (s <= tol) ung.Add(new[] { xx, yy, s, 2, -1 }); else ung.Add(new[] { c.Qx, c.Qy, Math.Sqrt(c.D2), 2, -1 });
                }
            }
            if (ung.Count == 0) { songSong = cs.Any(c => L[c.B].Cat); return -1; }
            var ch = ung.OrderBy(o => o[3]).ThenBy(o => o[2]).First();
            return ch[4] >= 0 ? (int)ch[4] : R.Get(ch[0], ch[1]);
        }

        // ---------- các điểm cắt của một đường: sắp theo trạm, bỏ điểm tại đầu đường, gộp các điểm cùng nút sát nhau ----------
        static List<Su> Gon(Ln ln, double tol)
        {
            int M = ln.N - 1; var o = new List<Su>();
            foreach (var x in ln.Su.OrderBy(s => s.S).ThenBy(s => s.S == Math.Floor(s.S) ? 0 : 1))
            {
                if (x.S <= 1e-12 || x.S >= M - 1e-12) continue;
                if (ln.VN[0] == x.Nut && Pos(ln, x.S) <= 2 * tol) continue;                // trùng nút ở đầu đường (đầu thừa đã đưa về giao điểm)
                if (ln.VN[M] == x.Nut && ln.Len - Pos(ln, x.S) <= 2 * tol) continue;
                if (o.Count > 0)
                {
                    var p = o[o.Count - 1];
                    if (p.Nut == x.Nut && Pos(ln, x.S) - Pos(ln, p.S) <= 2 * tol) { if (x.S == Math.Floor(x.S) && p.S != Math.Floor(p.S)) o[o.Count - 1] = x; continue; }
                }
                o.Add(x);
            }
            return o;
        }
        // các đoạn mới của một đường (Q: toạ độ đỉnh, đỉnh trùng nút lấy toạ độ nút; cat: điểm cắt đã sắp) – kèm trạm của từng điểm (để lấy cao độ)
        static void Doan(Ln ln, double[] Q, List<Su> cat, Nut R, List<double[]> pcs, List<double[]> sts)
        {
            int N = ln.N; var cur = new List<double>(); var cs = new List<double>(); bool catTruoc = false;
            // điểm trùng điểm liền trước: chỉ bỏ khi một trong hai là điểm cắt (đỉnh lặp sẵn có của đường giữ nguyên)
            Action<double, double, double, bool> them = (x, y, s, laCat) =>
            {
                int c = cur.Count;
                if (c >= 2 && cur[c - 2] == x && cur[c - 1] == y && (laCat || catTruoc)) { if (laCat) cs[cs.Count - 1] = s; catTruoc = true; return; }
                cur.Add(x); cur.Add(y); cs.Add(s); catTruoc = laCat;
            };
            Action xong = () => { pcs.Add(cur.ToArray()); sts.Add(cs.ToArray()); cur = new List<double>(); cs = new List<double>(); catTruoc = false; };
            them(Q[0], Q[1], 0, false); double s0 = 0;
            foreach (var x in cat)
            {
                for (int v = (int)Math.Floor(s0) + 1; v <= (int)Math.Ceiling(x.S) - 1; v++) them(Q[2 * v], Q[2 * v + 1], v, false);
                double nx = R.X[x.Nut], ny = R.Y[x.Nut]; them(nx, ny, x.S, true); xong(); them(nx, ny, x.S, true); s0 = x.S;
            }
            for (int v = (int)Math.Floor(s0) + 1; v <= N - 1; v++) them(Q[2 * v], Q[2 * v + 1], v, false);
            xong();
        }
        // kiểm tra trước khi ghi: các đoạn nối liền nhau, đi qua đúng mọi đỉnh cũ theo thứ tự (đỉnh giữa lệch ≤ 1 mm, đầu đường ≤ dung sai),
        // điểm cắt thêm vào nằm trên đúng đoạn cũ, tổng chiều dài không đổi (trừ phần đầu đường dời)
        static bool Dung(Ln ln, List<double[]> pcs, double tol, double td)
        {
            if (pcs.Count == 0) return false; double d2 = td * td * 1.0001 + 1e-18, t2 = tol * tol * 1.0001 + 1e-18, t, qx, qy;
            var seq = new List<double>();
            for (int i = 0; i < pcs.Count; i++)
            {
                var d = pcs[i]; if (d.Length < 4 || d.Length % 2 != 0) return false;
                double len = 0; for (int k = 2; k < d.Length; k += 2) len += Geo.Dist(d[k - 2], d[k - 1], d[k], d[k + 1]); if (len <= EPS) return false;
                if (i == 0) seq.AddRange(d);
                else { if (d[0] != seq[seq.Count - 2] || d[1] != seq[seq.Count - 1]) return false; for (int k = 2; k < d.Length; k++) seq.Add(d[k]); }
            }
            var S = Khu(seq); var P = Khu(new List<double>(ln.P)); int m = S.Count / 2, N = P.Count / 2; if (N < 2 || m < 2) return false;
            if (Geo.D2(S[0], S[1], P[0], P[1]) > t2 || Geo.D2(S[2 * m - 2], S[2 * m - 1], P[2 * N - 2], P[2 * N - 1]) > t2) return false;
            int k2 = 1;
            for (int v = 1; v < N - 1; v++)
            {
                while (k2 < m - 1 && Geo.D2(S[2 * k2], S[2 * k2 + 1], P[2 * v], P[2 * v + 1]) > d2)
                {
                    if (Geo.SegPt(S[2 * k2], S[2 * k2 + 1], P[2 * v - 2], P[2 * v - 1], P[2 * v], P[2 * v + 1], out t, out qx, out qy) > d2) return false;
                    k2++;
                }
                if (k2 >= m - 1) return false;
                k2++;
            }
            for (; k2 < m - 1; k2++) if (Geo.SegPt(S[2 * k2], S[2 * k2 + 1], P[2 * N - 4], P[2 * N - 3], P[2 * N - 2], P[2 * N - 1], out t, out qx, out qy) > d2) return false;
            double l0 = 0, l1 = 0; for (int i = 2; i < 2 * N; i += 2) l0 += Geo.Dist(P[i - 2], P[i - 1], P[i], P[i + 1]); for (int i = 2; i < 2 * m; i += 2) l1 += Geo.Dist(S[i - 2], S[i - 1], S[i], S[i + 1]);
            double cho = Geo.Dist(S[0], S[1], P[0], P[1]) + Geo.Dist(S[2 * m - 2], S[2 * m - 1], P[2 * N - 2], P[2 * N - 1]) + (Math.Max(0, m - N) + 2) * 2 * td + 1e-6;
            return Math.Abs(l1 - l0) <= cho;
        }
        static List<double> Khu(List<double> p) { var o = new List<double>(); for (int k = 0; k + 1 < p.Count; k += 2) { int c = o.Count; if (c >= 2 && o[c - 2] == p[k] && o[c - 1] == p[k + 1]) continue; o.Add(p[k]); o.Add(p[k + 1]); } return o; }
        // cao độ của từng điểm đoạn mới theo trạm trên đường cũ (đỉnh cũ: đúng cao độ của đỉnh; điểm cắt: nội suy trên đoạn)
        static double[] Cao(double[] zs, double[] st)
        {
            var o = new double[st.Length];
            for (int i = 0; i < st.Length; i++) { double s = st[i]; int j = (int)Math.Floor(s); if (j >= zs.Length - 1) { o[i] = zs[zs.Length - 1]; continue; } if (j < 0) j = 0; double t = s - j; o[i] = zs[j] + t * (zs[j + 1] - zs[j]); }
            return o;
        }
        static List<CatDiem> Gom(List<CatDiem> ds, double tol)
        {
            var o = new List<CatDiem>(); var g = new HashSet<string>(); double c = Math.Max(tol * 4, 0.01);
            foreach (var d in ds) { var k = Math.Floor(d.X / c).ToString(IC) + "," + Math.Floor(d.Y / c).ToString(IC); if (g.Add(k)) o.Add(d); }
            return o;
        }

        // ---------- sổ nút: mỗi nút một toạ độ; điểm mới cách nút có sẵn không quá 1 mm thì vào nút đó ----------
        class Nut
        {
            public List<double> X = new List<double>(), Y = new List<double>(); readonly double c, r2; readonly Dictionary<long, List<int>> g = new Dictionary<long, List<int>>();
            public Nut(double tol, double td) { c = Math.Max(tol, td); r2 = td * td; }
            static long K(long i, long j) { return i * 2000003L + j; }
            // các nút trong bán kính r (r không quá cạnh ô)
            public IEnumerable<int> Gan(double x, double y, double r)
            {
                long gi = (long)Math.Floor(x / c), gj = (long)Math.Floor(y / c); double rr = r * r; var o = new List<int>();
                for (long di = -1; di <= 1; di++) for (long dj = -1; dj <= 1; dj++) { List<int> C; if (!g.TryGetValue(K(gi + di, gj + dj), out C)) continue; foreach (int n in C) if (Geo.D2(X[n], Y[n], x, y) <= rr) o.Add(n); }
                return o;
            }
            public int Find(double x, double y) { int best = -1; double bd = r2; foreach (int n in Gan(x, y, Math.Sqrt(r2))) { double d = Geo.D2(X[n], Y[n], x, y); if (d <= bd) { bd = d; best = n; } } return best; }
            public int Get(double x, double y)
            {
                int n = Find(x, y); if (n >= 0) return n;
                n = X.Count; X.Add(x); Y.Add(y); long k = K((long)Math.Floor(x / c), (long)Math.Floor(y / c)); List<int> L; if (!g.TryGetValue(k, out L)) g[k] = L = new List<int>(); L.Add(n); return n;
            }
        }
    }
}
