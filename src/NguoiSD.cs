// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – người sử dụng đất: đủ các trường trên thẻ căn cước / căn cước công dân, quan hệ (chồng, vợ, chủ hộ,
// thành viên hộ, đồng sử dụng…) và hình thức sử dụng đất (riêng / chung – khoản 5 Điều 8 Thông tư 10/2024/TT-BTNMT).
// Ghi vào cột sẵn có của tệp thuộc tính (bảng Nguoi, ThuaDat); trường tệp chưa có cột thì ghi vào bảng phụ M8ProThongTin trong cùng tệp
// (không đổi cấu trúc bảng gốc). Chưa kết nối tệp thuộc tính: lưu trên máy cạnh dữ liệu thửa. © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace M8Pro
{
    // một trường thông tin cá nhân: khoá, tên hiện, các tên cột có thể có trong tệp thuộc tính, danh sách chọn (nếu có)
    public class TruongNguoi
    {
        public string K, L; public string[] Cot; public string[] Ds; public bool Dai; public int Rong;
        public TruongNguoi(string k, string l, int rong, params string[] cot) { K = k; L = l; Rong = rong; Cot = cot; }
    }
    public class NguoiSD
    {
        public object Id; public Dictionary<string, string> V = new Dictionary<string, string>();
        public string this[string k] { get { string s; return V.TryGetValue(k, out s) ? s ?? "" : ""; } set { V[k] = (value ?? "").Trim(); } }
        public string Ten { get { return this["ten"]; } }
        public bool Trong { get { return V.Values.All(x => string.IsNullOrWhiteSpace(x)) || (V.Where(kv => kv.Key != "qh" && kv.Key != "qt" && kv.Key != "lgt").All(kv => string.IsNullOrWhiteSpace(kv.Value))); } }
        public string Chup() { return string.Join("|", ChuSD.TRUONG.Select(t => this[t.K]).ToArray()) + "#" + Convert.ToString(Id, CultureInfo.InvariantCulture); }
    }
    public static class ChuSD
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        public static readonly string[] QUAN_HE = { "Người sử dụng đất", "Chồng", "Vợ", "Chủ hộ", "Thành viên hộ gia đình", "Người đồng sử dụng", "Người đại diện" };
        public static readonly string[] GIOI_TINH = { "Nam", "Nữ" };
        public static readonly string[] GIAY_TO = { "Thẻ căn cước", "Căn cước công dân", "Giấy chứng nhận căn cước", "Chứng minh nhân dân", "Hộ chiếu", "Giấy tờ của tổ chức" };
        // trường trên thẻ căn cước (Luật Căn cước 2023) và thẻ căn cước công dân; thứ tự như trên thẻ
        public static readonly TruongNguoi[] TRUONG = {
            new TruongNguoi("qh", "Vai trò", 130, "quanHe", "quanHeChuHo", "vaiTro") { Ds = QUAN_HE },
            new TruongNguoi("ten", "Họ, chữ đệm và tên", 190, "hoTen"),
            new TruongNguoi("gt", "Giới tính", 64, "gioiTinh") { Ds = GIOI_TINH },
            new TruongNguoi("ns", "Ngày sinh", 86, "ngaySinh", "namSinh"),
            new TruongNguoi("qt", "Quốc tịch", 84, "quocTich"),
            new TruongNguoi("lgt", "Loại giấy tờ", 130, "loaiGiayTo", "loaiGiayToTuyThan", "tenGiayTo") { Ds = GIAY_TO },
            new TruongNguoi("so", "Số định danh cá nhân", 120, "soDinhDanh", "soDinhDanhCaNhan", "soGiayTo", "soCCCD", "soCMND", "cmnd", "soGiayToTuyThan"),
            new TruongNguoi("nc", "Ngày cấp", 86, "ngayCap", "ngayCapGiayTo"),
            new TruongNguoi("noic", "Nơi cấp", 170, "noiCap", "noiCapGiayTo"),
            new TruongNguoi("ct", "Nơi cư trú", 240, "diaChi", "noiCuTru", "diaChiThuongTru", "noiThuongTru") { Dai = true },
        };
        public static TruongNguoi T(string k) { return TRUONG.First(t => t.K == k); }
        // hình thức sử dụng đất (khoản 5 Điều 8 Thông tư 10/2024/TT-BTNMT) – mã, tên hiện, quan hệ của từng người tự thêm
        public static readonly string[][] HINH_THUC = {
            new[] { "Sử dụng riêng – một cá nhân", "Người sử dụng đất" },
            new[] { "Sử dụng riêng – một tổ chức", "Người đại diện" },
            new[] { "Sử dụng riêng – một cộng đồng dân cư", "Người đại diện" },
            new[] { "Sử dụng riêng – một người gốc Việt Nam định cư ở nước ngoài", "Người sử dụng đất" },
            new[] { "Sử dụng chung – quyền sử dụng đất chung của vợ và chồng", "Chồng", "Vợ" },
            new[] { "Sử dụng chung – các thành viên có chung quyền sử dụng đất của hộ gia đình", "Chủ hộ", "Thành viên hộ gia đình" },
            new[] { "Sử dụng chung – nhiều cá nhân, tổ chức cùng sử dụng (đồng sử dụng)", "Người đồng sử dụng", "Người đồng sử dụng" },
        };
        public static string[] TenHinhThuc { get { return HINH_THUC.Select(h => h[0]).ToArray(); } }
        public static string[] VaiTro(string ht) { var h = HINH_THUC.FirstOrDefault(x => x[0] == ht); return h == null ? new string[0] : h.Skip(1).ToArray(); }
        public static bool Chung(string ht) { return (ht ?? "").StartsWith("Sử dụng chung"); }
        // quan hệ cho người thêm tiếp theo theo hình thức sử dụng
        public static string VaiTroThem(string ht, IList<NguoiSD> ds)
        {
            var v = VaiTro(ht); if (v.Length == 0) return ds.Count == 0 ? "Người sử dụng đất" : "Người đồng sử dụng";
            if (ds.Count < v.Length) return v[ds.Count];
            if (ht.Contains("hộ gia đình")) return "Thành viên hộ gia đình";
            return Chung(ht) ? "Người đồng sử dụng" : v[0];
        }
        // ---------- tệp thuộc tính ----------
        const string PHU = "M8ProThongTin";
        static string Cot(Gtp G, string bang, IEnumerable<string> ten)
        {
            List<string> L; if (G == null || !G.Cols.TryGetValue(bang, out L)) return null;
            foreach (var t in ten) { var c = L.FirstOrDefault(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)); if (c != null) return c; }
            return null;
        }
        static bool LaSo(Gtp G, string bang, string cot) { string ty; return G.Types.ContainsKey(bang) && G.Types[bang].TryGetValue(cot, out ty) && (ty.Contains("int") || ty.Contains("real") || ty.Contains("float") || ty.Contains("double") || ty.Contains("num") || ty == "bit"); }
        // cột của trường trong bảng Nguoi; cột kiểu số chỉ nhận trường số (năm sinh), quan hệ ghi theo từng thửa nên không dùng cột của Nguoi
        static string CotNguoi(Gtp G, TruongNguoi t)
        {
            if (t.K == "qh") return null;
            if (t.K == "ns") { var c = Cot(G, "Nguoi", new[] { "ngaySinh" }); if (c != null && !LaSo(G, "Nguoi", c)) return c; return null; }
            var k = Cot(G, "Nguoi", t.Cot); if (k == null) return null; if (LaSo(G, "Nguoi", k)) return null; return k;   // số định danh có số 0 đầu: cột kiểu số không giữ được
        }
        static void TaoPhu(Gtp G)
        {
            if (G.Cols.ContainsKey(PHU)) return;
            G.Db.Exec("create table if not exists " + Sqlite.Q(PHU) + " (bang text not null, khoa text not null, truong text not null, giaTri text, primary key (bang, khoa, truong))");
            G.Cols[PHU] = new List<string> { "bang", "khoa", "truong", "giaTri" }; G.Types[PHU] = new Dictionary<string, string> { { "bang", "text" }, { "khoa", "text" }, { "truong", "text" }, { "giaTri", "text" } }; G.Dirty = true;
        }
        static string S(object v) { if (v == null || v is DBNull) return ""; if (v is double) { var d = (double)v; if (d == Math.Floor(d) && Math.Abs(d) < 1e15) return ((long)d).ToString(IC); } return Convert.ToString(v, IC); }
        static Dictionary<string, string> DocPhu(Gtp G, string bang, string khoa)
        {
            var o = new Dictionary<string, string>(); if (G == null || !G.Cols.ContainsKey(PHU)) return o;
            foreach (var r in G.Query("select truong, giaTri from " + Sqlite.Q(PHU) + " where bang=? and khoa=?", bang, khoa)) o[S(r["truong"])] = S(r["giaTri"]);
            return o;
        }
        static void GhiPhu(Gtp G, string bang, string khoa, string truong, string v)
        {
            if (string.IsNullOrEmpty(v)) { if (G.Cols.ContainsKey(PHU)) { G.Db.Exec("delete from " + Sqlite.Q(PHU) + " where bang=? and khoa=? and truong=?", bang, khoa, truong); G.Dirty = true; } return; }
            TaoPhu(G); G.Db.Exec("insert or replace into " + Sqlite.Q(PHU) + " (bang, khoa, truong, giaTri) values (?,?,?,?)", bang, khoa, truong, v); G.Dirty = true;
        }
        static string Khoa(object thuaDatId, object nguoiId) { return S(thuaDatId) + "|" + S(nguoiId); }
        public static bool CoBang(Gtp G) { return G != null && G.Has("DangKyThua", "thuaDatId", "nguoiId") && G.Has("Nguoi", "nguoiId", "hoTen"); }
        // người sử dụng của một thửa (theo thứ tự đăng ký)
        public static List<NguoiSD> Doc(Gtp G, object thuaDatId)
        {
            var o = new List<NguoiSD>(); if (!CoBang(G) || thuaDatId == null) return o;
            var pk = G.Cols["DangKyThua"][0]; var seen = new HashSet<string>();
            foreach (var r in G.Query("select n.* from DangKyThua k join Nguoi n on n.nguoiId=k.nguoiId where k.thuaDatId=? order by k." + Sqlite.Q(pk), thuaDatId))
            {
                var id = S(r["nguoiId"]); if (!seen.Add(id)) continue; o.Add(TuDong(G, thuaDatId, r));
            }
            return o;
        }
        static NguoiSD TuDong(Gtp G, object thuaDatId, Dictionary<string, object> r)
        {
            var n = new NguoiSD { Id = r["nguoiId"] }; var phu = DocPhu(G, "Nguoi", S(r["nguoiId"])); var qh = DocPhu(G, "DangKyThua", Khoa(thuaDatId, r["nguoiId"]));
            foreach (var t in TRUONG)
            {
                var c = CotNguoi(G, t); string v = c != null && r.ContainsKey(c) ? S(r[c]) : "";
                string p; if (v == "" && phu.TryGetValue(t.K, out p)) v = p;
                if (t.K == "ns" && v == "") { var ns = Cot(G, "Nguoi", new[] { "namSinh" }); if (ns != null && r.ContainsKey(ns)) v = S(r[ns]); }
                if (t.K == "qh" && qh.TryGetValue("qh", out p)) v = p;
                n[t.K] = v;
            }
            return n;
        }
        // mọi người sử dụng của mọi thửa trong tệp (bảng thuộc tính): thuaDatId → danh sách người
        public static Dictionary<string, List<NguoiSD>> TatCa(Gtp G)
        {
            var o = new Dictionary<string, List<NguoiSD>>(); if (!CoBang(G)) return o; var pk = G.Cols["DangKyThua"][0];
            var phu = new Dictionary<string, Dictionary<string, string>>();
            if (G.Cols.ContainsKey(PHU)) foreach (var r in G.Query("select bang, khoa, truong, giaTri from " + Sqlite.Q(PHU) + " where bang in ('Nguoi','DangKyThua')"))
                { var k = S(r["bang"]) + ":" + S(r["khoa"]); Dictionary<string, string> d; if (!phu.TryGetValue(k, out d)) phu[k] = d = new Dictionary<string, string>(); d[S(r["truong"])] = S(r["giaTri"]); }
            Func<string, Dictionary<string, string>> P = k => { Dictionary<string, string> d; return phu.TryGetValue(k, out d) ? d : new Dictionary<string, string>(); };
            foreach (var r in G.Query("select k.thuaDatId as _thua, n.* from DangKyThua k join Nguoi n on n.nguoiId=k.nguoiId order by k." + Sqlite.Q(pk)))
            {
                var t0 = S(r["_thua"]); List<NguoiSD> ds; if (!o.TryGetValue(t0, out ds)) o[t0] = ds = new List<NguoiSD>();
                var id = S(r["nguoiId"]); if (ds.Any(x => S(x.Id) == id)) continue;
                var n = new NguoiSD { Id = r["nguoiId"] }; var pn = P("Nguoi:" + id); var pq = P("DangKyThua:" + t0 + "|" + id);
                foreach (var t in TRUONG)
                {
                    var c = CotNguoi(G, t); string v = c != null && r.ContainsKey(c) ? S(r[c]) : ""; string p;
                    if (v == "" && pn.TryGetValue(t.K, out p)) v = p;
                    if (t.K == "ns" && v == "") { var ns = Cot(G, "Nguoi", new[] { "namSinh" }); if (ns != null && r.ContainsKey(ns)) v = S(r[ns]); }
                    if (t.K == "qh" && pq.TryGetValue("qh", out p)) v = p;
                    n[t.K] = v;
                }
                ds.Add(n);
            }
            return o;
        }
        // ghi một trường của một người (bảng thuộc tính sửa trực tiếp trên ô)
        public static void GhiMot(Gtp G, object thuaDatId, object nguoiId, string k, string v)
        {
            v = (v ?? "").Trim(); var t = T(k);
            if (k == "qh") { GhiPhu(G, "DangKyThua", Khoa(thuaDatId, nguoiId), "qh", v); return; }
            if (k == "ten") { G.Set("Nguoi", "nguoiId", nguoiId, "hoTen", v); return; }
            var c = CotNguoi(G, t);
            if (c != null) { G.Set("Nguoi", "nguoiId", nguoiId, c, v); GhiPhu(G, "Nguoi", S(nguoiId), k, ""); }
            else GhiPhu(G, "Nguoi", S(nguoiId), k, v);
            // ngày sinh: tệp chỉ có cột năm sinh thì ghi năm vào cột đó, ngày đủ ở bảng phụ
            if (k == "ns") { var ns = Cot(G, "Nguoi", new[] { "namSinh" }); if (ns != null && c == null) { var m = Regex.Match(v, @"(\d{4})"); G.Set("Nguoi", "nguoiId", nguoiId, ns, m.Success ? m.Groups[1].Value : v); } }
        }
        // ghi danh sách người của thửa: thêm người mới, sửa người cũ, bỏ người đã xoá khỏi thửa
        public static void Ghi(Gtp G, object thuaDatId, IList<NguoiSD> ds, IEnumerable<object> xoa)
        {
            if (!CoBang(G)) throw new Exception("Tệp không có bảng chủ sử dụng");
            foreach (var id in xoa ?? new object[0])
            {
                G.Db.Exec("delete from DangKyThua where thuaDatId=? and nguoiId=?", thuaDatId, id); GhiPhu(G, "DangKyThua", Khoa(thuaDatId, id), "qh", "");
                var con = G.Query("select 1 as x from DangKyThua where nguoiId=? limit 1", id).Any();
                if (!con) { G.Db.Exec("delete from Nguoi where nguoiId=?", id); if (G.Cols.ContainsKey(PHU)) G.Db.Exec("delete from " + Sqlite.Q(PHU) + " where bang='Nguoi' and khoa=?", S(id)); }
                G.Dirty = true;
            }
            foreach (var n in ds)
            {
                if (n.Trong && n.Id == null) continue;
                if (n.Id == null || S(n.Id) == "") n.Id = G.AddOwner(thuaDatId, n.Ten);
                foreach (var t in TRUONG) GhiMot(G, thuaDatId, n.Id, t.K, n[t.K]);
            }
        }
        // ---------- hình thức sử dụng của thửa ----------
        public static string DocHT(Gtp G, object thuaDatId)
        {
            if (G == null || thuaDatId == null) return "";
            var c = Cot(G, "ThuaDat", new[] { "hinhThucSuDung", "hinhThucSD" });
            if (c != null && !LaSo(G, "ThuaDat", c)) { var r = G.Query("select " + Sqlite.Q(c) + " as v from ThuaDat where thuaDatId=?", thuaDatId).FirstOrDefault(); if (r != null && S(r["v"]) != "") return S(r["v"]); }
            string v; return DocPhu(G, "ThuaDat", S(thuaDatId)).TryGetValue("ht", out v) ? v : "";
        }
        public static Dictionary<string, string> TatCaHT(Gtp G)
        {
            var o = new Dictionary<string, string>(); if (G == null) return o;
            if (G.Cols.ContainsKey(PHU)) foreach (var r in G.Query("select khoa, giaTri from " + Sqlite.Q(PHU) + " where bang='ThuaDat' and truong='ht'")) o[S(r["khoa"])] = S(r["giaTri"]);
            var c = Cot(G, "ThuaDat", new[] { "hinhThucSuDung", "hinhThucSD" });
            if (c != null && !LaSo(G, "ThuaDat", c)) foreach (var r in G.Query("select thuaDatId as k, " + Sqlite.Q(c) + " as v from ThuaDat")) if (S(r["v"]) != "") o[S(r["k"])] = S(r["v"]);
            return o;
        }
        public static void GhiHT(Gtp G, object thuaDatId, string v)
        {
            var c = Cot(G, "ThuaDat", new[] { "hinhThucSuDung", "hinhThucSD" });
            if (c != null && !LaSo(G, "ThuaDat", c)) { G.Set("ThuaDat", "thuaDatId", thuaDatId, c, v ?? ""); GhiPhu(G, "ThuaDat", S(thuaDatId), "ht", ""); }
            else GhiPhu(G, "ThuaDat", S(thuaDatId), "ht", v ?? "");
        }
        // ---------- chưa kết nối tệp thuộc tính: lưu trên máy, theo số thửa của tờ bản đồ đang mở ----------
        static Dictionary<string, KeyValuePair<string, List<NguoiSD>>> local; static string localTep;
        static string TepMay { get { return Dc.LocalFile + ".nguoi"; } }
        static void NapMay()
        {
            var f = TepMay; if (local != null && localTep == f) return; local = new Dictionary<string, KeyValuePair<string, List<NguoiSD>>>(); localTep = f;
            try
            {
                if (!File.Exists(f)) return;
                foreach (var l in File.ReadAllLines(f, Encoding.UTF8))
                {
                    var a = l.Split('\t'); if (a.Length < 2) continue; var ds = new List<NguoiSD>();
                    for (int i = 2; i < a.Length; i++) { var n = new NguoiSD(); var v = a[i].Split('\u001f'); for (int j = 0; j < TRUONG.Length && j < v.Length; j++) n[TRUONG[j].K] = v[j]; ds.Add(n); }
                    local[a[0]] = new KeyValuePair<string, List<NguoiSD>>(a[1], ds);
                }
            }
            catch (Exception ex) { Program.Log(ex); }
        }
        public static List<NguoiSD> DocMay(string so, out string ht)
        {
            NapMay(); KeyValuePair<string, List<NguoiSD>> kv; ht = "";
            if (so == null || !local.TryGetValue(so, out kv)) return new List<NguoiSD>();
            ht = kv.Key; return kv.Value.Select(n => { var c = new NguoiSD(); foreach (var x in n.V) c.V[x.Key] = x.Value; return c; }).ToList();
        }
        public static void GhiMay(string so, string soCu, string ht, IList<NguoiSD> ds)
        {
            NapMay(); if (!string.IsNullOrEmpty(soCu) && soCu != so) local.Remove(soCu); if (string.IsNullOrEmpty(so)) return;
            local[so] = new KeyValuePair<string, List<NguoiSD>>(ht ?? "", ds.Where(n => !n.Trong).ToList());
            try
            {
                Func<string, string> s = x => (x ?? "").Replace('\t', ' ').Replace('\u001f', ' ').Replace('\r', ' ').Replace('\n', ' ');
                File.WriteAllLines(TepMay, local.Select(kv => s(kv.Key) + "\t" + s(kv.Value.Key) + string.Concat(kv.Value.Value.Select(n => "\t" + string.Join("\u001f", TRUONG.Select(t => s(n[t.K])).ToArray())).ToArray())).ToArray(), Encoding.UTF8);
            }
            catch (Exception ex) { Program.Log(ex); }
        }
    }
}
