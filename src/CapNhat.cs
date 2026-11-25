// Biên Tập BĐĐC – phiên bản và nút "Cập nhật phiên bản mới" trong công cụ. © Nguyễn Xuân Thủy
// Công cụ đang nạp trong MicroStation không thay được tệp của chính nó, nên việc tải bản mới, cài và hiện báo cáo
// do trình khởi động của bộ cài làm (BienTapBDDC.exe /capnhat): nó nhắc lưu bản vẽ, đóng MicroStation rồi mới cài.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace M8Pro
{
    public static class CapNhat
    {
        public const string PhienBan = "2026.11.24.3";   // số phiên bản: so với tệp capnhat.json trên mạng để biết có bản mới
        public const string Ngay = "24/11/2026";
        static Mutex dauHieu;
        // dấu hiệu "công cụ đã nạp vào MicroStation" cho trình khởi động (khỏi nạp lần hai)
        public static void DanhDau() { try { if (dauHieu == null) dauHieu = new Mutex(false, @"Local\BienTapBDDC_CongCu"); } catch { } }
        // thư mục cài: tệp đánh dấu cạnh M8ProDiaChinh.dll (bộ cài ghi), sổ đăng ký, thư mục của chính tệp này
        public static string ThuMucCai()
        {
            string me = null; try { me = Path.GetDirectoryName(typeof(CapNhat).Assembly.Location); } catch { }
            try
            {
                if (me != null)
                {
                    var f = Path.Combine(me, "BienTapBDDC.duongdan");
                    if (File.Exists(f)) { var d = File.ReadAllText(f).Trim(); if (d != "" && File.Exists(Path.Combine(d, "BienTapBDDC.exe"))) return d; }
                }
            }
            catch { }
            foreach (var v in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                try
                {
                    using (var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, v))
                    using (var k = b.OpenSubKey(@"Software\BienTapBDDC"))
                    {
                        var d = k == null ? null : k.GetValue("ThuMuc") as string;
                        if (!string.IsNullOrEmpty(d) && File.Exists(Path.Combine(d, "BienTapBDDC.exe"))) return d;
                    }
                }
                catch { }
            }
            try { if (me != null && File.Exists(Path.Combine(me, "BienTapBDDC.exe"))) return me; } catch { }
            return null;
        }
        public static void Mo()
        {
            var d = ThuMucCai();
            if (d == null)
            {
                MessageBox.Show("Chưa tìm thấy trình cập nhật của Biên Tập BĐĐC.\nHãy cài bằng bộ cài CaiDat_BienTapBDDC_….exe (một lần); sau đó nút này tự tải và cài phiên bản mới.",
                    Bt.Ten, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { Process.Start(new ProcessStartInfo(Path.Combine(d, "BienTapBDDC.exe"), "/capnhat") { UseShellExecute = false, WorkingDirectory = d }); }
            catch (Exception ex) { MessageBox.Show("Không mở được trình cập nhật: " + ex.Message, Bt.Ten, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        // bộ cài đã cài bản mới hơn công cụ đang chạy: bản mới chưa dựng được công cụ (lỗi biên dịch trên máy, tệp công cụ cũ đang bị giữ…)
        // → báo rõ một lần cho mỗi bản, mở báo cáo cập nhật để xem lý do
        public static void KiemTraBanCai(IWin32Window chu)
        {
            try
            {
                string cai = null;
                foreach (var v in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    try { using (var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, v)) using (var k = b.OpenSubKey(@"Software\BienTapBDDC")) { var x = k == null ? null : k.GetValue("PhienBan") as string; if (!string.IsNullOrEmpty(x)) { cai = x; break; } } }
                    catch { }
                }
                if (cai == null || SoSanh(cai, PhienBan) <= 0 || Cfg.I.BanDaBao == cai) return;
                Cfg.I.BanDaBao = cai; Cfg.I.Save();
                var hoi = "Đã cài bộ cài bản " + cai + " nhưng công cụ đang chạy vẫn là bản " + PhienBan + " – bản mới chưa dựng được công cụ.\r\n\r\n" +
                    "Mở báo cáo cập nhật để xem lý do? (có lỗi biên dịch thì gửi báo cáo cho tác giả; tệp công cụ cũ đang bị giữ thì lưu bản vẽ, đóng phần mềm bản đồ rồi bấm lại Cập nhật phiên bản mới)";
                if (MessageBox.Show(chu, hoi, Bt.Ten, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                var d = ThuMucCai(); if (d != null) Process.Start(new ProcessStartInfo(Path.Combine(d, "BienTapBDDC.exe"), "/baocao") { UseShellExecute = false, WorkingDirectory = d });
            }
            catch (Exception ex) { Program.Log(ex); }
        }
        // so số phiên bản NĂM.THÁNG.NGÀY[.n] theo từng phần số
        public static int SoSanh(string a, string b)
        {
            var x = (a ?? "").Split('.'); var y = (b ?? "").Split('.');
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                int p = 0, q = 0; if (i < x.Length) int.TryParse(x[i], out p); if (i < y.Length) int.TryParse(y[i], out q);
                if (p != q) return p.CompareTo(q);
            }
            return 0;
        }
    }
}
