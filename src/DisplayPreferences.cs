using System;
using System.Drawing;
using System.IO;

namespace MiyouDaily
{
    internal sealed class DisplayPreferences
    {
        public string Layout {get;set;}
        public string Resolution {get;set;}
        public DisplayPreferences(){Layout="auto";Resolution="auto";}
        internal void Normalize(){if(Layout!="compact"&&Layout!="wide")Layout="auto";if(Resolution!="1080p"&&Resolution!="2k"&&Resolution!="4k")Resolution="auto";}
        internal static DisplayPreferences Load(Store store)
        {try{var value=Json.Read<DisplayPreferences>(File.ReadAllText(Path.Combine(store.Root,"display.json")))??new DisplayPreferences();value.Normalize();return value;}catch{return new DisplayPreferences();}}
        internal void Save(Store store){Normalize();Store.AtomicWrite(Path.Combine(store.Root,"display.json"),Json.Write(this));}
        internal Size ResolutionSize{get{return Resolution=="1080p"?new Size(1920,1080):Resolution=="2k"?new Size(2560,1440):Resolution=="4k"?new Size(3840,2160):Size.Empty;}}
        internal Size PreferredClientSize(bool compact,int dpi){return ResolutionSize.IsEmpty?WindowLayout.Pixels(compact?new Size(900,760):new Size(1080,900),dpi):ResolutionSize;}
        internal bool Compact(Rectangle area,int dpi)
        {int width=ResolutionSize.IsEmpty?area.Width:Math.Min(area.Width,ResolutionSize.Width);return Layout=="compact"||(Layout=="auto"&&width*96.0/dpi<1100);}
    }
}
