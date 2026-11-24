// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – màu chủ đề thanh ứng dụng: thanh menu, thanh công cụ, thanh trạng thái và thanh tiêu đề
// cửa sổ (Windows 11). Chữ tự chọn trắng / đen cho rõ trên nền; nền tối thì biểu tượng thanh công cụ đổi sang nét sáng.
// © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace M8Pro
{
    public static class ChuDe
    {
        // bảng màu có sẵn (tên, mã màu); "" = giao diện Windows mặc định
        public static readonly string[][] BANG = {
            new[] { "Mặc định Windows", "" },
            new[] { "Xanh dương", "#1E5AA8" }, new[] { "Xanh đậm", "#1B2A47" }, new[] { "Xanh ngọc", "#00796B" }, new[] { "Xanh lá", "#2E7D32" },
            new[] { "Xanh rêu", "#556B2F" }, new[] { "Cam", "#D9650B" }, new[] { "Đỏ đô", "#9E1B32" }, new[] { "Tím", "#5E35B1" },
            new[] { "Hồng", "#AD1457" }, new[] { "Nâu", "#6D4C41" }, new[] { "Xám đậm", "#37474F" }, new[] { "Đen", "#1F2125" },
            new[] { "Xanh nhạt", "#DCEAF9" }, new[] { "Ngọc nhạt", "#DDF2EC" }, new[] { "Vàng kem", "#FAF0D2" }, new[] { "Hồng nhạt", "#FBE3EA" }, new[] { "Xám nhạt", "#E9ECEF" } };
        public static Color? Hien { get { return Doc(Cfg.I.MauChuDe); } }
        public static Color? DangDung;     // màu đang hiện trên thanh ứng dụng (kể cả lúc xem thử)
        public static Color? Doc(string s)
        {
            s = (s ?? "").Trim(); if (s == "") return null; if (s.StartsWith("#")) s = s.Substring(1);
            int v; if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) return null;
            return Color.FromArgb(255, (v >> 16) & 255, (v >> 8) & 255, v & 255);
        }
        public static string Ma(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }
        static double Lin(int v) { double c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        // nền tối: chữ trắng tương phản hơn chữ đen (theo độ sáng tương đối WCAG)
        public static bool Toi(Color c) { double l = 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B); return 1.05 / (l + 0.05) >= (l + 0.05) / 0.05; }
        public static Color Chu(Color nen) { return Toi(nen) ? Color.White : Color.FromArgb(24, 28, 36); }
        public static Color Tron(Color a, Color b, double t) { return Color.FromArgb(255, (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t)); }
        // màu chữ "Đã kết nối / Chưa kết nối" trên thanh trạng thái
        public static Color MauKetNoi(bool ok)
        {
            if (DangDung.HasValue && Toi(DangDung.Value)) return ok ? Color.FromArgb(139, 227, 154) : Color.FromArgb(197, 203, 211);
            return ok ? Color.ForestGreen : Color.Gray;
        }

        // áp màu chủ đề cho thanh ứng dụng; null: trở lại giao diện Windows mặc định
        public static void Ap(Form f, MenuStrip menu, ToolStrip tools, StatusStrip st, Color? mau)
        {
            DangDung = mau;
            if (mau.HasValue)
            {
                var ve = new ChuDeVe(mau.Value); var tb = ((ChuDeMau)ve.ColorTable);
                menu.Renderer = ve; tools.Renderer = ve; st.Renderer = ve;
                menu.BackColor = mau.Value; st.BackColor = mau.Value; tools.BackColor = tb.NenCongCu; f.BackColor = mau.Value;
            }
            else
            {
                menu.RenderMode = ToolStripRenderMode.ManagerRenderMode; tools.RenderMode = ToolStripRenderMode.System; st.RenderMode = ToolStripRenderMode.ManagerRenderMode;
                menu.ResetBackColor(); tools.ResetBackColor(); st.ResetBackColor(); f.ResetBackColor();
            }
            menu.Invalidate(); tools.Invalidate(); st.Invalidate();
        }

        // Windows 11: thanh tiêu đề, viền cửa sổ cùng màu (Windows 10 trở xuống bỏ qua)
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        public static void TieuDe(Form f, Color? mau)
        {
            if (f == null || !f.IsHandleCreated) return;
            try
            {
                int mac = unchecked((int)0xFFFFFFFF);
                int nen = mau.HasValue ? ColorTranslator.ToWin32(mau.Value) : mac, chu = mau.HasValue ? ColorTranslator.ToWin32(Chu(mau.Value)) : mac, vien = nen;
                DwmSetWindowAttribute(f.Handle, 35, ref nen, 4); DwmSetWindowAttribute(f.Handle, 36, ref chu, 4); DwmSetWindowAttribute(f.Handle, 34, ref vien, 4);
            }
            catch { }
        }

        // biểu tượng nét sáng cho nền tối: nét đậm đổi thành nét sáng, giữ nguyên sắc màu nhấn
        static readonly Dictionary<string, Image> sang = new Dictionary<string, Image>();
        public static Image BieuTuongSang(string ten, int px)
        {
            var k = ten + "|" + px; Image o; if (sang.TryGetValue(k, out o)) return o;
            var g = Icons.Get(ten, px); if (g == null) return null;
            Bitmap b;
            try
            {
                b = new Bitmap(g.Width, g.Height, PixelFormat.Format32bppArgb);
                using (var src = new Bitmap(g))
                    for (int y = 0; y < src.Height; y++)
                        for (int x = 0; x < src.Width; x++)
                        {
                            var c = src.GetPixel(x, y); if (c.A == 0) continue;
                            double h, s, l; HSL(c, out h, out s, out l); if (l < 0.62) l = 1 - l * 0.8;
                            b.SetPixel(x, y, TuHSL(c.A, h, s, l));
                        }
            }
            catch { return g; }
            sang[k] = b; return b;
        }
        static void HSL(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn;
            l = (mx + mn) / 2; h = 0; s = 0; if (d < 1e-9) return;
            s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
            if (mx == r) h = (g - b) / d + (g < b ? 6 : 0); else if (mx == g) h = (b - r) / d + 2; else h = (r - g) / d + 4; h /= 6;
        }
        static Color TuHSL(int a, double h, double s, double l)
        {
            Func<double, double, double, double> f = (p, q, t) => { if (t < 0) t += 1; if (t > 1) t -= 1; if (t < 1 / 6.0) return p + (q - p) * 6 * t; if (t < 0.5) return q; if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6; return p; };
            double r, g, b; if (s < 1e-9) r = g = b = l;
            else { double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q; r = f(p, q, h + 1 / 3.0); g = f(p, q, h); b = f(p, q, h - 1 / 3.0); }
            Func<double, int> k = v => Math.Max(0, Math.Min(255, (int)Math.Round(v * 255)));
            return Color.FromArgb(a, k(r), k(g), k(b));
        }
    }

    // bảng màu vẽ thanh theo một màu chủ đề: menu thả xuống giữ nền trắng cho dễ đọc, mục đang trỏ tô nhạt theo màu chủ đề
    class ChuDeMau : ProfessionalColorTable
    {
        readonly Color nen, cong, di, bam, vien, chon, le, tach;
        public Color NenCongCu { get { return cong; } }
        public Color Tach { get { return tach; } }
        public ChuDeMau(Color c)
        {
            UseSystemColors = false; bool t = ChuDe.Toi(c); nen = c;
            cong = t ? ChuDe.Tron(c, Color.White, 0.07) : ChuDe.Tron(c, Color.Black, 0.035);   // thanh công cụ hơi khác thanh menu
            di = t ? ChuDe.Tron(c, Color.White, 0.20) : ChuDe.Tron(c, Color.Black, 0.09);     // trỏ chuột
            bam = t ? ChuDe.Tron(c, Color.White, 0.34) : ChuDe.Tron(c, Color.Black, 0.18);    // đang bấm, đang bật
            vien = t ? ChuDe.Tron(c, Color.White, 0.62) : ChuDe.Tron(c, Color.Black, 0.38);
            chon = ChuDe.Tron(c, Color.White, 0.83); le = ChuDe.Tron(c, Color.White, 0.92);
            tach = t ? ChuDe.Tron(c, Color.White, 0.38) : ChuDe.Tron(c, Color.Black, 0.2);
        }
        public override Color MenuStripGradientBegin { get { return nen; } }
        public override Color MenuStripGradientEnd { get { return nen; } }
        public override Color StatusStripGradientBegin { get { return nen; } }
        public override Color StatusStripGradientEnd { get { return nen; } }
        public override Color ToolStripGradientBegin { get { return cong; } }
        public override Color ToolStripGradientMiddle { get { return cong; } }
        public override Color ToolStripGradientEnd { get { return cong; } }
        public override Color ToolStripBorder { get { return cong; } }
        public override Color ToolStripPanelGradientBegin { get { return nen; } }
        public override Color ToolStripPanelGradientEnd { get { return nen; } }
        public override Color ToolStripContentPanelGradientBegin { get { return nen; } }
        public override Color ToolStripContentPanelGradientEnd { get { return nen; } }
        public override Color RaftingContainerGradientBegin { get { return nen; } }
        public override Color RaftingContainerGradientEnd { get { return nen; } }
        public override Color OverflowButtonGradientBegin { get { return di; } }
        public override Color OverflowButtonGradientMiddle { get { return di; } }
        public override Color OverflowButtonGradientEnd { get { return bam; } }
        public override Color GripDark { get { return tach; } }
        public override Color GripLight { get { return cong; } }
        public override Color SeparatorDark { get { return tach; } }
        public override Color SeparatorLight { get { return cong; } }
        public override Color ButtonSelectedHighlight { get { return di; } }
        public override Color ButtonSelectedHighlightBorder { get { return vien; } }
        public override Color ButtonSelectedGradientBegin { get { return di; } }
        public override Color ButtonSelectedGradientMiddle { get { return di; } }
        public override Color ButtonSelectedGradientEnd { get { return di; } }
        public override Color ButtonSelectedBorder { get { return vien; } }
        public override Color ButtonPressedGradientBegin { get { return bam; } }
        public override Color ButtonPressedGradientMiddle { get { return bam; } }
        public override Color ButtonPressedGradientEnd { get { return bam; } }
        public override Color ButtonPressedHighlight { get { return bam; } }
        public override Color ButtonPressedHighlightBorder { get { return vien; } }
        public override Color ButtonPressedBorder { get { return vien; } }
        public override Color ButtonCheckedGradientBegin { get { return bam; } }
        public override Color ButtonCheckedGradientMiddle { get { return bam; } }
        public override Color ButtonCheckedGradientEnd { get { return bam; } }
        public override Color ButtonCheckedHighlight { get { return bam; } }
        public override Color ButtonCheckedHighlightBorder { get { return vien; } }
        public override Color CheckBackground { get { return chon; } }
        public override Color CheckSelectedBackground { get { return ChuDe.Tron(chon, Color.Black, 0.08); } }
        public override Color CheckPressedBackground { get { return ChuDe.Tron(chon, Color.Black, 0.08); } }
        public override Color MenuItemSelected { get { return chon; } }
        public override Color MenuItemBorder { get { return ChuDe.Tron(nen, Color.White, 0.45); } }
        public override Color MenuItemSelectedGradientBegin { get { return di; } }
        public override Color MenuItemSelectedGradientEnd { get { return di; } }
        public override Color MenuItemPressedGradientBegin { get { return Color.White; } }
        public override Color MenuItemPressedGradientMiddle { get { return Color.White; } }
        public override Color MenuItemPressedGradientEnd { get { return Color.White; } }
        public override Color MenuBorder { get { return ChuDe.Tron(nen, Color.Gray, 0.5); } }
        public override Color ToolStripDropDownBackground { get { return Color.White; } }
        public override Color ImageMarginGradientBegin { get { return le; } }
        public override Color ImageMarginGradientMiddle { get { return le; } }
        public override Color ImageMarginGradientEnd { get { return le; } }
        public override Color ImageMarginRevealedGradientBegin { get { return le; } }
        public override Color ImageMarginRevealedGradientMiddle { get { return le; } }
        public override Color ImageMarginRevealedGradientEnd { get { return le; } }
    }

    class ChuDeVe : ToolStripProfessionalRenderer
    {
        readonly Color chu, mo, tach;
        public ChuDeVe(Color c) : base(new ChuDeMau(c)) { RoundedEdges = false; chu = ChuDe.Chu(c); mo = ChuDe.Tron(c, chu, 0.55); tach = ((ChuDeMau)ColorTable).Tach; }
        // chữ trên thanh (menu chính, nút, trạng thái): trắng hoặc đen theo nền; mục menu chính đang mở nằm trên nền trắng thì chữ đen
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var lb = e.Item as ToolStripStatusLabel;
            if (!e.Item.IsOnDropDown && !(lb != null && lb.IsLink))
            {
                var mi = e.Item as ToolStripMenuItem;
                e.TextColor = !e.Item.Enabled ? mo : (mi != null && mi.Pressed) ? SystemColors.MenuText : chu;
            }
            base.OnRenderItemText(e);
        }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { if (e.Item != null && !e.Item.IsOnDropDown) e.ArrowColor = chu; base.OnRenderArrow(e); }
        // viền: thanh trạng thái chỉ một nét mảnh màu chủ đề ở mép trên (bộ vẽ chuẩn kẻ nét trắng), thanh menu / công cụ không kẻ viền
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown) { base.OnRenderToolStripBorder(e); return; }
            if (e.ToolStrip is StatusStrip) using (var p = new Pen(tach)) e.Graphics.DrawLine(p, 0, 0, e.ToolStrip.Width, 0);
        }
        // vạch ngăn: một nét mảnh (trên thanh theo màu chủ đề, trong menu thả xuống màu xám nhạt)
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var r = new Rectangle(Point.Empty, e.Item.Size);
            using (var p = new Pen(e.Item.IsOnDropDown ? Color.FromArgb(222, 226, 232) : tach))
            {
                if (e.Vertical) { int x = r.Width / 2; e.Graphics.DrawLine(p, x, 4, x, r.Height - 4); }
                else { int y = r.Height / 2; e.Graphics.DrawLine(p, e.Item.IsOnDropDown ? 30 : 2, y, r.Width - 3, y); }
            }
        }
    }

    // hộp chọn màu chủ đề: bấm ô màu là thấy ngay trên thanh ứng dụng; Đồng ý để lưu, Huỷ để trở lại màu cũ
    public class ChuDeForm : Form
    {
        public string Chon;
        readonly Action<Color?> xem; readonly string cu; readonly FlowLayoutPanel o;
        public ChuDeForm(Action<Color?> xemThu)
        {
            xem = xemThu; cu = Cfg.I.MauChuDe ?? ""; Chon = cu;
            Text = "Màu chủ đề thanh ứng dụng"; Font = new Font("Segoe UI", 9f); FormBorderStyle = FormBorderStyle.FixedDialog; GiaoDien.KhongCat(this); MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Padding = new Padding(12);
            try { Icon = Icons.AppIcon(); } catch { }
            var tt = new ToolTip();
            var lb = new Label { Text = "Bấm một màu để xem ngay trên thanh menu, thanh công cụ, thanh trạng thái (Windows 11: cả thanh tiêu đề):", AutoSize = true, MaximumSize = new Size(430, 0), Margin = new Padding(0, 0, 0, 6) };
            o = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(9 * 46 + 4, 0), WrapContents = true, Margin = new Padding(0, 0, 0, 8) };
            foreach (var m in ChuDe.BANG)
            {
                var ma = m[1]; var mau = ChuDe.Doc(ma); if (!mau.HasValue) continue;   // "Mặc định Windows" là nút chữ ở hàng dưới
                var b = new Button { Width = mau.HasValue ? 40 : 88, Height = 32, FlatStyle = FlatStyle.Flat, Margin = new Padding(3), Text = mau.HasValue ? "" : "Mặc định", Tag = ma, UseVisualStyleBackColor = false,
                    BackColor = mau ?? SystemColors.Control, ForeColor = mau.HasValue ? ChuDe.Chu(mau.Value) : SystemColors.ControlText, Cursor = Cursors.Hand };
                tt.SetToolTip(b, m[0] + (ma != "" ? " (" + ma + ")" : ""));
                b.Click += (s, e) => { Chon = (string)((Button)s).Tag; Vien(); xem(ChuDe.Doc(Chon)); };
                o.Controls.Add(b);
            }
            var mac = new Button { Text = "Mặc định Windows", AutoSize = true, Margin = new Padding(0, 0, 6, 0) };
            mac.Click += (s, e) => { Chon = ""; Vien(); xem(null); };
            var khac = new Button { Text = "Màu khác…", AutoSize = true, Margin = new Padding(0, 0, 18, 0) };
            khac.Click += (s, e) =>
            {
                using (var cd = new ColorDialog { FullOpen = true, AnyColor = true, Color = ChuDe.Doc(Chon) ?? Color.SteelBlue })
                    if (cd.ShowDialog(this) == DialogResult.OK) { Chon = ChuDe.Ma(cd.Color); Vien(); xem(cd.Color); }
            };
            var ok = new Button { Text = "Đồng ý", DialogResult = DialogResult.OK, AutoSize = true, Margin = new Padding(0, 0, 6, 0) };
            var huy = new Button { Text = "Huỷ", DialogResult = DialogResult.Cancel, AutoSize = true };
            AcceptButton = ok; CancelButton = huy;
            var hang = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Right };
            hang.Controls.Add(mac); hang.Controls.Add(khac); hang.Controls.Add(ok); hang.Controls.Add(huy);
            var bo = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
            bo.Controls.Add(lb); bo.Controls.Add(o); bo.Controls.Add(hang); Controls.Add(bo);
            Vien();
            FormClosed += (s, e) => { if (DialogResult != DialogResult.OK) xem(ChuDe.Doc(cu)); };
        }
        // viền đậm cho ô màu đang chọn
        void Vien()
        {
            foreach (Control c in o.Controls)
            {
                var b = c as Button; if (b == null) continue; bool la = string.Equals((string)b.Tag, Chon, StringComparison.OrdinalIgnoreCase);
                b.FlatAppearance.BorderSize = la ? 3 : 1; b.FlatAppearance.BorderColor = la ? Color.FromArgb(20, 20, 24) : Color.FromArgb(196, 200, 208);
            }
        }
    }
}
