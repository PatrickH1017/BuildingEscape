# Writes a picture that shows WHERE two pictures differ (red on a dimmed copy of the first): C:\bqs\uipics\diff\<name>.png
# Usage: .\picdiffimg.ps1 -A C:\bqs\uipics\before -B C:\bqs\uipics\classic -Names be_menu_esc,be_menu_lobby_main
param([Parameter(Mandatory)][string]$A, [Parameter(Mandatory)][string]$B, [Parameter(Mandatory)][string[]]$Names, [string]$Out = 'C:\bqs\uipics\diff')
Add-Type -AssemblyName System.Drawing
$src = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class PicDiffImg {
  static byte[] Bytes(Bitmap bmp) {
    var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
    var d = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var bytes = new byte[bmp.Width * bmp.Height * 4];
    Marshal.Copy(d.Scan0, bytes, 0, bytes.Length);
    bmp.UnlockBits(d);
    return bytes;
  }
  public static void Save(string a, string b, string outPath) {
    using (var ia = new Bitmap(a)) using (var ib = new Bitmap(b)) {
      if (ia.Width != ib.Width || ia.Height != ib.Height) return;
      var pa = Bytes(ia); var pb = Bytes(ib);
      var res = new Bitmap(ia.Width, ia.Height, PixelFormat.Format32bppArgb);
      var rect = new Rectangle(0, 0, ia.Width, ia.Height);
      var d = res.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
      var o = new byte[pa.Length];
      for (int i = 0; i < pa.Length; i += 4) {
        bool diff = Math.Abs(pa[i] - pb[i]) > 2 || Math.Abs(pa[i + 1] - pb[i + 1]) > 2 || Math.Abs(pa[i + 2] - pb[i + 2]) > 2;
        if (diff) { o[i] = 40; o[i + 1] = 40; o[i + 2] = 255; o[i + 3] = 255; }
        else { o[i] = (byte)(pa[i] / 3); o[i + 1] = (byte)(pa[i + 1] / 3); o[i + 2] = (byte)(pa[i + 2] / 3); o[i + 3] = 255; }
      }
      Marshal.Copy(o, 0, d.Scan0, o.Length);
      res.UnlockBits(d);
      res.Save(outPath, ImageFormat.Png);
      res.Dispose();
    }
  }
}
'@
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing
New-Item -ItemType Directory -Force $Out | Out-Null
foreach ($n in $Names) { [PicDiffImg]::Save((Join-Path $A "$n.png"), (Join-Path $B "$n.png"), (Join-Path $Out "$n.png")); "wrote $n" }
