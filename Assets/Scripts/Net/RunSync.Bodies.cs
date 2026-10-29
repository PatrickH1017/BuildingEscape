using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Dragging the body of an eliminated guard over the network (the owner, 2026-10-23: "Eliminated guards and players bodies should be draggable (in order to hide them)"; <see cref="DraggableBody"/>, <see cref="PlayerGrab"/>): the player asks, the host
    /// checks (a free player, near the body, empty hands, nobody else has it) and tells every machine who drags which body, or that it was let go. Only the host moves the body; the clients see it through the guard snapshots.
    /// </summary>
    public partial class RunSync
    {
        /// <summary>The local player takes hold of the body of this guard (the host's own request goes straight in).</summary>
        public void RequestDragBody(GuardAI g)
        {
            if (g == null || g.NetId < 0) return;
            if (IsServer) HostDragBody(Me, g.NetId); else DragBodyRequestRpc((ushort)g.NetId);
        }

        [Rpc(SendTo.Server)]
        void DragBodyRequestRpc(ushort id, RpcParams rp = default) => HostDragBody(rp.Receive.SenderClientId, id);

        void HostDragBody(ulong who, int id)
        {
            var np = PlayerOf(who);
            var g = NetIds.Get<GuardAI>(id);
            var pg = np ? np.GetComponent<PlayerGrab>() : null;
            var body = g ? g.GetComponent<DraggableBody>() : null;
            if (pg == null || body == null || pg.Held || pg.Dragging != null || !body.CanBeDragged) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;
            if (!Near(np.transform, body.transform, DraggableBodyRules.GrabReach + 1.5f)) return;               // (a little more than the reach: the request is a moment old)
            BodyDragRpc((ushort)id, (sbyte)np.Slot);
        }

        /// <summary>The local player lets go of the body he drags.</summary>
        public void RequestDropBody()
        {
            if (IsServer) HostDropBody(Me); else DropBodyRequestRpc();
        }

        [Rpc(SendTo.Server)]
        void DropBodyRequestRpc(RpcParams rp = default) => HostDropBody(rp.Receive.SenderClientId);

        void HostDropBody(ulong who)
        {
            var np = PlayerOf(who);
            var pg = np ? np.GetComponent<PlayerGrab>() : null;
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
            if (pg == null || vw == null || victim == np || pg.Held || pg.IsDragging || vw.Custody != CustodyState.Busted || vw.DraggedBy != null || vw.DraggedByGuard != null) return;
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

        /// <summary>Host: a body is let go because its dragger got too far from it (<see cref="DraggableBodyRules.LetGoDistance"/>).</summary>
        public void HostEndDrag(DraggableBody body)
        {
            if (IsServer && body != null && body.Guard != null && body.Guard.NetId >= 0) BodyDragRpc((ushort)body.Guard.NetId, -1);
        }

        /// <summary>Every machine: the player in <paramref name="slot"/> now drags the body of this guard, or (slot -1) it is let go.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void BodyDragRpc(ushort guardId, sbyte slot)
        {
            var g = NetIds.Get<GuardAI>(guardId);
            var body = g ? g.GetComponent<DraggableBody>() : null;
            if (!body) return;
            if (slot < 0)
            {
                var by = body.Dragger ? body.Dragger.GetComponent<PlayerGrab>() : null;
                if (by) by.ApplyDropBody(); else body.End();
                return;
            }
            var np = NetPlayer.BySlot(slot);
            var pg = np ? np.GetComponent<PlayerGrab>() : null;
            if (pg) pg.ApplyDragBody(body);
        }
    }
}
