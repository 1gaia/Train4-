using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LineFour.Oido
{
    // The legacy tunnel and open-line meshes have faces up to 80 authored metres long.
    // Slice them in travel metres before bending so walls and overhead lines follow the track.
    internal static class SceneryWarp
    {
        const float SliceLength = 12;

        readonly struct Vertex
        {
            internal readonly Vector3 Position;
            internal readonly Vector2 Uv;

            internal Vertex(Vector3 position, Vector2 uv)
            {
                Position = position;
                Uv = uv;
            }
        }

        internal static Mesh Corridor(TrackPath path, Mesh source, Matrix4x4 toWorld, Matrix4x4 toLocal)
        {
            var positions = source.vertices;
            var uv = source.uv;
            bool hasUv = uv.Length == positions.Length;
            var authored = new Vertex[positions.Length];
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            for (int i = 0; i < authored.Length; i++)
            {
                Vector3 world = toWorld.MultiplyPoint3x4(positions[i]);
                authored[i] = new Vertex(world, hasUv ? uv[i] : Vector2.zero);
                min = Mathf.Min(min, world.z);
                max = Mathf.Max(max, world.z);
            }

            float start = path.TravelAt(min);
            float end = path.TravelAt(max);
            var slices = new List<float>();
            for (float travel = start + SliceLength; travel < end; travel += SliceLength)
                slices.Add(path.SourceAt(travel));

            var vertices = new List<Vector3>();
            var coordinates = new List<Vector2>();
            var faces = new List<int>[source.subMeshCount];
            var polygon = new List<Vertex>(6);
            var floor = new List<Vertex>(6);
            var ceiling = new List<Vertex>(6);
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                var triangles = source.GetTriangles(submesh);
                var indices = faces[submesh] = new List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vertex a = authored[triangles[i]], b = authored[triangles[i + 1]], c = authored[triangles[i + 2]];
                    float from = Mathf.Min(a.Position.z, b.Position.z, c.Position.z);
                    float to = Mathf.Max(a.Position.z, b.Position.z, c.Position.z);
                    polygon.Clear();
                    polygon.Add(a);
                    polygon.Add(b);
                    polygon.Add(c);
                    int first = slices.BinarySearch(from);
                    if (first < 0) first = ~first;
                    for (int j = first; j < slices.Count && slices[j] < to; j++)
                    {
                        float edge = slices[j];
                        if (edge <= from) continue;
                        Clip(polygon, floor, edge, true);
                        Clip(polygon, ceiling, edge, false);
                        Emit(floor, indices, vertices, coordinates, path, toLocal);
                        (polygon, ceiling) = (ceiling, polygon);
                    }
                    Emit(polygon, indices, vertices, coordinates, path, toLocal);
                }
            }

            var mesh = new Mesh { name = source.name + " curved" };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            if (hasUv) mesh.SetUVs(0, coordinates);
            mesh.subMeshCount = faces.Length;
            for (int i = 0; i < faces.Length; i++) mesh.SetTriangles(faces[i], i, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Clip(List<Vertex> input, List<Vertex> output, float edge, bool before)
        {
            output.Clear();
            if (input.Count == 0) return;
            Vertex a = input[input.Count - 1];
            foreach (Vertex b in input)
            {
                float from = a.Position.z - edge;
                float to = b.Position.z - edge;
                if ((from < 0 && to > 0) || (from > 0 && to < 0))
                {
                    float t = from / (from - to);
                    Vector3 point = Vector3.LerpUnclamped(a.Position, b.Position, t);
                    point.z = edge;
                    output.Add(new Vertex(point, Vector2.LerpUnclamped(a.Uv, b.Uv, t)));
                }
                if (before ? to <= 0 : to >= 0) output.Add(b);
                a = b;
            }
        }

        static void Emit(List<Vertex> polygon, List<int> faces, List<Vector3> vertices, List<Vector2> coordinates,
            TrackPath path, Matrix4x4 toLocal)
        {
            if (polygon.Count < 3) return;
            int first = vertices.Count;
            foreach (Vertex vertex in polygon)
            {
                vertices.Add(toLocal.MultiplyPoint3x4(path.SceneryPoint(vertex.Position)));
                coordinates.Add(vertex.Uv);
            }
            for (int i = 1; i < polygon.Count - 1; i++)
            {
                faces.Add(first);
                faces.Add(first + i);
                faces.Add(first + i + 1);
            }
        }
    }
}
