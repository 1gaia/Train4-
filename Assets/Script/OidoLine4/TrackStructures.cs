using System;
using System.Collections.Generic;
using UnityEngine;

namespace LineFour.Oido
{
    public enum TrackStructure { Ground, Tunnel, Bridge }

    public sealed class TrackStructures
    {
        [Serializable]
        sealed class Data
        {
            public Range[] ranges;
        }

        [Serializable]
        sealed class Range
        {
            public int section;
            public float start, end;
            public TrackStructure kind;
        }

        internal readonly struct Span
        {
            internal readonly float Start, End;
            internal readonly TrackStructure Kind;

            internal Span(float start, float end, TrackStructure kind)
            {
                Start = start;
                End = end;
                Kind = kind;
            }
        }

        readonly Span[] spans;
        public bool HasMapData { get; }
        internal IReadOnlyList<Span> Spans => spans;

        internal TrackStructures(TrackPath path)
        {
            var asset = path.HasMappedTrack ? Resources.Load<TextAsset>("Line4Structures") : null;
            var ranges = new List<Span>();
            if (asset)
            {
                var data = JsonUtility.FromJson<Data>(asset.text);
                if (data?.ranges == null) throw new FormatException("Missing Line4Structures ranges");
                foreach (var range in data.ranges)
                {
                    if (range.section < 0 || range.section >= RouteSchedule.Stops.Length - 1 ||
                        !float.IsFinite(range.start) || !float.IsFinite(range.end) ||
                        range.start < 0 || range.end > 1 || range.start >= range.end ||
                        (range.kind != TrackStructure.Tunnel && range.kind != TrackStructure.Bridge))
                        throw new FormatException("Invalid mapped structure at section " + range.section);
                    float from = path.TravelAt(RouteSchedule.Stops[range.section]);
                    float to = path.TravelAt(RouteSchedule.Stops[range.section + 1]);
                    Add(ranges, new Span(Mathf.Lerp(from, to, range.start), Mathf.Lerp(from, to, range.end), range.kind));
                }
                HasMapData = true;
            }
            else
            {
                for (int i = 0; i < RouteSchedule.Stops.Length - 1; i++)
                    if (RouteSchedule.Tunnel(i + 1))
                        Add(ranges, new Span(path.TravelAt(RouteSchedule.Stops[i]),
                            path.TravelAt(RouteSchedule.Stops[i + 1]), TrackStructure.Tunnel));
            }
            if (ranges.Count > 0)
            {
                int last = ranges.Count - 1;
                float terminal = path.TravelAt(RouteSchedule.Stops[RouteSchedule.Stops.Length - 1]);
                if (Mathf.Abs(ranges[last].End - terminal) < .1f)
                    ranges[last] = new Span(ranges[last].Start, path.TravelAt(TrackPath.SourceEnd), ranges[last].Kind);
            }
            spans = ranges.ToArray();
        }

        static void Add(List<Span> spans, Span span)
        {
            int last = spans.Count - 1;
            if (last >= 0)
            {
                if (span.Start < spans[last].End - .01f) throw new FormatException("Mapped structures overlap");
                if (span.Kind == spans[last].Kind && span.Start <= spans[last].End + .01f)
                {
                    spans[last] = new Span(spans[last].Start, span.End, span.Kind);
                    return;
                }
            }
            spans.Add(span);
        }

        public TrackStructure AtTravel(float travel)
        {
            int low = 0, high = spans.Length;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (spans[mid].Start <= travel) low = mid + 1;
                else high = mid;
            }
            int index = low - 1;
            return index >= 0 && travel < spans[index].End ? spans[index].Kind : TrackStructure.Ground;
        }

        internal IEnumerable<float> Boundaries(float from, float to)
        {
            foreach (Span span in spans)
            {
                if (span.Start >= to) yield break;
                if (span.Start > from) yield return span.Start;
                if (span.End > from && span.End < to) yield return span.End;
            }
        }
    }
}
