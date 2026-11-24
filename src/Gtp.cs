// M8 Pro Địa chính – đọc/ghi tệp thuộc tính .gtp (SQLite, có thể đã mã hoá XOR). © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace M8Pro
{
    // SQLite qua winsqlite3.dll (có sẵn trên Windows 10/11) hoặc sqlite3.dll đặt cạnh chương trình
    public sealed class Sqlite : IDisposable
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr LoadLibrary(string f);
        [DllImport("kernel32", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr h, string n);
        [DllImport("libdl.so.2", EntryPoint = "dlopen")] static extern IntPtr dlopen(string f, int m);
        [DllImport("libdl.so.2", EntryPoint = "dlsym")] static extern IntPtr dlsym(IntPtr h, string n);
        static bool Nix { get { return Environment.OSVersion.Platform == PlatformID.Unix; } }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int D_open(byte[] f, out IntPtr db, int flags, IntPtr vfs);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int D_close(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int D_prep(IntPtr db, byte[] sql, int n, out IntPtr st, IntPtr tail);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int D_st(IntPtr st);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int D_sti(IntPtr st, int i);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr D_stip(IntPtr st, int i);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long D_stil(IntPtr st, int i);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate double D_stid(IntPtr st, int i);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int D_bt(IntPtr st, int i, byte[] v, int n, IntPtr d);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int D_bd(IntPtr st, int i, double v);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int D_bl(IntPtr st, int i, long v);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr D_err(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long D_lid(IntPtr db);
        static D_open f_open; static D_close f_close; static D_prep f_prep; static D_st f_step, f_fin, f_cc; static D_sti f_ctype, f_cbytes; static D_stip f_cname, f_ctext, f_cblob;
        static D_stil f_cint; static D_stid f_cdbl; static D_bt f_btext, f_bblob; static D_bd f_bdbl; static D_bl f_bint; static D_sti f_bnull; static D_err f_err; static D_lid f_lid;
        // winsqlite3.dll bản 32-bit dùng quy ước gọi stdcall; sqlite3.dll và mọi bản 64-bit dùng cdecl
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int S_open(byte[] f, out IntPtr db, int flags, IntPtr vfs);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int S_close(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int S_prep(IntPtr db, byte[] sql, int n, out IntPtr st, IntPtr tail);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int S_st(IntPtr st);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int S_sti(IntPtr st, int i);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr S_stip(IntPtr st, int i);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate long S_stil(IntPtr st, int i);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate double S_stid(IntPtr st, int i);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int S_bt(IntPtr st, int i, byte[] v, int n, IntPtr d);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int S_bd(IntPtr st, int i, double v);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int S_bl(IntPtr st, int i, long v);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr S_err(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate long S_lid(IntPtr db);
        static T G<T>(IntPtr h, string n) where T : class { var p = Nix ? dlsym(h, n) : GetProcAddress(h, n); if (p == IntPtr.Zero) throw new Exception("Thiếu hàm " + n); return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T; }
        static void Load()
        {
            if (f_open != null) return; IntPtr h = IntPtr.Zero; bool win = false;
            if (Nix) { h = dlopen("libsqlite3.so.0", 2); if (h == IntPtr.Zero) throw new Exception("Thiếu libsqlite3"); goto cdecl; }
            var local = Path.Combine(Path.GetDirectoryName(typeof(Sqlite).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory, "sqlite3.dll");
            if (File.Exists(local)) h = LoadLibrary(local);
            if (h == IntPtr.Zero) { h = LoadLibrary("winsqlite3.dll"); win = h != IntPtr.Zero; }
            if (h == IntPtr.Zero) h = LoadLibrary("sqlite3.dll");
            if (h == IntPtr.Zero) throw new Exception("Không tìm thấy thư viện SQLite. Windows 10/11 có sẵn winsqlite3.dll; với Windows 7 hãy đặt sqlite3.dll (bản 32-bit nếu chạy trong MicroStation V8i) cạnh M8ProDiaChinh.dll.");
            if (win && IntPtr.Size == 4)
            {
                var o = G<S_open>(h, "sqlite3_open_v2"); f_open = (byte[] f, out IntPtr db, int fl, IntPtr v) => o(f, out db, fl, v);
                var c = G<S_close>(h, "sqlite3_close"); f_close = x => c(x);
                var pr = G<S_prep>(h, "sqlite3_prepare_v2"); f_prep = (IntPtr db, byte[] q, int n, out IntPtr st, IntPtr t) => pr(db, q, n, out st, t);
                var a1 = G<S_st>(h, "sqlite3_step"); f_step = x => a1(x); var a2 = G<S_st>(h, "sqlite3_finalize"); f_fin = x => a2(x); var a3 = G<S_st>(h, "sqlite3_column_count"); f_cc = x => a3(x);
                var b1 = G<S_sti>(h, "sqlite3_column_type"); f_ctype = (x, i) => b1(x, i); var b2 = G<S_sti>(h, "sqlite3_column_bytes"); f_cbytes = (x, i) => b2(x, i); var b3 = G<S_sti>(h, "sqlite3_bind_null"); f_bnull = (x, i) => b3(x, i);
                var c1 = G<S_stip>(h, "sqlite3_column_name"); f_cname = (x, i) => c1(x, i); var c2 = G<S_stip>(h, "sqlite3_column_text"); f_ctext = (x, i) => c2(x, i); var c3 = G<S_stip>(h, "sqlite3_column_blob"); f_cblob = (x, i) => c3(x, i);
                var d1 = G<S_stil>(h, "sqlite3_column_int64"); f_cint = (x, i) => d1(x, i); var d2 = G<S_stid>(h, "sqlite3_column_double"); f_cdbl = (x, i) => d2(x, i);
                var e1 = G<S_bt>(h, "sqlite3_bind_text"); f_btext = (x, i, v, n, d) => e1(x, i, v, n, d); var e2 = G<S_bt>(h, "sqlite3_bind_blob"); f_bblob = (x, i, v, n, d) => e2(x, i, v, n, d);
                var g1 = G<S_bd>(h, "sqlite3_bind_double"); f_bdbl = (x, i, v) => g1(x, i, v); var g2 = G<S_bl>(h, "sqlite3_bind_int64"); f_bint = (x, i, v) => g2(x, i, v);
                var k1 = G<S_err>(h, "sqlite3_errmsg"); f_err = x => k1(x); var k2 = G<S_lid>(h, "sqlite3_last_insert_rowid"); f_lid = x => k2(x);
                return;
            }
        cdecl:
            f_open = G<D_open>(h, "sqlite3_open_v2"); f_close = G<D_close>(h, "sqlite3_close"); f_prep = G<D_prep>(h, "sqlite3_prepare_v2"); f_step = G<D_st>(h, "sqlite3_step"); f_fin = G<D_st>(h, "sqlite3_finalize");
            f_cc = G<D_st>(h, "sqlite3_column_count"); f_ctype = G<D_sti>(h, "sqlite3_column_type"); f_cbytes = G<D_sti>(h, "sqlite3_column_bytes"); f_cname = G<D_stip>(h, "sqlite3_column_name");
            f_ctext = G<D_stip>(h, "sqlite3_column_text"); f_cblob = G<D_stip>(h, "sqlite3_column_blob"); f_cint = G<D_stil>(h, "sqlite3_column_int64"); f_cdbl = G<D_stid>(h, "sqlite3_column_double");
            f_btext = G<D_bt>(h, "sqlite3_bind_text"); f_bblob = G<D_bt>(h, "sqlite3_bind_blob"); f_bdbl = G<D_bd>(h, "sqlite3_bind_double"); f_bint = G<D_bl>(h, "sqlite3_bind_int64"); f_bnull = G<D_sti>(h, "sqlite3_bind_null");
            f_err = G<D_err>(h, "sqlite3_errmsg"); f_lid = G<D_lid>(h, "sqlite3_last_insert_rowid");
        }
        static byte[] U8(string s) { return Encoding.UTF8.GetBytes(s + "\0"); }
        static string S8(IntPtr p, int n) { if (p == IntPtr.Zero) return null; if (n < 0) { n = 0; while (Marshal.ReadByte(p, n) != 0) n++; } var b = new byte[n]; Marshal.Copy(p, b, 0, n); return Encoding.UTF8.GetString(b); }
        IntPtr db;
        public Sqlite(string file) { Load(); if (f_open(U8(file), out db, 2, IntPtr.Zero) != 0) throw new Exception("Không mở được CSDL: " + Err()); }
        string Err() { return S8(f_err(db), -1); }
        public void Dispose() { if (db != IntPtr.Zero) { f_close(db); db = IntPtr.Zero; } }
        IntPtr Prep(string sql, object[] ps)
        {
            IntPtr st; var b = Encoding.UTF8.GetBytes(sql); if (f_prep(db, b, b.Length, out st, IntPtr.Zero) != 0) throw new Exception(Err() + " — " + sql);
            if (ps != null) for (int i = 0; i < ps.Length; i++)
                {
                    var v = ps[i]; var T = new IntPtr(-1);
                    if (v == null || v is DBNull) f_bnull(st, i + 1);
                    else if (v is byte[]) { var a = (byte[])v; f_bblob(st, i + 1, a, a.Length, T); }
                    else if (v is int || v is long || v is short) f_bint(st, i + 1, Convert.ToInt64(v));
                    else if (v is double || v is float || v is decimal) f_bdbl(st, i + 1, Convert.ToDouble(v));
                    else { var a = Encoding.UTF8.GetBytes(Convert.ToString(v)); f_btext(st, i + 1, a, a.Length, T); }
                }
            return st;
        }
        public List<Dictionary<string, object>> Query(string sql, params object[] ps)
        {
            var o = new List<Dictionary<string, object>>(); var st = Prep(sql, ps);
            try
            {
                int nc = f_cc(st); var names = new string[nc]; for (int i = 0; i < nc; i++) names[i] = S8(f_cname(st, i), -1);
                while (true)
                {
                    int rc = f_step(st); if (rc == 101) break; if (rc != 100) throw new Exception(Err());
                    var r = new Dictionary<string, object>();
                    for (int i = 0; i < nc; i++)
                    {
                        int t = f_ctype(st, i); object v = null;
                        if (t == 1) v = f_cint(st, i); else if (t == 2) v = f_cdbl(st, i); else if (t == 3) v = S8(f_ctext(st, i), f_cbytes(st, i));
                        else if (t == 4) { int n = f_cbytes(st, i); var a = new byte[n]; if (n > 0) Marshal.Copy(f_cblob(st, i), a, 0, n); v = a; }
                        r[names[i]] = v;
                    }
                    o.Add(r);
                }
            }
            finally { f_fin(st); }
            return o;
        }
        public void Exec(string sql, params object[] ps) { var st = Prep(sql, ps); try { int rc = f_step(st); if (rc != 101 && rc != 100) throw new Exception(Err()); } finally { f_fin(st); } }
        public long LastId { get { return f_lid(db); } }
        public static string Q(string n) { return "\"" + n.Replace("\"", "\"\"") + "\""; }
    }

    // một mục đích sử dụng của thửa (bảng DaMucDichSuDung)
    public class GMd { public object Id; public long? Loai; public string Code; public double? Dt; public bool Chinh; }
    public class GRow
    {
        public object Id; public string To, So, Kh, Ghep = "", DiaChi, GhiChu, Chu, ChuDc, NamSinh = ""; public double? Dt, Dtpl, TamX, TamY;
        public List<object> OwnerIds = new List<object>();
        public List<GMd> Mds = new List<GMd>();
        public List<double[]> Rings;      // hình thửa trong tệp (cột geom)
        public Dictionary<string, object> Raw;
        // diện tích chi tiết theo mục đích sử dụng: “ONT: 100.0 m²; CLN: 250.0 m²” (mục đích chính đứng trước)
        public string MdText { get { return string.Join("; ", Mds.OrderByDescending(m => m.Chinh).Select(m => (m.Code ?? "#" + m.Loai) + (m.Dt.HasValue ? ": " + m.Dt.Value.ToString("F" + Cfg.I.DtLe, System.Globalization.CultureInfo.InvariantCulture) + " m²" : "")).ToArray()); } }
    }

    // mã số loại đất trong tệp .gtp ↔ ký hiệu (học dần từ dữ liệu, lưu tại %APPDATA%\M8Pro\mamucdich.txt)
    public static class LoaiDatMa
    {
        static Dictionary<long, Dictionary<string, int>> votes; static readonly Dictionary<long, string> seed = new Dictionary<long, string> { { 86, "LUC" }, { 90, "HNK" }, { 91, "BHK" }, { 93, "CLN" }, { 102, "RSM" }, { 120, "ONT" }, { 134, "DGT" } };
        static string F { get { var d = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "M8Pro"); Directory.CreateDirectory(d); return System.IO.Path.Combine(d, "mamucdich.txt"); } }
        static void Load()
        {
            if (votes != null) return; votes = new Dictionary<long, Dictionary<string, int>>();
            foreach (var kv in seed) Put(kv.Key, kv.Value, 3);
            try { if (File.Exists(F)) foreach (var l in File.ReadAllLines(F)) { var a = l.Split('\t'); long k; int n; if (a.Length == 3 && long.TryParse(a[0], out k) && int.TryParse(a[2], out n)) Put(k, a[1], n); } } catch { }
        }
        static void Put(long k, string c, int n) { Dictionary<string, int> d; if (!votes.TryGetValue(k, out d)) votes[k] = d = new Dictionary<string, int>(); int o; d.TryGetValue(c, out o); d[c] = Math.Max(o, n); }
        public static void Vote(long k, string c) { Load(); if (string.IsNullOrEmpty(c) || c.Length > 6) return; Dictionary<string, int> d; if (!votes.TryGetValue(k, out d)) votes[k] = d = new Dictionary<string, int>(); int o; d.TryGetValue(c, out o); d[c] = o + 1; }
        public static string Get(long k) { Load(); Dictionary<string, int> d; return votes.TryGetValue(k, out d) && d.Count > 0 ? d.OrderByDescending(x => x.Value).First().Key : null; }
        public static long? Id(string code) { Load(); foreach (var kv in votes) if (Get(kv.Key) == code) return kv.Key; return null; }
        public static void Save() { try { Load(); File.WriteAllLines(F, votes.SelectMany(kv => kv.Value.Select(x => kv.Key + "\t" + x.Key + "\t" + x.Value)).ToArray()); } catch { } }
    }

    public class Gtp : IDisposable
    {
        const string KEY64 =
            "D5tTAhM8nrp9N2mOnp9EZJXwQIUtDOnmxAFmQib1kQg//hEUi9CvWZtM82qeTdxeGr3uuvI2924lylY1nbHaLejx4UiNpYSoWhuRKjFIIow0i3HirTjSw9sX5xJ6dtFN4z06UXWj5jDzpJAzAefwLdEx9Hl37l8S" +
            "cwJKDHj3R84ea6EFxi8uz3xZ5m9bd/QGmsvopSnpVh1w2MePKKA5JCfTeEaUHqJmqZutwSdtnCOa6zoY2Mzupf+DB5wLGOnSX/f+uErcHfAWXQ4TfqV9/kQ6OIOO2YN8UnlzumA0svCyK2jfJJFpmajgEWecgvid" +
            "UlP+PKEOtqWq0Cka/xAzDrYV5hhomob4a3fcDRzIp5lLZ6OuE45QUXEXrUPTB1V5v+jY/IXUfk8z2sXXAuU96S4LlzyShoo4f8x6C5tEI45AfEaP7T5C7EvGGffCH8uEkwGelPHoHiHWnInxS/LfHZHRu4uahQUp" +
            "M8ak58dUu+6qPM9qO3EyPOQGwiaY/Yhk5J2YL82q6FjtXXfuuhK7LUX6NboAkwEug7Cb5HNAoX3nE1vFWpRnyV74EFH+uA6veqbYgHQOdtzU1houUaP/zXlKQ12x+fQMfJiagenHkh1sRBFWRvXL7kEqRHsdEiU7" +
            "xm8I14wrPxWDj6OnSMjG6gK5Zw07wVn9+ZMrYuxNshkXAuOakm0l3yPigVzimDOibGUb53sq/eC4Er099DEmVBhcUM5a7SVzoxYv/YD61ZPFNlstXMsm7f6WxqGVJ9/Yni10q6RPsucQLqUkjaW2mPGRtPH2ljwj" +
            "RaUF5+bxWoQ46lgt8bymQzyEzfmNw5eHHbqsEGURw0xv05SyWkpT3V0rdzDKhAI36qEpUp0rG5JgmtO3ls6dsOsKLmgiast6C7EfppqjT0WE0pUbHxflWLRncy5XxVEXp9lwAFN/SrVUkisWZsKQB3Q9xixpKxYm" +
            "4ESIVZ0uPB67l8Hoa1mv/hjchWwC9gPTp26QSJ9JzA+kTwpPfoXVM2C9JMdHJPSEMQaeunR0ui7n09eqiDVl0+dN0vbG0Do78u4WC6viaamopeq69IJY4Z1sV32sCwGOhxYsihscbkJ+cIpkH2kiwDJys2XK9wB6" +
            "OA6vYn9osoiHs53G5M3nGIha4shjz4Go+EIFkNFmW7gGWbhmHr++OQmsSfvr959FToUBjsZPDjOK8lJC/NWWivSswE3a2yEb0R+D9uo4TdYKwKinSQoL8XEiGHjfiv1NWSr0Qxgl/UnG8o8iMsQvor+K/5114Y5e" +
            "ldlIsb06Oceufn8FXV9PGiOpCZLKlemZkG6L1udIMVpXO63kBJcuWS7+R8N3amlNCIKZQmTyWJ73lSw9gh30M/GV9YNzv46vzFxxFye9rcHe1AC6dQCNN30gEnTaLa2+MGC4aAsra/2WQ/808RjsQTmhgTrcp4J9" +
            "kI+a5LYK/oyQETPzOXxxTuxXjjpNdW9BpqLYAoiNeM3pPrfutAYXuK78RRMLIQm5LTxWSxlYmkK3heCA7tubY/Qait976G69mnmajKco0YRlEXEelrNJThQ5a3arS7AgOx/50dYeFyOz5x+LmI2dX5n4XwFtzwTg" +
            "A0JSrrhEXSxCVvC+ydIdBQGn5ydFSahYb7YxnmSRpbn4yHH3xEjgHuhbJTsbrhh11YFX3h3PhKg3HwMWGWeidXBZjaXULRl2hptbnUoos4s9zCS3HlPegbF7v/UpaO9RpZLgRAMGPYgaqVUsQTDx9Q741OtYgPgV" +
            "iJVjV+ThqFSxX5S+2Q/mCJF+555k65daaOM5isUtxNvSk8joYPDOvEpC5WBjAedtQ8FzerEKIqZpOsY8rkbY4mzre+lVvYpR3hccg7Fm+5o2aa2LzcVQM3XGohSK29t2qALjd/WzpuWTQLTYDTSDR0eR3FZEcWQi" +
            "CocN3jkWmAecfneTMTwj7WjT1STuYVLuXgfFk46/zKpohdwFJDhNHHYYUFeR4MWD67FCnZ0XtZLYRYHzfivmubJm6YyQAFPVpuGP3LIb2Nrd1jMkc0wTb+8TtxhNS/en7uN2H7Swf8Or+a44i0Ewb5UwaiwM5xqE" +
            "CaW0PfQZXCJmDlQfkEG9iNUJnAlPd09VqNSEdi1VeOQub75dhhvPT0/AXnkcFZaJGlMh1x6XIRUlBFbm3oJ/lZqc/e3kw3FpouPt6YwMVu5AXejzgPTPFQ0frMBaaXLuK2S1F5Pq+MCz8/rdNeHBG3mEM3Ajvbl8" +
            "m4yHYOvZLXWggamuGLO2Ml73fUqSnV7NnDGKN8u/WZ2Psi1u2W8MZmjFzUruVjpU+tOrxj5a+tgLbfFKLz1xWy3q0K2J6IAm7DYmKC3D24T1ZWVEpTyrG01zqNiAAOp+U9wXJbzKA69ofJ9U5WO1FRme8cyVxwAu" +
            "DKcWPbJL9GJGxRW0QDIeEVS0++wzZmictivsxQlAnCA4zc506UITsAEVPzjPXDHnuBhvNTMGKXmlA4vP2G7RwDabbOGeAKNadG5pdK50+G/W2hiwnDPGR01Uy63NmlqmfhH3O4v5jis2qdatzyi+MEfhBdqFZWSe" +
            "gO2QzracX5AdnckxI3ITrDsA6UJiHsayJ+SLo8nUhy1VQ3MqGKeEKnKtV+u5KhzcmPfEw9hdybLGyFR5jFKtv0s8r6g5ZFymKvlDco9im2IUbOojCO0t4VDiK1uKIhK/rjLrceBcE8ZK/yVaO27dZ+7LPz7Y5nOz" +
            "sY73L7tWTCUgwEsZGcAY3GoUCCvQZ+VQQXLntD38GW3bOV6FTi+t45MGgY/TqsVVrR3mHLB8rOFcjb3OOLgSLmwbBv8bFTDAWFSeaE8+M5ylNL+ZwNs4UyYh/tHNXfdG/6EwEDpb6x0IvrvwRrvsqPuzYQZ61+wb" +
            "e7adKlDi9WYrsdGmEUitzoi3j1W+B7cVspApGfre+UryDbzfLjUpnxrirTezsixL8GlXBC8MvOvXlsdNEzc/hNqhLh5tDmfkCnp4dp6jYEAvHqjyI1Xiy+fKaD4yKxcVXamlTXBqurvyrao4A5U9dPn9PgoN9LRL" +
            "06WGHco71Q0XFJ8o0R9WL1MKfjd/hSL60II3Vx3frHUd8CXge0t8azJu2Y8ToUFzEoXLt6bOp7nzA1E9MGlJAuN0jyYv5z86Fd+p6Zz3fYXnQGK11Vs16tSYFP0KYQVzHWdmbpIRLJPpobs+jRghvAipLV7NpOUM" +
            "m3UOVzeHcCJvD27PO/X9Iff+Bz7TPn5INbegMQSzlN9SmLQq3t4UKyWnLAivtTAS+D9JJKTO3wLiYYXij4o6ydDvoU/PLacJKvjDKO9EKLfSMJY22T6wYjUGBqSr4MBe+OREkLwYog5qH0lXBKvnB/xrAgHezoDg" +
            "9jiTDQ1tN6zBaPHzI1joE18RFAu935DuEhM9jXFgSO6RFVHRkymlizHSxDoSRRmZet4z3dPuqH/fb4qzu8wdtOd9ouNLo+oT5TubEpQT03gl6EZRBGG14nbibJst8jnMW0hUAogzXK/mfZVViNKspJ5IyrRGLdBm" +
            "Bpe35xE2llaZmB7877aJlHl9pWIJWRiI/44sGiaa7cem0+HNdDuQTYlLnh710axJIoUy7nc5Os745BHSzPfnIHa1jAcV0GYS7mFQjKmwKw7gbNU54xvk/Rt9V+BMZDgfmrOhwp+3t2Bj3Xi366tIs4Iaub/aO16j" +
            "9PrsH7dFkZwWucPdYsebSKDA3pGpo8ZTVrjmPdhofXJ3mwtZRGsMlTRIv+O1M2szJeDrJ13ZKziZ0YHPdCzblBFksg3H7A0yFiAbnfjZCoFUXcfUrySt+yuP/b+iphCe01iZjS0iOhxh7AOy9UR3uxwFvxUA3RPf" +
            "1Rwx/830x68ffT9uyhNNFLWUfENVSjYI0o0NbwqqbJi3uoBnIPXf30qCME2cH2vyigAhP5UD9JqA6b2PHBkyoKGnC4awh/DXjkm0pijclLEepQypjvzYcR5z/GlNTBWAyO7lEuE3RGvFrsmakmiK6Ol5M1rQAvHl" +
            "ADFzqsl1uUITJKKbEgpPqItgOr5s8CJuUNdw53/ME6SlEgbL/MCqDNBu4BXaMFPUiTQu7a6As5itBB/nTnql9ljqNky8zbbhdqc0i/8qSeUb3SJ4CY5D9m1gwMbbrZ7YP1uChhFNwzKPAhH4VTxpqKhC68imGWuh" +
            "yhNzePhI7eFeH/a6RhjGA2eD59+APnJ29NjERDlZWcycAOqgj3Xi7vpgqDZBb3lrInvgXI7NTf9d9GcunhaSKJEu8AIfJVRaskgUOgmSTdt+zCTVGT3WTE87OrmbzT2Z6VuVMo40silHr+etGMBPN9q0SB6HWCtK" +
            "lz3FYyxPZmo2PSAZ4ib/C19t8iCapngKAlgQsS7tCeqILHEnK9eY7aMU9v5FlfDvgmk8a42GY3iuP0q47zaPEGI079k9ZWnaGGin2x4dd6w9Sx6x4cbLhNNdS7sMoIyRlz4cNLQ687zznQMH3b8feo62P0fHj1Ey" +
            "diaQ6BJ1f+vUsGnBByP8XL6vMQWmnYT16JKrTLn4jSW30HCTiVMpcL9LiD9U2ji9d5RYqLowZV7yZEtb3PQabjkPV6FOJZXzvGkNH6KdUJn5KFWZqKWHQTA0P/82XW2Kv0JA/E75LPvHYd+w5uyVI5BnYHn27bze" +
            "TOvyb535XzsHQYzqlFAsWShG4NByEziLfRniwEgLYVVl1uJfo/ZmRFFM6YYE4tMbJgpKboXEcXnal6IMheWCsj7KYDkKst+k69VZekzaDDWjKh0SwjkaJZs9NAiwIfhFCVZMOUShTEdCDUlnnOT+SzXLAzlUUtTy" +
            "UGyll/3+g0m2KEg7fnl1AKsCSrwmIvYU76tsEgixBFbkTf9SNxFRnpeXxIuo/of6gjHmjZCcpz+a9fq8cUVx+MaqA/TKTLR5zYXDeu6v2mDs1uDRFp8LoXNghRCJAlZEArGoQixWBGzmWdyLhSYwlDhfCBdHg4Oc" +
            "MRQdEX9i7JttRJpB8H/irnxIefBwn2HH9ptrCfpFbDAd0yxaRMjEM0j+Yxkqm+1SRz1zb060n4l4HdXwGF0H5gIdgCZYH2YtjDdz+JUrtDtqdjvtbNcTlhg/aI6ngu3GMATSjLTIRGSJHXj0vHYhBEVP/eoA9fPt" +
            "03dNddn+sDFAWOYengVfThvmPaQ5TueYxHtcFm0SpCgYeq+gXSQfOFKPIy+UtdM9YCpUseKmQlI4knFVQU5hHxAzAwx5w+ZTHzoxWtpft+8cuwM6E5hpX57RLZtd2b1OYLq/BbAy8Dg1antr3A5uZBeX/Emu+edj" +
            "zAMhI/KOdSOvuWUQLRF2My6++EPSphEr6LFI0iz0BiMdljMBHNrPwq33UggKCBvQlZP5/G/QRRaDFxU5rvVVKI32Xrap+7Lx3g/GCS4FTGRbbLtR7fTZdZUVDLS7qtOdKUTsJlVg5iUlgBv+VDKzjV/sq1qX+gUa" +
            "vXTbJpFErTKskktgmDLUoQ==";
        static readonly byte[] MG = Encoding.ASCII.GetBytes("SQLite format 3\0");
        public string Path_, Temp, Name; public byte[] Key; public bool Dirty; public Sqlite Db;
        public Dictionary<string, List<string>> Cols = new Dictionary<string, List<string>>();
        public Dictionary<string, Dictionary<string, string>> Types = new Dictionary<string, Dictionary<string, string>>();
        DateTime stamp; bool backed;
        static bool IsSql(byte[] u) { for (int i = 0; i < 16; i++) if (u[i] != MG[i]) return false; return true; }
        static byte[] X(byte[] u, byte[] K) { var o = new byte[u.Length]; for (int i = 0; i < u.Length; i++) o[i] = (byte)(u[i] ^ K[i & 4095]); return o; }
        static byte[] Derive(byte[] u)
        {
            int n = u.Length >> 12; var K = new byte[4096]; var c = new int[256];
            for (int j = 0; j < 4096; j++) { Array.Clear(c, 0, 256); for (int p = 0; p < n; p++) c[u[p * 4096 + j]]++; int b = 0; for (int v = 1; v < 256; v++) if (c[v] > c[b]) b = v; K[j] = (byte)b; }
            for (int i = 0; i < 16; i++) K[i] = (byte)(u[i] ^ MG[i]); return K;
        }
        public static Gtp Open(string path)
        {
            var u = File.ReadAllBytes(path); if (u.Length < 1024) throw new Exception("Tệp quá nhỏ – không phải tệp thuộc tính .gtp");
            var tries = IsSql(u) ? new byte[][] { null } : new byte[][] { Convert.FromBase64String(KEY64), Derive(u) }; string er = "";
            foreach (var K in tries)
            {
                var p = K == null ? u : X(u, K); if (!IsSql(p)) continue;
                var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "m8pro_" + Guid.NewGuid().ToString("N") + ".sqlite"); File.WriteAllBytes(tmp, p);
                Sqlite db = null;
                try
                {
                    db = new Sqlite(tmp); var r = db.Query("pragma quick_check"); var ok = r.Count > 0 ? Convert.ToString(r[0].Values.First()) : "";
                    if (ok == "ok") { var g = new Gtp { Path_ = path, Temp = tmp, Name = System.IO.Path.GetFileName(path), Key = K, Db = db, stamp = File.GetLastWriteTimeUtc(path) }; g.Schema(); return g; }
                    er = ok;
                }
                catch (Exception e) { er = e.Message; }
                if (db != null) db.Dispose(); try { File.Delete(tmp); } catch { }
            }
            throw new Exception("Không đọc được tệp .gtp" + (er != "" ? " (" + er + ")" : "") + " – có đúng là tệp thuộc tính không?");
        }
        void Schema()
        {
            foreach (var t in Db.Query("select name from sqlite_master where type='table'"))
            {
                var n = Convert.ToString(t["name"]); var info = Db.Query("pragma table_info(" + Sqlite.Q(n) + ")");
                Cols[n] = info.Select(c => Convert.ToString(c["name"])).ToList(); Types[n] = new Dictionary<string, string>(); foreach (var c in info) Types[n][Convert.ToString(c["name"])] = (Convert.ToString(c["type"]) ?? "").ToLowerInvariant();
            }
        }
        public bool Has(string t, params string[] c) { List<string> L; return Cols.TryGetValue(t, out L) && c.All(L.Contains); }
        static string Str(object v) { if (v == null) return ""; if (v is double) { var d = (double)v; if (d == Math.Floor(d) && Math.Abs(d) < 1e15) return ((long)d).ToString(); } return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture); }
        static double? Num(object v) { if (v == null) return null; try { return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture); } catch { return null; } }
        public static string Norm(string s) { s = (s ?? "").Trim(); double d; if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) && d == Math.Floor(d)) return ((long)d).ToString(); return s; }
        public List<GRow> Rows()
        {
            if (!Has("ThuaDat", "soThuTuThua", "thuaDatId")) throw new Exception("Tệp không có bảng thửa đất (ThuaDat)");
            var own = new Dictionary<string, List<Dictionary<string, object>>>();
            if (Has("DangKyThua", "thuaDatId", "nguoiId") && Has("Nguoi", "nguoiId", "hoTen"))
            {
                Func<string, string> c = k => Has("Nguoi", k) ? "n." + k : "null";
                foreach (var r in Db.Query("select d.thuaDatId as t, n.nguoiId as nid, n.hoTen as h, " + c("hoTen2") + " as h2, " + c("diaChi") + " as dc, " + c("namSinh") + " as ns from DangKyThua d join Nguoi n on n.nguoiId=d.nguoiId order by d.dangKyThuaId"))
                { var k = Str(r["t"]); if (!own.ContainsKey(k)) own[k] = new List<Dictionary<string, object>>(); if (!own[k].Any(z => Str(z["nid"]) == Str(r["nid"]))) own[k].Add(r); }
            }
            // mục đích sử dụng: mã số loại đất (danh mục nội bộ của tệp) → ký hiệu
            var mds = new Dictionary<string, List<GMd>>();
            if (Has("DaMucDichSuDung", "thuaDatId", "dienTich"))
            {
                var ids = new[] { "loaiMucDichSuDungId", "loaiMucDichSuDungKiemKeId", "loaiMucDichSuDungQuyHoachId" }.Where(x => Has("DaMucDichSuDung", x)).ToArray();
                string k = ids.Length == 0 ? "null" : ids.Length == 1 ? ids[0] : "coalesce(" + string.Join(",", ids) + ")", ch = Has("DaMucDichSuDung", "laMucDichChinh") ? "laMucDichChinh" : "0";
                foreach (var r in Db.Query("select daMucDichSuDungId as id, thuaDatId as t, " + k + " as k, dienTich as dt, " + ch + " as c from DaMucDichSuDung order by daMucDichSuDungId"))
                {
                    var t = Str(r["t"]); if (!mds.ContainsKey(t)) mds[t] = new List<GMd>(); long kk; long? loai = long.TryParse(Str(r["k"]), out kk) ? kk : (long?)null;
                    mds[t].Add(new GMd { Id = r["id"], Loai = loai, Dt = Num(r["dt"]), Chinh = Str(r["c"]).Contains("1") });
                }
            }
            var ts = new Dictionary<string, string>();
            if (Has("TbTrangSoDiaChinh", "soHieuToBanDo", "soThuTuThua", "loaiDat"))
                foreach (var r in Db.Query("select soHieuToBanDo as t, soThuTuThua as s, loaiDat as l from TbTrangSoDiaChinh")) ts[Norm(Str(r["t"])) + "|" + Norm(Str(r["s"]))] = Str(r["l"]);
            var o = new List<GRow>();
            foreach (var v in Db.Query("select * from ThuaDat"))
            {
                Func<string, object> g = k => v.ContainsKey(k) ? v[k] : null; var id = g("thuaDatId"); List<Dictionary<string, object>> ow; own.TryGetValue(Str(id), out ow); ow = ow ?? new List<Dictionary<string, object>>();
                var geom = g("geom") as byte[]; v.Remove("geom");
                var row = new GRow
                {
                    Id = id, Raw = v, To = Norm(Str(g("soHieuToBanDo"))), So = Norm(Str(g("soThuTuThua"))), Dt = Num(g("dienTich")), Dtpl = Num(g("dienTichPhapLy")), Ghep = Str(g("mucDichSuDungGhep")).Replace(" ", "").Trim(),
                    DiaChi = Str(g("diaChi")).Trim(), GhiChu = Str(g("ghiChu")), TamX = Num(g("TamX")), TamY = Num(g("TamY")),
                    Chu = string.Join("; ", ow.Select(z => string.Join(" và ", new[] { Str(z["h"]), Str(z["h2"]) }.Where(x => x.Trim() != "").ToArray())).Where(x => x != "").ToArray()),
                    ChuDc = ow.Count > 0 ? Str(ow[0]["dc"]) : "", NamSinh = ow.Count > 0 ? Str(ow[0]["ns"]) : ""
                };
                foreach (var z in ow) row.OwnerIds.Add(z["nid"]);
                List<GMd> md; if (mds.TryGetValue(Str(id), out md)) row.Mds = md;
                try { if (geom != null && geom.Length > 9) row.Rings = WkbRead(geom); } catch { }
                o.Add(row);
            }
            LearnFrom(o); foreach (var r in o) SetKh(r, ts);
            return o;
        }
        // ký hiệu loại đất hiển thị: ưu tiên cột ghép, sau đó các mục đích (mục đích chính trước)
        static void SetKh(GRow r, Dictionary<string, string> ts)
        {
            foreach (var m in r.Mds) m.Code = m.Loai.HasValue ? LoaiDatMa.Get(m.Loai.Value) : null;
            // thửa nhiều mục đích mà cột ghép chỉ ghi mục đích chính: hiện đủ (ví dụ ONT+CLN) khi đã biết hết mã
            if (r.Ghep != "" && !(r.Mds.Count > r.Ghep.Split('+', ',').Length && r.Mds.All(m => m.Code != null))) { r.Kh = r.Ghep; return; }
            if (r.Mds.Count > 0) { r.Kh = string.Join("+", r.Mds.OrderByDescending(m => m.Chinh).Select(m => m.Code ?? ("#" + m.Loai)).ToArray()); return; }
            string l; r.Kh = ts.TryGetValue(r.To + "|" + r.So, out l) ? l : "";
        }
        public static void Refresh(GRow r) { SetKh(r, new Dictionary<string, string>()); }
        // học mã số ↔ ký hiệu từ các thửa có ghi cả hai
        static void LearnFrom(List<GRow> rows)
        {
            foreach (var r in rows)
            {
                if (r.Ghep == "" || r.Mds.Count == 0) continue; var codes = r.Ghep.Split('+', ',').Select(x => x.Trim()).Where(x => x != "").ToArray();
                if (codes.Length != r.Mds.Count) continue; var ms = r.Mds.OrderByDescending(m => m.Chinh).ToList();
                for (int i = 0; i < codes.Length; i++) if (ms[i].Loai.HasValue) LoaiDatMa.Vote(ms[i].Loai.Value, codes[i]);
            }
            LoaiDatMa.Save();
        }
        // đọc WKB: Polygon / MultiPolygon (cả EWKB có SRID, có Z/M)
        public static List<double[]> WkbRead(byte[] b)
        {
            int p = 0; var o = new List<double[]>();
            Func<bool, uint> U = le => { var v = le ? BitConverter.ToUInt32(b, p) : (uint)(b[p] << 24 | b[p + 1] << 16 | b[p + 2] << 8 | b[p + 3]); p += 4; return v; };
            Func<bool, double> D = le => { double v; if (le) v = BitConverter.ToDouble(b, p); else { var t = new byte[8]; Array.Copy(b, p, t, 0, 8); Array.Reverse(t); v = BitConverter.ToDouble(t, 0); } p += 8; return v; };
            Action geom = null;
            geom = () =>
            {
                bool le = b[p++] == 1; uint t = U(le); if ((t & 0x20000000) != 0) U(le);   // SRID
                int dim = 2; if ((t & 0x80000000) != 0) dim++; if ((t & 0x40000000) != 0) dim++; uint ty = t & 0x0FFFFFFF;
                if (ty >= 1000) { uint f = ty / 1000; ty %= 1000; dim += f == 3 ? 2 : 1; }
                if (ty == 3) { uint nr = U(le); for (int i = 0; i < nr; i++) { uint np = U(le); var r = new double[np * 2]; for (int j = 0; j < np; j++) { r[2 * j] = D(le); r[2 * j + 1] = D(le); p += 8 * (dim - 2); } if (np > 1 && r[0] == r[2 * np - 2] && r[1] == r[2 * np - 1]) Array.Resize(ref r, (int)(np - 1) * 2); o.Add(r); } }
                else if (ty == 6) { uint n = U(le); for (int i = 0; i < n; i++) geom(); }
                else throw new Exception("Kiểu hình " + ty);
            };
            geom(); return o;
        }
        public bool Update(object id, string col, object val) { return Set("ThuaDat", "thuaDatId", id, col, val); }
        // ghi một ô; giá trị chữ được đổi theo kiểu cột (số nguyên, số thực, đúng/sai, chữ).
        // Cột gCadas luôn cần có giá trị (mục GHI AN TOÀN CHO gCadas) không bao giờ bị ghi thành ô trống.
        public bool Set(string table, string key, object id, string col, object val)
        {
            if (!Has(table, col)) return false; string ty; Types[table].TryGetValue(col, out ty); ty = ty ?? "";
            if (val is string)
            {
                var s = ((string)val).Trim(); double d;
                bool num = double.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d);
                if (s == "" && !ty.Contains("char") && !ty.Contains("text")) val = null;
                else if (ty.Contains("int")) val = num ? (object)(long)Math.Round(d) : (object)s;
                else if (ty.Contains("float") || ty.Contains("double") || ty.Contains("real")) val = num ? (object)d : (object)s;
                else if (ty == "bit") val = s == "1" || s.ToLower() == "true" || s.ToLower() == "có" ? 1L : 0L;
            }
            if (val is DBNull) val = null;
            if (val == null && BatBuoc(table, col)) val = MacDinh(table, col, null);
            Db.Exec("update " + Sqlite.Q(table) + " set " + Sqlite.Q(col) + "=? where " + Sqlite.Q(key) + "=?", val, id); Dirty = true; return true;
        }
        public List<Dictionary<string, object>> Query(string sql, params object[] ps) { return Db.Query(sql, ps); }
        // thêm chủ sử dụng cho thửa: người mới có loại đối tượng sử dụng (gCadas luôn cần), đăng ký trên từng loại đất chi tiết của thửa
        public object AddOwner(object thuaDatId, string name)
        {
            if (!Has("Nguoi", "hoTen") || !Has("DangKyThua", "nguoiId", "thuaDatId")) throw new Exception("Tệp không có bảng chủ sử dụng");
            var xa = Has("ThuaDat", "xaId") ? Db.Query("select xaId from ThuaDat where thuaDatId=?", thuaDatId).Select(r => r["xaId"]).FirstOrDefault() : null;
            var f = new Dictionary<string, object> { { "hoTen", name } };
            if (Has("Nguoi", "xaID")) f["xaID"] = xa;
            if (Has("Nguoi", "loaiDoiTuongId")) f["loaiDoiTuongId"] = DoiTuongMacDinh();
            if (Has("Nguoi", "uid")) f["uid"] = UidMoi("Nguoi", "uid");
            var nid = Them("Nguoi", "nguoiId", f);
            DangKy(thuaDatId, nid, null);
            return nid;
        }

        public void SetOwner(object nguoiId, string name) { if (Has("Nguoi", "hoTen")) { Db.Exec("update Nguoi set hoTen=? where nguoiId=?", name, nguoiId); Dirty = true; } }
        // thêm thửa: chép một dòng có sẵn làm mẫu (giữ đúng kiểu cột), đặt lại các trường
        public object NewParcel(Dictionary<string, object> f)
        {
            var T = Cols["ThuaDat"]; var tpl = Db.Query("select * from ThuaDat limit 1"); var r = new Dictionary<string, object>();
            if (tpl.Count > 0) foreach (var c in T) r[c] = tpl[0].ContainsKey(c) ? tpl[0][c] : null;
            foreach (var c in new[] { "ghiChu", "diaChi", "dienTichPhapLy", "soHieuToBanDoCu", "soThuTuThuaCu", "dienTichCu", "loaiDatCu", "soHieuThuaTam", "geom", "maThua", "maDinhDanh", "thuaDatFTS", "saoLuuId" }) if (r.ContainsKey(c)) r[c] = null;
            object oldId = r.ContainsKey("thuaDatId") ? r["thuaDatId"] : null; r.Remove("thuaDatId");
            foreach (var kv in f) r[kv.Key] = kv.Value;
            // khoá kiểu chữ (GUID) thì tự sinh, kiểu số để SQLite tự tăng
            if (oldId is string) r["thuaDatId"] = NewUid(oldId); else if (oldId is byte[]) r["thuaDatId"] = NewUid(oldId);
            return Them("ThuaDat", "thuaDatId", r);
        }
        // chép một dòng có sẵn (src: dòng đọc bằng select *) sang dòng mới của bảng table: giữ đúng kiểu cột, đặt lại các trường set;
        // khoá kiểu chữ (GUID) thì sinh mới, khoá số để SQLite tự tăng; trả về khoá của dòng mới
        public object CopyRow(string table, string key, Dictionary<string, object> src, Dictionary<string, object> set)
        {
            var T = Cols[table]; var r = new Dictionary<string, object>();
            if (src != null) foreach (var c in T) if (src.ContainsKey(c)) r[c] = src[c];
            object oldId = r.ContainsKey(key) ? r[key] : null; r.Remove(key);
            if (set != null) foreach (var kv in set) if (T.Contains(kv.Key) && kv.Key != key) r[kv.Key] = kv.Value;
            if (oldId is string || oldId is byte[]) r[key] = NewUid(oldId);
            return Them(table, key, r);
        }

        // ================= GHI AN TOÀN CHO gCadas =================
        // gCadas đọc nhiều cột bằng Convert.ToInt32 / Convert.ToDouble: một ô trống (NULL) ở cột mà dữ liệu của gCadas luôn có giá trị
        // làm gCadas báo "Object cannot be cast from DBNull to other types" khi mở bảng thuộc tính. Mọi lần ghi của công cụ đều qua đây:
        //  · cột NOT NULL, hoặc cột mà trong tệp không có ô trống nào (xét trước lần ghi đầu tiên vào bảng) → luôn được điền giá trị đúng kiểu;
        //  · thửa luôn có ít nhất một loại đất chi tiết, đủ mã loại đất và diện tích; đăng ký của mỗi chủ có trên từng loại đất chi tiết;
        //  · mã uid sinh mới theo đúng định dạng của bảng (chuỗi hay nhị phân như dữ liệu sẵn có).
        class Cot { public string Ten, Kieu, MacDinhSql; public bool NotNull, Pk; public long Null, Dong; }
        readonly Dictionary<string, Dictionary<string, Cot>> hoSo = new Dictionary<string, Dictionary<string, Cot>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, object> nhoMode = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, object> nhoUid = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public List<string> MaTam = new List<string>();     // ký hiệu loại đất chưa biết mã số trong tệp: đã tạm ghi mã loại đất phổ biến nhất
        Dictionary<string, Cot> HoSo(string t)
        {
            Dictionary<string, Cot> h; if (hoSo.TryGetValue(t, out h)) return h; h = new Dictionary<string, Cot>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var c in Db.Query("pragma table_info(" + Sqlite.Q(t) + ")"))
                {
                    var k = new Cot { Ten = Convert.ToString(c["name"]), Kieu = (Convert.ToString(c["type"]) ?? "").ToLowerInvariant(), NotNull = Convert.ToString(c["notnull"]) == "1", Pk = Convert.ToString(c["pk"]) != "0", MacDinhSql = c["dflt_value"] == null ? null : Convert.ToString(c["dflt_value"]) };
                    h[k.Ten] = k;
                }
                if (h.Count > 0)
                {
                    var cs = h.Values.ToList();
                    var r = Db.Query("select count(*) as n" + string.Concat(cs.Select((c, i) => ", count(" + Sqlite.Q(c.Ten) + ") as c" + i).ToArray()) + " from " + Sqlite.Q(t)).FirstOrDefault();
                    long n = r == null ? 0 : Convert.ToInt64(r["n"]);
                    for (int i = 0; i < cs.Count; i++) { cs[i].Dong = n; cs[i].Null = n - (r == null ? 0 : Convert.ToInt64(r["c" + i])); }
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            hoSo[t] = h; return h;
        }
        // cột phải luôn có giá trị: NOT NULL không có giá trị mặc định, hoặc dữ liệu sẵn có không có ô trống nào
        public bool BatBuoc(string t, string c)
        {
            Cot k; if (!HoSo(t).TryGetValue(c, out k) || k.Pk) return false;
            return (k.NotNull && k.MacDinhSql == null) || (k.Dong > 0 && k.Null == 0) || (k.NotNull && k.Dong > 0);
        }
        // giá trị dùng nhiều nhất của cột (khi chiếm ít nhất tyLe số dòng)
        object Mode(string t, string c, double tyLe)
        {
            var key = t + "|" + c; object v;
            if (!nhoMode.TryGetValue(key, out v))
            {
                var r = Db.Query("select " + Sqlite.Q(c) + " as v, count(*) as n from " + Sqlite.Q(t) + " where " + Sqlite.Q(c) + " is not null group by 1 order by 2 desc limit 1").FirstOrDefault();
                v = r == null ? null : new object[] { r["v"], Convert.ToInt64(r["n"]) }; nhoMode[key] = v;
            }
            var a = v as object[]; if (a == null) return null; Cot k; if (!HoSo(t).TryGetValue(c, out k)) return null;
            return k.Dong > 0 && (long)a[1] >= tyLe * k.Dong ? a[0] : null;
        }
        // mã uid mới theo định dạng đang dùng nhiều nhất trong bảng (nhị phân 16 byte hay chuỗi GUID, hoa / thường, có ngoặc)
        object UidMoi(string t, string c)
        {
            var key = t + "|" + c; object mau;
            if (!nhoUid.TryGetValue(key, out mau))
            {
                var r = Db.Query("select typeof(" + Sqlite.Q(c) + ") as k, count(*) as n from " + Sqlite.Q(t) + " where " + Sqlite.Q(c) + " is not null group by 1 order by 2 desc limit 1").FirstOrDefault();
                mau = r == null ? null : Db.Query("select " + Sqlite.Q(c) + " as v from " + Sqlite.Q(t) + " where typeof(" + Sqlite.Q(c) + ")=? limit 1", Convert.ToString(r["k"])).Select(x => x["v"]).FirstOrDefault();
                nhoUid[key] = mau;
            }
            if (mau == null) return Guid.NewGuid().ToByteArray();
            if (mau is byte[]) { var a = (byte[])mau; var g = Guid.NewGuid().ToByteArray(); if (a.Length == 16) return g; var o = new byte[a.Length]; Array.Copy(g, o, Math.Min(16, a.Length)); return o; }
            var s = Convert.ToString(mau); var u = Guid.NewGuid().ToString(); if (s.Trim('{', '}').Any(ch => ch >= 'A' && ch <= 'F')) u = u.ToUpperInvariant();
            return s.StartsWith("{") ? "{" + u + "}" : u;
        }
        static object GiaTriSql(string lit, string ty)
        {
            var s = (lit ?? "").Trim().Trim('(', ')').Trim().Trim('\'', '"').Trim(); double d;
            bool so = double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d);
            if (ty.Contains("int") || ty == "bit" || ty.Contains("bool")) return so ? (object)(long)d : 0L;
            if (ty.Contains("float") || ty.Contains("double") || ty.Contains("real") || ty.Contains("dec") || ty.Contains("num")) return so ? (object)d : 0.0;
            return s;
        }
        // giá trị an toàn cho cột bắt buộc đang trống (dong: các giá trị khác của dòng đang ghi, để biết thửa)
        object MacDinh(string t, string c, Dictionary<string, object> dong)
        {
            Cot k; if (!HoSo(t).TryGetValue(c, out k)) return null; var ty = k.Kieu;
            if (c.Equals("uid", StringComparison.OrdinalIgnoreCase) || ty.Contains("guid") || ty.Contains("uniqueidentifier")) return UidMoi(t, c);
            if (k.NotNull && k.MacDinhSql != null) return GiaTriSql(k.MacDinhSql, ty);
            // khoá trỏ tới loại đất chi tiết: loại chính của chính thửa đó
            if (c.Equals("daMucDichSuDungId", StringComparison.OrdinalIgnoreCase) && !t.Equals("DaMucDichSuDung", StringComparison.OrdinalIgnoreCase))
            { object td; if (dong != null && dong.TryGetValue("thuaDatId", out td) && td != null) { var ds = MdCua(td, true); if (ds.Count > 0) return ds[0]; } return null; }
            if (c.Equals("loaiDoiTuongId", StringComparison.OrdinalIgnoreCase)) return DoiTuongMacDinh();
            if (c.Equals("loaiMucDichSuDungKiemKeId", StringComparison.OrdinalIgnoreCase)) return LoaiTam();
            // khoá trỏ tới thửa, người, đơn… của riêng từng dòng: không tự chọn (tránh trỏ nhầm sang thửa khác)
            foreach (var x in new[] { "thuaDatId", "nguoiId", "thuaDatCuId", "donDangKyId", "giayChungNhanId", "dangKyThuaId" }) if (c.Equals(x, StringComparison.OrdinalIgnoreCase)) return null;
            if (ty.Contains("date") || ty.Contains("time")) return Mode(t, c, 0.5) ?? "0001-01-01 00:00:00";
            if (ty.Contains("int") || ty == "bit" || ty.Contains("bool"))
            {
                if (c.EndsWith("id", StringComparison.OrdinalIgnoreCase)) { var m = Mode(t, c, 0); if (m != null) return m; }   // mã danh mục: mã dùng nhiều nhất
                return 0L;
            }
            if (ty.Contains("float") || ty.Contains("double") || ty.Contains("real") || ty.Contains("dec") || ty.Contains("num") || ty.Contains("money")) return 0.0;
            if (ty.Contains("blob") || ty.Contains("binary") || ty.Contains("image")) return null;
            return "";
        }
        // thêm một dòng: cột bắt buộc còn trống được điền giá trị an toàn; cột NOT NULL có giá trị mặc định thì để SQLite tự điền;
        // uid luôn sinh mới theo định dạng của bảng
        public object Them(string t, string key, Dictionary<string, object> r)
        {
            var h = HoSo(t); var T = Cols[t];
            foreach (var c in T)
            {
                if (string.Equals(c, key, StringComparison.OrdinalIgnoreCase)) continue;
                object v; r.TryGetValue(c, out v); if (v is DBNull) { v = null; r[c] = null; }
                if (c.Equals("uid", StringComparison.OrdinalIgnoreCase) && (v != null || BatBuoc(t, c))) { r[c] = UidMoi(t, c); continue; }
                if (v != null) continue;
                Cot k; if (!h.TryGetValue(c, out k)) continue;
                if (k.NotNull && k.MacDinhSql != null) { r.Remove(c); continue; }
                if (BatBuoc(t, c)) { var m = MacDinh(t, c, r); if (m != null) r[c] = m; }
            }
            if (r.ContainsKey(key) && r[key] == null) r.Remove(key);
            var cs = r.Keys.Where(T.Contains).ToList();
            if (cs.Count == 0) Db.Exec("insert into " + Sqlite.Q(t) + " default values");
            else Db.Exec("insert into " + Sqlite.Q(t) + " (" + string.Join(",", cs.Select(Sqlite.Q).ToArray()) + ") values (" + string.Join(",", cs.Select(c => "?").ToArray()) + ")", cs.Select(c => r[c]).ToArray());
            Dirty = true; return r.ContainsKey(key) ? r[key] : (object)Db.LastId;
        }
        // loại đối tượng sử dụng cho người mới: loại dùng nhiều nhất trong tệp, chưa có thì hộ gia đình, cá nhân (GDC)
        object DoiTuongMacDinh()
        {
            if (Has("Nguoi", "loaiDoiTuongId")) { var m = Mode("Nguoi", "loaiDoiTuongId", 0); if (m != null) return m; }
            if (Has("DM75_DoiTuongSuDung", "id", "ma")) { var r = Db.Query("select id from DM75_DoiTuongSuDung where upper(trim(ma))='GDC' limit 1").FirstOrDefault(); if (r != null) return r["id"]; }
            return 1L;
        }
        // mã loại đất tạm khi ký hiệu chưa có mã số trong tệp: mã loại đất dùng nhiều nhất của tệp
        object LoaiTam() { return Has("DaMucDichSuDung", "loaiMucDichSuDungKiemKeId") ? Mode("DaMucDichSuDung", "loaiMucDichSuDungKiemKeId", 0) : null; }
        // các loại đất chi tiết của thửa (loại chính trước); tao: thửa chưa có thì tạo theo ký hiệu loại đất của thửa
        public List<object> MdCua(object thuaDatId, bool tao)
        {
            if (thuaDatId == null || !Has("DaMucDichSuDung", "daMucDichSuDungId", "thuaDatId")) return new List<object>();
            if (tao) EnsureMd(thuaDatId);
            var ch = Has("DaMucDichSuDung", "laMucDichChinh") ? "case when laMucDichChinh in (1, '1') then 0 else 1 end, " : "";
            return Db.Query("select daMucDichSuDungId as id from DaMucDichSuDung where thuaDatId=? order by " + ch + "daMucDichSuDungId", thuaDatId).Select(r => r["id"]).ToList();
        }
        // thửa chưa có dòng loại đất chi tiết (gCadas luôn có ít nhất một): tạo theo ký hiệu ghép của thửa, loại chính nhận cả diện tích thửa
        public int EnsureMd(object thuaDatId)
        {
            if (thuaDatId == null || !Has("DaMucDichSuDung", "thuaDatId")) return 0;
            if (Db.Query("select 1 as x from DaMucDichSuDung where thuaDatId=? limit 1", thuaDatId).Count > 0) return 0;
            var t = Db.Query("select * from ThuaDat where thuaDatId=?", thuaDatId).FirstOrDefault(); if (t == null) return 0;
            Func<string, object> g = k => t.ContainsKey(k) ? t[k] : null;
            var codes = Str(g("mucDichSuDungGhep")).Split('+', ',').Select(x => x.Trim().ToUpperInvariant()).Where(x => x != "").Distinct().ToArray();
            if (codes.Length == 0) codes = new[] { "" };
            double dt = Num(g("dienTich")) ?? 0;
            for (int i = 0; i < codes.Length; i++) AddMd(thuaDatId, codes[i] == "" ? null : LoaiDatMa.Id(codes[i]), i == 0 ? dt : 0, i == 0, Norm(Str(g("soThuTuThua"))), Norm(Str(g("soHieuToBanDo"))), codes[i] == "" ? "(chưa có ký hiệu)" : codes[i]);
            return codes.Length;
        }
        // diện tích các loại đất chi tiết cộng lại bằng diện tích thửa (như dữ liệu gCadas): phần còn thiếu dồn vào loại đang trống
        // (loại chính trước). chiNull: chỉ coi ô NULL là trống (khi sửa tệp), không đụng loại đã ghi 0
        public void CanDienTichMd(object thuaDatId, double dt, bool chiNull)
        {
            if (thuaDatId == null || !Has("DaMucDichSuDung", "thuaDatId", "dienTich", "daMucDichSuDungId")) return;
            var ds = Db.Query("select daMucDichSuDungId as id, dienTich as a, " + (Has("DaMucDichSuDung", "laMucDichChinh") ? "laMucDichChinh" : "0") + " as c from DaMucDichSuDung where thuaDatId=? order by daMucDichSuDungId", thuaDatId);
            if (ds.Count == 0) return;
            if (ds.Count == 1) { var a = Num(ds[0]["a"]); if (!a.HasValue || (!chiNull && Math.Abs(a.Value - dt) > 1e-6)) Set("DaMucDichSuDung", "daMucDichSuDungId", ds[0]["id"], "dienTich", dt); return; }
            double biet = 0; var trong = new List<Dictionary<string, object>>();
            foreach (var r in ds) { var a = Num(r["a"]); if (a.HasValue && (chiNull || a.Value > 0)) biet += a.Value; else trong.Add(r); }
            if (trong.Count == 0) return;
            double con = Math.Round(Math.Max(0, dt - biet), 4); var chinh = trong.FirstOrDefault(r => Str(r["c"]).Contains("1")) ?? trong[0];
            foreach (var r in trong) Set("DaMucDichSuDung", "daMucDichSuDungId", r["id"], "dienTich", r == chinh ? con : 0.0);
        }
        // diện tích thửa đổi: thửa chỉ có một loại đất chi tiết thì diện tích loại đất đó đổi theo (như gCadas: tổng các loại bằng diện tích thửa);
        // thửa nhiều loại đất giữ nguyên diện tích từng loại để người dùng tự chia (Thông tin thửa đất báo "chưa khớp")
        public void DtMotLoai(object thuaDatId, object dt)
        {
            double? d = dt is string ? (double.TryParse(((string)dt).Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dtTam) ? (double?)dtTam : null) : Num(dt);
            if (thuaDatId == null || !d.HasValue || !Has("DaMucDichSuDung", "thuaDatId", "dienTich", "daMucDichSuDungId")) return;
            var ds = Db.Query("select daMucDichSuDungId as id, dienTich as a from DaMucDichSuDung where thuaDatId=?", thuaDatId);
            if (ds.Count == 1) { var a = Num(ds[0]["a"]); if (!a.HasValue || Math.Abs(a.Value - d.Value) > 1e-9) Set("DaMucDichSuDung", "daMucDichSuDungId", ds[0]["id"], "dienTich", d.Value); }
        }
        double dtTam;
        // đăng ký của một người trên thửa: mỗi loại đất chi tiết của thửa một dòng DangKyThua (như dữ liệu gCadas), dòng đã có thì thôi.
        // mau: dòng đăng ký cũ để chép các trường khác (đơn đăng ký, đồng sử dụng…)
        public int DangKy(object thuaDatId, object nguoiId, Dictionary<string, object> mau)
        {
            if (thuaDatId == null || nguoiId == null || !Has("DangKyThua", "nguoiId", "thuaDatId")) return 0;
            var pk = Cols["DangKyThua"][0]; bool coMd = Has("DangKyThua", "daMucDichSuDungId");
            var mds = coMd ? MdCua(thuaDatId, true) : new List<object>(); if (mds.Count == 0) mds.Add(null);
            object xa = null; if (mau == null && Has("DangKyThua", "xaId") && Has("ThuaDat", "xaId")) xa = Db.Query("select xaId from ThuaDat where thuaDatId=?", thuaDatId).Select(r => r["xaId"]).FirstOrDefault();
            int n = 0;
            foreach (var md in mds)
            {
                bool co = md != null ? Db.Query("select 1 as x from DangKyThua where thuaDatId=? and nguoiId=? and daMucDichSuDungId=? limit 1", thuaDatId, nguoiId, md).Count > 0
                                     : Db.Query("select 1 as x from DangKyThua where thuaDatId=? and nguoiId=? limit 1", thuaDatId, nguoiId).Count > 0;
                if (co) continue;
                var r = new Dictionary<string, object>(); if (mau != null) foreach (var kv in mau) r[kv.Key] = kv.Value;
                r.Remove(pk); r["thuaDatId"] = thuaDatId; r["nguoiId"] = nguoiId; if (md != null) r["daMucDichSuDungId"] = md;
                foreach (var c in new[] { "giayChungNhanId", "saoLuuId" }) if (r.ContainsKey(c)) r[c] = null;
                if (mau == null && Has("DangKyThua", "xaId")) r["xaId"] = xa;
                Them("DangKyThua", pk, r); n++;
            }
            return n;
        }
        // ---------- sửa tệp đã bị bản trước của công cụ ghi thiếu ----------
        // Chạy khi kết nối tệp thuộc tính. Chỉ sửa những chỗ dữ liệu của gCadas không bao giờ có (ô trống ở cột gCadas luôn cần,
        // thửa không có loại đất chi tiết…); dữ liệu khác giữ nguyên. Trả về mô tả những gì đã sửa ("" nếu tệp không có lỗi).
        public string SuaChoGcadas()
        {
            var o = new List<string>(); Func<string, long> Dem = sql => Convert.ToInt64(Db.Query(sql).First().Values.First());
            try
            {
                Db.Exec("savepoint m8sua");
                long n;
                // 1. chủ sử dụng thiếu loại đối tượng sử dụng
                if (Has("Nguoi", "loaiDoiTuongId") && (n = Dem("select count(*) from Nguoi where loaiDoiTuongId is null")) > 0)
                { Db.Exec("update Nguoi set loaiDoiTuongId=? where loaiDoiTuongId is null", DoiTuongMacDinh()); o.Add(n + " chủ sử dụng thiếu loại đối tượng sử dụng"); }
                if (Has("ThuaDat", "thuaDatId"))
                {
                    // 2. thửa thiếu diện tích, số thửa, số tờ, tâm thửa
                    var tds = Db.Query("select * from ThuaDat where " + string.Join(" or ", new[] { "dienTich", "soThuTuThua", "soHieuToBanDo", "TamX", "TamY" }.Where(c => Has("ThuaDat", c)).Select(c => Sqlite.Q(c) + " is null").DefaultIfEmpty("0").ToArray()));
                    int nt = 0;
                    foreach (var t in tds)
                    {
                        Func<string, object> g = k => t.ContainsKey(k) ? t[k] : null; List<double[]> rg = null; try { var b = g("geom") as byte[]; if (b != null && b.Length > 9) rg = WkbRead(b); } catch { }
                        var id = g("thuaDatId");
                        if (Has("ThuaDat", "dienTich") && g("dienTich") == null)
                        {
                            double dt = rg != null && rg.Count > 0 ? Math.Abs(rg.Sum(x => Geo.Area(x))) : 0;   // vòng ngoài, vòng lỗ ngược chiều nhau (như dữ liệu gCadas)
                            if (dt <= 0 && Has("DaMucDichSuDung", "thuaDatId", "dienTich")) dt = Num(Db.Query("select sum(dienTich) as s from DaMucDichSuDung where thuaDatId=?", id).First()["s"]) ?? 0;
                            Db.Exec("update ThuaDat set dienTich=? where thuaDatId=?", Math.Round(dt, 1), id);
                        }
                        if (Has("ThuaDat", "soThuTuThua") && g("soThuTuThua") == null) Db.Exec("update ThuaDat set soThuTuThua=? where thuaDatId=?", 0L, id);
                        if (Has("ThuaDat", "soHieuToBanDo") && g("soHieuToBanDo") == null) Db.Exec("update ThuaDat set soHieuToBanDo=? where thuaDatId=?", Mode("ThuaDat", "soHieuToBanDo", 0) ?? (object)0L, id);
                        if (Has("ThuaDat", "TamX", "TamY") && (g("TamX") == null || g("TamY") == null) && rg != null && rg.Count > 0)
                        { var lp = Geo.LabelPt(rg); Db.Exec("update ThuaDat set TamX=?, TamY=? where thuaDatId=?", lp[0], lp[1], id); }
                        nt++;
                    }
                    if (nt > 0) o.Add(nt + " thửa thiếu diện tích, số thửa, số tờ hoặc tâm thửa");
                }
                if (Has("DaMucDichSuDung", "thuaDatId", "daMucDichSuDungId"))
                {
                    // 3. thửa chưa có loại đất chi tiết
                    var ids = Db.Query("select thuaDatId as id from ThuaDat t where not exists (select 1 from DaMucDichSuDung m where m.thuaDatId=t.thuaDatId)").Select(r => r["id"]).ToList();
                    foreach (var id in ids) EnsureMd(id);
                    if (ids.Count > 0) o.Add(ids.Count + " thửa chưa có loại đất chi tiết");
                    // 4. loại đất chi tiết thiếu diện tích: phần diện tích thửa còn lại
                    if (Has("DaMucDichSuDung", "dienTich"))
                    {
                        var ts = Db.Query("select distinct m.thuaDatId as id, t.dienTich as dt from DaMucDichSuDung m left join ThuaDat t on t.thuaDatId=m.thuaDatId where m.dienTich is null");
                        foreach (var r in ts) CanDienTichMd(r["id"], Num(r["dt"]) ?? 0, true);
                        long con = Dem("select count(*) from DaMucDichSuDung where dienTich is null"); if (con > 0) Db.Exec("update DaMucDichSuDung set dienTich=0 where dienTich is null");
                        if (ts.Count > 0) o.Add(ts.Count + " thửa có loại đất chi tiết thiếu diện tích");
                    }
                    // 5. loại đất chi tiết thiếu mã loại đất: theo mã mục đích của chính dòng đó, theo ký hiệu ghép của thửa, sau cùng mã dùng nhiều nhất
                    if (Has("DaMucDichSuDung", "loaiMucDichSuDungKiemKeId") && (n = Dem("select count(*) from DaMucDichSuDung where loaiMucDichSuDungKiemKeId is null")) > 0
                        && n * 10 < Math.Max(1, Dem("select count(*) from DaMucDichSuDung")))
                    {
                        bool coL = Has("DaMucDichSuDung", "loaiMucDichSuDungId"), coG = Has("ThuaDat", "mucDichSuDungGhep");
                        foreach (var r in Db.Query("select m.daMucDichSuDungId as id, m.thuaDatId as t, " + (coL ? "m.loaiMucDichSuDungId" : "null") + " as l, " + (coG ? "t.mucDichSuDungGhep" : "null") + " as g from DaMucDichSuDung m left join ThuaDat t on t.thuaDatId=m.thuaDatId where m.loaiMucDichSuDungKiemKeId is null"))
                        {
                            object v = r["l"];
                            if (v == null)
                            {
                                var codes = Str(r["g"]).Split('+', ',').Select(x => x.Trim().ToUpperInvariant()).Where(x => x != "").ToArray(); var mds = MdCua(r["t"], false);
                                int i = mds.FindIndex(x => Str(x) == Str(r["id"])); string code = codes.Length == 0 ? null : i >= 0 && i < codes.Length ? codes[i] : codes[0];
                                long? id = code != null ? LoaiDatMa.Id(code) : null; v = id.HasValue ? (object)id.Value : null;
                                if (v == null) { v = LoaiTam(); if (v != null) MaTam.Add(code ?? "(chưa rõ ký hiệu)"); }
                            }
                            if (v != null) Db.Exec("update DaMucDichSuDung set loaiMucDichSuDungKiemKeId=? where daMucDichSuDungId=?", v, r["id"]);
                        }
                        o.Add(n + " loại đất chi tiết thiếu mã loại đất");
                    }
                }
                // 6. đăng ký, nguồn gốc sử dụng thiếu loại đất chi tiết (gCadas: mỗi chủ một dòng trên từng loại đất chi tiết)
                if (Has("DangKyThua", "daMucDichSuDungId", "thuaDatId", "nguoiId"))
                {
                    var ts = Db.Query("select distinct thuaDatId as id from DangKyThua where daMucDichSuDungId is null and thuaDatId is not null").Select(r => r["id"]).ToList();
                    foreach (var id in ts) DongBoDangKy(id);
                    if (ts.Count > 0) o.Add(ts.Count + " thửa có đăng ký chủ sử dụng thiếu loại đất chi tiết");
                }
                if (Has("NguonGocGiaoDat", "daMucDichSuDungId", "thuaDatId") && (n = Dem("select count(*) from NguonGocGiaoDat where daMucDichSuDungId is null and thuaDatId is not null")) > 0)
                {
                    foreach (var r in Db.Query("select " + Sqlite.Q(Cols["NguonGocGiaoDat"][0]) + " as k, thuaDatId as t from NguonGocGiaoDat where daMucDichSuDungId is null and thuaDatId is not null"))
                    { var mds = MdCua(r["t"], true); if (mds.Count > 0) Db.Exec("update NguonGocGiaoDat set daMucDichSuDungId=? where " + Sqlite.Q(Cols["NguonGocGiaoDat"][0]) + "=?", mds[0], r["k"]); }
                    o.Add(n + " nguồn gốc sử dụng thiếu loại đất chi tiết");
                }
                // 7. thửa cũ (lịch sử tách, hợp): loại đất chi tiết thiếu uid / mã giá đất như dữ liệu gCadas
                if (Has("DaMucDichSuDungCu", "daMucDichSuDungId"))
                {
                    long tong = Math.Max(1, Dem("select count(*) from DaMucDichSuDungCu")); int nc = 0;
                    if (Has("DaMucDichSuDungCu", "uid") && (n = Dem("select count(*) from DaMucDichSuDungCu where uid is null")) > 0 && n * 10 < tong)
                        foreach (var r in Db.Query("select daMucDichSuDungId as id from DaMucDichSuDungCu where uid is null")) { Db.Exec("update DaMucDichSuDungCu set uid=? where daMucDichSuDungId=?", UidMoi("DaMucDichSuDungCu", "uid"), r["id"]); nc++; }
                    if (Has("DaMucDichSuDungCu", "giaDatId") && (n = Dem("select count(*) from DaMucDichSuDungCu where giaDatId is null")) > 0 && n * 10 < tong)
                    { var m = Mode("DaMucDichSuDungCu", "giaDatId", 0); if (m != null) { Db.Exec("update DaMucDichSuDungCu set giaDatId=? where giaDatId is null", m); nc += (int)n; } }
                    if (nc > 0) o.Add(nc + " ô của loại đất chi tiết thửa cũ");
                }
                Db.Exec("release m8sua");
            }
            catch (Exception ex) { try { Db.Exec("rollback to m8sua"); Db.Exec("release m8sua"); } catch { } Program.Log(ex); o.Clear(); }
            hoSo.Clear(); nhoMode.Clear(); nhoUid.Clear();
            if (o.Count > 0) Dirty = true;
            return string.Join("; ", o.ToArray());
        }
        // mọi chủ đang đăng ký trên thửa đều có đăng ký trên từng loại đất chi tiết của thửa (sau khi thêm / bớt loại đất chi tiết)
        public int DongBoDangKy(object thuaDatId)
        {
            if (thuaDatId == null || !Has("DangKyThua", "nguoiId", "thuaDatId", "daMucDichSuDungId")) return 0; int n = 0;
            var mds = MdCua(thuaDatId, false); if (mds.Count == 0) return 0; var con = new HashSet<string>(mds.Select(Str));
            var ds = Db.Query("select * from DangKyThua where thuaDatId=? order by " + Sqlite.Q(Cols["DangKyThua"][0]), thuaDatId); var pk = Cols["DangKyThua"][0];
            // đăng ký trỏ tới loại đất chi tiết không còn của thửa: chuyển sang loại chính (đã có thì bỏ dòng thừa)
            foreach (var r in ds)
            {
                var md = r.ContainsKey("daMucDichSuDungId") ? r["daMucDichSuDungId"] : null; if (md != null && con.Contains(Str(md))) continue;
                bool trung = Db.Query("select 1 as x from DangKyThua where thuaDatId=? and nguoiId=? and daMucDichSuDungId=? limit 1", thuaDatId, r["nguoiId"], mds[0]).Count > 0;
                if (trung) Db.Exec("delete from DangKyThua where " + Sqlite.Q(pk) + "=?", r[pk]); else Db.Exec("update DangKyThua set daMucDichSuDungId=? where " + Sqlite.Q(pk) + "=?", mds[0], r[pk]);
                Dirty = true; n++;
            }
            foreach (var g in Db.Query("select * from DangKyThua where thuaDatId=? order by " + Sqlite.Q(pk), thuaDatId).GroupBy(r => Str(r["nguoiId"])))
                n += DangKy(thuaDatId, g.First()["nguoiId"], g.First());
            return n;
        }
        static object NewUid(object v)
        {
            if (v is byte[]) { var a = (byte[])v; return a.All(b => b == 0) ? new byte[a.Length] : Guid.NewGuid().ToByteArray().Take(a.Length).ToArray(); }
            var s = Convert.ToString(v); var core = s.Trim('{', '}'); if (core.Trim('0', '-') == "") return s;
            var u = Guid.NewGuid().ToString(); if (core.Any(ch => ch >= 'A' && ch <= 'F')) u = u.ToUpperInvariant(); return s.StartsWith("{") ? "{" + u + "}" : u;
        }
        // hình thửa dạng WKB như dữ liệu sẵn có: vòng ngoài theo chiều kim đồng hồ, vòng lỗ ngược chiều, vòng khép kín
        public static byte[] Wkb(List<double[]> rings)
        {
            var ms = new MemoryStream(); var w = new BinaryWriter(ms); w.Write((byte)1); w.Write(3u); w.Write((uint)rings.Count);
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i]; int n = r.Length / 2; if (n > 1 && r[0] == r[2 * n - 2] && r[1] == r[2 * n - 1]) n--;
                bool cw = Geo.Area(r) < 0, rev = (i == 0) != cw; w.Write((uint)(n + 1));
                for (int k = 0; k <= n; k++) { int q = rev ? (n - (k % n)) % n : k % n; w.Write(r[2 * q]); w.Write(r[2 * q + 1]); }
            }
            return ms.ToArray();
        }
        // thêm một loại đất chi tiết (bảng DaMucDichSuDung): chép dòng mẫu cho đủ cột, đặt lại các trường.
        // Ký hiệu chưa có mã số trong tệp (code): tạm ghi mã loại đất dùng nhiều nhất (gCadas không nhận ô trống) và ghi nhớ vào MaTam để báo.
        // Diện tích chưa biết: để người gọi cân lại bằng CanDienTichMd (tạm 0).
        public object AddMd(object thuaDatId, long? loai, double? dt, bool chinh, string so, string to) { return AddMd(thuaDatId, loai, dt, chinh, so, to, null); }
        public object AddMd(object thuaDatId, long? loai, double? dt, bool chinh, string so, string to, string code)
        {
            if (!Has("DaMucDichSuDung", "thuaDatId")) return null;
            var T = Cols["DaMucDichSuDung"]; var tpl = Db.Query("select * from DaMucDichSuDung limit 1"); var r = new Dictionary<string, object>();
            if (tpl.Count > 0) foreach (var c in T) r[c] = tpl[0].ContainsKey(c) ? tpl[0][c] : null;
            object oldId = r.ContainsKey("daMucDichSuDungId") ? r["daMucDichSuDungId"] : null; r.Remove("daMucDichSuDungId");
            foreach (var c in new[] { "mucDichSudungChiTiet", "soHieu", "thoiHanSuDung", "giaDatId", "loaiMucDichSuDungPhuId", "dienTichPhu", "dienTichPhapLy", "loaiMucDichSuDungQuyHoachId", "saoLuuId", "datTranhChap", "suDungChung" }) if (r.ContainsKey(c)) r[c] = null;
            r["thuaDatId"] = thuaDatId;
            object kk = loai.HasValue ? (object)loai.Value : null;
            if (kk == null && T.Contains("loaiMucDichSuDungKiemKeId")) { kk = LoaiTam(); if (kk != null) MaTam.Add(string.IsNullOrEmpty(code) ? "(chưa rõ ký hiệu)" : code); }
            if (T.Contains("loaiMucDichSuDungId")) r["loaiMucDichSuDungId"] = loai.HasValue ? (object)loai.Value : null;
            if (T.Contains("loaiMucDichSuDungKiemKeId")) r["loaiMucDichSuDungKiemKeId"] = kk;
            if (T.Contains("dienTich")) r["dienTich"] = dt.HasValue ? (object)dt.Value : 0.0; if (T.Contains("laMucDichChinh")) r["laMucDichChinh"] = chinh ? 1L : 0L;
            long v; if (T.Contains("soThuTuThua")) r["soThuTuThua"] = long.TryParse(so, out v) ? (object)v : so; if (T.Contains("soHieuToBanDo")) r["soHieuToBanDo"] = long.TryParse(to, out v) ? (object)v : to;
            if (oldId is string || oldId is byte[]) r["daMucDichSuDungId"] = NewUid(oldId);
            return Them("DaMucDichSuDung", "daMucDichSuDungId", r);
        }
        // xoá một loại đất chi tiết: đăng ký, nguồn gốc sử dụng đang trỏ tới nó chuyển sang loại chính còn lại của thửa (không để trỏ vào dòng đã xoá)
        public void DelMd(object id)
        {
            if (!Has("DaMucDichSuDung", "daMucDichSuDungId")) return;
            var td = Has("DaMucDichSuDung", "thuaDatId") ? Db.Query("select thuaDatId from DaMucDichSuDung where daMucDichSuDungId=?", id).Select(r => r["thuaDatId"]).FirstOrDefault() : null;
            Db.Exec("delete from DaMucDichSuDung where daMucDichSuDungId=?", id); Dirty = true;
            var con = MdCua(td, false); var thay = con.Count > 0 ? con[0] : null;
            foreach (var t in new[] { "NguonGocGiaoDat", "GhiChuDaMucDichSuDung", "ThuaDat_GiaDat" })
                if (Has(t, "daMucDichSuDungId")) { if (thay != null) Db.Exec("update " + Sqlite.Q(t) + " set daMucDichSuDungId=? where daMucDichSuDungId=?", thay, id); else Db.Exec("delete from " + Sqlite.Q(t) + " where daMucDichSuDungId=?", id); }
            if (td != null) DongBoDangKy(td);
        }
        public bool ChangedOnDisk { get { try { return File.GetLastWriteTimeUtc(Path_) != stamp; } catch { return false; } } }
        // ghi thẳng vào tệp .gtp (mã hoá lại như tệp gốc); bản gốc giữ một lần mỗi lần mở: <tên>.gtp.bak
        public void Save(string target)
        {
            var tmp2 = Temp + ".copy"; File.Copy(Temp, tmp2, true); var b = File.ReadAllBytes(tmp2); File.Delete(tmp2);
            if (Key != null) b = X(b, Key);
            if (!backed && File.Exists(target)) { File.Copy(target, target + ".bak", true); backed = true; }
            var part = target + ".m8tmp"; File.WriteAllBytes(part, b);
            if (File.Exists(target)) { File.Copy(part, target, true); File.Delete(part); } else File.Move(part, target);
            Dirty = false; if (target == Path_) stamp = File.GetLastWriteTimeUtc(target);
        }
        public void Dispose() { if (Db != null) Db.Dispose(); try { File.Delete(Temp); } catch { } }
    }
}
