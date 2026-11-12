using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MiyouDaily;

partial class Tests
{
    static void DragPerformanceChecks(bool requireCachedMoves=true)
    {
        using(var panel=new MainForm(TestStore(),new Settings()))
        using(var page=new FairyModuleForm(panel,FairySection.MiyouTasks))
        {
            page.Show();page.Location=new Point(-12000,-12000);Application.DoEvents();
            var surface=PopupSurface.Attach(page);surface.RenderNow();Application.DoEvents();
            var start=page.Location;int before=surface.CompositionCount;
            ScrollMessage(page.Handle,0x231,IntPtr.Zero,IntPtr.Zero); // enter native move/size loop
            var watch=Stopwatch.StartNew();
            for(int i=0;i<60;i++)
            {
                page.Location=new Point(start.X+(i%15)*3,start.Y+(i%10)*2);Application.DoEvents();
                if(requireCachedMoves)Assert(surface.Background.Bounds==surface.VisibleBounds(),"cached visual frame follows every drag position immediately");
            }
            watch.Stop();int captures=surface.CompositionCount-before;
            Console.WriteLine("Drag benchmark: 60 moves, "+watch.ElapsedMilliseconds+" ms, "+captures+" complete frame compositions.");
            if(requireCachedMoves)Assert(captures==0,"moving a stable popup never captures or composites its controls again");
            page.Text="Dragging update";
            Application.DoEvents();
            if(requireCachedMoves)Assert(surface.CompositionCount==before,"content changes during dragging do not block window movement");
            ScrollMessage(page.Handle,0x232,IntPtr.Zero,IntPtr.Zero);Application.DoEvents();
            if(requireCachedMoves)Assert(surface.CompositionCount>before,"releasing the window refreshes content changed during dragging");
            before=surface.CompositionCount;watch.Restart();
            for(int i=0;i<30;i++){page.Location=new Point(start.X+(i%10)*2,start.Y+(i%5)*3);Application.DoEvents();}
            watch.Stop();
            Console.WriteLine("Anchored/programmatic movement: 30 moves, "+watch.ElapsedMilliseconds+" ms, "+(surface.CompositionCount-before)+" compositions.");
            if(requireCachedMoves)Assert(surface.CompositionCount==before,"anchor-driven and programmatic movement also reuse the cached pixels");
            ScrollMessage(page.Handle,0x231,IntPtr.Zero,IntPtr.Zero);before=surface.CompositionCount;
            page.ClientSize=new Size(page.ClientSize.Width+30,page.ClientSize.Height+20);Application.DoEvents();
            if(requireCachedMoves)Assert(surface.CompositionCount>before&&surface.Background.Bounds==surface.VisibleBounds(),"resizing inside the move loop still rebuilds a correctly sized frame");
            ScrollMessage(page.Handle,0x232,IntPtr.Zero,IntPtr.Zero);Application.DoEvents();
            panel.AllowExit=true;page.Close();
        }
    }
}
