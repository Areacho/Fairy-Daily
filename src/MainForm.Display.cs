using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiyouDaily
{
    public sealed partial class MainForm
    {
        readonly FairyBackdrop root;
        readonly Panel projects;
        readonly DisplayPreferences display;
        readonly ComboBox displayLayout=new ThemeComboBox(),edgeQuality=new ThemeComboBox();
        readonly SmoothLabel screenInfo=new SmoothLabel();
        bool applyingDisplay,updatingMetrics,preparingFrame;
        int appliedDpi;string appliedLayout;Rectangle appliedArea;
        internal int PreparedFrames{get;private set;}
        internal DisplayPreferences DisplayOptions{get{return display;}}
        internal event Action DisplayOptionsChanged;
        bool CompactDisplay{get{return display.Compact((IsHandleCreated?Screen.FromControl(this):Screen.PrimaryScreen).WorkingArea,IsHandleCreated?Math.Max(96,DeviceDpi):96);}}
        internal Size MinimumLogical{get{return CompactDisplay?new Size(880,650):new Size(980,810);}}
        void BuildDisplayControls()
        {
            var card=new RoundedPanel{Dock=DockStyle.Fill,Padding=new Padding(10,5,10,5),Margin=new Padding(0,4,0,4)};
            var fields=new BufferedTableLayout{Dock=DockStyle.Fill,ColumnCount=5,RowCount=1,BackColor=Color.Transparent};
            foreach(float width in new[]{80,120,90,100})fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,width));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fields.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            fields.Controls.Add(new SmoothLabel{Text="显示布局",Dock=DockStyle.Fill,ForeColor=Theme.Text,TextAlign=ContentAlignment.MiddleLeft},0,0);
            fields.Controls.Add(displayLayout,1,0);fields.Controls.Add(new SmoothLabel{Text="挂件边缘",Dock=DockStyle.Fill,ForeColor=Theme.Text,TextAlign=ContentAlignment.MiddleCenter},2,0);fields.Controls.Add(edgeQuality,3,0);
            foreach(var combo in new[]{displayLayout,edgeQuality}){combo.Dock=DockStyle.Fill;combo.DropDownStyle=ComboBoxStyle.DropDownList;combo.FlatStyle=FlatStyle.Flat;combo.BackColor=Theme.Tile;combo.ForeColor=Theme.Text;combo.Margin=new Padding(0,2,0,0);}
            displayLayout.AccessibleName="总面板显示布局";edgeQuality.AccessibleName="总面板挂件边缘质量";edgeQuality.AccessibleDescription="精细可减少边缘锯齿，标准占用更少绘制时间";
            displayLayout.Items.AddRange(new object[]{"自动适配","紧凑窗口","宽屏窗口"});edgeQuality.Items.AddRange(new object[]{"标准","精细"});
            displayLayout.SelectedIndex=display.Layout=="compact"?1:display.Layout=="wide"?2:0;edgeQuality.SelectedIndex=display.EdgeQuality==3?1:0;
            screenInfo.Dock=DockStyle.Fill;screenInfo.ForeColor=Theme.Muted;screenInfo.TextAlign=ContentAlignment.MiddleRight;screenInfo.AccessibleName="当前显示器分辨率与缩放";fields.Controls.Add(screenInfo,4,0);
            card.Controls.Add(fields);root.Controls.Add(card,0,3);
            displayLayout.SelectedIndexChanged+=(s,e)=>SaveDisplayOptions();edgeQuality.SelectedIndexChanged+=(s,e)=>SaveDisplayOptions();
            root.Layout+=(s,e)=>UpdateMainScrollMetrics();
            ResizeEnd+=(s,e)=>ApplyDisplayLayout(true);
            EventHandler changed=(s,e)=>QueueDisplayUpdate();Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=changed;
            Disposed+=(s,e)=>Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=changed;
        }
        void QueueDisplayUpdate()
        {if(IsDisposed||!IsHandleCreated)return;BeginInvoke(new Action(()=>{if(!IsDisposed&&!WindowInteraction.IsMoving)ApplyDisplayLayout(true);}));}
        void SaveDisplayOptions()
        {
            if(applyingDisplay||IsDisposed||Disposing||displayLayout.SelectedIndex<0||edgeQuality.SelectedIndex<0)return;
            string next=new[]{"auto","compact","wide"}[displayLayout.SelectedIndex];bool layoutChanged=display.Layout!=next;
            display.Layout=next;display.EdgeQuality=edgeQuality.SelectedIndex==1?3:2;
            try{display.Save(store);if(layoutChanged)ApplyDisplayLayout(true);if(DisplayOptionsChanged!=null)DisplayOptionsChanged();Write("显示设置已保存。");}catch(Exception ex){ShowError(ex);}
        }
        internal void ApplyDisplayLayout(bool resize)
        {
            if(applyingDisplay||IsDisposed)return;
            var workArea=Screen.FromControl(this).WorkingArea;
            if(resize&&appliedDpi==DeviceDpi&&appliedLayout==display.Layout&&appliedArea==workArea){UpdateMainScrollMetrics();return;}
            applyingDisplay=true;var previous=Bounds;
            SuspendLayout();root.SuspendLayout();projectGrid.SuspendLayout();
            try
            {
                int columns=CompactDisplay?2:4,rows=8/columns;projectGrid.ColumnCount=columns;projectGrid.RowCount=rows;projectGrid.ColumnStyles.Clear();projectGrid.RowStyles.Clear();
                for(int i=0;i<columns;i++)projectGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/columns));
                for(int i=0;i<rows;i++)projectGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100f/rows));
                var cards=tiles.Cast<Control>().Concat(new[]{(Control)heyboxTile}).ToArray();
                for(int i=0;i<cards.Length;i++)projectGrid.SetCellPosition(cards[i],new TableLayoutPanelCellPosition(i%columns,i/columns));
                root.RowStyles[2].Height=WindowLayout.Pixels(columns==2?640:340,DeviceDpi);
                var area=Screen.FromControl(this).Bounds;screenInfo.Text=area.Width+" × "+area.Height+" · "+(int)Math.Round(DeviceDpi*100.0/96)+"%";
                if(resize){WindowLayout.Fit(this,MinimumLogical);ClientSize=WindowLayout.Pixels(CompactDisplay?new Size(900,760):new Size(1080,900),DeviceDpi);WindowLayout.PreserveCenter(this,previous,MinimumLogical);}
            }
            finally{projectGrid.ResumeLayout(true);root.ResumeLayout(true);ResumeLayout(true);applyingDisplay=false;}
            appliedDpi=DeviceDpi;appliedLayout=display.Layout;appliedArea=workArea;
            UpdateMainScrollMetrics();
        }
        void UpdateMainScrollMetrics()
        {
            if(updatingMetrics||!IsHandleCreated||root==null||root.RowStyles.Count<7)return;updatingMetrics=true;
            try
            {
                int minimum=WindowLayout.Pixels(CompactDisplay?820:1000,DeviceDpi),width=Math.Max(minimum,root.ClientSize.Width-root.Padding.Horizontal);
                if(root.ColumnStyles.Count==0)root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,width));
                var column=root.ColumnStyles[0];if(column.SizeType!=SizeType.Absolute)column.SizeType=SizeType.Absolute;if(Math.Abs(column.Width-width)>.1f)column.Width=width;
                foreach(Control row in root.Controls){var maximum=new Size(Math.Max(1,width-row.Margin.Horizontal),row.MaximumSize.Height);if(row.MaximumSize!=maximum)row.MaximumSize=maximum;}
                int height=(int)Math.Ceiling(root.RowStyles.Cast<RowStyle>().Sum(row=>row.Height))+root.Padding.Vertical;
                root.AutoScrollMinSize=new Size(minimum+root.Padding.Horizontal,height);
            }
            finally{updatingMetrics=false;}
        }
        protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);UpdateMainScrollMetrics();}
        // Prepare the entire child HWND tree before revealing the window. The
        // short-lived composition style is removed before ordinary interaction.
        protected override void SetVisibleCore(bool value)
        {
            if(!value||Visible||preparingFrame||Opacity==0){base.SetVisibleCore(value);return;}
            preparingFrame=true;double opacity=Opacity;Opacity=0;IntPtr window=Handle;bool hadComposition=(DisplayWindowStyle(window,-20)&0x02000000)!=0;
            try
            {
                SetDisplayWindowStyle(window,-20,DisplayWindowStyle(window,-20)|0x02000000);
                base.SetVisibleCore(true);ApplyDisplayLayout(true);PerformLayout();
                using(var frame=new Bitmap(Math.Max(1,Width),Math.Max(1,Height)))
                {
                    frame.SetResolution(DeviceDpi,DeviceDpi);int previousDpi=PopupSurface.RenderingDpi;PopupSurface.RenderingDpi=DeviceDpi;
                    try{DrawToBitmap(frame,new Rectangle(Point.Empty,Size));}finally{PopupSurface.RenderingDpi=previousDpi;}
                }
                PreparedFrames++;Refresh();Opacity=opacity;Refresh();
            }
            finally
            {
                if(!IsDisposed){Opacity=opacity;if(!hadComposition)SetDisplayWindowStyle(Handle,-20,DisplayWindowStyle(Handle,-20)&~0x02000000);}
                preparingFrame=false;
            }
        }
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int DisplayWindowStyle(IntPtr window,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW")]static extern int SetDisplayWindowStyle(IntPtr window,int index,int value);
    }
}
