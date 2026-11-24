// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – PHÁ ĐƯỜNG RANH. © Nguyễn Xuân Thủy
// • Sửa lỗi tự động: trước khi cắt ranh, phá mọi đường gấp khúc (line string) – và chuỗi phức chỉ gồm đoạn thẳng – trên các lớp sửa thành
//   đường thẳng (line) từng cạnh: giữ đúng toạ độ (cả cao độ) mọi đỉnh và kiểu (lớp, màu, nét, kiểu đường, nhóm). Sau đó mỗi đoạn thẳng chỉ
//   bị ngắt đúng tại điểm cắt.
using System;
using System.Collections.Generic;
using System.Linq;
using BCOM = Bentley.Interop.MicroStationDGN;

namespace M8Pro
{
    public static class NoiTach
    {
        public static Action<string> Bao;      // báo tiến độ (thanh trạng thái)
        static void TienDo(string s) { var b = Bao; if (b != null) try { b(s); } catch { } }

        // ================= PHÁ ĐƯỜNG GẤP KHÚC THÀNH ĐOẠN THẲNG =================
        // trả về mã phần tử cũ đã thay (để ghi lại liên kết thửa – đường ranh); soDuong: số đường đã phá, soDoan: số đoạn thẳng tạo ra
        public static CatKq Pha(ICollection<int> lv, out int soDuong, out int soDoan)
        {
            var kq = new CatKq { Sua = true }; soDuong = 0; soDoan = 0;
            var ds = new List<KeyValuePair<MEl, double[]>>();
            foreach (var m in Ms.Read(lv, new[] { Ms.T_LSTR, Ms.T_CSTR }))
            {
                if (m == null || m.El == null || m.Parent != null || m.Lines.Count == 0) continue;
                double[] p = null;
                if (m.Kind == Ms.T_LSTR && m.Lines.Count == 1) p = m.Lines[0];
                else if (m.Kind == Ms.T_CSTR && Ms.ChiDoanThang(m.El)) p = NoiPhan(m.Lines);
                if (p != null && p.Length >= 6 && !Khoa(m.El)) ds.Add(new KeyValuePair<MEl, double[]>(m, p));   // đường gấp khúc 2 điểm: như đường thẳng; phần tử bị khoá: giữ nguyên
            }
            int dem = 0;
            foreach (var kv in ds)
            {
                var m = kv.Key; var p = kv.Value; int n = p.Length / 2;
                if (++dem % 100 == 0) TienDo("Đang phá đường gấp khúc thành đoạn thẳng… " + dem + "/" + ds.Count);
                double[] zs = m.Kind == Ms.T_LSTR ? Ms.Zs(m.El) : null; if (zs != null && zs.Length != n) zs = null;
                long id = Ms.Id(m.El); var moi = new List<BCOM.Element>(); bool ok = true;
                for (int j = 0; j + 1 < n; j++)
                {
                    if (p[2 * j] == p[2 * j + 2] && p[2 * j + 1] == p[2 * j + 3]) continue;   // đỉnh lặp: không tạo đoạn dài 0
                    BCOM.Element d = null;
                    try { d = Ms.ThemTheoMau(m.El, new[] { p[2 * j], p[2 * j + 1], p[2 * j + 2], p[2 * j + 3] }, zs == null ? null : new[] { zs[j], zs[j + 1] }); }
                    catch (Exception ex) { Program.Log(ex); }
                    if (d == null) { ok = false; break; }
                    moi.Add(d);
                }
                if (ok && moi.Count > 0 && Ms.Xoa(m.El)) { soDuong++; soDoan += moi.Count; if (id != 0) kq.IdCu.Add(id); }
                else { foreach (var x in moi) Ms.Xoa(x); kq.Loi++; }
            }
            TienDo("");
            return kq;
        }
        // các thành phần của chuỗi phức nối liền thành một đường gấp khúc (không liền: null)
        static double[] NoiPhan(List<double[]> ls)
        {
            var o = new List<double>();
            foreach (var l in ls)
            {
                if (l == null || l.Length < 4) continue;
                if (o.Count == 0) { o.AddRange(l); continue; }
                if (Math.Abs(o[o.Count - 2] - l[0]) > 1e-6 || Math.Abs(o[o.Count - 1] - l[1]) > 1e-6) return null;
                for (int k = 2; k < l.Length; k++) o.Add(l[k]);
            }
            return o.Count >= 4 ? o.ToArray() : null;
        }

        // phần tử bị khoá (không xoá được): không phá
        static bool Khoa(BCOM.Element e) { try { return (bool)((dynamic)e).IsLocked; } catch { return false; } }
    }
}
