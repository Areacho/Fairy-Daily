using System;
namespace MiyouDaily
{
    public sealed class GreetingSession
    {
        bool played;
        public bool Begin(){if(played)return false;played=true;return true;}
        public static int StageAt(double ms){return ms<1450?0:ms<3350?1:2;}
        public static string LineAt(double ms){return StageAt(ms)==0?"主人，我在":StageAt(ms)==1?"今天有什么可以为您效劳":"";}
        public static string VisibleTextAt(double ms)
        {
            string line=LineAt(ms);double local=ms-(StageAt(ms)==1?1450:0);
            int count=Math.Max(0,Math.Min(line.Length,(int)((local-130)/65)));
            return line.Substring(0,count);
        }
    }
}
