// M8 Pro Địa chính – hồ sơ (mở bằng trình duyệt để in hoặc lưu PDF). © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace M8Pro
{
    public static class Reports
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static string E(string s) { return System.Security.SecurityElement.Escape(s ?? "") ?? ""; }
        static string N(double v) { return v.ToString("F1", IC); }
        const string HDR = "<div style='text-align:center;font-weight:bold'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div><div style='text-align:center;font-weight:bold;text-decoration:underline'>Độc lập - Tự do - Hạnh phúc</div>";
        const string CSS = "@page{size:A4;margin:15mm 15mm 15mm 20mm}body{font-family:'Times New Roman',serif;font-size:13pt;line-height:1.35;color:#000}h1{font-size:15pt;text-align:center;margin:12px 0 6px}table{border-collapse:collapse;width:100%}td,th{border:1px solid #000;padding:2px 5px;font-size:11.5pt}.nb td{border:0}.sig{display:flex;justify-content:space-between;text-align:center;margin-top:18px}.sig div{width:45%}.n{text-align:right}";
        static string Today { get { var d = DateTime.Now; return string.Format("ngày {0} tháng {1} năm {2}", d.Day, d.Month, d.Year); } }
        // gom nhiều hồ sơ thành một tệp in (mỗi hồ sơ một trang)
        static StringBuilder cap;
        public static int Batch(string title, IEnumerable<Parcel> ps, Action<Parcel> f)
        {
            cap = new StringBuilder(); int n = 0;
            try { foreach (var p in ps) { f(p); n++; } } finally { var b = cap.ToString(); cap = null; if (n > 0) Show(title, b); }
            return n;
        }
        public static void Show(string title, string body)
        {
            if (cap != null) { cap.Append("<div style='page-break-after:always'>" + body + "</div>"); return; }
            var f = Path.Combine(Path.GetTempPath(), "M8Pro_" + DateTime.Now.ToString("HHmmss") + "_" + new string(title.Where(char.IsLetterOrDigit).Take(30).ToArray()) + ".html");
            File.WriteAllText(f, "<!DOCTYPE html><html lang='vi'><head><meta charset='utf-8'><title>" + E(title) + "</title><style>" + CSS + "</style></head><body>" + body + "<script>setTimeout(function(){print()},500)</script></body></html>", new UTF8Encoding(true));
            Process.Start(f);
        }
        public static string Svg(Parcel p, int W, int H)
        {
            var nb = Dc.Neighbors(p); var b = (double[])p.BB.Clone(); double pad = Math.Max(b[2] - b[0], b[3] - b[1]) * .35; b = new[] { b[0] - pad, b[1] - pad, b[2] + pad, b[3] + pad };
            double s = Math.Min(W / (b[2] - b[0]), H / (b[3] - b[1]));
            Func<double, string> X = x => ((x - b[0]) * s).ToString("F1", IC); Func<double, string> Y = y => ((b[3] - y) * s).ToString("F1", IC);
            Func<double[], string> path = r => { var sb = new StringBuilder(); for (int i = 0; i < r.Length; i += 2) sb.Append(i == 0 ? "M" : "L").Append(X(r[i])).Append(' ').Append(Y(r[i + 1])); return sb.Append('Z').ToString(); };
            var o = new StringBuilder(string.Format("<svg xmlns='http://www.w3.org/2000/svg' width='{0}' height='{1}' viewBox='0 0 {0} {1}' style='font-family:Times New Roman'><defs><clipPath id='c'><rect width='{0}' height='{1}'/></clipPath></defs><g clip-path='url(#c)'>", W, H));
            var ce = Geo.Centroid(p.Rings[0]);
            foreach (var q in nb)
            {
                o.Append("<path d='" + string.Concat(q.Rings.Select(path).ToArray()) + "' fill='none' stroke='#555' stroke-width='.8'/>");
                // số thửa liền kề: trên đường từ tâm thửa chính sang thửa liền kề, phần nằm trong thửa liền kề và trong khung
                double t0 = -1, t1 = -1; for (int i = 1; i <= 60; i++) { double t = i / 60.0 * 1.5, x = ce[0] + (q.Lp[0] - ce[0]) * t, y = ce[1] + (q.Lp[1] - ce[1]) * t; bool ok = Geo.InPoly(x, y, q.Rings[0]) && x > b[0] && x < b[2] && y > b[1] && y < b[3]; if (ok) { if (t0 < 0) t0 = t; t1 = t; } else if (t0 >= 0) break; }
                if (t0 >= 0) { double t = (t0 + t1) / 2; o.Append("<text x='" + X(ce[0] + (q.Lp[0] - ce[0]) * t) + "' y='" + Y(ce[1] + (q.Lp[1] - ce[1]) * t) + "' font-size='10' text-anchor='middle' fill='#333'>" + E(q.So) + "</text>"); }
            }
            o.Append("<path d='" + string.Concat(p.Rings.Select(path).ToArray()) + "' fill='none' stroke='#000' stroke-width='2'/>");
            var v = Dc.Verts(p); int n = v.Length / 2; var c = Geo.Centroid(v);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n; double mx = (v[2 * i] + v[2 * j]) / 2, my = (v[2 * i + 1] + v[2 * j + 1]) / 2, L = Geo.Dist(v[2 * i], v[2 * i + 1], v[2 * j], v[2 * j + 1]);
                double a = -Math.Atan2(v[2 * j + 1] - v[2 * i + 1], v[2 * j] - v[2 * i]) * 180 / Math.PI; if (a > 90) a -= 180; if (a < -90) a += 180;
                double dx = c[0] - mx, dy = c[1] - my, dl = Math.Sqrt(dx * dx + dy * dy); if (dl == 0) dl = 1; double ox = mx + dx / dl * 8 / s, oy = my + dy / dl * 8 / s;
                o.Append("<text x='" + X(ox) + "' y='" + Y(oy) + "' font-size='9.5' text-anchor='middle' dominant-baseline='middle' transform='rotate(" + a.ToString("F1", IC) + " " + X(ox) + " " + Y(oy) + ")'>" + Dc.F(L, 2) + "</text>");
            }
            for (int i = 0; i < n; i++) { double x = v[2 * i], y = v[2 * i + 1], dx = x - c[0], dy = y - c[1], L = Math.Sqrt(dx * dx + dy * dy); if (L == 0) L = 1; o.Append("<circle cx='" + X(x) + "' cy='" + Y(y) + "' r='2.2'/><text x='" + X(x + dx / L * 11 / s) + "' y='" + Y(y + dy / L * 11 / s) + "' font-size='10' font-weight='bold' text-anchor='middle' dominant-baseline='middle'>" + (i + 1) + "</text>"); }
            o.Append("<text x='" + X(p.Lp[0]) + "' y='" + Y(p.Lp[1]) + "' font-size='12' font-weight='bold' text-anchor='middle'>" + E(p.So) + "</text>");
            o.Append("</g><g transform='translate(" + (W - 22) + ",22)'><path d='M0 -16 L6 6 L0 2 L-6 6 Z'/><text y='18' font-size='10' text-anchor='middle'>B</text></g></svg>");
            return o.ToString();
        }
        static string Coords(Parcel p)
        {
            var v = Dc.Verts(p); int n = v.Length / 2; var sb = new StringBuilder("<table><tr><th>Số hiệu đỉnh</th><th>X (m)</th><th>Y (m)</th><th>Cạnh</th><th>Chiều dài (m)</th></tr>");
            // X (Bắc) = toạ độ y của bản vẽ, Y (Đông) = toạ độ x
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; sb.Append("<tr><td style='text-align:center'>" + (i + 1) + "</td><td class='n'>" + Dc.F(v[2 * i + 1], 3) + "</td><td class='n'>" + Dc.F(v[2 * i], 3) + "</td><td style='text-align:center'>" + (i + 1) + "-" + (j + 1) + "</td><td class='n'>" + Dc.F(Geo.Dist(v[2 * i], v[2 * i + 1], v[2 * j], v[2 * j + 1]), 2) + "</td></tr>"); }
            return sb.Append("</table>").ToString();
        }
        static string Addr(Parcel p) { return E(p.DiaChi != "" ? p.DiaChi : Cfg.I.Place); }
        public static void TrichLuc(Parcel p)
        {
            Show("Trích lục thửa " + p.So, HDR + "<h1>TRÍCH LỤC BẢN ĐỒ ĐỊA CHÍNH</h1><p style='text-align:center'>(Số: ……… /TLBĐ)</p><table class='nb'><tr><td>1. Thửa đất số: <b>" + E(p.So) + "</b></td><td>Tờ bản đồ số: <b>" + E(Dc.To) + "</b></td></tr>" +
                "<tr><td colspan='2'>2. Địa chỉ thửa đất: " + Addr(p) + "</td></tr><tr><td colspan='2'>3. Diện tích: <b>" + Dc.FD(p.Dt) + " m²</b>" + (p.Dtpl != null ? " (diện tích pháp lý: " + Dc.FD(p.Dtpl.Value) + " m²)" : "") + "</td></tr>" +
                "<tr><td colspan='2'>4. Mục đích sử dụng đất: " + E(Dc.LdName(p.Ld)) + "</td></tr><tr><td colspan='2'>5. Tên người sử dụng đất: " + E(p.Chu) + "</td></tr><tr><td colspan='2'>6. Sơ đồ thửa đất:</td></tr></table>" +
                "<div style='text-align:center;border:1px solid #000;margin:4px 0'>" + Svg(p, 620, 460) + "</div><p style='margin:2px 0'>7. Toạ độ đỉnh thửa và chiều dài cạnh (hệ VN-2000):</p>" + Coords(p) +
                "<div class='sig'><div><i>…………, " + Today + "</i><br><b>NGƯỜI TRÍCH LỤC</b><br><i>(Ký, ghi rõ họ tên)</i></div><div><i>…………, " + Today + "</i><br><b>THỦ TRƯỞNG CƠ QUAN</b><br><i>(Ký tên, đóng dấu)</i></div></div>");
        }
        public static void Phieu(Parcel p)
        {
            var nb = Dc.Neighbors(p);
            Show("Phiếu đo đạc chỉnh lý thửa " + p.So, HDR + "<h1>PHIẾU ĐO ĐẠC CHỈNH LÝ THỬA ĐẤT</h1><p>1. Thửa đất số: <b>" + E(p.So) + "</b>; Tờ bản đồ số: <b>" + E(Dc.To) + "</b>; Tỷ lệ: 1/" + Cfg.I.TyLe + "<br>Địa chỉ thửa đất: " + Addr(p) +
                "<br>Diện tích sau đo đạc chỉnh lý: <b>" + Dc.FD(p.Dt) + " m²</b>; Mục đích sử dụng: " + E(Dc.LdName(p.Ld)) + "<br>Người sử dụng đất: " + E(p.Chu) + "<br>Nội dung chỉnh lý: ………………………………………………</p>" +
                "<p>2. Sơ đồ thửa đất (thửa tiếp giáp: " + string.Join(", ", nb.Select(q => E(q.So)).ToArray()) + "):</p><div style='text-align:center;border:1px solid #000'>" + Svg(p, 620, 430) + "</div><p>3. Bảng toạ độ đỉnh thửa:</p>" + Coords(p) +
                "<div class='sig'><div><b>NGƯỜI ĐO ĐẠC</b><br><i>(Ký, ghi rõ họ tên)</i></div><div><i>…………, " + Today + "</i><br><b>ĐƠN VỊ ĐO ĐẠC</b><br><i>(Ký tên, đóng dấu)</i></div></div>");
        }
        public static void BienBan(Parcel p)
        {
            var nb = Dc.Neighbors(p); int nv = Dc.Verts(p).Length / 2;
            Show("Biên bản ranh giới thửa " + p.So, HDR + "<h1>BIÊN BẢN XÁC ĐỊNH RANH GIỚI, MỐC GIỚI THỬA ĐẤT</h1><p>Hôm nay, " + Today + ", đã tiến hành xác định ranh giới, mốc giới thửa đất số <b>" + E(p.So) + "</b>, tờ bản đồ số <b>" + E(Dc.To) + "</b> tại " + Addr(p) +
                ".<br>Người sử dụng đất: <b>" + (p.Chu != "" ? E(p.Chu) : "…………………") + "</b>. Diện tích đo đạc: <b>" + Dc.FD(p.Dt) + " m²</b>.</p><p><b>Sơ đồ thửa đất:</b></p><div style='text-align:center;border:1px solid #000'>" + Svg(p, 620, 400) + "</div>" +
                "<p><b>Mô tả ranh giới:</b> thửa có " + nv + " đỉnh, ranh giới đi qua các điểm " + string.Join(" - ", Enumerable.Range(1, nv).Select(i => i.ToString()).ToArray()) + " - 1.</p>" +
                "<p><b>Ý kiến của người sử dụng đất liền kề:</b></p><table><tr><th>Cạnh tiếp giáp</th><th>Thửa liền kề</th><th>Người sử dụng đất liền kề</th><th>Ký xác nhận</th></tr>" +
                string.Concat(nb.Select(q => "<tr><td></td><td style='text-align:center'>" + E(q.So) + "</td><td>" + E(q.Chu) + "</td><td style='height:34px'></td></tr>").ToArray()) + "</table>" +
                "<div class='sig'><div><b>NGƯỜI SỬ DỤNG ĐẤT</b><br><i>(Ký, ghi rõ họ tên)</i></div><div><b>CÁN BỘ ĐO ĐẠC</b><br><i>(Ký, ghi rõ họ tên)</i></div></div><div class='sig'><div><b>CÔNG CHỨC ĐỊA CHÍNH CẤP XÃ</b><br><i>(Ký, ghi rõ họ tên)</i></div><div><b>XÁC NHẬN CỦA UBND CẤP XÃ</b><br><i>(Ký tên, đóng dấu)</i></div></div>");
        }
        public static void BangToaDo(Parcel p) { Show("Bảng toạ độ thửa " + p.So, "<h1>BẢNG TOẠ ĐỘ ĐỈNH THỬA ĐẤT</h1><p style='text-align:center'>Thửa số " + E(p.So) + ", tờ bản đồ số " + E(Dc.To) + " – " + E(Cfg.I.Place) + "<br>Hệ toạ độ VN-2000 · Diện tích " + Dc.FD(p.Dt) + " m²</p>" + Coords(p)); }
        public static void SoMucKe()
        {
            var L = Dc.P.OrderBy(p => { int v; return int.TryParse(p.So, out v) ? v : int.MaxValue; }).ToList(); var sb = new StringBuilder();
            for (int i = 0; i < L.Count; i++) { var p = L[i]; sb.Append("<tr><td>" + (i + 1) + "</td><td>" + E(p.So) + "</td><td>" + E(p.Chu) + "</td><td class='n'>" + Dc.FD(p.Dt) + "</td><td>" + E(p.Ld) + "</td><td>" + E(p.DiaChi) + "</td><td>" + E(p.GhiChu) + "</td></tr>"); }
            Show("Sổ mục kê tờ " + Dc.To, HDR + "<h1>SỔ MỤC KÊ ĐẤT ĐAI</h1><p style='text-align:center'>" + E(Cfg.I.Place) + " · Tờ bản đồ số " + E(Dc.To) + "</p><table><tr><th>STT</th><th>Số thửa</th><th>Tên người sử dụng, quản lý đất</th><th>Diện tích (m²)</th><th>Loại đất</th><th>Địa chỉ</th><th>Ghi chú</th></tr>" + sb +
                "<tr><th colspan='3'>Tổng cộng: " + L.Count + " thửa</th><th class='n'>" + Dc.FD(L.Sum(p => p.Dt)) + "</th><th colspan='3'></th></tr></table><div class='sig'><div></div><div><i>…………, " + Today + "</i><br><b>NGƯỜI LẬP</b></div></div>");
        }
        public static void ThongKe()
        {
            var L = Dc.StatLD(); double tot = Math.Max(Dc.P.Sum(p => p.Dt), 1e-9);
            Show("Thống kê loại đất", "<h1>BẢNG THỐNG KÊ DIỆN TÍCH THEO LOẠI ĐẤT</h1><p style='text-align:center'>" + E(Cfg.I.Place) + " · Tờ " + E(Dc.To) + "</p><table><tr><th>Loại đất</th><th>Tên loại đất</th><th>Số thửa</th><th>Diện tích (m²)</th><th>Tỷ lệ (%)</th></tr>" +
                string.Concat(L.Select(kv => "<tr><td>" + E(kv.Key) + "</td><td>" + E(Dc.LoaiDat.ContainsKey(kv.Key) ? Dc.LoaiDat[kv.Key] : "") + "</td><td class='n'>" + kv.Value[0] + "</td><td class='n'>" + Dc.FD(kv.Value[1]) + "</td><td class='n'>" + Dc.F(kv.Value[1] / tot * 100, 2) + "</td></tr>").ToArray()) +
                "<tr><th colspan='2'>Tổng</th><th class='n'>" + Dc.P.Count + "</th><th class='n'>" + Dc.FD(tot) + "</th><th class='n'>100</th></tr></table>");
        }
        // danh sách công khai kết quả kiểm tra hồ sơ đăng ký
        public static void CongKhai(List<Parcel> L)
        {
            var sb = new StringBuilder(); int i = 0;
            foreach (var p in L) { var r = p.Row; sb.Append("<tr><td>" + (++i) + "</td><td>" + E(p.Chu) + (r != null && r.NamSinh != "" ? "<br><i>Năm sinh: " + E(r.NamSinh) + "</i>" : "") + "</td><td>" + E(r != null ? r.ChuDc : "") + "</td><td style='text-align:center'>" + E(Dc.To) + "</td><td style='text-align:center'>" + E(p.So) + "</td><td class='n'>" + Dc.FD(p.Dt) + "</td><td>" + E(r != null && r.MdText != "" ? r.MdText : p.Ld) + "</td><td>" + E(p.DiaChi) + "</td><td></td></tr>"); }
            Show("Danh sách công khai tờ " + Dc.To, HDR + "<h1>DANH SÁCH CÔNG KHAI KẾT QUẢ KIỂM TRA HỒ SƠ ĐĂNG KÝ</h1><p style='text-align:center'>" + E(Cfg.I.Place) + " · Tờ bản đồ số " + E(Dc.To) + "</p><table><tr><th>STT</th><th>Tên người sử dụng đất</th><th>Địa chỉ thường trú</th><th>Tờ</th><th>Thửa</th><th>Diện tích (m²)</th><th>Mục đích sử dụng</th><th>Địa chỉ thửa đất</th><th>Ghi chú</th></tr>" + sb +
                "<tr><th colspan='5'>Tổng: " + L.Count + " thửa</th><th class='n'>" + Dc.FD(L.Sum(p => p.Dt)) + "</th><th colspan='3'></th></tr></table><p>Thời gian công khai: từ ngày …/…/…… đến ngày …/…/……</p><div class='sig'><div><b>ĐẠI DIỆN KHU DÂN CƯ</b></div><div><i>…………, " + Today + "</i><br><b>TM. ỦY BAN NHÂN DÂN</b></div></div>");
        }
        // bảng diện tích trong / ngoài quy hoạch
        public static void QuyHoach(List<Nv.QhKq> L)
        {
            var sb = new StringBuilder(); int i = 0;
            foreach (var k in L.OrderBy(x => { double v; return double.TryParse(x.P.So, out v) ? v : 1e12; })) { var p = k.P; sb.Append("<tr><td>" + (++i) + "</td><td style='text-align:center'>" + E(p.So) + "</td><td>" + E(p.Chu) + "</td><td>" + E(p.Ld) + "</td><td class='n'>" + Dc.FD(p.Dt) + "</td><td class='n'>" + Dc.FD(k.Trong) + "</td><td class='n'>" + Dc.FD(k.Ngoai) + "</td></tr>"); }
            Show("Diện tích quy hoạch tờ " + Dc.To, "<h1>BẢNG DIỆN TÍCH THỬA ĐẤT TRONG VÀ NGOÀI QUY HOẠCH</h1><p style='text-align:center'>" + E(Cfg.I.Place) + " · Tờ bản đồ số " + E(Dc.To) + "</p><table><tr><th>STT</th><th>Số thửa</th><th>Người sử dụng</th><th>Loại đất</th><th>Diện tích thửa (m²)</th><th>Trong quy hoạch (m²)</th><th>Ngoài quy hoạch (m²)</th></tr>" + sb +
                "<tr><th colspan='4'>Tổng " + L.Count + " thửa</th><th class='n'>" + Dc.FD(L.Sum(k => k.P.Dt)) + "</th><th class='n'>" + Dc.FD(L.Sum(k => k.Trong)) + "</th><th class='n'>" + Dc.FD(L.Sum(k => k.Ngoai)) + "</th></tr></table>");
        }
        // xuất danh sách thửa ra tệp CSV (Excel mở trực tiếp)
        public static void Csv(string file)
        {
            // dấu phân cách và dấu thập phân theo cài đặt vùng của Windows để Excel mở đúng cột, đúng số
            var cu = CultureInfo.CurrentCulture; string sep = cu.TextInfo.ListSeparator; if (string.IsNullOrEmpty(sep) || sep.Length != 1) sep = ","; if (sep == cu.NumberFormat.NumberDecimalSeparator) sep = ";";
            var sb = new StringBuilder(); Func<object, string> q = v => v is double ? ((double)v).ToString("0.###", cu) : "\"" + Convert.ToString(v, cu).Replace("\"", "\"\"") + "\"";
            sb.AppendLine(string.Join(sep, new[] { "Tờ", "Số thửa", "Diện tích bản đồ (m2)", "Diện tích trong tệp (m2)", "Diện tích pháp lý (m2)", "Loại đất", "Chi tiết mục đích", "Chủ sử dụng", "Năm sinh", "Địa chỉ chủ", "Địa chỉ thửa", "Ghi chú", "X tâm", "Y tâm" }.Select(q).ToArray()));
            foreach (var p in Dc.P.OrderBy(p => { int v; return int.TryParse(p.So, out v) ? v : int.MaxValue; }))
                sb.AppendLine(string.Join(sep, new object[] { Dc.To, p.So, Math.Round(p.Dt, Cfg.I.DtLe), p.Row != null && p.Row.Dt.HasValue ? (object)p.Row.Dt.Value : "", p.Dtpl.HasValue ? (object)p.Dtpl.Value : "", p.Ld, p.Row != null ? p.Row.MdText : "", p.Chu, p.Row != null ? p.Row.NamSinh : "", p.Row != null ? p.Row.ChuDc : "", p.DiaChi, p.GhiChu, Math.Round(p.Lp[1], 3), Math.Round(p.Lp[0], 3) }.Select(q).ToArray()));
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
        }
        // đọc tệp chữ: UTF-8 (có/không BOM), nếu sai mã thì đọc theo bảng mã Windows
        static string[] ReadLines(string file)
        {
            var b = File.ReadAllBytes(file);
            try { return new UTF8Encoding(false, true).GetString(b).TrimStart('\uFEFF').Replace("\r\n", "\n").Split('\n').Where(l => l.Trim() != "").ToArray(); }
            catch
            {
                Encoding enc; try { enc = Encoding.GetEncoding(1258); } catch { enc = Encoding.Default; }   // Excel trên Windows tiếng Việt lưu CSV theo bảng mã 1258
                return enc.GetString(b).Normalize(NormalizationForm.FormC).Replace("\r\n", "\n").Split('\n').Where(l => l.Trim() != "").ToArray();
            }
        }
        // nhập thông tin từ CSV (lưu từ Excel): cột "Số thửa" bắt buộc
        public static int ImportCsv(string file) { Dc.Hold++; try { return ImportCsv_(file); } finally { Dc.Hold--; Dc.Commit(); } }
        static int ImportCsv_(string file)
        {
            var lines = ReadLines(file); if (lines.Length < 2) return 0; char sep = lines[0].Count(ch => ch == ';') > lines[0].Count(ch => ch == ',') ? ';' : ',';
            Func<string, string[]> split = l => { var o = new List<string>(); var sb = new StringBuilder(); bool qt = false; foreach (char ch in l) { if (ch == '"') qt = !qt; else if (ch == sep && !qt) { o.Add(sb.ToString()); sb.Clear(); } else sb.Append(ch); } o.Add(sb.ToString()); return o.ToArray(); };
            Func<string, string> nz = s => new string(s.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray()).Replace('đ', 'd');
            var h = split(lines[0]).Select(nz).ToList(); Func<string, int> col = k => h.FindIndex(c => c.Contains(k));
            int cS = col("so thua"), cL = col("loai dat") >= 0 ? col("loai dat") : col("muc dich"), cC = h.FindIndex(c => (c.Contains("chu") || c.Contains("ten")) && !c.Contains("ghi") && !c.Contains("dia chi")), cD = h.FindIndex(c => c.Contains("dia chi") && !c.Contains("chu")), cP = col("phap ly"), cG = col("ghi chu"); if (cS < 0) throw new Exception("Không thấy cột “Số thửa”");
            var by = Dc.P.Where(p => p.So != "").GroupBy(p => p.So).ToDictionary(g => g.Key, g => g.First()); int n = 0;
            foreach (var l in lines.Skip(1))
            {
                var r = split(l); if (r.Length <= cS) continue; Parcel p; if (!by.TryGetValue(r[cS].Trim(), out p)) continue;
                Action<int, string> set = (i, k) => { if (i >= 0 && i < r.Length && r[i].Trim() != "") Dc.SetField(p, k, r[i]); };
                set(cL, "ld"); set(cC, "chu"); set(cD, "diachi"); set(cP, "dtpl"); set(cG, "ghichu"); n++;
            }
            return n;
        }
    }
}
