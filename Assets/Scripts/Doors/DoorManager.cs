using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Runs all the sliding doors of one floor (a child of the floor, so it goes with it). About ten times a second it looks at where the
    /// players and the officers are; every frame it moves only the doors that are in motion or waiting to close. Doors are local to each
    /// machine and nothing about them is sent over the network: every machine already knows where everybody is, and a door only decides
    /// what that machine's own physics bumps into (and what that machine hears).
    /// </summary>
    public sealed class DoorManager : MonoBehaviour
    {
        const float ScanEvery = 0.1f;
        const int MaxSoundsPerFrame = 3;

        readonly List<SlidingDoor> doors = new List<SlidingDoor>();
        readonly List<Vector3> actors = new List<Vector3>();
        float nextScan;

        public int Count => doors.Count;
        public IReadOnlyList<SlidingDoor> Doors => doors;

        public void Add(SlidingDoor d) { doors.Add(d); }

        /// <summary>Opens, without a sound, every door somebody is standing at right now.</summary>
        public void OpenDoorsWithPeople()
        {
            GatherActors();
            for (int i = 0; i < doors.Count; i++)
            {
                doors[i].Occupied = doors[i].AnyoneNear(actors);
                if (doors[i].Occupied) doors[i].SnapOpen();
            }
        }

        void Update()
        {
            float now = Time.time, dt = Time.deltaTime;
            bool scan = now >= nextScan;
            if (scan)
            {
                nextScan = now + ScanEvery;
                GatherActors();
            }
            int sounds = MaxSoundsPerFrame;
            for (int i = 0; i < doors.Count; i++)
            {
                var d = doors[i];
                if (scan) d.Occupied = d.AnyoneNear(actors);
                if (d.NeedsTick) d.Tick(now, dt, ref sounds);
            }
        }

        /// <summary>Where everybody who opens doors is: all the players, and every officer who is on his feet.</summary>
        void GatherActors()
        {
            actors.Clear();
            var players = WobblyPlayerRegistry.All;
            for (int i = 0; i < players.Count; i++) if (players[i]) actors.Add(players[i].transform.position);
            var guards = GuardAI.All;
            for (int i = 0; i < guards.Count; i++)
            {
                var g = guards[i];
                if (g && !g.Dead && g.State != GuardState.Stunned) actors.Add(g.transform.position);
            }
        }
    }
}
