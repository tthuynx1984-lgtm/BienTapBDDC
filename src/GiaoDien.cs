// BIÊN TẬP BẢN ĐỒ ĐỊA CHÍNH – bộ điều khiển dùng chung cho các hộp thoại: khung nhóm có thanh tiêu đề, danh sách lớp nhiều cột,
// thanh chuyển bản ghi, biểu tượng mũi tên. Kiểu dáng riêng của công cụ: nét phẳng, thanh tiêu đề nhóm nhạt theo màu chủ đề. © Nguyễn Xuân Thủy
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace M8Pro
{
    public static partial class GiaoDien
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        // diện tích: luôn dạng số, dấu "." thập phân, số chữ số lẻ theo thiết lập
        public static string SoDT(double v) { return v.ToString("F" + Cfg.I.DtLe, IC); }
        // màu nhấn: màu chủ đề đang dùng, chưa chọn thì xanh ngọc của công cụ
        public static Color Nhan { get { var c = ChuDe.DangDung; if (!c.HasValue) return Color.FromArgb(0, 109, 119); return ChuDe.Toi(c.Value) ? c.Value : ChuDe.Tron(c.Value, Color.Black, .45); } }
        public static Color NenNhom { get { return ChuDe.Tron(Nhan, Color.White, .9); } }
        public static Color VienNhom { get { return ChuDe.Tron(Nhan, Color.White, .72); } }
        public static Color ChuNhom { get { return ChuDe.Tron(Nhan, Color.Black, .35); } }
        public static Color NenXen { get { return ChuDe.Tron(Nhan, Color.White, .95); } }   // dòng xen kẽ của bảng
        public static Font Dam(Font f) { return new Font(f, FontStyle.Bold); }
        // nút lệnh: nút chính in đậm
        public static Button Nut(string text, int w, bool chinh)
        {
            var b = new Button { Text = text, Width = w, Height = 28, MinimumSize = new Size(70, 28), Margin = new Padding(4, 3, 0, 3), AutoSize = false, UseVisualStyleBackColor = true, TabStop = true };
            if (chinh) b.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9f, FontStyle.Bold);
            return b;
        }
        public static Label Nhan_(string s) { return new Label { Text = s, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }; }
        // số đọc được cả dấu phẩy lẫn dấu chấm thập phân
        public static bool So(string s, out double v)
        {
            s = (s ?? "").Trim().Replace(" ", ""); int c = s.LastIndexOf(','), d = s.LastIndexOf('.');
            if (c >= 0 && d >= 0) s = c > d ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", ""); else if (c >= 0) s = s.Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, IC, out v);
        }
        public static string ChuSo(double v) { return v.ToString("0.####", IC); }   // dấu chấm thập phân
        // biểu tượng tự vẽ: 0 về đầu, 1 lùi, 2 tới, 3 về cuối, 4 thêm, 5 bớt, 6 dò lại
        public static Bitmap MuiTen(int kieu, int sz, Color c)
        {
            var b = new Bitmap(sz, sz);
            using (var g = Graphics.FromImage(b))
            using (var br = new SolidBrush(c))
            using (var pn = new Pen(c, Math.Max(1.6f, sz / 9f)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; float m = sz * .22f, w = sz - 2 * m, cy = sz / 2f;
                Func<float, bool, PointF[]> tg = (x, phai) => phai ? new[] { new PointF(x, m), new PointF(x + w * .55f, cy), new PointF(x, sz - m) } : new[] { new PointF(x + w * .55f, m), new PointF(x, cy), new PointF(x + w * .55f, sz - m) };
                switch (kieu)
                {
                    case 0: g.FillRectangle(br, m, m, sz / 9f + .5f, w); g.FillPolygon(br, tg(m + w * .3f, false)); break;
                    case 1: g.FillPolygon(br, tg(m + w * .2f, false)); break;
                    case 2: g.FillPolygon(br, tg(m + w * .25f, true)); break;
                    case 3: g.FillPolygon(br, tg(m + w * .1f, true)); g.FillRectangle(br, sz - m - sz / 9f, m, sz / 9f + .5f, w); break;
                    case 4: g.DrawLine(pn, cy, m, cy, sz - m); g.DrawLine(pn, m, cy, sz - m, cy); break;
                    case 5: g.DrawLine(pn, m, cy, sz - m, cy); break;
                    case 6: g.DrawArc(pn, m, m, w, w, 30, 290); g.FillPolygon(br, new[] { new PointF(sz - m + 1, cy - 1), new PointF(sz - m - w * .32f, cy - w * .1f), new PointF(sz - m - w * .02f, cy - w * .45f) }); break;
                }
            }
            return b;
        }
    }

    // ---------- CHỮ KHÔNG BỊ CẮT, HỘP THOẠI CO GIÃN ----------
    public static partial class GiaoDien
    {
        // chiều cao chữ khi xuống dòng trong bề rộng w
        public static int CaoChu(string s, Font f, int w)
        {
            if (string.IsNullOrEmpty(s)) return TextRenderer.MeasureText("Ág", f).Height;
            return TextRenderer.MeasureText(s, f, new Size(Math.Max(10, w), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height;
        }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> daGian = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
        // nhãn / ô tích / nút chọn tự xuống dòng, cao vừa chữ theo bề rộng đang có (không thấp hơn caoMin) – đặt Dock = Top / Bottom hoặc trong ô bảng
        public static T Gian<T>(T c) where T : Control { return Gian(c, 0); }
        public static T Gian<T>(T c, int caoMin) where T : Control
        {
            object da; if (daGian.TryGetValue(c, out da)) return c; daGian.Add(c, caoMin); c.AutoSize = false; var lb = c as Label; if (lb != null) lb.AutoEllipsis = false;
            int le = 0; var cb = c as CheckBox; var rb = c as RadioButton;
            if (cb != null) { cb.CheckAlign = ContentAlignment.TopLeft; cb.TextAlign = ContentAlignment.TopLeft; le = 22; }
            if (rb != null) { rb.CheckAlign = ContentAlignment.TopLeft; rb.TextAlign = ContentAlignment.TopLeft; le = 22; }
            var bt = c as Button; if (bt != null) { le = (bt.Image != null ? bt.Image.Width + 12 : 0) + 14; caoMin = Math.Max(caoMin, 30); }
            bool dang = false;
            EventHandler tinh = (s, e) =>
            {
                if (dang || c.Width < 20) return; dang = true;
                try
                {
                    int w = c.Width - c.Padding.Horizontal - le - 4; if (bt != null) w = w * 9 / 10;   // nút: chừa lề chữ của nút (cách ngắt dòng khác nhãn)
                    int h = CaoChu(c.Text, c.Font, w) + c.Padding.Vertical + (bt != null ? 14 : le > 0 ? 7 : 4); h = Math.Max(h, caoMin); if (c.Height != h) c.Height = h;
                }
                catch { } finally { dang = false; }
            };
            c.SizeChanged += tinh; c.TextChanged += tinh; c.FontChanged += tinh; c.HandleCreated += tinh; tinh(c, EventArgs.Empty); return c;
        }
        // cột danh sách (ListView chi tiết) vừa tiêu đề và chữ dài nhất (tối đa max điểm ảnh); cột ẩn (rộng 0) giữ nguyên
        public static void VuaCot(ListView l) { VuaCot(l, 700); }
        public static void VuaCot(ListView l, int max)
        {
            if (l == null || l.View != View.Details || l.Columns.Count == 0) return;
            for (int i = 0; i < l.Columns.Count; i++)
            {
                var col = l.Columns[i]; if (col.Width == 0) continue; int w = TextRenderer.MeasureText(col.Text ?? "", l.Font).Width + 20;
                var dai = new List<KeyValuePair<string, Font>>(); int n = 0;
                foreach (ListViewItem it in l.Items)
                {
                    if (n++ > 20000) break; var t = i < it.SubItems.Count ? it.SubItems[i].Text : ""; if (string.IsNullOrEmpty(t)) continue;
                    var f = i == 0 || it.UseItemStyleForSubItems ? it.Font : it.SubItems[i].Font; dai.Add(new KeyValuePair<string, Font>(t, f ?? l.Font));
                    if (dai.Count > 400) dai = dai.OrderByDescending(kv => kv.Key.Length).Take(40).ToList();
                }
                foreach (var kv in dai.OrderByDescending(kv => kv.Key.Length).Take(40)) w = Math.Max(w, TextRenderer.MeasureText(kv.Key, kv.Value).Width + 14);
                if (i == 0 && (l.CheckBoxes || l.SmallImageList != null)) w += 22;
                col.Width = Math.Min(Math.Max(w, 30), max);
            }
        }
        // cột bảng (DataGridView) vừa tiêu đề và chữ dài nhất (tối đa max); cột tự giãn (Fill), cột ẩn giữ nguyên. Bảng gắn DataTable: đọc thẳng dữ liệu (nhanh với bảng lớn)
        public static void VuaCot(DataGridView g, int max, Font fTieuDe)
        {
            if (g == null || g.Columns.Count == 0) return; var tb = g.DataSource as System.Data.DataTable; var fh = fTieuDe ?? g.ColumnHeadersDefaultCellStyle.Font ?? g.Font;
            foreach (DataGridViewColumn col in g.Columns)
            {
                if (!col.Visible || col.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill) continue;
                int w = TextRenderer.MeasureText(col.HeaderText ?? "", fh).Width + 26; var f = col.DefaultCellStyle.Font ?? g.DefaultCellStyle.Font ?? g.Font;
                var dai = new List<string>();
                if (tb != null && tb.Columns.Contains(col.DataPropertyName ?? col.Name))
                {
                    var dc = tb.Columns[col.DataPropertyName != "" ? col.DataPropertyName : col.Name]; string fm = col.DefaultCellStyle.Format; var fp = col.DefaultCellStyle.FormatProvider;
                    foreach (System.Data.DataRow r in tb.Rows)
                    {
                        var v = r[dc]; if (v == DBNull.Value || v == null) continue;
                        var t = v is IFormattable && !string.IsNullOrEmpty(fm) ? ((IFormattable)v).ToString(fm, fp) : Convert.ToString(v, CultureInfo.CurrentCulture); if (t.Length == 0) continue;
                        dai.Add(t); if (dai.Count > 400) dai = dai.OrderByDescending(x => x.Length).Take(40).ToList();
                    }
                }
                else { int n = 0; foreach (DataGridViewRow r in g.Rows) { if (n++ > 5000) break; var t = Convert.ToString(r.Cells[col.Index].FormattedValue); if (!string.IsNullOrEmpty(t)) { dai.Add(t); if (dai.Count > 400) dai = dai.OrderByDescending(x => x.Length).Take(40).ToList(); } } }
                foreach (var t in dai.OrderByDescending(x => x.Length).Take(40)) w = Math.Max(w, TextRenderer.MeasureText(t, f).Width + 14);
                if (col is DataGridViewCheckBoxColumn) w = Math.Max(TextRenderer.MeasureText(col.HeaderText ?? "", fh).Width + 16, 40);
                col.Width = Math.Min(Math.Max(w, 36), max);
            }
        }
        // mọi hộp thoại: co giãn được (không nhỏ hơn cỡ thiết kế), chữ không bị cắt – nhãn cố định chiều cao tự xuống dòng và cao theo chữ,
        // tiêu đề / ô bảng nhỏ xuống dòng, cột danh sách vừa chữ dài nhất, nút rộng vừa chữ
        public static void KhongCat(Form f)
        {
            f.Load += (s, e) =>
            {
                try
                {
                    ChuanNut(f);
                    var bs = f.FormBorderStyle;
                    if (bs == FormBorderStyle.FixedDialog || bs == FormBorderStyle.FixedSingle || bs == FormBorderStyle.Fixed3D) { f.FormBorderStyle = FormBorderStyle.Sizable; if (f.AutoSize) f.AutoSizeMode = AutoSizeMode.GrowOnly; }
                    else if (bs == FormBorderStyle.FixedToolWindow) f.FormBorderStyle = FormBorderStyle.SizableToolWindow;
                    if (!f.AutoSize && f.MinimumSize.IsEmpty) f.MinimumSize = new Size(Math.Min(f.Width, 900), Math.Min(f.Height, 700));
                    Duyet(f);
                }
                catch (Exception ex) { Program.Log(ex); }
            };
            f.Shown += (s, e) => { try { CotTatCa(f); if (f.AutoSize) { var sz = f.Size; f.AutoSize = false; f.Size = sz; f.MinimumSize = sz; } } catch (Exception ex) { Program.Log(ex); } };
        }
        // Nút thao tác chính dùng chữ đậm và nút đóng/hủy có kích thước tối thiểu
        // thống nhất. Nút màu hoặc nút đã định dạng Flat được giữ nguyên.
        static void ChuanNut(Control c)
        {
            foreach (Control k in c.Controls)
            {
                var b = k as Button;
                if (b != null && b.FlatStyle != FlatStyle.Flat && b.FlatStyle != FlatStyle.Popup)
                {
                    b.UseVisualStyleBackColor = true;
                    b.MinimumSize = new Size(Math.Max(70, b.MinimumSize.Width), Math.Max(28, b.MinimumSize.Height));
                    if (b.Height < 28) b.Height = 28;
                    if (b.Margin == Padding.Empty) b.Margin = new Padding(4, 3, 0, 3);
                    var t = (b.Text ?? "").Trim().ToLowerInvariant();
                    if (b.DialogResult == DialogResult.OK || t == "thực hiện" || t == "ghi lại" || t == "kết nối" || t.StartsWith("gộp") || t.StartsWith("chuyển số tờ"))
                        b.Font = new Font(b.Font, FontStyle.Bold);
                    if (string.IsNullOrEmpty(b.AccessibleName)) b.AccessibleName = b.Text;
                }
                if (k.HasChildren) ChuanNut(k);
            }
        }
        static void Duyet(Control c)
        {
            foreach (Control k in c.Controls)
            {
                var lb = k as Label;
                if (lb != null && !(k is LinkLabel))
                {
                    lb.AutoEllipsis = false;
                    if (!lb.AutoSize && (lb.Dock == DockStyle.Top || lb.Dock == DockStyle.Bottom) && lb.Height > 0) Gian(lb, lb.Height);
                }
                var cb = k as CheckBox; if (cb != null && !cb.AutoSize && (cb.Dock == DockStyle.Top || cb.Dock == DockStyle.Bottom)) Gian(cb, cb.Height);
                var bt = k as Button; if (bt != null && !bt.AutoSize && bt.Dock == DockStyle.None && bt.Text != "" && TextRenderer.MeasureText(bt.Text, bt.Font).Width + 16 + (bt.Image != null ? bt.Image.Width + 6 : 0) > bt.Width) { bt.AutoSize = true; bt.AutoSizeMode = AutoSizeMode.GrowOnly; }
                var dg = k as DataGridView;
                if (dg != null && !(dg.Tag as string == "lon"))
                {
                    dg.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True; dg.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
                    dg.DefaultCellStyle.WrapMode = DataGridViewTriState.True; dg.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells; dg.ShowCellToolTips = true;
                }
                if (k.HasChildren) Duyet(k);
            }
        }
        static void CotTatCa(Control c)
        {
            foreach (Control k in c.Controls)
            {
                var lv = k as ListView; if (lv != null && lv.View == View.Details && !(lv.Tag as string == "tucot")) VuaCot(lv);
                if (k.HasChildren) CotTatCa(k);
            }
        }
    }

    // ---------- nút chữ dài: tự xuống dòng, cao vừa chữ – vẽ chữ đúng bằng cách đo nên không bị cắt chữ trên mọi máy ----------
    public class NutDai : Control
    {
        readonly Image hinh; bool tren, nhan;
        const TextFormatFlags CO = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.Left;
        public NutDai(string text, Image hinhNho)
        {
            hinh = hinhNho; Text = text; Height = 30; Padding = new Padding(8, 6, 8, 6); TabStop = true; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        }
        int LeChu { get { return Padding.Left + (hinh != null ? hinh.Width + 8 : 0); } }
        int RongChu { get { return Math.Max(20, Width - LeChu - Padding.Right); } }
        void TinhCao() { if (Width < 30) return; int h = Math.Max(30, GiaoDien.CaoChu(Text, Font, RongChu) + Padding.Vertical); if (Height != h) Height = h; }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); TinhCao(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); TinhCao(); Invalidate(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); TinhCao(); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { tren = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { tren = false; nhan = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { nhan = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { nhan = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; OnClick(EventArgs.Empty); } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var r = ClientRectangle;
            var st = !Enabled ? System.Windows.Forms.VisualStyles.PushButtonState.Disabled : nhan ? System.Windows.Forms.VisualStyles.PushButtonState.Pressed : tren ? System.Windows.Forms.VisualStyles.PushButtonState.Hot : Focused ? System.Windows.Forms.VisualStyles.PushButtonState.Default : System.Windows.Forms.VisualStyles.PushButtonState.Normal;
            try { ButtonRenderer.DrawButton(g, r, st); } catch { g.Clear(SystemColors.Control); ControlPaint.DrawButton(g, r, nhan ? ButtonState.Pushed : ButtonState.Normal); }
            if (hinh != null) g.DrawImage(hinh, Padding.Left, (Height - hinh.Height) / 2, hinh.Width, hinh.Height);
            int hc = GiaoDien.CaoChu(Text, Font, RongChu); var rt = new Rectangle(LeChu, Math.Max(Padding.Top, (Height - hc) / 2), RongChu, hc + 2);
            TextRenderer.DrawText(g, Text, Font, rt, Enabled ? ForeColor : SystemColors.GrayText, CO);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(r, -3, -3));
        }
    }

    // ---------- khung nhóm có thanh tiêu đề (nội dung đặt trong Than) ----------
    // tiêu đề dài thì xuống dòng (thanh tiêu đề cao thêm, không cắt chữ); Vua: cao vừa nội dung (các thành phần neo trên trong Than)
    public class Nhom : Panel
    {
        public readonly Panel Than = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        public const int CAO = 22;
        int cao = CAO; bool vua; Font fDam;
        public Nhom(string tieuDe)
        {
            Text = tieuDe; DoubleBuffered = true; Padding = new Padding(1, CAO + 1, 1, 1); Margin = new Padding(3);
            ResizeRedraw = true; Controls.Add(Than); Than.Padding = new Padding(6, 5, 6, 5);
            Than.ControlAdded += (s, e) => { e.Control.SizeChanged += (s2, e2) => DatCaoVua(); e.Control.VisibleChanged += (s2, e2) => DatCaoVua(); DatCaoVua(); };
            Than.ControlRemoved += (s, e) => DatCaoVua();
        }
        public bool Vua { get { return vua; } set { vua = value; DatCaoVua(); } }
        public int CaoTieuDe { get { return cao; } }
        public override string Text { get { return base.Text; } set { base.Text = value; TinhCao(); Invalidate(); } }
        Font Dam_ { get { if (fDam == null || !fDam.FontFamily.Equals(Font.FontFamily) || fDam.Size != Font.Size) fDam = new Font(Font, FontStyle.Bold); return fDam; } }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); TinhCao(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); fDam = null; TinhCao(); }
        void TinhCao()
        {
            if (Width < 20) return; int h = Math.Max(CAO, GiaoDien.CaoChu(Text, Dam_, Width - 14) + 6);
            if (h != cao)
            {
                int d = h - cao; cao = h; Padding = new Padding(1, cao + 1, 1, 1);
                if (!vua && Dock != DockStyle.Fill && !(Parent is TableLayoutPanel)) Height += d;
                Invalidate();
            }
            DatCaoVua();
        }
        // cao vừa nội dung: thanh tiêu đề + lề + các thành phần đang hiện trong Than
        public void DatCaoVua()
        {
            if (!vua) return; int h = cao + 2 + Than.Padding.Vertical;
            foreach (Control k in Than.Controls) if (k.Visible) h += k.Dock == DockStyle.Fill ? Math.Max(k.Height, 20) : k.Height;
            if (Height != h) Height = h;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g = e.Graphics; var r = ClientRectangle; if (r.Width < 4 || r.Height < 4) return;
            using (var nen = new SolidBrush(GiaoDien.NenNhom)) g.FillRectangle(nen, 0, 0, r.Width, cao);
            using (var ke = new SolidBrush(GiaoDien.Nhan)) g.FillRectangle(ke, 0, 0, 3, cao);
            using (var vien = new Pen(GiaoDien.VienNhom)) { g.DrawRectangle(vien, 0, 0, r.Width - 1, r.Height - 1); g.DrawLine(vien, 0, cao, r.Width, cao); }
            TextRenderer.DrawText(g, Text, Dam_, new Rectangle(9, 2, r.Width - 12, cao - 4), GiaoDien.ChuNhom, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    // ---------- danh sách level nhiều cột có ô tích (tự vẽ): ô tích nét mảnh; level có đối tượng đường in đậm, rê chuột thấy số đối tượng;
    // xếp theo cột từ trên xuống, nhiều level không hiện hết thì trượt ngang (hộp thấp thì thêm cuộn dọc) ----------
    public class DsLop : ScrollableControl
    {
        public class Muc { public int Lv, N; public bool Checked; public bool Bold { get { return N > 0; } } }
        public readonly List<Muc> Items = new List<Muc>();
        readonly ToolTip tip = new ToolTip(); int hot = -1, foc = -1, cw = 80; Font dam;
        const int CAO = 20, O = 13;
        public event EventHandler ItemCheck;
        public DsLop()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Dock = DockStyle.Fill; BackColor = SystemColors.Window; AutoScroll = true; TabStop = true; Padding = new Padding(3);
            var cm = new ContextMenuStrip();
            cm.Items.Add("Chọn mọi level có đối tượng", null, (s, e) => { foreach (var it in Items) it.Checked = it.Bold; Invalidate(); Bao(); });
            cm.Items.Add("Bỏ chọn tất cả", null, (s, e) => { foreach (var it in Items) it.Checked = false; Invalidate(); Bao(); });
            ContextMenuStrip = cm;
        }
        void Bao() { var h = ItemCheck; if (h != null) h(this, EventArgs.Empty); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); dam = null; DoCot(); }
        Font Dam { get { return dam ?? (dam = new Font(Font, FontStyle.Bold)); } }
        // dem: số đối tượng đường theo level; hiện level 1–63 và mọi level có đối tượng
        public void Nap(IDictionary<int, int> dem, ICollection<int> chon) { Nap(dem, chon, false); }
        // chiCo: chỉ hiện level có đối tượng (và level đang tích)
        public void Nap(IDictionary<int, int> dem, ICollection<int> chon, bool chiCo)
        {
            Items.Clear(); var lvs = chiCo ? new SortedSet<int>() : new SortedSet<int>(Enumerable.Range(1, 63)); foreach (var k in dem.Keys) lvs.Add(k); foreach (var k in chon) lvs.Add(k);
            foreach (var lv in lvs) { int n; dem.TryGetValue(lv, out n); Items.Add(new Muc { Lv = lv, N = n, Checked = chon.Contains(lv) }); }
            DoCot(); int d = Items.FindIndex(i => i.Checked); if (d >= 0) { foc = d; HienMuc(d); }
        }
        public List<int> Chon { get { return Items.Where(i => i.Checked).Select(i => i.Lv).ToList(); } }
        static string Ten(Muc m) { return "Level " + m.Lv; }
        void DoCot()
        {
            if (!IsHandleCreated && Items.Count == 0) return; int w = 40;
            foreach (var m in Items) w = Math.Max(w, TextRenderer.MeasureText(Ten(m), m.Bold ? Dam : Font).Width);
            cw = w + O + 14; int hang = Math.Max(1, (Height - Padding.Vertical - SystemInformation.HorizontalScrollBarHeight - 4) / CAO);
            int cot = (Items.Count + hang - 1) / hang;
            // vừa chiều rộng: chỉ trượt ngang khi không đủ chỗ; quá thấp (dưới 4 dòng) thì cuộn dọc
            if (hang < 4) { hang = 4; cot = Math.Max(1, (ClientSize.Width - Padding.Horizontal) / cw); AutoScrollMinSize = new Size(0, ((Items.Count + cot - 1) / cot) * CAO + Padding.Vertical); doc = true; }
            else { AutoScrollMinSize = new Size(cot * cw + Padding.Horizontal, 0); doc = false; }
            this.hang = hang; this.cot = cot; Invalidate();
        }
        int hang = 20, cot = 4; bool doc;
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); DoCot(); }
        Rectangle O_(int i)
        {
            int c, r; if (doc) { c = i % cot; r = i / cot; } else { c = i / hang; r = i % hang; }
            return new Rectangle(Padding.Left + c * cw + AutoScrollPosition.X, Padding.Top + r * CAO + AutoScrollPosition.Y, cw, CAO);
        }
        int Tai(Point p) { for (int i = 0; i < Items.Count; i++) if (O_(i).Contains(p)) return i; return -1; }
        void HienMuc(int i)
        {
            if (i < 0) return; var r = O_(i); var p = AutoScrollPosition; int x = -p.X, y = -p.Y;
            if (r.Left < 0) x += r.Left; else if (r.Right > ClientSize.Width) x += r.Right - ClientSize.Width;
            if (r.Top < 0) y += r.Top; else if (r.Bottom > ClientSize.Height) y += r.Bottom - ClientSize.Height;
            AutoScrollPosition = new Point(x, y); Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(BackColor);
            using (var vien = new Pen(Color.FromArgb(122, 122, 122))) using (var nhat = new Pen(Color.FromArgb(188, 188, 188))) using (var tich = new Pen(Color.FromArgb(33, 33, 33), 1.6f))
            using (var sang = new SolidBrush(Color.FromArgb(229, 243, 255)))
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var r = O_(i); if (!r.IntersectsWith(e.ClipRectangle)) continue; var m = Items[i];
                    if (i == hot) g.FillRectangle(sang, r.X, r.Y + 1, r.Width - 4, r.Height - 2);
                    var o = new Rectangle(r.X + 3, r.Y + (CAO - O) / 2, O - 1, O - 1);
                    g.FillRectangle(Brushes.White, o); g.DrawRectangle(m.Bold ? vien : nhat, o);
                    if (m.Checked)
                    {
                        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.DrawLines(tich, new[] { new PointF(o.X + 2.5f, o.Y + 6.5f), new PointF(o.X + 5f, o.Y + 9f), new PointF(o.X + 9.5f, o.Y + 3.5f) }); g.SmoothingMode = sm;
                    }
                    var tr = new Rectangle(o.Right + 4, r.Y, r.Width - O - 10, r.Height);
                    TextRenderer.DrawText(g, Ten(m), m.Bold ? Dam : Font, tr, m.Bold ? ForeColor : Color.FromArgb(110, 110, 110), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                    if (i == foc && Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(tr.X - 2, tr.Y + 2, Math.Min(tr.Width, TextRenderer.MeasureText(Ten(m), m.Bold ? Dam : Font).Width + 4), tr.Height - 4));
                }
            }
            using (var k = new Pen(Color.FromArgb(171, 173, 179))) g.DrawRectangle(k, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);   // viền mảnh như ô nhập
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); int i = Tai(e.Location); if (i == hot) return; hot = i; Invalidate();
            if (i >= 0) { var m = Items[i]; tip.SetToolTip(this, m.N > 0 ? Ten(m) + ": " + m.N + " đối tượng đường" : Ten(m) + ": chưa có đối tượng đường"); } else tip.SetToolTip(this, "");
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = -1; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); Focus(); if (e.Button != MouseButtons.Left) return; int i = Tai(e.Location); if (i < 0) return;
            foc = i; Items[i].Checked = !Items[i].Checked; Invalidate(); Bao();
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); if (foc < 0 && Items.Count > 0) foc = 0; Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override bool IsInputKey(Keys k) { var b = k & Keys.KeyCode; return b == Keys.Up || b == Keys.Down || b == Keys.Left || b == Keys.Right || b == Keys.Space || b == Keys.Home || b == Keys.End || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e); if (Items.Count == 0) return; int i = Math.Max(0, foc), buoc = doc ? cot : hang;
            switch (e.KeyCode)
            {
                case Keys.Space: Items[i].Checked = !Items[i].Checked; Bao(); break;
                case Keys.Down: i = doc ? i + cot : i + 1; break; case Keys.Up: i = doc ? i - cot : i - 1; break;
                case Keys.Right: i = doc ? i + 1 : i + buoc; break; case Keys.Left: i = doc ? i - 1 : i - buoc; break;
                case Keys.Home: i = 0; break; case Keys.End: i = Items.Count - 1; break; default: return;
            }
            foc = Math.Max(0, Math.Min(Items.Count - 1, i)); HienMuc(foc); e.Handled = true;
        }
        protected override void Dispose(bool d) { if (d) { tip.Dispose(); if (dam != null) dam.Dispose(); } base.Dispose(d); }
    }
    // ---------- thanh chuyển bản ghi: về đầu, lùi, vị trí i / n, tới, về cuối ----------
    public class ThanhChuyen : ToolStrip
    {
        public readonly ToolStripButton Dau, Lui, Toi, Cuoi; public readonly ToolStripLabel ViTri = new ToolStripLabel { AutoSize = false, Width = 96, TextAlign = ContentAlignment.MiddleCenter };
        public event Action<int> Chuyen;   // -2 về đầu, -1 lùi, 1 tới, 2 về cuối
        // thanh nút dùng đúng phông chữ của hộp thoại (ToolStrip mặc định không kế thừa)
        protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); var f = FindForm(); if (f != null) Font = f.Font; else if (Parent != null) Font = Parent.Font; }
        public ThanhChuyen(string tenDong)
        {
            GripStyle = ToolStripGripStyle.Hidden; Dock = DockStyle.Bottom; ImageScalingSize = new Size(16, 16); Padding = new Padding(2, 1, 2, 1); RenderMode = ToolStripRenderMode.System;
            var c = GiaoDien.Nhan;
            Func<int, string, int, ToolStripButton> B = (k, tip, d) => { var b = new ToolStripButton(null, GiaoDien.MuiTen(k, 16, c)) { ToolTipText = tip, DisplayStyle = ToolStripItemDisplayStyle.Image }; b.Click += (s, e) => { var h = Chuyen; if (h != null) h(d); }; return b; };
            Dau = B(0, "Về " + tenDong + " đầu", -2); Lui = B(1, "Lùi một " + tenDong + " (PgUp)", -1); Toi = B(2, "Tới " + tenDong + " sau (PgDn)", 1); Cuoi = B(3, "Về " + tenDong + " cuối", 2);
            Items.Add(Dau); Items.Add(Lui); Items.Add(ViTri); Items.Add(Toi); Items.Add(Cuoi); Dat(-1, 0);
        }
        // i: chỉ số đang đứng (-1: chưa đứng ở dòng nào), n: số dòng
        public void Dat(int i, int n)
        {
            ViTri.Text = (i >= 0 && n > 0 ? (i + 1).ToString(CultureInfo.InvariantCulture) : "–") + " / " + n.ToString(CultureInfo.InvariantCulture);
            Dau.Enabled = Lui.Enabled = Toi.Enabled = Cuoi.Enabled = n > 0;
        }
        public ToolStripButton Them(string text, Image img, string tip, Action a)
        {
            var b = new ToolStripButton(text, img) { ToolTipText = tip, DisplayStyle = img != null && text != null ? ToolStripItemDisplayStyle.ImageAndText : img != null ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.Text };
            b.Click += (s, e) => a(); Items.Add(b); return b;
        }
    }
}

namespace M8Pro
{
    // ô lọc nhanh dưới tiêu đề cột: viền mảnh như đường kẻ ô của bảng, gõ trực tiếp để lọc hoặc bấm biểu tượng lọc nhỏ để chọn giá trị trong danh sách
    public class OLoc : System.Windows.Forms.Panel
    {
        readonly System.Windows.Forms.TextBox O = new System.Windows.Forms.TextBox { BorderStyle = System.Windows.Forms.BorderStyle.None };
        readonly System.Windows.Forms.Label phieu = new System.Windows.Forms.Label { Dock = System.Windows.Forms.DockStyle.Right, Width = 13, Cursor = System.Windows.Forms.Cursors.Hand };
        readonly System.Windows.Forms.ToolTip tip = new System.Windows.Forms.ToolTip();
        public System.Drawing.Color Vien = System.Drawing.Color.FromArgb(200, 200, 200);
        public System.Func<System.Collections.Generic.IEnumerable<string>> GiaTri;
        public event System.EventHandler Doi;
        public string Tip { set { tip.SetToolTip(O, value); tip.SetToolTip(phieu, "Chọn giá trị để lọc"); } }
        public OLoc()
        {
            BackColor = System.Drawing.Color.White; Padding = new System.Windows.Forms.Padding(3, 3, 1, 1); DoubleBuffered = true;
            O.Dock = System.Windows.Forms.DockStyle.Fill; Controls.Add(O); Controls.Add(phieu);
            O.TextChanged += (s, e) => { phieu.Invalidate(); var d = Doi; if (d != null) d(this, System.EventArgs.Empty); };
            phieu.Paint += (s, e) =>
            {
                // phễu lọc nhỏ 7×7: xám khi chưa chọn, xanh đậm khi đang lọc khớp đúng một giá trị
                bool dang = (O.Text ?? "").StartsWith("="); var c = dang ? System.Drawing.Color.FromArgb(0, 90, 170) : System.Drawing.Color.FromArgb(128, 128, 128);
                int x = (phieu.Width - 7) / 2, y = (phieu.Height - 8) / 2;
                var pt = new[] { new System.Drawing.Point(x, y), new System.Drawing.Point(x + 7, y), new System.Drawing.Point(x + 4, y + 4), new System.Drawing.Point(x + 4, y + 8), new System.Drawing.Point(x + 3, y + 7), new System.Drawing.Point(x + 3, y + 4) };
                using (var b = new System.Drawing.SolidBrush(c)) e.Graphics.FillPolygon(b, pt);
            };
            phieu.MouseDown += (s, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) DanhSach(); };
            O.KeyDown += (s, e) => { if (e.Alt && e.KeyCode == System.Windows.Forms.Keys.Down) { e.Handled = true; DanhSach(); } };
        }
        public override string Text { get { return O.Text; } set { O.Text = value; } }
        protected override void OnFontChanged(System.EventArgs e) { base.OnFontChanged(e); O.Font = Font; }
        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            base.OnPaint(e); using (var p = new System.Drawing.Pen(Vien)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
        protected override void OnResize(System.EventArgs e) { base.OnResize(e); Invalidate(); }
        // danh sách chọn: (Tất cả) – bỏ lọc; (Ô trống); các giá trị khác nhau của cột
        void DanhSach()
        {
            var ds = new System.Collections.Generic.List<string> { "(Tất cả)", "(Ô trống)" }; if (GiaTri != null) ds.AddRange(GiaTri());
            var lb = new System.Windows.Forms.ListBox { BorderStyle = System.Windows.Forms.BorderStyle.None, IntegralHeight = false, Font = Font };
            lb.Items.AddRange(ds.ToArray());
            int w = Width; using (var g = CreateGraphics()) foreach (var x in ds) w = System.Math.Max(w, System.Windows.Forms.TextRenderer.MeasureText(g, x, Font).Width + 28);
            lb.Size = new System.Drawing.Size(System.Math.Min(w, 520), System.Math.Min(ds.Count * lb.ItemHeight + 4, 300));
            var cur = O.Text ?? ""; lb.SelectedIndex = cur == "" ? 0 : cur == "=" ? 1 : System.Math.Max(0, ds.IndexOf(cur.Substring(cur.StartsWith("=") ? 1 : 0)));
            var host = new System.Windows.Forms.ToolStripControlHost(lb) { Padding = System.Windows.Forms.Padding.Empty, Margin = System.Windows.Forms.Padding.Empty, AutoSize = false, Size = lb.Size };
            var dd = new System.Windows.Forms.ToolStripDropDown { Padding = new System.Windows.Forms.Padding(1) }; dd.Items.Add(host);
            System.Action chon = () => { int i = lb.SelectedIndex; if (i < 0) return; dd.Close(); O.Text = i == 0 ? "" : i == 1 ? "=" : "=" + ds[i]; O.Focus(); O.SelectionStart = O.Text.Length; };
            lb.MouseClick += (s, e) => chon(); lb.KeyDown += (s, e) => { if (e.KeyCode == System.Windows.Forms.Keys.Enter) { e.Handled = true; chon(); } else if (e.KeyCode == System.Windows.Forms.Keys.Escape) dd.Close(); };
            dd.Closed += (s, e) => dd.Dispose();
            dd.Show(this, new System.Drawing.Point(0, Height)); lb.Focus();
        }
    }
}

namespace M8Pro
{
    // ---------- ô tích tự vẽ dùng chung mọi hộp thoại: nét mảnh như ô nhập, ô tích luôn cùng hàng (cùng tâm) với dòng chữ đầu,
    // chữ dài xuống dòng thẳng lề sau ô tích; giữ nguyên phông, màu chữ của hộp thoại ----------
    public class OTich : System.Windows.Forms.CheckBox
    {
        const int O = 13, CACH = 5;
        bool tren;
        public OTich()
        {
            SetStyle(System.Windows.Forms.ControlStyles.UserPaint | System.Windows.Forms.ControlStyles.AllPaintingInWmPaint | System.Windows.Forms.ControlStyles.OptimizedDoubleBuffer | System.Windows.Forms.ControlStyles.ResizeRedraw | System.Windows.Forms.ControlStyles.SupportsTransparentBackColor, true);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); tren = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); tren = false; Invalidate(); }
        public override System.Drawing.Size GetPreferredSize(System.Drawing.Size proposed)
        {
            if (Appearance != System.Windows.Forms.Appearance.Normal) return base.GetPreferredSize(proposed);
            var t = System.Windows.Forms.TextRenderer.MeasureText(Text ?? "", Font, System.Drawing.Size.Empty, System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.SingleLine);
            return new System.Drawing.Size(Padding.Horizontal + O + CACH + t.Width + 2, Math.Max(O, t.Height) + Padding.Vertical + 4);
        }
        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            if (Appearance != System.Windows.Forms.Appearance.Normal) { base.OnPaint(e); return; }
            var g = e.Graphics;
            // nền: theo màu nền của hộp/nhóm chứa ô tích
            var nen = BackColor; if (nen.A < 255 && Parent != null) nen = Parent.BackColor; if (nen.A < 255) nen = System.Drawing.SystemColors.Control;
            using (var b = new System.Drawing.SolidBrush(nen)) g.FillRectangle(b, ClientRectangle);
            var f = System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.WordBreak | System.Windows.Forms.TextFormatFlags.Left;
            int x = Padding.Left, w = Math.Max(10, ClientSize.Width - x - O - CACH - Padding.Right);
            int dong = System.Windows.Forms.TextRenderer.MeasureText("Ág", Font).Height;
            int caoChu = string.IsNullOrEmpty(Text) ? dong : System.Windows.Forms.TextRenderer.MeasureText(Text, Font, new System.Drawing.Size(w, 10000), f).Height;
            // dòng chữ đầu: một dòng thì căn giữa theo chiều cao; nhiều dòng (Gian) thì bám trên
            int yChu = caoChu <= dong && (CheckAlign & (System.Drawing.ContentAlignment.TopLeft | System.Drawing.ContentAlignment.TopCenter | System.Drawing.ContentAlignment.TopRight)) == 0
                ? (ClientSize.Height - dong) / 2 : Padding.Top + 1;
            var o = new System.Drawing.Rectangle(x, yChu + (dong - O) / 2 + 1, O - 1, O - 1);
            var vienMau = !Enabled ? System.Drawing.Color.FromArgb(188, 188, 188) : tren || Focused ? System.Drawing.Color.FromArgb(0, 120, 215) : System.Drawing.Color.FromArgb(122, 122, 122);
            g.FillRectangle(Enabled ? System.Drawing.Brushes.White : System.Drawing.Brushes.WhiteSmoke, o);
            using (var p = new System.Drawing.Pen(vienMau)) g.DrawRectangle(p, o);
            if (CheckState == System.Windows.Forms.CheckState.Checked)
            {
                var sm = g.SmoothingMode; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var p = new System.Drawing.Pen(Enabled ? System.Drawing.Color.FromArgb(33, 33, 33) : System.Drawing.Color.FromArgb(150, 150, 150), 1.6f))
                    g.DrawLines(p, new[] { new System.Drawing.PointF(o.X + 2.5f, o.Y + 6.5f), new System.Drawing.PointF(o.X + 5f, o.Y + 9f), new System.Drawing.PointF(o.X + 9.5f, o.Y + 3.5f) });
                g.SmoothingMode = sm;
            }
            else if (CheckState == System.Windows.Forms.CheckState.Indeterminate)
                using (var b = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(90, 90, 90))) g.FillRectangle(b, o.X + 3, o.Y + 3, O - 7, O - 7);
            var tr = new System.Drawing.Rectangle(o.Right + CACH, yChu, w, ClientSize.Height - yChu);
            System.Windows.Forms.TextRenderer.DrawText(g, Text ?? "", Font, tr, Enabled ? ForeColor : System.Drawing.SystemColors.GrayText, f);
            if (Focused && ShowFocusCues && !string.IsNullOrEmpty(Text))
            {
                var sz = System.Windows.Forms.TextRenderer.MeasureText(Text, Font, new System.Drawing.Size(w, 10000), f);
                System.Windows.Forms.ControlPaint.DrawFocusRectangle(g, new System.Drawing.Rectangle(tr.X - 1, tr.Y, Math.Min(w, sz.Width) + 2, Math.Min(tr.Height, sz.Height)));
            }
        }
    }
}
