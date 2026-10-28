# A plain hit flashed and sounded TWICE on the shooter's machine in a networked run (found by GunNet's marker count, 8 markers for 4 bullets): the shooter's machine plays a plain hit at once when its crosshair is on a foe (a guess), and the host's
# answer for the same bullet played it again - on a client through ShotFeedbackRpc, on the host through RunSync.HostGunShot (its own GunController guess and then the host's answer). Now the guess is remembered (HitFeedback.Guess) and the host's answer
# for that bullet (HitFeedback.Answer) only adds what the guess could not know: a headshot or an elimination plays as the stronger marker, a plain hit is not shown again; an answer with no guess before it (a pellet on a foe the crosshair was not on) plays.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Scripts\Guns\HitFeedback.cs')
Rep '        static void ResetStatics() { instance = null; Last = HitKind.None; LastAt = -100f; Count = 0; }' @'
        static void ResetStatics() { instance = null; Last = HitKind.None; LastAt = -100f; Count = 0; guesses.Clear(); }

        // a networked run: the shooter's machine plays a plain hit at once when its crosshair is on a foe (a guess, see Guess) and the host's answer for that bullet (see Answer) must not play it a second time
        static readonly System.Collections.Generic.Queue<float> guesses = new System.Collections.Generic.Queue<float>();
        /// <summary>How long (s) a guess waits for the host's answer to its bullet before it is forgotten (the host said no hit).</summary>
        public const float GuessLife = 1f;

        /// <summary>The shooter's machine guesses a hit at once: the plain marker and its sound play, and the guess is kept until the host's answer for the bullet comes.</summary>
        public static void Guess()
        {
            guesses.Enqueue(Time.unscaledTime);
            Play(HitKind.Hit);
        }

        /// <summary>
        /// The host's answer for a bullet (the host's own player on the host, a client's through <c>RunSync.ShotFeedbackRpc</c>): what the guess before it could not know - a headshot or an elimination - plays (the stronger marker over the plain one); a
        /// plain hit is not shown again. An answer with no guess before it plays as it is.
        /// </summary>
        public static void Answer(HitKind kind)
        {
            while (guesses.Count > 0 && Time.unscaledTime - guesses.Peek() > GuessLife) guesses.Dequeue();
            if (guesses.Count > 0) { guesses.Dequeue(); if (kind == HitKind.Hit) return; }
            Play(kind);
        }
'@
Save-Src

Open-Src ($A + 'Scripts\Guns\GunController.cs')
Rep 'if (foeSeen) { GameAudio.BulletHit(false, LastImpact.point); HitFeedback.Play(HitKind.Hit); }' 'if (foeSeen) { GameAudio.BulletHit(false, LastImpact.point); HitFeedback.Guess(); }'
Save-Src

Open-Src ($A + 'Scripts\Net\RunSync.Guns.cs')
Rep '                if (who == Me) HitFeedback.Play(feedback); else ShotFeedbackRpc((byte)feedback, RpcTarget.Single(who, RpcTargetUse.Temp));' '                if (who == Me) HitFeedback.Answer(feedback); else ShotFeedbackRpc((byte)feedback, RpcTarget.Single(who, RpcTargetUse.Temp));'
Rep '            if (kind >= 1 && kind <= 3) HitFeedback.Play((HitKind)kind);' '            if (kind >= 1 && kind <= 3) HitFeedback.Answer((HitKind)kind);'
Save-Src

# the checks: one marker for a plain hit, the stronger one for what the guess could not know (offline scenario), the client's marker count in GunNet
Open-Src ($A + 'Editor\NetDev\NetDevBulletDamage.cs')
Rep '            // ---- the dev menu''s alarm buttons' @'
            // ---- a networked run: the guess and the host's answer make ONE marker for a plain hit, and the stronger one for a headshot or an elimination (the guess is forgotten after a second)
            int g0 = HitFeedback.Count;
            HitFeedback.Guess(); HitFeedback.Answer(HitKind.Hit);
            BdCheck("(net feedback) a guessed hit and the host's answer for it are ONE marker", HitFeedback.Count == g0 + 1 && HitFeedback.Last == HitKind.Hit, $"({HitFeedback.Count - g0} markers)");
            HitFeedback.Guess(); HitFeedback.Answer(HitKind.Kill);
            BdCheck("(net feedback) a guess and the host's answer that it was an elimination: the plain marker, then the elimination's", HitFeedback.Count == g0 + 3 && HitFeedback.Last == HitKind.Kill && HitFeedback.LastSound == SoundId.HitKill, $"({HitFeedback.Count - g0} markers, last {HitFeedback.Last})");
            HitFeedback.Answer(HitKind.Head);
            BdCheck("(net feedback) an answer with no guess before it plays as it is (a headshot on a foe the crosshair was not on)", HitFeedback.Count == g0 + 4 && HitFeedback.Last == HitKind.Head, $"({HitFeedback.Count - g0} markers, last {HitFeedback.Last})");
            HitFeedback.Guess();
            yield return new WaitForSecondsRealtime(HitFeedback.GuessLife + 0.2f);
            HitFeedback.Answer(HitKind.Hit);
            BdCheck("(net feedback) a guess the host never answered is forgotten after a second: a later plain hit is shown", HitFeedback.Count == g0 + 6 && HitFeedback.Last == HitKind.Hit, $"({HitFeedback.Count - g0} markers)");

            // ---- the dev menu's alarm buttons
'@
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevPewLoot.cs')
Rep 'HitFeedback.Count >= 4 && HitFeedback.Last == HitKind.Kill' 'HitFeedback.Count >= 4 && HitFeedback.Count <= 5 && HitFeedback.Last == HitKind.Kill'
Save-Src
"combat fix 6 patched"
