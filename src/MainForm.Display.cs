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
        bool applyingDisplay,updatingMetrics,preparingFrame;
        int appliedDpi;string appliedLayout,appliedResolution;Rectangle appliedArea;
        internal int PreparedFrames{get;private set;}
        internal DisplayPreferences DisplayOptions{get{return display;}}
        internal event Action DisplayOptionsChanged;
        bool CompactDisplay{get{return display.Compact((IsHandleCreated?Screen.FromControl(this):Screen.PrimaryScreen).WorkingArea,IsHandleCreated?Math.Max(96,DeviceDpi):96);}}
        internal Size MinimumLogical{get{var size=CompactDisplay?new Size(880,650):new Size(980,810);var resolution=display.ResolutionSize;if(!resolution.IsEmpty){int dpi=IsHandleCreated?DeviceDpi:96;size=new Size(Math.Min(size.Width,(int)(resolution.Width*96.0/dpi)),Math.Min(size.Height,(int)(resolution.Height*96.0/dpi)));}return size;}}
        void BuildDisplayControls()
        {
            var heading=(Control)root.GetControlFromPosition(0,0);
            var entry=MakeButton("显示设置",false);entry.AccessibleName="打开显示设置";entry.Size=new Size(112,38);heading.Controls.Add(entry);
            heading.Layout+=(s,e)=>{int z=DeviceDpi;entry.SetBounds(heading.Width-WindowLayout.Pixels(112,z),WindowLayout.Pixels(8,z),WindowLayout.Pixels(112,z),WindowLayout.Pixels(38,z));};
            entry.Click+=(s,e)=>{using(var settings=CreateDisplaySettings())if(settings.ShowDialog(this)==DialogResult.OK)SaveDisplayOptions(settings.Selection);};
            root.Layout+=(s,e)=>UpdateMainScrollMetrics();
            ResizeEnd+=(s,e)=>ApplyDisplayLayout(true);
            EventHandler changed=(s,e)=>QueueDisplayUpdate();Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=changed;
            Disposed+=(s,e)=>Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=changed;
        }
        void QueueDisplayUpdate()
        {if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke(new Action(()=>{if(!IsDisposed&&!WindowInteraction.IsMoving)ApplyDisplayLayout(true);}));}catch(InvalidOperationException){}}
        internal DisplaySettingsForm CreateDisplaySettings(){return new DisplaySettingsForm(display);}
        internal void SaveDisplayOptions(DisplayPreferences selection)
        {
            if(applyingDisplay||IsDisposed||Disposing)return;
            selection.Normalize();
            try{selection.Save(store);root.AutoScrollPosition=Point.Empty;display.Layout=selection.Layout;display.Resolution=selection.Resolution;ApplyDisplayLayout(true);root.AutoScrollPosition=Point.Empty;if(DisplayOptionsChanged!=null)DisplayOptionsChanged();Write("显示设置已保存。");}catch(Exception ex){ShowError(ex);}
        }
        internal void ApplyDisplayLayout(bool resize)
        {
            if(applyingDisplay||IsDisposed)return;
            var workArea=Screen.FromControl(this).WorkingArea;
            if(resize&&appliedDpi==DeviceDpi&&appliedLayout==display.Layout&&appliedResolution==display.Resolution&&appliedArea==workArea){UpdateMainScrollMetrics();return;}
            applyingDisplay=true;var previous=Bounds;
            root.AutoScrollMinSize=Size.Empty;root.AutoScroll=false;
            SuspendLayout();root.SuspendLayout();projectGrid.SuspendLayout();
            try
            {
                int[] heights={90,146,164,340,60,190};for(int i=0;i<heights.Length;i++)root.RowStyles[i].Height=WindowLayout.Pixels(heights[i],DeviceDpi);
                root.Padding=new Padding(WindowLayout.Pixels(26,DeviceDpi),WindowLayout.Pixels(18,DeviceDpi),WindowLayout.Pixels(26,DeviceDpi),WindowLayout.Pixels(18,DeviceDpi));
                foreach(Control row in root.Controls)row.Margin=new Padding(0,WindowLayout.Pixels(6,DeviceDpi),0,WindowLayout.Pixels(6,DeviceDpi));
                projects.Padding=new Padding(0,WindowLayout.Pixels(38,DeviceDpi),0,0);projectGrid.Padding=new Padding(WindowLayout.Pixels(10,DeviceDpi),0,WindowLayout.Pixels(10,DeviceDpi),WindowLayout.Pixels(10,DeviceDpi));
                buttons.Padding=new Padding(0,WindowLayout.Pixels(8,DeviceDpi),0,0);
                foreach(Control button in buttons.Controls){button.Size=WindowLayout.Pixels(new Size(button==cancel?72:112,34),DeviceDpi);button.Margin=new Padding(0,0,WindowLayout.Pixels(12,DeviceDpi),0);}
                int columns=CompactDisplay?2:4,rows=8/columns;projectGrid.ColumnCount=columns;projectGrid.RowCount=rows;projectGrid.ColumnStyles.Clear();projectGrid.RowStyles.Clear();
                for(int i=0;i<columns;i++)projectGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/columns));
                for(int i=0;i<rows;i++)projectGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100f/rows));
                var cards=tiles.Cast<Control>().Concat(new[]{(Control)heyboxTile}).ToArray();
                for(int i=0;i<cards.Length;i++)projectGrid.SetCellPosition(cards[i],new TableLayoutPanelCellPosition(i%columns,i/columns));
                root.RowStyles[3].Height=WindowLayout.Pixels(columns==2?640:340,DeviceDpi);
                if(resize){WindowLayout.Fit(this,MinimumLogical);ClientSize=display.PreferredClientSize(CompactDisplay,DeviceDpi);WindowLayout.PreserveCenter(this,previous,MinimumLogical);}
            }
            finally{projectGrid.ResumeLayout(true);root.ResumeLayout(true);ResumeLayout(true);applyingDisplay=false;}
            appliedDpi=DeviceDpi;appliedLayout=display.Layout;appliedResolution=display.Resolution;appliedArea=workArea;
            layoutAccounts();
            UpdateMainScrollMetrics();root.PerformLayout();
        }
        void UpdateMainScrollMetrics()
        {
            if(updatingMetrics||!IsHandleCreated||root==null||root.RowStyles.Count<6)return;updatingMetrics=true;
            try
            {
                int minimum=WindowLayout.Pixels(CompactDisplay?820:1000,DeviceDpi),width=Math.Max(minimum,root.ClientSize.Width-root.Padding.Horizontal);
                if(root.ColumnStyles.Count==0)root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,width));
                var column=root.ColumnStyles[0];if(column.SizeType!=SizeType.Absolute)column.SizeType=SizeType.Absolute;if(Math.Abs(column.Width-width)>.1f)column.Width=width;
                foreach(Control row in root.Controls){int index=root.GetRow(row);var maximum=new Size(Math.Max(1,width-row.Margin.Horizontal),Math.Max(1,(int)root.RowStyles[index].Height-row.Margin.Vertical));if(row.MaximumSize!=maximum)row.MaximumSize=maximum;}
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
