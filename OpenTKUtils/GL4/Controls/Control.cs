﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTKUtils.GL4.Controls
{
    public struct Padding
    {
        public int Left; public int Top; public int Right; public int Bottom;
        public Padding(int left, int top, int right, int bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
        public Padding(int pad = 0) { Left = pad; Top = pad; Right = pad; Bottom = pad; }
        public int TotalWidth { get { return Left + Right; } }
        public int TotalHeight { get { return Top + Bottom; } }
    };

    public struct Margin
    {
        public int Left; public int Top; public int Right; public int Bottom;
        public Margin(int left, int top, int right, int bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
        public Margin(int pad = 0) { Left = pad; Top = pad; Right = pad; Bottom = pad; }
        public int TotalWidth { get { return Left + Right; } }
        public int TotalHeight { get { return Top + Bottom; } }
    };

    public struct MouseEventArgs
    {
        public MouseEventArgs(Point l) { Button = 0;Location = l;Clicks = 0;  }
        public MouseEventArgs(int b, Point l, int c) { Button = b; Location = l; Clicks = c; }

        public int Button { get; set; }
        public Point Location { get; set; }
        public int Clicks { get; set; }
    }

    public abstract class GLBaseControl
    {
        // co-ords are in parent control positions
        
        public int Left { get { return window.Left; } set { SetPos(value, window.Top, window.Width, window.Height); } }
        public int Right { get { return window.Right; } set { SetPos(window.Left, window.Top, value - window.Left, window.Height); } }
        public int Top { get { return window.Top; } set { SetPos(window.Left, value, window.Width, window.Height); } }
        public int Bottom { get { return window.Bottom; } set { SetPos(window.Left, window.Top, window.Width, value - window.Top); } }
        public int Width { get { return window.Width; } set { SetPos(window.Left, window.Top, value, window.Height); } }
        public int Height { get { return window.Height; } set { SetPos(window.Left, window.Top, window.Width, value); } }
        public Rectangle Position { get { return window; } set { SetPos(value.Left, value.Top, value.Width, value.Height); } }
        public Point Location { get { return new Point(window.Left, window.Top); } set { SetPos(value.X, value.Y, window.Width, window.Height); } }
        public Size Size { get { return new Size(window.Width, window.Height); } set { SetPos(window.Left, window.Top, value.Width, value.Height); } }
        public Rectangle ClientRectangle { get { return window; } }

        public enum DockingType { None, Left, Right, Top, Bottom, Fill };
        public DockingType Dock { get { return docktype; } set { docktype = value; } } // parent?.PerformLayout(); } }
        public float DockPercent { get; set; } = 0.0f;        // % in 0-1 terms used to dock on left,top,right,bottom.  0 means just use width/height

        public GLBaseControl Parent { get { return parent; } }
        public Font Font { get { return fnt ?? parent?.fnt; } set { fnt = value; } }

        public string Name { get; set; } = "?";

        public Color BackColor { get; set; } = Color.Transparent;
        public Color BorderColor { get; set; } = Color.Transparent;
        public int BorderWidth { get; set; } = 1;
        public GL4.Controls.Padding Padding { get; set; }
        public GL4.Controls.Margin Margin { get; set; }

        public bool InvalidateOnEnterLeave { get; set; } = false;       // if set, invalidate on enter/leave to force a redraw

        public void Invalidate() { NeedRedraw = true; FindForm().RequestRender = true;}     // we need redraw, and form needs rerendering

        public GLForm FindForm() { return this is GLForm ? this as GLForm: parent.FindForm(); }

        public bool Hover { get; set; } = false;            // mouse is over control
        public bool MouseButtonDown { get; set; } = false;        // mouse is down over control

        Action<MouseEventArgs> MouseDown { get; set; } = null;
        Action<MouseEventArgs> MouseUp { get; set; } = null;
        Action<MouseEventArgs> MouseMove { get; set; } = null;
        Action<MouseEventArgs> MouseEnter { get; set; } = null;
        Action<MouseEventArgs> MouseLeave { get; set; } = null;
        Action<MouseEventArgs> MouseClick { get; set; } = null;

        public Bitmap GetBitmap() { return levelbmp ?? parent.GetBitmap(); }

        public void Add(GLBaseControl other)
        {
            other.parent = this;
            children.Add(other);

            if (this is GLForm) // if adding to a form, the child must have a bitmap
                other.levelbmp = new Bitmap(other.Width, other.Height);

            SetRedraw();
        }

        public void Remove(GLBaseControl other)
        {
            if (other.levelbmp != null)
                other.levelbmp.Dispose();

            children.Remove(other);

            SetRedraw();
        }

        public Point FormCoords()       // co-ordinates in the Form, not the screen
        {
            Point p = Location;
            GLBaseControl b = this;
            while (b.Parent != null)
            {
                p = new Point(p.X + b.parent.Left, p.Y + b.parent.Top);
                b = b.parent;
            }

            return p;
        }

        public GLBaseControl FindControlOver(Point p)       // p = form co-ords
        {
            if (p.X < Left || p.X > Right || p.Y < Top || p.Y > Bottom)
                return null;

            foreach (GLBaseControl c in children)
            {
                var r = c.FindControlOver(new Point(p.X - Left, p.Y - Top));   // find, converting co-ords into child co-ords
                if (r != null)
                    return r;
            }

            return this;
        }

        // imp

        ////////////// imp

        protected bool NeedRedraw { get; private set; } = true;

        protected void SetPos(int left, int top, int width, int height)
        {
            window = new Rectangle(left, top, width, height);
            parent?.PerformLayout();        // go up one and perform layout on all its children, since we are part of it.
        }

        protected Bitmap levelbmp;       // set if the level has a new bitmap.  Controls under Form always does. Other ones may if they scroll
        protected Rectangle window;       // total area owned, in parent co-ords
        private DockingType docktype { get; set; }  = DockingType.None;
        private GLBaseControl parent { get; set; } = null;       // its parent, null if top of top
        protected List<GLBaseControl> children;   // its children
        private Font fnt;

        public GLBaseControl(GLBaseControl p = null)
        {
            parent = p;
            children = new List<GLBaseControl>();
            window = new Rectangle(0, 0, 100, 100);
        }

        public virtual void PerformLayout()
        {
            Rectangle area = AdjustByPaddingBorderMargin(new Rectangle(0, 0, Width, Height));

            foreach (var c in children)
            {
                area = c.Layout(area);
                c.PerformLayout();
            }
        }

        private Rectangle AdjustByPaddingBorderMargin(Rectangle area)
        {
            int bs = BorderColor != Color.Transparent ? BorderWidth : 0;
            return new Rectangle(area.Left + Margin.Left + Padding.Left + bs,
                                    area.Top + Margin.Top + Padding.Top + bs,
                                    area.Width - Margin.TotalWidth - Padding.TotalWidth - bs * 2,
                                    area.Height - Margin.TotalHeight - Padding.TotalHeight - bs * 2);
        }

        protected virtual Rectangle Layout(Rectangle area)
        {
            int ws = DockPercent>0 ? ((int)(area.Width * DockPercent)) : window.Width;
            ws = Math.Min(ws, area.Width);
            int hs = DockPercent>0 ? ((int)(area.Height * DockPercent)) : window.Height;
            hs = Math.Min(hs, area.Height);

            Rectangle oldwindow = window;
            Rectangle areaout = area;

            if ( docktype == DockingType.Fill )
            {
                window = area;
                areaout = new Rectangle(0, 0, 0, 0);
            }
            else if (docktype == DockingType.Left)
            {
                window = new Rectangle(area.Left, area.Top, ws, area.Height);
                areaout = new Rectangle(area.Left+ws, area.Top, area.Width - ws, area.Height);
            }
            else if (docktype == DockingType.Right)
            {
                window = new Rectangle(area.Right-ws, area.Top, ws, area.Height);
                areaout = new Rectangle(window.Left, area.Top, area.Width - window.Width, area.Height);
            }
            else if (docktype == DockingType.Top)
            {
                window = new Rectangle(area.Left, area.Top, area.Width, hs);
                areaout =  new Rectangle(window.Left, area.Top + hs, area.Width, area.Height - hs);
            }
            else if (docktype == DockingType.Bottom)
            {
                window = new Rectangle(area.Left, area.Bottom-hs, area.Width, hs);
                areaout =  new Rectangle(window.Left, area.Top, area.Width, area.Height - hs);
            }

            System.Diagnostics.Debug.WriteLine("{0} in {1} out {2} dock {3} win {4}", Name, area, areaout , Dock, window);

            if ( oldwindow != window )
            {
                SetRedraw();            // set redraw on us and all parents

                if ( levelbmp != null && oldwindow.Size != window.Size) // if window size changed
                {
                    levelbmp.Dispose();
                    levelbmp = new Bitmap(Width, Height);       // occurs for controls directly under form
                }
            }

            return areaout;
        }

        public void SetRedraw()                                 // cascade redraw command right up to top to the form itself
        {
            NeedRedraw = true;
            parent?.SetRedraw();
        }

        public virtual bool Redraw(Bitmap usebmp, Rectangle area, Graphics gr, bool forceredraw )
        {
            if (levelbmp != null)                               // bitmap on this level, use it for the children
            {
                usebmp = levelbmp;
                area = new Rectangle(0, 0, Width, Height);      // restate area in terms of bitmap
                gr = Graphics.FromImage(usebmp);        // get graphics for it
                gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            }

            bool redrawn = false;

            if (NeedRedraw || forceredraw)          // if we need a redraw, or we are forced to draw by a parent redrawing above us.
            {
                System.Diagnostics.Debug.WriteLine("Redraw {0} to {1}", Name, area);

                if (BackColor != Color.Transparent)
                {
                    using (Brush b = new SolidBrush(BackColor))
                        gr.FillRectangle(b, area);
                }

                if (BorderColor != Color.Transparent)
                {
                    Rectangle rectarea = new Rectangle(area.Left + Margin.Left,
                                                    area.Top + Margin.Top,
                                                    area.Width - Margin.TotalWidth - 1,
                                                    area.Height - Margin.TotalHeight - 1);

                    using (var p = new Pen(BorderColor, BorderWidth))
                    {
                        gr.DrawRectangle(p, rectarea);
                    }
                }

                foreach (var c in children)
                {
                    Rectangle controlarea = new Rectangle(area.Left + c.Left,
                                                            area.Top + c.Top,
                                                            c.Width, c.Height);
                    // child, redraw using this bitmap, in this area of the bitmap
                    c.Redraw(usebmp, controlarea, gr, true);
                }

                Paint(usebmp, area, gr);

                NeedRedraw = false;
                redrawn = true;
            }
            else
            {                                                           // we don't require a redraw, but the children might
                foreach (var c in children)
                {
                    Rectangle controlarea = new Rectangle(area.Left + c.Left,
                                                            area.Top + c.Top,
                                                            c.Width, c.Height);
                    // child, redraw using this bitmap, in this area of the bitmap
                    redrawn |= c.Redraw(usebmp, controlarea, gr, false);
                }
            }

            if (levelbmp != null)                               // bitmap on this level, we made a GR, dispose
                gr.Dispose();

            return redrawn;
        }


        // overrides

        public virtual void Paint(Bitmap bmp, Rectangle area, Graphics gr)
        {
            System.Diagnostics.Debug.WriteLine("Paint {0} to {1}", Name, area);
        }

        public virtual void OnMouseLeave(MouseEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("leave " + Name + " " + e.Location);
            MouseLeave?.Invoke(e);

            if (InvalidateOnEnterLeave)
                Invalidate();
        }
        public virtual void OnMouseEnter(MouseEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("enter " + Name + " " + e.Location);
            MouseEnter?.Invoke(e);

            if (InvalidateOnEnterLeave)
                Invalidate();
        }

        public virtual void OnMouseUp(MouseEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("up   " + Name + " " + e.Location + " " + e.Button);
            MouseUp?.Invoke(e);
        }
        public virtual void OnMouseDown(MouseEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("down " + Name + " " + e.Location +" " + e.Button);
            MouseDown?.Invoke(e);
        }

        public virtual void OnMouseClick(MouseEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("click " + Name + " " + e.Button + " " + e.Clicks + " " + e.Location );
            MouseClick?.Invoke(e);
        }

        public virtual void OnMouseMove(MouseEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Over " + Name + " " + e.Location);
            MouseMove?.Invoke(e);
        }
    }
}