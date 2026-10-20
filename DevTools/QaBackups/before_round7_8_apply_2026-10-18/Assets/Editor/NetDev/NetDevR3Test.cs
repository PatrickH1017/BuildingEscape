using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>The rules of round 3 (DEVELOPER_NOTES 2.23) that can be checked without playing.</summary>
    public static partial class NetDevTest
    {
        public static void R3RulesTest()
        {
            SightTest();
            DoorSlideTest();
        }

        /// <summary>R12: the doors slide into the wall beside them, and two doors never overlap.</summary>
        static void DoorSlideTest()
        {
            const float H = 3.5f;
            Rc Wall(float a0, float a1) => new Rc(a0, 0f, a1, 0.2f);                   // a wall box along x (thickness 0.2)
            Rc Beam(float a0, float a1) => new Rc(a0, 0f, a1, 0.2f);

            // one 2 m doorway in a long wall: both leaves slide in all the way, hidden in the wall
            var one = DoorSlide.PlanAll(new[] { Beam(4f, 6f) }, new[] { Wall(0f, 4f), Wall(6f, 12f) }, null);
            Check("a door with wall on both sides slides into it", one[0].slide && one[0].lowLane == 0 && one[0].highLane == 0);
            Check("...with both leaves clear of the doorway", Mathf.Approximately(one[0].lowTravel, 1f) && Mathf.Approximately(one[0].highTravel, 1f));

            // a short piece of wall at one end: that leaf slides as far as it can (the rest stays in the doorway)
            var shortEnd = DoorSlide.PlanAll(new[] { Beam(4.9f, 6.9f) }, new[] { Wall(4f, 4.9f), Wall(6.9f, 12f) }, null);
            Check("a 0.9 m end piece takes 0.82 m of the leaf (never more than the wall holds)", shortEnd[0].slide && shortEnd[0].lowTravel > 0.8f && shortEnd[0].lowTravel < 0.9f && Mathf.Approximately(shortEnd[0].highTravel, 1f));

            // a wall with no room at all: the door narrows into its jambs as before
            var none = DoorSlide.PlanAll(new[] { Beam(0.2f, 2.2f) }, new[] { Wall(2.2f, 8f) }, null);
            Check("a door with wall on one side only (half the travel) narrows into its jambs", !none[0].slide);

            // two doors with one piece of wall between them that holds both leaves: both slide in, side by side
            var two = DoorSlide.PlanAll(new[] { Beam(2f, 4f), Beam(8f, 10f) }, new[] { Wall(0f, 2f), Wall(4f, 8f), Wall(10f, 14f) }, null);
            Check("two doors with room for both leaves between them: both slide in all the way", two[0].slide && two[1].slide && Mathf.Approximately(two[0].highTravel, 1f) && Mathf.Approximately(two[1].lowTravel, 1f));

            // a wide hall door (4.8 m, 2.4 m leaves) 3 m from a narrow one: the piece holds 3.0 m for 3.4 m of leaves - the two share it, they do not overlap
            var wide = DoorSlide.PlanAll(new[] { Beam(2.4f, 7.2f), Beam(10.2f, 12.2f) }, new[] { Wall(0f, 2.4f), Wall(7.2f, 10.2f), Wall(12.2f, 18f) }, null);
            float usedByWide = wide[0].highTravel, usedByNarrow = wide[1].lowTravel;
            Check("the piece between a wide and a narrow door is shared: their leaves together never go further than the piece", usedByWide + usedByNarrow <= 3.0f - 2f * DoorSlide.EndMargin - DoorSlide.LeafGap + 1e-3f);
            Check("...and both doors still slide (each gets its share)", wide[0].slide && wide[1].slide && usedByWide > 1.9f && usedByNarrow > 0.7f);

            // a glass front beside a door: the leaf cannot go inside it and slides in front of the glass, on the corridor's side
            var glass = DoorSlide.PlanAll(new[] { Beam(4f, 6f) }, new[] { Wall(0f, 4f), Wall(6f, 12f) }, new[] { 1, 0 });
            Check("a door beside a glass front slides in front of the glass on the corridor's side", glass[0].slide && glass[0].lowLane == 1 && glass[0].highLane == 0);

            // a door in a wall along z (turned a quarter): its low leaf is on the high side
            var tall = DoorSlide.PlanAll(new[] { new Rc(0f, 4f, 0.2f, 6f) }, new[] { new Rc(0f, 0f, 0.2f, 4f), new Rc(0f, 6f, 0.2f, 12f) }, new[] { 0, -1 });
            Check("in a wall along z the low leaf is on the far side (the door is turned a quarter): the glass lane follows", tall[0].slide && tall[0].highLane == 0 && tall[0].lowLane == -1);

            // every door of 60 real floors: nothing slides further than its leaf or its wall, and two doors never overlap
            int doors = 0, slides = 0, swings = 0, badSwing = 0, overlaps = 0, tooFar = 0, floors = 0;
            for (int seed = 1; seed <= 20; seed++)
                foreach (int fl in new[] { 1, 4, 9 })
                {
                    var shape = FloorShape.Maze(new System.Random(seed * 31 + fl), 8, 5, 0.12);
                    var layout = OfficePlanner.Build(shape.PlanFor(fl, seed * 31 + fl, 18f, 3.5f, 777));
                    var beams = new System.Collections.Generic.List<WallBox>();
                    var plans = DoorSlide.ForLayout(layout, H, null, beams);
                    floors++;
                    // the walls (pieces) the doors touch: the leaves that go into the same piece must be disjoint
                    var pieceUse = new System.Collections.Generic.Dictionary<WallBox, float>();
                    for (int i = 0; i < beams.Count; i++)
                    {
                        doors++;
                        float leaf = (beams[i].rc.W >= beams[i].rc.D ? beams[i].rc.W : beams[i].rc.D) * 0.5f;
                        if (plans[i].slide) slides++;
                        else
                        {
                            swings++;
                            // a door that cannot slide swings open into the room it belongs to (the outpost's door: out of the room)
                            var rr = beams[i].rc; bool along = rr.W >= rr.D;
                            float px = along ? rr.Cx : rr.Cx + plans[i].swing * 1.3f, pz = along ? rr.Cz + plans[i].swing * 1.3f : rr.Cz;
                            RoomInfo owner = null;
                            foreach (var room in layout.rooms) { foreach (var dd in room.doors) if (dd.Overlaps(rr.Grow(0.05f))) { owner = room; break; } if (owner != null) break; }
                            bool inside = owner != null && owner.bounds.Contains(px, pz);
                            if (plans[i].swing == 0 || owner == null || inside != (owner.theme != "outpost")) badSwing++;
                        }
                        if (plans[i].lowTravel > leaf + 1e-3f || plans[i].highTravel > leaf + 1e-3f || plans[i].lowTravel < 0f || plans[i].highTravel < 0f) tooFar++;
                    }
                    // no two doors' leaves overlap: for every piece, what its two neighbours take fits in it
                    foreach (var w in layout.walls)
                    {
                        if (w.kind != "wall" || w.y0 > 0.01f || w.y1 < H - 0.01f) continue;
                        bool ax = w.rc.W >= w.rc.D;
                        float lo = ax ? w.rc.x0 : w.rc.z0, hi = ax ? w.rc.x1 : w.rc.z1, perp = ax ? w.rc.Cz : w.rc.Cx;
                        float taken = 0f; int touching = 0;
                        for (int i = 0; i < beams.Count; i++)
                        {
                            var r = beams[i].rc; bool bx = r.W >= r.D;
                            if (bx != ax || Mathf.Abs((bx ? r.Cz : r.Cx) - perp) > 0.06f) continue;
                            float blo = bx ? r.x0 : r.z0, bhi = bx ? r.x1 : r.z1;
                            bool touchesLo = Mathf.Abs(bhi - lo) <= 0.06f, touchesHi = Mathf.Abs(blo - hi) <= 0.06f;     // the doorway ends where the piece starts / starts where the piece ends
                            if (!touchesLo && !touchesHi) continue;
                            touching++;
                            // the leaf on the side of the doorway that faces this piece (in the door's frame: low leaf = low side for a wall along x, high side for a wall along z)
                            // touchesHi: the piece lies on the doorway's low side, so the leaf on that side slides into it (in the door's own frame: the low leaf for a wall along x, the high leaf for a wall along z)
                            float t = touchesHi ? (bx ? plans[i].lowTravel : plans[i].highTravel) : (bx ? plans[i].highTravel : plans[i].lowTravel);
                            if (plans[i].slide) taken += t;
                        }
                        if (touching >= 1 && taken > (hi - lo) - (touching == 2 ? 2f * DoorSlide.EndMargin + DoorSlide.LeafGap : DoorSlide.EndMargin) + 1e-3f) overlaps++;
                    }
                }
            Check($"the plans of {floors} real floors ({doors} doors): no leaf slides further than its own width", tooFar == 0);
            Check($"...and no two doors take more of a piece of wall than it has ({overlaps} overlaps)", overlaps == 0);
            Check($"...and most doors slide into the wall ({slides} of {doors}); the {swings} that cannot swing open", doors > 500 && slides >= doors * 70 / 100 && slides + swings == doors);
            Check($"...every door that swings opens into its own room, the outpost's out of it ({badSwing} wrong)", swings > 100 && badSwing == 0);
        }

        /// <summary>R14: officers see much further down a straight line of sight.</summary>
        static void SightTest()
        {
            const float range = 14f, angle = 65f, far = range * GuardRules.LongSightFactor, longHalf = GuardRules.LongSightHalfAngle;
            Check("the long sight is much further than the ordinary one", far >= range * 2.5f);
            Check("dead ahead the sight is the long range", Mathf.Approximately(GuardRules.RangeAt(0f, range, far, longHalf), far));
            Check("at the edge of the long beam and beyond it is the ordinary range", Mathf.Approximately(GuardRules.RangeAt(longHalf, range, far, longHalf), range) && Mathf.Approximately(GuardRules.RangeAt(40f, range, far, longHalf), range));
            bool monotonic = true, symmetric = true;
            float last = float.MaxValue;
            for (float d = 0f; d <= 12f; d += 0.5f)
            {
                float r = GuardRules.RangeAt(d, range, far, longHalf);
                if (r > last + 1e-4f) monotonic = false;
                if (!Mathf.Approximately(r, GuardRules.RangeAt(-d, range, far, longHalf))) symmetric = false;
                last = r;
            }
            Check("the sight falls smoothly from the middle of the beam to its edge, the same on both sides", monotonic && symmetric);
            Check("no long beam when the long range is not longer (a hiding player is seen from close by only)", Mathf.Approximately(GuardRules.RangeAt(0f, 1.5f, 1.5f, longHalf), 1.5f));
            Check("a guard made blind (a tiny range) stays blind down the hall too", !GuardRules.InVision(0f, 1f, 0f, 5f, 65f, 0.1f, 0.1f * GuardRules.LongSightFactor, longHalf));

            // a hallway: the officer faces +z
            bool Sees(float dist, float degrees) => GuardRules.InVision(0f, 1f, Mathf.Sin(degrees * Mathf.Deg2Rad) * dist, Mathf.Cos(degrees * Mathf.Deg2Rad) * dist, angle, range, far, longHalf);
            Check("a player 40 m down the hall, dead ahead, is seen", Sees(40f, 0f));
            Check("...but 46 m away is not", !Sees(46f, 0f));
            Check("...and 40 m away but 5 degrees off is not (the beam narrows with the distance)", !Sees(40f, 5f));
            Check("20 m away and 5 degrees off is seen", Sees(20f, 5f));
            Check("20 m away and 15 degrees off is not seen (only the ordinary 14 m reach there)", !Sees(20f, 15f));
            Check("12 m away and 60 degrees off is seen (the ordinary cone is as it was)", Sees(12f, 60f));
            Check("15 m away and 60 degrees off is not seen", !Sees(15f, 60f));
            Check("behind him is not seen", !Sees(5f, 120f));
            Check("somebody on top of him is seen whatever way he faces", GuardRules.InVision(0f, 1f, 0f, -0.3f, angle, range, far, longHalf));
            Check("InVision with no long beam is InCone", GuardRules.InVision(0f, 1f, 3f, 9f, angle, range, range, longHalf) == GuardRules.InCone(0f, 1f, 3f, 9f, angle, range)
                                                         && GuardRules.InVision(0f, 1f, 9f, 14f, angle, range, range, longHalf) == GuardRules.InCone(0f, 1f, 9f, 14f, angle, range));
        }
    }
}
