using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
namespace MiyouDaily
{
    public static class AppFonts
    {
        static readonly PrivateFontCollection fonts=new PrivateFontCollection();
        static readonly List<IntPtr> memory=new List<IntPtr>(),registrations=new List<IntPtr>();
        static FontFamily family;
        [DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(IntPtr data,uint length,IntPtr reserved,ref uint count);
        public static FontFamily Family
        {
            get
            {
                if(family!=null)return family;
                foreach(string name in new[]{"MiyouUI-Regular.ttf","MiyouUI-Bold.ttf"})
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MiyouDaily."+name))
                {
                    if(stream==null)throw new InvalidDataException("缺少内嵌字体："+name);
                    byte[] bytes=new byte[stream.Length];int offset=0,read;
                    while((read=stream.Read(bytes,offset,bytes.Length-offset))>0)offset+=read;
                    IntPtr ptr=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,ptr,bytes.Length);
                    uint count=0;var registration=AddFontMemResourceEx(ptr,(uint)bytes.Length,IntPtr.Zero,ref count);
                    if(registration==IntPtr.Zero||count==0)throw new InvalidDataException("字体加载失败："+name);
                    memory.Add(ptr);registrations.Add(registration);fonts.AddMemoryFont(ptr,bytes.Length);
                }
                family=fonts.Families[0];return family;
            }
        }
        public static Font Create(float size,FontStyle style=FontStyle.Regular)
        {return new Font(Family,size,style,GraphicsUnit.Point);}
        public static Font Chinese(float size,FontStyle style=FontStyle.Regular)
        {return Create(size,style);}
        public static bool IsChinese(char c)
        {return (c>='\u2e80'&&c<='\u9fff')||(c>='\uf900'&&c<='\ufaff')||(c>='\uff00'&&c<='\uffef');}
    }
}
