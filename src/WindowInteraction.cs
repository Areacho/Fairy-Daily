using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace MiyouDaily
{
    // All UI timers share this gate, including the eye behind a moving page.
    // The native windows retain their last pixels while their position changes.
    internal sealed class WindowInteraction : NativeWindow,IDisposable
    {
        static readonly ConditionalWeakTable<Form,WindowInteraction> watches=new ConditionalWeakTable<Form,WindowInteraction>();
        static int movingCount;
        readonly Form form;bool moving;
        internal static bool IsMoving{get{return movingCount>0;}}
        internal static void Attach(Form form){watches.GetValue(form,f=>new WindowInteraction(f));}
        internal static void SetMoving(Form form,bool value){watches.GetValue(form,f=>new WindowInteraction(f)).SetMoving(value);}
        WindowInteraction(Form form)
        {
            this.form=form;form.HandleCreated+=Created;form.HandleDestroyed+=Destroyed;form.Disposed+=Disposed;
            if(form.IsHandleCreated)AssignHandle(form.Handle);
        }
        void SetMoving(bool value){if(moving==value)return;moving=value;movingCount+=value?1:-1;}
        void Created(object sender,EventArgs e){AssignHandle(form.Handle);}
        void Destroyed(object sender,EventArgs e){SetMoving(false);ReleaseHandle();}
        void Disposed(object sender,EventArgs e){Dispose();}
        protected override void WndProc(ref Message message)
        {
            if(message.Msg==0x231)SetMoving(true);
            try{base.WndProc(ref message);}
            finally{if(message.Msg==0x232)SetMoving(false);}
        }
        public void Dispose()
        {
            SetMoving(false);form.HandleCreated-=Created;form.HandleDestroyed-=Destroyed;form.Disposed-=Disposed;
            ReleaseHandle();watches.Remove(form);
        }
    }
}
