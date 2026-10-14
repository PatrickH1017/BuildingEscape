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

            // Tiny Toon people: a sixth part names the character; old five-part strings stay Party looks
            var toon = PlayerAppearance.ForSlot(1);
            toon.model = "male_a";
            string ts = toon.Serialize();
            Check("toon model round trip", PlayerAppearance.TryParse(ts, out var t) && t.IsToon && t.model == "male_a" && t.Serialize() == ts);
            Check("toon look carries the model", t.ToLook("x").model == "male_a" && PlayerAppearance.ForSlot(0).ToLook("x").model == null);
            Check("old five-part string is a Party look", PlayerAppearance.TryParse("40E6B2-1-2-100-100", out var old) && !old.IsToon && old.model == null);
            Check("rejects bad model ids", !PlayerAppearance.TryParse("40E6B2-1-2-100-100-Male-A", out _)
                && !PlayerAppearance.TryParse("40E6B2-1-2-100-100-bad id", out _)
                && !PlayerAppearance.TryParse("40E6B2-1-2-100-100-" + new string('a', 30), out _)
                && !PlayerAppearance.TryParse("40E6B2-1-2-100-100-", out _));
            var longest = PlayerAppearance.ForSlot(0); longest.model = new string('a', 24);
            Check("longest legal toon string fits the network field", longest.Serialize().Length <= 61
                && PlayerAppearance.TryParse(longest.Serialize(), out _));
            Check("unknown model id still parses (friend on a newer build)", PlayerAppearance.TryParse("40E6B2-1-2-100-100-zz_new_guy", out var unk) && unk.model == "zz_new_guy");
            Check("ids", CharacterCatalog.ValidId("male_a") && !CharacterCatalog.ValidId("Male") && !CharacterCatalog.ValidId("a-b") && !CharacterCatalog.ValidId(""));
        }

        /// <summary>Builds the character catalog from the Tiny Toon pack and checks it (needs the pack imported).</summary>
        public static void ToonSetup()
        {
            UnityEditor.AssetDatabase.Refresh();
            CharacterCatalogBuilder.BuildNow(true);
            var cat = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/Resources/CharacterCatalog.asset");
            Check("catalog asset exists", cat != null);
            if (!cat) return;
            Check("catalog has an atlas", cat.atlas != null);
            Check("catalog lists characters", cat.characters.Length >= 4);
            foreach (var c in cat.characters)
                Debug.Log($"[NetTest] catalog character id={c.id} label={c.label} role={c.role} clips={c.clips} prefab={(c.prefab ? c.prefab.name : "NONE")}");
            Check("three players to pick from", cat.Players().Count >= 3);
            Check("a guard character", cat.FirstWithRole(ToonRole.Guard) != null);
            foreach (var (n, s) in new[] { ("male", cat.male), ("female", cat.female), ("zombie", cat.zombie) })
                Debug.Log($"[NetTest] clipset {n}: idle={s.idle != null} walk={s.walk != null} run={s.run != null} death={s.death != null} attack={s.attack != null} pistolIdle={s.pistolIdle != null} pistolRun={s.pistolRun != null} pistolShoot={s.pistolShoot != null}");
            Check("male clips complete", cat.male.idle && cat.male.walk && cat.male.run && cat.male.death && cat.male.attack);
            Check("female clips complete", cat.female.idle && cat.female.walk && cat.female.run && cat.female.death && cat.female.attack);
            Check("male pistol clips (armed guards)", cat.male.pistolIdle && cat.male.pistolRun && cat.male.pistolShoot);
            foreach (var clip in new[] { cat.male.idle, cat.male.walk, cat.male.run, cat.male.death, cat.male.attack })
                if (clip) Debug.Log($"[NetTest] clip {clip.name}: length={clip.length:0.00}s humanMotion={clip.humanMotion} looping={clip.isLooping}");
        }

        static void Check(string name, bool ok) => Debug.Log($"[NetTest] policy {name}: {(ok ? "PASS" : "FAIL")}");
    }
}