﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTKUtils.GL4.Controls
{
    public abstract class TabStyleCustom
    {
        public enum TabAlignment
        {
            Top = 0,
            Bottom = 1
        };

        public abstract void DrawTab(Graphics gr, Rectangle borderrect, bool selected, Color color1, Color color2, Color coloroutline, TabAlignment alignment);

        public virtual void DrawText(Graphics gr, Rectangle borderrect, bool selected, Color color, string text, Font ft, Image icon)        // provide a standard version..
        {
            bool textpresent = text.Length > 0;

            if (icon != null)
            {
                int off = 8;
                Point pos = new Point(borderrect.X + off, borderrect.Y + borderrect.Height / 2 - icon.Height / 2);

                if (!textpresent)
                    pos = new Point(borderrect.X + borderrect.Width / 2 - icon.Width / 2, borderrect.Y + borderrect.Height / 2 - icon.Height / 2);

                gr.SmoothingMode = SmoothingMode.Default;
                gr.DrawImage(icon, pos);
                borderrect.X += icon.Width + off / 2;
                borderrect.Width -= icon.Width + off / 2;
            }

            if (textpresent)
            {
                gr.SmoothingMode = SmoothingMode.AntiAlias;

                using (StringFormat f = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (Brush textb = new SolidBrush(color))
                    gr.DrawString(text, ft, textb, borderrect, f);
            }
        }
    }

    public class TabStyleSquare : TabStyleCustom
    {
        public override void DrawTab(Graphics gr, Rectangle borderrect, bool selected, Color color1, Color color2, Color coloroutline, TabAlignment alignment)
        {
            System.Diagnostics.Debug.Assert(alignment == TabAlignment.Top);

            if (!selected)
            {
                borderrect.Height -= 2;                     // shorter by this.
                borderrect.Y += 2;
            }

            int xfar = borderrect.Right - 1;
            int yfar = borderrect.Bottom - 1;

            GraphicsPath fill = new GraphicsPath();
            fill.AddLine(borderrect.X, yfar, borderrect.X, borderrect.Y);
            fill.AddLine(borderrect.X, borderrect.Y, xfar, borderrect.Y);
            fill.AddLine(xfar, borderrect.Y, xfar, yfar);
            GraphicsPath border = new GraphicsPath();
            border.AddLine(borderrect.X, yfar+1, borderrect.X, borderrect.Y);
            border.AddLine(borderrect.X, borderrect.Y, xfar, borderrect.Y);
            border.AddLine(xfar, borderrect.Y, xfar, yfar+1);

            gr.SmoothingMode = SmoothingMode.Default;
            using (Brush b = new System.Drawing.Drawing2D.LinearGradientBrush(borderrect, color1, color2, 90))
                gr.FillPath(b, fill);
            using (Pen p = new Pen(coloroutline, 1.0F))
                gr.DrawPath(p, border);
        }
    }

    public class TabStyleRoundedEdge : TabStyleCustom
    {
        public override void DrawTab(Graphics gr, Rectangle borderrect, bool selected, Color color1, Color color2, Color coloroutline, TabAlignment alignment)
        {
            System.Diagnostics.Debug.Assert(alignment == TabAlignment.Top);

            if (!selected)
            {
                borderrect.Height -= 2;                     // shorter by this.
                borderrect.Y += 2;
            }

            int radius = 10;
            int xfar = borderrect.Right - 1;
            int yfar = borderrect.Bottom - 1;

            GraphicsPath border = new GraphicsPath();
            border.AddLine(borderrect.X, yfar, borderrect.X, borderrect.Y);
            border.AddArc(xfar - radius * 2, borderrect.Y, radius * 2, radius * 2, 270, 90);
            border.AddLine(xfar, yfar, xfar, yfar);

            GraphicsPath fill = new GraphicsPath();
            fill.AddLine(borderrect.X + 1, yfar+1, borderrect.X + 1, borderrect.Y);
            fill.AddArc(xfar - radius * 2, borderrect.Y, radius * 2, radius * 2, 270, 90);
            fill.AddLine(xfar, borderrect.Y-radius*2, xfar, yfar+1 );

            gr.SmoothingMode = SmoothingMode.Default;

            using (Brush b = new System.Drawing.Drawing2D.LinearGradientBrush(borderrect, color1, color2, 90))
                gr.FillPath(b, fill);

            gr.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen p = new Pen(coloroutline, 1.0F))
                gr.DrawPath(p, border);
        }
    }

    public class TabStyleAngled : TabStyleCustom
    {
        private const int shift = 6;

        public override void DrawTab(Graphics gr, Rectangle borderrect, bool selected, Color color1, Color color2, Color coloroutline, TabAlignment alignment)
        {
            GraphicsPath border = new GraphicsPath();
            GraphicsPath fill = new GraphicsPath();

            int xfar = borderrect.Right - 1;

            int ybot = (alignment == TabAlignment.Bottom) ? (borderrect.Y) : (borderrect.Bottom - 1);
            int ytop = (alignment == TabAlignment.Bottom) ? (borderrect.Bottom - 1 - ((selected) ? 0 : 2)) : (borderrect.Y + ((selected) ? 0 : 2));

            border.AddLine(borderrect.X, ybot, borderrect.X + shift, ytop);
            border.AddLine(borderrect.X + shift, ytop, xfar, ytop);
            border.AddLine(xfar, ytop, xfar + shift, ybot);

            fill.AddLine(borderrect.X, ybot+1, borderrect.X + shift, ytop);
            fill.AddLine(borderrect.X + shift, ytop, xfar, ytop);
            fill.AddLine(xfar, ytop, xfar + shift, ybot+1 );

            gr.SmoothingMode = SmoothingMode.Default;

            using (Brush b = new System.Drawing.Drawing2D.LinearGradientBrush(borderrect, color1, color2, 90))
                gr.FillPath(b, fill);

            gr.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen p = new Pen(coloroutline, 1.0F))
                gr.DrawPath(p, border);
        }

        public override void DrawText(Graphics gr, Rectangle borderrect, bool selected, Color color, string text, Font ft, Image icon)        // provide a standard version..
        {
            borderrect.Width += shift;
            base.DrawText(gr, borderrect, selected, color, text, ft, icon);
        }
    }
}