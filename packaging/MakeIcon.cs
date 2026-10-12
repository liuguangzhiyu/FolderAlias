using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Draw the same simple vector geometry as assets/FolderAlias.svg at every target size.
static class MakeIcon
{
    static GraphicsPath Rounded(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath(); float d = r * 2;
        path.AddArc(x,y,d,d,180,90); path.AddArc(x+w-d,y,d,d,270,90);
        path.AddArc(x+w-d,y+h-d,d,d,0,90); path.AddArc(x,y+h-d,d,d,90,90); path.CloseFigure(); return path;
    }
    static byte[] Render(int size)
    {
        using(var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb))
        using(var graphics=Graphics.FromImage(bitmap))
        using(var stream=new MemoryStream())
        using(var back=new SolidBrush(Color.FromArgb(217,146,23)))
        using(var front=new SolidBrush(Color.FromArgb(255,201,74)))
        using(var blue=new SolidBrush(Color.FromArgb(37,99,235)))
        using(var shine=new Pen(Color.FromArgb(255,228,154),2))
        using(var ink=new Pen(Color.White,2))
        {
            graphics.Clear(Color.Transparent); graphics.SmoothingMode=SmoothingMode.AntiAlias;
            graphics.ScaleTransform(size/48F,size/48F);
            using(var folder=Rounded(4,14,37,28,4)) graphics.FillPath(back,folder);
            using(var tab=new GraphicsPath()) { tab.AddPolygon(new[]{new PointF(4,16),new PointF(4,12),new PointF(8,9),new PointF(18,9),new PointF(24,15)}); graphics.FillPath(back,tab); }
            using(var folder=Rounded(4,16,37,26,4)) graphics.FillPath(front,folder);
            shine.StartCap=shine.EndCap=LineCap.Round; graphics.DrawLine(shine,8,18,36,18);
            graphics.FillEllipse(blue,23,23,24,24); ink.LineJoin=LineJoin.Round;
            graphics.DrawRectangle(ink,30,30,10,8); graphics.DrawLine(ink,35,27,35,43);
            bitmap.Save(stream,ImageFormat.Png); return stream.ToArray();
        }
    }
    static void Main(string[] args)
    {
        string root=Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        var sizes=new[]{16,20,24,32,48,64,128,256}; var images=new List<byte[]>(); foreach(int size in sizes) images.Add(Render(size));
        using(var output=new BinaryWriter(File.Create(Path.Combine(root,"FolderAlias.ico"))))
        {
            output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++)
            { output.Write((byte)(sizes[i]==256?0:sizes[i]));output.Write((byte)(sizes[i]==256?0:sizes[i]));output.Write((byte)0);output.Write((byte)0);output.Write((ushort)1);output.Write((ushort)32);output.Write(images[i].Length);output.Write(offset);offset+=images[i].Length; }
            foreach(var bytes in images) output.Write(bytes);
        }
        File.WriteAllBytes(Path.Combine(root,"Logo.png"),Render(150));
        File.WriteAllBytes(Path.Combine(root,"Icon-preview.png"),Render(256));
    }
}
