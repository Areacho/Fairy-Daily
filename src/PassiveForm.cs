using System;
using System.Windows.Forms;

namespace MiyouDaily
{
    public class PassiveForm : Form
    {
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams
        {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
        protected override void WndProc(ref Message message)
        {
            // MA_NOACTIVATE keeps intentional button clicks, without taking
            // keyboard focus or mouse capture from the foreground application.
            if(message.Msg==0x21){message.Result=new IntPtr(3);return;}
            base.WndProc(ref message);
        }
    }
}
