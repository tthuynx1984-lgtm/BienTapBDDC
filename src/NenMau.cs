// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – NỀN MÀU LOẠI ĐẤT dưới bản đồ DGN (bật / tắt). © Nguyễn Xuân Thủy
// Tô mỗi thửa đúng màu loại đất: bộ màu dịu (mặc định) hoặc màu chuẩn bản đồ hiện trạng (TT 08/2024/TT-BTNMT); mã loại đất cũ tự quy về mã mới;
// sửa được màu từng mã.
// Vẽ bằng phần tử tạm (transient) của chương trình bản vẽ, vẽ trước mọi đối tượng của bản vẽ: KHÔNG ghi gì vào tệp DGN, không vào Undo, không chọn,
// không bắt điểm, không tính vào Fit View, không in – sửa bản đồ không đụng tới nền, nền không đụng tới đối tượng nào; ranh, nhãn luôn nằm trên.
// Loại đất đổi ở thửa nào (bảng thuộc tính, phiếu thửa, nhập Excel, tách / hợp, đọc lại nhãn…) thì chỉ thửa đó được tô lại; thêm / bớt thửa cũng vậy.
// Bật cùng ảnh vệ tinh nền: màu tô trong mờ để vẫn thấy ảnh. Thửa có lỗ (bao quanh thửa khác): tô đúng phần đất của thửa, không đè lên thửa bên trong.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using BCOM = Bentley.Interop.MicroStationDGN;

namespace M8Pro
{
    // ===== BẢNG MÀU LOẠI ĐẤT =====
    // Hai bộ màu:
    // • Màu dịu (mặc định, dễ nhìn): lúa vàng nhạt, cây hằng năm cam nhạt, cây lâu năm xanh lá nhạt, rừng xanh lá, thủy sản – làm muối xanh nước,
    //   đất ở hồng, giao thông xám, thủy lợi – sông – mặt nước xanh trời, nghĩa trang tím nhạt, đất chưa sử dụng be, loại khác xám rất nhạt.
    // • Màu chuẩn bản đồ hiện trạng sử dụng đất: bảng ký hiệu loại đất (TT 08/2024/TT-BTNMT).
    // Mã loại đất cũ (TT 27/2018, TT 08/2007) quy về mã mới. Lưu trên máy (%APPDATA%\M8Pro\bangmau.txt): BO=diu|chuan và các mã đã đổi màu riêng: MÃ=R,G,B.
    public static class MauLoaiDat
    {
        public class Loai { public string Ma, Ten; public int MacDinh, Mau, Diu, Chuan; }
        public const int BO_DIU = 0, BO_CHUAN = 1;
        public static readonly string[] TenBo = { "Màu dịu, dễ nhìn (mặc định)", "Màu chuẩn bản đồ hiện trạng (TT 08/2024)" };
        public static readonly string[] TenNgan = { "bộ màu dịu", "bộ màu chuẩn bản đồ hiện trạng" };
        public static int Bo { get; private set; }
        const string BANG = "NNP|255,255,100|Nhóm đất nông nghiệp;CHN|255,252,120|Đất trồng cây hằng năm;LUA|255,252,130|Đất trồng lúa;LUC|255,252,140|Đất chuyên trồng lúa;LUK|255,252,150|Đất trồng lúa còn lại;HNK|255,240,180|Đất trồng cây hằng năm khác;CLN|255,210,160|Đất trồng cây lâu năm;LNP|170,255,50|Đất lâm nghiệp;RDD|110,255,100|Đất rừng đặc dụng;RPH|190,255,30|Đất rừng phòng hộ;RSX|180,255,180|Đất rừng sản xuất;RSN|180,255,180|Đất rừng sản xuất là rừng tự nhiên;NTS|170,255,255|Đất nuôi trồng thủy sản;CNT|230,230,130|Đất chăn nuôi tập trung;LMU|255,255,254|Đất làm muối;NKH|245,255,180|Đất nông nghiệp khác;PNN|255,255,100|Nhóm đất phi nông nghiệp;OTC|255,180,255|Đất ở;ONT|255,208,255|Đất ở tại nông thôn;ODT|255,160,255|Đất ở tại đô thị;TSC|255,170,160|Đất xây dựng trụ sở cơ quan;CQA|255,120,120|Đất quốc phòng, an ninh;CQP|255,100,80|Đất quốc phòng;CAN|255,80,70|Đất an ninh;DSN|255,160,170|Đất xây dựng công trình sự nghiệp;DVH|255,170,160|Đất xây dựng cơ sở văn hóa;DXH|255,170,160|Đất xây dựng cơ sở xã hội;DYT|255,170,160|Đất xây dựng cơ sở y tế;DGD|255,170,160|Đất xây dựng cơ sở giáo dục và đào tạo;DTT|255,170,160|Đất xây dựng cơ sở thể dục, thể thao;DKH|255,170,160|Đất xây dựng cơ sở khoa học và công nghệ;DMT|255,170,160|Đất xây dựng cơ sở môi trường;DKT|255,170,160|Đất xây dựng cơ sở khí tượng thủy văn;DNG|255,170,160|Đất xây dựng cơ sở ngoại giao;DSK|255,170,160|Đất xây dựng công trình sự nghiệp khác;CSK|255,160,170|Đất sản xuất, kinh doanh phi nông nghiệp;SCC|255,170,160|Đất khu công nghiệp, cụm công nghiệp;SKK|250,170,160|Đất khu công nghiệp;SKN|250,170,160|Đất cụm công nghiệp;SCT|255,170,160|Đất khu công nghệ thông tin tập trung;TMD|250,170,160|Đất thương mại, dịch vụ;SKC|250,170,160|Đất cơ sở sản xuất phi nông nghiệp;SKS|205,170,205|Đất sử dụng cho hoạt động khoáng sản;CCC|255,170,160|Đất sử dụng vào mục đích công cộng;DGT|255,170,50|Đất công trình giao thông;DTL|170,255,255|Đất công trình thủy lợi;DCT|170,255,255|Đất công trình cấp nước, thoát nước;DPC|255,170,170|Đất công trình phòng, chống thiên tai;DDD|255,170,160|Đất có di tích lịch sử - văn hóa, danh lam thắng cảnh, di sản thiên nhiên;DRA|205,170,205|Đất công trình xử lý chất thải;DNL|255,170,160|Đất công trình năng lượng, chiếu sáng công cộng;DBV|255,170,160|Đất công trình hạ tầng bưu chính, viễn thông, công nghệ thông tin;DCH|255,170,160|Đất chợ dân sinh, chợ đầu mối;DKV|255,170,160|Đất khu vui chơi, giải trí công cộng, sinh hoạt cộng đồng;TON|255,170,160|Đất tôn giáo;TIN|255,170,160|Đất tín ngưỡng;NTD|210,210,210|Đất nghĩa trang, nhà tang lễ, cơ sở hỏa táng; đất cơ sở lưu trữ tro cốt;TVC|180,255,255|Đất có mặt nước chuyên dùng;MNC|180,255,255|Đất có mặt nước chuyên dùng dạng ao, hồ, đầm, phá;SON|160,255,255|Đất có mặt nước dạng sông, ngòi, kênh, rạch, suối;PNK|255,170,160|Đất phi nông nghiệp khác;CSD|255,255,254|Nhóm đất chưa sử dụng;CGT|255,255,254|Đất do Nhà nước thu hồi chưa giao, chưa cho thuê;BCS|255,255,254|Đất bằng chưa sử dụng;DCS|255,255,254|Đất đồi núi chưa sử dụng;NCS|230,230,200|Núi đá không có rừng cây;MCS|180,255,255|Đất có mặt nước chưa sử dụng";
        // màu dịu theo nhóm loại đất: R,G,B|các mã; mã không có ở đây: xám rất nhạt
        const string BANG_DIU = "255,241,168|LUA,LUC,LUK;255,226,184|CHN,HNK;214,239,192|CLN;184,224,174|LNP,RDD,RPH,RSX,RSN;191,227,245|NTS,LMU;255,208,219|OTC,ONT,ODT;" +
            "229,229,229|DGT;205,230,255|DTL,DCT,SON,MNC,TVC,MCS;226,210,238|NTD;240,230,216|CSD,CGT,BCS,DCS,NCS";
        const int DIU_KHAC = 0xF3F4F6;
        static readonly Dictionary<string, string> CU = new Dictionary<string, string> {
            { "BHK", "HNK" }, { "NHK", "HNK" }, { "COC", "HNK" }, { "LUN", "LUK" }, { "DHT", "CCC" }, { "DCK", "CCC" }, { "DSH", "DKV" }, { "DDT", "DDD" }, { "DDL", "DDD" },
            { "DTS", "DSN" }, { "SKX", "SKC" }, { "SKV", "SKC" }, { "SKT", "SKK" }, { "TSN", "NTS" }, { "TSL", "NTS" }, { "NTN", "NTS" }, { "NTM", "NTS" },
            { "TSK", "TSC" }, { "CTS", "TSC" }, { "TTN", "TON" }, { "SMN", "SON" } };
        // mã cũ khác của lúa, rừng: theo hai chữ đầu
        static readonly string[,] DAU = { { "LU", "LUA" }, { "RS", "RSX" }, { "RP", "RPH" }, { "RD", "RDD" } };
        public static readonly List<Loai> Ds = new List<Loai>();
        static readonly Dictionary<string, Loai> theoMa = new Dictionary<string, Loai>(StringComparer.Ordinal);
        public static event Action Doi;
        static MauLoaiDat()
        {
            var diu = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var nhom in BANG_DIU.Split(';'))
            {
                var g = nhom.Split('|'); var cg = g[0].Split(',');
                int mg = (Kep(int.Parse(cg[0])) << 16) | (Kep(int.Parse(cg[1])) << 8) | Kep(int.Parse(cg[2]));
                foreach (var mm in g[1].Split(',')) diu[mm] = mg;
            }
            foreach (var muc in BANG.Split(';'))
            {
                var a = muc.Split('|'); if (a.Length < 3) continue; var c = a[1].Split(','); if (c.Length != 3) continue;
                int rgb = (Kep(int.Parse(c[0])) << 16) | (Kep(int.Parse(c[1])) << 8) | Kep(int.Parse(c[2])), d;
                if (!diu.TryGetValue(a[0], out d)) d = DIU_KHAC;
                var l = new Loai { Ma = a[0], Ten = a[2], Chuan = rgb, Diu = d, MacDinh = d, Mau = d }; Ds.Add(l); theoMa[l.Ma] = l;
            }
            Nap();
        }
        // ký hiệu đầu tiên của loại đất, chữ in hoa: "ONT+CLN" → ONT, "luc/luk" → LUC
        public static string KyHieu(string ld)
        {
            if (string.IsNullOrEmpty(ld)) return ""; var s = ld.ToUpperInvariant(); int i = 0;
            while (i < s.Length && !(s[i] >= 'A' && s[i] <= 'Z')) i++;
            int j = i; while (j < s.Length && s[j] >= 'A' && s[j] <= 'Z') j++;
            return s.Substring(i, j - i);
        }
        // mã trong bảng màu ("" nếu không có): mã mới dùng thẳng, mã cũ quy về mã mới
        public static string Ma(string ld)
        {
            var k = KyHieu(ld); if (k == "") return ""; if (theoMa.ContainsKey(k)) return k;
            string m; if (CU.TryGetValue(k, out m) && theoMa.ContainsKey(m)) return m;
            if (k.Length >= 3) for (int i = 0; i < DAU.GetLength(0); i++) if (k.StartsWith(DAU[i, 0], StringComparison.Ordinal) && theoMa.ContainsKey(DAU[i, 1])) return DAU[i, 1];
            return "";
        }
        public static Loai Tim(string ma) { Loai l; return ma != null && theoMa.TryGetValue(ma, out l) ? l : null; }
        public static int? Rgb(string ma) { var l = Tim(ma); return l == null ? (int?)null : l.Mau; }
        public static void Dat(string ma, int rgb) { var l = Tim(ma); if (l == null || l.Mau == rgb) return; l.Mau = rgb; Luu(); Bao(); }
        public static void MacDinhHet() { bool doi = false; foreach (var l in Ds) if (l.Mau != l.MacDinh) { l.Mau = l.MacDinh; doi = true; } if (doi) { Luu(); Bao(); } }
        // số mã đã đổi màu riêng (khác màu mặc định của bộ màu đang chọn)
        public static int SoDaDoi { get { int n = 0; foreach (var l in Ds) if (l.Mau != l.MacDinh) n++; return n; } }
        // chọn bộ màu: mọi mã về màu mặc định của bộ đó (bỏ các màu đã đổi riêng)
        public static void DatBo(int bo)
        {
            if (bo != BO_CHUAN) bo = BO_DIU; bool doi = bo != Bo || SoDaDoi > 0; DatMacDinh(bo);
            if (doi) { Luu(); Bao(); }
        }
        static void DatMacDinh(int bo) { Bo = bo; foreach (var l in Ds) l.MacDinh = l.Mau = bo == BO_CHUAN ? l.Chuan : l.Diu; }
        static void Bao() { var d = Doi; if (d != null) try { d(); } catch (Exception ex) { Program.Log(ex); } }
        public static Color MauHe(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
        public static int TuMau(Color c) { return (c.R << 16) | (c.G << 8) | c.B; }
        public static string ChuoiRgb(int rgb) { return ((rgb >> 16) & 255) + ", " + ((rgb >> 8) & 255) + ", " + (rgb & 255); }
        static int Kep(int v) { return v < 0 ? 0 : v > 255 ? 255 : v; }
        static string Tep { get { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "M8Pro"); try { Directory.CreateDirectory(d); } catch { } return Path.Combine(d, "bangmau.txt"); } }
        static void Nap()
        {
            try
            {
                if (!File.Exists(Tep)) return;
                var ds = File.ReadAllLines(Tep, Encoding.UTF8);
                // bộ màu trước (màu mặc định), rồi các mã đã đổi màu riêng
                foreach (var d0 in ds) { var t0 = d0.Trim(); if (t0.StartsWith("BO=", StringComparison.OrdinalIgnoreCase)) DatMacDinh(t0.Substring(3).Trim().ToLowerInvariant() == "chuan" ? BO_CHUAN : BO_DIU); }
                foreach (var dong in ds)
                {
                    var t = dong.Trim(); if (t == "" || t.StartsWith("#") || t.StartsWith("BO=", StringComparison.OrdinalIgnoreCase)) continue; int k = t.IndexOf('='); if (k <= 0) continue;
                    var l = Tim(t.Substring(0, k).Trim().ToUpperInvariant()); if (l == null) continue;
                    var c = t.Substring(k + 1).Split(','); int r, g, b;
                    if (c.Length == 3 && int.TryParse(c[0].Trim(), out r) && int.TryParse(c[1].Trim(), out g) && int.TryParse(c[2].Trim(), out b)) l.Mau = (Kep(r) << 16) | (Kep(g) << 8) | Kep(b);
                }
            }
            catch (Exception ex) { Program.Log(ex); }
        }
        static void Luu()
        {
            try
            {
                var ds = new List<string>();
                foreach (var l in Ds) if (l.Mau != l.MacDinh) ds.Add(l.Ma + "=" + ((l.Mau >> 16) & 255) + "," + ((l.Mau >> 8) & 255) + "," + (l.Mau & 255));
                if (ds.Count == 0 && Bo == BO_DIU) { if (File.Exists(Tep)) File.Delete(Tep); return; }
                ds.Insert(0, "BO=" + (Bo == BO_CHUAN ? "chuan" : "diu"));
                ds.Insert(0, "# Bảng màu loại đất của nền màu – BO: bộ màu (diu: màu dịu, chuan: màu chuẩn bản đồ hiện trạng); các mã đã đổi màu riêng: MÃ=R,G,B");
                File.WriteAllLines(Tep, ds.ToArray(), Encoding.UTF8);
            }
            catch (Exception ex) { Program.Log(ex); }
        }
    }

    // ===== DỰNG NỀN: so thửa cần tô với phần đã vẽ, chỉ vẽ thêm / gỡ phần thay đổi (không phụ thuộc chương trình bản vẽ – kiểm thử được) =====
    public static class NenVe
    {
        public class Muc { public long K; public List<double[]> R; public int Rgb; public double Tr, D2; public object H; }
        static readonly Dictionary<long, List<Muc>> da = new Dictionary<long, List<Muc>>();
        static readonly List<Muc> hang = new List<Muc>();
        public static Func<double[], int, double, object> Tao;   // vẽ một vùng: toạ độ x,y nối tiếp, màu 0xRRGGBB, độ trong suốt (0 – 1) → phần tử tạm
        public static Action<object> Xoa;                         // gỡ phần tử tạm
        public static int CoMau, KhongLoai, KhongMa;              // lần dựng gần nhất: số thửa có màu / chưa có loại đất / ký hiệu chưa có trong bảng
        public static readonly Dictionary<string, int> MaLa = new Dictionary<string, int>();
        public static int SoVe { get { int n = 0; foreach (var l in da.Values) foreach (var m in l) if (m.H != null) n++; return n; } }
        public static int ConLai { get { return hang.Count; } }
        // đặt lại danh sách thửa cần tô (tr: độ trong suốt; cx, cy: tâm khung nhìn – vẽ gần trước); trả về số vùng đã gỡ
        public static int Dat(IList<Parcel> ds, double tr, double cx, double cy)
        {
            var muon = new Dictionary<long, List<Muc>>(); CoMau = KhongLoai = KhongMa = 0; MaLa.Clear();
            foreach (var p in ds)
            {
                if (p == null || p.Rings == null || p.Rings.Count == 0 || p.Rings[0] == null || p.Rings[0].Length < 6) continue;
                var rgb = MauLoaiDat.Rgb(MauLoaiDat.Ma(p.Ld));
                if (rgb == null)
                {
                    var kh = MauLoaiDat.KyHieu(p.Ld); if (kh == "") KhongLoai++; else { KhongMa++; int c0; MaLa.TryGetValue(kh, out c0); MaLa[kh] = c0 + 1; }
                    continue;
                }
                CoMau++; var k = Khoa(p.Rings, rgb.Value, tr); List<Muc> l; if (!muon.TryGetValue(k, out l)) muon[k] = l = new List<Muc>();
                var bb = p.BB ?? Geo.BBox(p.Rings[0]);
                l.Add(new Muc { K = k, R = p.Rings, Rgb = rgb.Value, Tr = tr, D2 = Geo.D2((bb[0] + bb[2]) / 2, (bb[1] + bb[3]) / 2, cx, cy) });
            }
            int go = 0;
            foreach (var kv in da.ToList())
            {
                List<Muc> w; int can = muon.TryGetValue(kv.Key, out w) ? w.Count : 0;
                while (kv.Value.Count > can) { var m = kv.Value[kv.Value.Count - 1]; kv.Value.RemoveAt(kv.Value.Count - 1); if (m.H != null) { GoMot(m.H); go++; } }
                if (kv.Value.Count == 0) da.Remove(kv.Key);
            }
            hang.Clear();
            foreach (var kv in muon)
            {
                List<Muc> co; int n0 = da.TryGetValue(kv.Key, out co) ? co.Count : 0;
                for (int i = n0; i < kv.Value.Count; i++) hang.Add(kv.Value[i]);
            }
            hang.Sort((a, b) => b.D2.CompareTo(a.D2));   // gần tâm khung nhìn nhất nằm cuối hàng – vẽ trước
            return go;
        }
        // vẽ các vùng đang chờ trong thời gian cho phép (ms); trả về số vùng đã vẽ, loi: số lần vẽ bị lỗi
        public static int Chay(int ms, out int loi)
        {
            loi = 0; int n = 0; var t = Tao; if (t == null) return 0; var sw = Stopwatch.StartNew();
            while (hang.Count > 0 && (n == 0 || sw.ElapsedMilliseconds < ms))
            {
                var m = hang[hang.Count - 1]; hang.RemoveAt(hang.Count - 1);
                try { m.H = t(MotVong(m.R), m.Rgb, m.Tr); } catch (Exception ex) { m.H = null; if (loi++ == 0) Program.Log(ex); }
                List<Muc> l; if (!da.TryGetValue(m.K, out l)) da[m.K] = l = new List<Muc>(); l.Add(m); n++;
            }
            return n;
        }
        // gỡ toàn bộ nền; trả về số vùng đã gỡ
        public static int XoaHet()
        {
            int n = 0; foreach (var l in da.Values) foreach (var m in l) if (m.H != null) { GoMot(m.H); n++; }
            da.Clear(); hang.Clear(); return n;
        }
        static void GoMot(object h) { try { var x = Xoa; if (x != null) x(h); } catch (Exception ex) { Program.Log(ex); } }
        // khoá của một vùng: hình (toạ độ làm tròn mm) + màu + độ trong suốt – hình, màu không đổi thì không vẽ lại
        public static long Khoa(List<double[]> rings, int rgb, double tr)
        {
            unchecked
            {
                const ulong NT = 1099511628211UL; ulong h = 14695981039346656037UL;
                foreach (var r in rings) { if (r == null) continue; h = (h ^ (ulong)r.Length) * NT; for (int i = 0; i < r.Length; i++) h = (h ^ (ulong)(long)Math.Round(r[i] * 1000)) * NT; }
                h = (h ^ (ulong)rgb) * NT; h = (h ^ (ulong)(long)Math.Round(tr * 1000)) * NT; return (long)h;
            }
        }
        // vòng ngoài + các lỗ → một vòng kín: nối mỗi lỗ vào vòng ngoài bằng một cầu đi – về, để vùng tô đúng phần đất của thửa,
        // không đè lên thửa nằm trong lỗ. Bỏ điểm cuối trùng điểm đầu; quá 4900 đỉnh thì thưa bớt (một vùng tối đa 5000 đỉnh)
        public static double[] MotVong(List<double[]> rings)
        {
            int tong = 0; foreach (var r in rings) if (r != null) tong += r.Length / 2;
            int buoc = tong > 4900 ? (tong + 4899) / 4900 : 1;
            var o = Gon(rings[0], buoc, true); if (o.Count < 6 || rings.Count == 1) return o.ToArray();
            var lo = new List<List<double>>();
            for (int i = 1; i < rings.Count; i++) { if (rings[i] == null) continue; var g = Gon(rings[i], buoc, false); if (g.Count >= 6) lo.Add(g); }
            foreach (var h in lo.OrderByDescending(z => Math.Abs(DienTich(z)))) Noi(o, h, lo);
            return o.ToArray();
        }
        static List<double> Gon(double[] r, int buoc, bool nguoc)
        {
            int n = r.Length / 2; if (n > 1 && r[0] == r[2 * n - 2] && r[1] == r[2 * n - 1]) n--;
            var o = new List<double>(2 * (n / buoc + 2));
            for (int i = 0; i < n; i += buoc) { o.Add(r[2 * i]); o.Add(r[2 * i + 1]); }
            if (o.Count >= 6 && (DienTich(o) > 0) != nguoc) DaoChieu(o);
            return o;
        }
        static double DienTich(List<double> p)
        {
            double a = 0; int n = p.Count / 2;
            for (int i = 0, j = n - 1; i < n; j = i++) a += (p[2 * j] + p[2 * i]) * (p[2 * j + 1] - p[2 * i + 1]);
            return -a / 2;
        }
        static void DaoChieu(List<double> p)
        {
            int n = p.Count / 2;
            for (int i = 0, j = n - 1; i < j; i++, j--) { double x = p[2 * i], y = p[2 * i + 1]; p[2 * i] = p[2 * j]; p[2 * i + 1] = p[2 * j + 1]; p[2 * j] = x; p[2 * j + 1] = y; }
        }
        // nối lỗ h vào vòng o: chọn cặp đỉnh gần nhau nhất mà cầu nối không cắt cạnh nào (của vòng ngoài, các lỗ)
        static void Noi(List<double> o, List<double> h, List<List<double>> lo)
        {
            int no = o.Count / 2, nh = h.Count / 2, buocH = Math.Max(1, (int)((long)no * nh / 1500000)), K = 24;
            var ung = new List<double[]>();   // {d2, i, j} tăng dần
            for (int j = 0; j < nh; j += buocH)
                for (int i = 0; i < no; i++)
                {
                    double d = Geo.D2(o[2 * i], o[2 * i + 1], h[2 * j], h[2 * j + 1]);
                    if (ung.Count == K && d >= ung[K - 1][0]) continue;
                    int at = ung.Count; while (at > 0 && ung[at - 1][0] > d) at--; ung.Insert(at, new double[] { d, i, j }); if (ung.Count > K) ung.RemoveAt(K);
                }
            if (ung.Count == 0) return;
            int bi = (int)ung[0][1], bj = (int)ung[0][2];
            foreach (var u in ung)
            {
                int ui = (int)u[1], uj = (int)u[2]; double ax = o[2 * ui], ay = o[2 * ui + 1], bx = h[2 * uj], by = h[2 * uj + 1];
                bool cat = CatVong(o, ax, ay, bx, by); if (!cat) foreach (var q in lo) if (CatVong(q, ax, ay, bx, by)) { cat = true; break; }
                if (!cat) { bi = ui; bj = uj; break; }
            }
            var moi = new List<double>(o.Count + h.Count + 8);
            for (int a = 0; a <= bi; a++) { moi.Add(o[2 * a]); moi.Add(o[2 * a + 1]); }
            for (int s = 0; s <= nh; s++) { int q = (bj + s) % nh; moi.Add(h[2 * q]); moi.Add(h[2 * q + 1]); }
            moi.Add(o[2 * bi]); moi.Add(o[2 * bi + 1]);
            for (int a = bi + 1; a < no; a++) { moi.Add(o[2 * a]); moi.Add(o[2 * a + 1]); }
            o.Clear(); o.AddRange(moi);
        }
        static bool CatVong(List<double> v, double ax, double ay, double bx, double by)
        {
            int n = v.Count / 2;
            for (int i = 0, j = n - 1; i < n; j = i++) if (Cat(ax, ay, bx, by, v[2 * j], v[2 * j + 1], v[2 * i], v[2 * i + 1])) return true;
            return false;
        }
        // hai đoạn cắt nhau thật sự (chạm ở đầu mút không tính)
        static bool Cat(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
        {
            double d1 = Ccw(cx, cy, dx, dy, ax, ay), d2 = Ccw(cx, cy, dx, dy, bx, by), d3 = Ccw(ax, ay, bx, by, cx, cy), d4 = Ccw(ax, ay, bx, by, dx, dy);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }
        static double Ccw(double ax, double ay, double bx, double by, double cx, double cy) { return (bx - ax) * (cy - ay) - (by - ay) * (cx - ax); }
    }

    // ===== NỀN MÀU LOẠI ĐẤT TRÊN BẢN VẼ (bật / tắt) =====
    public static class NenMau
    {
        public const string TEN = "Nền màu loại đất (bật / tắt)", TEN_BM = "Bảng màu loại đất (chú giải, đổi màu)…";
        public static bool On { get; private set; }
        public static event Action Doi;
        const double TRONG = 0.45;   // độ trong suốt của màu tô khi bật cùng ảnh vệ tinh nền
        static MainForm Fm; static Timer tm; static string tep = ""; static long ky; static bool lai, baoXong, daDung, daNoi, daGhi;
        static DateTime cho = DateTime.MinValue, lanVe = DateTime.MinValue; static int loiLien;

        static void Noi()
        {
            if (daNoi) return; daNoi = true;
            Dc.Changed += () => { if (On) lai = true; };
            AnhNen.Doi += () => { if (On) { lai = true; baoXong = true; } };
            MauLoaiDat.Doi += () => { if (On) lai = true; };
        }
        public static void BatTat(MainForm f) { Fm = f; if (On) Tat(true); else Bat(); }
        static void Bat()
        {
            Noi(); NenVe.Tao = VeVung; NenVe.Xoa = GoVung;
            if (!Ms.Connected) { Fm.Msg("Chưa kết nối – mở tệp bản đồ (.dgn) rồi bật lại nền màu loại đất"); return; }
            try { ChuanBiTam(); } catch (Exception ex) { Program.Log(ex); GhiChanDoan(ex.Message); Fm.Msg("Không bật được nền màu loại đất: " + ex.Message); return; }
            try
            {
                if (Dc.P.Count == 0 || Dc.Dirty) Fm.Msg(Dc.Build(false));
                if (Dc.P.Count == 0 && Dc.G != null && !Dc.DocLap) Fm.Msg(Dc.FromGtp());
            }
            catch (Exception ex) { Program.Log(ex); }
            if (Dc.P.Count == 0) { Fm.Msg("Chưa có thửa để tô màu: bấm Bản đồ → Tạo thửa (hoặc kết nối tệp thuộc tính có hình thửa) rồi bật lại nền màu"); return; }
            On = true; tep = Ms.FullName; ky = 0; lai = true; baoXong = true; daDung = true; loiLien = 0; cho = DateTime.MinValue;
            if (tm == null) { tm = new Timer { Interval = 250 }; tm.Tick += (s, e) => Tick(); }
            tm.Start(); Bao(); Fm.Msg("Nền màu loại đất: đang tô " + Dc.P.Count + " thửa…"); Tick();
        }
        public static void Tat(bool bao)
        {
            if (!On) return; On = false; if (tm != null) tm.Stop();
            int n = NenVe.XoaHet(); if (n > 0) VeLai(); Bao();
            if (bao && Fm != null) Fm.Msg("Đã tắt nền màu loại đất (bản vẽ không thay đổi)");
        }
        static void Bao() { var d = Doi; if (d != null) try { d(); } catch { } }
        // trước khi đóng tệp bản đồ: gỡ nền (phần tử tạm không thuộc tệp nào – để lại sẽ hiện lên tệp mở sau); tệp mới tự tô lại nếu còn bật
        public static void TruocKhiDongTep()
        {
            if (NenVe.SoVe == 0 && NenVe.ConLai == 0) return;
            try { NenVe.XoaHet(); } catch (Exception ex) { Program.Log(ex); }
            tep = null; ky = 0; lai = true; mauNoi.Clear(); khuon.Clear(); bangMau = null;
        }
        static void Tick()
        {
            if (!On) return;
            try
            {
                if (!Ms.Connected) return;   // đang mở / đóng tệp
                var fn = Ms.FullName; if (fn == "") return;
                if (fn != tep)
                {
                    NenVe.XoaHet(); tep = fn; ky = 0; lai = true; baoXong = true; daDung = false; mauNoi.Clear(); khuon.Clear(); bangMau = null;
                    cho = DateTime.Now.AddSeconds(2.5);   // đợi bảng chính nhận tệp mới (số tờ, tệp thuộc tính) rồi mới tạo thửa, tô màu
                    return;
                }
                if (DateTime.Now < cho) return;
                if (!daDung && Dc.P.Count == 0 && Dc.MapName == Ms.FileName)
                {
                    daDung = true; var s0 = Dc.Build(false); if (Dc.P.Count == 0 && Dc.G != null && !Dc.DocLap) s0 = Dc.FromGtp();
                    if (Fm != null) Fm.Msg(s0); Dc.Notify();
                }
                long k = Ky();
                if (lai || k != ky)
                {
                    lai = false; ky = k; double cx, cy; TamKhung(out cx, out cy);
                    double tr = AnhNen.On ? TRONG : (truoc ? 0 : 0.35);
                    if (NenVe.Dat(Dc.P, tr, cx, cy) > 0 && NenVe.ConLai == 0) VeLai();
                }
                if (NenVe.ConLai > 0)
                {
                    int loi; NenVe.Chay(60, out loi);
                    if (loi > 0 && (loiLien += loi) > 30) { var ld = "Không vẽ được nền màu loại đất trên máy này – đã tắt. Gửi tệp " + TepChanDoan + " cho tác giả để sửa"; GhiChanDoan(ld); Tat(false); if (Fm != null) Fm.Msg(ld); return; }
                    if (loi == 0) loiLien = 0;
                    if (NenVe.ConLai == 0 || (DateTime.Now - lanVe).TotalSeconds > 1.5) VeLai();
                    if (NenVe.ConLai == 0 && baoXong) { baoXong = false; BaoKetQua(); }
                }
            }
            catch (Exception ex) { Program.Log(ex); }
        }
        static void VeLai() { MsRaster.VeLai(); lanVe = DateTime.Now; }
        // dấu nhanh của danh sách thửa: đổi khi thêm / bớt thửa, đổi hình (tạo lại thửa) hoặc đổi loại đất
        static long Ky()
        {
            unchecked
            {
                long h = Dc.P.Count * 1000003L + RuntimeHelpers.GetHashCode(Dc.P);
                foreach (var p in Dc.P) h = h * 31 + (p == null ? 0 : RuntimeHelpers.GetHashCode(p.Rings) * 17L + (p.Ld ?? "").GetHashCode());
                return h;
            }
        }
        static void TamKhung(out double cx, out double cy)
        {
            cx = cy = 0;
            try
            {
                var vs = Ms.App.ActiveDesignFile.Views;
                for (int i = 1; i <= 8; i++)
                {
                    var v = vs[i]; if (!MsRaster.Bool(v, "IsOpen")) continue;
                    double ox, oy, ex, ey; if (!MsRaster.Pt(v, "get_Origin", out ox, out oy) || !MsRaster.Pt(v, "get_Extents", out ex, out ey)) continue;
                    cx = ox + ex / 2; cy = oy + ey / 2; return;
                }
            }
            catch { }
        }
        static void BaoKetQua()
        {
            if (Fm == null) return;
            var s = "Nền màu loại đất: đã tô " + NenVe.CoMau + " thửa theo " + MauLoaiDat.TenNgan[MauLoaiDat.Bo] + (AnhNen.On ? " (trong mờ trên ảnh vệ tinh)" : "");
            if (NenVe.KhongLoai > 0) s += " · " + NenVe.KhongLoai + " thửa chưa có loại đất (để trống)";
            if (NenVe.KhongMa > 0) s += " · ký hiệu chưa có trong bảng màu (để trống): " + string.Join(", ", NenVe.MaLa.OrderByDescending(z => z.Value).Take(6).Select(z => z.Key + " – " + z.Value + " thửa").ToArray());
            if (cachMau == 1) s += " · chưa đặt được màu thật: đang dùng màu gần nhất trong bảng màu của tệp DGN";
            if (!truoc) s += " · không vẽ được nền trước bản vẽ: màu tô để trong mờ";
            if (AnhNen.On && !coTrongSuot) s += " · không đặt được độ trong suốt: màu tô che ảnh vệ tinh trong thửa";
            s += " · không thấy màu: bật Fill trong View Attributes (Ctrl+B)";
            Fm.Msg(s);
            if (!daGhi) { daGhi = true; GhiChanDoan("lần bật đầu tiên"); }
        }
        public static void BangMau(MainForm f) { Fm = f; using (var b = new BangMauForm()) b.ShowDialog(f); }

        // ----- Vẽ nền: mỗi thửa một vùng tô là phần tử tạm (transient), vẽ trước mọi phần tử của bản vẽ, không bắt điểm, không in -----
        static MethodInfo hamTam; static object[] mauThamSo; static bool truoc; static string coTam = "";
        static bool coTo = true, coTrongSuot = true;
        static object VeVung(double[] xy, int rgb, double tr)
        {
            int n = xy.Length / 2; if (n < 3) return null;
            var v = new BCOM.Point3d[n]; for (int i = 0; i < n; i++) { v[i].X = xy[2 * i]; v[i].Y = xy[2 * i + 1]; }
            // vùng tô lấy màu từ phần tử mẫu cùng màu (màu tô kín theo màu phần tử), rồi đặt lại màu, màu tô cho chắc
            int c = MauNoi(rgb); BCOM.Element tpl = null; try { tpl = KhuonMau(c); } catch { }
            var e = (BCOM.Element)Ms.App.CreateShapeElement1(tpl, ref v, (BCOM.MsdFillMode)1);   // phần tử Create… trả về phải ép sang Element
            e.Color = c; try { e.LineWeight = 0; } catch { }   // lớp hiển thị lấy theo phần tử mẫu (lớp Default)
            if (coTo) try { ((dynamic)e).FillColor = c; } catch { coTo = false; }
            if (tr > 0 && coTrongSuot) try { ((dynamic)e).Transparency = tr; } catch { coTrongSuot = false; }
            return TaoTam(e);
        }
        static void GoVung(object h) { ((dynamic)h).Reset(); }
        static readonly Dictionary<int, BCOM.Element> khuon = new Dictionary<int, BCOM.Element>();
        static BCOM.Element KhuonMau(int c)
        {
            BCOM.Element t; if (khuon.TryGetValue(c, out t)) return t;
            var tg = new[] { new BCOM.Point3d(), new BCOM.Point3d { X = 1 }, new BCOM.Point3d { Y = 1 } };
            t = (BCOM.Element)Ms.App.CreateShapeElement1(null, ref tg, (BCOM.MsdFillMode)0); t.Color = c; try { t.LineWeight = 0; } catch { }
            try { var L = LopNen(); if (L != null) t.Level = L; } catch { }
            khuon[c] = t; return t;
        }
        // lớp hiển thị của nền (mặc định Default): chỉ tìm lớp có sẵn, không tạo lớp mới – nền không phải phần tử của bản vẽ
        static BCOM.Level LopNen() { return Ms.LevelTen(string.IsNullOrEmpty(Cfg.I.NenLop) ? "Default" : Cfg.I.NenLop) ?? Ms.LevelTen("Default"); }
        static object TaoTam(BCOM.Element e)
        {
            ChuanBiTam(); var a = (object[])mauThamSo.Clone(); a[0] = e; return hamTam.Invoke(Ms.App, a);
        }
        // đọc một lần hàm tạo phần tử tạm và cờ "vẽ trước bản vẽ" (tên cờ theo phiên bản chương trình đang chạy)
        static void ChuanBiTam()
        {
            if (hamTam == null)
            {
                var m = typeof(BCOM.Application).GetMethod("CreateTransientElementContainer1");
                if (m == null) throw new Exception("Chương trình bản vẽ này không có phần tử tạm (CreateTransientElementContainer1)");
                var ps = m.GetParameters(); var mau = new object[ps.Length];
                for (int i = 1; i < ps.Length; i++)
                {
                    var t = ps[i].ParameterType; if (t.IsByRef) t = t.GetElementType(); var ten = t.Name.ToLowerInvariant();
                    if (!t.IsEnum) { mau[i] = t.IsValueType ? Activator.CreateInstance(t) : null; continue; }
                    if (ten.Contains("view")) mau[i] = Enum.ToObject(t, 255);
                    else if (ten.Contains("transient") || ten.Contains("flag"))
                    {
                        long co = 0; var ds = new List<string>();
                        foreach (var nm in Enum.GetNames(t))
                        {
                            long gt = Convert.ToInt64(Enum.Parse(t, nm)); ds.Add(nm + " = " + gt); var x = nm.ToLowerInvariant();
                            if (x.Contains("displayfirst") || x.Contains("drawfirst")) { co |= gt; truoc = true; }
                        }
                        coTam = string.Join(", ", ds.ToArray()); mau[i] = Enum.ToObject(t, co);
                    }
                    else mau[i] = Enum.ToObject(t, 0);
                }
                mauThamSo = mau; hamTam = m;
            }
        }
        // màu 0xRRGGBB → số màu của bản vẽ: màu thật (true color) nếu được, không thì màu gần nhất trong bảng màu của tệp
        static readonly Dictionary<int, int> mauNoi = new Dictionary<int, int>(); static int[] bangMau; static int cachMau;
        static int MauNoi(int rgb)
        {
            int c; if (mauNoi.TryGetValue(rgb, out c)) return c;
            int vb = ((rgb >> 16) & 255) | (rgb & 0xFF00) | ((rgb & 255) << 16);   // RGB() kiểu VBA: đỏ + lục·256 + lam·65536
            if (cachMau == 0)
            {
                try { dynamic mr = Ms.App.ActiveModelReference; c = Convert.ToInt32(mr.InternalColorFromRGBColor(vb)); mauNoi[rgb] = c; return c; }
                catch (Exception ex) { cachMau = 1; Program.Log(ex); }
            }
            c = GanNhat(rgb); mauNoi[rgb] = c; return c;
        }
        static int GanNhat(int rgb)
        {
            if (bangMau == null)
            {
                var bm = new int[255];
                try { dynamic df = Ms.App.ActiveDesignFile; dynamic ct = df.ExtractColorTable(); for (int i = 0; i < 255; i++) { int q = Convert.ToInt32(ct.GetColorAtIndex(i)); bm[i] = ((q & 255) << 16) | (q & 0xFF00) | ((q >> 16) & 255); } }
                catch (Exception ex) { Program.Log(ex); bm = new[] { 0xFFFFFF, 0x0000FF, 0x00FF00, 0xFF0000, 0xFFFF00, 0xFF00FF, 0xFF7F00, 0x00FFFF }; }
                bangMau = bm;
            }
            // màu có sắc (lúa vàng, đất ở hồng, rừng xanh…): chọn màu cùng sắc gần nhất – bảng màu thường chỉ có màu đậm, chọn theo khoảng cách
            // RGB thuần sẽ ra trắng / xám cho mọi màu nhạt; màu gần trắng, xám: chọn theo khoảng cách RGB
            int r = (rgb >> 16) & 255, g = (rgb >> 8) & 255, b = rgb & 255, tot = -1; double h0, s0; Hsv(r, g, b, out h0, out s0);
            bool sac = s0 >= 0.15; double dmin = double.MaxValue;
            for (int lan = 0; lan < 2 && tot < 0; lan++)
                for (int k = 0; k < bangMau.Length; k++)
                {
                    int w = bangMau[k], wr = (w >> 16) & 255, wg = (w >> 8) & 255, wb = w & 255; double hk, sk; Hsv(wr, wg, wb, out hk, out sk);
                    double d = Bp(wr - r) + Bp(wg - g) + Bp(wb - b);
                    if (sac && lan == 0) { if (sk < 0.3) continue; double dh = Math.Abs(hk - h0); if (dh > 180) dh = 360 - dh; d += 4 * Bp((int)(dh / 180 * 255)); }
                    if (d < dmin) { dmin = d; tot = k; }
                }
            return Math.Max(0, tot);
        }
        static long Bp(int x) { return (long)x * x; }
        static void Hsv(int r, int g, int b, out double h, out double s)
        {
            int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), c = mx - mn; s = mx == 0 ? 0 : (double)c / mx; h = 0;
            if (c == 0) return;
            if (mx == r) h = 60.0 * (g - b) / c; else if (mx == g) h = 60.0 * (b - r) / c + 120; else h = 60.0 * (r - g) / c + 240;
            if (h < 0) h += 360;
        }
        // chẩn đoán: cách chương trình bản vẽ trên máy nhận phần tử tạm, màu thật, độ trong suốt (để tác giả sửa khi nền không hiện đúng)
        public static string TepChanDoan { get { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "M8Pro"); try { Directory.CreateDirectory(d); } catch { } return Path.Combine(d, "NenMau_ChanDoan.txt"); } }
        static void GhiChanDoan(string ly_do)
        {
            try
            {
                var sb = new StringBuilder(); sb.AppendLine("Biên tập BĐĐC " + CapNhat.PhienBan + " – chẩn đoán nền màu loại đất " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                sb.AppendLine("Lý do: " + ly_do); sb.AppendLine("Cờ phần tử tạm: " + coTam + " · vẽ trước bản vẽ: " + (truoc ? "có" : "không"));
                sb.AppendLine("Màu: " + (cachMau == 0 ? "màu thật" : "màu gần nhất trong bảng màu tệp") + " · tô kín (FillColor): " + (coTo ? "có" : "không") + " · trong suốt: " + (coTrongSuot ? "có" : "không"));
                sb.AppendLine("Số vùng đã vẽ: " + NenVe.SoVe + " · còn chờ: " + NenVe.ConLai);
                var asm = typeof(BCOM.Application).Assembly; sb.AppendLine("Giao diện COM: " + asm.FullName);
                Type[] ts; try { ts = asm.GetTypes(); } catch (ReflectionTypeLoadException ex) { ts = ex.Types.Where(z => z != null).ToArray(); }
                foreach (var t in ts.Where(z => z.Name.IndexOf("Transient", StringComparison.OrdinalIgnoreCase) >= 0 || z.Name == "ColorTable" || z.Name == "ModelReference" || z.Name == "_Element" || z.Name == "Element").OrderBy(z => z.Name))
                {
                    sb.AppendLine("== " + t.FullName);
                    if (t.IsEnum) { foreach (var nm in Enum.GetNames(t)) sb.AppendLine("   " + nm + " = " + Convert.ToInt64(Enum.Parse(t, nm))); continue; }
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if ((t.Name == "ModelReference" || t.Name.EndsWith("Element")) && m.Name.IndexOf("Color", StringComparison.OrdinalIgnoreCase) < 0 && m.Name.IndexOf("Transparen", StringComparison.OrdinalIgnoreCase) < 0 && m.Name.IndexOf("Fill", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        sb.AppendLine("   " + m.ReturnType.Name + " " + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray()) + ")");
                    }
                }
                File.WriteAllText(TepChanDoan, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    // ===== BẢNG MÀU LOẠI ĐẤT: chú giải (màu, mã, tên, số thửa trên bản đồ đang mở) – bấm đúp đổi màu một mã, nền màu đổi theo ngay =====
    public class BangMauForm : Form
    {
        readonly ListView lv = new ListView { View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill, HideSelection = false, MultiSelect = false };
        readonly ImageList il = new ImageList { ImageSize = new Size(30, 16), ColorDepth = ColorDepth.Depth32Bit };
        public BangMauForm()
        {
            Text = "Bảng màu loại đất – nền màu"; Font = new Font("Segoe UI", 9f); Width = 660; Height = 720; GiaoDien.KhongCat(this); StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; ShowInTaskbar = false;
            var tt = new Label { Dock = DockStyle.Top, Height = 70, Padding = new Padding(8, 4, 8, 4), Text = "Bộ màu dịu (mặc định) hoặc màu chuẩn bản đồ hiện trạng theo bảng ký hiệu loại đất (TT 08/2024/TT-BTNMT); mã loại đất cũ tự quy về mã mới. Bấm đúp một dòng (hoặc Đổi màu…) để đổi màu mã đó – nền màu trên bản đồ đổi theo ngay. Số thửa: trên bản đồ đang mở; dòng chữ đậm là loại đất có trên bản đồ." };
            var pb = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(6, 5, 6, 0), WrapContents = false };
            var cbo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
            cbo.Items.AddRange(MauLoaiDat.TenBo); cbo.SelectedIndex = MauLoaiDat.Bo;
            cbo.SelectedIndexChanged += (s, e) =>
            {
                if (cbo.SelectedIndex == MauLoaiDat.Bo) return; int n = MauLoaiDat.SoDaDoi;
                if (n > 0 && MessageBox.Show(this, "Chọn bộ màu khác sẽ bỏ " + n + " màu đã đổi riêng. Tiếp tục?", Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) { cbo.SelectedIndex = MauLoaiDat.Bo; return; }
                MauLoaiDat.DatBo(cbo.SelectedIndex); Nap();
            };
            pb.Controls.Add(new Label { Text = "Bộ màu:", AutoSize = true, Margin = new Padding(3, 4, 3, 3) }); pb.Controls.Add(cbo);
            lv.Columns.Add("Màu", 46); lv.Columns.Add("Mã", 56); lv.Columns.Add("Loại đất", 310); lv.Columns.Add("R, G, B", 96); lv.Columns.Add("Số thửa", 66, HorizontalAlignment.Right);
            lv.SmallImageList = il; lv.DoubleClick += (s, e) => DoiMau(); lv.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoiMau(); };
            var bp = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var bDong = new Button { Text = "Đóng", AutoSize = true, DialogResult = DialogResult.OK };
            var bHet = new Button { Text = "Màu mặc định cho tất cả", AutoSize = true };
            bHet.Click += (s, e) => { if (MessageBox.Show(this, "Đưa tất cả mã loại đất về màu mặc định của " + MauLoaiDat.TenNgan[MauLoaiDat.Bo] + "?", Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK) { MauLoaiDat.MacDinhHet(); Nap(); } };
            var bMot = new Button { Text = "Màu mặc định mã này", AutoSize = true };
            bMot.Click += (s, e) => { var l = Chon(); if (l != null) { MauLoaiDat.Dat(l.Ma, l.MacDinh); Nap(); } };
            var bDoi = new Button { Text = "Đổi màu…", AutoSize = true }; bDoi.Click += (s, e) => DoiMau();
            bp.Controls.AddRange(new Control[] { bDong, bHet, bMot, bDoi });
            Controls.Add(lv); Controls.Add(tt); Controls.Add(pb); Controls.Add(bp); AcceptButton = bDong; CancelButton = bDong;
            Nap();
        }
        MauLoaiDat.Loai Chon() { return lv.SelectedItems.Count == 0 ? null : lv.SelectedItems[0].Tag as MauLoaiDat.Loai; }
        void DoiMau()
        {
            var l = Chon(); if (l == null) return;
            using (var cd = new ColorDialog { FullOpen = true, AnyColor = true, Color = MauLoaiDat.MauHe(l.Mau) })
                if (cd.ShowDialog(this) == DialogResult.OK) { MauLoaiDat.Dat(l.Ma, MauLoaiDat.TuMau(cd.Color)); Nap(); }
        }
        void Nap()
        {
            var dem = new Dictionary<string, int>(); var la = new Dictionary<string, int>(); int trong = 0;
            foreach (var p in Dc.P)
            {
                if (p == null) continue; var ma = MauLoaiDat.Ma(p.Ld); int c0;
                if (ma != "") { dem.TryGetValue(ma, out c0); dem[ma] = c0 + 1; continue; }
                var kh = MauLoaiDat.KyHieu(p.Ld); if (kh == "") trong++; else { la.TryGetValue(kh, out c0); la[kh] = c0 + 1; }
            }
            var cu = Chon(); string maCu = cu == null ? null : cu.Ma; int dau = lv.TopItem == null ? 0 : lv.TopItem.Index;
            lv.BeginUpdate(); lv.Items.Clear(); il.Images.Clear(); il.Images.Add(O(-1));
            var dam = new Font(lv.Font, FontStyle.Bold);
            foreach (var l in MauLoaiDat.Ds)
            {
                il.Images.Add(O(l.Mau)); int n; dem.TryGetValue(l.Ma, out n);
                var it = new ListViewItem("", il.Images.Count - 1) { Tag = l, UseItemStyleForSubItems = true };
                it.SubItems.Add(l.Ma); it.SubItems.Add(l.Ten); it.SubItems.Add(MauLoaiDat.ChuoiRgb(l.Mau) + (l.Mau != l.MacDinh ? " (đã đổi)" : "")); it.SubItems.Add(n > 0 ? n.ToString() : "");
                if (n > 0) it.Font = dam; lv.Items.Add(it); if (l.Ma == maCu) it.Selected = true;
            }
            foreach (var kv in la.OrderByDescending(z => z.Value))
            {
                var it = new ListViewItem("", 0) { ForeColor = SystemColors.GrayText };
                it.SubItems.Add(kv.Key); it.SubItems.Add("ký hiệu chưa có trong bảng màu – để trống"); it.SubItems.Add(""); it.SubItems.Add(kv.Value.ToString()); lv.Items.Add(it);
            }
            if (trong > 0) { var it = new ListViewItem("", 0) { ForeColor = SystemColors.GrayText }; it.SubItems.Add("–"); it.SubItems.Add("thửa chưa có loại đất – để trống"); it.SubItems.Add(""); it.SubItems.Add(trong.ToString()); lv.Items.Add(it); }
            lv.EndUpdate(); GiaoDien.VuaCot(lv, 520);
            try { if (lv.Items.Count > 0) lv.TopItem = lv.Items[Math.Min(dau, lv.Items.Count - 1)]; } catch { }
        }
        static Bitmap O(int rgb)
        {
            var b = new Bitmap(30, 16);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                if (rgb >= 0) { using (var br = new SolidBrush(MauLoaiDat.MauHe(rgb))) g.FillRectangle(br, 3, 2, 24, 12); g.DrawRectangle(Pens.Gray, 3, 2, 23, 11); }
                else using (var pn = new Pen(Color.Silver) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot }) g.DrawRectangle(pn, 3, 2, 23, 11);
            }
            return b;
        }
    }
}
