# Compares two folders of pictures by name: how many pixels differ (any channel by more than 2). Identical-looking pictures print 0.
# Usage: .\piccmp.ps1 -A C:\bqs\uipics\before -B C:\bqs\uipics\classic
param([Parameter(Mandatory)][string]$A, [Parameter(Mandatory)][string]$B)
Add-Type -AssemblyName System.Drawing
$src = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class PicDiff {
  static byte[] Bytes(Bitmap bmp, out int w, out int h) {
    w = bmp.Width; h = bmp.Height;
    var rect = new Rectangle(0, 0, w, h);
    var d = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var bytes = new byte[w * h * 4];
    Marshal.Copy(d.Scan0, bytes, 0, bytes.Length);
    bmp.UnlockBits(d);
    return bytes;
  }
  public static long Count(string a, string b, out long total) {
    using (var ia = new Bitmap(a)) using (var ib = new Bitmap(b)) {
      int wa, ha, wb, hb;
      var pa = Bytes(ia, out wa, out ha); var pb = Bytes(ib, out wb, out hb);
      total = (long)wa * ha;
      if (wa != wb || ha != hb) return -1;
      long diff = 0;
      for (int i = 0; i < pa.Length; i += 4)
        if (Math.Abs(pa[i] - pb[i]) > 2 || Math.Abs(pa[i + 1] - pb[i + 1]) > 2 || Math.Abs(pa[i + 2] - pb[i + 2]) > 2) diff++;
      return diff;
    }
  }
}
'@
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing
foreach ($f in Get-ChildItem $A -Filter *.png) {
  $other = Join-Path $B $f.Name
  if (-not (Test-Path $other)) { "{0,-42} missing in B" -f $f.Name; continue }
  $total = 0L
  $d = [PicDiff]::Count($f.FullName, $other, [ref]$total)
  if ($d -lt 0) { "{0,-42} different size" -f $f.Name } else { "{0,-42} {1,8} of {2} pixels differ ({3:0.000} %)" -f $f.Name, $d, $total, (100.0 * $d / [Math]::Max(1, $total)) }
}
