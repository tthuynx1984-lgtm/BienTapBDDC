// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – ẢNH VỆ TINH NỀN dưới bản đồ DGN (bật / tắt). © Nguyễn Xuân Thủy
// Bật: tải ô ảnh vệ tinh (Google Maps qua Map Tiles API bằng khoá của người dùng, hoặc Esri World Imagery), nắn đúng về lưới VN-2000
// (kinh tuyến trục theo thiết lập – mặc định 105°00′, múi chiếu 3°, k0 = 0,9999, 7 tham số VN-2000 → WGS84), ghép thành một ảnh JPEG kèm tệp
// toạ độ .jgw rồi gắn vào MicroStation bằng Raster Manager ở lớp nền (Background), không in. Thu phóng / di chuyển khung nhìn: tự dựng lại ảnh
// đúng độ nét màn hình – nét tới mức chi tiết nhất nguồn ảnh có ở vùng đó, phóng quá mức đó thì nội suy mượt (bicubic), không vỡ ô.
// Tải song song trên luồng nền, giữ ảnh cũ tới khi ảnh mới xong – MicroStation không bị đơ. Tắt, đóng tệp: tự gỡ ảnh khỏi bản vẽ, xoá tệp tạm.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BCOM = Bentley.Interop.MicroStationDGN;

namespace M8Pro
{
    public static class AnhNen
    {
        public const string TEN = "Ảnh vệ tinh nền (bật / tắt)", TEN_TL = "Thiết lập ảnh vệ tinh…", LOGIC = "AnhVeTinh";
        public static bool On { get; private set; }
        public static event Action Doi;               // bật / tắt đổi trạng thái (nút trên thanh công cụ hiện đang bấm)
        static MainForm Fm;
        static System.Windows.Forms.Timer tm;
        static int gen, soTep;
        static bool busy;
        static string tep = "", xet;                   // bản đồ đang gắn ảnh; khung nhìn đã xét
        static Khung daThay; static DateTime luc = DateTime.MinValue, nghiDen = DateTime.MinValue; static int viewMoi;
        static readonly Dictionary<int, string> vet = new Dictionary<int, string>();
        static KetQua cur;                             // ảnh đang hiện
        static NguonAnh nguon;
        static readonly CultureInfo IV = CultureInfo.InvariantCulture;
        const int TS = 256, MAXP = 2600, MAXO = 160;
        // tên ảnh tạm riêng cho từng lần chạy MicroStation: tệp DGN còn sót ảnh của lần trước (máy tắt đột ngột) không bao giờ trỏ nhầm sang ảnh vùng khác
        static readonly string PHIEN = TaoPhien();
        static string TaoPhien() { int pid = 0; try { pid = System.Diagnostics.Process.GetCurrentProcess().Id; } catch { } return DateTime.Now.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture) + "_" + pid.ToString(CultureInfo.InvariantCulture); }
        // vùng nhớ dựng ảnh dùng lại giữa các lần (MicroStation V8i chạy 32 bit: tránh xin liên tục khối nhớ lớn mới)
        static readonly object khoaVe = new object();
        static int[] bM, bT, bO;
        static int[] Dem(ref int[] b, int n) { if (b == null || b.Length < n) { b = null; b = new int[n]; } return b; }

        // ---------- thư mục ảnh tạm: C:\ProgramData\M8Pro\AnhVeTinh (đường dẫn không chứa tên người dùng) ----------
        static string dir;
        public static string Dir
        {
            get
            {
                if (dir != null) return dir;
                foreach (var b in new[] { Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.GetTempPath() })
                {
                    try { if (string.IsNullOrEmpty(b)) continue; var d = Path.Combine(Path.Combine(b, "M8Pro"), "AnhVeTinh"); Directory.CreateDirectory(d); var t = Path.Combine(d, "~ghi.tmp"); File.WriteAllText(t, "1"); File.Delete(t); return dir = d; } catch { }
                }
                return dir = Path.GetTempPath();
            }
        }

        // ================= BẬT / TẮT =================
        public static void BatTat(MainForm f)
        {
            Fm = f;
            if (On) { Tat(true); return; }
            if (string.IsNullOrEmpty(Cfg.I.AnhNguon) && !ThietLap(f, true)) return;
            try { f.HoiKtt(); } catch { }
            nguon = TaoNguon(); On = true; gen++; busy = false; cur = null; daThay = null; xet = null; vet.Clear(); tep = null; nghiDen = DateTime.MinValue; luc = DateTime.MinValue;
            if (tm == null) { tm = new System.Windows.Forms.Timer { Interval = 300 }; tm.Tick += (s, e) => { try { Tick(); } catch (Exception ex) { Program.Log(ex); } }; }
            tm.Start(); Bao();
            f.Msg("Ảnh vệ tinh nền: đang tải " + nguon.Ten + " theo khung nhìn (VN-2000, kinh tuyến trục " + TachHop.DoPhut(Cfg.I.Ktt) + ", múi chiếu 3°)…");
        }
        public static void Tat(bool bao)
        {
            if (!On) return; On = false; gen++; busy = false; if (tm != null) tm.Stop();
            int n = 0; try { n = MsRaster.Go(r => MsRaster.LaCuaTa(r)); MsRaster.VeLai(); } catch (Exception ex) { Program.Log(ex); }
            cur = null; XoaTepCu(null); bM = bT = bO = null; Bao();
            if (bao && Fm != null) Fm.Msg("Đã tắt ảnh vệ tinh nền" + (n > 0 ? " – đã gỡ ảnh khỏi bản vẽ" : ""));
        }
        static void Bao() { var d = Doi; if (d != null) try { d(); } catch { } }

        // trước khi MicroStation đóng tệp bản đồ (đổi tệp, thoát): gỡ ảnh để tệp DGN không giữ ảnh tạm; tệp mới mở tự gắn lại nếu còn bật
        public static void TruocKhiDongTep()
        {
            if (!On && cur == null) return;
            try { MsRaster.Go(r => MsRaster.LaCuaTa(r)); } catch (Exception ex) { Program.Log(ex); }
            cur = null; tep = null; gen++; busy = false; daThay = null; xet = null;
        }
        // nạp trong MicroStation (AddIn): bắt sự kiện đóng / mở tệp bản đồ (tên sự kiện đọc theo phiên bản MicroStation đang chạy)
        public static void GanSuKien(object addin)
        {
            try
            {
                if (addin == null) return; var ev = addin.GetType().GetEvent("NewDesignFileEvent"); if (ev == null) return;
                var m = typeof(AnhNen).GetMethod("SuKienTep", BindingFlags.Static | BindingFlags.NonPublic);
                ev.AddEventHandler(addin, Delegate.CreateDelegate(ev.EventHandlerType, m));
            }
            catch (Exception ex) { Program.Log(ex); }
        }
        static void SuKienTep(object sender, object e)
        {
            try
            {
                var p = e == null ? null : e.GetType().GetProperty("WhenCode"); var w = p == null ? "" : Convert.ToString(p.GetValue(e, null));
                if (w.IndexOf("BeforeDesignFileClose", StringComparison.OrdinalIgnoreCase) >= 0) { TruocKhiDongTep(); NenMau.TruocKhiDongTep(); }
                else if (!On && w.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0 && w.IndexOf("Close", StringComparison.OrdinalIgnoreCase) < 0) DonSot();
            }
            catch (Exception ex) { Program.Log(ex); }
        }
        // tệp vừa mở còn ảnh nền tạm của lần chạy trước (MicroStation tắt đột ngột lúc đang bật ảnh) mà ảnh nền đang tắt: tự gỡ.
        // Chỉ xét khi thư mục ảnh tạm còn ảnh của lần chạy khác – máy chưa dùng ảnh vệ tinh thì không đụng tới Raster Manager.
        static System.Windows.Forms.Timer tmDon;
        static void DonSot()
        {
            try
            {
                var d = dir ?? Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "M8Pro"), "AnhVeTinh");
                if (!Directory.Exists(d) || !Directory.GetFiles(d, "AnhVeTinh_*.jpg").Any(f => !CuaPhien(f))) return;
                if (tmDon == null)
                {
                    tmDon = new System.Windows.Forms.Timer { Interval = 1500 };
                    tmDon.Tick += (s, e) => { tmDon.Stop(); try { if (!On && Ms.App != null && MsRaster.Go(r => MsRaster.LaCuaTa(r)) > 0) MsRaster.VeLai(); } catch (Exception ex) { Program.Log(ex); } };
                }
                tmDon.Stop(); tmDon.Start();
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        // ================= THIẾT LẬP =================
        static readonly string[] NG = { "Google Maps (cần khoá Map Tiles API)", "Esri World Imagery (không cần khoá)" };
        public static bool ThietLap(MainForm f, bool lanDau)
        {
            var fs = new List<F>();
            if (lanDau) fs.Add(new F("_0", "Bật ảnh vệ tinh: ảnh hiện ngay dưới nền bản đồ trong MicroStation, đúng toạ độ VN-2000 (kinh tuyến trục " + TachHop.DoPhut(Cfg.I.Ktt) + ", múi chiếu 3°); thu phóng tới đâu ảnh nét tới đó.", "") { Info = true });
            bool esri = Cfg.I.AnhNguon == "Esri" || (string.IsNullOrEmpty(Cfg.I.AnhNguon) && string.IsNullOrEmpty(Cfg.I.GoogleKey));
            fs.Add(new F("ng", "Nguồn ảnh", esri ? NG[1] : NG[0]) { Opt = NG });
            fs.Add(new F("key", "Khoá Google Maps Platform (Map Tiles API)", Cfg.I.GoogleKey ?? ""));
            fs.Add(new F("_1", "Ảnh Google: tạo khoá tại console.cloud.google.com → bật Map Tiles API → Credentials → Create API key (Google miễn phí 100.000 ô ảnh mỗi tháng, quá mức tính phí theo bảng giá Google; mỗi lần dựng ảnh khoảng 30–150 ô, ô đã tải được dùng lại). Chưa có khoá: dùng ảnh Esri.", "") { Info = true });
            fs.Add(new F("_2", "Ảnh chỉ để xem, đối chiếu (điều khoản của Google không cho số hoá, vẽ ranh theo ảnh). Ảnh nền không in ra, tự gỡ khỏi bản vẽ khi tắt hoặc đóng tệp.", "") { Info = true });
            fs.Add(new F("auto", "Tự dựng lại ảnh khi di chuyển, thu phóng khung nhìn", Cfg.I.AnhTuLamMoi ? "1" : "0") { Check = true });
            var r = Ask.Show(f, "Ảnh vệ tinh nền", fs.ToArray()); if (r == null) return false;
            var key = r["key"].Trim(); bool gg = r["ng"] == NG[0];
            if (gg && key == "") { MessageBox.Show(f, "Chưa có khoá Google – dùng ảnh Esri. Có khoá thì vào Bản đồ → " + TEN_TL + " để nhập.", "Ảnh vệ tinh nền", MessageBoxButtons.OK, MessageBoxIcon.Information); gg = false; }
            Cfg.I.GoogleKey = key; Cfg.I.AnhNguon = gg ? "Google" : "Esri"; Cfg.I.AnhTuLamMoi = r["auto"] == "1"; Cfg.I.Save();
            if (On) { nguon = TaoNguon(); LamMoi(); f.Msg("Ảnh vệ tinh nền: nguồn " + nguon.Ten); }
            return true;
        }
        static NguonAnh TaoNguon()
        {
            if (Cfg.I.AnhNguon == "Google" && !string.IsNullOrEmpty(Cfg.I.GoogleKey)) return new NguonGoogle(Cfg.I.GoogleKey.Trim());
            return new NguonEsri();
        }
        // dựng lại ngay theo khung nhìn hiện tại
        public static void LamMoi() { if (!On) return; gen++; busy = false; cur = null; daThay = null; xet = null; luc = DateTime.MinValue; nghiDen = DateTime.MinValue; }

        // ================= THEO DÕI KHUNG NHÌN (luồng giao diện, 0,3 giây một lần) =================
        class Khung { public int V; public double X0, Y0, X1, Y1; public int W, H; public string Key; }
        static void Tick()
        {
            if (!On || Ms.App == null) return;
            string fn = ""; try { fn = Ms.FullName ?? ""; } catch { }
            if (fn != tep)
            {
                tep = fn; cur = null; daThay = null; xet = null; vet.Clear(); gen++; busy = false;
                if (fn != "") try { MsRaster.Go(r => MsRaster.LaCuaTa(r)); } catch (Exception ex) { Program.Log(ex); }   // ảnh tạm còn sót trong tệp (máy tắt đột ngột…)
                XoaTepCu(null);                                                                                          // tệp cũ đã đóng: ảnh tạm của nó không còn dùng
            }
            if (fn == "") return;
            var k = DocKhung(); if (k == null) return;
            if (daThay == null || daThay.Key != k.Key) { daThay = k; luc = DateTime.Now; xet = null; }
            if (busy || DateTime.Now < nghiDen) return;
            if (cur != null && (DateTime.Now - luc).TotalMilliseconds < 380) return;   // còn đang kéo / thu phóng
            if (cur != null && !Cfg.I.AnhTuLamMoi) return;
            if (xet == daThay.Key) return;
            DoPixel(daThay); xet = daThay.Key;
            if (Phu(daThay)) return;
            Bat(daThay);
        }
        // khung nhìn vừa thay đổi (hoặc khung mở đầu tiên): toạ độ góc dưới trái, góc trên phải (mét VN-2000)
        static Khung DocKhung()
        {
            BCOM.Views vs; try { vs = Ms.App.ActiveDesignFile.Views; } catch { return null; }
            Khung chon = null, dau = null;
            for (int i = 1; i <= 8; i++)
            {
                try
                {
                    var v = vs[i]; if (!MsRaster.Bool(v, "IsOpen")) continue;
                    double ox, oy, ex, ey; if (!MsRaster.Pt(v, "get_Origin", out ox, out oy) || !MsRaster.Pt(v, "get_Extents", out ex, out ey)) continue;
                    if (ex <= 0 || ey <= 0) continue;
                    var k = new Khung { V = i, X0 = ox, Y0 = oy, X1 = ox + ex, Y1 = oy + ey };
                    k.Key = string.Format(IV, "{0}|{1:0.####}|{2:0.####}|{3:0.####}|{4:0.####}", i, k.X0, k.Y0, k.X1, k.Y1);
                    string cu; if (vet.TryGetValue(i, out cu) && cu != k.Key) viewMoi = i; vet[i] = k.Key;
                    if (dau == null) dau = k; if (i == viewMoi) chon = k;
                }
                catch { }
            }
            return chon ?? dau;
        }
        // kích thước khung nhìn trên màn hình (điểm ảnh) – để dựng ảnh đúng độ nét màn hình
        static void DoPixel(Khung k)
        {
            k.W = 0; k.H = 0;
            try
            {
                var v = Ms.App.ActiveDesignFile.Views[k.V]; double ax, ay, bx, by;
                if (MsRaster.Pixel(v, k.X0, k.Y0, out ax, out ay) && MsRaster.Pixel(v, k.X1, k.Y1, out bx, out by)) { k.W = (int)Math.Round(Math.Abs(bx - ax)); k.H = (int)Math.Round(Math.Abs(by - ay)); }
            }
            catch { }
            if (k.W < 64 || k.H < 64 || k.W > 9000 || k.H > 9000)
            {   // không đọc được: ước theo màn hình
                var sc = Screen.PrimaryScreen.WorkingArea; double asp = (k.X1 - k.X0) / Math.Max(1e-9, k.Y1 - k.Y0);
                k.W = (int)(sc.Width * 0.8); k.H = (int)Math.Max(64, k.W / Math.Max(0.05, asp)); if (k.H > sc.Height) { k.H = sc.Height; k.W = (int)Math.Max(64, k.H * asp); }
            }
        }
        // ảnh đang hiện còn phủ kín khung nhìn và còn đúng độ nét màn hình (không cần dựng lại) – phóng to quá 1,2 lần là dựng lại cho nét
        static bool Phu(Khung k)
        {
            if (cur == null) return false; double e = (k.X1 - k.X0) * 0.002;
            if (k.X0 < cur.X0 - e || k.Y0 < cur.Y0 - e || k.X1 > cur.X1 + e || k.Y1 > cur.Y1 + e) return false;
            double t = cur.Res / ResDung(k); return t > 0.72 && t < 1.2;
        }
        // cỡ điểm ảnh (mét) sẽ dựng cho khung nhìn: đúng độ nét màn hình, trừ khi ảnh vượt MAXP điểm ảnh mỗi chiều
        static double ResDung(Khung k)
        {
            double w = k.X1 - k.X0, h = k.Y1 - k.Y0, res = w / Math.Max(64, k.W), m = Math.Max(w, h) * 1.16 / res;
            return m > MAXP ? res * m / MAXP : res;
        }

        // ================= DỰNG ẢNH (luồng nền) =================
        public class KetQua
        {
            public string File, Nguon; public double X0, Y0, X1, Y1, Res; public int Z, ZMuon, NO, NTai, W, H, V; public long Ms_;
        }
        static void Bat(Khung k)
        {
            busy = true; int g = ++gen; var n = nguon; double ktt = Cfg.I.Ktt; int so = ++soTep;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                KetQua kq = null; Exception err = null; var sw = System.Diagnostics.Stopwatch.StartNew();
                try { kq = Ve(k, n, ktt, so, () => g != gen || !On); kq.Ms_ = sw.ElapsedMilliseconds; } catch (Exception ex) { err = ex; }
                Program.Ui(() => KetThuc(g, kq, err));
            });
        }
        static void KetThuc(int g, KetQua kq, Exception err)
        {
            if (g == gen) busy = false;
            if (g != gen || !On) { if (kq != null) XoaTep(kq.File); return; }
            if (err != null || kq == null)
            {
                xet = null; nghiDen = DateTime.Now.AddSeconds(8);
                if (err is HuyException) return; Program.Log(err);
                var gg = nguon as NguonGoogle;
                if (gg != null && gg.KhoaLoi) { nguon = new NguonEsri(); nghiDen = DateTime.MinValue; if (Fm != null) Fm.Msg("⚠ Ảnh Google: " + err.Message + " – tạm dùng ảnh Esri. Kiểm tra khoá ở Bản đồ → " + TEN_TL); return; }
                if (Fm != null) Fm.Msg("⚠ Ảnh vệ tinh nền: " + (err == null ? "không có ảnh" : err.Message) + " – thử lại sau ít giây");
                return;
            }
            object moi;
            try { moi = MsRaster.Gan(kq); }
            catch (Exception ex)
            {
                Program.Log(ex); XoaTep(kq.File); MsRaster.GhiChanDoan(ex); Tat(false);
                MessageBox.Show(Fm, "Không gắn được ảnh vệ tinh vào MicroStation: " + ex.Message + "\n\nĐã ghi tệp chẩn đoán:\n" + MsRaster.TepChanDoan + "\nGửi tệp này cho tác giả để sửa.", "Ảnh vệ tinh nền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int id = MsRaster.Id(moi);
            try { MsRaster.Go(r => MsRaster.LaCuaTa(r) && MsRaster.Id(r) != id); } catch (Exception ex) { Program.Log(ex); }
            if (MsRaster.GoNham)
            {
                MsRaster.GhiChanDoan(new Exception("Raster Manager gỡ cả ảnh raster khác")); Tat(false);
                MessageBox.Show(Fm, "Raster Manager vừa gỡ cả ảnh raster khác của bản vẽ cùng với ảnh vệ tinh nền. Bấm Undo (Ctrl+Z) trong MicroStation để gắn lại ảnh đó.\n\nẢnh vệ tinh nền đã tắt. Tệp chẩn đoán: " + MsRaster.TepChanDoan, "Ảnh vệ tinh nền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MsRaster.VeLai(); cur = kq; XoaTepCu(kq.File);
            if (Fm != null) Fm.Msg("Ảnh vệ tinh nền " + kq.Nguon + " · mức " + kq.Z + (kq.ZMuon > kq.Z ? " (nguồn có ảnh chi tiết nhất mức này ở vùng đang xem, phóng thêm thì nội suy mượt)" : "") + " · " + kq.NO + " ô" + (kq.NTai > 0 ? " (tải mới " + kq.NTai + ")" : " (có sẵn)") + " · " + (kq.Ms_ / 1000.0).ToString("0.0", CultureInfo.GetCultureInfo("vi-VN")) + " giây");
        }
        class HuyException : Exception { public HuyException() : base("đã huỷ") { } }

        // ảnh cho khung nhìn k: lưới VN-2000 trục theo màn hình, mỗi điểm ảnh lấy màu ô ảnh Web Mercator tại đúng vĩ độ, kinh độ của nó
        // dựng ảnh cho một khung (x0, y0, x1, y1 mét VN-2000; w×h điểm ảnh màn hình) – dùng cho kiểm thử
        public static KetQua VeThu(double x0, double y0, double x1, double y1, int w, int h, NguonAnh n, double ktt, int so) { return Ve(new Khung { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, W = w, H = h, V = 1 }, n, ktt, so, () => false); }
        // mỗi lúc chỉ dựng một ảnh (lần dựng cũ bị huỷ thì thoát ngay ở bước kiểm tra kế tiếp)
        static KetQua Ve(Khung k, NguonAnh n, double ktt, int so, Func<bool> huy) { lock (khoaVe) { if (huy()) throw new HuyException(); return Ve0(k, n, ktt, so, huy); } }
        static KetQua Ve0(Khung k, NguonAnh n, double ktt, int so, Func<bool> huy)
        {
            // khung ảnh = khung nhìn nới 8% mỗi phía; độ phân giải = độ nét màn hình
            double w = k.X1 - k.X0, h = k.Y1 - k.Y0, res = w / Math.Max(64, k.W);
            double X0 = k.X0 - w * 0.08, X1 = k.X1 + w * 0.08, Y0 = k.Y0 - h * 0.08, Y1 = k.Y1 + h * 0.08;
            int W = (int)Math.Ceiling((X1 - X0) / res), H = (int)Math.Ceiling((Y1 - Y0) / res);
            if (W > MAXP || H > MAXP) { res *= Math.Max(W, H) / (double)MAXP; W = (int)Math.Ceiling((X1 - X0) / res); H = (int)Math.Ceiling((Y1 - Y0) / res); }
            X1 = X0 + W * res; Y0 = Y1 - H * res;
            var c = Geo.Vn2000ToWgs((X0 + X1) / 2, (Y0 + Y1) / 2, ktt);
            // mức ô ảnh có độ phân giải gần nhất (ưu tiên nét hơn) – Web Mercator: 156543,03392804097·cos(φ) / 2^z mét/điểm ảnh
            double zf = Math.Log(156543.03392804097 * Math.Cos(c[0] * Math.PI / 180) / res, 2);
            int zMuon = Math.Max(1, Math.Min(n.MaxZ, (int)Math.Ceiling(zf - 0.25)));
            // các góc, điểm giữa cạnh → vùng vĩ độ, kinh độ
            double la0 = 90, la1 = -90, lo0 = 180, lo1 = -180;
            for (int i = 0; i <= 4; i++) for (int j = 0; j <= 4; j++)
                {
                    if (i != 0 && i != 4 && j != 0 && j != 4) continue; var p = Geo.Vn2000ToWgs(X0 + (X1 - X0) * i / 4, Y0 + (Y1 - Y0) * j / 4, ktt);
                    la0 = Math.Min(la0, p[0]); la1 = Math.Max(la1, p[0]); lo0 = Math.Min(lo0, p[1]); lo1 = Math.Max(lo1, p[1]);
                }
            int z = Math.Min(zMuon, n.ZCo(la1, la0, lo1, lo0, zMuon, huy));
            int tx0, ty0, tx1, ty1;
            while (true)
            {
                double ax, ay, bx, by; Merc(la1, lo0, z, out ax, out ay); Merc(la0, lo1, z, out bx, out by);
                tx0 = (int)Math.Floor(ax / TS); ty0 = (int)Math.Floor(ay / TS); tx1 = (int)Math.Floor(bx / TS); ty1 = (int)Math.Floor(by / TS);
                if ((tx1 - tx0 + 1) * (ty1 - ty0 + 1) <= MAXO || z <= 1) break; z--;
            }
            if (huy()) throw new HuyException();
            int nx = tx1 - tx0 + 1, ny = ty1 - ty0 + 1, mw = nx * TS, mh = ny * TS; var M = Dem(ref bM, mw * mh); var co = new bool[nx * ny];
            int nTai = 0;
            // tải song song (10 luồng riêng); ô có sẵn trong bộ nhớ dùng lại ngay
            var ds = new List<int[]>(); for (int y = ty0; y <= ty1; y++) for (int x = tx0; x <= tx1; x++) ds.Add(new[] { x, y });
            Exception loi = null;
            SongSong(ds, 10, huy, t =>
            {
                try
                {
                    bool moi; var b = n.O(z, t[0], t[1], out moi); if (moi) Interlocked.Increment(ref nTai);
                    if (b != null && DatO(M, mw, (t[0] - tx0) * TS, (t[1] - ty0) * TS, b, 0, 0, TS)) co[(t[1] - ty0) * nx + (t[0] - tx0)] = true;
                }
                catch (Exception ex) { loi = ex; }
            });
            if (huy()) throw new HuyException();
            if (co.All(x => !x)) { if (loi != null) throw loi; }
            // ô thiếu (nguồn chưa có ảnh ở mức này): lấy ô mức thấp hơn phóng lên
            var thieu = new List<int>(); for (int i = 0; i < co.Length; i++) if (!co[i]) thieu.Add(i);
            SongSong(thieu, 8, huy, i =>
            {
                int x = tx0 + i % nx, y = ty0 + i / nx;
                for (int d = 1; d <= 8 && z - d >= 0; d++)
                {
                    bool moi; byte[] b = null; try { b = n.O(z - d, x >> d, y >> d, out moi); if (moi) Interlocked.Increment(ref nTai); } catch { }
                    if (b == null) continue; int sz = TS >> d, ox = (x & ((1 << d) - 1)) * sz, oy = (y & ((1 << d) - 1)) * sz;
                    if (DatO(M, mw, (x - tx0) * TS, (y - ty0) * TS, b, ox, oy, sz)) { co[i] = true; break; }
                }
                if (!co[i]) LapXam(M, mw, (x - tx0) * TS, (y - ty0) * TS);
            });
            if (huy()) throw new HuyException();
            if (co.All(x => !x)) throw loi ?? new Exception("không tải được ô ảnh nào (kiểm tra mạng)");
            if (huy()) throw new HuyException();
            // nắn về lưới VN-2000: vị trí trên ảnh ghép tính chính xác ở lưới thưa 16 điểm ảnh, nội suy song tuyến bên trong ô lưới
            const int G = 16; int gw = W / G + 2, gh = H / G + 2; var gx = new double[gw * gh]; var gy = new double[gw * gh];
            double ox0 = tx0 * (double)TS, oy0 = ty0 * (double)TS; int zz = z;
            Parallel.For(0, gh, j =>
            {
                for (int i = 0; i < gw; i++)
                {
                    var wg = Geo.Vn2000ToWgs(X0 + i * G * res, Y1 - j * G * res, ktt); double px, py; Merc(wg[0], wg[1], zz, out px, out py);
                    gx[j * gw + i] = px - ox0; gy[j * gw + i] = py - oy0;
                }
            });
            // độ phóng (điểm ảnh màn hình trên một điểm ảnh ô ảnh): ≤ 1,2 song tuyến; tới 3 lần bicubic Catmull-Rom (sắc nét);
            // trên 3 lần bicubic Mitchell và làm mịn trước ảnh ghép để không lộ ô vuông nén JPEG của ảnh gốc (không vỡ, không gãy)
            double phong = G / Math.Max(1e-9, Math.Abs(gx[1] - gx[0]));
            if (phong > 4) LamMin(M, mw, mh, phong > 8 ? 2 : 1);
            double cB = phong > 3 ? 1 / 3.0 : 0, cC = phong > 3 ? 1 / 3.0 : 0.5; bool bic = phong > 1.2;
            var O = Dem(ref bO, W * H);
            Parallel.For(0, H, v =>
            {
                double fv = (v + 0.5) / G; int j = Math.Min(gh - 2, (int)fv); double b = fv - j;
                for (int u = 0; u < W; u++)
                {
                    double fu = (u + 0.5) / G; int i = Math.Min(gw - 2, (int)fu); double a = fu - i; int q = j * gw + i;
                    double sx = (1 - b) * ((1 - a) * gx[q] + a * gx[q + 1]) + b * ((1 - a) * gx[q + gw] + a * gx[q + gw + 1]);
                    double sy = (1 - b) * ((1 - a) * gy[q] + a * gy[q + 1]) + b * ((1 - a) * gy[q + gw] + a * gy[q + gw + 1]);
                    O[v * W + u] = bic ? Cubic(M, mw, mh, sx - 0.5, sy - 0.5, cB, cC) : Bilinear(M, mw, mh, sx - 0.5, sy - 0.5);
                }
            });
            if (huy()) throw new HuyException();
            var file = Path.Combine(Dir, "AnhVeTinh_" + PHIEN + "_" + so + ".jpg");
            using (var bmp = new Bitmap(W, H, PixelFormat.Format32bppRgb))
            {
                var bd = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                try { for (int v = 0; v < H; v++) Marshal.Copy(O, v * W, new IntPtr(bd.Scan0.ToInt64() + (long)v * bd.Stride), W); } finally { bmp.UnlockBits(bd); }
                GhiNguon(bmp, n.BanQuyen(), n is NguonGoogle, (k.X0 - X0) / res, (k.X1 - X0) / res, (Y1 - k.Y0) / res);
                var enc = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
                using (var ep = new EncoderParameters(1)) { ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L); bmp.Save(file, enc, ep); }
            }
            // tệp toạ độ (world file): cỡ điểm ảnh, 0, 0, −cỡ điểm ảnh, toạ độ tâm điểm ảnh trên trái
            File.WriteAllLines(Path.ChangeExtension(file, ".jgw"), new[] { res.ToString("0.############", IV), "0", "0", (-res).ToString("0.############", IV), (X0 + res / 2).ToString("0.######", IV), (Y1 - res / 2).ToString("0.######", IV) });
            return new KetQua { File = file, X0 = X0, Y0 = Y0, X1 = X1, Y1 = Y1, Res = res, Z = z, ZMuon = zMuon, NO = nx * ny, NTai = nTai, W = W, H = H, V = k.V, Nguon = n.Ten };
        }
        // nguồn ảnh (bản quyền) ở góc dưới phải khung nhìn; Google: thêm chữ “Google Maps” góc dưới trái
        static void GhiNguon(Bitmap bmp, string bq, bool google, double vx0, double vx1, double vy1)
        {
            try
            {
                using (var g = Graphics.FromImage(bmp))
                using (var f = new Font("Arial", 8.5f, FontStyle.Regular, GraphicsUnit.Point))
                using (var fb = new Font("Arial", 10f, FontStyle.Bold, GraphicsUnit.Point))
                using (var nen = new SolidBrush(Color.FromArgb(170, 255, 255, 255)))
                {
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; g.SmoothingMode = SmoothingMode.AntiAlias;
                    float x1 = (float)Math.Min(bmp.Width - 2, vx1) - 4, y1 = (float)Math.Min(bmp.Height - 2, vy1) - 3;
                    if (!string.IsNullOrEmpty(bq))
                    {
                        var sz = g.MeasureString(bq, f); var r = new RectangleF(x1 - sz.Width - 4, y1 - sz.Height - 2, sz.Width + 4, sz.Height + 2);
                        g.FillRectangle(nen, r); g.DrawString(bq, f, Brushes.Black, r.X + 2, r.Y + 1);
                    }
                    if (google)
                    {
                        const string s2 = "Google Maps"; float x0 = (float)Math.Max(2, vx0) + 6; var sz = g.MeasureString(s2, fb);
                        using (var vien = new Pen(Color.FromArgb(220, 255, 255, 255), 3f) { LineJoin = LineJoin.Round })
                        using (var gp = new GraphicsPath())
                        {
                            gp.AddString(s2, fb.FontFamily, (int)FontStyle.Bold, g.DpiY * fb.SizeInPoints / 72f, new PointF(x0, y1 - sz.Height), StringFormat.GenericDefault);
                            g.DrawPath(vien, gp); g.FillPath(Brushes.DimGray, gp);
                        }
                    }
                }
            }
            catch { }
        }
        public static void Merc(double lat, double lon, int z, out double px, out double py)
        {
            double n = TS * Math.Pow(2, z), s = Math.Sin(Math.Max(-85.05, Math.Min(85.05, lat)) * Math.PI / 180);
            px = (lon + 180) / 360 * n; py = (0.5 - Math.Log((1 + s) / (1 - s)) / (4 * Math.PI)) * n;
        }
        // đặt ô ảnh (JPEG) vào ảnh ghép: lấy vùng (ox, oy, sz) của ô, phóng lên đủ 256 điểm ảnh
        static bool DatO(int[] M, int mw, int dx, int dy, byte[] b, int ox, int oy, int sz)
        {
            try
            {
                using (var ms = new MemoryStream(b)) using (var im = Image.FromStream(ms, false, false)) using (var bm = new Bitmap(TS, TS, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bm))
                    {
                        g.InterpolationMode = sz == TS ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.CompositingMode = CompositingMode.SourceCopy;
                        double kk = im.Width / (double)TS;
                        using (var ia = new ImageAttributes()) { ia.SetWrapMode(WrapMode.TileFlipXY); g.DrawImage(im, new Rectangle(0, 0, TS, TS), (float)(ox * kk), (float)(oy * kk), (float)(sz * kk), (float)(sz * kk), GraphicsUnit.Pixel, ia); }
                    }
                    var bd = bm.LockBits(new Rectangle(0, 0, TS, TS), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try { for (int y = 0; y < TS; y++) Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), M, (dy + y) * mw + dx, TS); } finally { bm.UnlockBits(bd); }
                }
                return true;
            }
            catch { return false; }
        }
        // chạy f cho từng phần tử trên n luồng riêng – việc tải mạng phải chờ, ThreadPool của .NET chỉ thêm luồng chậm (vài luồng mỗi giây)
        // nên lần tải đầu sẽ chậm; luồng riêng chạy ngay đủ n luồng. Lỗi trong f không bao giờ làm đổ MicroStation.
        static void SongSong<T>(IList<T> ds, int n, Func<bool> huy, Action<T> f)
        {
            if (ds.Count == 0) return; int vt = -1; n = Math.Max(1, Math.Min(n, ds.Count));
            ThreadStart chay = () =>
            {
                try { int j; while (!huy() && (j = Interlocked.Increment(ref vt)) < ds.Count) { try { f(ds[j]); } catch (Exception ex) { Program.Log(ex); } } }
                catch { }
            };
            var ts = new List<Thread>();
            for (int t = 1; t < n; t++) { try { var th = new Thread(chay, 512 * 1024) { IsBackground = true, Name = "AnhVeTinh" }; th.Start(); ts.Add(th); } catch { break; } }
            chay();
            foreach (var th in ts) th.Join();
        }
        static void LapXam(int[] M, int mw, int dx, int dy) { int c = unchecked((int)0xFFD8D8D8); for (int y = 0; y < TS; y++) for (int x = 0; x < TS; x++) M[(dy + y) * mw + dx + x] = c; }
        static int Px(int[] M, int mw, int mh, int x, int y) { if (x < 0) x = 0; else if (x >= mw) x = mw - 1; if (y < 0) y = 0; else if (y >= mh) y = mh - 1; return M[y * mw + x]; }
        static int Bilinear(int[] M, int mw, int mh, double x, double y)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y); double a = x - x0, b = y - y0;
            int p00 = Px(M, mw, mh, x0, y0), p10 = Px(M, mw, mh, x0 + 1, y0), p01 = Px(M, mw, mh, x0, y0 + 1), p11 = Px(M, mw, mh, x0 + 1, y0 + 1);
            double w00 = (1 - a) * (1 - b), w10 = a * (1 - b), w01 = (1 - a) * b, w11 = a * b;
            int r = (int)(((p00 >> 16) & 255) * w00 + ((p10 >> 16) & 255) * w10 + ((p01 >> 16) & 255) * w01 + ((p11 >> 16) & 255) * w11 + 0.5);
            int g = (int)(((p00 >> 8) & 255) * w00 + ((p10 >> 8) & 255) * w10 + ((p01 >> 8) & 255) * w01 + ((p11 >> 8) & 255) * w11 + 0.5);
            int bl = (int)((p00 & 255) * w00 + (p10 & 255) * w10 + (p01 & 255) * w01 + (p11 & 255) * w11 + 0.5);
            return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | bl;
        }
        // nội suy bậc ba họ (B, C): Catmull-Rom (0, ½) sắc nét; Mitchell (⅓, ⅓) mượt, không gợn
        static void Wc(double t, double B, double C, out double w0, out double w1, out double w2, out double w3)
        {
            w0 = K(1 + t, B, C); w1 = K(t, B, C); w2 = K(1 - t, B, C); w3 = K(2 - t, B, C); double s = w0 + w1 + w2 + w3; w0 /= s; w1 /= s; w2 /= s; w3 /= s;
        }
        static double K(double x, double B, double C)
        {
            x = Math.Abs(x); double x2 = x * x, x3 = x2 * x;
            if (x < 1) return ((12 - 9 * B - 6 * C) * x3 + (-18 + 12 * B + 6 * C) * x2 + (6 - 2 * B)) / 6;
            if (x < 2) return ((-B - 6 * C) * x3 + (6 * B + 30 * C) * x2 + (-12 * B - 48 * C) * x + (8 * B + 24 * C)) / 6;
            return 0;
        }
        static int Cubic(int[] M, int mw, int mh, double x, double y, double B, double C)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y); double wx0, wx1, wx2, wx3, wy0, wy1, wy2, wy3;
            Wc(x - x0, B, C, out wx0, out wx1, out wx2, out wx3); Wc(y - y0, B, C, out wy0, out wy1, out wy2, out wy3);
            double r = 0, g = 0, bl = 0;
            for (int j = 0; j < 4; j++)
            {
                int yy = y0 - 1 + j; double wy = j == 0 ? wy0 : j == 1 ? wy1 : j == 2 ? wy2 : wy3;
                int p0 = Px(M, mw, mh, x0 - 1, yy), p1 = Px(M, mw, mh, x0, yy), p2 = Px(M, mw, mh, x0 + 1, yy), p3 = Px(M, mw, mh, x0 + 2, yy);
                r += wy * (((p0 >> 16) & 255) * wx0 + ((p1 >> 16) & 255) * wx1 + ((p2 >> 16) & 255) * wx2 + ((p3 >> 16) & 255) * wx3);
                g += wy * (((p0 >> 8) & 255) * wx0 + ((p1 >> 8) & 255) * wx1 + ((p2 >> 8) & 255) * wx2 + ((p3 >> 8) & 255) * wx3);
                bl += wy * ((p0 & 255) * wx0 + (p1 & 255) * wx1 + (p2 & 255) * wx2 + (p3 & 255) * wx3);
            }
            return unchecked((int)0xFF000000) | (C8(r) << 16) | (C8(g) << 8) | C8(bl);
        }
        // làm mịn ảnh ghép (lọc [1 2 1]/4 hai chiều, lặp n lần) – xoá gờ ô vuông nén JPEG khi phóng rất lớn
        static void LamMin(int[] M, int mw, int mh, int n)
        {
            var T = Dem(ref bT, mw * mh);
            for (int k = 0; k < n; k++)
            {
                Parallel.For(0, mh, y => { int o = y * mw; for (int x = 0; x < mw; x++) { int a = M[o + Math.Max(0, x - 1)], b = M[o + x], c = M[o + Math.Min(mw - 1, x + 1)]; T[o + x] = Tron(a, b, c); } });
                Parallel.For(0, mh, y => { int o = y * mw, oa = Math.Max(0, y - 1) * mw, oc = Math.Min(mh - 1, y + 1) * mw; for (int x = 0; x < mw; x++) M[o + x] = Tron(T[oa + x], T[o + x], T[oc + x]); });
            }
        }
        static int Tron(int a, int b, int c)
        {
            int r = (((a >> 16) & 255) + 2 * ((b >> 16) & 255) + ((c >> 16) & 255) + 2) >> 2, g = (((a >> 8) & 255) + 2 * ((b >> 8) & 255) + ((c >> 8) & 255) + 2) >> 2, bl = ((a & 255) + 2 * (b & 255) + (c & 255) + 2) >> 2;
            return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | bl;
        }
        static int C8(double v) { int i = (int)(v + 0.5); return i < 0 ? 0 : i > 255 ? 255 : i; }

        // ---------- tệp tạm ----------
        static void XoaTep(string f) { try { if (f != null) { File.Delete(f); File.Delete(Path.ChangeExtension(f, ".jgw")); } } catch { } }
        // xoá ảnh tạm cũ của lần chạy này, giữ ảnh đang hiện và 2 ảnh trước đó (phím Undo của MicroStation có thể gắn lại ảnh vừa gỡ);
        // ảnh của lần chạy khác (MicroStation khác đang mở cùng lúc) để nguyên, trừ ảnh sót đã quá 12 giờ
        static void XoaTepCu(string giu)
        {
            try
            {
                var ds = Directory.GetFiles(Dir, "AnhVeTinh_*.jpg").OrderByDescending(f => File.GetLastWriteTimeUtc(f)).ToList();
                var giuLai = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (giu != null) { giuLai.Add(Path.GetFullPath(giu)); foreach (var f in ds) { if (giuLai.Count >= 3) break; if (CuaPhien(f)) giuLai.Add(Path.GetFullPath(f)); } }
                foreach (var f in ds) if (!giuLai.Contains(Path.GetFullPath(f)) && (CuaPhien(f) || Cu(f))) XoaTep(f);
                foreach (var f in Directory.GetFiles(Dir, "AnhVeTinh_*.jgw")) if (!File.Exists(Path.ChangeExtension(f, ".jpg"))) try { File.Delete(f); } catch { }
            }
            catch { }
        }
        static bool CuaPhien(string f) { return Path.GetFileName(f).StartsWith("AnhVeTinh_" + PHIEN + "_", StringComparison.OrdinalIgnoreCase); }
        static bool Cu(string f) { try { return (DateTime.UtcNow - File.GetLastWriteTimeUtc(f)).TotalHours > 12; } catch { return false; } }

        // ================= NGUỒN ẢNH =================
        public abstract class NguonAnh
        {
            public abstract string Ten { get; }
            public abstract int MaxZ { get; }
            // mức chi tiết nhất nguồn có ở vùng (bắc, nam, đông, tây), không quá zMuon
            public abstract int ZCo(double n, double s, double e, double w, int zMuon, Func<bool> huy);
            // ô ảnh z/x/y (JPEG); null: vùng chưa có ảnh ở mức này
            public abstract byte[] O(int z, int x, int y, out bool moi);
            public abstract string BanQuyen();
        }
        // Esri World Imagery – không cần khoá; ô nào có ảnh đọc bằng bản đồ ô (tilemap) của dịch vụ, theo khối 16×16 ô
        public class NguonEsri : NguonAnh
        {
            const string U = "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/";
            public override string Ten { get { return "Esri World Imagery"; } }
            public override int MaxZ { get { return 21; } }
            static readonly Dictionary<string, bool> coAnh = new Dictionary<string, bool>();
            static readonly Dictionary<string, object> khoaKhoi = new Dictionary<string, object>();   // mỗi khối 16×16 ô một khoá: các khối khác nhau tải song song
            public static bool CoAnh(int z, int x, int y)
            {
                var k = z + "/" + x + "/" + y; bool v;
                lock (coAnh) if (coAnh.TryGetValue(k, out v)) return v;
                int bx = x & ~15, by = y & ~15; object khoa; var kk0 = z + "/" + bx + "/" + by;
                lock (khoaKhoi) { if (khoaKhoi.Count > 20000) khoaKhoi.Clear(); if (!khoaKhoi.TryGetValue(kk0, out khoa)) khoaKhoi[kk0] = khoa = new object(); }
                lock (khoa)
                {
                    lock (coAnh) if (coAnh.TryGetValue(k, out v)) return v;
                    int st; var b = Http.Get(U + "tilemap/" + z + "/" + by + "/" + bx + "/16/16?f=json", out st);
                    var tam = new Dictionary<string, bool>();
                    if (b != null)
                    {
                        var s = Encoding.UTF8.GetString(b); var m = Regex.Match(s, "\"data\"\\s*:\\s*\\[([^\\]]*)\\]");
                        var L = Regex.Match(s, "\"left\"\\s*:\\s*(\\d+)"); var T = Regex.Match(s, "\"top\"\\s*:\\s*(\\d+)"); var Wd = Regex.Match(s, "\"width\"\\s*:\\s*(\\d+)");
                        if (m.Success && !Regex.IsMatch(s, "\"valid\"\\s*:\\s*false"))
                        {
                            int lx = L.Success ? int.Parse(L.Groups[1].Value) : bx, ty = T.Success ? int.Parse(T.Groups[1].Value) : by, ww = Math.Max(1, Wd.Success ? int.Parse(Wd.Groups[1].Value) : 16);
                            var d = m.Groups[1].Value.Split(','); for (int i = 0; i < d.Length; i++) tam[z + "/" + (lx + i % ww) + "/" + (ty + i / ww)] = d[i].Trim() == "1";
                        }
                    }
                    lock (coAnh)
                    {
                        if (coAnh.Count > 300000) coAnh.Clear();
                        for (int yy = 0; yy < 16; yy++) for (int xx = 0; xx < 16; xx++) { var kk = z + "/" + (bx + xx) + "/" + (by + yy); bool vv; coAnh[kk] = tam.TryGetValue(kk, out vv) ? vv : true; }
                        return coAnh[k];
                    }
                }
            }
            public override int ZCo(double n, double s, double e, double w, int zMuon, Func<bool> huy)
            {
                for (int z = zMuon; z >= Math.Max(1, zMuon - 10); z--)
                {
                    if (huy()) throw new HuyException();
                    double ax, ay, bx, by; Merc(n, w, z, out ax, out ay); Merc(s, e, z, out bx, out by);
                    // 9 điểm mẫu: giữa, 4 góc, 4 điểm giữa cạnh của vùng – hỏi song song
                    int co = 0, tong = 9, zz = z;
                    SongSong(Enumerable.Range(0, 9).ToList(), 9, huy, q =>
                    {
                        int i = q % 3, j = q / 3; double px = ax + (bx - ax) * (0.1 + 0.4 * i), py = ay + (by - ay) * (0.1 + 0.4 * j);
                        if (CoAnh(zz, (int)(px / TS), (int)(py / TS))) Interlocked.Increment(ref co);
                    });
                    if (co * 3 >= tong * 2) return z;
                }
                return Math.Max(1, zMuon - 10);
            }
            public override byte[] O(int z, int x, int y, out bool moi)
            {
                moi = false; var k = "esri/" + z + "/" + x + "/" + y; byte[] b; if (BoNho.Lay(k, out b)) return b;
                if (!CoAnh(z, x, y)) return null;
                int st; b = Http.Get(U + "tile/" + z + "/" + y + "/" + x, out st); moi = true;
                if (b == null) { if (st == 404) BoNho.Dat(k, null, 3600); return null; }
                BoNho.Dat(k, b, 6 * 3600); return b;
            }
            public override string BanQuyen() { return "Esri, Maxar, Earthstar Geographics, GIS User Community"; }
        }
        // Google Maps – Map Tiles API (ảnh vệ tinh 2D) bằng khoá của người dùng: phiên làm việc, ô ảnh, mức có ảnh và dòng bản quyền theo khung nhìn
        public class NguonGoogle : NguonAnh
        {
            const string U = "https://tile.googleapis.com/";
            readonly string key; string ses; DateTime hetHan = DateTime.MinValue; public volatile bool KhoaLoi;
            string bq = "", vpKey = ""; int vpZ = 22;
            public NguonGoogle(string k) { key = k; }
            public override string Ten { get { return "Google Maps"; } }
            public override int MaxZ { get { return 22; } }
            string Ses()
            {
                lock (this)
                {
                    if (ses != null && DateTime.UtcNow < hetHan) return ses;
                    int st; string loi; var b = Http.Post(U + "v1/createSession?key=" + Uri.EscapeDataString(key), "{\"mapType\":\"satellite\",\"language\":\"vi-VN\",\"region\":\"VN\",\"imageFormat\":\"jpeg\"}", out st, out loi);
                    if (b == null) { if (st == 400 || st == 401 || st == 403) KhoaLoi = true; throw new Exception("Google từ chối khoá (" + (loi ?? ("mã " + st)) + ")"); }
                    var s = Encoding.UTF8.GetString(b); var m = Regex.Match(s, "\"session\"\\s*:\\s*\"([^\"]+)\""); if (!m.Success) throw new Exception("Google không trả phiên làm việc");
                    ses = m.Groups[1].Value; var ex = Regex.Match(s, "\"expiry\"\\s*:\\s*\"?(\\d+)");
                    hetHan = ex.Success ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(double.Parse(ex.Groups[1].Value, IV)).AddMinutes(-30) : DateTime.UtcNow.AddDays(7);
                    return ses;
                }
            }
            public override int ZCo(double n, double s, double e, double w, int zMuon, Func<bool> huy)
            {
                var vk = string.Format(IV, "{0}|{1:0.000}|{2:0.000}", zMuon, (n + s) / 2, (e + w) / 2);
                if (vk == vpKey) return Math.Min(zMuon, vpZ);
                int st; string loi; var b = Http.Get(string.Format(IV, U + "tile/v1/viewport?session={0}&key={1}&zoom={2}&north={3:0.######}&south={4:0.######}&east={5:0.######}&west={6:0.######}", Uri.EscapeDataString(Ses()), Uri.EscapeDataString(key), zMuon, n, s, e, w), out st, out loi);
                int zc = zMuon;
                if (b != null)
                {
                    var t = Encoding.UTF8.GetString(b); var c = Regex.Match(t, "\"copyright\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); bq = c.Success ? Regex.Unescape(c.Groups[1].Value) : "";
                    double la = (n + s) / 2, lo = (e + w) / 2; int best = -1, mn = 99;
                    foreach (Match r in Regex.Matches(t, "\\{[^{}]*\"maxZoom\"\\s*:\\s*(\\d+)[^{}]*\\}"))
                    {
                        int mz = int.Parse(r.Groups[1].Value); mn = Math.Min(mn, mz);
                        double rn = Num(r.Value, "north"), rs = Num(r.Value, "south"), re = Num(r.Value, "east"), rw = Num(r.Value, "west");
                        if (!double.IsNaN(rn) && la <= rn && la >= rs && lo <= re && lo >= rw) best = Math.Max(best, mz);
                    }
                    if (best < 0 && mn < 99) best = mn; if (best > 0) zc = Math.Min(zMuon, best);
                }
                else if (st == 400 || st == 401 || st == 403) { KhoaLoi = true; throw new Exception("Google từ chối khoá (" + (loi ?? ("mã " + st)) + ")"); }
                vpKey = vk; vpZ = zc; return zc;
            }
            static double Num(string s, string k) { var m = Regex.Match(s, "\"" + k + "\"\\s*:\\s*(-?[0-9.eE+-]+)"); double v; return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, IV, out v) ? v : double.NaN; }
            public override byte[] O(int z, int x, int y, out bool moi)
            {
                moi = false; var k = "gg/" + z + "/" + x + "/" + y; byte[] b; if (BoNho.Lay(k, out b)) return b;
                int st, tuoi; string loi; b = Http.Get(U + "v1/2dtiles/" + z + "/" + x + "/" + y + "?session=" + Uri.EscapeDataString(Ses()) + "&key=" + Uri.EscapeDataString(key), out st, out loi, out tuoi); moi = true;
                if (b == null)
                {
                    if (st == 404) { BoNho.Dat(k, null, 1800); return null; }
                    if (st == 401 || st == 403) { KhoaLoi = true; throw new Exception("Google từ chối khoá (" + (loi ?? ("mã " + st)) + ")"); }
                    if (st == 400 || st == 410) lock (this) ses = null;
                    return null;
                }
                if (tuoi != 0) BoNho.Dat(k, b, tuoi < 0 ? 3600 : tuoi);   // giữ trong bộ nhớ theo Cache-Control của Google (no-store: không giữ)
                return b;
            }
            public override string BanQuyen() { return string.IsNullOrEmpty(bq) ? "Google" : bq; }
        }
        // bộ nhớ đệm ô ảnh (chỉ trong bộ nhớ, tối đa 2000 ô và 40 MB – MicroStation 32 bit, có hạn dùng)
        static class BoNho
        {
            static readonly Dictionary<string, KeyValuePair<byte[], DateTime>> d = new Dictionary<string, KeyValuePair<byte[], DateTime>>();
            static readonly LinkedList<string> thuTu = new LinkedList<string>();
            static long tong; const long MAXB = 40L << 20;
            public static bool Lay(string k, out byte[] b)
            {
                lock (d)
                {
                    KeyValuePair<byte[], DateTime> v; b = null; if (!d.TryGetValue(k, out v)) return false;
                    if (DateTime.UtcNow > v.Value) return false;          // hết hạn: lần tải sau ghi đè
                    b = v.Key; return true;
                }
            }
            public static void Dat(string k, byte[] b, int giay)
            {
                lock (d)
                {
                    KeyValuePair<byte[], DateTime> c;
                    if (d.TryGetValue(k, out c)) { if (c.Key != null) tong -= c.Key.Length; } else thuTu.AddLast(k);
                    d[k] = new KeyValuePair<byte[], DateTime>(b, DateTime.UtcNow.AddSeconds(giay)); if (b != null) tong += b.Length;
                    while (thuTu.Count > 0 && (thuTu.Count > 2000 || tong > MAXB))
                    {
                        var f = thuTu.First.Value; thuTu.RemoveFirst();
                        if (d.TryGetValue(f, out c)) { if (c.Key != null) tong -= c.Key.Length; d.Remove(f); }
                    }
                }
            }
        }
        // ---------- tải qua mạng (TLS 1.2) ----------
        static class Http
        {
            static Http() { try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | (SecurityProtocolType)768; } catch { } try { ServicePointManager.DefaultConnectionLimit = Math.Max(ServicePointManager.DefaultConnectionLimit, 16); ServicePointManager.Expect100Continue = false; } catch { } }
            public static byte[] Get(string url, out int st) { string l; int t; return Get(url, out st, out l, out t); }
            public static byte[] Get(string url, out int st, out string loi) { int t; return Get(url, out st, out loi, out t); }
            // tuoi: số giây được giữ theo Cache-Control (-1: không ghi, 0: không được giữ)
            public static byte[] Get(string url, out int st, out string loi, out int tuoi)
            {
                st = 0; loi = null; tuoi = -1;
                for (int lan = 0; lan < 2; lan++)
                {
                    try
                    {
                        var rq = (HttpWebRequest)WebRequest.Create(url); rq.Timeout = 15000; rq.ReadWriteTimeout = 15000; rq.UserAgent = "BienTapBDDC/" + CapNhat.PhienBan + " (MicroStation)"; rq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                        using (var rs = (HttpWebResponse)rq.GetResponse()) using (var s = rs.GetResponseStream()) using (var ms = new MemoryStream())
                        {
                            st = (int)rs.StatusCode; s.CopyTo(ms); var cc = rs.Headers["Cache-Control"] ?? "";
                            if (cc.IndexOf("no-store", StringComparison.OrdinalIgnoreCase) >= 0) tuoi = 0; else { var m = Regex.Match(cc, "max-age=(\\d+)"); if (m.Success) tuoi = (int)Math.Min(int.MaxValue, long.Parse(m.Groups[1].Value)); }
                            return ms.ToArray();
                        }
                    }
                    catch (WebException ex)
                    {
                        var r = ex.Response as HttpWebResponse; st = r != null ? (int)r.StatusCode : 0; loi = DocLoi(r) ?? ex.Message;
                        if (r != null) { r.Close(); if (st != 429 && st < 500) return null; }
                        if (lan == 0) Thread.Sleep(st == 429 ? 1500 : 300);
                    }
                    catch (IOException ex) { st = 0; loi = ex.Message; if (lan == 0) Thread.Sleep(300); }   // đứt kết nối giữa chừng: thử lại một lần
                }
                return null;
            }
            public static byte[] Post(string url, string json, out int st, out string loi)
            {
                st = 0; loi = null;
                try
                {
                    var rq = (HttpWebRequest)WebRequest.Create(url); rq.Method = "POST"; rq.ContentType = "application/json"; rq.Timeout = 15000; rq.UserAgent = "BienTapBDDC/" + CapNhat.PhienBan + " (MicroStation)";
                    var body = Encoding.UTF8.GetBytes(json); rq.ContentLength = body.Length; using (var s = rq.GetRequestStream()) s.Write(body, 0, body.Length);
                    using (var rs = (HttpWebResponse)rq.GetResponse()) using (var s = rs.GetResponseStream()) using (var ms = new MemoryStream()) { st = (int)rs.StatusCode; s.CopyTo(ms); return ms.ToArray(); }
                }
                catch (WebException ex) { var r = ex.Response as HttpWebResponse; st = r != null ? (int)r.StatusCode : 0; loi = DocLoi(r) ?? ex.Message; if (r != null) r.Close(); return null; }
            }
            static string DocLoi(HttpWebResponse r)
            {
                try
                {
                    if (r == null) return null;
                    using (var s = r.GetResponseStream()) using (var sr = new StreamReader(s, Encoding.UTF8))
                    {
                        var t = sr.ReadToEnd(); var m = Regex.Match(t, "\"message\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); return m.Success ? Regex.Unescape(m.Groups[1].Value) : "mã " + (int)r.StatusCode;
                    }
                }
                catch { return null; }
            }
        }
    }

    // ================= RASTER MANAGER (MicroStation V8i) – gọi muộn (late binding), chạy được với bộ giao diện COM của từng máy =================
    public static class MsRaster
    {
        const BindingFlags GP = BindingFlags.GetProperty, SP = BindingFlags.SetProperty, IM = BindingFlags.InvokeMethod;
        static readonly CultureInfo IV = CultureInfo.InvariantCulture;
        static Assembly Asm { get { return typeof(BCOM.Application).Assembly; } }
        public static object Get(object o, string n, params object[] a) { return o.GetType().InvokeMember(n, GP, null, o, a); }
        public static void Set(object o, string n, object v) { o.GetType().InvokeMember(n, SP, null, o, new[] { v }); }
        public static object Call(object o, string n, params object[] a) { return o.GetType().InvokeMember(n, IM, null, o, a); }
        static readonly StringBuilder nk = new StringBuilder();
        public static void Ghi(string s) { lock (nk) { if (nk.Length > 20000) nk.Remove(0, 10000); nk.AppendLine(DateTime.Now.ToString("HH:mm:ss ", IV) + s); } }
        static bool TrySet(object o, string n, object v) { try { Set(o, n, v); return true; } catch (Exception ex) { Ghi("đặt " + n + " = " + v + ": " + ex.Message); return false; } }
        static object Rm() { return Get(Ms.App, "RasterManager"); }

        // kiểu giao diện (interface) trong bộ giao diện COM của MicroStation theo tên, dùng khi tham số là cấu trúc Point3d / Point2d hoặc tham chiếu
        static Type KieuTen(string n) { try { return Asm.GetType("Bentley.Interop.MicroStationDGN." + n, false); } catch { return null; } }
        static readonly Dictionary<string, string[]> KIEU = new Dictionary<string, string[]> {
            { "View", new[] { "View", "_View" } }, { "Geo", new[] { "GeoReferenceInformation", "_GeoReferenceInformation" } }, { "Rasters", new[] { "Rasters", "_Rasters" } } };
        static MethodInfo PhuongThuc(object o, string nhom, string name)
        {
            string[] ds; if (!KIEU.TryGetValue(nhom, out ds)) ds = new[] { nhom };
            foreach (var n in ds)
            {
                var t = KieuTen(n); if (t == null) continue;
                try { if (!t.IsInstanceOfType(o)) continue; } catch { continue; }
                var m = t.GetMethods().FirstOrDefault(x => x.Name == name); if (m != null) return m;
                foreach (var b in t.GetInterfaces()) { m = b.GetMethods().FirstOrDefault(x => x.Name == name); if (m != null) return m; }
            }
            return null;
        }
        public static bool Bool(object o, string n)
        {
            try { var m = PhuongThuc(o, "View", "get_" + n); if (m != null) return Convert.ToBoolean(m.Invoke(o, null)); } catch { }
            try { return Convert.ToBoolean(Get(o, n)); } catch { return false; }
        }
        static bool XY(object p, out double x, out double y)
        {
            x = y = 0; if (p == null) return false;
            var t = p.GetType(); var fx = t.GetField("X"); var fy = t.GetField("Y"); if (fx == null || fy == null) return false;
            x = Convert.ToDouble(fx.GetValue(p)); y = Convert.ToDouble(fy.GetValue(p)); return true;
        }
        // toạ độ điểm (Point3d / Point2d) đọc qua giao diện có kiểu
        public static bool Pt(object o, string getter, out double x, out double y) { return Pt(o, "View", getter, out x, out y); }
        static bool Pt(object o, string nhom, string getter, out double x, out double y)
        {
            x = y = 0;
            try { var m = PhuongThuc(o, nhom, getter); return m != null && XY(m.Invoke(o, null), out x, out y); } catch (Exception ex) { Ghi(getter + ": " + ex.Message); return false; }
        }
        // điểm (mét) → vị trí điểm ảnh trên màn hình
        public static bool Pixel(BCOM.View v, double x, double y, out double px, out double py)
        {
            px = py = 0;
            try { var m = PhuongThuc(v, "View", "GetPixelPosition"); if (m == null) return false; var a = new object[] { Ms.P(x, y), false }; return XY(m.Invoke(v, a), out px, out py); }
            catch { return false; }
        }
        // giá trị kiểu liệt kê của MicroStation theo phần tên (vd …Background)
        public static bool GiaTri(string kieu, Func<string, bool> chon, out int v)
        {
            v = 0;
            try { var t = KieuTen(kieu); if (t == null || !t.IsEnum) return false; foreach (var n in Enum.GetNames(t)) if (chon(n.ToLowerInvariant())) { v = Convert.ToInt32(Enum.Parse(t, n)); return true; } } catch { }
            return false;
        }

        // ---------- danh sách ảnh raster đang gắn ----------
        public static List<object> DanhSach(object rs)
        {
            var o = new List<object>(); int n = 0; try { n = Convert.ToInt32(Get(rs, "Count")); } catch { return o; }
            for (int i = 1; i <= n; i++) { try { o.Add(Get(rs, "Item", i)); } catch { } }
            if (o.Count < n) { o.Clear(); for (int i = 0; i < n; i++) { try { o.Add(Get(rs, "Item", i)); } catch { } } }
            return o;
        }
        public static int Id(object r) { try { return Convert.ToInt32(Get(r, "ID")); } catch { return -1; } }
        static string TenTep(object r) { try { var ri = Get(r, "RasterInformation"); var fn = Convert.ToString(Get(ri, "FullName")); return string.IsNullOrEmpty(fn) ? Convert.ToString(Get(ri, "Name")) ?? "" : fn; } catch { return ""; } }
        public static bool LaCuaTa(object r)
        {
            try { var ei = Get(r, "ExtendedInformation"); if (Convert.ToString(Get(ei, "LogicalName")) == AnhNen.LOGIC) return true; } catch { }
            var ten = Path.GetFileName(TenTep(r)); return ten.StartsWith("AnhVeTinh_", StringComparison.OrdinalIgnoreCase) && ten.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase);
        }

        // ---------- gắn ảnh: định vị theo tệp toạ độ .jgw, lớp nền, hiện ở cả 8 khung nhìn, không in ----------
        public static object Gan(AnhNen.KetQua kq)
        {
            var rm = Rm(); var rs = Get(rm, "Rasters"); bool tu = true;
            try { tu = Convert.ToBoolean(Get(rm, "IsUpdateViewAutomatic")); if (tu) Set(rm, "IsUpdateViewAutomatic", false); } catch { }
            try
            {
                object r = null; int am;
                // chế độ gắn theo toạ độ của ảnh (không bắt chạm đặt bằng tay) nếu bộ giao diện có tên kiểu đó; không thì để mặc định
                if (GiaTri("MsdAttachMode", n => n.Contains("georef") || (n.Contains("geo") && !n.Contains("interactive")), out am)) { try { r = Call(rs, "Attach", kq.File, (byte)0xFF, am); } catch (Exception ex) { Ghi("Attach(geo): " + ex.Message); r = null; } }
                if (r == null) r = Call(rs, "Attach", kq.File);
                if (r == null) r = DanhSach(rs).FirstOrDefault(x => TenTep(x).Equals(kq.File, StringComparison.OrdinalIgnoreCase));
                if (r == null) throw new Exception("Raster Manager không trả về ảnh vừa gắn");
                try { var ei = Get(r, "ExtendedInformation"); TrySet(ei, "LogicalName", AnhNen.LOGIC); TrySet(ei, "Description", "Anh ve tinh nen - Bien tap BDDC (tu go khi tat)"); TrySet(ei, "IsSelected", false); } catch (Exception ex) { Ghi("ExtendedInformation: " + ex.Message); }
                try
                {
                    var ri = Get(r, "RenderingInformation"); int pl;
                    if (GiaTri("MsdRasterDisplayPriorityPlane", n => n.Contains("background") || n.EndsWith("back"), out pl)) TrySet(ri, "DisplayPriorityPlane", pl);
                    TrySet(ri, "Plot", false); for (int i = 1; i <= 8; i++) TrySet(ri, "View" + i, true);
                }
                catch (Exception ex) { Ghi("RenderingInformation: " + ex.Message); }
                int ob; if (GiaTri("MsdRasterDisplayOrderCommand", n => n.Contains("toback") || n.Contains("bottom") || (n.EndsWith("back") && !n.Contains("ward")), out ob)) try { Call(rs, "ChangeDisplayOrder", r, ob); } catch (Exception ex) { Ghi("ChangeDisplayOrder: " + ex.Message); }
                KiemViTri(r, rs, kq);
                return r;
            }
            finally { try { if (tu) Set(rm, "IsUpdateViewAutomatic", true); } catch { } }
        }
        // kiểm tra ảnh nằm đúng toạ độ (gốc, kích thước theo tệp .jgw); sai thì đặt lại gốc, kích thước và xác nhận bằng cách tìm ảnh tại các điểm trong khung nhìn
        static void KiemViTri(object r, object rs, AnhNen.KetQua kq)
        {
            double w = kq.X1 - kq.X0, h = kq.Y1 - kq.Y0, tol = Math.Max(w, h) * 0.01;
            object gi; try { gi = Get(r, "GeoReferenceInformation"); } catch (Exception ex) { Ghi("GeoReferenceInformation: " + ex.Message); return; }
            double ox, oy, ex_, ey; bool coO = Pt(gi, "Geo", "get_Origin", out ox, out oy), coE = Pt(gi, "Geo", "get_Extent", out ex_, out ey);
            Ghi(string.Format(IV, "gắn {0}: gốc {1:0.###},{2:0.###} kích thước {3:0.###}×{4:0.###} (mong {5:0.###},{6:0.###} / {7:0.###}×{8:0.###})", Path.GetFileName(kq.File), ox, oy, ex_, ey, kq.X0, kq.Y0, w, h));
            if (!coO || !coE) return;
            if (Math.Abs(Math.Abs(ex_) - w) < tol && Math.Abs(Math.Abs(ey) - h) < tol && Math.Abs(ox - kq.X0) < tol && (Math.Abs(oy - kq.Y0) < tol || Math.Abs(oy - kq.Y1) < tol)) return;
            // tệp toạ độ không được dùng: đặt trực tiếp kích thước, gốc (thử gốc dưới trái rồi trên trái)
            try
            {
                var p2 = KieuTen("Point2d"); if (p2 != null) { var e = Activator.CreateInstance(p2); p2.GetField("X").SetValue(e, w); p2.GetField("Y").SetValue(e, h); Dat(gi, "set_Extent", e); }
                foreach (var y0 in new[] { kq.Y0, kq.Y1 })
                {
                    Dat(gi, "set_Origin", Ms.P(kq.X0, y0));
                    if (Thay(rs, r, (kq.X0 + kq.X1) / 2, (kq.Y0 + kq.Y1) / 2, kq.V) && Thay(rs, r, kq.X0 + w * 0.15, kq.Y1 - h * 0.15, kq.V) && Thay(rs, r, kq.X1 - w * 0.15, kq.Y0 + h * 0.15, kq.V)) { Ghi("đặt gốc " + (y0 == kq.Y0 ? "dưới trái" : "trên trái") + ": đúng"); return; }
                }
                Ghi("đặt lại gốc, kích thước: chưa xác nhận được vị trí");
            }
            catch (Exception ex) { Ghi("đặt vị trí: " + ex.Message); }
        }
        static void Dat(object o, string setter, object v) { var m = PhuongThuc(o, "Geo", setter); if (m == null) throw new MissingMethodException(setter); m.Invoke(o, new[] { v }); }
        static bool Thay(object rs, object r, double x, double y, int view)
        {
            try { var m = PhuongThuc(rs, "Rasters", "FindRasterInView"); if (m == null) return false; var t = m.Invoke(rs, new object[] { Ms.P(x, y), view }); return t != null && Id(t) == Id(r); }
            catch (Exception ex) { Ghi("FindRasterInView: " + ex.Message); return false; }
        }
        // ---------- gỡ các ảnh thoả điều kiện: Raster Manager gỡ các ảnh đang chọn → chỉ chọn ảnh của mình, gỡ, trả lại lựa chọn cũ ----------
        public static int Go(Func<object, bool> chon)
        {
            var rm = Rm(); var rs = Get(rm, "Rasters"); var all = DanhSach(rs); var mine = all.Where(chon).ToList(); if (mine.Count == 0) return 0;
            var ids = new HashSet<int>(mine.Select(x => Id(x))); var giu = new List<KeyValuePair<object, bool>>();
            bool tu = true; try { tu = Convert.ToBoolean(Get(rm, "IsUpdateViewAutomatic")); if (tu) Set(rm, "IsUpdateViewAutomatic", false); } catch { }
            int n0 = all.Count, n1 = n0;
            try
            {
                foreach (var x in all)
                {
                    try
                    {
                        var ei = Get(x, "ExtendedInformation"); bool s = Convert.ToBoolean(Get(ei, "IsSelected")), m = ids.Contains(Id(x));
                        if (!m) giu.Add(new KeyValuePair<object, bool>(ei, s)); if (s != m) Set(ei, "IsSelected", m);
                    }
                    catch (Exception ex) { Ghi("chọn ảnh: " + ex.Message); }
                }
                // an toàn: chỉ gỡ khi đúng các ảnh của mình đang được chọn
                int dung = 0, sai = 0;
                foreach (var x in DanhSach(rs)) { try { if (Convert.ToBoolean(Get(Get(x, "ExtendedInformation"), "IsSelected"))) { if (ids.Contains(Id(x))) dung++; else sai++; } } catch { } }
                if (sai > 0 || dung == 0) { Ghi("không gỡ: lựa chọn ảnh không đúng (" + dung + "/" + sai + ")"); return 0; }
                Call(rs, "Detach"); n1 = DanhSach(rs).Count;
                if (n0 - n1 != mine.Count) Ghi("gỡ " + (n0 - n1) + " ảnh, mong " + mine.Count);
                if (n0 - n1 > mine.Count) GoNham = true;
            }
            finally
            {
                foreach (var kv in giu) if (kv.Value) TrySet(kv.Key, "IsSelected", true);
                try { if (tu) Set(rm, "IsUpdateViewAutomatic", true); } catch { }
            }
            return n0 - n1;
        }
        public static bool GoNham;   // Raster Manager gỡ cả ảnh không phải ảnh nền (không được xảy ra) → tắt tính năng
        public static void VeLai()
        {
            try { var vs = Ms.App.ActiveDesignFile.Views; for (int i = 1; i <= 8; i++) { try { var v = vs[i]; if (Bool(v, "IsOpen")) v.Redraw(); } catch { } } } catch { }
        }
        // ---------- chẩn đoán: kiểu, hàm Raster Manager của bộ giao diện COM trên máy ----------
        public static string TepChanDoan { get { return Path.Combine(AnhNen.Dir, "AnhVeTinh_ChanDoan.txt"); } }
        public static void GhiChanDoan(Exception ex)
        {
            try
            {
                var sb = new StringBuilder(); sb.AppendLine("Biên tập BĐĐC " + CapNhat.PhienBan + " – chẩn đoán ảnh vệ tinh nền " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", IV));
                sb.AppendLine("Giao diện COM: " + Asm.FullName); sb.AppendLine("Lỗi: " + ex);
                lock (nk) sb.AppendLine("--- nhật ký ---\r\n" + nk);
                Type[] ts; try { ts = Asm.GetTypes(); } catch (ReflectionTypeLoadException e) { ts = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in ts.Where(x => x.Name.IndexOf("Raster", StringComparison.OrdinalIgnoreCase) >= 0 || x.Name.EndsWith("GeoReferenceInformation") || x.Name.EndsWith("ExtendedInformation") || x.Name.EndsWith("RenderingInformation") || x.Name == "MsdAttachMode").OrderBy(x => x.Name))
                {
                    sb.AppendLine("== " + t.FullName);
                    if (t.IsEnum) { foreach (var n in Enum.GetNames(t)) sb.AppendLine("   " + n + " = " + Convert.ToInt32(Enum.Parse(t, n))); continue; }
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) sb.AppendLine("   " + m.ReturnType.Name + " " + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name + (p.IsOptional ? "=opt" : "")).ToArray()) + ")");
                }
                File.WriteAllText(TepChanDoan, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}
