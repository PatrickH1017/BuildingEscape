// The launcher at the top of the friends' build (the owner, 2026-10-24: "hide all the other files in a build folder behind another folder and keep the executable one folder outside so it is by itself and testers see it first" ... "yes launcher separate please").
// It sits next to a folder called Game that holds the real player (EmployeesTakeRevenge.exe, UnityPlayer.dll, EmployeesTakeRevenge_Data, ...), starts Game\EmployeesTakeRevenge.exe with Game as its working folder, passes every command-line word on to it
// (-screen-width ..., -be-autopilot ... for the self-play), waits for it and returns its exit code. Built with the .NET Framework's own csc (it is part of Windows): package_launcher.ps1.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

static class Launcher
{
    const string Title = "Employees Take Revenge";

    [STAThread]
    static int Main(string[] args)
    {
        string here = AppDomain.CurrentDomain.BaseDirectory;
        string game = Path.Combine(here, "Game");
        string exe = Path.Combine(game, "EmployeesTakeRevenge.exe");
        if (!File.Exists(exe))
        {
            MessageBox.Show("Game\\EmployeesTakeRevenge.exe was not found. Unzip the whole folder first, then run this again.", Title);
            return 2;
        }
        var info = new ProcessStartInfo(exe) { WorkingDirectory = game, UseShellExecute = false, Arguments = Join(args) };
        try
        {
            using (var process = Process.Start(info))
            {
                process.WaitForExit();
                return process.ExitCode;
            }
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, Title);
            return 3;
        }
    }

    /// <summary>The words as one command line: a word with a space or a quote in it goes in quotes (the quotes and the backslashes before them escaped).</summary>
    static string Join(string[] args)
    {
        var sb = new StringBuilder();
        foreach (var a in args)
        {
            if (sb.Length > 0) sb.Append(' ');
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) { sb.Append(a); continue; }
            sb.Append('"');
            int slashes = 0;
            foreach (char c in a)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { sb.Append('\\', slashes * 2 + 1); sb.Append('"'); }
                else { sb.Append('\\', slashes); sb.Append(c); }
                slashes = 0;
            }
            sb.Append('\\', slashes * 2);
            sb.Append('"');
        }
        return sb.ToString();
    }
}
