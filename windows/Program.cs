using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MatrixScreensaver
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string arg = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "/c";
            if (arg.StartsWith("/c") || arg.StartsWith("-c"))
            {
                Application.Run(new SettingsForm());
                return;
            }

            if (arg.StartsWith("/p") || arg.StartsWith("-p"))
            {
                IntPtr parent = IntPtr.Zero;
                string raw = null;
                if (args.Length > 1) raw = args[1];
                else
                {
                    int colon = arg.IndexOf(':');
                    if (colon >= 0 && colon + 1 < arg.Length) raw = arg.Substring(colon + 1);
                }

                long handleValue;
                if (raw != null && long.TryParse(raw, out handleValue)) parent = new IntPtr(handleValue);
                if (parent != IntPtr.Zero) Application.Run(new MatrixForm(Rectangle.Empty, true, parent));
                return;
            }

            Rectangle bounds = Screen.PrimaryScreen != null ? Screen.PrimaryScreen.Bounds : new Rectangle(0, 0, 1920, 1080);
            Application.Run(new MatrixForm(bounds, false, IntPtr.Zero));
        }
    }

    internal sealed class MatrixSettings
    {
        private const string KeyPath = @"Software\MatrixScreensaver";
        public int Speed = 5;
        public int Density = 80;
        public int FontSize = 22;
        public int Fps = 30;
        public int Trail = 16;
        public bool Glow = true;
        public string Mode = "Matrix";

        public static MatrixSettings Load()
        {
            var s = new MatrixSettings();
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (key == null) return s;
                    s.Speed = ReadInt(key, "Speed", s.Speed, 1, 10);
                    s.Density = ReadInt(key, "Density", s.Density, 35, 140);
                    s.FontSize = ReadInt(key, "FontSize", s.FontSize, 12, 40);
                    s.Fps = ReadInt(key, "Fps", s.Fps, 20, 60);
                    s.Trail = ReadInt(key, "Trail", s.Trail, 6, 30);
                    s.Glow = ReadInt(key, "Glow", s.Glow ? 1 : 0, 0, 1) == 1;
                    var m = key.GetValue("Mode") as string;
                    if (!string.IsNullOrWhiteSpace(m)) s.Mode = m;
                }
            }
            catch { }
            return s;
        }

        private static int ReadInt(RegistryKey key, string name, int fallback, int min, int max)
        {
            try
            {
                object value = key.GetValue(name, fallback);
                int result = Convert.ToInt32(value);
                return Math.Max(min, Math.Min(max, result));
            }
            catch { return fallback; }
        }

        public void Save()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (key == null) return;
                    key.SetValue("Speed", Speed, RegistryValueKind.DWord);
                    key.SetValue("Density", Density, RegistryValueKind.DWord);
                    key.SetValue("FontSize", FontSize, RegistryValueKind.DWord);
                    key.SetValue("Fps", Fps, RegistryValueKind.DWord);
                    key.SetValue("Trail", Trail, RegistryValueKind.DWord);
                    key.SetValue("Glow", Glow ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("Mode", Mode, RegistryValueKind.String);
                }
            }
            catch { }
        }
    }

    internal sealed class RainStream
    {
        public float X;
        public float Y;
        public float SpeedFactor;
        public int Trail;
        public int Seed;
    }

    internal sealed class MatrixForm : Form
    {
        private readonly bool preview;
        private readonly IntPtr previewParent;
        private readonly Rectangle requestedBounds;
        private readonly Random random = new Random();
        private readonly List<RainStream> streams = new List<RainStream>();
        private readonly MatrixSettings settings;
        private readonly Timer timer = new Timer();
        private Bitmap canvas;
        private Graphics gfx;
        private Font font;
        private Point initialMouse;
        private bool mouseInitialized;

        private const string MatrixChars = "ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string MixedChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ#$%&*+-<>[]{}";

        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        public MatrixForm(Rectangle bounds, bool isPreview, IntPtr parent)
        {
            requestedBounds = bounds;
            preview = isPreview;
            previewParent = parent;
            settings = MatrixSettings.Load();

            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
            StartPosition = FormStartPosition.Manual;

            Load += OnLoaded;
            FormClosed += OnClosed;
            Paint += OnPaintFrame;
            KeyDown += delegate { if (!preview) Close(); };
            MouseDown += delegate { if (!preview) Close(); };
            MouseMove += OnMouseMove;

            timer.Tick += TickFrame;
        }

        private void OnLoaded(object sender, EventArgs e)
        {
            if (preview)
            {
                SetParent(Handle, previewParent);
                RECT rc;
                if (GetClientRect(previewParent, out rc)) Bounds = new Rectangle(0, 0, Math.Max(1, rc.Right - rc.Left), Math.Max(1, rc.Bottom - rc.Top));
            }
            else
            {
                Bounds = requestedBounds;
                TopMost = true;
                Cursor.Hide();
            }

            BuildCanvas();
            timer.Interval = Math.Max(15, 1000 / Math.Max(20, settings.Fps));
            timer.Start();
        }

        private void OnClosed(object sender, FormClosedEventArgs e)
        {
            timer.Stop();
            if (!preview) Cursor.Show();
            if (gfx != null) gfx.Dispose();
            if (canvas != null) canvas.Dispose();
            if (font != null) font.Dispose();
        }

        private void BuildCanvas()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            if (gfx != null) gfx.Dispose();
            if (canvas != null) canvas.Dispose();
            if (font != null) font.Dispose();

            canvas = new Bitmap(ClientSize.Width, ClientSize.Height);
            gfx = Graphics.FromImage(canvas);
            gfx.Clear(Color.Black);
            gfx.SmoothingMode = SmoothingMode.HighSpeed;
            gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            font = new Font("Consolas", Math.Max(8, settings.FontSize), FontStyle.Bold, GraphicsUnit.Pixel);

            streams.Clear();
            float cell = Math.Max(8f, settings.FontSize * (100f / Math.Max(35, settings.Density)));
            int count = Math.Max(8, (int)(ClientSize.Width / cell));
            for (int i = 0; i < count; i++)
            {
                streams.Add(new RainStream
                {
                    X = i * cell + (float)(random.NextDouble() * Math.Max(1f, cell * 0.35f)),
                    Y = -random.Next(0, Math.Max(2, ClientSize.Height / Math.Max(10, settings.FontSize))),
                    SpeedFactor = 0.65f + (float)random.NextDouble() * 1.25f,
                    Trail = Math.Max(4, settings.Trail + random.Next(-4, 5)),
                    Seed = random.Next()
                });
            }
        }

        private void TickFrame(object sender, EventArgs e)
        {
            if (gfx == null || canvas == null) return;

            using (var fade = new SolidBrush(Color.FromArgb(settings.Fps >= 45 ? 42 : 55, 0, 0, 0)))
                gfx.FillRectangle(fade, 0, 0, canvas.Width, canvas.Height);

            float line = Math.Max(10f, settings.FontSize * 0.95f);
            float baseSpeed = 0.22f + settings.Speed * 0.12f;
            int rows = Math.Max(1, (int)(canvas.Height / line));

            foreach (var s in streams)
            {
                for (int t = s.Trail; t >= 0; t--)
                {
                    float row = s.Y - t;
                    float y = row * line;
                    if (y < -line || y > canvas.Height + line) continue;

                    int alpha;
                    Color color;
                    if (t == 0)
                    {
                        alpha = 255;
                        color = Color.FromArgb(alpha, 220, 255, 220);
                    }
                    else
                    {
                        float intensity = 1f - (t / (float)(s.Trail + 1));
                        alpha = Math.Max(20, (int)(210 * intensity * intensity));
                        color = Color.FromArgb(alpha, 20, 255, 70);
                    }

                    char ch = PickChar(s.Seed + ((int)row * 37) + (Environment.TickCount / 90) + t * 11);
                    string text = ch.ToString();

                    if (settings.Glow && t < 3)
                    {
                        using (var glow = new SolidBrush(Color.FromArgb(t == 0 ? 80 : 35, 0, 255, 70)))
                        {
                            gfx.DrawString(text, font, glow, s.X - 1, y);
                            gfx.DrawString(text, font, glow, s.X + 1, y);
                        }
                    }

                    using (var brush = new SolidBrush(color))
                        gfx.DrawString(text, font, brush, s.X, y);
                }

                s.Y += baseSpeed * s.SpeedFactor;
                if (s.Y - s.Trail > rows + 3)
                {
                    s.Y = -random.Next(2, Math.Max(4, rows / 2));
                    s.SpeedFactor = 0.65f + (float)random.NextDouble() * 1.25f;
                    s.Trail = Math.Max(4, settings.Trail + random.Next(-4, 5));
                    s.Seed = random.Next();
                }
            }

            Invalidate();
        }

        private char PickChar(int seed)
        {
            string chars;
            switch ((settings.Mode ?? "Matrix").ToLowerInvariant())
            {
                case "numeri": chars = "0123456789"; break;
                case "binario": chars = "01"; break;
                case "esadecimale": chars = "0123456789ABCDEF"; break;
                case "mista": chars = MixedChars; break;
                default: chars = MatrixChars; break;
            }
            int idx = seed & 0x7fffffff;
            return chars[idx % chars.Length];
        }

        private void OnPaintFrame(object sender, PaintEventArgs e)
        {
            if (canvas != null) e.Graphics.DrawImageUnscaled(canvas, 0, 0);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated && ClientSize.Width > 0 && ClientSize.Height > 0 && Visible) BuildCanvas();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (preview) return;
            if (!mouseInitialized)
            {
                initialMouse = Cursor.Position;
                mouseInitialized = true;
                return;
            }
            Point p = Cursor.Position;
            if (Math.Abs(p.X - initialMouse.X) > 8 || Math.Abs(p.Y - initialMouse.Y) > 8) Close();
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly NumericUpDown speed = MakeNumber(1, 10);
        private readonly NumericUpDown density = MakeNumber(35, 140);
        private readonly NumericUpDown fontSize = MakeNumber(12, 40);
        private readonly NumericUpDown trail = MakeNumber(6, 30);
        private readonly ComboBox fps = new ComboBox();
        private readonly ComboBox mode = new ComboBox();
        private readonly CheckBox glow = new CheckBox();
        private readonly MatrixSettings current = MatrixSettings.Load();

        public SettingsForm()
        {
            Text = "Matrix Screensaver - Impostazioni";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(470, 430);
            BackColor = Color.FromArgb(10, 14, 10);
            ForeColor = Color.FromArgb(120, 255, 150);
            Font = new Font("Segoe UI", 10f);

            var title = new Label { Text = "MATRIX SCREENSAVER", AutoSize = true, Font = new Font("Segoe UI", 18f, FontStyle.Bold), Left = 22, Top = 18 };
            Controls.Add(title);

            int y = 75;
            AddRow("Velocità", speed, ref y);
            AddRow("Densità", density, ref y);
            AddRow("Dimensione caratteri", fontSize, ref y);
            AddRow("Lunghezza scia", trail, ref y);

            fps.DropDownStyle = ComboBoxStyle.DropDownList;
            fps.Items.AddRange(new object[] { "20", "30", "45", "60" });
            AddRow("FPS", fps, ref y);

            mode.DropDownStyle = ComboBoxStyle.DropDownList;
            mode.Items.AddRange(new object[] { "Matrix", "Numeri", "Binario", "Esadecimale", "Mista" });
            AddRow("Caratteri", mode, ref y);

            glow.Text = "Glow luminoso";
            glow.AutoSize = true;
            glow.Left = 205;
            glow.Top = y + 4;
            Controls.Add(glow);
            y += 42;

            var eco = MakeButton("Eco", 22, y, delegate { ApplyPreset(3, 55, 20, 20, 12, false); });
            var balanced = MakeButton("Bilanciato", 132, y, delegate { ApplyPreset(5, 80, 22, 30, 16, true); });
            var max = MakeButton("Massimo", 272, y, delegate { ApplyPreset(8, 120, 26, 60, 22, true); });
            Controls.AddRange(new Control[] { eco, balanced, max });
            y += 55;

            var save = MakeButton("Salva", 250, y, delegate { SaveSettings(); });
            var cancel = MakeButton("Annulla", 350, y, delegate { Close(); });
            Controls.AddRange(new Control[] { save, cancel });

            speed.Value = current.Speed;
            density.Value = current.Density;
            fontSize.Value = current.FontSize;
            trail.Value = current.Trail;
            fps.SelectedItem = current.Fps.ToString();
            if (fps.SelectedIndex < 0) fps.SelectedItem = "30";
            mode.SelectedItem = current.Mode;
            if (mode.SelectedIndex < 0) mode.SelectedItem = "Matrix";
            glow.Checked = current.Glow;
        }

        private static NumericUpDown MakeNumber(int min, int max)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Width = 125, BackColor = Color.FromArgb(20, 28, 20), ForeColor = Color.FromArgb(150, 255, 170) };
        }

        private Button MakeButton(string text, int x, int y, EventHandler click)
        {
            var b = new Button { Text = text, Left = x, Top = y, Width = text == "Bilanciato" ? 125 : 95, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(18, 38, 20), ForeColor = Color.FromArgb(140, 255, 160) };
            b.Click += click;
            return b;
        }

        private void AddRow(string label, Control control, ref int y)
        {
            var l = new Label { Text = label, AutoSize = true, Left = 22, Top = y + 5 };
            control.Left = 205;
            control.Top = y;
            Controls.Add(l);
            Controls.Add(control);
            y += 40;
        }

        private void ApplyPreset(int s, int d, int size, int frameRate, int trailLength, bool useGlow)
        {
            speed.Value = s;
            density.Value = d;
            fontSize.Value = size;
            trail.Value = trailLength;
            fps.SelectedItem = frameRate.ToString();
            glow.Checked = useGlow;
            mode.SelectedItem = "Matrix";
        }

        private void SaveSettings()
        {
            current.Speed = (int)speed.Value;
            current.Density = (int)density.Value;
            current.FontSize = (int)fontSize.Value;
            current.Trail = (int)trail.Value;
            current.Fps = int.Parse((string)fps.SelectedItem);
            current.Mode = (string)mode.SelectedItem;
            current.Glow = glow.Checked;
            current.Save();
            MessageBox.Show("Impostazioni salvate.", "Matrix Screensaver", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
    }
}