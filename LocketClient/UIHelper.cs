using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocketClient
{
    public class RoundedButton : Button
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int BorderRadius { get; set; } = 20;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color BorderColor { get; set; } = Color.PaleVioletRed;

        public RoundedButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.BackColor = Color.Gold;
            this.ForeColor = Color.Black;
            this.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            this.Cursor = Cursors.Hand;
            this.Size = new Size(150, 40);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(0, 0, BorderRadius, BorderRadius, 180, 90);
            path.AddArc(Width - BorderRadius, 0, BorderRadius, BorderRadius, 270, 90);
            path.AddArc(Width - BorderRadius, Height - BorderRadius, BorderRadius, BorderRadius, 0, 90);
            path.AddArc(0, Height - BorderRadius, BorderRadius, BorderRadius, 90, 90);
            path.CloseAllFigures();

            this.Region = new Region(path);
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            base.OnPaint(pevent);

            using (Pen pen = new Pen(BorderColor, 2f))
            {
                pevent.Graphics.DrawPath(pen, path);
            }
        }
    }

    public static class UIStyle
    {
        public static Color DarkBack = Color.FromArgb(24, 24, 24);
        public static Color LightText = Color.White;

        public static void ApplyDarkMode(Form form)
        {
            form.BackColor = DarkBack;
            form.ForeColor = LightText;

            foreach (Control c in form.Controls)
            {
                ApplyStyle(c);
            }
        }

        private static void ApplyStyle(Control c)
        {
            if (c is TextBox tb)
            {
                tb.BackColor = Color.FromArgb(40, 40, 40);
                tb.ForeColor = Color.White;
                tb.BorderStyle = BorderStyle.FixedSingle; 
            }
            else if (c is ListBox lb) 
            {
                lb.BackColor = Color.FromArgb(30, 30, 30);
                lb.ForeColor = Color.White;
                lb.BorderStyle = BorderStyle.None;
            }
            else if (c is Label)
            {
                c.ForeColor = LightText;
            }

            // Đệ quy
            if (c.HasChildren)
            {
                foreach (Control child in c.Controls) ApplyStyle(child);
            }
        }
    }
}