using System;
using System.Collections.Generic;
using UnityEngine;

namespace LineFour.Oido
{
    public sealed class TrackAlignment : ScriptableObject
    {
        [Tooltip("Oido to Jeongwang in local metres. The first point is Oido and +Z is the departure direction.")]
        [SerializeField] Vector3[] knots = {
            Vector3.zero, new Vector3(0, 0, 182), new Vector3(-280, 0, 780),
            new Vector3(-620, 0, 832), new Vector3(-839, 0, 835)
        };
        [SerializeField, Min(.3f)] float sleeperSpacing = .6f;
        [SerializeField] TextAsset mappedTrack;
        [SerializeField] Material foundation;
        [SerializeField] Material ballast;
        [SerializeField] Material rail;
        [SerializeField] Material sleeper;

        public TrackPath Build() => new TrackPath(knots, Mathf.Max(.3f, sleeperSpacing), mappedTrack);
        internal Material[] Materials => new[] { foundation, ballast, rail, sleeper };
    }

    public readonly struct TrackFrame
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public TrackFrame(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Point(float across, float height) => Position + Rotation * new Vector3(across, height, 0);
    }

    public sealed class TrackPath
    {
        public const float ParallelOffset = 5.3f;
        public const float SourceStart = -260;
        public static float SourceEnd => RouteSchedule.Stops[RouteSchedule.Stops.Length - 1] + 150;
        const float SampleStep = .5f;
        const float StretchStart = 60;
        const float StretchEnd = 390;
        readonly float[] approachDistance;
        readonly float[] approachParallelDistance;
        readonly TrackFrame[] approachFrames;
        readonly float[] mainDistances;
        readonly float[] parallelDistances;
        readonly TrackFrame arrival;
        readonly Quaternion inverseArrival;
        readonly MappedTrack mapped;
        public bool HasMappedTrack => mapped != null;
        public float FirstLength => approachDistance[approachDistance.Length - 1];
        public float FirstCurveStart { get; }
        public float FirstCurveEnd { get; }
        public float SleeperSpacing { get; }

        public TrackPath(Vector3[] points, float sleeperSpacing = .6f, TextAsset mappedTrack = null)
        {
            SleeperSpacing = Mathf.Max(.3f, sleeperSpacing);
            var knots = new List<Vector3> { Vector3.zero };
            if (points != null)
            {
                for (int i = 1; i < points.Length; i++)
                {
                    Vector3 point = points[i] - points[0];
                    if ((point - knots[knots.Count - 1]).sqrMagnitude > .01f) knots.Add(point);
                }
            }
            if (knots.Count == 1) knots.Add(Vector3.forward * RouteSchedule.Stops[1]);

            Vector3 direction = knots[knots.Count - 1] - knots[knots.Count - 2];
            bool level = Mathf.Abs(direction.y) < .001f;
            direction.y = 0;
            SampleSpline(knots, Vector3.forward, direction.sqrMagnitude > .01f ? direction.normalized : Vector3.forward, level,
                out approachDistance, out approachFrames);
            approachParallelDistance = ParallelDistances(approachFrames);
            double parallelLength = approachParallelDistance[approachParallelDistance.Length - 1];
            arrival = approachFrames[approachFrames.Length - 1];
            inverseArrival = Quaternion.Inverse(arrival.Rotation);
            FirstCurveStart = FindTurn(false);
            FirstCurveEnd = FindTurn(true);
            try
            {
                mapped = MappedTrack.Read(mappedTrack, arrival, FirstLength, (float)parallelLength);
            }
            catch (FormatException error)
            {
                Debug.LogWarning(error.Message);
            }
            if (mapped != null) return;

            float firstStop = RouteSchedule.Stops[1];
            int samples = Mathf.RoundToInt((SourceEnd - firstStop) / SampleStep) + 1;
            mainDistances = new float[samples];
            parallelDistances = new float[samples];
            mainDistances[0] = FirstLength;
            parallelDistances[0] = (float)parallelLength;
            double mainLength = FirstLength;
            TrackFrame previous = Frame(firstStop);
            for (int i = 1; i < samples; i++)
            {
                TrackFrame current = Frame(firstStop + i * SampleStep);
                mainLength += Vector3.Distance(previous.Position, current.Position);
                parallelLength += Vector3.Distance(previous.Point(ParallelOffset, 0), current.Point(ParallelOffset, 0));
                mainDistances[i] = (float)mainLength;
                parallelDistances[i] = (float)parallelLength;
                previous = current;
            }
        }

        internal static float[] ParallelDistances(TrackFrame[] frames)
        {
            var result = new float[frames.Length];
            double distance = 0;
            for (int i = 1; i < frames.Length; i++)
            {
                distance += Vector3.Distance(frames[i - 1].Point(ParallelOffset, 0), frames[i].Point(ParallelOffset, 0));
                result[i] = (float)distance;
            }
            return result;
        }

        internal static void SampleSpline(IReadOnlyList<Vector3> points, Vector3 initialDirection, Vector3 finalDirection,
            bool straightArrival, out float[] distances, out TrackFrame[] frames)
        {
            var directions = new Vector3[points.Count];
            directions[0] = initialDirection;
            for (int i = 1; i < points.Count - 1; i++)
                directions[i] = ((points[i] - points[i - 1]).normalized + (points[i + 1] - points[i]).normalized).normalized;
            directions[points.Count - 1] = finalDirection;
            if (straightArrival && points.Count > 2) directions[points.Count - 2] = finalDirection;

            var length = new List<float> { 0 };
            var samples = new List<TrackFrame> { new TrackFrame(points[0], Quaternion.LookRotation(initialDirection)) };
            double accumulated = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 start = points[i];
                Vector3 end = points[i + 1];
                float chord = Vector3.Distance(start, end);
                Vector3 a = 2 * (start - end) + chord * (directions[i] + directions[i + 1]);
                Vector3 b = 3 * (end - start) - chord * (2 * directions[i] + directions[i + 1]);
                Vector3 c = chord * directions[i];
                int steps = Mathf.Max(4, Mathf.CeilToInt(chord / SampleStep));
                for (int step = 1; step <= steps; step++)
                {
                    float t = (float)step / steps;
                    Vector3 point = step == steps ? end : ((a * t + b) * t + c) * t + start;
                    Vector3 tangent = step == steps ? directions[i + 1] : (3 * a * t + 2 * b) * t + c;
                    accumulated += Vector3.Distance(samples[samples.Count - 1].Position, point);
                    samples.Add(new TrackFrame(point, Quaternion.LookRotation(tangent)));
                    length.Add((float)accumulated);
                }
            }
            frames = samples.ToArray();
            distances = length.ToArray();
        }

        float FindTurn(bool fromEnd)
        {
            int first = fromEnd ? approachFrames.Length - 1 : 0;
            int end = fromEnd ? -1 : approachFrames.Length;
            int step = fromEnd ? -1 : 1;
            Vector3 straight = approachFrames[first].Rotation * Vector3.forward;
            for (int i = first; i != end; i += step)
            {
                Vector3 forward = approachFrames[i].Rotation * Vector3.forward;
                if (Vector2.Angle(new Vector2(straight.x, straight.z), new Vector2(forward.x, forward.z)) > 2)
                    return approachDistance[i];
            }
            return 0;
        }

        public TrackFrame ApproachFrame(float distance) => InterpolateFrame(distance, approachDistance, approachFrames);

        internal static TrackFrame InterpolateFrame(float distance, float[] distances, TrackFrame[] frames)
        {
            int upper = Array.BinarySearch(distances, distance);
            if (upper >= 0) return frames[upper];
            upper = Mathf.Clamp(~upper, 1, frames.Length - 1);
            int lower = upper - 1;
            float t = Mathf.InverseLerp(distances[lower], distances[upper], distance);
            return new TrackFrame(Vector3.Lerp(frames[lower].Position, frames[upper].Position, t),
                Quaternion.Slerp(frames[lower].Rotation, frames[upper].Rotation, t));
        }

        public TrackFrame Frame(float source)
        {
            if (source <= 0) return new TrackFrame(Vector3.forward * source, Quaternion.identity);
            float firstStop = RouteSchedule.Stops[1];
            if (source < firstStop) return ApproachFrame(FirstTravel(source));
            if (mapped != null) return mapped.Frame(source);
            TrackFrame outbound = OutboundFrame(source);
            return new TrackFrame(arrival.Position + arrival.Rotation * (outbound.Position - Vector3.forward * firstStop),
                arrival.Rotation * outbound.Rotation);
        }

        static TrackFrame OutboundFrame(float source)
        {
            float offset = SectionOffset(source);
            float slope = (SectionOffset(source + .25f) - SectionOffset(source - .25f)) * 2;
            return new TrackFrame(new Vector3(offset, 0, source), Quaternion.LookRotation(new Vector3(slope, 0, 1)));
        }

        static float SectionOffset(float source)
        {
            int section = Section(source);
            if (section < 1 || section >= RouteSchedule.Stops.Length - 1) return 0;
            LaterCurveBounds(section, out float start, out float end);
            if (source <= start || source >= end) return 0;
            float length = end - start;
            float wave = Mathf.Sin((source - start) / length * Mathf.PI);
            return (section % 2 == 0 ? 1 : -1) * Mathf.Min(24, length * length / 5000) * wave * wave * wave * wave;
        }

        public TrackFrame OutboundTransform =>
            new TrackFrame(arrival.Position - arrival.Rotation * Vector3.forward * RouteSchedule.Stops[1], arrival.Rotation);

        internal Vector3 SceneryPoint(Vector3 original)
        {
            if (mapped == null && original.z >= RouteSchedule.Stops[1]) return OutboundFrame(original.z).Point(original.x, original.y);
            return inverseArrival * (Frame(original.z).Point(original.x, original.y) - OutboundTransform.Position);
        }

        internal static int Section(float source)
        {
            int index = Array.BinarySearch(RouteSchedule.Stops, source);
            return index >= 0 ? index : ~index - 1;
        }

        internal static void LaterCurveBounds(int section, out float start, out float end)
        {
            start = RouteSchedule.Stops[section] + 100;
            end = RouteSchedule.Stops[section + 1] - 260;
        }

        float FirstTravel(float source)
        {
            float firstStop = RouteSchedule.Stops[1];
            if (FirstLength < firstStop) return FirstLength * (source / firstStop);
            float t = Mathf.InverseLerp(StretchStart, StretchEnd, source);
            return source + (FirstLength - firstStop) * t * t * (3 - 2 * t);
        }

        float FirstSource(float travel)
        {
            float firstStop = RouteSchedule.Stops[1];
            if (FirstLength < firstStop) return firstStop * (travel / FirstLength);
            if (travel <= StretchStart) return travel;
            float extra = FirstLength - firstStop;
            if (travel >= StretchEnd + extra) return travel - extra;
            // The smooth stretch is monotonic and leaves both platform approaches at unit scale.
            float low = StretchStart, high = StretchEnd;
            for (int i = 0; i < 23; i++)
            {
                float mid = (low + high) * .5f;
                if (FirstTravel(mid) < travel) low = mid;
                else high = mid;
            }
            return (low + high) * .5f;
        }

        internal static float InterpolateDistance(float distance, float[] from, float[] to)
        {
            int upper = Array.BinarySearch(from, distance);
            if (upper >= 0) return to[upper];
            upper = Mathf.Clamp(~upper, 1, from.Length - 1);
            return Mathf.Lerp(to[upper - 1], to[upper], Mathf.InverseLerp(from[upper - 1], from[upper], distance));
        }

        public float TravelAt(float source, bool parallel = false)
        {
            if (source <= 0) return source;
            float firstStop = RouteSchedule.Stops[1];
            if (source < firstStop)
            {
                float travel = FirstTravel(source);
                return parallel ? InterpolateDistance(travel, approachDistance, approachParallelDistance) : travel;
            }
            if (mapped != null) return mapped.TravelAt(source, parallel);
            float[] distances = parallel ? parallelDistances : mainDistances;
            if (source >= SourceEnd) return distances[distances.Length - 1] + source - SourceEnd;
            float index = (source - firstStop) / SampleStep;
            int i = (int)index;
            return Mathf.Lerp(distances[i], distances[i + 1], index - i);
        }

        public float SourceAt(float travel, bool parallel = false)
        {
            if (travel <= 0) return travel;
            float firstLength = parallel ? approachParallelDistance[approachParallelDistance.Length - 1] : FirstLength;
            if (travel < firstLength) return FirstSource(parallel
                ? InterpolateDistance(travel, approachParallelDistance, approachDistance) : travel);
            if (mapped != null) return mapped.SourceAt(travel, parallel);
            float[] distances = parallel ? parallelDistances : mainDistances;
            if (travel >= distances[distances.Length - 1]) return SourceEnd + travel - distances[distances.Length - 1];
            int upper = Array.BinarySearch(distances, travel);
            float firstStop = RouteSchedule.Stops[1];
            if (upper >= 0) return firstStop + upper * SampleStep;
            upper = ~upper;
            float fraction = Mathf.InverseLerp(distances[upper - 1], distances[upper], travel);
            return firstStop + (upper - 1 + fraction) * SampleStep;
        }

        public TrackFrame AtTravel(float travel, bool parallel = false)
        {
            TrackFrame frame;
            if (travel > 0 && travel < (parallel ? approachParallelDistance[approachParallelDistance.Length - 1] : FirstLength))
                frame = ApproachFrame(parallel ? InterpolateDistance(travel, approachParallelDistance, approachDistance) : travel);
            else if (mapped != null && travel >= (parallel ? approachParallelDistance[approachParallelDistance.Length - 1] : FirstLength))
                frame = mapped.AtTravel(travel, parallel);
            else
                frame = Frame(SourceAt(travel, parallel));
            return parallel ? new TrackFrame(frame.Point(ParallelOffset, 0), frame.Rotation) : frame;
        }

        internal bool TryMappedCurve(float travel, float warningDistance, float trainLength, out float remaining)
        {
            remaining = 0;
            return mapped != null && mapped.TryCurve(travel, warningDistance, trainLength, out remaining);
        }
    }
}
