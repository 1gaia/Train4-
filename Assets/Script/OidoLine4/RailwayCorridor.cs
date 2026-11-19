using System.Collections.Generic;
using UnityEngine;

namespace LineFour.Oido
{
    internal static class RailwayCorridor
    {
        const float SampleStep = 3;
        const float PoleSpacing = 40;
        static readonly Vector2[] Ground = Rectangle(-170, 170, -1.39f, -.68f);
        static readonly Vector2[] TunnelFloor = Rectangle(-11, 16.3f, -.85f, -.66f);
        static readonly Vector2[] TunnelLeft = Rectangle(-11, -10.58f, -.66f, 7.8f);
        static readonly Vector2[] TunnelRight = Rectangle(15.88f, 16.3f, -.66f, 7.8f);
        static readonly Vector2[] TunnelRoof = Rectangle(-11, 16.3f, 7.5f, 7.8f);
        static readonly Vector2[] Deck = Rectangle(-11, 16.3f, -1.35f, -.66f);

        static Vector2[] Rectangle(float left, float right, float bottom, float top) => new[] {
            new Vector2(left, bottom), new Vector2(right, bottom),
            new Vector2(right, top), new Vector2(left, top)
        };

        internal static Mesh Build(TrackPath path, float from, float to)
        {
            var builder = new TrackMesh.Builder(path.AtTravel(from).Position);
            var boundaries = new List<float> { from };
            boundaries.AddRange(path.Structures.Boundaries(from, to));
            boundaries.Add(to);
            for (int i = 1; i < boundaries.Count; i++)
            {
                float start = boundaries[i - 1], end = boundaries[i];
                var kind = path.Structures.AtTravel((start + end) * .5f);
                int steps = Mathf.Max(1, Mathf.CeilToInt((end - start) / SampleStep));
                var frames = new TrackFrame[steps + 1];
                for (int sample = 0; sample <= steps; sample++)
                    frames[sample] = path.AtTravel(Mathf.Lerp(start, end, (float)sample / steps));
                if (kind == TrackStructure.Tunnel)
                {
                    builder.Sweep(frames, TunnelFloor, 0, 0);
                    builder.Sweep(frames, TunnelLeft, 0, 0);
                    builder.Sweep(frames, TunnelRight, 0, 0);
                    builder.Sweep(frames, TunnelRoof, 0, 0);
                    foreach (float side in new[] { -10.24f, 15.54f })
                    {
                        builder.Sweep(frames, Rectangle(side - .12f, side + .12f, 1.6f, 1.69f), 0, 2);
                        builder.Sweep(frames, Rectangle(side - .12f, side + .12f, 2.15f, 2.24f), 0, 2);
                    }
                }
                else if (kind == TrackStructure.Bridge)
                {
                    builder.Sweep(frames, Deck, 0, 0);
                    foreach (float side in new[] { -10.8f, 16.1f })
                    {
                        builder.Sweep(frames, Rectangle(side - .12f, side + .12f, -.66f, .45f), 0, 0);
                        builder.Sweep(frames, Rectangle(side - .035f, side + .035f, 1.10f, 1.16f), 0, 2);
                    }
                }
                else
                {
                    builder.Sweep(frames, Ground, 0, 0);
                    foreach (float side in new[] { -12f, 17f })
                    {
                        builder.Sweep(frames, Rectangle(side - .13f, side + .13f, -.68f, .22f), 0, 0);
                        builder.Sweep(frames, Rectangle(side - .035f, side + .035f, 1.12f, 1.18f), 0, 2);
                    }
                }
                foreach (float wire in new[] { 0f, TrackPath.ParallelOffset })
                    builder.Sweep(frames, Rectangle(wire - .018f, wire + .018f, 5.65f, 5.69f), 0, 2);
            }

            int first = Mathf.CeilToInt(from / PoleSpacing);
            int last = Mathf.CeilToInt(to / PoleSpacing);
            for (int i = first; i < last; i++)
            {
                float distance = i * PoleSpacing;
                var kind = path.Structures.AtTravel(distance);
                var frame = path.AtTravel(distance);
                if (kind == TrackStructure.Tunnel) continue;
                if (AtPlatform(path, distance)) continue;
                float pole = kind == TrackStructure.Bridge ? 8.5f : 10.2f;
                builder.Box(frame, new Vector3(pole, 3.3f, 0), new Vector3(.20f, 7.9f, .20f), 2);
                builder.Box(frame, new Vector3((pole - 1.4f) * .5f, 6.75f, 0),
                    new Vector3(pole + 1.4f, .16f, .18f), 2);
                if (kind == TrackStructure.Bridge)
                {
                    builder.Box(frame, new Vector3(2.65f, -1.60f, 0), new Vector3(10.8f, .5f, 2.3f), 0);
                    builder.Box(frame, new Vector3(2.65f, -5.5f, 0), new Vector3(1.5f, 7.5f, 1.5f), 0);
                }
            }
            var mesh = builder.Finish();
            mesh.name = "Mapped tunnel bridge and trackside";
            return mesh;
        }

        static bool AtPlatform(TrackPath path, float distance)
        {
            int section = Mathf.Max(0, TrackPath.Section(path.SourceAt(distance)));
            int next = Mathf.Min(section + 1, RouteSchedule.Stops.Length - 1);
            float after = distance - path.TravelAt(RouteSchedule.Stops[section]);
            float before = path.TravelAt(RouteSchedule.Stops[next]) - distance;
            return after < 35 || before < 235;
        }
    }
}
