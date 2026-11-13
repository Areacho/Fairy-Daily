using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MiyouDaily
{
    internal sealed class FairyEyeRenderer : IDisposable
    {
        Bitmap large,frame;int quality;
        internal Bitmap Render(Size size,double seconds,PointF look,bool busy,float openness,int requestedQuality)
        {
            int next=requestedQuality==3?3:2;
            if(frame==null||frame.Size!=size||quality!=next)
            {Dispose();quality=next;large=new Bitmap(size.Width*quality,size.Height*quality,PixelFormat.Format32bppPArgb);frame=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);}
            using(var g=Graphics.FromImage(large))
            {
                g.Clear(Color.Transparent);g.ScaleTransform(quality,quality);g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                float side=Math.Min(size.Width,size.Height),inset=Math.Min(8,side/20);
                FairyArt.FlareEye(g,new RectangleF((size.Width-side)/2+inset,(size.Height-side)/2,side-inset*2,side-inset*2),seconds,look,busy,openness);
            }
            using(var g=Graphics.FromImage(frame))
            {
                g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                g.DrawImage(large,new Rectangle(Point.Empty,size),0,0,large.Width,large.Height,GraphicsUnit.Pixel);
            }
            return frame;
        }
        public void Dispose(){if(large!=null)large.Dispose();if(frame!=null)frame.Dispose();large=frame=null;}
    }
    // Copy premultiplied BGRA pixels directly into a reusable native DIB. This
    // avoids GetHbitmap conversions and new GDI bitmap/DC objects every frame.
    internal sealed class LayeredFrame : IDisposable
    {
        IntPtr dc,image,original,bits;Size size;
        internal int AllocationCount{get;private set;}
        internal void Present(IntPtr window,Bitmap bitmap,Point location,byte alpha)
        {
            if(dc==IntPtr.Zero||size!=bitmap.Size)
            {
                Dispose();size=bitmap.Size;dc=CreateCompatibleDC(IntPtr.Zero);
                var info=new BitmapInfo{Size=40,Width=size.Width,Height=-size.Height,Planes=1,BitCount=32};
                image=CreateDIBSection(dc,ref info,0,out bits,IntPtr.Zero,0);
                if(dc==IntPtr.Zero||image==IntPtr.Zero||bits==IntPtr.Zero){Dispose();throw new System.ComponentModel.Win32Exception();}
                original=SelectObject(dc,image);AllocationCount++;
            }
            var data=bitmap.LockBits(new Rectangle(Point.Empty,size),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            try
            {
                int row=size.Width*4;
                if(data.Stride==row)CopyMemory(bits,data.Scan0,new UIntPtr((uint)(row*size.Height)));
                else for(int y=0;y<size.Height;y++)CopyMemory(IntPtr.Add(bits,y*row),IntPtr.Add(data.Scan0,y*data.Stride),new UIntPtr((uint)row));
            }
            finally{bitmap.UnlockBits(data);}
            var position=location;var source=Point.Empty;var blend=new Blend{Alpha=alpha,Format=1};var dimensions=size;
            if(!UpdateLayeredWindow(window,IntPtr.Zero,ref position,ref dimensions,dc,ref source,0,ref blend,2))throw new System.ComponentModel.Win32Exception();
        }
        public void Dispose()
        {
            if(dc!=IntPtr.Zero&&original!=IntPtr.Zero)SelectObject(dc,original);
            if(image!=IntPtr.Zero)DeleteObject(image);if(dc!=IntPtr.Zero)DeleteDC(dc);dc=image=original=bits=IntPtr.Zero;
        }
        [StructLayout(LayoutKind.Sequential)]struct BitmapInfo{public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,ImageSize;public int XPixels,YPixels;public uint Used,Important;}
        [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend{public byte Operation,Flags,Alpha,Format;}
        [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
        [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr image);
        [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr image);
        [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
        [DllImport("kernel32.dll",EntryPoint="RtlMoveMemory")]static extern void CopyMemory(IntPtr target,IntPtr source,UIntPtr size);
        [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr window,IntPtr screen,ref Point position,ref Size size,IntPtr dc,ref Point source,uint key,ref Blend blend,uint flags);
    }
}
