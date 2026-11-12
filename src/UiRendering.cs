using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MiyouDaily
{
    public static class UiRendering
    {
        static readonly Dictionary<string,Bitmap> textCache=new Dictionary<string,Bitmap>();
        static readonly Queue<string> textOrder=new Queue<string>();
        static long textBytes;
        const long TextBudget=16*1024*1024;
        public static void Quality(Graphics g)
        {g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;g.InterpolationMode=InterpolationMode.HighQualityBicubic;}
        internal static float PixelDpi(Graphics g){return PopupSurface.RenderingDpi>0?PopupSurface.RenderingDpi:g.DpiX;}
        public static void Text(Graphics g,string text,Font font,Rectangle bounds,Color color,bool centered,bool wrap=false,bool top=false)
        {
            if(string.IsNullOrEmpty(text)||bounds.Width<=0||bounds.Height<=0)return;
            // Rasterize glyphs at 3x the target monitor's pixel density, then
            // filter only the glyph coverage. The window itself is never scaled.
            // Cache the final native-size image; resize/DPI/font changes have
            // distinct keys, and old frames are bounded by a fixed memory budget.
            // WM_PRINT uses a memory DC whose DPI can differ from the window's
            // monitor. Use the frame's target DPI for both rasterization/cache.
            float dpiX=PixelDpi(g),dpiY=PopupSurface.RenderingDpi>0?PopupSurface.RenderingDpi:g.DpiY;
            string key=string.Join("|",new[]{text,font.Name,font.SizeInPoints.ToString("R"),((int)font.Style).ToString(),dpiX.ToString("R"),dpiY.ToString("R"),bounds.Size.ToString(),color.ToArgb().ToString(),centered.ToString(),wrap.ToString(),top.ToString()});
            Bitmap glyphs;
            if(!textCache.TryGetValue(key,out glyphs))
            {
                glyphs=new Bitmap(bounds.Width,bounds.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                glyphs.SetResolution(dpiX,dpiY);
                using(var large=new Bitmap(bounds.Width*3,bounds.Height*3,System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                {
                    large.SetResolution(dpiX,dpiY);
                    using(var hi=Graphics.FromImage(large))
                    using(var format=(StringFormat)StringFormat.GenericTypographic.Clone())
                    using(var brush=new SolidBrush(color))
                    {
                        hi.ScaleTransform(3,3);hi.TextRenderingHint=TextRenderingHint.AntiAlias;
                        format.Alignment=centered?StringAlignment.Center:StringAlignment.Near;
                        format.LineAlignment=top?StringAlignment.Near:StringAlignment.Center;
                        format.Trimming=StringTrimming.EllipsisCharacter;
                        format.FormatFlags=wrap?StringFormatFlags.LineLimit:StringFormatFlags.NoWrap;
                        hi.DrawString(text,font,brush,new Rectangle(0,0,bounds.Width,bounds.Height),format);
                    }
                    using(var down=Graphics.FromImage(glyphs))
                    {
                        down.CompositingMode=CompositingMode.SourceCopy;down.InterpolationMode=InterpolationMode.HighQualityBicubic;down.PixelOffsetMode=PixelOffsetMode.HighQuality;
                        down.DrawImage(large,new Rectangle(0,0,bounds.Width,bounds.Height),0,0,large.Width,large.Height,GraphicsUnit.Pixel);
                    }
                }
                long bytes=(long)glyphs.Width*glyphs.Height*4;
                while(textOrder.Count>0&&(textBytes+bytes>TextBudget||textOrder.Count>=96))
                {string old=textOrder.Dequeue();var bitmap=textCache[old];textBytes-=(long)bitmap.Width*bitmap.Height*4;textCache.Remove(old);bitmap.Dispose();}
                if(bytes<=TextBudget){textCache.Add(key,glyphs);textOrder.Enqueue(key);textBytes+=bytes;}
                else{DrawGlyphs(g,glyphs,bounds);glyphs.Dispose();return;}
            }
            DrawGlyphs(g,glyphs,bounds);
        }
        static void DrawGlyphs(Graphics g,Bitmap image,Rectangle bounds)
        {
            var state=g.Save();
            g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            g.DrawImage(image,bounds,0,0,image.Width,image.Height,GraphicsUnit.Pixel);g.Restore(state);
        }
        public static void FormatLog(RichTextBox log,int start,int length)
        {
            // Chinese and Latin now share one family. Formatting every Chinese
            // run separately forced the native RichEdit to relayout repeatedly.
            int end=Math.Min(log.TextLength,start+length);
            log.Select(start,Math.Max(0,end-start));log.SelectionFont=log.Font;
            log.Select(log.TextLength,0);log.SelectionFont=log.Font;
        }
    }
    public sealed class SmoothLabel : Label
    {
        public SmoothLabel(){UseCompatibleTextRendering=true;DoubleBuffered=true;}
        protected override void OnPaintBackground(PaintEventArgs e){Theme.PaintBehind(this,e.Graphics);}
        protected override void OnPaint(PaintEventArgs e)
        {
            UiRendering.Quality(e.Graphics);bool centered=TextAlign==ContentAlignment.MiddleCenter||TextAlign==ContentAlignment.TopCenter||TextAlign==ContentAlignment.BottomCenter;
            bool top=TextAlign==ContentAlignment.TopLeft||TextAlign==ContentAlignment.TopCenter||TextAlign==ContentAlignment.TopRight;
            UiRendering.Text(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:SystemColors.GrayText,centered,true,top);
        }
    }
}
