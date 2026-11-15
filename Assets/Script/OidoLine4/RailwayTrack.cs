using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LineFour.Oido
{
    internal sealed class RailwayTrack : IDisposable
    {
        const float ChunkLength = 90;
        readonly TrackPath path;
        readonly Transform root;
        readonly Material[] materials;
        readonly Dictionary<int, MeshFilter> chunks = new Dictionary<int, MeshFilter>();
        readonly List<int> expired = new List<int>();
        readonly float start, end;
        int previousFirst = -1, previousLast = -1;

        internal RailwayTrack(TrackPath path, Transform parent, Material[] materials)
        {
            this.path = path;
            this.materials = materials;
            root = new GameObject("Generated double track").transform;
            root.SetParent(parent, false);
            start = path.TravelAt(TrackPath.SourceStart);
            end = path.TravelAt(TrackPath.SourceEnd);
        }

        internal void ShowAround(float source, float cameraRange)
        {
            float travel = path.TravelAt(source);
            int first = Mathf.Max(0, Mathf.FloorToInt((travel - cameraRange - 240 - start) / ChunkLength));
            int last = Mathf.Min(Mathf.CeilToInt((end - start) / ChunkLength) - 1,
                Mathf.FloorToInt((travel + cameraRange + 120 - start) / ChunkLength));
            if (first == previousFirst && last == previousLast) return;
            previousFirst = first;
            previousLast = last;

            expired.Clear();
            foreach (var entry in chunks)
                if (entry.Key < first || entry.Key > last) expired.Add(entry.Key);
            foreach (int number in expired)
            {
                Release(chunks[number]);
                chunks.Remove(number);
            }

            for (int number = first; number <= last; number++)
            {
                if (chunks.ContainsKey(number)) continue;
                float from = start + number * ChunkLength;
                float to = Mathf.Min(end, from + ChunkLength);
                var chunk = new GameObject("Track chunk " + number, typeof(MeshFilter), typeof(MeshRenderer));
                chunk.transform.SetParent(root, false);
                var filter = chunk.GetComponent<MeshFilter>();
                var renderer = chunk.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                filter.sharedMesh = TrackMesh.Build(path, from, to, out Vector3 origin);
                chunk.transform.position = origin;
                filter.sharedMesh.UploadMeshData(true);
                chunks.Add(number, filter);
            }
        }

        static void Release(MeshFilter filter)
        {
            if (!filter) return;
            UnityEngine.Object.Destroy(filter.sharedMesh);
            UnityEngine.Object.Destroy(filter.gameObject);
        }

        public void Dispose()
        {
            foreach (var chunk in chunks.Values) Release(chunk);
            chunks.Clear();
            if (root) UnityEngine.Object.Destroy(root.gameObject);
        }
    }

    public static class TrackMesh
    {
        public const float Gauge = 1.435f;
        public const float HeadHalfWidth = .0355f;
        public const float RailCenter = Gauge * .5f + HeadHalfWidth;
        public const float RailSeat = .065f;
        public const float RailTop = .2275f;
        public const float SleeperWidth = 2.43f;
        public const float SleeperDepth = .255f;
        public const float SleeperBottom = -.105f;
        const float SweepStep = .75f;

        static readonly Vector2[] RailProfile = {
            new Vector2(-.0685f, RailSeat), new Vector2(.0685f, RailSeat),
            new Vector2(.0685f, .088f), new Vector2(.012f, .111f),
            new Vector2(.012f, .181f), new Vector2(HeadHalfWidth, .193f),
            new Vector2(HeadHalfWidth, RailTop), new Vector2(-HeadHalfWidth, RailTop),
            new Vector2(-HeadHalfWidth, .193f), new Vector2(-.012f, .181f),
            new Vector2(-.012f, .111f), new Vector2(-.0685f, .088f)
        };
        static readonly Vector2[] FoundationProfile = {
            new Vector2(-3.65f, -.66f), new Vector2(8.95f, -.66f),
            new Vector2(8.95f, -.30f), new Vector2(-3.65f, -.30f)
        };
        static readonly Vector2[] BallastProfile = {
            new Vector2(-1.61f, -.33f), new Vector2(6.91f, -.33f),
            new Vector2(6.68f, -.095f), new Vector2(-1.38f, -.095f)
        };
        static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };

        // Half-open ranges keep a single continuous sleeper grid across chunk boundaries.
        public static void SleeperRange(TrackPath path, float from, float to, bool parallel, out int first, out int end)
        {
            if (parallel)
            {
                from = path.TravelAt(path.SourceAt(from), true);
                to = path.TravelAt(path.SourceAt(to), true);
            }
            first = Mathf.CeilToInt(from / path.SleeperSpacing);
            end = Mathf.CeilToInt(to / path.SleeperSpacing);
        }

        public static Mesh Build(TrackPath path, float from, float to, out Vector3 origin)
        {
            origin = path.AtTravel(from).Position;
            var builder = new Builder(origin);
            int steps = Mathf.Max(1, Mathf.CeilToInt((to - from) / SweepStep));
            var frames = new TrackFrame[steps + 1];
            for (int i = 0; i <= steps; i++) frames[i] = path.AtTravel(Mathf.Lerp(from, to, (float)i / steps));
            builder.Sweep(frames, FoundationProfile, 0, 0);
            builder.Sweep(frames, BallastProfile, 0, 1);
            for (int track = 0; track < 2; track++)
            {
                float offset = track * TrackPath.ParallelOffset;
                builder.Sweep(frames, RailProfile, offset - RailCenter, 2);
                builder.Sweep(frames, RailProfile, offset + RailCenter, 2);
                bool parallel = track == 1;
                SleeperRange(path, from, to, parallel, out int first, out int end);
                for (int index = first; index < end; index++)
                    builder.Sleeper(path.AtTravel(index * path.SleeperSpacing, parallel));
            }
            return builder.Finish();
        }

        sealed class Builder
        {
            readonly Vector3 origin;
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<int>[] faces = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

            internal Builder(Vector3 origin) => this.origin = origin;

            internal void Sweep(TrackFrame[] frames, Vector2[] profile, float offset, int material)
            {
                for (int side = 0; side < profile.Length; side++)
                {
                    Vector2 a = profile[side], b = profile[(side + 1) % profile.Length];
                    Vector2 edge = b - a;
                    Vector3 normal = new Vector3(edge.y, -edge.x, 0).normalized;
                    int start = vertices.Count;
                    for (int row = 0; row < frames.Length; row++)
                    {
                        TrackFrame frame = frames[row];
                        vertices.Add(frame.Point(offset + a.x, a.y) - origin);
                        vertices.Add(frame.Point(offset + b.x, b.y) - origin);
                        Vector3 n = frame.Rotation * normal;
                        normals.Add(n);
                        normals.Add(n);
                        if (row > 0)
                        {
                            int i = start + (row - 1) * 2;
                            Quad(faces[material], i, i + 1, i + 3, i + 2);
                        }
                    }
                }
            }

            internal void Sleeper(TrackFrame frame)
            {
                Vector3 half = new Vector3(SleeperWidth, RailSeat - SleeperBottom, SleeperDepth) * .5f;
                Vector3 center = frame.Point(0, (RailSeat + SleeperBottom) * .5f) - origin;
                for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 n = frame.Rotation * Axes[axis] * sign;
                    Vector3 face = center + n * half[axis];
                    Vector3 u = frame.Rotation * Axes[(axis + 1) % 3] * half[(axis + 1) % 3];
                    Vector3 v = frame.Rotation * Axes[(axis + 2) % 3] * (half[(axis + 2) % 3] * sign);
                    int i = vertices.Count;
                    vertices.Add(face - u - v);
                    vertices.Add(face + u - v);
                    vertices.Add(face + u + v);
                    vertices.Add(face - u + v);
                    for (int j = 0; j < 4; j++) normals.Add(n);
                    Quad(faces[3], i, i + 1, i + 2, i + 3);
                }
            }

            static void Quad(List<int> indices, int a, int b, int c, int d)
            {
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
                indices.Add(a);
                indices.Add(c);
                indices.Add(d);
            }

            internal Mesh Finish()
            {
                var mesh = new Mesh { name = "Standard gauge double track" };
                if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.subMeshCount = faces.Length;
                for (int i = 0; i < faces.Length; i++) mesh.SetTriangles(faces[i], i, false);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
