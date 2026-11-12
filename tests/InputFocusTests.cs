using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    [DllImport("user32.dll")]static extern IntPtr GetFocus();
    [DllImport("user32.dll")]static extern IntPtr GetActiveWindow();
    static void InputFocusChecks()
    {
        using(var login=new HeyboxForm(TestStore(),new Settings()))
        {
            var fields=FindControls(login).OfType<TextBox>().ToArray();
            Assert(fields.Length==2&&fields.All(f=>f.ImeMode==ImeMode.Disable),"phone and verification-code fields use direct numeric input without IME composition");
            var cooldown=(Timer)typeof(HeyboxForm).GetField("cooldown",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(login);
            Assert(!cooldown.Enabled,"an idle login editor has no countdown polling until a code is sent");
        }
        using(var owner=new Form{StartPosition=FormStartPosition.Manual,Location=new Point(-12000,-12000),Size=new Size(600,400),ShowInTaskbar=false})
        using(var host=new Form{Text="Offline modal text input",StartPosition=FormStartPosition.Manual,Location=new Point(-12000,-12000),ClientSize=new Size(650,448),ShowInTaskbar=false})
        using(var timer=new Timer{Interval=80})
        {
            var root=new FairyBackdrop{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};
            root.RowStyles.Add(new RowStyle(SizeType.Percent,50));root.RowStyles.Add(new RowStyle(SizeType.Percent,50));host.Controls.Add(root);
            var first=new TextBox{Dock=DockStyle.Top,Text=""};var second=new TextBox{Dock=DockStyle.Top,Text=""};root.Controls.Add(first,0,0);root.Controls.Add(second,0,1);
            var surface=PopupSurface.Attach(host);Exception failure=null;int ticks=0;TextBox selected=first;
            bool disturbFocus=false;root.Paint+=(s,e)=>{if(disturbFocus){disturbFocus=false;second.Select();}};
            Action step=()=>
            {
                try
                {
                    if(ticks==0||ticks==5){selected=ticks==0?first:second;selected.Select();}
                    Console.WriteLine("Modal input: tick="+ticks+", focused="+GetFocus()+", expected="+selected.Handle+", active native="+GetActiveWindow()+", host="+host.Handle+", foreground="+GetForegroundWindow());
                    Assert(selected.Focused&&GetFocus()==selected.Handle,"offline modal text input has focus before a visual refresh");
                    if(ticks==3)disturbFocus=true; // reproduce a native printing focus side effect
                    var foreground=GetForegroundWindow();
                    surface.RenderNow();
                    Assert(GetForegroundWindow()==foreground,"editor focus protection never activates another foreground window");
                    Assert(selected.Focused&&GetFocus()==selected.Handle,"visual refresh preserves the modal editor's keyboard focus");
                    if(ticks==3)Assert(!disturbFocus,"the focus restoration regression exercised a real sibling focus change during capture");
                    ScrollMessage(GetFocus(),0x102,new IntPtr('7'),IntPtr.Zero);
                    Assert(selected.Text.Length==(ticks%5)+1,"the focused modal text input keeps receiving characters across repaint and caret ticks");
                    if(++ticks==10){timer.Stop();host.Close();}
                }
                catch(Exception ex){failure=ex;timer.Stop();host.Close();}
            };
            timer.Tick+=(s,e)=>step();host.Shown+=(s,e)=>timer.Start();owner.Show();
            host.ShowDialog(owner);owner.Hide();
            if(failure!=null)throw failure;
        }
    }
}
