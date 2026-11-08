using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Threading;

static class Ui {
    public static readonly Color Bar = Color.FromArgb(32, 38, 50);
    public static readonly Color Group = Color.FromArgb(48, 56, 72);
    public static readonly Color Active = Color.FromArgb(47, 111, 214);
    public static readonly Color Primary = Color.FromArgb(37, 120, 245);
    public static readonly Color Hover = Color.FromArgb(70, 82, 102);
    public static readonly Color Line = Color.FromArgb(96, 112, 138);
    public static readonly Color Muted = Color.FromArgb(176, 186, 200);
    public static readonly Color Border = Color.FromArgb(76, 86, 103);
}

static class AppIcons {
    static MemoryStream data;
    static Icon shared;
    public static Icon Clone() {
        if (shared == null) {
            Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream("App.ico");
            if (input == null) shared = (Icon)SystemIcons.Application.Clone();
            else {
                data = new MemoryStream();
                input.CopyTo(data);
                input.Dispose();
                data.Position = 0;
                shared = new Icon(data);
            }
        }
        return (Icon)shared.Clone();
    }
}

static class Program {
    [STAThread]
    static void Main() {
        bool created;
        using (Mutex mutex = new Mutex(true, @"Local\LightShotLocal.SingleInstance", out created)) {
            if (!created) {
                MessageBox.Show("SnipLite 已在运行，请使用托盘图标或快捷键。", "SnipLite");
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Tray());
        }
    }
}

class Tray : Form {
    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint key);
    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr h, int id);
    NotifyIcon tray;
    int key = (int)Keys.A;
    int mod = 6;

    public Tray() {
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Visible = false;
        LoadSettings();
        MigrateStartup();
        tray = new NotifyIcon();
        tray.Icon = AppIcons.Clone();
        tray.Text = "SnipLite";
        tray.Visible = true;
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("截图", null, delegate { StartCapture(); });
        menu.Items.Add("设置", null, delegate { Settings(); });
        menu.Items.Add("退出", null, delegate { Close(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { StartCapture(); };
        if (!RegisterHotKey(Handle, 1, (uint)mod, (uint)key)) MessageBox.Show("快捷键注册失败，请在设置中修改。", "SnipLite");
    }

    protected override void SetVisibleCore(bool v) { base.SetVisibleCore(false); }
    protected override void WndProc(ref Message m) {
        if (m.Msg == 0x312) StartCapture();
        base.WndProc(ref m);
    }
    void StartCapture() {
        using (CaptureForm overlay = new CaptureForm()) overlay.ShowDialog();
    }
    void MigrateStartup() {
        try {
            using (RegistryKey r = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
                if (r == null || r.GetValue("LightShotLocal") == null) return;
                if (r.GetValue("SnipLite") == null) r.SetValue("SnipLite", "\"" + Application.ExecutablePath + "\"");
                r.DeleteValue("LightShotLocal", false);
            }
        } catch (Exception) { }
    }
    void LoadSettings() {
        using (RegistryKey r = Registry.CurrentUser.OpenSubKey(@"Software\LightShotLocal")) {
            if (r != null) {
                key = (int)r.GetValue("Key", (int)Keys.A);
                mod = (int)r.GetValue("Mod", 6);
            }
        }
    }
    void Settings() {
        using (Form f = new Form()) {
            f.Text = "设置";
            f.Icon = AppIcons.Clone();
            f.Size = new Size(420, 250);
            f.StartPosition = FormStartPosition.CenterScreen;
            Label label = new Label();
            label.Text = "点击输入框并按下快捷键";
            label.Left = 20;
            label.Top = 20;
            label.AutoSize = true;
            TextBox box = new TextBox();
            box.Left = 20;
            box.Top = 48;
            box.Width = 270;
            box.ReadOnly = true;
            box.Text = Format();
            int nk = key, nm = mod;
            box.KeyDown += delegate(object s, KeyEventArgs e) {
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu) return;
                nk = (int)e.KeyCode;
                nm = (e.Control ? 2 : 0) | (e.Shift ? 4 : 0) | (e.Alt ? 1 : 0);
                box.Text = e.Modifiers + " + " + e.KeyCode;
                e.SuppressKeyPress = true;
            };
            CheckBox start = new CheckBox();
            start.Left = 20;
            start.Top = 80;
            start.Text = "开机启动";
            start.AutoSize = true;
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) start.Checked = run != null && (run.GetValue("SnipLite") != null || run.GetValue("LightShotLocal") != null);
            Label pathLabel = new Label();
            pathLabel.Left = 20;
            pathLabel.Top = 112;
            pathLabel.Text = "默认保存位置";
            pathLabel.AutoSize = true;
            string initialPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            using (RegistryKey r = Registry.CurrentUser.OpenSubKey(@"Software\LightShotLocal")) {
                if (r != null) initialPath = (string)r.GetValue("SavePath", initialPath);
            }
            TextBox pathBox = new TextBox();
            pathBox.Left = 20;
            pathBox.Top = 135;
            pathBox.Width = 290;
            pathBox.Text = initialPath;
            Button browse = new Button();
            browse.Left = 315;
            browse.Top = 133;
            browse.Text = "选择";
            browse.Click += delegate {
                using (FolderBrowserDialog d = new FolderBrowserDialog()) {
                    d.SelectedPath = pathBox.Text;
                    if (d.ShowDialog() == DialogResult.OK) pathBox.Text = d.SelectedPath;
                }
            };
            Button save = new Button();
            save.Left = 310;
            save.Top = 175;
            save.Text = "保存";
            save.Click += delegate {
                if (!System.IO.Directory.Exists(pathBox.Text)) {
                    MessageBox.Show("请选择有效的保存位置。");
                    return;
                }
                try {
                    UnregisterHotKey(Handle, 1);
                    if (!RegisterHotKey(Handle, 1, (uint)nm, (uint)nk)) {
                        RegisterHotKey(Handle, 1, (uint)mod, (uint)key);
                        MessageBox.Show("快捷键已被占用，请更换。");
                        return;
                    }
                    key = nk;
                    mod = nm;
                    using (RegistryKey r = Registry.CurrentUser.CreateSubKey(@"Software\LightShotLocal")) {
                        r.SetValue("Key", key);
                        r.SetValue("Mod", mod);
                        r.SetValue("SavePath", pathBox.Text);
                    }
                    using (RegistryKey r = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
                        if (start.Checked) r.SetValue("SnipLite", "\"" + Application.ExecutablePath + "\"");
                        else r.DeleteValue("SnipLite", false);
                        r.DeleteValue("LightShotLocal", false);
                    }
                    f.Close();
                } catch (Exception ex) {
                    MessageBox.Show("保存设置失败：" + ex.Message, "SnipLite");
                }
            };
            f.Controls.AddRange(new Control[] { label, box, start, pathLabel, pathBox, browse, save });
            f.ShowDialog();
        }
    }
    string Format() {
        return ((mod & 2) != 0 ? "Ctrl + " : "") + ((mod & 4) != 0 ? "Shift + " : "") + ((mod & 1) != 0 ? "Alt + " : "") + ((Keys)key);
    }
    protected override void OnFormClosed(FormClosedEventArgs e) {
        UnregisterHotKey(Handle, 1);
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosed(e);
    }
}

class CaptureForm : Form {
    Bitmap screen;
    Rectangle bounds, selection;
    Point start, current;
    bool dragging;
    string tool = "";
    string selectionDrag = "";
    Rectangle originalSelection;
    int lineWidth = 3;
    int textSize = 22;
    Font textFont;
    Point markOffset = Point.Empty, originalOffset;
    Dictionary<string, Color> toolColors = new Dictionary<string, Color>();
    Dictionary<string, int> toolWidths = new Dictionary<string, int>();
    FlowLayoutPanel options;
    MoreColorButton moreColor;
    List<ColorChip> colorChips = new List<ColorChip>();
    List<WidthChip> widthChips = new List<WidthChip>();
    List<TextSizeChip> textChips = new List<TextSizeChip>();
    FlowLayoutPanel styleGroup;
    List<Action<Graphics>> marks = new List<Action<Graphics>>();
    List<Point> stroke;
    FlowLayoutPanel bar;
    TextBox text;
    Color color = Color.Red;
    Screen coachScreen;
    Font uiFont = new Font("Microsoft YaHei UI", 9f);
    Font coachTitleFont = new Font("Microsoft YaHei UI", 16f, FontStyle.Regular, GraphicsUnit.Point);
    Font coachSubFont = new Font("Microsoft YaHei UI", 11f, FontStyle.Regular, GraphicsUnit.Point);

    public CaptureForm() {
        toolColors["矩形"] = Color.Red;
        toolColors["箭头"] = Color.Red;
        toolColors["画笔"] = Color.Red;
        toolColors["文字"] = Color.Red;
        toolWidths["矩形"] = 3;
        toolWidths["箭头"] = 3;
        toolWidths["画笔"] = 3;
        toolWidths["文字"] = 22;
        bounds = SystemInformation.VirtualScreen;
        screen = new Bitmap(bounds.Width, bounds.Height);
        using (Graphics g = Graphics.FromImage(screen)) g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;
        Icon = AppIcons.Clone();
        AccessibleName = "截图";
        AccessibleDescription = "拖动框选截图区域，按 Esc 取消";
        bar = new FlowLayoutPanel();
        bar.AutoSize = true;
        bar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        bar.WrapContents = false;
        bar.BackColor = Ui.Bar;
        bar.Padding = new Padding(6);
        bar.Margin = Padding.Empty;
        bar.Visible = false;
        FlowLayoutPanel drawGroup = MakeGroup("绘图", false);
        FlowLayoutPanel undoGroup = MakeGroup("撤销", false);
        FlowLayoutPanel outputGroup = MakeGroup("固定、保存、复制", false);
        outputGroup.Margin = new Padding(0, 0, 18, 0);
        FlowLayoutPanel cancelGroup = MakeGroup("取消", true);
        drawGroup.Controls.Add(MakeButton("选区"));
        drawGroup.Controls.Add(MakeButton("矩形"));
        drawGroup.Controls.Add(MakeButton("箭头"));
        drawGroup.Controls.Add(MakeButton("画笔"));
        drawGroup.Controls.Add(MakeButton("文字"));
        undoGroup.Controls.Add(MakeButton("撤销"));
        outputGroup.Controls.Add(MakeButton("固定"));
        outputGroup.Controls.Add(MakeButton("保存"));
        outputGroup.Controls.Add(MakeButton("复制"));
        cancelGroup.Controls.Add(MakeButton("取消"));
        bar.Controls.Add(drawGroup);
        bar.Controls.Add(undoGroup);
        bar.Controls.Add(outputGroup);
        bar.Controls.Add(cancelGroup);
        Controls.Add(bar);

        options = new FlowLayoutPanel();
        options.AutoSize = true;
        options.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        options.WrapContents = false;
        options.BackColor = Ui.Bar;
        options.Padding = new Padding(6);
        options.Margin = Padding.Empty;
        options.Visible = false;
        FlowLayoutPanel colorGroup = MakeGroup("颜色", false);
        styleGroup = MakeGroup("线宽", true);
        Color[] palette = new Color[] { Color.Red, Color.Orange, Color.Yellow, Color.LimeGreen, Color.DeepSkyBlue, Color.White, Color.Black };
        string[] paletteNames = new string[] { "红色", "橙色", "黄色", "绿色", "蓝色", "白色", "黑色" };
        for (int i = 0; i < palette.Length; i++) {
            Color chosen = palette[i];
            ColorChip chip = new ColorChip(chosen, paletteNames[i]);
            chip.Click += delegate {
                color = chosen;
                if (toolColors.ContainsKey(tool)) toolColors[tool] = chosen;
                SaveStyles();
                UpdateOptions();
                ApplyTextStyle();
            };
            colorChips.Add(chip);
            colorGroup.Controls.Add(chip);
        }
        moreColor = new MoreColorButton();
        moreColor.Font = uiFont;
        moreColor.Click += delegate {
            using (ColorDialog dialog = new ColorDialog()) {
                dialog.Color = color;
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) == DialogResult.OK) {
                    color = dialog.Color;
                    if (toolColors.ContainsKey(tool)) toolColors[tool] = color;
                    SaveStyles();
                    UpdateOptions();
                    ApplyTextStyle();
                }
            }
        };
        colorGroup.Controls.Add(moreColor);
        int[] widths = new int[] { 3, 6, 10 };
        for (int i = 0; i < widths.Length; i++) {
            int chosenWidth = widths[i];
            WidthChip line = new WidthChip(chosenWidth);
            line.Click += delegate {
                lineWidth = chosenWidth;
                if (toolWidths.ContainsKey(tool)) toolWidths[tool] = chosenWidth;
                SaveStyles();
                UpdateOptions();
            };
            widthChips.Add(line);
            styleGroup.Controls.Add(line);
        }
        int[] sizes = new int[] { 14, 22, 32 };
        for (int i = 0; i < sizes.Length; i++) {
            int chosenSize = sizes[i];
            TextSizeChip sample = new TextSizeChip(chosenSize);
            sample.Visible = false;
            sample.Click += delegate {
                textSize = chosenSize;
                toolWidths["文字"] = chosenSize;
                SaveStyles();
                UpdateOptions();
                ApplyTextStyle();
            };
            textChips.Add(sample);
            styleGroup.Controls.Add(sample);
        }
        options.Controls.Add(colorGroup);
        options.Controls.Add(styleGroup);
        Controls.Add(options);
        LoadStyles();
        RefreshToolButtons();
        KeyDown += delegate(object s, KeyEventArgs e) {
            if (e.KeyCode == Keys.Escape && text == null) Close();
            if (e.Control && e.KeyCode == Keys.Z) Undo();
            if (e.Control && e.KeyCode == Keys.C && selection.Width > 2 && selection.Height > 2) Command("复制");
            if (e.KeyCode == Keys.Enter && selection.Width > 2 && selection.Height > 2 && text == null) Command("复制");
        };
        Shown += delegate { Invalidate(); };
    }

    FlowLayoutPanel MakeGroup(string accessibleName, bool last) {
        FlowLayoutPanel group = new FlowLayoutPanel();
        group.AutoSize = true;
        group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        group.WrapContents = false;
        group.BackColor = Ui.Group;
        group.Padding = new Padding(4, 3, 4, 3);
        group.Margin = new Padding(0, 0, last ? 0 : 10, 0);
        group.AccessibleName = accessibleName;
        group.Paint += delegate(object s, PaintEventArgs e) {
            Control p = (Control)s;
            if (p.Width < 2 || p.Height < 2) return;
            using (Pen pen = new Pen(Ui.Line)) e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        return group;
    }

    Button MakeButton(string label) {
        Button b = new Button();
        b.Text = label;
        b.AutoSize = true;
        b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        b.FlatStyle = FlatStyle.Flat;
        b.UseVisualStyleBackColor = false;
        b.BackColor = label == "复制" ? Ui.Primary : Ui.Group;
        b.ForeColor = label == "取消" ? Ui.Muted : Color.White;
        b.Font = uiFont;
        b.Padding = new Padding(8, 4, 8, 4);
        b.Margin = new Padding(label == "复制" ? 8 : 1, 1, 1, 1);
        b.Cursor = Cursors.Hand;
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = label == "复制" ? Color.FromArgb(64, 140, 255) : Ui.Hover;
        string command = label;
        b.Click += delegate { Command(command); RefreshToolButtons(); };
        return b;
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.DrawImageUnscaled(screen, 0, 0);
        using (SolidBrush shade = new SolidBrush(Color.FromArgb(125, 0, 0, 0))) e.Graphics.FillRectangle(shade, ClientRectangle);
        if (selection.Width == 0 || selection.Height == 0) {
            Rectangle screenBounds = Screen.FromPoint(Cursor.Position).Bounds;
            Rectangle area = new Rectangle(screenBounds.Left - bounds.Left, screenBounds.Top - bounds.Top, screenBounds.Width, screenBounds.Height);
            DrawCoach(e.Graphics, area);
        }
        if (selection.Width > 0 && selection.Height > 0) {
            e.Graphics.DrawImage(screen, selection, selection, GraphicsUnit.Pixel);
            GraphicsState state = e.Graphics.Save();
            e.Graphics.SetClip(selection);
            e.Graphics.TranslateTransform(markOffset.X, markOffset.Y);
            foreach (Action<Graphics> a in marks) a(e.Graphics);
            e.Graphics.TranslateTransform(-markOffset.X, -markOffset.Y);
            if (dragging && (tool == "矩形" || tool == "箭头" || tool == "画笔")) DrawPreview(e.Graphics);
            e.Graphics.Restore(state);
            using (Pen p = new Pen(Color.DeepSkyBlue, 2)) e.Graphics.DrawRectangle(p, selection);
            foreach (Point pt in Handles()) e.Graphics.FillRectangle(Brushes.White, pt.X - 4, pt.Y - 4, 8, 8);
            string sizeText = selection.Width + " × " + selection.Height;
            SizeF size = e.Graphics.MeasureString(sizeText, uiFont);
            float sx = selection.X;
            float sy = Math.Max(0, selection.Y - size.Height - 4);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(210, 32, 38, 50))) e.Graphics.FillRectangle(bg, sx, sy, size.Width + 8, size.Height);
            e.Graphics.DrawString(sizeText, uiFont, Brushes.White, sx + 4, sy);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (tool == "文字" && selection.Contains(e.Location)) {
            if (text != null) { text.Focus(); return; }
            text = new TextBox();
            text.Location = e.Location;
            text.BorderStyle = BorderStyle.FixedSingle;
            text.AccessibleName = "输入文字，Enter 确认，Escape 取消";
            text.TextChanged += delegate { ResizeTextBox(); };
            text.KeyDown += delegate(object s, KeyEventArgs k) {
                if (k.KeyCode == Keys.Escape) { k.SuppressKeyPress = true; CloseText(false); return; }
                if (k.KeyCode == Keys.Enter) { k.SuppressKeyPress = true; CloseText(true); }
            };
            Controls.Add(text);
            ApplyTextStyle();
            text.BringToFront();
            bar.BringToFront();
            options.BringToFront();
            text.Focus();
            return;
        }
        start = e.Location;
        current = e.Location;
        dragging = true;
        selectionDrag = "";
        if (tool == "" && selection.Width > 2 && selection.Height > 2) {
            originalSelection = selection;
            originalOffset = markOffset;
            selectionDrag = HitSelection(e.Location);
            if (selectionDrag != "") {
                Cursor = CursorFor(selectionDrag);
                return;
            }
        }
        if (tool == "画笔") {
            stroke = new List<Point>();
            stroke.Add(e.Location);
        } else if (tool == "") {
            selection = Rectangle.Empty;
            marks.Clear();
            markOffset = Point.Empty;
            bar.Visible = false;
            options.Visible = false;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (!dragging) {
            UpdateSelectionCursor(e.Location);
            if (selection.Width == 0 || selection.Height == 0) {
                Screen now = Screen.FromPoint(Cursor.Position);
                if (coachScreen == null || now.DeviceName != coachScreen.DeviceName) {
                    coachScreen = now;
                    Invalidate();
                }
            }
            return;
        }
        current = e.Location;
        if (tool == "") {
            if (selectionDrag == "move") {
                int dx = Math.Max(-originalSelection.Left, Math.Min(ClientSize.Width - originalSelection.Right, e.X - start.X));
                int dy = Math.Max(-originalSelection.Top, Math.Min(ClientSize.Height - originalSelection.Bottom, e.Y - start.Y));
                selection = new Rectangle(originalSelection.X + dx, originalSelection.Y + dy, originalSelection.Width, originalSelection.Height);
                markOffset = new Point(originalOffset.X + dx, originalOffset.Y + dy);
            } else if (selectionDrag != "") {
                int x = Math.Max(0, Math.Min(ClientSize.Width, e.X));
                int y = Math.Max(0, Math.Min(ClientSize.Height, e.Y));
                int l = selectionDrag.Contains("l") ? Math.Min(x, originalSelection.Right - 1) : originalSelection.Left;
                int r = selectionDrag.Contains("r") ? Math.Max(x, originalSelection.Left + 1) : originalSelection.Right;
                int t = selectionDrag.Contains("t") ? Math.Min(y, originalSelection.Bottom - 1) : originalSelection.Top;
                int b = selectionDrag.Contains("b") ? Math.Max(y, originalSelection.Top + 1) : originalSelection.Bottom;
                selection = Rectangle.FromLTRB(l, t, r, b);
            } else selection = Rect(start, e.Location);
        } else if (tool == "画笔") stroke.Add(e.Location);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        if (!dragging) return;
        dragging = false;
        Point end = e.Location;
        if (tool != "") {
            start = new Point(start.X - markOffset.X, start.Y - markOffset.Y);
            end = new Point(end.X - markOffset.X, end.Y - markOffset.Y);
        }
        Color markColor = color;
        int markWidth = lineWidth;
        if (tool == "矩形") {
            Rectangle r = Rect(start, end);
            marks.Add(delegate(Graphics g) { using (Pen p = new Pen(markColor, markWidth)) g.DrawRectangle(p, r); });
        }
        if (tool == "箭头") {
            Point a = start, b = end;
            marks.Add(delegate(Graphics g) {
                using (Pen p = new Pen(markColor, markWidth))
                using (AdjustableArrowCap cap = new AdjustableArrowCap(5, 6)) {
                    p.CustomEndCap = cap;
                    g.DrawLine(p, a, b);
                }
            });
        }
        if (tool == "画笔" && stroke != null && stroke.Count > 1) {
            Point[] pts = stroke.ConvertAll(delegate(Point pt) { return new Point(pt.X - markOffset.X, pt.Y - markOffset.Y); }).ToArray();
            marks.Add(delegate(Graphics g) { using (Pen p = new Pen(markColor, markWidth)) g.DrawLines(p, pts); });
        }
        if (selection.Width > 2 && selection.Height > 2) PlaceChrome();
        UpdateSelectionCursor(e.Location);
        Invalidate();
    }

    void DrawPreview(Graphics g) {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (Pen p = new Pen(color, lineWidth)) {
            if (tool == "矩形") {
                Rectangle r = Rect(start, current);
                if (r.Width > 0 && r.Height > 0) g.DrawRectangle(p, r);
            } else if (tool == "箭头" && start != current) {
                using (AdjustableArrowCap cap = new AdjustableArrowCap(5, 6)) {
                    p.CustomEndCap = cap;
                    g.DrawLine(p, start, current);
                }
            } else if (tool == "画笔" && stroke != null && stroke.Count > 1) g.DrawLines(p, stroke.ToArray());
        }
    }

    void DrawCoach(Graphics g, Rectangle area) {
        string line1 = "拖动框选截图区域";
        string line2 = "Esc 取消";
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        SizeF s1 = g.MeasureString(line1, coachTitleFont);
        SizeF s2 = g.MeasureString(line2, coachSubFont);
        int w = (int)Math.Max(s1.Width, s2.Width) + 52;
        int h = (int)(s1.Height + s2.Height) + 32;
        int x = area.Left + (area.Width - w) / 2;
        int y = area.Top + Math.Max(28, (area.Height - h) / 5);
        x = Math.Max(area.Left + 12, Math.Min(x, area.Right - w - 12));
        y = Math.Max(area.Top + 12, Math.Min(y, area.Bottom - h - 12));
        using (SolidBrush bg = new SolidBrush(Color.FromArgb(235, 32, 38, 50))) g.FillRectangle(bg, x, y, w, h);
        using (Pen pen = new Pen(Ui.Line)) g.DrawRectangle(pen, x, y, w, h);
        using (SolidBrush accent = new SolidBrush(Color.DeepSkyBlue)) g.FillRectangle(accent, x, y, 4, h);
        float textLeft = x + 20;
        float textTop = y + 12;
        g.DrawString(line1, coachTitleFont, Brushes.White, textLeft, textTop);
        using (SolidBrush sub = new SolidBrush(Ui.Muted)) g.DrawString(line2, coachSubFont, sub, textLeft, textTop + s1.Height + 2);
    }

    void PlaceChrome() {
        bar.PerformLayout();
        Size pref = bar.PreferredSize;
        int x = Math.Max(0, Math.Min(selection.Left, Math.Max(0, ClientSize.Width - pref.Width)));
        int y = selection.Bottom + 8 + pref.Height < ClientSize.Height ? selection.Bottom + 8 : Math.Max(0, selection.Top - 8 - pref.Height);
        bar.Location = new Point(x, y);
        bar.Visible = true;
        bar.BringToFront();
        UpdateOptions();
    }

    void PositionOptions() {
        if (options == null || !options.Visible) return;
        options.PerformLayout();
        Size pref = options.PreferredSize;
        int y = bar.Bottom + pref.Height <= ClientSize.Height ? bar.Bottom : Math.Max(0, bar.Top - pref.Height);
        int x = Math.Max(0, Math.Min(bar.Left, Math.Max(0, ClientSize.Width - pref.Width)));
        options.Location = new Point(x, y);
        options.BringToFront();
    }

    void UpdateOptions() {
        if (options == null) return;
        bool show = toolColors.ContainsKey(tool);
        options.Visible = show && bar.Visible;
        if (!show) return;
        bool letters = tool == "文字";
        if (styleGroup != null) styleGroup.AccessibleName = letters ? "字号" : "线宽";
        bool preset = false;
        foreach (ColorChip chip in colorChips) {
            Color swatch = chip.Swatch;
            chip.Selected = swatch.ToArgb() == color.ToArgb();
            if (chip.Selected) preset = true;
            chip.AccessibleName = (chip.Selected ? "已选择，" : "") + "标注颜色 " + chip.ColorLabel;
            chip.Invalidate();
        }
        foreach (WidthChip chip in widthChips) {
            chip.Visible = !letters;
            chip.Selected = chip.WidthPx == lineWidth;
            chip.AccessibleName = (chip.Selected ? "已选择，" : "") + "线宽 " + chip.WidthPx + " 像素";
            chip.Invalidate();
        }
        foreach (TextSizeChip chip in textChips) {
            chip.Visible = letters;
            chip.Selected = chip.PointSize == textSize;
            chip.AccessibleName = (chip.Selected ? "已选择，" : "") + "字号 " + chip.PointSize;
            chip.Invalidate();
        }
        moreColor.Current = color;
        moreColor.Marked = !preset;
        moreColor.AccessibleName = moreColor.Marked ? "已选择自定义颜色" : "更多颜色";
        moreColor.Invalidate();
        PositionOptions();
    }

    void RefreshToolButtons() {
        foreach (Control group in bar.Controls) {
            foreach (Control c in group.Controls) {
                Button b = c as Button;
                if (b == null) continue;
                bool active = (b.Text == "选区" && tool == "") || b.Text == tool;
                if (b.Text == "复制") b.BackColor = Ui.Primary;
                else if (active) b.BackColor = Ui.Active;
                else b.BackColor = Ui.Group;
            }
        }
        UpdateOptions();
        if (IsHandleCreated) UpdateSelectionCursor(PointToClient(Cursor.Position));
    }

    Point[] Handles() {
        return new Point[] {
            new Point(selection.Left, selection.Top), new Point(selection.Right, selection.Top),
            new Point(selection.Left, selection.Bottom), new Point(selection.Right, selection.Bottom),
            new Point(selection.Left + selection.Width / 2, selection.Top), new Point(selection.Left + selection.Width / 2, selection.Bottom),
            new Point(selection.Left, selection.Top + selection.Height / 2), new Point(selection.Right, selection.Top + selection.Height / 2)
        };
    }

    void LoadStyles() {
        using (RegistryKey r = Registry.CurrentUser.OpenSubKey(@"Software\LightShotLocal\Styles")) {
            if (r == null) return;
            string[] names = new string[] { "矩形", "箭头", "画笔" };
            foreach (string t in names) {
                toolColors[t] = Color.FromArgb((int)r.GetValue(t + "Color", Color.Red.ToArgb()));
                int w = (int)r.GetValue(t + "Width", 3);
                toolWidths[t] = (w == 3 || w == 6 || w == 10) ? w : 3;
            }
            toolColors["文字"] = Color.FromArgb((int)r.GetValue("文字Color", Color.Red.ToArgb()));
            int savedSize = (int)r.GetValue("文字Width", 22);
            toolWidths["文字"] = (savedSize == 14 || savedSize == 22 || savedSize == 32) ? savedSize : 22;
        }
    }

    void SaveStyles() {
        try {
            using (RegistryKey r = Registry.CurrentUser.CreateSubKey(@"Software\LightShotLocal\Styles")) {
                foreach (string t in toolColors.Keys) {
                    r.SetValue(t + "Color", toolColors[t].ToArgb());
                    r.SetValue(t + "Width", toolWidths[t]);
                }
            }
        } catch (Exception ex) {
            MessageBox.Show(this, "无法保存标注样式：" + ex.Message, "SnipLite");
        }
    }

    static Rectangle Rect(Point a, Point b) {
        return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    void Undo() {
        if (marks.Count > 0) {
            marks.RemoveAt(marks.Count - 1);
            Invalidate();
        }
    }

    Bitmap Result() {
        Bitmap b = new Bitmap(selection.Width, selection.Height);
        using (Graphics g = Graphics.FromImage(b)) {
            g.DrawImage(screen, new Rectangle(0, 0, b.Width, b.Height), selection, GraphicsUnit.Pixel);
            g.TranslateTransform(-selection.X + markOffset.X, -selection.Y + markOffset.Y);
            foreach (Action<Graphics> a in marks) a(g);
        }
        return b;
    }

    void CloseText(bool commit) {
        if (text == null) return;
        if (commit && text.Text.Length > 0) {
            string value = text.Text;
            Point p = new Point(text.Left - markOffset.X, text.Top - markOffset.Y);
            Color c = color;
            float emPx = textSize;
            using (Graphics tg = text.CreateGraphics()) emPx = text.Font.SizeInPoints * tg.DpiY / 72f;
            marks.Add(delegate(Graphics g) {
                TextRenderingHint hint = g.TextRenderingHint;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (Font f = new Font("Microsoft YaHei UI", emPx, FontStyle.Regular, GraphicsUnit.Pixel))
                using (SolidBrush brush = new SolidBrush(c)) g.DrawString(value, f, brush, p);
                g.TextRenderingHint = hint;
            });
        }
        text.Font = uiFont;
        Controls.Remove(text);
        text.Dispose();
        text = null;
        if (textFont != null) { textFont.Dispose(); textFont = null; }
        Invalidate();
    }

    void ApplyTextStyle() {
        if (text == null) return;
        Font next = new Font("Microsoft YaHei UI", textSize, FontStyle.Regular, GraphicsUnit.Point);
        text.Font = next;
        text.ForeColor = color;
        float lum = (0.299f * color.R + 0.587f * color.G + 0.114f * color.B) / 255f;
        text.BackColor = lum > 0.62f ? Color.FromArgb(32, 38, 50) : Color.White;
        Font previous = textFont;
        textFont = next;
        if (previous != null) previous.Dispose();
        ResizeTextBox();
    }

    void ResizeTextBox() {
        if (text == null) return;
        string sample = text.Text.Length == 0 ? "文字" : text.Text;
        int minW = TextRenderer.MeasureText("文字", text.Font).Width + 12;
        int wanted = TextRenderer.MeasureText(sample, text.Font).Width + 16;
        int maxW = Math.Max(minW, ClientSize.Width - text.Left - 8);
        text.Width = Math.Max(minW, Math.Min(maxW, wanted));
    }

    string HitSelection(Point p) {
        if (selection.Width <= 2 || selection.Height <= 2) return "";
        int band = 6;
        bool nearL = Math.Abs(p.X - selection.Left) <= band && p.Y >= selection.Top - band && p.Y <= selection.Bottom + band;
        bool nearR = Math.Abs(p.X - selection.Right) <= band && p.Y >= selection.Top - band && p.Y <= selection.Bottom + band;
        bool nearT = Math.Abs(p.Y - selection.Top) <= band && p.X >= selection.Left - band && p.X <= selection.Right + band;
        bool nearB = Math.Abs(p.Y - selection.Bottom) <= band && p.X >= selection.Left - band && p.X <= selection.Right + band;
        if ((nearL || nearR) && (nearT || nearB)) return (nearT ? "t" : "b") + (nearL ? "l" : "r");
        if (nearT) return "t";
        if (nearB) return "b";
        if (nearL) return "l";
        if (nearR) return "r";
        if (selection.Contains(p)) return "move";
        return "";
    }

    Cursor CursorFor(string hit) {
        if (hit == "move") return Cursors.SizeAll;
        bool left = hit.IndexOf('l') >= 0;
        bool right = hit.IndexOf('r') >= 0;
        bool top = hit.IndexOf('t') >= 0;
        bool bottom = hit.IndexOf('b') >= 0;
        if ((top && left) || (bottom && right)) return Cursors.SizeNWSE;
        if ((top && right) || (bottom && left)) return Cursors.SizeNESW;
        if (top || bottom) return Cursors.SizeNS;
        if (left || right) return Cursors.SizeWE;
        return Cursors.Cross;
    }

    void UpdateSelectionCursor(Point p) {
        if (text != null) return;
        if (tool != "" || selection.Width <= 2 || selection.Height <= 2) Cursor = Cursors.Cross;
        else Cursor = CursorFor(HitSelection(p));
    }

    void Command(string s) {
        if (s == "选区") { tool = ""; return; }
        if (s == "取消") { Close(); return; }
        if (text != null) {
            MessageBox.Show(this, "请先按 Enter 确认文字，或按 Esc 取消输入。", "SnipLite");
            text.Focus();
            return;
        }
        if (s == "撤销") { Undo(); return; }
        if (s == "矩形" || s == "箭头" || s == "画笔" || s == "文字") {
            tool = s;
            if (toolColors.ContainsKey(s)) color = toolColors[s];
            if (s == "文字") textSize = toolWidths["文字"];
            else if (toolWidths.ContainsKey(s)) lineWidth = toolWidths[s];
            ApplyTextStyle();
            return;
        }
        using (Bitmap b = Result()) {
            if (Output.Execute(s, b, this)) Close();
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing && screen != null) screen.Dispose();
        base.Dispose(disposing);
        if (disposing) {
            uiFont.Dispose();
            coachTitleFont.Dispose();
            coachSubFont.Dispose();
            if (textFont != null) textFont.Dispose();
        }
    }
}

static class Output {
    public static bool Execute(string action, Bitmap image, IWin32Window owner) {
        try {
            if (action == "复制") {
                Clipboard.SetImage(image);
                return true;
            }
            if (action == "固定") {
                new Pin(image).Show();
                return true;
            }
            if (action == "保存") {
                string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                using (RegistryKey r = Registry.CurrentUser.OpenSubKey(@"Software\LightShotLocal")) {
                    if (r != null) folder = (string)r.GetValue("SavePath", folder);
                }
                using (SaveFileDialog d = new SaveFileDialog()) {
                    d.InitialDirectory = folder;
                    d.Filter = "PNG 图片|*.png";
                    d.FileName = "截图_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                    if (d.ShowDialog(owner) != DialogResult.OK) return false;
                    image.Save(d.FileName, ImageFormat.Png);
                    return true;
                }
            }
        } catch (Exception ex) {
            MessageBox.Show(owner, "操作失败：" + ex.Message, "SnipLite");
        }
        return false;
    }
}

class Pin : Form {
    const int CaptionHeight = 36;
    const int Grip = 6;
    Bitmap image;
    PictureBox picture;
    Panel panel;
    Button pinButton;
    Font uiFont = new Font("Microsoft YaHei UI", 9f);
    Font closeFont = new Font("Microsoft YaHei UI", 14f);
    Point anchor;
    bool move;
    double scale;

    public Pin(Bitmap b) {
        image = new Bitmap(b);
        FormBorderStyle = FormBorderStyle.None;
        Text = "贴图";
        AccessibleName = "贴图";
        BackColor = Ui.Border;
        Padding = new Padding(1);
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        Icon = AppIcons.Clone();
        scale = Math.Min(1.0, 800.0 / Math.Max(1, image.Width));
        int viewW = Math.Max(120, (int)(image.Width * scale));
        int viewH = Math.Max(80, Math.Min(600, (int)(image.Height * scale)));
        ClientSize = new Size(viewW + 2, viewH + CaptionHeight + 2);
        MinimumSize = new Size(220, CaptionHeight + 100);

        Panel caption = new Panel();
        caption.Dock = DockStyle.Top;
        caption.Height = CaptionHeight;
        caption.BackColor = Ui.Bar;
        Label titleLabel = new Label();
        titleLabel.Text = "贴图";
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        titleLabel.ForeColor = Color.White;
        titleLabel.BackColor = Ui.Bar;
        titleLabel.Font = uiFont;
        titleLabel.Padding = new Padding(12, 0, 0, 0);
        titleLabel.AutoEllipsis = false;
        pinButton = CaptionButton("置顶", 56, uiFont);
        pinButton.AccessibleName = "切换置顶";
        pinButton.Click += delegate { TopMost = !TopMost; SyncTopMost(); };
        Button closeButton = CaptionButton("×", 40, closeFont);
        closeButton.AccessibleName = "关闭";
        closeButton.Click += delegate { Close(); };
        closeButton.MouseEnter += delegate { closeButton.BackColor = Color.FromArgb(196, 56, 48); };
        closeButton.MouseLeave += delegate { closeButton.BackColor = Ui.Bar; };
        caption.Controls.Add(titleLabel);
        caption.Controls.Add(pinButton);
        caption.Controls.Add(closeButton);

        panel = new Panel();
        panel.Dock = DockStyle.Fill;
        panel.AutoScroll = true;
        panel.BackColor = Ui.Bar;
        picture = new PictureBox();
        picture.Image = image;
        picture.SizeMode = PictureBoxSizeMode.StretchImage;
        picture.BackColor = Ui.Bar;
        panel.Controls.Add(picture);
        Controls.Add(panel);
        Controls.Add(caption);
        ResizeImage();
        SyncTopMost();
        HookMove(picture);
        HookMove(titleLabel);
        HookMove(caption);
        picture.DoubleClick += delegate { Close(); };
        MouseEventHandler zoom = delegate(object s, MouseEventArgs e) {
            scale = Math.Max(0.05, Math.Min(4.0, scale * (e.Delta > 0 ? 1.1 : 1.0 / 1.1)));
            ResizeImage();
        };
        picture.MouseWheel += zoom;
        panel.MouseWheel += zoom;
        picture.MouseEnter += delegate { picture.Focus(); };
        panel.MouseEnter += delegate { panel.Focus(); };

        ContextMenuStrip menu = new ContextMenuStrip();
        foreach (string t in new string[] { "复制", "保存", "切换置顶", "关闭" }) {
            string item = t;
            menu.Items.Add(item, null, delegate {
                if (item == "关闭") Close();
                else if (item == "切换置顶") { TopMost = !TopMost; SyncTopMost(); }
                else Output.Execute(item, image, this);
            });
        }
        picture.ContextMenuStrip = menu;
        panel.ContextMenuStrip = menu;
        caption.ContextMenuStrip = menu;
        titleLabel.ContextMenuStrip = menu;
    }

    Button CaptionButton(string text, int width, Font font) {
        Button b = new Button();
        b.Text = text;
        b.Width = width;
        b.Dock = DockStyle.Right;
        b.FlatStyle = FlatStyle.Flat;
        b.UseVisualStyleBackColor = false;
        b.ForeColor = Color.White;
        b.BackColor = Ui.Bar;
        b.Font = font;
        b.Cursor = Cursors.Hand;
        b.TabStop = false;
        b.Margin = Padding.Empty;
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Ui.Hover;
        return b;
    }

    void SyncTopMost() {
        pinButton.BackColor = TopMost ? Ui.Active : Ui.Bar;
        pinButton.FlatAppearance.MouseOverBackColor = TopMost ? Color.FromArgb(70, 130, 214) : Ui.Hover;
        pinButton.AccessibleName = TopMost ? "已置顶" : "置顶";
    }

    void HookMove(Control c) {
        c.MouseDown += delegate(object s, MouseEventArgs e) {
            if (e.Button != MouseButtons.Left) return;
            anchor = Cursor.Position;
            move = true;
            c.Capture = true;
        };
        c.MouseMove += delegate {
            if (!move || !c.Capture) return;
            Point pt = Cursor.Position;
            Left += pt.X - anchor.X;
            Top += pt.Y - anchor.Y;
            anchor = pt;
        };
        c.MouseUp += delegate {
            move = false;
            c.Capture = false;
        };
    }

    void ResizeImage() {
        picture.Size = new Size(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
    }

    protected override CreateParams CreateParams {
        get {
            CreateParams cp = base.CreateParams;
            cp.Style |= 0x00020000;
            cp.ClassStyle |= 0x00020000;
            return cp;
        }
    }

    protected override void WndProc(ref Message m) {
        if (m.Msg == 0x84) {
            base.WndProc(ref m);
            if ((int)m.Result == 1) {
                Point p = PointToClient(Cursor.Position);
                bool left = p.X < Grip;
                bool right = p.X >= ClientSize.Width - Grip;
                bool top = p.Y < Grip;
                bool bottom = p.Y >= ClientSize.Height - Grip;
                if (top && left) m.Result = (IntPtr)13;
                else if (top && right) m.Result = (IntPtr)14;
                else if (bottom && left) m.Result = (IntPtr)16;
                else if (bottom && right) m.Result = (IntPtr)17;
                else if (left) m.Result = (IntPtr)10;
                else if (right) m.Result = (IntPtr)11;
                else if (top) m.Result = (IntPtr)12;
                else if (bottom) m.Result = (IntPtr)15;
            }
            return;
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing) {
        if (disposing && image != null) image.Dispose();
        base.Dispose(disposing);
        if (disposing) {
            uiFont.Dispose();
            closeFont.Dispose();
        }
    }
}

class ColorChip : Control {
    public Color Swatch;
    public string ColorLabel;
    public bool Selected;
    bool hover;
    public ColorChip(Color swatch, string label) {
        Swatch = swatch;
        ColorLabel = label;
        Size = new Size(30, 30);
        Margin = new Padding(2);
        Cursor = Cursors.Hand;
        BackColor = Ui.Group;
        AccessibleName = "标注颜色 " + label;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        if (Selected) {
            using (Pen pen = new Pen(Color.DeepSkyBlue, 2)) g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
        } else if (hover) {
            using (Pen pen = new Pen(Color.FromArgb(170, 184, 204))) g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
        } else {
            using (Pen pen = new Pen(Color.FromArgb(90, 102, 120))) g.DrawRectangle(pen, 3, 3, Width - 7, Height - 7);
        }
        int inset = Selected ? 6 : 5;
        using (SolidBrush brush = new SolidBrush(Swatch)) g.FillRectangle(brush, inset, inset, Width - inset * 2, Height - inset * 2);
        if (!Selected) return;
        float luminance = (0.299f * Swatch.R + 0.587f * Swatch.G + 0.114f * Swatch.B) / 255f;
        Color mark = luminance > 0.62f ? Color.Black : Color.White;
        using (Pen pen = new Pen(mark, 2.2f)) {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            int cx = Width / 2;
            int cy = Height / 2 + 1;
            g.DrawLines(pen, new Point[] { new Point(cx - 5, cy), new Point(cx - 1, cy + 4), new Point(cx + 6, cy - 4) });
        }
    }
}

class WidthChip : Control {
    public int WidthPx;
    public bool Selected;
    bool hover;
    public WidthChip(int widthPx) {
        WidthPx = widthPx;
        Size = new Size(56, 30);
        Margin = new Padding(2);
        Cursor = Cursors.Hand;
        BackColor = Ui.Group;
        AccessibleName = "线宽 " + widthPx + " 像素";
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Selected ? Ui.Active : (hover ? Ui.Hover : BackColor));
        if (Selected) {
            using (Pen border = new Pen(Color.DeepSkyBlue, 2)) g.DrawRectangle(border, 1, 1, Width - 3, Height - 3);
        }
        using (Pen pen = new Pen(Color.White, WidthPx)) {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            int y = Height / 2;
            g.DrawLine(pen, 10, y, Width - 10, y);
        }
    }
}

class TextSizeChip : Control {
    public int PointSize;
    public bool Selected;
    bool hover;
    public TextSizeChip(int pointSize) {
        PointSize = pointSize;
        Size = new Size(40, 30);
        Margin = new Padding(2);
        Cursor = Cursors.Hand;
        BackColor = Ui.Group;
        AccessibleName = "字号 " + pointSize;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        g.Clear(Selected ? Ui.Active : (hover ? Ui.Hover : BackColor));
        if (Selected) {
            using (Pen border = new Pen(Color.DeepSkyBlue, 2)) g.DrawRectangle(border, 1, 1, Width - 3, Height - 3);
        }
        float sample = PointSize <= 14 ? 8f : (PointSize <= 22 ? 12f : 16f);
        using (Font font = new Font("Microsoft YaHei UI", sample, FontStyle.Regular, GraphicsUnit.Point))
            TextRenderer.DrawText(g, "文", font, ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

class MoreColorButton : Control {
    public Color Current = Color.Red;
    public bool Marked;
    bool hover;
    public MoreColorButton() {
        Size = new Size(104, 30);
        Margin = new Padding(4, 2, 2, 2);
        Cursor = Cursors.Hand;
        BackColor = Ui.Group;
        ForeColor = Color.White;
        AccessibleName = "更多颜色";
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(hover && !Marked ? Ui.Hover : BackColor);
        if (Marked) {
            using (Pen pen = new Pen(Color.DeepSkyBlue, 2)) g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
        }
        int box = 14;
        int bx = Width - box - 8;
        int by = (Height - box) / 2;
        using (SolidBrush b = new SolidBrush(Current)) g.FillRectangle(b, bx, by, box, box);
        using (Pen pen = new Pen(Marked ? Color.White : Color.FromArgb(140, 152, 170))) g.DrawRectangle(pen, bx, by, box - 1, box - 1);
        TextRenderer.DrawText(g, "更多颜色", Font, new Rectangle(8, 0, bx - 10, Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}
