// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – giao diện Tách thửa, Hợp thửa, Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22), ảnh Google đúng toạ độ. © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace M8Pro
{
    public partial class MainForm
    {
        // chạm vào bên trong ranh các thửa để chọn (chạm lại để bỏ chọn), chuột phải: xong
        void ChonThua(string viec, Action<List<Parcel>> xong)
        {
            var sel = new List<Parcel>();
            Action hien = () =>
            {
                Ms.HiliteMany(sel.Select(p => p.Rings).ToList());
                var s = viec + " · đã chọn " + sel.Count + (sel.Count > 0 ? ": " + string.Join(", ", sel.Select(q => TachHop.Ten(q)).ToArray()) : "") + " – chuột phải: xong";
                Msg(s); Ms.Prompt(s);
            };
            Msg(viec + " – chạm vào bên trong ranh thửa (chạm lại để bỏ chọn), chuột phải: xong");
            Ms.PickMany(viec + " › chạm vào trong thửa, chuột phải: xong", (x, y) =>
            {
                var p = Dc.Find(x, y); if (p == null) { Msg("Không có thửa tại điểm vừa chạm"); return; }
                if (sel.Contains(p)) sel.Remove(p); else sel.Add(p); Cur = p; hien();
            }, () =>
            {
                Ms.Hilite(null); if (sel.Count == 0) { Msg("Chưa chọn thửa nào – đã thôi"); return; }
                try { xong(sel); } catch (Exception ex) { Program.Log(ex); Msg("Lỗi: " + ex.Message); MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }, () => { Ms.Hilite(null); Msg("Đã thôi chọn thửa"); });
        }
        static string Ktt(double v) { return TachHop.DoPhut(v); }

        // ================= TÁCH THỬA =================
        void TachThua()
        {
            var cu = Dc.P.Where(p => !p.Virtual && !p.FromGtp).ToList();
            var msg = Dc.Build(false); if (Dc.P.Count == 0) { Msg(msg); return; }
            ChonThua("Tách thửa (chạm vào trong từng thửa mới theo thứ tự đánh số)", sel => TachXong(cu, sel));
        }
        void TachXong(List<Parcel> cu, List<Parcel> sel)
        {
            string loi; var nhom = TachHop.NhomTach(sel, cu, out loi);
            if (nhom == null)
            {
                Msg("⚠ " + loi);
                MessageBox.Show(this, loi + "\n\nCách tách thửa: vẽ ranh tách trên level ranh thửa bằng công cụ vẽ của MicroStation (nối kín từ ranh tới ranh), chọn lệnh Tách thửa, chạm vào trong từng thửa mới theo thứ tự muốn đánh số, chuột phải để kết thúc.", "Tách thửa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            foreach (var g in nhom) if (!TachMot(g)) return;
        }
        public bool TachMot(NhomTach g)
        {
            var c = g.Goc; int n0 = TachHop.MaxSo(); var fs = new List<F>();
            fs.Add(new F("_a", "Thửa gốc số " + c.So + ", tờ " + c.To + ": " + Dc.FD(c.DtGtp ?? c.Dt) + " m²" + (c.Ld != "" ? ", " + c.Ld : "") + (c.Chu != "" ? " – " + c.Chu : ""), "") { Info = true });
            if (g.CanhBao != "") fs.Add(new F("_b", "⚠ " + g.CanhBao, "") { Info = true });
            fs.Add(new F("_c", "Số thửa mới đánh tiếp theo số lớn nhất của tờ (" + n0 + ") – Điều 17 Thông tư 26/2024/TT-BTNMT; số tạm thì ghi " + c.So + "-1, " + c.So + "-2…", "") { Info = true });
            for (int i = 0; i < g.Moi.Count; i++) { fs.Add(new F("so" + i, "Thửa mới " + (i + 1) + " · " + Dc.FD(g.Moi[i].Dt) + " m² – số thửa", (n0 + i + 1).ToString())); fs.Add(new F("ld" + i, "      loại đất", c.Ld)); }
            ThemLuaChon(fs, "thửa " + c.So);
            Ms.HiliteMany(g.Moi.Select(p => p.Rings).ToList());
            var r = Ask.Show(this, "Tách thửa " + c.So + " thành " + g.Moi.Count + " thửa", fs.ToArray()); Ms.Hilite(null);
            if (r == null) { Msg("Đã thôi tách thửa " + c.So); return false; }
            var so = new List<string>(); var ld = new List<string>();
            for (int i = 0; i < g.Moi.Count; i++) { so.Add(r["so" + i].Trim()); ld.Add(r["ld" + i].Trim().ToUpperInvariant().Replace(" ", "")); }
            var loi = KiemSo(so, g.Moi, c.Row); if (loi != "") { MessageBox.Show(this, loi, "Tách thửa", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            bool bv = LuuLuaChon(r);
            Msg(TachHop.ApDungTach(g, so, ld, r.ContainsKey("gtp") && r["gtp"] == "1", r["nhan"] == "1")); Lam();
            if (bv) LapBanVe(TachHop.Json("tach", new List<ThuaCu> { c }, g.Moi), g.Moi[0], r["fmt"]);
            return true;
        }
        // số thửa mới: không trống, không trùng nhau, không trùng thửa khác của tờ
        string KiemSo(List<string> so, List<Parcel> moi, GRow bo)
        {
            if (so.Any(s => s == "")) return "Chưa ghi số thửa cho thửa mới.";
            var d = so.GroupBy(s => s).Where(x => x.Count() > 1).Select(x => x.Key).ToList(); if (d.Count > 0) return "Số thửa mới trùng nhau: " + string.Join(", ", d.ToArray());
            var co = new HashSet<string>(Dc.P.Where(p => !moi.Contains(p) && p.So != "").Select(p => p.So));
            if (Dc.G != null) foreach (var r in Dc.SheetRows()) if (r != bo && r.So != "" && !moi.Any(p => p.Row == r)) co.Add(r.So);
            var t = so.Where(co.Contains).ToList(); return t.Count > 0 ? "Số thửa " + string.Join(", ", t.ToArray()) + " đã có trên tờ bản đồ – chọn số khác." : "";
        }
        void ThemLuaChon(List<F> fs, string goc)
        {
            if (Dc.G != null && !Dc.DocLap) fs.Add(new F("gtp", "Ghi vào tệp thuộc tính " + Dc.G.Name + ": thêm thửa mới, " + goc + " lưu vào thửa cũ", "1") { Check = true });
            else if (Dc.DocLap) fs.Add(new F("_g", "Tệp " + Dc.G.Name + " đang kết nối độc lập (mọi tờ): không ghi vào tệp, thửa cũ lưu trên máy để lập bản vẽ", "") { Info = true });
            else fs.Add(new F("_g", "Chưa kết nối tệp thuộc tính (.gtp): chỉ sửa bản đồ, thửa cũ lưu trên máy để lập bản vẽ", "") { Info = true });
            fs.Add(new F("nhan", "Vẽ nhãn thửa mới theo quy định (số thửa và gạch ngang level " + Cfg.I.SoLop + ", loại đất level " + Cfg.I.LdLop + ", diện tích level " + Cfg.I.DtLop + ")", "1") { Check = true });
            fs.Add(new F("_n", "Nhãn cũ của " + goc + ": " + (Cfg.I.NhanCuLop > 0 ? "chuyển sang level " + Cfg.I.NhanCuLop + " (không xoá; đổi level trong Thiết lập)" : "xoá (Thiết lập: level nhãn cũ = 0)"), "") { Info = true });
            fs.Add(new F("bv", "Lập ngay Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22)", Cfg.I.TachHopBanVe ? "1" : "0") { Check = true });
            int k = Math.Max(0, Array.IndexOf(DdMa, Cfg.I.DinhDang ?? "pdf")); fs.Add(new F("fmt", "Định dạng bản vẽ", DdTen[k]) { Opt = DdTen });
        }
        bool LuuLuaChon(Dictionary<string, string> r)
        {
            bool bv = r["bv"] == "1"; Cfg.I.TachHopBanVe = bv; int k = Math.Max(0, Array.IndexOf(DdTen, r["fmt"])); Cfg.I.DinhDang = DdMa[k]; Cfg.I.Save(); return bv;
        }
        void Lam() { if (attr != null && !attr.IsDisposed) try { attr.Reload(); } catch { } }

        // ================= HỢP THỬA =================
        void HopThua()
        {
            var msg = Dc.Build(false); if (Dc.P.Count == 0) { Msg(msg); return; }
            if (Dc.P.Any(p => p.FromGtp)) { Msg("Hợp thửa cần thửa dựng từ ranh trên bản đồ (Bản đồ → Tạo thửa)"); return; }
            ChonThua("Hợp thửa (chạm vào trong các thửa cần hợp; thửa chạm đầu tiên giữ thông tin chính)", HopXong);
        }
        void HopXong(List<Parcel> sel)
        {
            string loi; var g = TachHop.NhomHop(sel, out loi);
            if (g == null) { Msg("⚠ " + loi); MessageBox.Show(this, loi, "Hợp thửa", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            int n0 = TachHop.MaxSo(); var fs = new List<F>();
            foreach (var p in sel) fs.Add(new F("_" + p.I, "Thửa " + (p.So == "" ? "?" : p.So) + ": " + Dc.FD(p.Dt) + " m²" + (p.Ld != "" ? ", " + p.Ld : "") + (p.Chu != "" ? " – " + p.Chu : ""), "") { Info = true });
            if (g.CanhBao != "") fs.Add(new F("_w", "⚠ " + g.CanhBao, "") { Info = true });
            fs.Add(new F("_r", "Đoạn ranh chung chuyển sang level " + Cfg.I.HopLop + "; thửa sau khi hợp đánh số tiếp theo số lớn nhất của tờ (" + n0 + ")", "") { Info = true });
            fs.Add(new F("so", "Số thửa sau khi hợp", (n0 + 1).ToString())); fs.Add(new F("ld", "Loại đất", TachHop.HopLd(sel)));
            ThemLuaChon(fs, "các thửa " + string.Join(", ", sel.Select(p => p.So).ToArray()));
            Ms.HiliteMany(sel.Select(p => p.Rings).ToList());
            var r = Ask.Show(this, "Hợp " + sel.Count + " thửa", fs.ToArray()); Ms.Hilite(null);
            if (r == null) { Msg("Đã thôi hợp thửa"); return; }
            var so = r["so"].Trim(); var ld = r["ld"].Trim().ToUpperInvariant().Replace(" ", "");
            var e = KiemSo(new List<string> { so }, sel, null);
            if (e != "" && !sel.Any(p => p.So == so)) { MessageBox.Show(this, e, "Hợp thửa", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            bool bv = LuuLuaChon(r); Parcel m; List<ThuaCu> cu;
            Msg(TachHop.ApDungHop(g, so, ld, r.ContainsKey("gtp") && r["gtp"] == "1", r["nhan"] == "1", out m, out cu)); Lam();
            if (m != null && bv) LapBanVe(TachHop.Json("hop", cu, new List<Parcel> { m }), m, r["fmt"]);
        }

        // ================= BẢN VẼ TÁCH THỬA, HỢP THỬA ĐẤT (Mẫu số 22) =================
        // chạm vào trong thửa mới (sau tách / hợp) → bản vẽ theo thửa cũ đã lưu; chưa tách / hợp: lập phương án (đường tách đang chọn, hoặc các thửa định hợp)
        void BanVeTachHop(string name, string extra)
        {
            var msg = Dc.Build(false); if (Dc.P.Count == 0) { Msg(msg); return; }
            var cut = T2Cut(); // đường tách đang chọn trong MicroStation (đọc trước khi bắt đầu chạm thửa)
            ChonThua("Bản vẽ tách thửa, hợp thửa (chạm vào trong thửa mới sau tách / hợp" + (cut != "" ? ", hoặc thửa cần tách theo đường tách đang chọn" : "") + ")", sel =>
            {
                var lich = TachHop.LichSuJson(sel);
                if (lich != null) { GuiT2b(name, sel[0], extra + ",\"lich\":" + lich); return; }
                var hop = sel.Count > 1 ? T2Hop(sel) : "";
                if (cut != "" || hop != "") { GuiT2b(name, sel[0], extra + cut + hop); return; }
                MessageBox.Show(this, "Thửa " + TachHop.Ten(sel[0]) + " chưa có thông tin thửa cũ (chưa tách, hợp bằng lệnh Tách thửa / Hợp thửa).\n\n" +
                    "• Tách, hợp trên bản đồ: dùng lệnh Tách thửa hoặc Hợp thửa – xong lập bản vẽ ngay, hoặc chọn lại lệnh này và chạm thửa mới.\n" +
                    "• Chỉ lập phương án (chưa sửa bản đồ): vẽ đường tách cắt qua thửa trong MicroStation, chọn đường đó rồi chọn lệnh này và chạm thửa; hoặc chạm vào trong 2 thửa trở lên để lập phương án hợp.", name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }
        // đường tách vẽ trong MicroStation đang được chọn (lập phương án chưa sửa bản đồ)
        string T2Cut()
        {
            var cuts = new List<double[]>();
            foreach (var e in Ms.Selected()) { int t; try { t = (int)e.Type; } catch { continue; } if (t == Ms.T_LINE || t == Ms.T_LSTR || t == Ms.T_CSTR) foreach (var l in Ms.LinesOf(e)) if (l.Length >= 4) cuts.Add(l); }
            if (cuts.Count == 0) return ""; var ic = CultureInfo.InvariantCulture; var sb = new StringBuilder(",\"cut\":[");
            for (int i = 0; i < cuts.Count; i++) { if (i > 0) sb.Append(','); sb.Append('[').Append(string.Join(",", cuts[i].Select(v => v.ToString("0.####", ic)).ToArray())).Append(']'); }
            return sb.Append(']').ToString();
        }
        static string T2Hop(List<Parcel> sel)
        {
            var ic = CultureInfo.InvariantCulture; var sb = new StringBuilder(",\"hop\":[");
            for (int i = 0; i < sel.Count; i++) { if (i > 0) sb.Append(','); sb.Append('[').Append(sel[i].Lp[0].ToString("0.####", ic)).Append(',').Append(sel[i].Lp[1].ToString("0.####", ic)).Append(']'); }
            return sb.Append(']').ToString();
        }
        void LapBanVe(string lich, Parcel p, string fmtTen)
        {
            int k = Math.Max(0, Array.IndexOf(DdTen, fmtTen)); if (fmtTen == null) k = Math.Max(0, Array.IndexOf(DdMa, Cfg.I.DinhDang ?? "pdf"));
            GuiT2b("Bản vẽ tách thửa, hợp thửa đất (Mẫu số 22)", p, ",\"fmt\":" + Web.J(DdMa[k]) + ",\"lich\":" + lich);
        }
        // gửi lệnh lập bản vẽ: lưu tệp thuộc tính và bản đồ trước (bộ máy lập mẫu đọc lại tệp trên đĩa)
        void GuiT2b(string name, Parcel p, string extra)
        {
            if (Dc.G != null) try { Dc.Commit(); } catch (Exception ex) { Program.Log(ex); }
            if (Dc.G != null && Dc.G.Dirty && Confirm("Tệp thuộc tính " + Dc.G.Name + " có thay đổi chưa lưu. Bản vẽ lập theo tệp đã lưu trên đĩa.\n\nLưu tệp thuộc tính ngay?"))
                try { Dc.G.Save(Dc.G.Path_); } catch (Exception ex) { Msg("Không lưu được " + Dc.G.Name + ": " + ex.Message); }
            Ms.Save(); Web.Send("t2b", name, p, extra);
            Msg("Đang lập " + name + (p != null ? " cho thửa " + p.So : "") + " … (tệp lưu vào " + Web.OutDir() + ")");
        }

        // ================= ẢNH GOOGLE ĐÚNG TOẠ ĐỘ =================
        // chạm vào trong thửa (hoặc điểm bất kỳ trên bản vẽ): mở ảnh vệ tinh Google Maps, ghim đúng điểm – toạ độ VN-2000 (kinh tuyến trục theo thiết lập,
        // mặc định 105°00′, múi chiếu 3°, k0 = 0,9999) đổi sang WGS84; máy có Google Earth Pro thì mở thêm ranh thửa (tệp .kml) chồng lên ảnh
        void AnhGoogle()
        {
            HoiKtt();
            if (Dc.P.Count == 0 || Dc.Dirty) try { Msg(Dc.Build(false)); } catch (Exception ex) { Program.Log(ex); }
            var s = "Ảnh Google đúng toạ độ › chạm vào trong thửa hoặc điểm cần xem (VN-2000, kinh tuyến trục " + Ktt(Cfg.I.Ktt) + ", múi chiếu 3°)";
            Msg(s + " – chuột phải để thôi");
            Ms.PickPoint(s, (x, y) =>
            {
                var p = Dc.P.Count > 0 ? Dc.Find(x, y) : null;
                if (p != null) { Cur = p; Ms.Hilite(p.Rings); if (attr != null && !attr.IsDisposed) attr.SelectParcel(p); }
                MoGoogle(p, x, y);
            }, () => Msg("Đã thôi xem ảnh Google"));
        }
        // p: thửa vừa chạm (null: điểm ngoài thửa) – ghim tại điểm đặt nhãn của thửa (luôn nằm trong thửa), phóng vừa thửa
        void MoGoogle(Parcel p, double x, double y)
        {
            var ic = CultureInfo.InvariantCulture; double ex = p != null ? p.Lp[0] : x, ey = p != null ? p.Lp[1] : y;
            var w = Geo.Vn2000ToWgs(ex, ey, Cfg.I.Ktt); string q = w[0].ToString("0.0000000", ic) + "," + w[1].ToString("0.0000000", ic);
            int z = 19; if (p != null) { double span = Math.Max(p.BB[2] - p.BB[0], p.BB[3] - p.BB[1]); z = span < 40 ? 20 : span < 100 ? 19 : span < 220 ? 18 : span < 450 ? 17 : 16; }
            string kml = null; if (p != null) try { kml = Kml(new List<Parcel> { p }); } catch (Exception e2) { Program.Log(e2); }
            bool ge = kml != null && MoGoogleEarth(kml);
            try { System.Diagnostics.Process.Start("https://www.google.com/maps/place/" + q + "/@" + q + "," + z + "z/data=!3m1!1e3"); } catch (Exception e3) { Msg("Không mở được trình duyệt: " + e3.Message); return; }
            Msg("Ảnh Google " + (p != null ? "tại thửa " + p.So + " tờ " + Dc.To : "tại điểm X = " + ey.ToString("0.000", ic) + ", Y = " + ex.ToString("0.000", ic)) + " – vĩ độ, kinh độ (WGS84) " + q.Replace(",", ", ") +
                " · VN-2000, kinh tuyến trục " + Ktt(Cfg.I.Ktt) + ", múi chiếu 3°" + (ge ? " · Google Earth Pro: ranh thửa chồng lên ảnh" : kml != null ? " · ranh thửa (KML, mở bằng Google Earth): " + kml : ""));
        }
        // ranh các thửa (WGS84) dạng KML – mở bằng Google Earth Pro để thấy ranh thửa chồng đúng lên ảnh vệ tinh
        static string Kml(List<Parcel> sel)
        {
            var ic = CultureInfo.InvariantCulture; var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document><name>").Append(Esc("Tờ " + Dc.To + " – thửa " + string.Join(", ", sel.Select(p => p.So).ToArray()))).Append("</name>");
            sb.Append("<Style id=\"t\"><LineStyle><color>ff00ffff</color><width>2.5</width></LineStyle><PolyStyle><fill>0</fill></PolyStyle></Style>");
            foreach (var p in sel)
            {
                sb.Append("<Placemark><name>").Append(Esc(p.So)).Append("</name><description>").Append(Esc("Thửa " + p.So + " tờ " + Dc.To + ": " + Dc.FD(p.Dt) + " m² " + p.Ld)).Append("</description><styleUrl>#t</styleUrl><Polygon><tessellate>1</tessellate>");
                for (int r = 0; r < p.Rings.Count; r++)
                {
                    sb.Append(r == 0 ? "<outerBoundaryIs>" : "<innerBoundaryIs>").Append("<LinearRing><coordinates>"); var g = p.Rings[r];
                    for (int i = 0; i <= g.Length / 2; i++) { int k = i % (g.Length / 2); var w = Geo.Vn2000ToWgs(g[2 * k], g[2 * k + 1], Cfg.I.Ktt); sb.Append(w[1].ToString("0.00000000", ic)).Append(',').Append(w[0].ToString("0.00000000", ic)).Append(",0 "); }
                    sb.Append("</coordinates></LinearRing>").Append(r == 0 ? "</outerBoundaryIs>" : "</innerBoundaryIs>");
                }
                sb.Append("</Polygon></Placemark>");
            }
            sb.Append("</Document></kml>");
            var d = Path.Combine(Web.OutDir(), "Google"); Directory.CreateDirectory(d);
            var f = Path.Combine(d, "Thua_" + string.Join("-", sel.Select(p => p.So == "" ? "x" : p.So).Take(6).ToArray()) + "_to-" + (Dc.To == "" ? "x" : Dc.To) + ".kml");
            File.WriteAllText(f, sb.ToString(), new UTF8Encoding(false)); return f;
        }
        static string Esc(string s) { return System.Security.SecurityElement.Escape(s ?? ""); }
        static bool MoGoogleEarth(string kml)
        {
            foreach (var d in new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") })
            {
                if (string.IsNullOrEmpty(d)) continue; var f = Path.Combine(d, @"Google\Google Earth Pro\client\googleearth.exe"); if (!File.Exists(f)) continue;
                try { System.Diagnostics.Process.Start(f, "\"" + kml + "\""); return true; } catch (Exception ex) { Program.Log(ex); }
            }
            return false;
        }
        // kinh tuyến trục còn để 104°45′: hỏi một lần có đổi sang 105°00′ (bản đồ khu vực dùng kinh tuyến trục 105°00′, múi chiếu 3°)
        public void HoiKtt()
        {
            try
            {
                if (Cfg.I.KttHoi || Math.Abs(Cfg.I.Ktt - 105) < 1e-9) return; Cfg.I.KttHoi = true; Cfg.I.Save();
                if (MessageBox.Show(this, "Kinh tuyến trục đang đặt " + Ktt(Cfg.I.Ktt) + ".\n\nBản đồ dùng hệ VN-2000 kinh tuyến trục 105°00′, múi chiếu 3°? Chọn Có để đổi sang 105°00′ (ảnh Google, chỉ đường, toạ độ WGS84 sẽ đúng vị trí).\nĐổi lại sau ở Hệ thống → Thiết lập đơn vị hành chính.", Bt.Ten, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                { Cfg.I.Ktt = 105; Cfg.I.Save(); Msg("Đã đặt kinh tuyến trục 105°00′, múi chiếu 3°"); }
            }
            catch { }
        }
    }
}
