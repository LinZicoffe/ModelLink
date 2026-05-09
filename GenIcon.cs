using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

var size = 32;
using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
using var g = Graphics.FromImage(bmp);
g.Clear(Color.Transparent);
g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
using var brush = new SolidBrush(Color.FromArgb(255, 217, 119, 87));
g.FillEllipse(brush, 2, 2, size - 4, size - 4);
var iconPath = Path.Combine("Assets", "tray_icon.ico");
using var icon = Icon.FromHandle(bmp.GetHicon());
using var stream = new FileStream(iconPath, FileMode.Create);
icon.Save(stream);
Console.WriteLine("OK: " + Path.GetFullPath(iconPath));
