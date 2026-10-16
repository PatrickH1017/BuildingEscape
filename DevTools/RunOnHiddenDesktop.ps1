# Runs a program on a separate, hidden Windows desktop, so a game or Editor window never appears on your screen and never takes your focus.
#
# Why: the HUD is drawn with IMGUI, and IMGUI is NOT drawn in Unity's batch mode (neither the Editor nor a player started with -batchmode ever
# calls OnGUI). To get a real picture of it you need a real window, and this gives it one that nobody sees. See DEVELOPER_NOTES.md
# section 2.14 and 8.6 (NetDevHarness.Ui is the scenario that takes the pictures).
#
# Usage (a real, NON-batch Unity Editor on a spare copy of the project; the Editor must not already have that project open):
#   .\DevTools\RunOnHiddenDesktop.ps1 -Exe "C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe" `
#       -Arguments '-projectPath "C:\path\to\a\spare\copy" -executeMethod BuildingEscape.EditorTools.NetDevHarness.Ui -logFile "C:\temp\ui.log"' -TimeoutSeconds 600
# or a player build:
#   .\DevTools\RunOnHiddenDesktop.ps1 -Exe C:\path\Game.exe -Arguments '-screen-width 1920 -screen-height 1080 -screen-fullscreen 0'
# It prints the program's exit code, or TIMEOUT (the program is killed after -TimeoutSeconds).
param([Parameter(Mandatory = $true)][string]$Exe, [string]$Arguments = '', [int]$TimeoutSeconds = 120, [string]$Desktop = 'HiddenDesktop')
if (-not ('HiddenDesktop' -as [type])) {
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class HiddenDesktop
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateDesktopW(string name, IntPtr device, IntPtr devmode, int flags, uint access, IntPtr sa);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool CloseDesktop(IntPtr h);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO
    {
        public int cb; public string lpReserved; public string lpDesktop; public string lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessW(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr h, uint code);
    [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr h, out uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    /// <summary>Returns the exit code, -1 if it had to be killed after the timeout, -2 if it could not be started.</summary>
    public static int Run(string exe, string args, string workDir, string desktop, int timeoutMs)
    {
        IntPtr d = CreateDesktopW(desktop, IntPtr.Zero, IntPtr.Zero, 0, 0x10000000, IntPtr.Zero);
        if (d == IntPtr.Zero) { Console.WriteLine("CreateDesktop failed: " + Marshal.GetLastWin32Error()); return -2; }
        var si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        si.lpDesktop = "WinSta0\\" + desktop;
        PROCESS_INFORMATION pi;
        var cmd = new StringBuilder("\"" + exe + "\" " + args);
        if (!CreateProcessW(null, cmd, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, workDir, ref si, out pi))
        {
            Console.WriteLine("CreateProcess failed: " + Marshal.GetLastWin32Error());
            CloseDesktop(d);
            return -2;
        }
        uint w = WaitForSingleObject(pi.hProcess, (uint)timeoutMs);
        int result;
        if (w != 0) { TerminateProcess(pi.hProcess, 1); WaitForSingleObject(pi.hProcess, 5000); result = -1; }
        else { uint code; GetExitCodeProcess(pi.hProcess, out code); result = (int)code; }
        CloseHandle(pi.hProcess); CloseHandle(pi.hThread);
        CloseDesktop(d);
        return result;
    }
}
"@
}
$r = [HiddenDesktop]::Run($Exe, $Arguments, (Split-Path $Exe), $Desktop, $TimeoutSeconds * 1000)
if ($r -eq -1) { "TIMEOUT (killed after $TimeoutSeconds s)" } elseif ($r -eq -2) { "COULD NOT START" } else { "exit=$r" }
