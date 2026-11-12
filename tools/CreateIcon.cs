using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
class CreateIcon
{
    static void Circle(Graphics g,Color c,float x,float y,float r){using(var b=new SolidBrush(c))g.FillEllipse(b,x-r,y-r,r*2,r*2);}
    static void Main(string[] args)
    {
        var images=new List<byte[]>();int[] sizes={16,32,48,64,128,256};
        foreach(int size in sizes)using(var bmp=new Bitmap(size,size,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(bmp))
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/160f,size/160f);g.TranslateTransform(80,80);
            Circle(g,Color.FromArgb(224,237,255),0,0,76);Circle(g,Color.FromArgb(55,77,225),0,0,72);
            g.RotateTransform(12);using(var b=new SolidBrush(Color.FromArgb(26,37,95)))g.FillRectangle(b,-48,-48,96,96);g.RotateTransform(-12);
            Circle(g,Color.FromArgb(239,246,255),0,0,52);Circle(g,Color.FromArgb(102,163,227),0,0,36);
            Circle(g,Color.FromArgb(24,35,90),0,0,27);Circle(g,Color.FromArgb(242,250,255),23,25,14);
            using(var ms=new MemoryStream()){bmp.Save(ms,ImageFormat.Png);images.Add(ms.ToArray());}
        }
        using(var writer=new BinaryWriter(File.Create(args[0])))
        {
            writer.Write((short)0);writer.Write((short)1);writer.Write((short)images.Count);int offset=6+16*images.Count;
            for(int i=0;i<images.Count;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((short)0);writer.Write((short)1);writer.Write((short)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
            foreach(var bytes in images)writer.Write(bytes);
        }
    }
}
