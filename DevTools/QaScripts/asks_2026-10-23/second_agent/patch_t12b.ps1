# T12, second half (the owner, 2026-10-23: "Eliminated guards and players bodies should be draggable(in order to hide them)"): the body of a BUSTED teammate is taken hold of with the grab key too and trails behind the player who drags it.
# A busted body is an ordinary physics body on the machine of its owner (it tipped over and lies); that machine pulls it along behind the dragger (a velocity towards the spot behind him, so walls stop it), and the network transform shows the rest.
# Needs T12 (guards) applied: its PlayerGrab / WobblyPlayer / RunSync.Bodies are the anchors. Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = 'C:\bd\Assets\'

# ---- the busted body is pulled along on the machine that owns it
$SC = $A + 'Scripts\WobblyPlayer.cs'; Open-Src $SC
Rep '        public float DragFactor { get; set; } = 1f;' @'
        public float DragFactor { get; set; } = 1f;
        /// <summary>The player who drags THIS player's body (this player is busted: T12), or null. Every machine knows it (<see cref="PlayerGrab.ApplyDragPlayerBody"/>); the machine that owns this body pulls it along.</summary>
        public WobblyPlayer DraggedBy { get; set; }
'@.TrimEnd()
Rep '            if (InCustody) { HoldDown(); return; }' '            if (InCustody) { HoldDown(); FollowDragger(); return; }'
Rep '        /// <summary>Pinned / busted: the body lies where it fell. No walking, no righting torque; it just settles.</summary>' @'
        /// <summary>A busted body that a teammate drags (T12): it slides to the spot behind him at most <see cref="DraggableBodyRules.FollowSpeed"/> m/s (a velocity, so a wall stops it); only the machine that owns the body runs this.</summary>
        void FollowDragger()
        {
            var by = DraggedBy;
            if (!by) return;
            if (Custody != CustodyState.Busted) { DraggedBy = null; return; }
            var p = by.transform.position; var fwd = by.transform.forward;
            DraggableBodyRules.HoldSpot(p.x, p.z, fwd.x, fwd.z, out float tx, out float tz);
            var d = new Vector3(tx - rb.position.x, 0f, tz - rb.position.z);
            var v = Vector3.ClampMagnitude(d * 6f, DraggableBodyRules.FollowSpeed);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        /// <summary>Pinned / busted: the body lies where it fell. No walking, no righting torque; it just settles.</summary>
'@.TrimEnd()
Save-Src

# ---- the grab key takes hold of a busted body when there is no guard's body in reach
$SC = $A + 'Scripts\PlayerGrab.cs'; Open-Src $SC
Rep '            if (!wob && !TryGetComponent(out wob)) return;
            if (!Net.NetPlayer.IsLocalPlayer(wob)) return; // teammates are driven by their own machine' '            if (!wob && !TryGetComponent(out wob)) return;
            UpkeepPlayerDrag();
            if (!Net.NetPlayer.IsLocalPlayer(wob)) return; // teammates are driven by their own machine'
Rep '            if (Input.GetKeyDown(grabKey)) { if (Held) AskRelease(false); else if (Dragging != null) AskDropBody(); else TryGrab(); }' '            if (Input.GetKeyDown(grabKey)) { if (Held) AskRelease(false); else if (IsDragging) AskDropBody(); else TryGrab(); }'
Rep @'
            if (!wob || wob.InCustody || Held || Dragging != null) return false;
            var body = DraggableBody.Nearest(transform, reach);
            if (body == null) return false;
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestDragBody(body.Guard);
            else ApplyDragBody(body);
            return true;
        }
'@.TrimEnd() @'
            if (!wob || wob.InCustody || Held || IsDragging) return false;
            var body = DraggableBody.Nearest(transform, reach);
            if (body != null)
            {
                if (Net.NetRole.Synced) Net.RunSync.Instance.RequestDragBody(body.Guard);
                else ApplyDragBody(body);
                return true;
            }
            var friend = BustedBodyNear();                               // (no guard's body in reach: perhaps a busted teammate's)
            if (friend == null) return false;
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestDragPlayerBody(friend);
            else ApplyDragPlayerBody(friend);
            return true;
        }

        WobblyPlayer draggingPlayer;

        /// <summary>The busted teammate whose body this player drags, or null (<see cref="WobblyPlayer.DraggedBy"/>).</summary>
        public WobblyPlayer DraggingPlayer => draggingPlayer != null && wob != null && draggingPlayer.DraggedBy == wob ? draggingPlayer : null;

        /// <summary>Does this player drag a body now (a guard's or a teammate's)?</summary>
        public bool IsDragging => Dragging != null || DraggingPlayer != null;

        /// <summary>The nearest busted teammate within reach (flat distance, <see cref="DraggableBodyRules.GrabReach"/>) whom nobody drags yet, or null.</summary>
        WobblyPlayer BustedBodyNear()
        {
            WobblyPlayer best = null; float bestD = float.MaxValue;
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p || p == wob || p.Custody != CustodyState.Busted || p.DraggedBy != null) continue;
                var d = p.transform.position - transform.position; d.y = 0f;
                if (!DraggableBodyRules.CanGrab(d.magnitude, false) || d.magnitude >= bestD) continue;
                best = p; bestD = d.magnitude;
            }
            return best;
        }

        /// <summary>This player now drags the body of this busted teammate (offline: at once; a networked run: the host said so, every machine applies it).</summary>
        public void ApplyDragPlayerBody(WobblyPlayer friend)
        {
            if (!friend || friend == wob || IsDragging) return;
            if (!wob) TryGetComponent(out wob);
            if (!wob) return;
            draggingPlayer = friend;
            friend.DraggedBy = wob;
            wob.DragFactor = DraggableBodyRules.DraggerSpeedShare;
        }

        /// <summary>Every frame, on every machine: a teammate who is not busted any more (bought back) is let go of; on the dragger's own machine, one who is too far away is too.</summary>
        void UpkeepPlayerDrag()
        {
            if (draggingPlayer == null) return;
            if (!draggingPlayer || draggingPlayer.Custody != CustodyState.Busted || wob.InCustody) { ApplyDropBody(); return; }
            if (!Net.NetPlayer.IsLocalPlayer(wob)) return;
            var d = draggingPlayer.transform.position - transform.position; d.y = 0f;
            if (DraggableBodyRules.MustLetGo(d.magnitude)) AskDropBody();
        }
'@.TrimEnd()
Rep @'
        public void ApplyDropBody()
        {
            if (dragging != null) dragging.End();
            dragging = null;
        }
'@.TrimEnd() @'
        public void ApplyDropBody()
        {
            if (dragging != null) dragging.End();
            dragging = null;
            if (draggingPlayer != null)
            {
                if (draggingPlayer && draggingPlayer.DraggedBy == wob) draggingPlayer.DraggedBy = null;
                draggingPlayer = null;
                if (wob) wob.DragFactor = 1f;
            }
        }
'@.TrimEnd()
Save-Src

# ---- the messages: the host checks a request for a busted body and tells everybody; the let-go message covers both kinds
$SC = $A + 'Scripts\Net\RunSync.Bodies.cs'; Open-Src $SC
Rep @'
            if (pg == null || pg.Dragging == null || pg.Dragging.Guard == null) return;
            BodyDragRpc((ushort)pg.Dragging.Guard.NetId, -1);
        }
'@.TrimEnd() @'
            if (pg == null) return;
            if (pg.DraggingPlayer != null && pg.DraggingPlayer.TryGetComponent(out NetPlayer dragged) && dragged.IsSpawned) { PlayerBodyDragRpc((sbyte)dragged.Slot, -1); return; }
            if (pg.Dragging == null || pg.Dragging.Guard == null) return;
            BodyDragRpc((ushort)pg.Dragging.Guard.NetId, -1);
        }

        /// <summary>The local player takes hold of the body of this busted teammate (the host's own request goes straight in).</summary>
        public void RequestDragPlayerBody(WobblyPlayer friend)
        {
            if (friend == null || !friend.TryGetComponent(out NetPlayer fnp) || !fnp.IsSpawned) return;
            if (IsServer) HostDragPlayerBody(Me, fnp.Slot); else DragPlayerRequestRpc((sbyte)fnp.Slot);
        }

        [Rpc(SendTo.Server)]
        void DragPlayerRequestRpc(sbyte slot, RpcParams rp = default) => HostDragPlayerBody(rp.Receive.SenderClientId, slot);

        void HostDragPlayerBody(ulong who, int slot)
        {
            var np = PlayerOf(who);
            var victim = NetPlayer.BySlot(slot);
            var pg = np ? np.GetComponent<PlayerGrab>() : null;
            var vw = victim ? victim.GetComponent<WobblyPlayer>() : null;
            if (pg == null || vw == null || victim == np || pg.Held || pg.IsDragging || vw.Custody != CustodyState.Busted || vw.DraggedBy != null) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;
            if (!Near(np.transform, victim.transform, DraggableBodyRules.GrabReach + 1.5f)) return;               // (a little more than the reach: the request is a moment old)
            PlayerBodyDragRpc((sbyte)victim.Slot, (sbyte)np.Slot);
        }

        /// <summary>Every machine: the player in <paramref name="draggerSlot"/> now drags the body of the busted teammate in <paramref name="victimSlot"/>, or (draggerSlot -1) it is let go.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void PlayerBodyDragRpc(sbyte victimSlot, sbyte draggerSlot)
        {
            var victim = NetPlayer.BySlot(victimSlot);
            var vw = victim ? victim.GetComponent<WobblyPlayer>() : null;
            if (vw == null) return;
            if (draggerSlot < 0)
            {
                var by = vw.DraggedBy ? vw.DraggedBy.GetComponent<PlayerGrab>() : null;
                if (by) by.ApplyDropBody(); else vw.DraggedBy = null;
                return;
            }
            var dragger = NetPlayer.BySlot(draggerSlot);
            var pg = dragger ? dragger.GetComponent<PlayerGrab>() : null;
            if (pg) pg.ApplyDragPlayerBody(vw);
        }
'@.TrimEnd()
Save-Src

# ---- the scenario: a teammate (a copy of the player) is busted, taken hold of, trails behind, is let go of
$SC = $A + 'Editor\NetDev\NetDevAsks1023F.cs'; Open-Src $SC
Rep @'
            GuardCorpse.WaitOverride = -1f; GuardDrops.TestRoll = -1;
            Log($"[Asks1023F] DONE fails={a23fFails}");
'@.TrimEnd() @'
            // ---- T12, the other half: the body of a BUSTED teammate (a copy of the player stands for him)
            {
                GuardCorpse.WaitOverride = 30f;
                if (OpenSpot(player.transform.position + Vector3.up * 0.9f, 8f, out var spotA, true))
                {
                    var mate = Object.Instantiate(player.gameObject);
                    mate.name = "Teammate_test";
                    var mw = mate.GetComponent<WobblyPlayer>(); var mrb = mate.GetComponent<Rigidbody>();
                    var dir2 = new Vector3(spotA.x - player.transform.position.x, 0f, spotA.z - player.transform.position.z).normalized;
                    player.TeleportTo(spotA + Vector3.up * 1.1f, Quaternion.LookRotation(dir2));
                    body0.linearVelocity = Vector3.zero;
                    mw.TeleportTo(spotA + dir2 * 1.0f + Vector3.up * 1.1f, Quaternion.identity);
                    mrb.linearVelocity = Vector3.zero;
                    yield return new WaitForSeconds(0.3f);
                    A23F("T12 (players): a teammate who is free has no body to drag", !pg.TryGrabBody() && !pg.IsDragging);
                    mw.ApplyCustody(CustodyState.Busted, 0f, 2);
                    yield return new WaitForSeconds(1.0f);
                    var mateAt = mate.transform.position;
                    A23F("T12 (players): the body of a busted teammate within reach is taken hold of with the grab key", pg.TryGrabBody() && pg.DraggingPlayer == mw && mw.DraggedBy == player, $"(dragging {(pg.DraggingPlayer ? pg.DraggingPlayer.name : "nobody")})");
                    A23F("T12 (players): the player drags slower while he holds the body", Mathf.Approximately(player.DragFactor, DraggableBodyRules.DraggerSpeedShare));
                    // he walks away: the body trails behind him
                    var farSpot = player.transform.position - dir2 * 5f;
                    for (int i = 1; i <= 25; i++)
                    {
                        var p = Vector3.Lerp(spotA, farSpot, i / 25f);
                        player.TeleportTo(new Vector3(p.x, player.transform.position.y, p.z), Quaternion.LookRotation(-dir2));
                        body0.linearVelocity = Vector3.zero;
                        yield return new WaitForSeconds(0.12f);
                    }
                    yield return new WaitForSeconds(2.5f);
                    var gap2 = mate.transform.position - player.transform.position; gap2.y = 0f;
                    A23F("T12 (players): the busted body followed him: it moved with him and lies within a few metres of him", Vector3.Distance(mate.transform.position, mateAt) > 2.5f && gap2.magnitude < 3.2f, $"(moved {Vector3.Distance(mate.transform.position, mateAt):0.0} m, now {gap2.magnitude:0.0} m from him)");
                    pg.ApplyDropBody();
                    yield return null;
                    A23F("T12 (players): let go, nobody drags it and the player is as fast as before", mw.DraggedBy == null && !pg.IsDragging && Mathf.Approximately(player.DragFactor, 1f));
                    // bought back while dragged: let go at once
                    player.TeleportTo(mate.transform.position + Vector3.right * 1.0f + Vector3.up * 1.1f, Quaternion.identity);
                    body0.linearVelocity = Vector3.zero;
                    yield return null;
                    bool again = pg.TryGrabBody();
                    mw.ApplyCustody(CustodyState.Free, 0f, 0);
                    yield return new WaitForSeconds(0.3f);
                    A23F("T12 (players): a teammate who is bought back while he is dragged is let go of", again && !pg.IsDragging && mw.DraggedBy == null && Mathf.Approximately(player.DragFactor, 1f), $"(took hold {again}, dragging {pg.IsDragging})");
                    Object.Destroy(mate);
                }
                else A23F("(test) an open spot for the teammate", false);
                GuardCorpse.WaitOverride = -1f;
            }

            GuardCorpse.WaitOverride = -1f; GuardDrops.TestRoll = -1;
            Log($"[Asks1023F] DONE fails={a23fFails}");
'@.TrimEnd()
Save-Src
"T12 part 2 patched"
