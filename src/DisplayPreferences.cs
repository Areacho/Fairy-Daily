using System;
using System.Drawing;
using System.IO;

namespace MiyouDaily
{
    internal sealed class DisplayPreferences
    {
        public string Layout {get;set;}
        public DisplayPreferences(){Layout="auto";}
        internal void Normalize(){if(Layout!="compact"&&Layout!="wide")Layout="auto";}
        internal static DisplayPreferences Load(Store store)
        {try{var value=Json.Read<DisplayPreferences>(File.ReadAllText(Path.Combine(store.Root,"display.json")))??new DisplayPreferences();value.Normalize();return value;}catch{return new DisplayPreferences();}}
        internal void Save(Store store){Normalize();Store.AtomicWrite(Path.Combine(store.Root,"display.json"),Json.Write(this));}
        internal Size PreferredClientSize(bool compact,int dpi){return WindowLayout.Pixels(compact?new Size(900,760):new Size(1080,900),dpi);}
        internal bool Compact(Rectangle area,int dpi)
        {return Layout=="compact"||(Layout=="auto"&&area.Width*96.0/dpi<1100);}
    }
}
