using System;
using System.IO;

namespace MiyouDaily
{
    internal static class WidgetSession
    {
        static string ClosedPath(Store store){return Path.Combine(store.Root,"widget-closed.flag");}
        internal static bool BackgroundAllowed(Store store){return !File.Exists(ClosedPath(store));}
        internal static void Close(Store store){Store.AtomicWrite(ClosedPath(store),DateTimeOffset.UtcNow.ToString("o"));}
        // A direct launch or Windows logon is a new explicit UI session.
        internal static void Resume(Store store){string path=ClosedPath(store);if(File.Exists(path))File.Delete(path);}
        internal static bool BeginLaunch(Store store,bool notification)
        {if(notification)return BackgroundAllowed(store);Resume(store);return true;}
    }
}
