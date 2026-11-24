// M8 Pro Địa chính – cầu nối với MicroStation V8i qua COM. Mọi lời gọi MicroStation đều nằm trong tệp này. © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using BCOM = Bentley.Interop.MicroStationDGN;

namespace M8Pro
{
    // phần tử đọc từ bản vẽ (toạ độ theo đơn vị chính – mét)
    public class MEl
    {
        public BCOM.Element El; public int Kind; public int Lv; public int Color; public int Weight;
        public BCOM.Element Parent;                           // ô (cell) chứa phần tử này, nếu là thành phần của ô
        public List<double[]> Lines = new List<double[]>();   // các đường gấp khúc
        public string Text; public double Cx, Cy, H;          // chữ: tâm khung chữ, cao
        public int Idx;
    }

    public static class Ms
    {
        public static BCOM.Application App;
        public const int T_CELL = 2, T_LINE = 3, T_LSTR = 4, T_SHAPE = 6, T_TNODE = 7, T_CSTR = 12, T_CSHAPE = 14, T_ELL = 15, T_ARC = 16, T_TEXT = 17;
        static int fails;
        // MicroStation đang mở / đóng tệp thì tạm chưa có bản vẽ: KHÔNG bỏ kết nối (chạy trong MicroStation luôn dùng lại đối tượng ứng dụng)
        public static bool Connected
        {
            get
            {
#if ADDIN
                if (App == null) { try { App = Bentley.MicroStation.InteropServices.Utilities.ComApp; } catch { } }
#endif
                if (App == null) return false;
                try { var n = App.ActiveDesignFile.Name; fails = 0; return n != null; }
                catch
                {
#if !ADDIN
                    if (++fails >= 4) App = null;   // chạy ngoài MicroStation: chỉ coi là mất kết nối khi lỗi nhiều lần liền
#endif
                    return false;
                }
            }
        }
        public static string Connect()
        {
#if ADDIN
            try { App = Bentley.MicroStation.InteropServices.Utilities.ComApp; if (App != null) return null; } catch { }
#endif
            try { App = (BCOM.Application)Marshal.GetActiveObject("MicroStationDGN.Application"); return null; }
            catch (Exception e) { App = null; return "Chưa thấy MicroStation V8i đang chạy (" + e.Message + ")."; }
        }
        // ngắt kết nối: bỏ tham chiếu tới MicroStation (MicroStation vẫn chạy bình thường)
        public static void Disconnect()
        {
            try { if (cur != null) App.CommandState.StartDefaultCommand(); } catch { } cur = null; lvCache.Clear();
#if !ADDIN
            try { if (App != null) Marshal.ReleaseComObject(App); } catch { }
#endif
            App = null;
        }
        public static string FindUstation()
        {
            var roots = new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramFiles"), @"C:\Program Files (x86)", @"C:\Program Files" };
            foreach (var r in roots)
            {
                if (string.IsNullOrEmpty(r)) continue; var b = Path.Combine(r, "Bentley"); if (!Directory.Exists(b)) continue;
                try { foreach (var f in Directory.GetFiles(b, "ustation.exe", SearchOption.AllDirectories)) return f; } catch { }
            }
            return null;
        }
        public static string FileName { get { try { return App.ActiveDesignFile.Name; } catch { return ""; } } }
        public static string FullName { get { try { return App.ActiveDesignFile.FullName; } catch { return ""; } } }
        public static void Prompt(string s) { try { App.ShowPrompt(s); } catch { } }
        public static void Status(string s) { try { App.ShowStatus(s); } catch { } }

        static readonly Dictionary<int, BCOM.Level> lvCache = new Dictionary<int, BCOM.Level>();
        public static BCOM.Level Level(int n)
        {
            BCOM.Level l; if (lvCache.TryGetValue(n, out l)) { try { var x = l.Number; return l; } catch { lvCache.Clear(); } }
            BCOM.Level byName = null;
            foreach (BCOM.Level lv in App.ActiveDesignFile.Levels)
            {
                try { if (lv.Number == n) { lvCache[n] = lv; return lv; } } catch { }
                try { var nm = lv.Name; if (byName == null && (nm == n.ToString() || nm == "Level " + n || nm == "Lớp " + n)) byName = lv; } catch { }
            }
            if (byName != null) lvCache[n] = byName;
            return byName;
        }
        public static void ResetCache() { lvCache.Clear(); }
        public static BCOM.Point3d P(double x, double y) { return App.Point3dFromXY(x, y); }
        public static int LvNum(BCOM.Element e) { return LvNumL(e.Level); }
        public static int LvNumL(BCOM.Level L)
        {
            if (L == null) return -1;
            try { return L.Number; } catch { }
            try { var m = System.Text.RegularExpressions.Regex.Match(L.Name, @"\d+"); if (m.Success) return int.Parse(m.Value); } catch { }
            return -1;
        }
        // thông tin chẩn đoán lần đọc gần nhất
        public static string LastRead = ""; public static int FillErr; public static string FillMsg = "";
        // gọi hàm tạo phần tử theo chữ ký thật của thư viện trên máy (tham số mẫu = null, điểm/mảng điểm, tham số tuỳ chọn để mặc định)
        static object CallApp(string name, BCOM.Point3d[] pts) { return CallApp(name, pts, null); }
        // mau: phần tử mẫu – phần tử mới nhận kiểu của mẫu (lớp, màu, nét, kiểu đường, nhóm…)
        static object CallApp(string name, BCOM.Point3d[] pts, BCOM.Element mau)
        {
            var m = typeof(BCOM.Application).GetMethod(name); if (m == null) throw new Exception("Thư viện không có hàm " + name);
            var ps = m.GetParameters(); var args = new object[ps.Length]; int k = 0;
            for (int i = 0; i < ps.Length; i++)
            {
                var t = ps[i].ParameterType; if (t.IsByRef) t = t.GetElementType();
                if (t.IsArray || t == typeof(Array)) args[i] = pts;
                else if (t == typeof(BCOM.Point3d)) args[i] = k < pts.Length ? pts[k++] : pts[pts.Length - 1];
                else if (t == typeof(object) && ps[i].Name.ToLower().Contains("vert")) args[i] = pts;
                else if (mau != null && t == typeof(BCOM.Element)) args[i] = mau;
                else if (!t.IsValueType) args[i] = ps[i].IsOptional && !(ps[i].DefaultValue is DBNull) ? ps[i].DefaultValue : null;
                else args[i] = ps[i].IsOptional && !(ps[i].DefaultValue is DBNull) && ps[i].DefaultValue != null ? ps[i].DefaultValue : Activator.CreateInstance(t);
            }
            return m.Invoke(App, args);
        }

        // ---------- ĐỌC ----------
        // đọc phần tử theo lớp; types: các kiểu phần tử cần lấy
        // dùng để kiểm thử ngoài MicroStation
        public static Func<ICollection<int>, ICollection<int>, List<MEl>> Reader;
        // levels = null: mọi lớp
        public static List<MEl> Read(ICollection<int> levels, ICollection<int> types) { return Read(levels, types, false); }
        // cells = true: lấy cả chữ, đường nằm trong ô (cell) – nhãn thửa dạng ô N_THUA
        public static List<MEl> Read(ICollection<int> levels, ICollection<int> types, bool cells)
        {
            if (Reader != null) { var r0 = Reader(levels, types); if (cells && CellReader != null) r0.AddRange(CellReader(levels, types)); return r0; }
            var outp = new List<MEl>(); BCOM.ElementEnumerator en = null; string how = "lọc theo level";
            FillErr = 0; FillMsg = ""; HashSet<int> allow = null;
            try
            {
                var sc = (BCOM.ElementScanCriteria)App.CreateObjectInMicroStation("MicroStationDGN.ElementScanCriteria");
                sc.ExcludeNonGraphical(); sc.ExcludeAllTypes(); foreach (int t in types) sc.IncludeType((BCOM.MsdElementType)t);
                if (levels != null)
                {
                    sc.ExcludeAllLevels(); allow = new HashSet<int>();
                    foreach (int l in levels) { var L = Level(l); if (L != null) { sc.IncludeLevel(L); allow.Add(LvNumL(L)); } }
                    if (allow.Count == 0) { LastRead = "Không có level " + string.Join(",", levels.Select(x => x.ToString()).ToArray()) + " trong tệp bản đồ"; return outp; }
                }
                en = App.ActiveModelReference.Scan(sc);
            }
            catch (Exception ex)
            {
                how = "quét toàn bộ (" + ex.Message + ")"; en = App.ActiveModelReference.Scan();
                if (levels != null && allow == null) { allow = new HashSet<int>(); foreach (int l in levels) { var L = Level(l); allow.Add(L != null ? LvNumL(L) : l); } }
            }
            int seen = 0;
            while (en.MoveNext())
            {
                var e = en.Current; int t, lv; seen++;
                try { t = (int)e.Type; if (!types.Contains(t)) continue; if (!e.IsGraphical) continue; lv = LvNum(e); if (allow != null && !allow.Contains(lv)) continue; } catch { continue; }
                var m = new MEl { El = e, Kind = t, Lv = lv, Idx = outp.Count };
                try { m.Color = e.Color; m.Weight = e.LineWeight; } catch { }
                try { Fill(m, e, t); } catch (Exception ex) { FillErr++; if (FillMsg == "") FillMsg = "kiểu " + t + ": " + ex.Message; }
                outp.Add(m);
            }
            LastRead = string.Format("Cách đọc: {0}; đã duyệt {1} phần tử, lấy {2}; lỗi đọc hình {3}{4}", how, seen, outp.Count, FillErr, FillMsg != "" ? " (" + FillMsg + ")" : "");
            if (cells) try { ReadCells(levels == null ? null : allow ?? new HashSet<int>(levels), types, outp); } catch (Exception ex) { LastRead += "; ô: " + ex.Message; }
            return outp;
        }
        public static Func<ICollection<int>, ICollection<int>, List<MEl>> CellReader;   // kiểm thử
        // các ô (cell) trên mọi lớp: lấy thành phần thuộc lớp cần đọc (lớp của ô không quan trọng)
        static void ReadCells(HashSet<int> allow, ICollection<int> types, List<MEl> outp)
        {
            BCOM.ElementEnumerator en;
            try { var sc = (BCOM.ElementScanCriteria)App.CreateObjectInMicroStation("MicroStationDGN.ElementScanCriteria"); sc.ExcludeNonGraphical(); sc.ExcludeAllTypes(); sc.IncludeType((BCOM.MsdElementType)T_CELL); en = App.ActiveModelReference.Scan(sc); }
            catch { en = App.ActiveModelReference.Scan(); }
            int n = 0;
            while (en.MoveNext() && n++ < 200000)
            {
                var c = en.Current; try { if ((int)c.Type != T_CELL || !c.IsGraphical) continue; } catch { continue; }
                try { Comps(c, c, allow, types, outp, 0); } catch { }
            }
        }
        static void Comps(BCOM.Element cell, BCOM.Element e, HashSet<int> allow, ICollection<int> types, List<MEl> outp, int depth)
        {
            dynamic se = ((dynamic)e).AsComplexElement.GetSubElements();
            while ((bool)se.MoveNext())
            {
                BCOM.Element s = se.Current; int t, lv; try { t = (int)s.Type; lv = LvNum(s); } catch { continue; }
                if (t == T_CELL) { if (depth < 3) Comps(cell, s, allow, types, outp, depth + 1); continue; }
                if (!types.Contains(t) || (allow != null && !allow.Contains(lv))) continue;
                var m = new MEl { El = s, Parent = cell, Kind = t, Lv = lv, Idx = outp.Count };
                try { m.Color = s.Color; m.Weight = s.LineWeight; } catch { }
                try { Fill(m, s, t); } catch { }
                outp.Add(m);
            }
        }
        // mã phần tử (Element ID) trong tệp DGN
        public static long Id(BCOM.Element e)
        {
            if (e == null) return 0; try { dynamic d = ((dynamic)e).ID; return ((long)(int)d.High << 32) | (uint)(int)d.Low; } catch { return 0; }
        }
        // tìm nhanh các chữ (mọi lớp) có nội dung thoả điều kiện – chỉ lấy vị trí của chữ khớp
        public static List<MEl> FindTexts(Func<string, bool> ok)
        {
            if (Reader != null) return Reader(null, new[] { T_TEXT, T_TNODE }).Where(m => ok(m.Text ?? "")).ToList();
            var o = new List<MEl>(); BCOM.ElementEnumerator en;
            try { var sc = (BCOM.ElementScanCriteria)App.CreateObjectInMicroStation("MicroStationDGN.ElementScanCriteria"); sc.ExcludeNonGraphical(); sc.ExcludeAllTypes(); sc.IncludeType((BCOM.MsdElementType)T_TEXT); sc.IncludeType((BCOM.MsdElementType)T_TNODE); en = App.ActiveModelReference.Scan(sc); }
            catch { en = App.ActiveModelReference.Scan(); }
            int n = 0;
            while (en.MoveNext() && n++ < 400000)
            {
                var e = en.Current; try
                {
                    int t = (int)e.Type; if (t != T_TEXT && t != T_TNODE) continue; string s;
                    if (t == T_TEXT) { var te = e as BCOM.TextElement; s = null; if (te != null) try { s = te.Text; } catch { s = null; } if (s == null) s = (string)((dynamic)e).AsTextElement.Text; }
                    else { var parts = new List<string>(); dynamic se = ((dynamic)e).AsComplexElement.GetSubElements(); while ((bool)se.MoveNext()) { try { parts.Add((string)((dynamic)se.Current).AsTextElement.Text); } catch { } } s = string.Join(" ", parts.ToArray()); }
                    if (!ok(s ?? "")) continue; var R = Range(e); o.Add(new MEl { El = e, Kind = t, Text = s, Cx = (R[0] + R[2]) / 2, Cy = (R[1] + R[3]) / 2, H = R[3] - R[1] });
                }
                catch { }
            }
            return o;
        }
        // thống kê toàn bộ phần tử theo lớp và kiểu (chẩn đoán)
        public static Dictionary<string, int> Census()
        {
            var d = new Dictionary<string, int>(); var en = App.ActiveModelReference.Scan(); int n = 0;
            while (en.MoveNext() && n < 300000)
            {
                n++; var e = en.Current; string k;
                try { if (!e.IsGraphical) continue; k = "Level " + LvNum(e) + " · kiểu " + (int)e.Type; } catch { k = "không đọc được"; }
                int c; d.TryGetValue(k, out c); d[k] = c + 1;
            }
            return d;
        }
        // các thành phần dưới đây gọi qua IDispatch (dynamic) để chạy được với mọi bản V8i
        static double[] Pts(dynamic arr)
        {
            var o = new List<double>(); foreach (dynamic v in arr) { o.Add((double)v.X); o.Add((double)v.Y); } return o.ToArray();
        }
        static void Fill(MEl m, BCOM.Element e, int t)
        {
            dynamic d = e;
            if (t == T_LINE || t == T_LSTR || t == T_SHAPE)
            {
                var a = Pts(d.AsVertexList.GetVertices()); int n = a.Length;
                if (t == T_SHAPE && n > 2 && (a[0] != a[n - 2] || a[1] != a[n - 1])) { var b = new double[n + 2]; a.CopyTo(b, 0); b[n] = a[0]; b[n + 1] = a[1]; a = b; }
                m.Lines.Add(a);
            }
            else if (t == T_CSTR || t == T_CSHAPE)
            {
                dynamic en = d.AsComplexElement.GetSubElements();
                while ((bool)en.MoveNext()) { BCOM.Element s = en.Current; var sub = new MEl(); try { Fill(sub, s, (int)s.Type); } catch { } m.Lines.AddRange(sub.Lines); }
            }
            else if (t == T_ARC)
            {
                dynamic a = d.AsArcElement; double cx = a.CenterPoint.X, cy = a.CenterPoint.Y, r = a.PrimaryRadius, sw = a.SweepAngle;
                double a0 = Math.Atan2((double)a.StartPoint.Y - cy, (double)a.StartPoint.X - cx); int n = Math.Max(6, (int)Math.Ceiling(Math.Abs(sw) / (Math.PI / 36)));
                var o = new double[(n + 1) * 2]; for (int i = 0; i <= n; i++) { double g = a0 + sw * i / n; o[2 * i] = cx + r * Math.Cos(g); o[2 * i + 1] = cy + r * Math.Sin(g); }
                m.Lines.Add(o);
            }
            else if (t == T_ELL)
            {
                dynamic a = d.AsEllipseElement; double cx = a.CenterPoint.X, cy = a.CenterPoint.Y, r = a.PrimaryRadius; int n = 72;
                var o = new double[(n + 1) * 2]; for (int i = 0; i <= n; i++) { double g = 2 * Math.PI * i / n; o[2 * i] = cx + r * Math.Cos(g); o[2 * i + 1] = cy + r * Math.Sin(g); }
                m.Lines.Add(o);
            }
            else if (t == T_TEXT || t == T_TNODE)
            {
                if (t == T_TEXT) m.Text = (string)d.AsTextElement.Text;
                else { var parts = new List<string>(); dynamic en = d.AsComplexElement.GetSubElements(); while ((bool)en.MoveNext()) { try { parts.Add((string)((dynamic)en.Current).AsTextElement.Text); } catch { } } m.Text = string.Join(" ", parts.ToArray()); }
                double[] R = Range(e); m.Cx = (R[0] + R[2]) / 2; m.Cy = (R[1] + R[3]) / 2; m.H = R[3] - R[1];
            }
        }
        // các đường gấp khúc của phần tử (đường thẳng, đường gấp khúc; chuỗi phức nối liền thành một đường) – toạ độ mét
        public static List<double[]> LinesOf(BCOM.Element e)
        {
            var m = new MEl { El = e }; int t; try { t = (int)e.Type; Fill(m, e, t); } catch { return m.Lines; }
            if (t != T_CSTR || m.Lines.Count < 2) return m.Lines;
            var o = new List<double>(m.Lines[0]);
            for (int i = 1; i < m.Lines.Count; i++)
            {
                var a = m.Lines[i]; if (a.Length < 2) continue; int s0 = Math.Abs(a[0] - o[o.Count - 2]) < 1e-6 && Math.Abs(a[1] - o[o.Count - 1]) < 1e-6 ? 2 : 0;
                for (int k = s0; k < a.Length; k++) o.Add(a[k]);
            }
            return new List<double[]> { o.ToArray() };
        }
        public static double[] Range(BCOM.Element e) { dynamic R = ((dynamic)e).Range; return new double[] { (double)R.Low.X, (double)R.Low.Y, (double)R.High.X, (double)R.High.Y }; }

        // ---------- GHI ----------
        // lớp chưa có trong bản vẽ thì tạo lớp (tên chuẩn "Level N") rồi gán – không để phần tử rơi vào lớp đang dùng
        static void Sym(BCOM.Element e, int lv, int color, int weight)
        {
            BCOM.Level L = null; try { L = lv > 0 ? EnsureLevel(lv) : Level(lv); } catch (Exception ex) { Program.Log(ex); }
            if (L != null) e.Level = L; if (color >= 0) e.Color = color; if (weight >= 0) e.LineWeight = weight;
        }
        // đường chưa đưa vào bản vẽ (để gom vào ô)
        public static BCOM.Element MakeLine(double[] pts, int lv, int color, int weight)
        {
            var v = new BCOM.Point3d[pts.Length / 2]; for (int i = 0; i < v.Length; i++) v[i] = P(pts[2 * i], pts[2 * i + 1]);
            BCOM.Element e = null;
            try { e = (BCOM.Element)CallApp(v.Length == 2 ? "CreateLineElement1" : "CreateLineElement2", v); } catch { e = null; }
            if (e == null) e = (BCOM.Element)CallApp("CreateLineElement2", v);
            Sym(e, lv, color, weight); return e;
        }
        // ô (cell) từ các phần tử chưa đưa vào bản vẽ – nhãn thửa dạng ô N_THUA; không tạo được ô thì đưa từng phần tử vào bản vẽ
        public static BCOM.Element AddCell(string name, List<BCOM.Element> els, double ox, double oy)
        {
            try
            {
                var m = typeof(BCOM.Application).GetMethod("CreateCellElement1");
                if (m != null)
                {
                    var ps = m.GetParameters(); var args = new object[ps.Length]; var o = P(ox, oy);
                    for (int i = 0; i < ps.Length; i++)
                    {
                        var t = ps[i].ParameterType; if (t.IsByRef) t = t.GetElementType();
                        if (t == typeof(string)) args[i] = name;
                        else if (t.IsArray) { var arr = Array.CreateInstance(t.GetElementType(), els.Count); for (int k = 0; k < els.Count; k++) arr.SetValue(els[k], k); args[i] = arr; }
                        else if (t == typeof(BCOM.Point3d)) args[i] = o;
                        else if (t == typeof(bool)) args[i] = false;
                        else args[i] = ps[i].IsOptional && !(ps[i].DefaultValue is DBNull) ? ps[i].DefaultValue : (t.IsValueType ? Activator.CreateInstance(t) : null);
                    }
                    var cell = (BCOM.Element)m.Invoke(App, args);
                    if (cell != null) { App.ActiveModelReference.AddElement(cell); return cell; }
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            foreach (var e in els) App.ActiveModelReference.AddElement(e); return null;
        }
        // các thành phần của ô theo thứ tự (để ghi liên kết nhãn vào tệp thuộc tính)
        public static List<BCOM.Element> CellParts(BCOM.Element cell)
        {
            var o = new List<BCOM.Element>(); if (cell == null) return o;
            try { dynamic se = ((dynamic)cell).AsComplexElement.GetSubElements(); while ((bool)se.MoveNext()) o.Add((BCOM.Element)se.Current); } catch { }
            return o;
        }
        // xoá chữ / đường của nhãn: thành phần của ô thì xoá cả ô (mỗi ô một lần)
        public static int DeleteAll(IEnumerable<MEl> els) { return DeleteAll(els, new HashSet<long>()); }
        public static int DeleteAll(IEnumerable<MEl> els, HashSet<long> done)
        {
            int n = 0;
            foreach (var m in els) { var e = m.Parent ?? m.El; long id = Id(e); if (id != 0 && !done.Add(id)) continue; Delete(e); n++; }
            return n;
        }
        public static BCOM.Element AddLine(double[] pts, int lv, int color, int weight)
        {
            var v = new BCOM.Point3d[pts.Length / 2]; for (int i = 0; i < v.Length; i++) v[i] = P(pts[2 * i], pts[2 * i + 1]);
            BCOM.Element e = null;
            try { e = (BCOM.Element)CallApp("CreateLineElement2", v); } catch { e = null; }
            if (e == null)
            {
                // dự phòng: vẽ từng đoạn thẳng
                for (int i = 1; i < v.Length; i++) { var a = v[i - 1]; var b = v[i]; var s = (BCOM.Element)CallApp("CreateLineElement1", new[] { a, b }); Sym(s, lv, color, weight); App.ActiveModelReference.AddElement(s); e = s; }
                return e;
            }
            Sym(e, lv, color, weight); App.ActiveModelReference.AddElement(e); return e;
        }
        public static BCOM.Element AddShape(double[] pts, int lv, int color, bool fill)
        {
            int n = pts.Length / 2; if (n > 1 && pts[0] == pts[2 * n - 2] && pts[1] == pts[2 * n - 1]) n--;
            var v = new BCOM.Point3d[n]; for (int i = 0; i < n; i++) v[i] = P(pts[2 * i], pts[2 * i + 1]);
            var e = (BCOM.Element)App.CreateShapeElement1(null, ref v, (BCOM.MsdFillMode)(fill ? 1 : 0)); Sym(e, lv, color, 0); App.ActiveModelReference.AddElement(e); return e;
        }
        public static BCOM.Element AddCircle(double x, double y, double r, int lv, int color)
        {
            var o = P(x, y); var m = App.Matrix3dIdentity();
            var e = (BCOM.Element)App.CreateEllipseElement2(null, ref o, r, r, ref m, (BCOM.MsdFillMode)0); Sym(e, lv, color, 0); App.ActiveModelReference.AddElement(e); return e;
        }
        // chữ đặt TÂM tại (x,y); h: cao chữ (m); rot: góc (radian)
        public static BCOM.Element AddText(string s, double x, double y, double h, double rot, int lv, int color)
        {
            var e = MakeText(s, x, y, h, rot, lv, color); App.ActiveModelReference.AddElement(e); return e;
        }
        // mã canh lề chữ trong tệp bản vẽ: 6 giữa – trên, 7 giữa – giữa, 8 giữa – dưới, 13 phải – giữa
        public const int CanhGiuaTren = 6, CanhGiua = 7, CanhGiuaDuoi = 8, CanhPhaiGiua = 13;
        // chữ chưa đưa vào bản vẽ (để gom vào ô) – TÂM chữ tại (x,y)
        public static BCOM.Element MakeText(string s, double x, double y, double h, double rot, int lv, int color) { return MakeText(s, x, y, h, rot, lv, color, CanhGiua); }
        // chữ canh lề just, điểm canh (gốc chữ) đúng tại (x,y) – như nhãn của phần mềm địa chính. Không dựa vào khung bao của chữ:
        // tệp bản vẽ lưu khung bao chữ rộng quanh gốc chữ (không ôm sát nét chữ) nên đặt theo khung bao là lệch chữ
        public static BCOM.Element MakeText(string s, double x, double y, double h, double rot, int lv, int color, int just)
        {
            if (Cfg.I.Tcvn3) s = Tcvn.FromUnicode(s);
            var o = P(x, y); var m = Math.Abs(rot) < 1e-12 ? App.Matrix3dIdentity() : App.Matrix3dFromAxisAndRotationAngle(2, rot);
            var t = App.CreateTextElement1(null, s, ref o, ref m);
            dynamic dt = t; bool canh = false;
            try { dynamic ts = dt.TextStyle; ts.Height = h; ts.Width = h * Cfg.I.TextWidthRatio; try { ts.Justification = just; canh = true; } catch { } dt.TextStyle = ts; }
            catch { try { dt.TextStyle.Height = h; dt.TextStyle.Width = h * Cfg.I.TextWidthRatio; } catch { } }
            var e = (BCOM.Element)t; Sym(e, lv, color, 0);
            if (!(canh && DatGoc(dt, x, y)))
            {
                // dự phòng (không đặt được canh lề / gốc chữ): đặt theo khung bao chữ như bản trước
                try { var R = Range(e); var d = P(x - (R[0] + R[2]) / 2, y - (R[1] + R[3]) / 2); ((dynamic)e).Move(ref d); } catch { }
            }
            return e;
        }
        // đặt điểm canh của chữ tại (x,y): gán gốc chữ; không gán được thì dời theo gốc hiện tại
        static bool DatGoc(dynamic t, double x, double y)
        {
            try { var o = P(x, y); t.Origin = o; return true; } catch { }
            try { BCOM.Point3d g = t.Origin; var d = P(x - g.X, y - g.Y); t.Move(ref d); return true; } catch { }
            return false;
        }
        // bề rộng chữ (mét) khi cao h: tạo chữ tạm (không đưa vào bản vẽ) rồi đo khung chữ
        // (khung bao chữ trong tệp bản vẽ rộng quanh gốc chữ – cao hơn 1,5 lần cao chữ thì không dùng được: lấy bề rộng ước theo nét chữ)
        public static double TextW(string s, double h)
        {
            if (Reader != null || App == null) return 0;
            var s0 = s; if (Cfg.I.Tcvn3) s = Tcvn.FromUnicode(s);
            var o = P(0, 0); var m = App.Matrix3dIdentity(); var t = App.CreateTextElement1(null, s, ref o, ref m); dynamic dt = t;
            try { dynamic ts = dt.TextStyle; ts.Height = h; ts.Width = h * Cfg.I.TextWidthRatio; dt.TextStyle = ts; }
            catch { try { dt.TextStyle.Height = h; dt.TextStyle.Width = h * Cfg.I.TextWidthRatio; } catch { } }
            var R = Range((BCOM.Element)t); double w = R[2] - R[0], cao = R[3] - R[1];
            return w > 0 && cao > 0 && cao <= h * 1.5 ? w : UocRong(s0, h);
        }
        // bề rộng ước của chữ (phông Times / .VnTime: chữ số 0,63 h, dấu chấm phẩy 0,32 h, chữ in hoa theo độ rộng từng chữ)
        public static double UocRong(string s, double h)
        {
            const string HOA = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; double[] rHoa = { .72, .67, .67, .72, .61, .56, .72, .72, .33, .39, .72, .61, .89, .72, .72, .56, .72, .67, .56, .61, .72, .72, .94, .72, .72, .61 };
            double w = 0; foreach (var ch in (s ?? "").Normalize(System.Text.NormalizationForm.FormC))
            {
                double em; char c = char.ToUpperInvariant(ch); int i = HOA.IndexOf(c);
                if (char.IsDigit(ch)) em = .5; else if (ch == '.' || ch == ',' || ch == ' ' || ch == ':' || ch == ';') em = .25; else if (ch == '+') em = .56; else if (ch == '/' || ch == '-') em = .33;
                else if (ch == 'Đ' || ch == 'đ') em = char.IsUpper(ch) ? .72 : .5; else if (i >= 0) em = char.IsUpper(ch) ? rHoa[i] : .47; else em = .6;
                w += em;
            }
            return w * 1.26 * h * Math.Max(.2, Cfg.I.TextWidthRatio);
        }
        // đổi nội dung chữ (giữ nguyên vị trí, kiểu chữ)
        public static bool SetText(BCOM.Element e, string s)
        {
            try { dynamic t = ((dynamic)e).AsTextElement; t.Text = s; e.Rewrite(); return true; } catch { return false; }
        }
        // chữ là thành phần của ô: sửa trong ô rồi ghi lại cả ô
        public static bool SetText(MEl m, string s)
        {
            if (m.Parent == null) return SetText(m.El, s);
            try
            {
                long id = Id(m.El); dynamic c = ((dynamic)m.Parent).AsCellElement; c.ResetElementEnumeration(); bool ok = false;
                while ((bool)c.MoveToNextElement(true))
                {
                    dynamic cur = c.CopyCurrentElement(); BCOM.Element ce = cur; int t; try { t = (int)ce.Type; } catch { continue; } if (t != T_TEXT) continue;
                    string old; try { old = (string)cur.AsTextElement.Text; } catch { continue; }
                    bool same = id != 0 && Id(ce) == id; if (!same && old == m.Text) { var R = Range(ce); same = Math.Abs((R[0] + R[2]) / 2 - m.Cx) < 1e-3 && Math.Abs((R[1] + R[3]) / 2 - m.Cy) < 1e-3; }
                    if (!same) continue; dynamic te = cur.AsTextElement; te.Text = s; c.ReplaceCurrentElement(cur); ok = true; break;
                }
                if (ok) m.Parent.Rewrite(); return ok;
            }
            catch { return false; }
        }
        // tô sáng thửa trên bản vẽ (phần tử tạm, không ghi vào tệp); rings = null: bỏ tô sáng
        static readonly List<object> hils = new List<object>();
        public static void Hilite(List<double[]> rings) { HiliteMany(rings == null ? null : new List<List<double[]>> { rings }); }
        // tô sáng nhiều thửa cùng lúc (chọn nhiều thửa để tách, hợp); null: bỏ tô sáng
        public static void HiliteMany(List<List<double[]>> all)
        {
            foreach (var h in hils) try { ((dynamic)h).Reset(); } catch { }
            hils.Clear();
            if (all == null || App == null || Reader != null) return;
            foreach (var rings in all) { var h = Transient(rings); if (h != null) hils.Add(h); }
        }
        static object Transient(List<double[]> rings)
        {
            if (rings == null || rings.Count == 0) return null;
            try
            {
                var pts = rings[0]; int n = pts.Length / 2; if (n > 1 && pts[0] == pts[2 * n - 2] && pts[1] == pts[2 * n - 1]) n--; if (n < 3) return null;
                var v = new BCOM.Point3d[n]; for (int i = 0; i < n; i++) v[i] = P(pts[2 * i], pts[2 * i + 1]);
                var shp = (BCOM.Element)App.CreateShapeElement1(null, ref v, (BCOM.MsdFillMode)0); shp.Color = Cfg.I.MauSang; shp.LineWeight = 6;
                var m = typeof(BCOM.Application).GetMethod("CreateTransientElementContainer1"); if (m == null) return null;
                var ps = m.GetParameters(); var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    var t = ps[i].ParameterType; if (t.IsByRef) t = t.GetElementType(); var nm = t.Name.ToLowerInvariant();
                    if (i == 0) args[i] = shp;
                    else if (t.IsEnum) args[i] = Enum.ToObject(t, nm.Contains("view") ? 255 : nm.Contains("transient") || nm.Contains("flag") ? 1 : 0);
                    else args[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
                }
                return m.Invoke(App, args);
            }
            catch (Exception ex) { Program.Log(ex); return null; }
        }
        // chuyển phần tử sang lớp khác (giữ nguyên màu, nét, mã phần tử); chưa có lớp thì tạo lớp
        public static bool SetLevel(BCOM.Element e, int lv)
        {
            try { var L = EnsureLevel(lv); if (L == null) return false; e.Level = L; e.Rewrite(); return true; } catch { return false; }
        }
        // đổi lớp cả phần tử phức (ô nhãn N_THUA, khối chữ).
        // Ô (cell): luôn DỰNG LẠI – bản sao các thành phần (đã sang lớp mới) gom thành ô mới cùng tên, cùng gốc ô, đưa vào bản vẽ, rồi xoá ô cũ
        // (đọc lại theo mã phần tử ngay trước khi dựng). Cách này không phụ thuộc việc thư viện có ghi lại được thành phần của ô hay không
        // (đổi lớp tại chỗ có máy chỉ đổi được đầu ô, thành phần vẫn ở lớp cũ). Không tạo được ô thì đưa từng thành phần (đã ở lớp mới) vào bản vẽ.
        // Khối chữ (text node): đổi lớp tại chỗ. Sau cả lượt, LevelAll quét lại bản vẽ để kiểm tra thật (KiemLai). Trả false khi chưa đổi được.
        public static bool SetLevelDeep(BCOM.Element e, int lv)
        {
            BCOM.Level L; try { L = EnsureLevel(lv); } catch { L = null; } if (L == null || e == null) { GhiLoi("không tạo được level " + lv); return false; }
            int t = -1; try { t = (int)e.Type; } catch { }
            if (t != T_CELL && t != T_TNODE) { try { e.Level = L; e.Rewrite(); return true; } catch (Exception ex) { GhiLoi("đổi level: " + ex.Message); return false; } }
            var cu = DocLai(e);
            if (t == T_CELL) return DungLaiO(cu, L);
            try { DoiLopTrong(cu, L, t); try { cu.Level = L; } catch { } cu.Rewrite(); return true; }
            catch (Exception ex) { Program.Log(ex); GhiLoi("khối chữ: " + ex.Message); return TachKhoiChu(cu, L); }
        }
        public static int LevelDung;   // số ô đã dựng lại khi đổi lớp (để chẩn đoán)
        public static string LevelMsg = "";   // lỗi đầu tiên khi đổi lớp (hiện trong thông báo)
        static void GhiLoi(string s) { if (string.IsNullOrEmpty(LevelMsg)) LevelMsg = s; Program.Log(new Exception("Đổi level nhãn cũ: " + s)); }
        // dựng lại ô ở lớp L: bản sao thành phần → ô mới cùng tên, cùng gốc → đưa vào bản vẽ → xoá ô cũ; xoá không được thì bỏ phần vừa thêm
        static bool DungLaiO(BCOM.Element cu, BCOM.Level L)
        {
            string name = ""; double ox, oy; var R0 = new double[4]; try { R0 = Range(cu); } catch { }
            ox = (R0[0] + R0[2]) / 2; oy = (R0[1] + R0[3]) / 2;
            try { dynamic c = ((dynamic)cu).AsCellElement; try { name = (string)c.Name ?? ""; } catch { } try { ox = (double)c.Origin.X; oy = (double)c.Origin.Y; } catch { } } catch { }
            var parts = BanSaoThanhPhan(cu, L, T_CELL);
            if (parts.Count == 0) { GhiLoi("không chép được thành phần của ô " + name); return false; }
            BCOM.Element moi;
            try { moi = AddCell(name == "" ? "NHAN_CU" : name, parts, ox, oy); }      // null: các thành phần đã đưa vào bản vẽ từng phần tử
            catch (Exception ex) { Program.Log(ex); GhiLoi("không tạo được ô mới " + name + ": " + ex.Message); return false; }
            if (moi != null) try { moi.Level = L; moi.Rewrite(); } catch { }
            if (!Xoa(cu))
            {
                if (moi != null) Xoa(moi); else foreach (var x in parts) Xoa(x);
                GhiLoi("không xoá được ô cũ " + name); return false;
            }
            LevelDung++; return true;
        }
        // khối chữ không đổi lớp tại chỗ được: các dòng chữ (đã sang lớp L) đưa vào bản vẽ thành chữ riêng, xoá khối chữ cũ
        static bool TachKhoiChu(BCOM.Element cu, BCOM.Level L)
        {
            var parts = BanSaoThanhPhan(cu, L, T_TNODE); if (parts.Count == 0) { GhiLoi("không chép được dòng chữ của khối chữ"); return false; }
            var them = new List<BCOM.Element>();
            foreach (var x in parts) try { App.ActiveModelReference.AddElement(x); them.Add(x); } catch (Exception ex) { GhiLoi("thêm dòng chữ: " + ex.Message); }
            if (them.Count == 0 || !Xoa(cu)) { foreach (var x in them) Xoa(x); GhiLoi("không thay được khối chữ cũ"); return false; }
            return true;
        }
        // bản sao các thành phần trực tiếp của ô / khối chữ, đã đặt lớp L (ô lồng trong ô: đổi lớp cả thành phần của nó).
        // Cách 1: duyệt thành phần của thư viện (CopyCurrentElement); cách 2: thành phần đọc như khi đọc nhãn (GetSubElements) rồi nhân bản (Clone)
        static List<BCOM.Element> BanSaoThanhPhan(BCOM.Element e, BCOM.Level L, int t)
        {
            var o = new List<BCOM.Element>();
            try
            {
                dynamic d = e; dynamic c = t == T_CELL ? d.AsCellElement : d.AsTextNodeElement; c.ResetElementEnumeration();
                while ((bool)c.MoveToNextElement(false)) { BCOM.Element x = c.CopyCurrentElement(); if (x != null) o.Add(x); }
            }
            catch (Exception ex) { Program.Log(ex); o.Clear(); }
            if (o.Count == 0)
            {
                try { dynamic se = ((dynamic)e).AsComplexElement.GetSubElements(); while ((bool)se.MoveNext()) { BCOM.Element x = ((dynamic)se.Current).Clone(); if (x != null) o.Add(x); } }
                catch (Exception ex) { Program.Log(ex); o.Clear(); GhiLoi("chép thành phần: " + ex.Message); }
            }
            foreach (var x in o)
            {
                int tx = -1; try { tx = (int)x.Type; } catch { }
                if (tx == T_CELL || tx == T_TNODE) try { DoiLopTrong(x, L, tx); } catch { }
                try { x.Level = L; } catch (Exception ex) { GhiLoi("đặt level thành phần: " + ex.Message); }
            }
            return o;
        }
        static void DoiLopTrong(BCOM.Element e, BCOM.Level L, int t)
        {
            dynamic d = e; dynamic c = t == T_CELL ? d.AsCellElement : d.AsTextNodeElement; c.ResetElementEnumeration();
            while ((bool)c.MoveToNextElement(true)) { try { BCOM.Element cur = c.CopyCurrentElement(); cur.Level = L; c.ReplaceCurrentElement(cur); } catch (Exception ex) { Program.Log(ex); } }
        }
        // quét mới các ô / khối chữ vừa đổi lớp: thành phần nào chưa sang lớp mới (ô cũ chưa xoá được, khối chữ chưa ghi được) thì làm lại
        // từ chính phần tử vừa đọc; trả về số phần tử vẫn chưa đổi được
        static int KiemLai(Dictionary<long, int> phuc, int lv)
        {
            var L = Level(lv); if (L == null || phuc.Count == 0) return 0; int lvn = LvNumL(L); List<BCOM.Element> con = null;
            for (int lan = 0; lan < 3; lan++)
            {
                con = new List<BCOM.Element>();
                try
                {
                    var en = QuetKieu(T_CELL, T_TNODE);
                    while (en.MoveNext()) { var x = en.Current; long id = Id(x); if (id == 0 || !phuc.ContainsKey(id)) continue; if (!DungLop(x, lvn)) con.Add(x); }
                }
                catch (Exception ex) { Program.Log(ex); return 0; }
                if (con.Count == 0 || lan == 2) break;
                foreach (var x in con) { int t = phuc[Id(x)]; if (t == T_CELL) DungLaiO(x, L); else if (!TachKhoiChu(x, L)) try { DoiLopTrong(x, L, t); x.Rewrite(); } catch { } }
            }
            if (con != null && con.Count > 0) GhiLoi(con.Count + " ô / khối chữ đọc lại vẫn ở level cũ");
            return con == null ? 0 : con.Count;
        }
        static BCOM.ElementEnumerator QuetKieu(params int[] types)
        {
            try { var sc = (BCOM.ElementScanCriteria)App.CreateObjectInMicroStation("MicroStationDGN.ElementScanCriteria"); sc.ExcludeNonGraphical(); sc.ExcludeAllTypes(); foreach (var t in types) sc.IncludeType((BCOM.MsdElementType)t); return App.ActiveModelReference.Scan(sc); }
            catch { return App.ActiveModelReference.Scan(); }
        }
        // phần tử đúng như đang lưu trên bản vẽ (đọc lại theo mã phần tử); không đọc lại được thì dùng chính phần tử đang có
        static BCOM.Element DocLai(BCOM.Element e)
        {
            try { dynamic mr = App.ActiveModelReference; dynamic id = ((dynamic)e).ID; BCOM.Element x = mr.GetElementByID(id); if (x != null) return x; } catch { }
            return e;
        }
        // mọi thành phần (cả ô lồng trong ô) đều ở lớp lvn; không đọc được thành phần thì coi như đúng (không dựng lại khi không chắc)
        static bool DungLop(BCOM.Element e, int lvn)
        {
            try
            {
                dynamic se = ((dynamic)e).AsComplexElement.GetSubElements();
                while ((bool)se.MoveNext())
                {
                    BCOM.Element s = se.Current; int ts = -1; try { ts = (int)s.Type; } catch { }
                    if ((ts == T_CELL || ts == T_TNODE) && !DungLop(s, lvn)) return false;
                    if (LvNum(s) != lvn) return false;
                }
                return true;
            }
            catch { return true; }
        }
        // đổi lớp các phần tử (thành phần của ô thì đổi cả ô, mỗi ô một lần) – nhãn cũ khi vẽ nhãn mới; LevelLoi: số phần tử chưa đổi được
        public static int LevelAll(IEnumerable<MEl> els, int lv) { return LevelAll(els, lv, new HashSet<long>()); }
        // done: mã các phần tử đã xử lý trong cùng một lượt (một ô nhãn chia cho nhiều thửa chỉ đổi một lần – ô dựng lại thì ô cũ đã xoá)
        public static int LevelAll(IEnumerable<MEl> els, int lv, HashSet<long> done)
        {
            int n = 0, loi = 0; var phuc = new Dictionary<long, int>();   // ô / khối chữ đã xử lý: kết quả thật do lần quét lại quyết định
            foreach (var m in els)
            {
                var e = m.Parent ?? m.El; if (e == null) continue; long id = Id(e); if (id != 0 && !done.Add(id)) continue;
                int t = -1; try { t = (int)e.Type; } catch { }
                bool ok = SetLevelDeep(e, lv);
                if ((t == T_CELL || t == T_TNODE) && id != 0) { phuc[id] = t; n++; } else if (ok) n++; else loi++;
            }
            if (phuc.Count > 0) { int con = Level(lv) == null ? phuc.Count : KiemLai(phuc, lv); n -= con; loi += con; }
            LevelLoi += loi; return n;
        }
        public static int LevelLoi;
        // lớp n của bản vẽ; chưa có thì tạo lớp tên chuẩn "Level n", mã lớp n (ghi vào bảng lớp của tệp DGN)
        public static BCOM.Level EnsureLevel(int n)
        {
            var L = Level(n); if (L != null) return L;
            try
            {
                dynamic d = App.ActiveDesignFile; dynamic nl = d.AddNewLevel("Level " + n); try { nl.Number = n; } catch { } try { d.Levels.Rewrite(); } catch { } lvCache.Clear();
                var r = Level(n) ?? (BCOM.Level)nl; if (!LopMoi.Contains(n)) LopMoi.Add(n); return r;
            }
            catch (Exception ex) { Program.Log(ex); return null; }
        }
        // lớp vừa tạo thêm (bản vẽ chưa có) – báo người dùng một lần rồi xoá danh sách
        public static readonly List<int> LopMoi = new List<int>();
        public static string BaoLopMoi()
        {
            if (LopMoi.Count == 0) return ""; var s = " · bản đồ chưa có level " + string.Join(", ", LopMoi.OrderBy(x => x).Select(x => "Level " + x).ToArray()) + " – đã tạo level rồi vẽ"; LopMoi.Clear(); return s;
        }
        // lớp Default của bản vẽ (không tạo lớp mới): nền hiển thị như nền màu loại đất
        public static BCOM.Level LevelTen(string ten)
        {
            if (App == null || string.IsNullOrEmpty(ten)) return null;
            try { foreach (BCOM.Level lv in App.ActiveDesignFile.Levels) { try { if (string.Equals((lv.Name ?? "").Trim(), ten.Trim(), StringComparison.OrdinalIgnoreCase)) return lv; } catch { } } } catch (Exception ex) { Program.Log(ex); }
            return null;
        }
        public static void Delete(BCOM.Element e) { try { App.ActiveModelReference.RemoveElement(e); } catch { } }
        public static bool Xoa(BCOM.Element e) { if (e == null) return false; try { App.ActiveModelReference.RemoveElement(e); return true; } catch (Exception ex) { Program.Log(ex); return false; } }
        // đường mới (đường thẳng 2 điểm, đường gấp khúc từ 3 điểm) cùng kiểu với phần tử mẫu: lớp, màu, nét, kiểu đường, nhóm – dùng khi cắt ranh tại giao điểm
        public static BCOM.Element ThemTheoMau(BCOM.Element mau, double[] pts) { return ThemTheoMau(mau, pts, null); }
        // z: cao độ từng điểm (null: 0) – giữ đúng cao độ các đỉnh của đường cũ trên bản vẽ 3D
        public static BCOM.Element ThemTheoMau(BCOM.Element mau, double[] pts, double[] z)
        {
            var v = new BCOM.Point3d[pts.Length / 2]; for (int i = 0; i < v.Length; i++) { v[i] = P(pts[2 * i], pts[2 * i + 1]); if (z != null && i < z.Length) v[i].Z = z[i]; }
            string fn = v.Length == 2 ? "CreateLineElement1" : "CreateLineElement2"; BCOM.Element e = null;
            try { e = (BCOM.Element)CallApp(fn, v, mau); } catch { e = null; }
            if (e == null) e = (BCOM.Element)CallApp(fn, v, null);
            if (e == null) return null;
            try { e.Level = mau.Level; } catch { } try { e.Color = mau.Color; } catch { } try { e.LineWeight = mau.LineWeight; } catch { }
            dynamic a = mau, b = e;
            try { b.LineStyle = a.LineStyle; } catch { } try { b.GraphicGroup = a.GraphicGroup; } catch { } try { b.Class = a.Class; } catch { }
            App.ActiveModelReference.AddElement(e); return e;
        }
        // chuỗi phức / vùng phức chỉ gồm đoạn thẳng, đường gấp khúc (không có cung) – cắt được như đường gấp khúc
        public static bool ChiDoanThang(BCOM.Element e)
        {
            try { dynamic se = ((dynamic)e).AsComplexElement.GetSubElements(); int n = 0; while ((bool)se.MoveNext()) { int t = (int)((BCOM.Element)se.Current).Type; if (t != T_LINE && t != T_LSTR) return false; n++; } return n > 0; }
            catch { return false; }
        }
        // đổi đỉnh đầu/cuối của đường (giữ cao độ của đỉnh)
        public static bool MoveVertex(BCOM.Element e, int idx, double x, double y)
        {
            try
            {
                var p = P(x, y); dynamic vl = ((dynamic)e).AsVertexList;
                try { dynamic vs = vl.GetVertices(); int k = 0; foreach (dynamic q in vs) { if (k++ == idx) { p.Z = (double)q.Z; break; } } } catch { }
                vl.ModifyVertex(idx, ref p); e.Rewrite(); return true;
            }
            catch { return false; }
        }
        // cao độ các đỉnh của đường (null: bản vẽ phẳng – mọi đỉnh cao độ 0 – hoặc không đọc được)
        public static double[] Zs(BCOM.Element e)
        {
            try { var o = new List<double>(); bool co = false; foreach (dynamic q in ((dynamic)e).AsVertexList.GetVertices()) { double z = (double)q.Z; if (z != 0) co = true; o.Add(z); } return co ? o.ToArray() : null; }
            catch { return null; }
        }

        // ---------- XEM ----------
        public static void ZoomTo(double x0, double y0, double x1, double y1)
        {
            try
            {
                var v = App.ActiveDesignFile.Views[1]; double w = Math.Max(x1 - x0, 1), h = Math.Max(y1 - y0, 1);
                var o = P(x0, y0); var ex = P(w, h); v.set_Origin(ref o); v.set_Extents(ref ex); v.Redraw();
            }
            catch
            {
                try { App.CadInputQueue.SendKeyin(string.Format(System.Globalization.CultureInfo.InvariantCulture, "zoom center {0},{1}", (x0 + x1) / 2, (y0 + y1) / 2)); } catch { }
            }
        }
        public static void Select(IEnumerable<BCOM.Element> els)
        {
            try { App.ActiveModelReference.UnselectAllElements(); foreach (var e in els) App.ActiveModelReference.SelectElement(e, true); } catch { }
        }
        public static List<BCOM.Element> Selected()
        {
            var o = new List<BCOM.Element>(); try { var en = App.ActiveModelReference.GetSelectedElements(); while (en.MoveNext()) o.Add(en.Current); } catch { } return o;
        }
        public static void Save() { try { App.ActiveDesignFile.Save(); } catch { } }

        // ---------- LẤY ĐIỂM TRÊN BẢN VẼ ----------
        static PickCmd cur;
        public static void PickPoint(string prompt, Action<double, double> done, Action cancel)
        {
            if (cur != null) try { App.CommandState.StartDefaultCommand(); } catch { }
            cur = new PickCmd { Prompt = prompt, Done = done, Cancel = cancel };
            App.CommandState.StartPrimitive(cur, false);
        }
        // chạm liên tiếp nhiều điểm (chọn nhiều thửa): mỗi lần chạm gọi pt, lệnh vẫn chạy; chuột phải (Reset) là xong; lệnh khác chen vào là huỷ
        public static void PickMany(string prompt, Action<double, double> pt, Action done, Action cancel)
        {
            if (cur != null) try { App.CommandState.StartDefaultCommand(); } catch { }
            cur = new PickCmd { Prompt = prompt, Done = pt, Cancel = cancel, Finish = done, Many = true };
            App.CommandState.StartPrimitive(cur, false);
        }
        [ComVisible(true)]
        public class PickCmd : BCOM.IPrimitiveCommandEvents
        {
            public string Prompt; public Action<double, double> Done; public Action Cancel, Finish; public bool Many; bool fin;
            public void Start() { try { App.ShowCommand(Bt.Ten); App.ShowPrompt(Prompt); } catch { } }
            public void DataPoint(ref BCOM.Point3d Point, BCOM.View View)
            {
                double x = Point.X, y = Point.Y;
                if (Many) { var p = Done; if (p != null) Program.Ui(() => p(x, y)); return; }
                fin = true;
                try { App.CommandState.StartDefaultCommand(); } catch { }
                var d = Done; if (d != null) Program.Ui(() => d(x, y));
            }
            public void Dynamics(ref BCOM.Point3d Point, BCOM.View View, BCOM.MsdDrawingMode DrawMode) { }
            public void Keyin(string Keyin) { }
            public void Reset() { fin = true; try { App.CommandState.StartDefaultCommand(); } catch { } var c = Many && Finish != null ? Finish : Cancel; if (c != null) Program.Ui(c); }
            public void Cleanup() { if (!fin) { fin = true; var c = Cancel; if (c != null) Program.Ui(c); } }
        }
    }

    // chuyển Unicode → TCVN3 (phông .VnTime, .VnArial…)
    public static class Tcvn
    {
        const string U = "ĂÂÊÔƠƯĐăâêôơưđàảãáạằẳẵắặầẩẫấậèẻẽéẹềểễếệìỉĩíịòỏõóọồổỗốộờởỡớợùủũúụừửữứựỳỷỹýỵ";
        static readonly int[] C = { 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE,
            0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBB, 0xBC, 0xBD, 0xBE, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xCB, 0xCC, 0xCE, 0xCF, 0xD0, 0xD1, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6,
            0xD7, 0xD8, 0xDC, 0xDD, 0xDE, 0xDF, 0xE1, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC, 0xED, 0xEE, 0xEF, 0xF1, 0xF2, 0xF3, 0xF4,
            0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE };
        public static string ToUnicode(string s)
        {
            if (string.IsNullOrEmpty(s)) return s; var sb = new System.Text.StringBuilder();
            foreach (char ch in s) { int i = Array.IndexOf(C, (int)ch); sb.Append(i >= 0 ? U[i] : ch); }
            return sb.ToString();
        }
        public static string FromUnicode(string s)
        {
            if (string.IsNullOrEmpty(s)) return s; s = s.Normalize(System.Text.NormalizationForm.FormC); var sb = new System.Text.StringBuilder();
            foreach (char ch in s)
            {
                int i = U.IndexOf(ch); if (i >= 0) { sb.Append((char)C[i]); continue; }
                // chữ hoa có dấu: dùng mã chữ thường (hiển thị bằng phông chữ hoa .VnTimeH)
                char lo = char.ToLowerInvariant(ch); i = U.IndexOf(lo, 7); if (i >= 0 && ch != lo) { sb.Append((char)C[i]); continue; }
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
