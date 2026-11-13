using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiyouDaily
{
    // Use the same cached rounded button and menu drawing as the main UI;
    // no native ComboBox border or light dropdown-arrow surface is involved.
    internal sealed class RoundChoice : RoundButton
    {
        readonly string[] items;
        readonly FairyContextMenu choices=new FairyContextMenu{Opacity=1};
        int selected=-1;
        internal event EventHandler SelectedIndexChanged;
        internal int SelectedIndex
        {
            get{return selected;}
            set{if(value<0||value>=items.Length)throw new ArgumentOutOfRangeException("value");if(selected==value)return;selected=value;Text=items[value]+"  ▾";AccessibleDescription=items[value];if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}
        }
        internal RoundChoice(string[] values)
        {
            items=values;AccessibleRole=AccessibleRole.ComboBox;
            for(int i=0;i<items.Length;i++){int index=i;choices.Items.Add(new FairyMenuItem(items[i],"",(s,e)=>SelectedIndex=index));}
            Click+=(s,e)=>{for(int i=0;i<choices.Items.Count;i++)((FairyMenuItem)choices.Items[i]).Hint=i==selected?"当前":"";choices.Show(this,new Point(0,Height));};
            SelectedIndex=0;
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(e.KeyCode==Keys.Down||e.KeyCode==Keys.Up){SelectedIndex=(selected+(e.KeyCode==Keys.Down?1:items.Length-1))%items.Length;e.Handled=true;e.SuppressKeyPress=true;}
            else if(e.KeyCode==Keys.F4){PerformClick();e.Handled=true;e.SuppressKeyPress=true;}
            base.OnKeyDown(e);
        }
        protected override void Dispose(bool disposing){if(disposing)choices.Dispose();base.Dispose(disposing);}
    }
    internal sealed class DisplaySettingsForm : PreparedForm
    {
        readonly FairyBackdrop root;
        readonly SmoothLabel screenInfo=new SmoothLabel();
        internal readonly RoundChoice LayoutChoice=new RoundChoice(new[]{"自动适配","紧凑窗口","宽屏窗口"});
        internal DisplayPreferences Selection{get{return new DisplayPreferences{Layout=new[]{"auto","compact","wide"}[LayoutChoice.SelectedIndex]};}}
        internal DisplaySettingsForm(DisplayPreferences current)
        {
            Text="FAIRY / 显示设置";Icon=AppAssets.Icon;Font=AppFonts.Create(10);ForeColor=Theme.Text;BackColor=Theme.Background;
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(610,360);MinimizeBox=false;ShowInTaskbar=false;
            root=new FairyBackdrop{Dock=DockStyle.Fill,Padding=new Padding(24,18,24,20),ColumnCount=1,RowCount=4,AutoScroll=true};
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,562));foreach(int height in new[]{66,110,64,62})root.RowStyles.Add(new RowStyle(SizeType.Absolute,height));Controls.Add(root);ScrollbarTheme.Attach(root,true);
            new PageWheelRouter(this,root);
            root.SizeChanged+=(s,e)=>UpdateWidths();
            root.Controls.Add(new SmoothLabel{Text="显示设置",Font=AppFonts.Create(20,FontStyle.Bold),ForeColor=Theme.Text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
            LayoutChoice.AccessibleName="显示布局";
            LayoutChoice.SelectedIndex=current.Layout=="compact"?1:current.Layout=="wide"?2:0;
            root.Controls.Add(SettingCard("显示布局","自动选择卡片列数，也可固定为两列或四列。",LayoutChoice),0,1);
            screenInfo.ForeColor=Theme.Muted;screenInfo.Dock=DockStyle.Fill;screenInfo.TextAlign=ContentAlignment.MiddleLeft;root.Controls.Add(screenInfo,0,2);
            var actions=new BufferedFlowLayout{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,BackColor=Color.Transparent,Padding=new Padding(0,8,0,0)};
            var apply=new RoundButton{Text="应用设置",Primary=true,Size=new Size(136,40),Margin=new Padding(12,0,0,0),DialogResult=DialogResult.OK};
            var cancel=new RoundButton{Text="取消",Size=new Size(110,40),Margin=Padding.Empty,DialogResult=DialogResult.Cancel};
            actions.Controls.Add(apply);actions.Controls.Add(cancel);root.Controls.Add(actions,0,3);AcceptButton=apply;CancelButton=cancel;
            WindowLayout.Watch(this,()=>new Size(480,270),true);
            DpiChanged+=(s,e)=>QueueMetrics();
        }
        protected override void PrepareVisibleFrame(){ApplyMetrics();FitWindow();PerformLayout();}
        void QueueMetrics(){if(!IsHandleCreated||IsDisposed||Disposing)return;try{BeginInvoke(new Action(()=>{if(!IsDisposed&&!Disposing){ApplyMetrics();FitWindow();}}));}catch(InvalidOperationException){}}
        void FitWindow(){var previous=Bounds;WindowLayout.Fit(this,new Size(480,270));ClientSize=WindowLayout.Pixels(new Size(610,360),DeviceDpi);WindowLayout.PreserveCenter(this,previous,new Size(480,270));root.AutoScrollPosition=Point.Empty;}
        bool applyingMetrics;
        void ApplyMetrics()
        {
            if(root==null||applyingMetrics)return;applyingMetrics=true;root.SuspendLayout();root.AutoScrollMinSize=Size.Empty;root.AutoScroll=false;
            try{int[] heights={66,110,64,62};for(int i=0;i<heights.Length;i++)root.RowStyles[i].Height=WindowLayout.Pixels(heights[i],DeviceDpi);root.Padding=new Padding(WindowLayout.Pixels(24,DeviceDpi),WindowLayout.Pixels(18,DeviceDpi),WindowLayout.Pixels(24,DeviceDpi),WindowLayout.Pixels(20,DeviceDpi));foreach(Control control in root.Controls)if(control is RoundedPanel)control.Margin=new Padding(0,WindowLayout.Pixels(6,DeviceDpi),0,WindowLayout.Pixels(6,DeviceDpi));var screen=Screen.FromControl(this);screenInfo.Text="自动适配  ·  "+screen.Bounds.Width+" × "+screen.Bounds.Height+"  ·  缩放 "+(int)Math.Round(DeviceDpi*100.0/96)+"%";}
            finally{root.ResumeLayout(true);applyingMetrics=false;}
            var actions=(BufferedFlowLayout)root.GetControlFromPosition(0,3);actions.Padding=new Padding(0,WindowLayout.Pixels(8,DeviceDpi),0,0);
            foreach(RoundButton button in actions.Controls){bool apply=button.DialogResult==DialogResult.OK;button.Size=WindowLayout.Pixels(new Size(apply?136:110,40),DeviceDpi);button.Margin=new Padding(apply?WindowLayout.Pixels(12,DeviceDpi):0,0,0,0);}
            UpdateWidths();root.AutoScroll=true;root.PerformLayout();
        }
        bool updatingWidths;
        void UpdateWidths()
        {
            if(updatingWidths||applyingMetrics||root==null||root.ColumnStyles.Count==0)return;updatingWidths=true;
            try{int width=Math.Max(WindowLayout.Pixels(512,DeviceDpi),root.ClientSize.Width-root.Padding.Horizontal);root.ColumnStyles[0].Width=width;foreach(Control row in root.Controls){int index=root.GetRow(row);row.MaximumSize=new Size(Math.Max(1,width-row.Margin.Horizontal),Math.Max(1,(int)root.RowStyles[index].Height-row.Margin.Vertical));}root.AutoScrollMinSize=new Size(WindowLayout.Pixels(560,DeviceDpi),(int)(root.RowStyles[0].Height+root.RowStyles[1].Height+root.RowStyles[2].Height+root.RowStyles[3].Height)+root.Padding.Vertical);}
            finally{updatingWidths=false;}
        }
        protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);ApplyMetrics();}
        static Control SettingCard(string title,string description,RoundChoice choice)
        {
            var card=new RoundedPanel{Dock=DockStyle.Fill,Margin=new Padding(0,6,0,6)};
            var name=new SmoothLabel{Text=title,ForeColor=Theme.Cyan,BackColor=Color.Transparent};
            var detail=new SmoothLabel{Text=description,ForeColor=Theme.Muted,BackColor=Color.Transparent};
            card.Controls.AddRange(new Control[]{name,detail,choice});
            card.Layout+=(s,e)=>{int dpi=card.DeviceDpi;name.SetBounds(WindowLayout.Pixels(18,dpi),WindowLayout.Pixels(14,dpi),WindowLayout.Pixels(106,dpi),WindowLayout.Pixels(38,dpi));choice.SetBounds(WindowLayout.Pixels(130,dpi),WindowLayout.Pixels(12,dpi),Math.Max(1,card.Width-WindowLayout.Pixels(148,dpi)),WindowLayout.Pixels(42,dpi));detail.SetBounds(WindowLayout.Pixels(18,dpi),WindowLayout.Pixels(62,dpi),Math.Max(1,card.Width-WindowLayout.Pixels(36,dpi)),WindowLayout.Pixels(26,dpi));};
            return card;
        }
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Theme.DarkTitlebar(Handle);}
    }
}
