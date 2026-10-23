using System;
using System.Collections.Generic;
namespace BuildingEscape.EditorTools
{
    /// <summary>Runs the editor's pure reload-motion checks (NetDevSightsAimTest.cs, unmodified) outside Unity.</summary>
    public static partial class NetDevTest
    {
        public static int Checks, Fails;
        public static List<string> Lines = new List<string>();
        static void Check(string name, bool ok, string detail = "")
        {
            Checks++;
            if (!ok) { Fails++; Lines.Add("FAIL " + name + " [" + detail + "]"); }
            else if (Environment.GetEnvironmentVariable("V") == "1") Lines.Add("pass " + name + (detail.Length > 0 ? " [" + detail + "]" : ""));
        }
        public static int RunPure()
        {
            SightsAimTest();
            foreach (var l in Lines) Console.WriteLine(l);
            Console.WriteLine("reload motion checks: " + Checks + ", failures " + Fails);
            return Fails == 0 ? 0 : 1;
        }
    }
    public static class PureMain { public static int Main() => NetDevTest.RunPure(); }
}
