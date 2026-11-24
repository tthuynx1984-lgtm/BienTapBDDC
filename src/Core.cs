// M8 Pro Địa chính – nghiệp vụ: thửa đất, nhãn, kiểm tra ranh, tách/gộp, thuộc tính. © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace M8Pro
{
    public enum KieuNhan { Ba_lop_chuan, Ba_dong, Phan_so }

    // ================= THIẾT LẬP (hiện trong bảng thuộc tính PropertyGrid) =================
    public class Cfg
    {
        public static Cfg I = Load();
        [Category("1. Tờ bản đồ"), DisplayName("Tỉnh / thành phố")] public string Tinh { get; set; }
        [Category("1. Tờ bản đồ"), DisplayName("Huyện (để trống nếu không dùng)")] public string Huyen { get; set; }
        [Category("1. Tờ bản đồ"), DisplayName("Xã / phường / thị trấn")] public string Xa { get; set; }
        [Category("1. Tờ bản đồ"), DisplayName("Mã đơn vị hành chính cấp xã")] public string MaXa { get; set; }
        [Category("1. Tờ bản đồ"), DisplayName("Tờ bản đồ số"), Description("Riêng cho bản đồ đang mở. Để trống: tự tìm chữ “Tờ số …” trên bản đồ, nếu không có thì lấy số cuối trong tên tệp DGN")] public string To { get; set; }
        [Category("1. Tờ bản đồ"), DisplayName("Tỷ lệ 1:")] public int TyLe { get; set; }
        [Category("1. Tờ bản đồ"), DisplayName("Kinh tuyến trục (độ)"), Description("Hệ toạ độ VN-2000, múi chiếu 3°: ghi 105 cho kinh tuyến trục 105°00′, 104,75 cho 104°45′, 105,5 cho 105°30′. Dùng để đổi toạ độ sang WGS84 (ảnh Google, chỉ đường, GeoJSON).")] public double Ktt { get; set; }
        [Category("2. Level"), DisplayName("Level ranh thửa"), Description("Nhiều level cách nhau dấu phẩy, ví dụ 10")] public string LvRanh { get; set; }
        [Category("2. Level"), DisplayName("Level chữ nhãn (để đọc nhãn)"), Description("Nhiều level cách nhau dấu phẩy, ví dụ 13")] public string LvNhan { get; set; }
        [Category("2. Level"), DisplayName("Dung sai bắt nối ranh (m)")] public double Tol { get; set; }
        [Category("2. Level"), DisplayName("Khoảng sửa lỗi ranh tối đa (m)")] public double FixTol { get; set; }
        [Category("2. Level"), DisplayName("Cắt ranh tại giao điểm"), Description("Bật: Sửa lỗi tự động phá đường gấp khúc (line string) thành đoạn thẳng (line) rồi cắt chỉ đúng tại giao điểm (hai đường cắt nhau, đầu đường chạm giữa đường khác, đỉnh nằm trên đường khác); Tách thửa, Chia thửa cắt đường ranh tại chỗ tách. Các đoạn gặp nhau dùng chung đúng một toạ độ nút, mọi đỉnh giữ nguyên, không xoá đoạn nào – tạo thửa bằng phần mềm khác không phải cắt lại ranh.")] public bool CatGiao { get; set; }
        [Category("2. Level"), DisplayName("Tạo thửa: hỏi cắt ranh tại giao điểm trước"), Description("Bật: bấm Tạo thửa mà trên level ranh còn chỗ chưa cắt tại giao điểm / đầu đường chưa khớp điểm nối thì công cụ hỏi cắt ranh trước (Có: cắt rồi tạo thửa; Không: tạo thửa luôn; Huỷ: thôi). Tắt: tạo thửa luôn, không hỏi.")] public bool HoiCatTao { get; set; }
        [Category("2. Level"), DisplayName("Tạo thửa: bỏ vùng có diện tích nhỏ hơn (m²)"), Description("Vùng khép kín nhỏ hơn diện tích này không tạo thành thửa (0: chỉ bỏ vùng dưới 0,01 m²)")] public double BoVung { get; set; }
        [Category("2. Level"), DisplayName("Tìm lỗi: vùng rất nhỏ khi diện tích dưới (m²)")] public double VungNho { get; set; }
        [Browsable(false)] public string TimLoai { get; set; }     // Tìm lỗi dữ liệu: các loại lỗi cần tìm (trống: tất cả)
        [Browsable(false)] public bool ChiLopCo { get; set; }      // Sửa lỗi tự động: chỉ hiện lớp có đối tượng đường
        [Category("3. Nhãn thửa"), DisplayName("Kiểu nhãn"), Description("Ba_lop_chuan: nhãn chuẩn 3 level 3 màu của bản đồ địa chính — số thửa trên gạch ngang, diện tích dưới gạch, loại đất bên trái ngang giữa gạch")] public KieuNhan Kieu { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Dấu thập phân theo nhãn cũ trên tờ"), Description("Bật: diện tích ghi dấu phẩy hay dấu chấm theo đa số nhãn đang có trên tờ; tờ chưa có nhãn thì theo ô “Dùng dấu phẩy thập phân”")] public bool TpTheoNhan { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Nhãn cũ khi vẽ nhãn mới – level"), Description("Vẽ nhãn mới (Vẽ nhãn, tách thửa, hợp thửa, ghi thông tin thửa…): nhãn cũ của thửa (chữ nhãn, gạch ngang của nhãn) chuyển sang level này, giữ nguyên chữ và màu, không xoá. Ghi 0 để xoá nhãn cũ như các bản trước.")] public int NhanCuLop { get; set; }
        [Browsable(false)] public int CfgVer { get; set; }
        [Browsable(false)] public string FixLv { get; set; }
        [Browsable(false)] public string GtpTx { get; set; }     // tệp thuộc tính toàn xã (.gtp) cho Hồ sơ toàn xã
        [Browsable(false)] public string BanDaBao { get; set; }   // bản cài đã báo "chưa dựng được công cụ" (báo một lần)
        [Category("3. Nhãn thửa"), DisplayName("Cao chữ nhãn (mm trên giấy)")] public double HNhan { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Tỷ lệ rộng/cao chữ")] public double TextWidthRatio { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Số thửa – level")] public int SoLop { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Số thửa – màu")] public int SoMau { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Diện tích – level")] public int DtLop { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Diện tích – màu")] public int DtMau { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Loại đất – level")] public int LdLop { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Loại đất – màu")] public int LdMau { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Số lẻ diện tích")] public int DtLe { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Dùng dấu phẩy thập phân")] public bool DauPhay { get; set; }
        [Category("3. Nhãn thửa"), DisplayName("Chữ tiếng Việt mã TCVN3 (.Vn)"), Description("Bật nếu bản đồ dùng phông .VnTime/.VnArial")] public bool Tcvn3 { get; set; }
        [Category("4. Biên tập"), DisplayName("Kích thước cạnh – level")] public int KtLop { get; set; }
        [Category("4. Biên tập"), DisplayName("Kích thước cạnh – màu")] public int KtMau { get; set; }
        [Category("4. Biên tập"), DisplayName("Số hiệu đỉnh – level")] public int DinhLop { get; set; }
        [Category("4. Biên tập"), DisplayName("Số hiệu đỉnh – màu")] public int DinhMau { get; set; }
        [Category("4. Biên tập"), DisplayName("Khung bản đồ – level")] public int KhungLop { get; set; }
        [Category("4. Biên tập"), DisplayName("Khung bản đồ – màu")] public int KhungMau { get; set; }
        [Category("4. Biên tập"), DisplayName("Tô màu loại đất – level")] public int ToLop { get; set; }
        [Category("4. Biên tập"), DisplayName("Đường tách thửa – level")] public int TachLop { get; set; }
        [Category("4. Biên tập"), DisplayName("Ranh thửa cũ khi hợp thửa – level"), Description("Hợp thửa: đoạn ranh chung giữa các thửa được chuyển sang level này (không xoá), ví dụ 49")] public int HopLop { get; set; }
        [Category("4. Biên tập"), DisplayName("Tách, hợp thửa xong lập bản vẽ (Mẫu số 22)"), Description("Bật: tách thửa, hợp thửa xong hỏi lập ngay Bản vẽ tách thửa, hợp thửa đất")] public bool TachHopBanVe { get; set; }
        [Category("4. Biên tập"), DisplayName("Ngưỡng chênh lệch diện tích (%)")] public double Lech { get; set; }
        [Category("5. Hồ sơ"), DisplayName("Cơ quan lập hồ sơ")] public string CoQuan { get; set; }
        [Category("6. Khởi động"), DisplayName("Mở cửa sổ công cụ khi nạp ứng dụng"), Description("Tắt: khi nạp chỉ hiện menu Biên tập BĐĐC trên MicroStation")] public bool TuMo { get; set; }
        [Category("6. Khởi động"), DisplayName("Hiện menu Biên tập BĐĐC trên MicroStation")] public bool NutMenu { get; set; }
        [Category("7. Tệp thuộc tính"), DisplayName("Lưu thẳng vào tệp .gtp sau mỗi lần sửa"), Description("Bản gốc được giữ lại thành .gtp.bak mỗi lần mở tệp")] public bool TuLuu { get; set; }
        [Category("7. Tệp thuộc tính"), DisplayName("Tự mở tệp .gtp cùng tên với bản đồ")] public bool TuMoGtp { get; set; }
        [Category("7. Tệp thuộc tính"), DisplayName("Tạo thửa xong ghi vào tệp thuộc tính"), Description("Ghi diện tích, tâm, hình thửa, các đường ranh tạo thành thửa và thêm thửa mới vào tệp .gtp ngay sau khi tạo thửa.")] public bool GhiKhiTaoThua { get; set; }
        [Category("5. Hồ sơ"), DisplayName("Thư mục lưu hồ sơ"), Description("Để trống: thư mục HoSo cạnh tệp bản đồ.")] public string ThuMucHoSo { get; set; }
        [Category("5. Hồ sơ"), DisplayName("Mở tệp hồ sơ sau khi lập")] public bool MoHoSo { get; set; }
        [Category("5. Hồ sơ"), DisplayName("Lập hồ sơ chạy ngầm (không mở cửa sổ)"), Description("Bật: các mẫu Đăng ký, Hồ sơ, Kết xuất lập ngay trong công cụ (Edge / Chrome chạy ngầm), tệp lưu vào thư mục hồ sơ. Tắt: mở cửa sổ hồ sơ riêng như M8 Mobile.")] public bool ChayNgam { get; set; }
        [Browsable(false)] public string DinhDang { get; set; }
        [Browsable(false)] public bool KttHoi { get; set; }
        [Browsable(false)] public string AnhNguon { get; set; }      // ảnh vệ tinh nền: Google / Esri (trống: chưa chọn)
        [Browsable(false)] public string GoogleKey { get; set; }     // khoá Google Maps Platform (Map Tiles API) của người dùng
        [Browsable(false)] public bool AnhTuLamMoi { get; set; }
        [Browsable(false)] public string MauChuDe { get; set; }      // màu chủ đề thanh ứng dụng "#RRGGBB" (trống: giao diện Windows mặc định)
        [Category("4. Biên tập"), DisplayName("Màu tô sáng thửa đang chọn")] public int MauSang { get; set; }
        [Category("4. Biên tập"), DisplayName("Nền màu loại đất – level hiển thị"), Description("Level gán cho nền màu loại đất. Nền chỉ hiển thị trên màn hình, không vẽ phần tử nào vào bản đồ. Mặc định Default; tên level không có trong bản vẽ thì dùng Default, không tạo level mới.")] public string NenLop { get; set; }
        [Category("7. Tệp thuộc tính"), DisplayName("Hỏi kết nối tệp thuộc tính khi mở bản đồ"), Description("Bật: mỗi khi MicroStation mở tờ bản đồ, hiện bảng chọn tệp .gtp (cùng tên, cùng thư mục hoặc tệp khác). Tắt: tự kết nối tệp đã chọn lần trước cho tờ đó, hoặc tệp cùng tên.")] public bool HoiGtp { get; set; }
        [Category("4. Biên tập"), DisplayName("Điểm đo – level")] public int DiemLop { get; set; }
        [Category("4. Biên tập"), DisplayName("Điểm đo – màu")] public int DiemMau { get; set; }
        [Category("4. Biên tập"), DisplayName("Tên chủ sử dụng – level")] public int ChuLop { get; set; }
        [Category("4. Biên tập"), DisplayName("Tên chủ sử dụng – màu")] public int ChuMau { get; set; }
        [Category("4. Biên tập"), DisplayName("Ranh quy hoạch (vùng khép kín) – level")] public int QhLop { get; set; }

        static string File_ { get { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "M8Pro"); Directory.CreateDirectory(d); return Path.Combine(d, "diachinh.ini"); } }
        static Cfg Load()
        {
            var c = new Cfg
            {
                Tinh = "Phú Thọ", Huyen = "", Xa = "", MaXa = "", HoiGtp = true, GhiKhiTaoThua = true, ThuMucHoSo = "", MoHoSo = true, ChayNgam = true, AnhNguon = "", GoogleKey = "", AnhTuLamMoi = true, MauChuDe = "", DinhDang = "pdf", GtpTx = "", BanDaBao = "", MauSang = 4, To = "", TyLe = 1000, Ktt = 105, LvRanh = "10", LvNhan = "13", Tol = 0.02, FixTol = 0.5, CatGiao = true, BoVung = 0, HoiCatTao = true, NenLop = "Default", VungNho = 1, TimLoai = "", ChiLopCo = true, Kieu = KieuNhan.Ba_lop_chuan, HNhan = 1.2, TextWidthRatio = 1, TpTheoNhan = true,
                SoLop = 13, SoMau = 1, DtLop = 4, DtMau = 3, LdLop = 2, LdMau = 2, DtLe = 1, DauPhay = true, KtLop = 14, KtMau = 6, DinhLop = 15, DinhMau = 5, KhungLop = 1, KhungMau = 0, ToLop = 63, TachLop = 10, HopLop = 49, NhanCuLop = 49, TachHopBanVe = true, Lech = 5, CoQuan = "", TuMo = true, NutMenu = true, TuLuu = true, TuMoGtp = true, DiemLop = 21, DiemMau = 1, ChuLop = 20, ChuMau = 7, QhLop = 50
            };
            try
            {
                if (File.Exists(File_)) foreach (var l in File.ReadAllLines(File_, Encoding.UTF8))
                    {
                        int i = l.IndexOf('='); if (i < 0) continue; var p = typeof(Cfg).GetProperty(l.Substring(0, i)); if (p == null) continue; var v = l.Substring(i + 1);
                        try { p.SetValue(c, p.PropertyType.IsEnum ? Enum.Parse(p.PropertyType, v) : Convert.ChangeType(v, p.PropertyType, CultureInfo.InvariantCulture), null); } catch { }
                    }
            }
            catch { }
            // bản 2: nhãn chuẩn 3 lớp 3 màu (số thửa + gạch ngang lớp 13 màu 1, loại đất lớp 2 màu 2, diện tích lớp 4 màu 3)
            if (c.CfgVer < 2)
            {
                c.Kieu = KieuNhan.Ba_lop_chuan; c.SoLop = 13; c.SoMau = 1; c.LdLop = 2; c.LdMau = 2; c.DtLop = 4; c.DtMau = 3; c.TpTheoNhan = true;
                if (c.DiemLop == 2) c.DiemLop = 21;
                c.CfgVer = 2; c.Save();
            }
            return c;
        }
        public void Save()
        {
            try { File.WriteAllLines(File_, typeof(Cfg).GetProperties().Where(p => p.CanWrite).Select(p => p.Name + "=" + Convert.ToString(p.GetValue(this, null), CultureInfo.InvariantCulture)).ToArray(), Encoding.UTF8); } catch { }
        }
        public static List<int> Lvs(string s) { var o = new List<int>(); foreach (var x in (s ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)) { int v; if (int.TryParse(x, out v)) o.Add(v); } return o; }
        public double H { get { return HNhan * TyLe / 1000.0; } }   // cao chữ nhãn theo mét trên bản đồ
        public string Place { get { return string.Join(", ", new[] { Xa, Huyen, Tinh }.Where(x => !string.IsNullOrEmpty(x)).ToArray()); } }
    }

    public class Parcel
    {
        public int I; public List<double[]> Rings; public double[] BB, Lp; public HashSet<string> Edges; public double Dt;
        public string So = "", Ld = "", Chu = "", DiaChi = "", GhiChu = ""; public double? DtNhan, Dtpl;
        public List<MEl> Labels = new List<MEl>(); public object Gid; public GRow Row; public bool FromGtp, Virtual; public string NhanSo = "", NhanLd = "";
        public List<HashSet<string>> RingEdges;   // các cạnh của từng vòng (vòng ngoài trước) – để ghi các đường ranh tạo thành thửa
    }
    public class Err { public string Text; public double X, Y; public Parcel P; }

    public static class Dc
    {
        public static List<Parcel> P = new List<Parcel>();
        public static Planar Topo; public static List<MEl> Ranh = new List<MEl>();
        public static Gtp G; public static List<GRow> GRows = new List<GRow>();
        public static string MapName = "";
        public static bool Dirty;
        public static List<Err> Errs = new List<Err>();
        public static event Action Changed;
        public static void Notify() { var c = Changed; if (c != null) c(); }
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        // số hiển thị trong hộp thoại, bảng, báo cáo: luôn dấu chấm thập phân (thống nhất toàn ứng dụng)
        public static string F(double v, int d) { return v.ToString("F" + d, IC); }
        // số ghi lên nhãn bản đồ: dấu thập phân theo thiết lập "Dùng dấu phẩy thập phân"
        public static string FNhan(double v) { var s = v.ToString("F" + Cfg.I.DtLe, IC); return Cfg.I.DauPhay ? s.Replace('.', ',') : s; }
        public static string FD(double v) { return F(v, Cfg.I.DtLe); }
        public static string ToAuto = "", ToSrc = "";
        public static string To
        {
            get
            {
                if (!string.IsNullOrEmpty(Cfg.I.To)) { ToSrc = "đã chọn cho tờ bản đồ này"; return Cfg.I.To.Trim(); }
                if (ToAuto != "") { ToSrc = "chữ “Tờ số” trên bản đồ"; return ToAuto; }
                ToSrc = "tên tệp bản đồ"; return ToTheoTen();
            }
        }
        // số tờ theo tên tờ bản đồ (không kể số tờ đã chọn tay): chữ “Tờ số …” trên bản đồ, không có thì số cuối trong tên tệp DGN
        public static string ToTheoTen()
        {
            if (ToAuto != "") return ToAuto;
            return SoTo.TheoTen(MapName);     // số cuối trong tên tệp, bỏ phần trong ngoặc (số tờ cũ): dc101(dc07) → 101
        }
        // số tờ lưu riêng cho từng tệp bản đồ
        static string ToFile { get { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "M8Pro"); Directory.CreateDirectory(d); return Path.Combine(d, "toban.txt"); } }
        static Dictionary<string, string> ToMap()
        {
            var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); try { if (File.Exists(ToFile)) foreach (var l in File.ReadAllLines(ToFile, Encoding.UTF8)) { var a = l.Split('\t'); if (a.Length == 2) o[a[0]] = a[1]; } } catch { }
            return o;
        }
        public static void SetTo(string to)
        {
            to = (to ?? "").Trim(); Cfg.I.To = to; Cfg.I.Save(); var d = ToMap(); if (to == "") d.Remove(MapName ?? ""); else d[MapName ?? ""] = to;
            try { File.WriteAllLines(ToFile, d.Select(kv => kv.Key + "\t" + kv.Value).ToArray(), Encoding.UTF8); } catch { }
        }
        // gọi khi đổi tệp bản đồ
        public static void OnMapChanged(string name)
        {
            MapName = name; P.Clear(); Topo = null; Ranh.Clear(); Errs.Clear(); string t; Cfg.I.To = ToMap().TryGetValue(name ?? "", out t) ? t : ""; ToAuto = "";
            if (Cfg.I.To != "") return;   // số tờ đã nhớ cho tệp này: không phải quét chữ (mở tờ nhanh)
            // lọc nhanh trước (chữ có số và chữ s) rồi mới chuẩn hoá – tờ nhiều chữ vẫn mở nhanh
            try { ToAuto = DetectTo(Ms.FindTexts(x => { if (x.Length > 60 || x.IndexOfAny("0123456789".ToCharArray()) < 0 || (x.IndexOf('s') < 0 && x.IndexOf('S') < 0)) return false; var q = Plain(x) + " " + Plain(Tcvn.ToUnicode(x)); return q.Contains("so") && Regex.IsMatch(q, @"\bto\b"); })); } catch { }
        }
        static string Plain(string s) { s = (s ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD); var sb = new StringBuilder(); foreach (var ch in s) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch == 'đ' ? 'd' : ch); return sb.ToString(); }
        // tìm chữ "Tờ số 9", "Tờ bản đồ số 9" (cả phông TCVN3); lấy chữ cao nhất
        public static string DetectTo(List<MEl> texts)
        {
            string best = ""; double bh = -1; var rx = new Regex(@"\bto\s*(?:ban\s*do\s*)?so\s*[:.]?\s*(\d+)");
            foreach (var t in texts)
            {
                if (string.IsNullOrEmpty(t.Text)) continue;
                foreach (var cand in new[] { t.Text, Tcvn.ToUnicode(t.Text) })
                {
                    var m = rx.Match(Plain(cand)); if (!m.Success) continue;
                    if (t.H > bh) { bh = t.H; best = int.Parse(m.Groups[1].Value).ToString(); } break;
                }
            }
            return best;
        }
        public static void Reset() { if (G != null) { try { Commit(); } catch { } G.Dispose(); G = null; } GRows = new List<GRow>(); P.Clear(); Topo = null; Ranh.Clear(); Errs.Clear(); MapName = ""; ToAuto = ""; Notify(); }
        static double Area(Parcel p) { double a = Math.Abs(Geo.Area(p.Rings[0])); foreach (var h in p.Rings.Skip(1)) a -= Math.Abs(Geo.Area(h)); return a; }

        // ================= TẠO THỬA TỪ RANH =================
        public static string Build(bool inheritSplit)
        {
            var old = P.ToList();
            var lr = Cfg.Lvs(Cfg.I.LvRanh); Ranh = Ms.Read(lr, new[] { 3, 4, 6, 12, 14, 15, 16 });
            if (Ranh.Count == 0) return "Không đọc được đường ranh nào trên level " + Cfg.I.LvRanh + ". " + Ms.LastRead + ". Vào Hệ thống → Thiết lập để chọn đúng level ranh thửa, hoặc Hệ thống → Chẩn đoán.";
            var segs = new List<Seg>();
            foreach (var m in Ranh) foreach (var l in m.Lines) for (int i = 2; i < l.Length; i += 2) if (l[i - 2] != l[i] || l[i - 1] != l[i + 1]) segs.Add(new Seg(l[i - 2], l[i - 1], l[i], l[i + 1], m.Idx));
            if (segs.Count == 0) return "Đọc được " + Ranh.Count + " phần tử trên level ranh nhưng không lấy được toạ độ. " + Ms.LastRead;
            Topo = Planar.Build(segs, Cfg.I.Tol);
            P = new List<Parcel>(); int k = 0;
            foreach (var f in Topo.Faces)
            {
                if (f.Area < Math.Max(0.01, Cfg.I.BoVung)) continue; var rings = new List<double[]> { f.Ring }; rings.AddRange(f.Holes);
                var re = new List<HashSet<string>> { f.Edges }; re.AddRange(f.HoleEdges);
                var p = new Parcel { I = k++, Rings = rings, BB = f.BB, Edges = f.Edges, Lp = Geo.LabelPt(rings), RingEdges = re }; p.Dt = Area(p); P.Add(p);
            }
            ReadLabels();
            // giữ thuộc tính đã nhập trên máy / thửa cũ
            var local = LoadLocal(); var oldBySo = new Dictionary<string, Parcel>(); foreach (var o in old) if (o.So != "" && !oldBySo.ContainsKey(o.So)) oldBySo[o.So] = o;
            foreach (var p in P)
            {
                string[] L; if (p.So != "" && local.TryGetValue(p.So, out L)) { if (p.Ld == "") p.Ld = L[0]; p.Chu = L[1]; p.DiaChi = L[2]; p.Dtpl = Num(L[3]); p.GhiChu = L[4]; }
                Parcel o; if (p.So != "" && oldBySo.TryGetValue(p.So, out o)) { if (p.Chu == "") p.Chu = o.Chu; if (p.DiaChi == "") p.DiaChi = o.DiaChi; if (p.Dtpl == null) p.Dtpl = o.Dtpl; p.Gid = o.Gid; p.Row = o.Row; }
            }
            var split = new List<Parcel>();
            if (inheritSplit)
            {
                int max = P.Concat(old).Select(x => ToInt(x.So)).DefaultIfEmpty(0).Max();
                foreach (var o in old)
                {
                    if (o.So == "") continue; var parts = P.Where(q => Geo.InPoly(q.Lp[0], q.Lp[1], o.Rings[0])).ToList(); if (parts.Count < 2) continue;
                    var keep = parts.FirstOrDefault(q => Geo.InPoly(o.Lp[0], o.Lp[1], q.Rings[0])) ?? parts.OrderByDescending(q => q.Dt).First();
                    foreach (var q in parts.OrderByDescending(q => q.Dt))
                    {
                        q.Ld = o.Ld; q.Chu = o.Chu; q.DiaChi = o.DiaChi;
                        if (q == keep) { q.So = o.So; q.Gid = o.Gid; q.Row = o.Row; } else { q.So = (++max).ToString(); q.Gid = null; q.Row = null; }
                        split.Add(q);
                    }
                }
            }
            if (G != null) Link();
            Dirty = false; SaveLocal();
            if (split.Count > 0) { DrawLabels(split, true); return "Đã tách thửa: " + string.Join(", ", split.Select(q => q.So + " (" + FD(q.Dt) + " m²)").ToArray()); }
            Notify();
            return string.Format("Đã tạo {0} thửa · {1} thửa chưa có số · {2} đầu treo", P.Count, P.Count(x => x.So == ""), Topo.Dangles.Count);
        }
        static int ToInt(string s) { int v; return int.TryParse(s, out v) ? v : 0; }
        static double? Num(string s) { double v; if (double.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Float, IC, out v)) return v; return null; }
        // số có dấu nhóm nghìn: dấu cuối (khác dấu nhóm) là dấu thập phân
        static double? NumNghin(string s)
        {
            s = (s ?? "").Replace(" ", ""); int a = s.LastIndexOf('.'), b = s.LastIndexOf(','); char tp = a > b ? '.' : ',', ng = tp == '.' ? ',' : '.';
            bool coTp = s.IndexOf(ng) >= 0 && s.IndexOf(tp) > s.LastIndexOf(ng); var t = coTp ? s.Replace(ng.ToString(), "").Replace(tp, '.') : s.Replace(".", "").Replace(",", "");
            double v; return double.TryParse(t, NumberStyles.Float, IC, out v) ? v : (double?)null;
        }
        static void ReadLabels()
        {
            var lv = new HashSet<int>(Cfg.Lvs(Cfg.I.LvNhan)); lv.Add(Cfg.I.SoLop); lv.Add(Cfg.I.DtLop); lv.Add(Cfg.I.LdLop);
            var txt = Ms.Read(lv, new[] { 17, 7 }, true); var G2 = new PGrid(P);   // cả chữ trong ô nhãn N_THUA
            foreach (var t in txt)
            {
                var p = G2.Find(t.Cx, t.Cy); if (p == null) continue; var s = (t.Text ?? "").Trim(); bool lab;
                if (t.Kind == 7)
                {
                    // khối chữ nhiều dòng (số thửa / diện tích / loại đất): là nhãn khi mọi dòng đều là phần của nhãn
                    var tk = s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    lab = tk.Length > 0 && tk.All(x => LaNhan(x, null)); if (lab) foreach (var x in tk) LaNhan(x, p);
                }
                else lab = LaNhan(s, p);   // chữ khác (địa danh, ghi chú…) không phải nhãn thửa: không bao giờ xoá
                if (lab) p.Labels.Add(t);
            }
            try { Bt.ReadBars(G2); } catch { }
        }
        // một chữ là phần của nhãn thửa: số thửa (cả số tạm 5-1), diện tích (cả dấu nhóm nghìn), loại đất, dạng gộp 12/345,6 hoặc 12(345,6)ONT;
        // p khác null: ghi số thửa, diện tích, loại đất đọc được vào thửa
        static bool LaNhan(string s, Parcel p)
        {
            var a = Regex.Replace(s, @"\s*m(2|²)$", "", RegexOptions.IgnoreCase);
            if (Regex.IsMatch(s, @"^\d+(-\d+)?$")) { if (p != null) { if (p.NhanSo == "") p.NhanSo = s; if (p.So == "") p.So = s; } return true; }
            if (Regex.IsMatch(a, @"^\d+([.,]\d+)?$")) { if (p != null && p.DtNhan == null) p.DtNhan = Num(a); return true; }
            if (Regex.IsMatch(a, @"^\d{1,3}([.,\s]\d{3})+([.,]\d+)?$")) { if (p != null && p.DtNhan == null) p.DtNhan = NumNghin(a); return true; }
            if (Regex.IsMatch(s, @"^[A-Z]{2,4}\d?([+,/][A-Z]{2,4}\d?)*$")) { if (p != null) { if (p.NhanLd == "") p.NhanLd = s; if (p.Ld == "") p.Ld = s; } return true; }
            var m = Regex.Match(s, @"^(\d+)\s*[/(]\s*(\d+[.,]?\d*)\)?\s*([A-Z+]{2,})?$");
            if (!m.Success) return false;
            if (p != null) { if (p.NhanSo == "") p.NhanSo = m.Groups[1].Value; if (m.Groups[3].Success && p.NhanLd == "") p.NhanLd = m.Groups[3].Value; if (p.So == "") p.So = m.Groups[1].Value; if (p.DtNhan == null) p.DtNhan = Num(m.Groups[2].Value); if (m.Groups[3].Success && p.Ld == "") p.Ld = m.Groups[3].Value; }
            return true;
        }
        public class PGrid
        {
            double[] b = { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue }; double cs; Dictionary<long, List<Parcel>> m = new Dictionary<long, List<Parcel>>();
            public PGrid(List<Parcel> ps)
            {
                foreach (var p in ps) { b[0] = Math.Min(b[0], p.BB[0]); b[1] = Math.Min(b[1], p.BB[1]); b[2] = Math.Max(b[2], p.BB[2]); b[3] = Math.Max(b[3], p.BB[3]); }
                cs = Math.Max((b[2] - b[0] + b[3] - b[1]) / 2 / Math.Max(4, Math.Sqrt(ps.Count)), 1);
                foreach (var p in ps) for (long i = (long)((p.BB[0] - b[0]) / cs); i <= (long)((p.BB[2] - b[0]) / cs); i++) for (long j = (long)((p.BB[1] - b[1]) / cs); j <= (long)((p.BB[3] - b[1]) / cs); j++)
                        { long k = i * 1000003L + j; List<Parcel> L; if (!m.TryGetValue(k, out L)) m[k] = L = new List<Parcel>(); L.Add(p); }
            }
            public Parcel Find(double x, double y)
            {
                if (x < b[0] || y < b[1]) return null; List<Parcel> L; if (!m.TryGetValue((long)((x - b[0]) / cs) * 1000003L + (long)((y - b[1]) / cs), out L)) return null;
                foreach (var p in L) if (Geo.InPoly(x, y, p.Rings[0]) && !p.Rings.Skip(1).Any(h => Geo.InPoly(x, y, h))) return p; return null;
            }
        }
        public static Parcel Find(double x, double y) { return new PGrid(P).Find(x, y); }
        public static List<Parcel> Neighbors(Parcel p)
        {
            var o = new List<Parcel>();
            foreach (var q in P) { if (q == p || q.BB[0] > p.BB[2] || q.BB[2] < p.BB[0] || q.BB[1] > p.BB[3] || q.BB[3] < p.BB[1]) continue; if (q.Edges.Overlaps(p.Edges)) o.Add(q); }
            return o;
        }
        public static double[] AllBB() { double[] b = { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue }; foreach (var p in P) { b[0] = Math.Min(b[0], p.BB[0]); b[1] = Math.Min(b[1], p.BB[1]); b[2] = Math.Max(b[2], p.BB[2]); b[3] = Math.Max(b[3], p.BB[3]); } return b; }
        public static void Zoom(Parcel p, double k) { var b = p.BB; double w = Math.Max(b[2] - b[0], b[3] - b[1]) * k; Ms.ZoomTo(b[0] - w, b[1] - w, b[2] + w, b[3] + w); Ms.Select(p.Labels.Select(l => l.El)); }

        // ================= LƯU THUỘC TÍNH TRÊN MÁY (khi không có .gtp) =================
        internal static string LocalFile { get { var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "M8Pro", "thuoctinh"); Directory.CreateDirectory(d); return Path.Combine(d, Regex.Replace(MapName ?? "x", @"[^\w.-]", "_") + ".txt"); } }
        static Dictionary<string, string[]> LoadLocal()
        {
            var o = new Dictionary<string, string[]>(); try { if (File.Exists(LocalFile)) foreach (var l in File.ReadAllLines(LocalFile, Encoding.UTF8)) { var a = l.Split('\t'); if (a.Length >= 6) o[a[0]] = a.Skip(1).ToArray(); } } catch { }
            return o;
        }
        public static void SaveLocal()
        {
            try { File.WriteAllLines(LocalFile, P.Where(p => p.So != "").Select(p => string.Join("\t", new[] { p.So, p.Ld, p.Chu, p.DiaChi, p.Dtpl.HasValue ? p.Dtpl.Value.ToString(IC) : "", p.GhiChu }.Select(x => (x ?? "").Replace('\t', ' ')).ToArray())).ToArray(), Encoding.UTF8); } catch { }
        }

        // ================= NHÃN THỬA =================
        // delOld: nhãn cũ trong thửa chuyển sang lớp nhãn cũ (mặc định 49) – hoặc xoá khi lớp nhãn cũ = 0
        public static void DrawLabels(IEnumerable<Parcel> list, bool delOld) { DrawLabels(list, delOld, null); }
        // goc: hình và số thửa của thửa gốc (tách / hợp) – ô nhãn cũ của thửa gốc tìm thêm theo hình (ONhanCu) trước khi vẽ nhãn mới
        public static void DrawLabels(IEnumerable<Parcel> list, bool delOld, IEnumerable<KeyValuePair<List<double[]>, string>> goc)
        {
            var c = Cfg.I; double h = c.H; var ds = list.ToList(); NhanCuN = 0; NhanCuXoa = 0; Ms.LevelLoi = 0; Ms.LevelMsg = ""; Ms.LopMoi.Clear();
            var theoHinh = delOld && goc != null ? ONhanCu(goc) : new List<MEl>();
            HashSet<string> da = delOld && c.NhanCuLop > 0 && (theoHinh.Count > 0 || ds.Any(p => p.Labels.Count > 0)) ? NhanTrenLopCu() : null; var xong = new HashSet<long>();
            if (theoHinh.Count > 0) { if (c.NhanCuLop > 0) NhanCuN += Ms.LevelAll(theoHinh, c.NhanCuLop, xong); else NhanCuXoa += Ms.DeleteAll(theoHinh, xong); }
            foreach (var p in ds)
            {
                if (delOld) BoNhanCu(p, da, xong); p.Labels.Clear();
                if (c.Kieu == KieuNhan.Ba_lop_chuan) { Bt.Lab3(p); continue; }
                double x = p.Lp[0], y = p.Lp[1]; string dt = FNhan(p.Dt);
                var els = new List<Bentley.Interop.MicroStationDGN.Element>(); var txt = new List<string>(); var pos = new List<double[]>();
                Action<Bentley.Interop.MicroStationDGN.Element, string, double, double> put = (e, s1, x1, y1) => { els.Add(e); txt.Add(s1); pos.Add(new[] { x1, y1 }); };
                if (c.Kieu == KieuNhan.Phan_so)
                {
                    string so = p.So == "" ? "?" : p.So; double w = Math.Max(so.Length, dt.Length) * h * .75 + h * .4;
                    if (p.Ld != "") { double lx = x + w / 2 + h * .3 + p.Ld.Length * h * .35; put(Ms.MakeText(p.Ld, lx, y, h * .9, 0, c.LdLop, c.LdMau), p.Ld, lx, y); }
                    put(Ms.MakeText(so, x, y + h * .85, h, 0, c.SoLop, c.SoMau), so, x, y + h * .85); put(Ms.MakeLine(new[] { x - w / 2, y, x + w / 2, y }, c.SoLop, c.SoMau, 0), null, x, y);
                    put(Ms.MakeText(dt, x, y - h * .85, h, 0, c.DtLop, c.DtMau), dt, x, y - h * .85);
                }
                else
                {
                    var rows = new List<object[]> { new object[] { p.So == "" ? "?" : p.So, c.SoLop, c.SoMau }, new object[] { dt, c.DtLop, c.DtMau } }; if (p.Ld != "") rows.Add(new object[] { p.Ld, c.LdLop, c.LdMau });
                    double sp = h * 1.45; for (int i = 0; i < rows.Count; i++) { double yy = y + ((rows.Count - 1) / 2.0 - i) * sp; put(Ms.MakeText((string)rows[i][0], x, yy, h, 0, (int)rows[i][1], (int)rows[i][2]), (string)rows[i][0], x, yy); }
                }
                Bt.PutCell(p, els, txt, pos, x, y);
            }
            Notify();
        }
        static void Add(Parcel p, Bentley.Interop.MicroStationDGN.Element e) { p.Labels.Add(new MEl { El = e, Kind = 17 }); }
        // nhãn cũ ở lần vẽ nhãn gần nhất: số phần tử đã chuyển sang lớp nhãn cũ, số phần tử đã xoá
        public static int NhanCuN, NhanCuXoa;
        public static string NhanCuMoTa { get { return Cfg.I.NhanCuLop > 0 ? "nhãn cũ chuyển sang level " + Cfg.I.NhanCuLop : "xoá nhãn cũ"; } }
        public static string NhanCuBao() { return (NhanCuN > 0 ? " · " + NhanCuN + " phần tử nhãn cũ chuyển sang level " + Cfg.I.NhanCuLop : NhanCuXoa > 0 ? " · xoá " + NhanCuXoa + " phần tử nhãn cũ" : "") + (Ms.LevelLoi > 0 ? " · ⚠ " + Ms.LevelLoi + " phần tử nhãn cũ chưa chuyển được level" + (Ms.LevelMsg != "" ? " (" + Ms.LevelMsg + ")" : "") + " – chọn nhãn đó, đổi level bằng tay" : "") + Ms.BaoLopMoi(); }
        // tách / hợp thửa không vẽ nhãn mới: nhãn cũ của thửa vẫn chuyển sang lớp nhãn cũ (hoặc xoá khi lớp nhãn cũ = 0)
        public static void ChuyenNhanCu(IEnumerable<Parcel> list) { ChuyenNhanCu(list, null); }
        public static void ChuyenNhanCu(IEnumerable<Parcel> list, IEnumerable<KeyValuePair<List<double[]>, string>> goc)
        {
            var ds = list.ToList(); NhanCuN = 0; NhanCuXoa = 0; Ms.LevelLoi = 0; Ms.LevelMsg = "";
            var theoHinh = goc != null ? ONhanCu(goc) : new List<MEl>();
            if (theoHinh.Count == 0 && !ds.Any(p => p.Labels.Count > 0)) return;
            HashSet<string> da = Cfg.I.NhanCuLop > 0 ? NhanTrenLopCu() : null; var xong = new HashSet<long>();
            if (theoHinh.Count > 0) { if (Cfg.I.NhanCuLop > 0) NhanCuN += Ms.LevelAll(theoHinh, Cfg.I.NhanCuLop, xong); else NhanCuXoa += Ms.DeleteAll(theoHinh, xong); }
            foreach (var p in ds) { BoNhanCu(p, da, xong); p.Labels.Clear(); }
            Notify();
        }
        // ô nhãn cũ (cell N_THUA…) của thửa gốc tìm theo hình: tâm các chữ của ô nằm trong hình thửa gốc, ô có chữ đúng số thửa gốc và mọi chữ của ô
        // đều là phần của nhãn (số thửa, diện tích, loại đất) – bổ sung cho nhãn đã gán vào thửa mới (chữ nhãn rơi đúng ranh tách, ngoài thửa mới…)
        public static List<MEl> ONhanCu(IEnumerable<KeyValuePair<List<double[]>, string>> goc)
        {
            var o = new List<MEl>(); var ds = goc.Where(g => g.Key != null && g.Key.Count > 0 && g.Key[0].Length >= 6 && !string.IsNullOrEmpty(g.Value)).ToList(); if (ds.Count == 0) return o;
            var lv = new HashSet<int>(Cfg.Lvs(Cfg.I.LvNhan)); lv.Add(Cfg.I.SoLop); lv.Add(Cfg.I.DtLop); lv.Add(Cfg.I.LdLop); lv.Remove(Cfg.I.NhanCuLop); foreach (var r in Cfg.Lvs(Cfg.I.LvRanh)) lv.Remove(r);
            List<MEl> tp; try { tp = Ms.Read(lv, new[] { 17 }, true).Where(m => m.Parent != null).ToList(); } catch (Exception ex) { Program.Log(ex); return o; }
            foreach (var nhom in tp.GroupBy(m => Ms.Id(m.Parent)))
            {
                if (nhom.Key == 0) continue; var L = nhom.ToList();
                if (!L.All(m => LaNhan((m.Text ?? "").Trim(), null))) continue;
                double cx = L.Average(m => m.Cx), cy = L.Average(m => m.Cy);
                if (ds.Any(g => Geo.InPoly(cx, cy, g.Key[0]) && !g.Key.Skip(1).Any(hh => Geo.InPoly(cx, cy, hh)) && L.Any(m => Gtp.Norm((m.Text ?? "").Trim()) == Gtp.Norm(g.Value)))) o.AddRange(L);
            }
            return o;
        }
        static string KhoaNhan(MEl t) { return (t.Text ?? "").Trim() + "@" + Math.Round(t.Cx * 100).ToString(IC) + "," + Math.Round(t.Cy * 100).ToString(IC); }
        // chữ đang có trên lớp nhãn cũ (để vẽ lại nhãn nhiều lần không chồng thêm bản sao giống hệt lên lớp nhãn cũ)
        static HashSet<string> NhanTrenLopCu()
        {
            var o = new HashSet<string>(); try { foreach (var t in Ms.Read(new[] { Cfg.I.NhanCuLop }, new[] { 17, 7 }, true)) if (!string.IsNullOrEmpty(t.Text)) o.Add(KhoaNhan(t)); } catch (Exception ex) { Program.Log(ex); }
            return o;
        }
        // nhãn cũ của thửa khi vẽ nhãn mới: chuyển sang lớp nhãn cũ (giữ nguyên chữ, màu, vị trí). Nhãn giống hệt (cùng chữ, cùng chỗ) đã có trên lớp nhãn cũ thì xoá
        // để lớp nhãn cũ không chồng nhiều bản giống nhau; lớp nhãn cũ = 0: xoá như các bản trước
        static void BoNhanCu(Parcel p, HashSet<string> da, HashSet<long> xong)
        {
            var L = p.Labels.Where(m => m.El != null || m.Parent != null).ToList(); if (L.Count == 0) return;
            if (Cfg.I.NhanCuLop <= 0) { NhanCuXoa += Ms.DeleteAll(L, xong); return; }
            var chu = L.Where(m => m.Kind != 3 && !string.IsNullOrEmpty(m.Text)).ToList();
            if (da != null && chu.Count > 0 && chu.All(m => da.Contains(KhoaNhan(m)))) { NhanCuXoa += Ms.DeleteAll(L, xong); return; }
            NhanCuN += Ms.LevelAll(L, Cfg.I.NhanCuLop, xong);
            if (da != null) foreach (var m in chu) da.Add(KhoaNhan(m));
        }
        public static int DeleteLabels() { int n = 0; foreach (var p in P) { n += Ms.DeleteAll(p.Labels); p.Labels.Clear(); } Notify(); return n; }
        public static void AutoNumber(int start, bool onlyNew, bool zigzag)
        {
            var list = onlyNew ? P.Where(p => p.So == "").ToList() : P.ToList(); if (list.Count == 0) return;
            int n = onlyNew ? P.Select(p => ToInt(p.So)).DefaultIfEmpty(0).Max() + 1 : start;
            var hs = list.Select(p => p.BB[3] - p.BB[1]).OrderBy(v => v).ToList(); double band = hs[hs.Count / 2];
            var rows = new List<List<Parcel>>(); double ry = 0;
            foreach (var p in list.OrderByDescending(p => p.Lp[1])) { if (rows.Count > 0 && ry - p.Lp[1] < band * .8) rows[rows.Count - 1].Add(p); else { rows.Add(new List<Parcel> { p }); ry = p.Lp[1]; } }
            for (int i = 0; i < rows.Count; i++) { var R = rows[i].OrderBy(p => p.Lp[0]).ToList(); if (zigzag && i % 2 == 1) R.Reverse(); foreach (var p in R) { p.So = (n++).ToString(); SetGtp(p, "so", p.So); } }
            SaveLocal(); Commit(); Notify();
        }

        // ================= KIỂM TRA & SỬA LỖI RANH =================
        public static void Check()
        {
            Errs = new List<Err>(); var R = Topo; if (R == null) return;
            foreach (var d in R.Dangles) Errs.Add(new Err { Text = "Đầu treo (bắt chưa tới hoặc bắt quá)", X = d[0], Y = d[1] });
            foreach (var o in R.Overlaps) Errs.Add(new Err { Text = "Đoạn ranh vẽ chồng lên nhau", X = o[0], Y = o[1] });
            var cnt = P.Where(p => p.So != "").GroupBy(p => p.So).ToDictionary(g => g.Key, g => g.Count());
            foreach (var p in P)
            {
                if (p.So == "") Errs.Add(new Err { Text = "Thửa chưa có số thửa", X = p.Lp[0], Y = p.Lp[1], P = p }); else if (cnt[p.So] > 1) Errs.Add(new Err { Text = "Trùng số thửa " + p.So, X = p.Lp[0], Y = p.Lp[1], P = p });
                if (p.Dt < 1) Errs.Add(new Err { Text = "Thửa quá nhỏ (" + FD(p.Dt) + " m²) – có thể là lỗi ranh", X = p.Lp[0], Y = p.Lp[1], P = p });
                if (p.DtNhan != null && Math.Abs(p.DtNhan.Value - Math.Round(p.Dt, Cfg.I.DtLe)) > 0.5 * Math.Pow(10, -Cfg.I.DtLe) + 1e-6) Errs.Add(new Err { Text = "Diện tích nhãn " + FD(p.DtNhan.Value) + " khác bản đồ " + FD(p.Dt) + " m²", X = p.Lp[0], Y = p.Lp[1], P = p });
            }
        }
        public static string Fix(bool under, bool over, bool dup) { if (Topo == null) return "Chưa tạo thửa"; return FixOn(Ranh, Topo, under, over, dup); }
        // sửa lỗi trên tập đường ranh cho trước (đọc từ các lớp người dùng chọn).
        // Chỉ sửa ĐẦU HỞ của đường (đỉnh đầu / đỉnh cuối không nối với gì): không động tới đỉnh nào khác, không bao giờ lấy chính đường đó làm đích,
        // không gộp đỉnh, không xoá đoạn nào của đường:
        //  · bắt quá (over): đoạn cuối cắt qua một đường khác rồi thò ra không quá khoảng sửa → đưa đầu đường về đúng giao điểm (giao điểm phải nằm trong đoạn cuối);
        //  · bắt chưa tới (under): kéo dài đầu đường THEO ĐÚNG HƯỚNG đoạn cuối của nó tới đường khác gần nhất trong khoảng sửa (sát đỉnh đường kia thì lấy đúng đỉnh đó);
        //  · đoạn trùng (dup): xoá đoạn thẳng 2 điểm trùng hệt một đoạn khác cùng lớp.
        public static string FixOn(List<MEl> Ranh, Planar R, bool under, bool over, bool dup)
        {
            double tol = Math.Max(Cfg.I.Tol, 1e-4), D = Cfg.I.FixTol; int nu = 0, no = 0, nd = 0;
            // các đoạn của mọi đường; Tag = chỉ số đường trong Ranh (để loại chính đường đang sửa)
            var all = new List<Seg>(); var cuoi = new Dictionary<int, Seg[]>();
            for (int k = 0; k < Ranh.Count; k++)
            {
                var m = Ranh[k]; foreach (var l in m.Lines) for (int i = 2; i < l.Length; i += 2) all.Add(new Seg(l[i - 2], l[i - 1], l[i], l[i + 1], k));
                if ((m.Kind == 3 || m.Kind == 4) && m.Lines.Count == 1 && m.Lines[0].Length >= 4) { int c = all.Count, nseg = m.Lines[0].Length / 2 - 1; cuoi[k] = new[] { all[c - nseg], all[c - 1] }; }
            }
            var daSua = new HashSet<long>();
            for (int n = 0; n < R.Nodes.Count && (under || over); n++)
            {
                if (R.Degree[n] != 1) continue; var N = R.Nodes[n];
                // đường hở có đầu mút tại nút này
                int k = -1, vi = -1;
                for (int j = 0; j < Ranh.Count && k < 0; j++)
                {
                    if (!cuoi.ContainsKey(j)) continue; var l = Ranh[j].Lines[0]; int c = l.Length;
                    if (Geo.D2(l[0], l[1], N[0], N[1]) <= tol * tol) { k = j; vi = 0; }
                    else if (Geo.D2(l[c - 2], l[c - 1], N[0], N[1]) <= tol * tol) { k = j; vi = c / 2 - 1; }
                }
                if (k < 0 || !daSua.Add(((long)k << 20) | (uint)vi)) continue;
                var el = Ranh[k]; var L = el.Lines[0]; int last = L.Length / 2 - 1; if (last < 1) continue;
                // đoạn cuối a → b (b là đầu hở)
                double ax = vi == 0 ? L[2] : L[2 * last - 2], ay = vi == 0 ? L[3] : L[2 * last - 1], bx = L[2 * vi], by = L[2 * vi + 1], len = Geo.Dist(ax, ay, bx, by);
                if (len <= tol) continue;
                double[] dich = null; bool quaGiao = false;
                if (over)
                {
                    // giao điểm gần đầu hở nhất trên đoạn cuối, với đường khác đi xuyên qua (không phải đầu mút của đường kia)
                    double bt = -1;
                    foreach (var s in all)
                    {
                        if (s.Tag == k) continue; double x, y, t, u;
                        if (!Geo.SegX(ax, ay, bx, by, s.X1, s.Y1, s.X2, s.Y2, false, out x, out y, out t, out u)) continue;
                        double thua = (1 - t) * len; if (thua <= tol || thua > D || t * len <= tol) continue;
                        if (LaDauMut(Ranh[s.Tag], x, y, tol)) continue;
                        if (t > bt) { bt = t; dich = new[] { x, y }; }
                    }
                    quaGiao = dich != null;
                }
                if (dich == null && under)
                {
                    // tia từ đầu hở theo hướng a → b, dài tối đa khoảng sửa
                    double ux = (bx - ax) / len, uy = (by - ay) / len, ex = bx + ux * D, ey = by + uy * D, bt = 2; Seg sb = null;
                    foreach (var s in all)
                    {
                        if (s.Tag == k) continue; double x, y, t, u;
                        if (!Geo.SegX(bx, by, ex, ey, s.X1, s.Y1, s.X2, s.Y2, false, out x, out y, out t, out u)) continue;
                        if (t * D <= tol * 0.5) continue;
                        if (t < bt) { bt = t; dich = new[] { x, y }; sb = s; }
                    }
                    if (dich != null)
                    {
                        // sát đỉnh của đường đích (trong dung sai bắt nối) thì lấy đúng đỉnh đó – không để lại mẩu đoạn rất ngắn
                        foreach (var v in new[] { new[] { sb.X1, sb.Y1 }, new[] { sb.X2, sb.Y2 } }) if (Geo.D2(v[0], v[1], dich[0], dich[1]) <= tol * tol) { dich = v; break; }
                    }
                }
                if (dich == null) continue;
                if (!Ms.MoveVertex(el.El, vi, dich[0], dich[1])) continue;
                if (quaGiao) no++; else nu++;
                Upd(el, vi, dich);
                var sg = cuoi[k][vi == 0 ? 0 : 1]; if (vi == 0) { sg.X1 = dich[0]; sg.Y1 = dich[1]; } else { sg.X2 = dich[0]; sg.Y2 = dich[1]; }
            }
            if (dup)
            {
                var seen = new HashSet<string>();
                foreach (var m in Ranh)
                {
                    if (m.Kind != 3 || m.Lines.Count != 1 || m.Lines[0].Length != 4 || m.El == null) continue; var l = m.Lines[0];
                    long a = (long)Math.Round(l[0] * 1000), b = (long)Math.Round(l[1] * 1000), c = (long)Math.Round(l[2] * 1000), d = (long)Math.Round(l[3] * 1000);
                    // chỉ đoạn trùng trên cùng một lớp (ranh thửa trùng ranh giao thông ở lớp khác thì giữ cả hai)
                    string key = m.Lv + "|" + ((a < c || (a == c && b < d)) ? a + "," + b + "," + c + "," + d : c + "," + d + "," + a + "," + b); if (!seen.Add(key)) { Ms.Delete(m.El); nd++; }
                }
            }
            FixN = nu + no + nd;
            var bao = new List<string>(); if (under) bao.Add("nối " + nu + " đầu hở"); if (over) bao.Add("cắt " + no + " phần thừa"); if (dup) bao.Add("xoá " + nd + " đoạn trùng");
            return bao.Count == 0 ? "" : "Đã " + string.Join(", ", bao.ToArray());
        }
        // (x, y) là đỉnh đầu / đỉnh cuối của đường (không phải điểm đường đó đi xuyên qua)
        static bool LaDauMut(MEl m, double x, double y, double tol)
        {
            if (m.Kind != 3 && m.Kind != 4) return false;
            foreach (var l in m.Lines) { int c = l.Length; if (c < 4) continue; if (Geo.D2(l[0], l[1], x, y) <= tol * tol || Geo.D2(l[c - 2], l[c - 1], x, y) <= tol * tol) return true; }
            return false;
        }
        public static int FixN;
        static void Upd(MEl m, int vi, double[] p) { m.Lines[0][2 * vi] = p[0]; m.Lines[0][2 * vi + 1] = p[1]; }

        // ================= TÁCH / GỘP THỬA =================
        public static double[] ClipToRing(double[] line, double[] ring)
        {
            var ps = new List<double>(); int n = ring.Length / 2; double x, y, t, u;
            for (int i = 0; i + 3 < line.Length; i += 2) for (int j = 0; j < n; j++) { int k = (j + 1) % n; if (Geo.SegX(line[i], line[i + 1], line[i + 2], line[i + 3], ring[2 * j], ring[2 * j + 1], ring[2 * k], ring[2 * k + 1], false, out x, out y, out t, out u)) ps.Add(i / 2 + t); }
            ps.Sort(); if (ps.Count < 2) return null; double s0 = ps[0], s1 = ps[ps.Count - 1];
            Func<double, double[]> at = s => { int i = Math.Min((int)Math.Floor(s), line.Length / 2 - 2); double tt = s - i; return new[] { line[2 * i] + (line[2 * i + 2] - line[2 * i]) * tt, line[2 * i + 1] + (line[2 * i + 3] - line[2 * i + 1]) * tt }; };
            var o = new List<double>(at(s0)); for (int i = (int)Math.Floor(s0) + 1; i < s1; i++) { o.Add(line[2 * i]); o.Add(line[2 * i + 1]); } o.AddRange(at(s1)); return o.ToArray();
        }
        public static string Split(Parcel p, double[] line)
        {
            var c = ClipToRing(line, p.Rings[0]); if (c == null) return "Đường chia phải cắt qua ranh thửa ít nhất 2 lần";
            var lr = Cfg.Lvs(Cfg.I.LvRanh); int lv = lr.Contains(Cfg.I.TachLop) || lr.Count == 0 ? Cfg.I.TachLop : lr[0];
            Ms.AddLine(c, lv, -1, -1); return Build(true);
        }
        public static string Merge(Parcel a, Parcel b)
        {
            var sh = new HashSet<string>(a.Edges.Where(b.Edges.Contains)); if (sh.Count == 0) return "Hai thửa không có cạnh chung";
            var R = Topo; if (R == null || a.FromGtp) return "Gộp thửa cần tạo thửa từ ranh trên bản đồ (Bản đồ → Tạo thửa)"; var tags = new HashSet<int>(sh.Select(k => R.EdgeByKey[k][2]));
            foreach (var id in tags)
            {
                var el = Ranh[id]; var keep = R.Edges.Where(e => e[2] == id && !sh.Contains(e[0] + "_" + e[1])).ToList();
                Ms.Delete(el.El); foreach (var pl in Planar.Chain(keep, R.Nodes)) if (pl.Length >= 4) Ms.AddLine(pl, el.Lv, el.Color, el.Weight);
            }
            string so = a.So, bs = b.So, ld = a.Ld, chu = a.Chu, dc = a.DiaChi; object gid = a.Gid; var row = a.Row; var lp = a.Lp;
            Ms.DeleteAll(a.Labels.Concat(b.Labels));
            Build(false); var m = Find(lp[0], lp[1]);
                if (m != null) { m.So = so; m.Ld = ld; m.Chu = chu; m.DiaChi = dc; m.Gid = gid; m.Row = row; SetGtp(m, "dt", Math.Round(m.Dt, Cfg.I.DtLe).ToString(IC)); Commit(); DrawLabels(new[] { m }, false); }
            return "Đã gộp thửa " + bs + " vào thửa " + so + (G != null ? " – thửa " + bs + " vẫn còn trong tệp thuộc tính, xử lý theo hồ sơ biến động" : "");
        }

        // ================= KÍCH THƯỚC CẠNH, SỐ HIỆU ĐỈNH, KHUNG, TÔ MÀU =================
        public static double[] Verts(Parcel p)
        {
            var r0 = p.Rings[0]; var cl = new double[r0.Length + 2]; r0.CopyTo(cl, 0); cl[r0.Length] = r0[0]; cl[r0.Length + 1] = r0[1];
            var r = Geo.Simplify(cl, Math.Max(Cfg.I.Tol, 0.005)); r = r.Take(r.Length - 2).ToArray();
            if (Geo.Area(r) > 0) { var o = new double[r.Length]; for (int i = 0, n = r.Length / 2; i < n; i++) { o[2 * i] = r[2 * (n - 1 - i)]; o[2 * i + 1] = r[2 * (n - 1 - i) + 1]; } r = o; }   // theo chiều kim đồng hồ
            int s = 0; double bv = double.MinValue; for (int i = 0; i < r.Length; i += 2) { double v = r[i + 1] - r[i] * .3; if (v > bv) { bv = v; s = i; } }   // bắt đầu từ đỉnh Tây Bắc
            return r.Skip(s).Concat(r.Take(s)).ToArray();
        }
        public static void Dims(IEnumerable<Parcel> list)
        {
            var c = Cfg.I; double h = c.H * .8;
            foreach (var p in list)
            {
                var v = Verts(p); int n = v.Length / 2; var ce = Geo.Centroid(v);
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n; double ax = v[2 * i], ay = v[2 * i + 1], bx = v[2 * j], by = v[2 * j + 1], L = Geo.Dist(ax, ay, bx, by); if (L < 0.05) continue;
                    double r = Math.Atan2(by - ay, bx - ax); if (r > Math.PI / 2) r -= Math.PI; if (r < -Math.PI / 2) r += Math.PI;
                    double mx = (ax + bx) / 2, my = (ay + by) / 2, nx = -Math.Sin(r), ny = Math.Cos(r); if ((ce[0] - mx) * nx + (ce[1] - my) * ny < 0) { nx = -nx; ny = -ny; }
                    Ms.AddText(F(L, 2), mx + nx * h * .8, my + ny * h * .8, h, r, c.KtLop, c.KtMau);
                }
            }
        }
        public static void VertNums(IEnumerable<Parcel> list)
        {
            var c = Cfg.I; double h = c.H * .7;
            foreach (var p in list)
            {
                var v = Verts(p); int n = v.Length / 2; var ce = Geo.Centroid(v);
                for (int i = 0; i < n; i++) { double x = v[2 * i], y = v[2 * i + 1], dx = x - ce[0], dy = y - ce[1], L = Math.Sqrt(dx * dx + dy * dy); if (L == 0) L = 1; Ms.AddCircle(x, y, h * .25, c.DinhLop, c.DinhMau); Ms.AddText((i + 1).ToString(), x + dx / L * h * 1.1, y + dy / L * h * 1.1, h, 0, c.DinhLop, c.DinhMau); }
            }
        }
        public static string Frame(double[] b)
        {
            var c = Cfg.I; double k = c.TyLe / 1000.0, g = 100 * k, mm = k; int L = c.KhungLop, C = c.KhungMau; double h = 2.5 * mm;
            b = new[] { Math.Floor(b[0] / g) * g, Math.Floor(b[1] / g) * g, Math.Ceiling(b[2] / g) * g, Math.Ceiling(b[3] / g) * g };
            Ms.AddShape(new[] { b[0], b[1], b[2], b[1], b[2], b[3], b[0], b[3] }, L, C, false);
            var ob = new[] { b[0] - 12 * mm, b[1] - 12 * mm, b[2] + 12 * mm, b[3] + 12 * mm }; var oe = Ms.AddShape(new[] { ob[0], ob[1], ob[2], ob[1], ob[2], ob[3], ob[0], ob[3] }, L, C, false); try { oe.LineWeight = 3; oe.Rewrite(); } catch { }
            for (double x = b[0] + g; x < b[2] - 1e-6; x += g) for (double y = b[1] + g; y < b[3] - 1e-6; y += g) { Ms.AddLine(new[] { x - 5 * mm, y, x + 5 * mm, y }, L, C, 0); Ms.AddLine(new[] { x, y - 5 * mm, x, y + 5 * mm }, L, C, 0); }
            for (double x = b[0]; x <= b[2] + 1e-6; x += g) { var t = x.ToString("F0", IC); Ms.AddText(t, x, b[1] - 5 * mm, h, 0, L, C); Ms.AddText(t, x, b[3] + 4 * mm, h, 0, L, C); Ms.AddLine(new[] { x, b[1], x, b[1] - 2 * mm }, L, C, 0); Ms.AddLine(new[] { x, b[3], x, b[3] + 2 * mm }, L, C, 0); }
            for (double y = b[1]; y <= b[3] + 1e-6; y += g) { var t = y.ToString("F0", IC); Ms.AddText(t, b[0] - 5 * mm, y, h, Math.PI / 2, L, C); Ms.AddText(t, b[2] + 5 * mm, y, h, Math.PI / 2, L, C); Ms.AddLine(new[] { b[0], y, b[0] - 2 * mm, y }, L, C, 0); Ms.AddLine(new[] { b[2], y, b[2] + 2 * mm, y }, L, C, 0); }
            double cx = (b[0] + b[2]) / 2;
            Ms.AddText("BẢN ĐỒ ĐỊA CHÍNH", cx, ob[3] + 16 * mm, 6 * mm, 0, L, C);
            Ms.AddText(c.Place.ToUpper(), cx, ob[3] + 8 * mm, 3.5 * mm, 0, L, C);
            Ms.AddText("Tờ số: " + To, b[2] - 10 * mm, ob[3] + 4 * mm, 3 * mm, 0, L, C);
            Ms.AddText("Tỷ lệ 1:" + c.TyLe, cx, ob[1] - 8 * mm, 3.5 * mm, 0, L, C);
            Ms.AddText("Hệ toạ độ VN-2000, kinh tuyến trục " + TachHop.DoPhut(c.Ktt).Replace("′", "'") + ", múi chiếu 3°", cx, ob[1] - 14 * mm, 2.5 * mm, 0, L, C);
            Ms.ZoomTo(ob[0] - 20 * mm, ob[1] - 25 * mm, ob[2] + 20 * mm, ob[3] + 25 * mm);
            return "Đã vẽ khung bản đồ địa chính tờ " + To;
        }
        public static readonly Dictionary<string, int> MauNhom = new Dictionary<string, int> { { "lua", 4 }, { "hn", 6 }, { "ln", 30 }, { "rung", 2 }, { "nn", 7 }, { "o", 3 }, { "gt", 9 }, { "nuoc", 1 }, { "csd", 10 }, { "pnn", 5 }, { "", 0 } };
        public static string Nhom(string k)
        {
            k = Regex.Split((k ?? "").ToUpperInvariant(), "[^A-Z]+").FirstOrDefault(s => s != "") ?? "";
            if (Regex.IsMatch(k, "^(LUC|LUK|LUN)$")) return "lua"; if (Regex.IsMatch(k, "^(HNK|BHK|NHK|COC)$")) return "hn"; if (Regex.IsMatch(k, "^(CLN|LNQ|LNC|LNK)$")) return "ln";
            if (k.StartsWith("R")) return "rung"; if (Regex.IsMatch(k, "^(NTS|TSN|LMU|CNT|NKH)$")) return "nn"; if (Regex.IsMatch(k, "^(ONT|ODT)$")) return "o"; if (k == "DGT") return "gt";
            if (Regex.IsMatch(k, "^(DTL|SON|MNC)$")) return "nuoc"; if (Regex.IsMatch(k, "^(BCS|DCS|NCS)$")) return "csd"; return k == "" ? "" : "pnn";
        }
        public static int FillColors()
        {
            if (Cfg.Lvs(Cfg.I.LvRanh).Contains(Cfg.I.ToLop)) throw new Exception("Level tô màu trùng level ranh thửa – chọn level khác trong Thiết lập");
            foreach (var o in Ms.Read(new[] { Cfg.I.ToLop }, new[] { 6, 14 })) Ms.Delete(o.El);   // xoá vùng tô cũ
            int n = 0; foreach (var p in P) { Ms.AddShape(p.Rings[0], Cfg.I.ToLop, MauNhom[Nhom(p.Ld)], true); n++; }
            return n;
        }

        // ================= TỆP THUỘC TÍNH =================
        public static void OpenGtp(string path) { OpenGtp(path, null); }
        // pre: tệp đã mở sẵn (bảng kết nối vừa đọc để xem trước) – dùng luôn, không giải mã lại
        // kết nối tệp thuộc tính. Tệp bị bản trước của công cụ ghi thiếu (gCadas báo lỗi DBNull khi mở bảng thuộc tính) được sửa ngay:
        // SuaGtp ghi lại những gì đã sửa để báo người dùng; bật "Lưu thẳng vào tệp .gtp" thì lưu luôn (bản cũ giữ thành .gtp.bak)
        public static string SuaGtp = "";
        // ký hiệu loại đất chưa có mã số trong tệp (đã tạm ghi mã dùng nhiều nhất): câu báo cho người dùng, rồi xoá danh sách
        public static string BaoMaTam()
        {
            if (G == null || G.MaTam.Count == 0) return ""; var s = " · ký hiệu " + string.Join(", ", G.MaTam.Distinct().ToArray()) + " chưa có mã số loại đất trong tệp: tạm ghi mã loại đất dùng nhiều nhất, kiểm tra lại loại đất chi tiết trong gCadas";
            G.MaTam.Clear(); return s;
        }
        public static void OpenGtp(string path, Gtp pre)
        {
            if (G != null) { Commit(); G.Dispose(); } G = pre != null && string.Equals(pre.Path_, path, StringComparison.OrdinalIgnoreCase) ? pre : Gtp.Open(path);
            SuaGtp = ""; try { SuaGtp = G.SuaChoGcadas(); } catch (Exception ex) { Program.Log(ex); }
            GRows = G.Rows(); if (P.Count > 0 && !P.Any(p => p.FromGtp)) Link(); else if (P.Count > 0) FromGtp();
            // thửa đã liên kết với bản đồ mà tệp chưa có hình thửa: ghi hình theo ranh trên bản đồ
            try
            {
                var dem = G.Has("ThuaDat", "geom") ? G.Query("select count(*) as n, count(geom) as g from ThuaDat").First() : null;
                long tong = dem == null ? 0 : Convert.ToInt64(dem["n"]), co = dem == null ? 0 : Convert.ToInt64(dem["g"]);
                if (co < tong && (tong - co) * 10 < tong)   // gCadas luôn ghi hình thửa: thiếu ở vài thửa là do bản trước "Cập nhật … thêm thửa"
                {
                    var thieu = new HashSet<string>(G.Query("select thuaDatId as id from ThuaDat where geom is null").Select(r => Convert.ToString(r["id"], IC))); int n = 0;
                    foreach (var p in P) if (p.Gid != null && !p.Virtual && p.Rings != null && thieu.Contains(Convert.ToString(p.Gid, IC))) { G.Update(p.Gid, "geom", Gtp.Wkb(p.Rings)); n++; }
                    if (n > 0) SuaGtp += (SuaGtp != "" ? "; " : "") + n + " thửa thiếu hình thửa";
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            if (SuaGtp != "" && Cfg.I.TuLuu && !G.ChangedOnDisk) try { G.Save(G.Path_); } catch (Exception ex) { Program.Log(ex); }
            Notify();
        }
        // tệp thuộc tính đi kèm bản đồ: cùng tên, cùng thư mục
        public static string GtpFor(string dgn) { try { var f = Path.ChangeExtension(dgn, ".gtp"); return File.Exists(f) ? f : null; } catch { return null; } }
        // dòng của tờ bản đồ đang mở (liên kết thửa trên bản đồ). Kết nối độc lập: tệp không gắn với tờ bản đồ – không có dòng nào của tờ này
        public static IEnumerable<GRow> SheetRows() { if (DocLap) return new List<GRow>(); var to = To; var L = GRows.Where(r => to == "" || r.To == to).ToList(); return L.Count > 0 ? L : GRows; }
        // ================= KẾT NỐI TỆP THUỘC TÍNH ĐỘC LẬP =================
        // tệp thuộc tính (thường là tệp toàn xã đã gộp) kết nối riêng, không gắn với tờ bản đồ: bảng thuộc tính hiện đủ mọi tờ, mọi thửa;
        // không liên kết thửa trên bản đồ, không ghi gì từ bản đồ vào tệp (tạo thửa, cập nhật, tách / hợp, cắt ranh)
        static Gtp gDocLap;
        public static bool DocLap { get { return G != null && ReferenceEquals(G, gDocLap); } }
        public static void OpenGtpDocLap(string path)
        {
            if (G != null) { try { Commit(); } catch (Exception ex) { Program.Log(ex); } G.Dispose(); G = null; }
            foreach (var p in P) { p.Gid = null; p.Row = null; } P.RemoveAll(p => p.FromGtp);
            G = Gtp.Open(path); gDocLap = G; SuaGtp = ""; try { SuaGtp = G.SuaChoGcadas(); } catch (Exception ex) { Program.Log(ex); }
            GRows = G.Rows();
            if (SuaGtp != "" && Cfg.I.TuLuu && !G.ChangedOnDisk) try { G.Save(G.Path_); } catch (Exception ex) { Program.Log(ex); }
            Notify();
        }
        // các tờ trong tệp (số tờ xếp theo số) và số thửa mỗi tờ
        public static List<KeyValuePair<string, int>> CacTo()
        {
            Func<string, double> so = t => { double v; return double.TryParse(t, NumberStyles.Float, IC, out v) ? v : 1e12; };
            return GRows.GroupBy(r => r.To ?? "").Select(g => new KeyValuePair<string, int>(g.Key, g.Count())).OrderBy(kv => so(kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        }
        // thửa dựng từ một dòng của tệp (có hình thì theo hình; không có hình vẫn mở được thông tin thửa)
        public static Parcel RowParcel(GRow r)
        {
            var p = Virtual(r); if (p != null) return p;
            return new Parcel { Row = r, Gid = r.Id, So = r.So, Ld = r.Kh ?? "", Chu = r.Chu ?? "", DiaChi = r.DiaChi ?? "", GhiChu = r.GhiChu ?? "", Dtpl = r.Dtpl, Dt = r.Dt ?? 0, FromGtp = true, Virtual = true,
                Lp = r.TamX.HasValue && r.TamY.HasValue ? new[] { r.TamX.Value, r.TamY.Value } : new double[] { 0, 0 }, Edges = new HashSet<string>() };
        }
        public static void Link()
        {
            if (G == null) return; if (DocLap) { foreach (var p in P) { p.Gid = null; p.Row = null; } return; }
            var rows = SheetRows().ToList(); var pg = new PGrid(P); var used = new HashSet<object>(); foreach (var p in P) { p.Gid = null; p.Row = null; }
            foreach (var r in rows)
            {
                if (r.TamX == null || r.TamY == null) continue;
                foreach (var xy in new[] { new[] { r.TamX.Value, r.TamY.Value }, new[] { r.TamY.Value, r.TamX.Value } }) { var p = pg.Find(xy[0], xy[1]); if (p != null && p.Row == null) { p.Row = r; p.Gid = r.Id; used.Add(r); break; } }
            }
            var bySo = new Dictionary<string, GRow>(); foreach (var r in rows) if (!used.Contains(r) && !bySo.ContainsKey(r.So)) bySo[r.So] = r;
            foreach (var p in P) { if (p.Row != null || p.So == "") continue; GRow r; if (bySo.TryGetValue(p.So, out r)) { p.Row = r; p.Gid = r.Id; bySo.Remove(p.So); } }
            bool learned = false;
            foreach (var p in P)
            {
                var r = p.Row; if (r == null || r.Ghep != "" || p.Ld == "" || p.FromGtp) continue; var codes = p.Ld.Split('+', ',').Select(x => x.Trim()).Where(x => x != "").ToArray();
                var ms = r.Mds.OrderByDescending(m => m.Chinh).ToList(); if (codes.Length != ms.Count) continue;
                for (int i = 0; i < codes.Length; i++) if (ms[i].Loai.HasValue && ms[i].Code == null) { LoaiDatMa.Vote(ms[i].Loai.Value, codes[i]); learned = true; }
            }
            if (learned) { LoaiDatMa.Save(); foreach (var r in GRows) Gtp.Refresh(r); }
            foreach (var p in P) { var r = p.Row; if (r == null) continue; if (r.So != "") p.So = r.So; if (r.Kh != "" && !(r.Kh.Contains("#") && p.Ld != "")) p.Ld = r.Kh; if (r.Chu != "") p.Chu = r.Chu; if (r.DiaChi != "") p.DiaChi = r.DiaChi; if (r.Dtpl != null) p.Dtpl = r.Dtpl; if (r.GhiChu != "") p.GhiChu = r.GhiChu; }
        }
        // ghi một trường của thửa vào tệp thuộc tính (nếu đã liên kết)
        // ghi một trường của thửa vào tệp thuộc tính (nếu đã liên kết)
        public static void SetGtp(Parcel p, string k, string v)
        {
            if (G == null || p.Gid == null) return; var r = p.Row; v = (v ?? "").Trim();
            switch (k)
            {
                case "so": G.Update(p.Gid, "soThuTuThua", v); if (r != null) r.So = Gtp.Norm(v); break;
                case "to": G.Update(p.Gid, "soHieuToBanDo", v); if (r != null) r.To = Gtp.Norm(v); break;
                case "dt": G.Update(p.Gid, "dienTich", v); G.DtMotLoai(p.Gid, v); if (r != null) r.Dt = Num(v); break;
                case "dtpl": G.Update(p.Gid, "dienTichPhapLy", v); if (r != null) r.Dtpl = Num(v); break;
                case "diachi": G.Update(p.Gid, "diaChi", v); if (r != null) r.DiaChi = v; break;
                case "ghichu": G.Update(p.Gid, "ghiChu", v); if (r != null) r.GhiChu = v; break;
                case "ld":
                    {
                        v = v.ToUpperInvariant().Replace(" ", ""); G.Update(p.Gid, "mucDichSuDungGhep", v == "" ? null : v.Replace("+", " + "));
                        // một mục đích: ghi luôn mã số loại đất vào bảng mục đích sử dụng (nếu đã biết mã)
                        var codes = v.Split('+', ',').Where(x => x != "").ToArray();
                        if (r != null && codes.Length == 1 && r.Mds.Count == 1) { var id = LoaiDatMa.Id(codes[0]); if (id.HasValue) foreach (var c in new[] { "loaiMucDichSuDungId", "loaiMucDichSuDungKiemKeId", "loaiMucDichSuDungQuyHoachId" }) G.Set("DaMucDichSuDung", "daMucDichSuDungId", r.Mds[0].Id, c, id.Value); if (id.HasValue) r.Mds[0].Loai = id; }
                        if (r != null) { r.Ghep = v; Gtp.Refresh(r); }
                        break;
                    }
                case "chu":
                    if (r != null && r.OwnerIds.Count > 0) G.Set("Nguoi", "nguoiId", r.OwnerIds[0], "hoTen", v);
                    else if (v != "") { var nid = G.AddOwner(p.Gid, v); if (r != null) r.OwnerIds.Add(nid); }
                    if (r != null) r.Chu = v; break;
                case "chudc": if (r != null && r.OwnerIds.Count > 0) { G.Set("Nguoi", "nguoiId", r.OwnerIds[0], "diaChi", v); r.ChuDc = v; } break;
                case "namsinh": if (r != null && r.OwnerIds.Count > 0) { G.Set("Nguoi", "nguoiId", r.OwnerIds[0], "namSinh", v); r.NamSinh = v; } break;
            }
        }
        // ghi tệp thuộc tính xuống đĩa (nếu bật tự lưu). Hold > 0: đang sửa hàng loạt, lưu một lần ở cuối
        public static int Hold;
        public static string Commit()
        {
            if (G == null || !G.Dirty || !Cfg.I.TuLuu || Hold > 0) return "";
            if (G.ChangedOnDisk) throw new Exception("Tệp " + G.Name + " vừa bị chương trình khác sửa – không ghi đè. Đóng chương trình đó rồi mở lại tệp thuộc tính.");
            G.Save(G.Path_); return "đã lưu vào " + G.Name;
        }
        public static void SetField(Parcel p, string k, string v)
        {
            v = (v ?? "").Trim(); if (k == "ld") v = v.ToUpperInvariant().Replace(" ", "");
            if (k == "so") p.So = v; else if (k == "ld") p.Ld = v; else if (k == "chu") p.Chu = v; else if (k == "diachi") p.DiaChi = v; else if (k == "ghichu") p.GhiChu = v; else if (k == "dtpl") p.Dtpl = Num(v);
            SetGtp(p, k, v); if (!p.Virtual) SaveLocal(); Commit();
        }
        // thửa dựng từ hình trong tệp thuộc tính (dùng khi chưa đọc được ranh trên bản đồ)
        public static Parcel Virtual(GRow r)
        {
            if (r.Rings == null || r.Rings.Count == 0 || r.Rings[0].Length < 6) return null;
            var p = new Parcel { Rings = r.Rings, BB = Geo.BBox(r.Rings[0]), Row = r, Gid = r.Id, So = r.So, Ld = r.Kh ?? "", Chu = r.Chu ?? "", DiaChi = r.DiaChi ?? "", GhiChu = r.GhiChu ?? "", Dtpl = r.Dtpl, FromGtp = true, Virtual = true };
            p.Dt = Area(p); p.Lp = r.TamX.HasValue && r.TamY.HasValue && Geo.InPoly(r.TamX.Value, r.TamY.Value, r.Rings[0]) ? new[] { r.TamX.Value, r.TamY.Value } : Geo.LabelPt(r.Rings);
            p.Edges = new HashSet<string>(); foreach (var ring in r.Rings) { int n = ring.Length / 2; for (int i = 0; i < n; i++) { int j = (i + 1) % n; string a = K(ring[2 * i], ring[2 * i + 1]), b = K(ring[2 * j], ring[2 * j + 1]); p.Edges.Add(string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a); } }
            return p;
        }
        static string K(double x, double y) { return Math.Round(x * 100).ToString(IC) + "," + Math.Round(y * 100).ToString(IC); }
        public static string FromGtp()
        {
            if (G == null) return "Chưa mở tệp thuộc tính";
            if (DocLap) return "Tệp thuộc tính " + G.Name + " đang kết nối độc lập (mọi tờ, không theo bản đồ) – không dựng thửa theo hình trong tệp lên tờ bản đồ";
            P = SheetRows().Select(Virtual).Where(p => p != null).ToList(); int k = 0; foreach (var p in P) { p.I = k++; p.Virtual = false; }
            Topo = null; Ranh = new List<MEl>(); Dirty = false; try { ReadLabels(); } catch { }   // nhãn đang có trên bản đồ (để vẽ lại nhãn không bị trùng)
            Notify();
            return "Đã dựng " + P.Count + " thửa theo hình trong tệp " + G.Name + " (tờ " + To + ")";
        }
        public static string Push(bool so, bool dt, bool ld, bool tam, bool geom, bool add)
        {
            if (G == null) return "Chưa mở tệp thuộc tính"; if (DocLap) return DocLapBao("cập nhật từ bản đồ vào tệp"); int n = 0, a = 0; var T = G.Cols["ThuaDat"];
            foreach (var p in P)
            {
                double dtv = Math.Round(p.Dt, Cfg.I.DtLe);
                if (p.Gid == null)
                {
                    if (!add || p.So == "") continue; var f = new Dictionary<string, object> { { "soThuTuThua", (long)ToInt(p.So) }, { "dienTich", dtv } };
                    if (To != "") f["soHieuToBanDo"] = (long)ToInt(To); if (p.Ld != "") f["mucDichSuDungGhep"] = p.Ld.Replace("+", " + "); if (T.Contains("TamX")) { f["TamX"] = p.Lp[0]; f["TamY"] = p.Lp[1]; }
                    if (T.Contains("geom") && !p.Virtual && p.Rings != null) f["geom"] = Gtp.Wkb(p.Rings);
                    // thửa mới luôn có loại đất chi tiết (gCadas cần), diện tích các loại cộng lại bằng diện tích thửa
                    try { p.Gid = G.NewParcel(f); G.EnsureMd(p.Gid); G.CanDienTichMd(p.Gid, dtv, false); a++; } catch (Exception ex) { Program.Log(ex); }
                    continue;
                }
                if (so && p.So != "") SetGtp(p, "so", p.So); if (dt) { G.Update(p.Gid, "dienTich", dtv); G.DtMotLoai(p.Gid, dtv); } if (ld && p.Ld != "") SetGtp(p, "ld", p.Ld);
                if (tam && T.Contains("TamX")) { G.Update(p.Gid, "TamX", p.Lp[0]); G.Update(p.Gid, "TamY", p.Lp[1]); }
                if (geom && T.Contains("geom")) G.Update(p.Gid, "geom", Gtp.Wkb(p.Rings)); n++;
            }
            GRows = G.Rows(); Link(); var sv = Commit(); Notify(); return string.Format("Đã cập nhật {0} thửa, thêm mới {1} thửa vào {2}", n, a, G.Name) + (sv != "" ? " – " + sv : " – nhớ bấm “Lưu tệp thuộc tính”") + BaoMaTam();
        }
        // ================= GHI THỬA VÀO TỆP THUỘC TÍNH SAU KHI TẠO THỬA =================
        // cập nhật diện tích, tâm, hình thửa; thêm thửa mới (đã có số); các đường ranh tạo thành từng vòng của thửa (ThuaDatTopo); lưu tệp
        // câu báo khi lệnh ghi từ bản đồ vào tệp gặp tệp đang kết nối độc lập
        public static string DocLapBao(string viec) { return "Tệp " + (G != null ? G.Name : "thuộc tính") + " đang kết nối độc lập (mọi tờ, không theo bản đồ) – không " + viec + ". Muốn ghi: Hệ thống → Kết nối tệp thuộc tính (.gtp) theo tờ bản đồ"; }
        public static string SyncGtp()
        {
            if (G == null) return ""; if (DocLap) return "tệp " + G.Name + " đang kết nối độc lập – không ghi thửa từ bản đồ vào tệp";
            if (G.ChangedOnDisk) throw new Exception("Tệp " + G.Name + " vừa bị chương trình khác sửa – không ghi đè. Đóng chương trình đó rồi kết nối lại tệp thuộc tính.");
            var T = G.Cols["ThuaDat"]; int up = 0, add = 0, skip = 0, topo = 0; bool hasTopo = G.Has("ThuaDatTopo", "thuaDatId", "ringId", "elementId");
            var sheet = new HashSet<string>(SheetRows().Select(r => r.So));
            foreach (var p in P)
            {
                if (p.Virtual || p.FromGtp) continue;
                double dtv = Math.Round(p.Dt, Cfg.I.DtLe);
                if (p.Gid == null)
                {
                    if (p.So == "" || sheet.Contains(p.So)) { skip++; continue; }
                    var f = new Dictionary<string, object> { { "soThuTuThua", (long)ToInt(p.So) }, { "dienTich", dtv } };
                    if (To != "") f["soHieuToBanDo"] = (long)ToInt(To);
                    if (p.Ld != "") f["mucDichSuDungGhep"] = p.Ld.Replace("+", " + ");
                    if (T.Contains("TamX")) { f["TamX"] = p.Lp[0]; f["TamY"] = p.Lp[1]; }
                    if (T.Contains("geom")) f["geom"] = Gtp.Wkb(p.Rings);
                    p.Gid = G.NewParcel(f); add++; sheet.Add(p.So);
                    // loại đất chi tiết: mỗi ký hiệu một dòng (một loại đất thì ghi cả diện tích thửa)
                    var codes = p.Ld.Split('+', ',').Select(x => x.Trim()).Where(x => x != "").ToArray();
                    for (int i = 0; i < codes.Length; i++) G.AddMd(p.Gid, LoaiDatMa.Id(codes[i]), codes.Length == 1 ? (double?)dtv : null, i == 0, p.So, To, codes[i]);
                    G.EnsureMd(p.Gid); G.CanDienTichMd(p.Gid, dtv, false);
                }
                else
                {
                    G.Update(p.Gid, "dienTich", dtv); G.DtMotLoai(p.Gid, dtv); G.EnsureMd(p.Gid);
                    if (T.Contains("TamX")) { G.Update(p.Gid, "TamX", p.Lp[0]); G.Update(p.Gid, "TamY", p.Lp[1]); }
                    if (T.Contains("geom")) G.Update(p.Gid, "geom", Gtp.Wkb(p.Rings));
                    if (p.Row != null && p.Row.So == "" && p.So != "") G.Update(p.Gid, "soThuTuThua", p.So);
                    up++;
                }
                if (hasTopo) topo += GhiTopo(p);
            }
            GRows = G.Rows(); Link(); string sv = "";
            if (G.Dirty) { G.Save(G.Path_); sv = " · đã lưu " + G.Name; }
            Notify();
            return string.Format("Tệp thuộc tính: cập nhật {0} thửa, thêm mới {1} thửa{2}{3}{4}", up, add, topo > 0 ? ", " + topo + " liên kết đường ranh" : "", skip > 0 ? ", " + skip + " thửa chưa ghi (chưa có số hoặc trùng số trong tờ)" : "", sv) + BaoMaTam();
        }
        // các đường ranh (mã phần tử) tạo thành từng vòng của thửa → bảng ThuaDatTopo; trả về số liên kết đã ghi
        public static int GhiTopo(Parcel p)
        {
            if (G == null || DocLap || p.Gid == null || p.RingEdges == null || Topo == null || !G.Has("ThuaDatTopo", "thuaDatId", "ringId", "elementId")) return 0; int n = 0;
            G.Db.Exec("delete from ThuaDatTopo where thuaDatId=?", p.Gid);
            for (int r = 0; r < p.RingEdges.Count; r++)
            {
                var ids = new List<long>(); var seen = new HashSet<long>();
                foreach (var k in p.RingEdges[r]) { int[] e; if (Topo.EdgeByKey.TryGetValue(k, out e) && e[2] >= 0 && e[2] < Ranh.Count) { long id = Ms.Id(Ranh[e[2]].El); if (id > 0 && seen.Add(id)) ids.Add(id); } }
                foreach (var id in ids) { G.Db.Exec("insert into ThuaDatTopo (id, thuaDatId, ringId, elementId) values ((select coalesce(max(id),0)+1 from ThuaDatTopo), ?, ?, ?)", p.Gid, (long)r, id); n++; }
            }
            G.Dirty = true; return n;
        }
        // mã phần tử các đường ranh tạo thành thửa (để biết thửa nào cần ghi lại ThuaDatTopo khi đường ranh đổi)
        public static HashSet<long> TopoIds(Parcel p)
        {
            var o = new HashSet<long>(); if (p.RingEdges == null || Topo == null) return o;
            foreach (var re in p.RingEdges) foreach (var k in re) { int[] e; if (Topo.EdgeByKey.TryGetValue(k, out e) && e[2] >= 0 && e[2] < Ranh.Count) { long id = Ms.Id(Ranh[e[2]].El); if (id > 0) o.Add(id); } }
            return o;
        }

        // ================= SAU KHI CẮT RANH TẠI GIAO ĐIỂM =================
        // cắt trên chính các đường của mạng ranh đang dùng (Ranh): phần tử cũ thay bằng các đoạn mới, mỗi cạnh của mạng ranh trỏ sang đoạn chứa nó
        // – các thửa đang dựng giữ nguyên, liên kết thửa – đường ranh ghi theo mã phần tử mới
        public static void ThayRanh(CatKq kq)
        {
            if (Topo == null || kq == null || kq.Thay.Count == 0) return;
            var moi = new Dictionary<int, List<int>>();
            foreach (var kv in kq.Thay)
            {
                if (kv.Key < 0 || kv.Key >= Ranh.Count) continue; var L = new List<int>();
                foreach (var m in kv.Value) { m.Idx = Ranh.Count; Ranh.Add(m); L.Add(m.Idx); }
                Ranh[kv.Key].El = null; Ranh[kv.Key].Lines = new List<double[]>(); moi[kv.Key] = L;
            }
            foreach (var e in Topo.Edges)
            {
                List<int> L; if (!moi.TryGetValue(e[2], out L) || L.Count == 0) continue;
                var a = Topo.Nodes[e[0]]; var b = Topo.Nodes[e[1]]; double mx = (a[0] + b[0]) / 2, my = (a[1] + b[1]) / 2, bd = double.MaxValue; int best = L[0];
                foreach (int i in L) { double d = DGap(Ranh[i].Lines[0], mx, my); if (d < bd) { bd = d; best = i; } }
                e[2] = best;
            }
            Dirty = true;
        }
        static double DGap(double[] l, double x, double y) { double m = double.MaxValue, t, qx, qy; for (int i = 0; i + 3 < l.Length; i += 2) m = Math.Min(m, Geo.SegPt(x, y, l[i], l[i + 1], l[i + 2], l[i + 3], out t, out qx, out qy)); return m; }
        // các thửa trong tệp thuộc tính đang trỏ tới đường ranh vừa thay (ThuaDatTopo): ghi lại theo mã phần tử mới rồi lưu tệp
        // dung: dựng lại thửa theo ranh mới trước (khi mạng ranh đang có không phải mạng vừa cắt)
        public static string LamMoiTopo(CatKq kq, bool dung)
        {
            if (G == null || DocLap || kq == null || kq.IdCu.Count == 0 || !G.Has("ThuaDatTopo", "thuaDatId", "ringId", "elementId")) return "";
            var thua = new HashSet<string>();
            try { foreach (var r in G.Query("select thuaDatId as t, elementId as e from ThuaDatTopo")) { long e; if (r["e"] != null && long.TryParse(Convert.ToString(r["e"], IC), out e) && kq.IdCu.Contains(e)) thua.Add(Convert.ToString(r["t"], IC)); } }
            catch (Exception ex) { Program.Log(ex); return "chưa đọc được liên kết thửa – đường ranh: " + ex.Message; }
            if (thua.Count == 0) return "";
            if (G.ChangedOnDisk) return "⚠ tệp " + G.Name + " vừa bị chương trình khác sửa – chưa ghi lại liên kết đường ranh của " + thua.Count + " thửa (kết nối lại tệp rồi Tạo thửa)";
            if (dung) Build(false);
            int n = 0; var xong = new HashSet<string>();
            foreach (var p in P) { if (p.Gid == null || p.Virtual || p.FromGtp) continue; var k = Convert.ToString(p.Gid, IC); if (!thua.Contains(k) || !xong.Add(k)) continue; GhiTopo(p); n++; }
            if (n > 0 && G.Dirty) G.Save(G.Path_);
            int con = thua.Count - n;
            return "ghi lại liên kết đường ranh của " + n + " thửa vào " + G.Name + (con > 0 ? " (" + con + " thửa chưa thấy trên bản đồ – chạy Tạo thửa để ghi lại)" : "");
        }
        public static List<KeyValuePair<string, double[]>> StatLD()
        {
            return P.GroupBy(p => (p.Ld == "" ? "(chưa có)" : p.Ld.Split('+', ',')[0].Trim())).Select(g => new KeyValuePair<string, double[]>(g.Key, new[] { g.Count(), g.Sum(p => p.Dt) })).OrderByDescending(kv => kv.Value[1]).ToList();
        }
        public static readonly Dictionary<string, string> LoaiDat = new Dictionary<string, string> {
            {"LUC","Đất chuyên trồng lúa"},{"LUK","Đất trồng lúa còn lại"},{"HNK","Đất trồng cây hàng năm khác"},{"BHK","Đất bằng trồng cây hàng năm khác"},{"NHK","Đất nương rẫy trồng cây hàng năm khác"},{"CLN","Đất trồng cây lâu năm"},
            {"RSX","Đất rừng sản xuất"},{"RPH","Đất rừng phòng hộ"},{"RDD","Đất rừng đặc dụng"},{"NTS","Đất nuôi trồng thủy sản"},{"NKH","Đất nông nghiệp khác"},{"ONT","Đất ở tại nông thôn"},{"ODT","Đất ở tại đô thị"},
            {"TSC","Đất xây dựng trụ sở cơ quan"},{"CQP","Đất quốc phòng"},{"CAN","Đất an ninh"},{"DGD","Đất xây dựng cơ sở giáo dục và đào tạo"},{"DYT","Đất xây dựng cơ sở y tế"},{"DVH","Đất xây dựng cơ sở văn hóa"},
            {"DTT","Đất xây dựng cơ sở thể dục, thể thao"},{"SKC","Đất cơ sở sản xuất phi nông nghiệp"},{"TMD","Đất thương mại, dịch vụ"},{"DGT","Đất công trình giao thông"},{"DTL","Đất công trình thủy lợi"},
            {"DNL","Đất công trình năng lượng"},{"DCH","Đất chợ"},{"DSH","Đất sinh hoạt cộng đồng"},{"DKV","Đất khu vui chơi, giải trí công cộng"},{"TON","Đất tôn giáo"},{"TIN","Đất tín ngưỡng"},
            {"NTD","Đất nghĩa trang, nhà tang lễ, cơ sở hỏa táng"},{"SON","Đất sông, ngòi, kênh, rạch, suối"},{"MNC","Đất có mặt nước chuyên dùng"},{"PNK","Đất phi nông nghiệp khác"},
            {"BCS","Đất bằng chưa sử dụng"},{"DCS","Đất đồi núi chưa sử dụng"},{"NCS","Núi đá không có rừng cây"},{"DRA","Đất công trình xử lý chất thải"},{"DBV","Đất công trình bưu chính, viễn thông"} };
        public static string LdName(string k) { return string.Join("; ", (k ?? "").Split('+', ',').Select(s => s.Trim()).Where(s => s != "").Select(s => LoaiDat.ContainsKey(s) ? LoaiDat[s] + " (" + s + ")" : s).ToArray()); }
    }
}
