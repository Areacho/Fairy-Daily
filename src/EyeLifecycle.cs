using System;

namespace MiyouDaily
{
    internal enum EyePhase { Opening, Ready, Closing, Closed }
    internal sealed class EyeLifecycle
    {
        internal const double OpenSeconds=.62,CloseSeconds=.50;
        internal EyePhase Phase {get;private set;}
        internal float Openness {get;private set;}
        internal float Alpha {get;private set;}
        double began;float closingFrom=1;
        internal EyeLifecycle(){BeginOpening(0);}
        internal void BeginOpening(double now){began=now;Phase=EyePhase.Opening;Openness=Alpha=0;}
        internal void BeginClosing(double now)
        {Advance(now);closingFrom=Openness;began=now;Phase=EyePhase.Closing;}
        static float Ease(double value){double t=Math.Max(0,Math.Min(1,value));return (float)(1-Math.Pow(1-t,3));}
        internal bool Advance(double now)
        {
            if(Phase==EyePhase.Opening)
            {
                Openness=Ease((now-began)/OpenSeconds);Alpha=(float)Math.Sqrt(Openness);
                if(now-began>=OpenSeconds){Phase=EyePhase.Ready;Openness=Alpha=1;}
            }
            else if(Phase==EyePhase.Closing)
            {
                double t=Math.Max(0,(now-began)/CloseSeconds);Openness=closingFrom*(1-Ease(t));
                Alpha=(float)Math.Sqrt(Openness);
                if(t>=1){Phase=EyePhase.Closed;Openness=Alpha=0;return true;}
            }
            return Phase==EyePhase.Closed;
        }
    }
}
