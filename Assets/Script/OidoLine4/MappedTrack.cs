using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LineFour.Oido
{
    internal sealed class MappedTrack
    {
        const float DepartureClearance = 60;
        const float ArrivalClearance = 225;
        const float CurveSampleSpacing = 10;
        const float CurveSampleHalfWidth = 25;
        const float CurveThresholdDegrees = 2.5f;
        readonly Segment[] segments;
        readonly float[] stops;
        readonly float[] parallelStops;
        readonly List<Vector2> curves = new List<Vector2>();

        internal static MappedTrack Read(TextAsset asset, TrackFrame firstArrival, float firstLength, float firstParallelLength)
        {
            if (!asset) return null;
            var points = new List<Vector3>[RouteSchedule.Stops.Length - 1];
            using (var input = new System.IO.StringReader(asset.text))
            {
                string line;
                while ((line = input.ReadLine()) != null)
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    string[] fields = line.Split(',');
                    if (fields.Length != 3 || !int.TryParse(fields[0], out int section) ||
                        section < 1 || section >= points.Length ||
                        !float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                        !float.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) ||
                        !float.IsFinite(x) || !float.IsFinite(z))
                        throw new FormatException("Invalid record in " + asset.name + ": " + line);
                    var sectionPoints = points[section] ??= new List<Vector3>();
                    Vector3 point = firstArrival.Position + firstArrival.Rotation * new Vector3(x, 0, z);
                    if (sectionPoints.Count == 0 || (point - sectionPoints[sectionPoints.Count - 1]).sqrMagnitude > .01f)
                        sectionPoints.Add(point);
                }
            }
            for (int i = 1; i < points.Length; i++)
                if (points[i] == null || points[i].Count < 2)
                    throw new FormatException("Missing mapped section " + RouteSchedule.Names[i] + " – " + RouteSchedule.Names[i + 1]);
            return new MappedTrack(points, firstArrival, firstLength, firstParallelLength);
        }

        MappedTrack(List<Vector3>[] points, TrackFrame firstArrival, float firstLength, float firstParallelLength)
        {
            stops = new float[RouteSchedule.Stops.Length];
            parallelStops = new float[stops.Length];
            segments = new Segment[stops.Length - 1];
            stops[1] = firstLength;
            parallelStops[1] = firstParallelLength;
            TrackFrame previous = firstArrival;
            double distance = firstLength, parallelDistance = firstParallelLength;
            for (int i = 1; i < segments.Length; i++)
            {
                var knots = points[i];
                if (Vector3.Distance(knots[0], previous.Position) > .1f)
                    throw new FormatException("Mapped track endpoints do not meet at " + RouteSchedule.Names[i]);
                knots[0] = previous.Position;
                Vector3 finalDirection = (knots[knots.Count - 1] - knots[knots.Count - 2]).normalized;
                var segment = new Segment(i, knots, previous.Rotation * Vector3.forward, finalDirection);
                segments[i] = segment;
                AddCurves(segment, stops[i]);
                distance += segment.Length;
                parallelDistance += segment.ParallelLength;
                stops[i + 1] = (float)distance;
                parallelStops[i + 1] = (float)parallelDistance;
                previous = segment.Last;
            }
        }

        void AddCurves(Segment segment, float offset)
        {
            for (float at = CurveSampleHalfWidth; at < segment.Length - CurveSampleHalfWidth; at += CurveSampleSpacing)
            {
                var before = segment.Frame(at - CurveSampleHalfWidth);
                var after = segment.Frame(at + CurveSampleHalfWidth);
                if (Quaternion.Angle(before.Rotation, after.Rotation) < CurveThresholdDegrees) continue;
                float start = offset + at - CurveSampleHalfWidth;
                float end = offset + at + CurveSampleHalfWidth;
                int last = curves.Count - 1;
                if (last >= 0 && start <= curves[last].y + 40)
                    curves[last] = new Vector2(curves[last].x, end);
                else
                    curves.Add(new Vector2(start, end));
            }
        }

        internal bool TryCurve(float travel, float warningDistance, float trainLength, out float remaining)
        {
            int low = 0, high = curves.Count;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (curves[mid].y + trainLength < travel) low = mid + 1;
                else high = mid;
            }
            remaining = low < curves.Count ? curves[low].x - travel : float.PositiveInfinity;
            return remaining <= warningDistance;
        }

        internal float TravelAt(float source, bool parallel)
        {
            float[] stationDistances = parallel ? parallelStops : stops;
            int section = TrackPath.Section(source);
            if (section >= segments.Length) return stationDistances[section] + source - RouteSchedule.Stops[section];
            return stationDistances[section] + segments[section].TravelAt(source, parallel);
        }

        internal float SourceAt(float distance, bool parallel)
        {
            float[] stationDistances = parallel ? parallelStops : stops;
            int section = FindStation(stationDistances, distance);
            if (section >= segments.Length) return RouteSchedule.Stops[section] + distance - stationDistances[section];
            return segments[section].SourceAt(distance - stationDistances[section], parallel);
        }

        internal TrackFrame Frame(float source)
        {
            int section = Mathf.Min(TrackPath.Section(source), segments.Length - 1);
            var segment = segments[section];
            return segment.Frame(segment.TravelAt(source, false));
        }

        internal TrackFrame AtTravel(float distance, bool parallel)
        {
            float[] stationDistances = parallel ? parallelStops : stops;
            int section = Mathf.Min(FindStation(stationDistances, distance), segments.Length - 1);
            var segment = segments[section];
            return segment.Frame(segment.MainAt(distance - stationDistances[section], parallel));
        }

        static int FindStation(float[] stationDistances, float distance)
        {
            int index = Array.BinarySearch(stationDistances, distance);
            return index >= 0 ? index : ~index - 1;
        }

        sealed class Segment
        {
            readonly float[] distance, parallelDistance;
            readonly TrackFrame[] frames;
            readonly float sourceStart, sourceLength;
            readonly float stretchEnd;
            internal float Length => distance[distance.Length - 1];
            internal float ParallelLength => parallelDistance[parallelDistance.Length - 1];
            internal TrackFrame Last => frames[frames.Length - 1];

            internal Segment(int section, List<Vector3> knots, Vector3 initialDirection, Vector3 finalDirection)
            {
                sourceStart = RouteSchedule.Stops[section];
                sourceLength = RouteSchedule.Stops[section + 1] - sourceStart;
                stretchEnd = sourceLength - ArrivalClearance;
                TrackPath.SampleSpline(knots, initialDirection, finalDirection, false, out distance, out frames);
                parallelDistance = TrackPath.ParallelDistances(frames);
            }

            float ToTravel(float source)
            {
                if (Length < sourceLength) return source * (Length / sourceLength);
                float t = Mathf.InverseLerp(DepartureClearance, stretchEnd, source);
                return source + (Length - sourceLength) * t * t * (3 - 2 * t);
            }

            internal float TravelAt(float source, bool parallel)
            {
                float length = ToTravel(source - sourceStart);
                if (!parallel) return length;
                if (length >= Length) return ParallelLength + length - Length;
                return TrackPath.InterpolateDistance(length, distance, parallelDistance);
            }

            internal float MainAt(float travel, bool parallel)
            {
                if (!parallel) return travel;
                if (travel >= ParallelLength) return Length + travel - ParallelLength;
                return TrackPath.InterpolateDistance(travel, parallelDistance, distance);
            }

            internal float SourceAt(float travel, bool parallel)
            {
                float physical = MainAt(travel, parallel);
                if (Length < sourceLength) return sourceStart + physical * (sourceLength / Length);
                if (physical <= DepartureClearance) return sourceStart + physical;
                float extra = Length - sourceLength;
                if (physical >= stretchEnd + extra) return sourceStart + physical - extra;
                float low = DepartureClearance, high = stretchEnd;
                for (int i = 0; i < 23; i++)
                {
                    float mid = (low + high) * .5f;
                    if (ToTravel(mid) < physical) low = mid;
                    else high = mid;
                }
                return sourceStart + (low + high) * .5f;
            }

            internal TrackFrame Frame(float travel)
            {
                if (travel >= Length) return new TrackFrame(Last.Position + Last.Rotation * Vector3.forward * (travel - Length), Last.Rotation);
                return TrackPath.InterpolateFrame(travel, distance, frames);
            }
        }
    }
}
