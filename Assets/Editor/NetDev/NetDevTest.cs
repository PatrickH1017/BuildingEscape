using BuildingEscape.EditorTools;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>Batch-mode entry points used to verify the networking work.</summary>
    public static class NetDevTest
    {
        public static void BuildAll()
        {
            GreyboxBuilder.Build();
            NetSceneBuilder.Build();
        }

        /// <summary>Pre-sets the define Steamworks.NET adds on first load, so it cannot recompile mid-Play in tests.</summary>
        public static void SetDefines()
        {
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            string cur = UnityEditor.PlayerSettings.GetScriptingDefineSymbols(target);
            if (!cur.Contains("STEAMWORKS_NET"))
                UnityEditor.PlayerSettings.SetScriptingDefineSymbols(target, string.IsNullOrEmpty(cur) ? "STEAMWORKS_NET" : cur + ";STEAMWORKS_NET");
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("[NetTest] defines: " + UnityEditor.PlayerSettings.GetScriptingDefineSymbols(target));
        }

        public static void PolicyTest()
        {
            RulesTest();
            AppearanceTest();
            string v = NetConst.BuildVersion;
            Check("allow", JoinPolicy.Evaluate(false, 1, v, v, true) == JoinVerdict.Allow);
            Check("allow 4th (3 connected)", JoinPolicy.Evaluate(false, 3, v, v, true) == JoinVerdict.Allow);
            Check("full at 4", JoinPolicy.Evaluate(false, 4, v, v, true) == JoinVerdict.Full);
            Check("run started", JoinPolicy.Evaluate(true, 1, v, v, true) == JoinVerdict.RunStarted);
            Check("version", JoinPolicy.Evaluate(false, 1, "x", v, true) == JoinVerdict.WrongVersion);
            Check("not member", JoinPolicy.Evaluate(false, 1, v, v, false) == JoinVerdict.NotInLobby);
        }

        public static void RulesTest()
        {
            Check("13 floors + docks", TowerRules.OfficeFloors == 13 && TowerRules.Levels == 14);
            Check("group ends are 3,6,9,12", TowerRules.IsGroupEnd(3) && TowerRules.IsGroupEnd(6) && TowerRules.IsGroupEnd(9) && TowerRules.IsGroupEnd(12)
                && !TowerRules.IsGroupEnd(1) && !TowerRules.IsGroupEnd(2) && !TowerRules.IsGroupEnd(13) && !TowerRules.IsGroupEnd(0));
            Check("up needs the quota", TowerRules.UpFrom(1, false, 1) == -1 && TowerRules.UpFrom(1, true, 1) == 2);
            Check("no up from a group end", TowerRules.UpFrom(3, true, 4) == -1 && TowerRules.UpFrom(12, true, 13) == -1);
            Check("docks resume at the next floor", TowerRules.UpFrom(0, false, 4) == 4);
            Check("down goes to the docks", TowerRules.DownFrom(5) == 0 && TowerRules.DownFrom(0) == -1 && TowerRules.DownFrom(13) == -1);
            Check("no up from the boss floor", TowerRules.UpFrom(13, true, 13) == -1);
            Check("resume after leaving", TowerRules.ResumeAfterLeaving(3, true) == 4 && TowerRules.ResumeAfterLeaving(2, false) == 2);
            Check("labels", TowerRules.Label(0) == "Loading Docks" && TowerRules.Label(7) == "Floor 7" && TowerRules.Label(13).StartsWith("Floor 13"));
        }

        public static void AppearanceTest()
        {
            var a = PlayerAppearance.ForSlot(2);
            string text = a.Serialize();
            Check("appearance round trip", PlayerAppearance.TryParse(text, out var b) && b.Serialize() == text);
            Check("appearance default slot 2 is Bubble pink + chef hat", a.hat == 2 && a.body.r == 255);
            Check("rejects empty", !PlayerAppearance.TryParse("", out _));
            Check("rejects garbage", !PlayerAppearance.TryParse("hello-world", out _));
            Check("rejects bad hex", !PlayerAppearance.TryParse("ZZZZZZ-1-0-100-100", out _));
            Check("rejects too long", !PlayerAppearance.TryParse(new string('1', 80), out _));
            Check("clamps face / hat / build", PlayerAppearance.TryParse("FFFFFF-9-9-300-1", out var c)
                && c.face == 3 && c.hat == PlayerAppearance.HatNames.Length - 1
                && c.height == PlayerAppearance.MaxBuild && c.width == PlayerAppearance.MinBuild);
            var rng = new System.Random(7);
            bool allOk = true;
            for (int i = 0; i < 50; i++) allOk &= PlayerAppearance.TryParse(PlayerAppearance.Random(rng).Serialize(), out _);
            Check("50 random looks all parse", allOk);
            Check("look builds", PlayerAppearance.ForSlot(0).ToLook("x").name == "x");
        }

        static void Check(string name, bool ok) => Debug.Log($"[NetTest] policy {name}: {(ok ? "PASS" : "FAIL")}");
    }
}