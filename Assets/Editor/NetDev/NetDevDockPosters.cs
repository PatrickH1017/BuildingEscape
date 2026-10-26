using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The posters of the Loading Docks' break room (the owner, 2026-10-24: "please put a poster or two up in the Loading Docks break room, reference image attached for which spots"). The pure rules are <c>NetDevTest.DockPosterRulesTest</c> (PolicyTest); the Play half
    /// (<c>DockPosterChecks</c>, run at the start of the scenario <c>AlarmLooks</c>, which begins in the basement) looks at the real room: two posters on its walls, high up, clear of the TV; the picture is %TEMP%\be_ui_alarm_dock_posters.png.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void DockPosterRulesTest()
        {
            var room = new Rect(0.2f, 0.2f, 9.1f, 4.6f);          // (the break room: x 0.2..9.3, z 0.2..4.8)
            var spots = DockPosterRules.BreakRoom(room.xMin, room.yMin, room.xMax, room.yMax);
            Check("dock posters: the break room gets two, one on the west wall and one on the south wall, facing into the room", spots.Length == 2
                && Mathf.Abs(spots[0].x - room.xMin) < 1e-4f && Mathf.Abs(spots[0].yaw - 90f) < 1e-4f && Mathf.Abs(spots[1].z - room.yMin) < 1e-4f && Mathf.Abs(spots[1].yaw) < 1e-4f);
            float tvX = 5.8f, tvHalf = 0.75f + 0.04f;          // (the TV: 1.5 m wide and its frame, on the south wall)
            Check("dock posters: the west one hangs over the desk (z 1.5 to 2.5), the south one two metres or more west of the TV and clear of the corner",
                spots[0].z > 1.5f && spots[0].z < 2.5f && spots[1].x < tvX - tvHalf - 1.5f && spots[1].x > room.xMin + 1.5f, $"(west z {spots[0].z:0.00}, south x {spots[1].x:0.00})");
            Check("dock posters: their middles hang between 1.6 and 2.2 m up, and each is a wall poster of the prop table (a mount, no taller than 1.3 m)", spots.All(s => s.centreY > 1.6f && s.centreY < 2.2f && PropTable.TryGet(s.key, out var d) && d.cat == PropCat.Mount && d.H < 1.3f));
        }
    }

    public partial class NetDevRunner
    {
        /// <summary>The basement's break room: two posters, on its walls, high up, away from the TV; then a picture from where the owner's picture was taken (east of the TV, looking west).</summary>
        IEnumerator DockPosterChecks()
        {
            var gen = FindFirstObjectByType<FloorGenerator>();
            if (gen == null) { AlarmCheck("dock posters: the basement's generator is there", false); yield break; }
            var br = gen.BreakRoom;
            var found = new List<Bounds>();
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name != "poster_1" && t.name != "poster_3") continue;
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (br.Contains(new Vector2(b.center.x, b.center.z))) found.Add(b);
            }
            AlarmCheck("the break room has two posters on show", found.Count == 2, $"({found.Count})");
            bool onWall = true, high = true, clearOfTv = true;
            var tv = FindFirstObjectByType<DockTv>();
            Bounds tvBounds = default; bool haveTv = false;
            if (tv != null) { var trs = tv.GetComponentsInChildren<Renderer>(); if (trs.Length > 0) { tvBounds = trs[0].bounds; foreach (var r in trs) tvBounds.Encapsulate(r.bounds); haveTv = true; } }
            foreach (var b in found)
            {
                float west = Mathf.Abs(b.min.x - br.xMin), south = Mathf.Abs(b.min.z - br.yMin), east = Mathf.Abs(b.max.x - br.xMax), north = Mathf.Abs(b.max.z - br.yMax);
                onWall &= Mathf.Min(Mathf.Min(west, south), Mathf.Min(east, north)) < 0.06f;
                high &= b.min.y > 1.0f && b.max.y < 2.7f;
                if (haveTv) { var grown = tvBounds; grown.Expand(0.4f); clearOfTv &= !grown.Intersects(b); }
            }
            AlarmCheck("each poster has its back against a wall of the break room", found.Count == 2 && onWall);
            AlarmCheck("they hang high on the wall (between 1 and 2.7 m) and clear of the TV", found.Count == 2 && high && clearOfTv && haveTv);
            if (CanPicture)
            {
                // two pictures from a camera of their own (be_shot_dock_posters_*.png): the west wall over the desk, and the south wall west of the TV
                var cam = new GameObject("PosterCam").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f; cam.enabled = false; cam.fieldOfView = 70f;
                cam.transform.position = new Vector3(6.8f, 1.6f, 2.4f); cam.transform.LookAt(new Vector3(0.2f, 1.8f, 2.2f));
                yield return null;
                ShotCam(cam, "dock_posters_west");
                cam.transform.position = new Vector3(7.2f, 1.6f, 3.9f); cam.transform.LookAt(new Vector3(3.5f, 1.8f, 0.2f));
                yield return null;
                ShotCam(cam, "dock_posters_south");
                Destroy(cam.gameObject);
            }
        }
    }
}
