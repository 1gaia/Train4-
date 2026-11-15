using System;
using System.Collections.Generic;
using UnityEngine;

namespace LineFour.Oido
{
    public sealed class CurveRoute : MonoBehaviour
    {
        const float TrainLength = 205.2f;
        const float WarningDistance = 120;
        const float HalfBogieSpacing = 6.55f; // The FBX axles are 5.5 m and 7.6 m from each car's pivot.
        const string AlignmentResource = "OidoAlignment";

        static TrackPath activePath;
        public static TrackPath Path => activePath ??= CreatePath(Resources.Load<TrackAlignment>(AlignmentResource));

        static TrackPath CreatePath(TrackAlignment alignment) =>
            alignment ? alignment.Build() : new TrackPath(null);

        public static void ResetPath() => activePath = null;

        sealed class Car
        {
            public LODGroup Lod;
            public Vector3 Center;
            public Quaternion RestRotation;
            public Quaternion RouteRotation;
        }

        readonly List<Car> cars = new List<Car>();
        readonly List<Mesh> generated = new List<Mesh>();

        RailwayTrack railway;

        public static Quaternion Heading(float source) => Path.Frame(source).Rotation;

        public static Vector3 Map(Vector3 source) => Path.Frame(source.z).Point(source.x, source.y);

        public static TrackFrame AtOffset(float source, float offset) => Path.AtTravel(Path.TravelAt(source) + offset);

        public static bool TryCurveLimit(float source, out float remaining, out int limitKmh) =>
            TryCurveLimitAtTravel(Path.TravelAt(source), out remaining, out limitKmh);

        internal static bool TryCurveLimitAtTravel(float travel, out float remaining, out int limitKmh)
        {
            TrackPath path = Path;
            float toFirstCurve = path.FirstCurveStart - travel;
            if (path.FirstCurveEnd > path.FirstCurveStart && travel <= path.FirstCurveEnd + TrainLength && toFirstCurve <= WarningDistance)
            {
                remaining = toFirstCurve;
                limitKmh = 40;
                return true;
            }
            if (path.HasMappedTrack)
            {
                bool hasCurve = path.TryMappedCurve(travel, WarningDistance, TrainLength, out remaining);
                limitKmh = hasCurve ? 45 : 0;
                return hasCurve;
            }

            int first = Mathf.Max(1, TrackPath.Section(path.SourceAt(travel - TrainLength)));
            int last = Mathf.Min(RouteSchedule.Stops.Length - 2, TrackPath.Section(path.SourceAt(travel + WarningDistance)));
            for (int section = first; section <= last; section++)
            {
                TrackPath.LaterCurveBounds(section, out float start, out float end);
                start = path.TravelAt(start);
                end = path.TravelAt(end);
                if (travel > end + TrainLength || start - travel > WarningDistance) continue;
                remaining = start - travel;
                limitKmh = 45;
                return true;
            }
            remaining = 0;
            limitKmh = 0;
            return false;
        }

        public void Prepare(Line4Drive drive)
        {
            var alignment = Resources.Load<TrackAlignment>(AlignmentResource);
            activePath = CreatePath(alignment);
            var materials = alignment ? alignment.Materials : null;
            bool generateTrack = materials != null && Array.TrueForAll(materials, material => material);
            TrackFrame outbound = Path.OutboundTransform;
            Vector3 nose = drive.consist.position;
            foreach (var group in drive.consist.GetComponentsInChildren<LODGroup>())
                cars.Add(new Car { Lod = group, Center = group.transform.position - nose, RestRotation = group.transform.rotation });
            cars.Sort((a, b) => b.Center.z.CompareTo(a.Center.z));
            if (cars.Count > 0)
            {
                foreach (var renderer in drive.consist.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.name.StartsWith("Console_")) continue;
                    var car = renderer.name.Contains("4954") ? cars[cars.Count - 1] : cars[0];
                    renderer.transform.SetParent(car.Lod.transform, true);
                }
                for (int i = 0; i < cars.Count; i++)
                {
                    var riders = drive.consist.Find("Saloon occupants " + (i + 1));
                    if (riders) riders.SetParent(cars[i].Lod.transform, true);
                }
            }

            foreach (var root in drive.gameObject.scene.GetRootGameObjects())
            {
                if (root == drive.gameObject || root == drive.consist.gameObject || root == drive.cabCamera.gameObject || root.GetComponent<Light>())
                    continue;
                bool routeRoot = root.name == "Oido - Geumjeong" || root.name == "Geumjeong - Jinjeop";
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (generateTrack && routeRoot && IsLegacyTrack(filter))
                    {
                        filter.GetComponent<MeshRenderer>().enabled = false;
                        continue;
                    }
                    WarpMesh(filter);
                }
                // Preserve the authored local transforms used by screen doors and station assets.
                var t = root.transform;
                t.SetPositionAndRotation(outbound.Position + outbound.Rotation * t.position, outbound.Rotation * t.rotation);
            }
            if (generateTrack) railway = new RailwayTrack(Path, transform, materials);
            ShowTrack(nose.z, drive.cabCamera.farClipPlane);
            PlaceTrain(nose);
        }

        static bool IsLegacyTrack(MeshFilter filter) =>
            (filter.name.StartsWith("Track_", StringComparison.Ordinal) ||
             filter.name.StartsWith("Track ", StringComparison.Ordinal)) && filter.GetComponent<MeshRenderer>();

        public void ShowTrack(float source, float viewDistance) => railway?.ShowAround(source, viewDistance);

        void WarpMesh(MeshFilter filter)
        {
            var source = filter.sharedMesh;
            if (!source || !source.isReadable) return;
            if (filter.TryGetComponent<MeshRenderer>(out var renderer) && !renderer.enabled) return;
            var localToWorld = filter.transform.localToWorldMatrix;
            var worldToLocal = filter.transform.worldToLocalMatrix;
            if (Path.HasMappedTrack && (filter.name.StartsWith("Tunnel ", StringComparison.Ordinal) ||
                filter.name == "TunnelShared" || filter.name == "Open railway"))
            {
                Mesh corridor = SceneryWarp.Corridor(Path, source, localToWorld, worldToLocal);
                filter.sharedMesh = corridor;
                generated.Add(corridor);
                return;
            }
            var points = source.vertices;
            bool changed = false;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 world = localToWorld.MultiplyPoint3x4(points[i]);
                Vector3 curved = Path.SceneryPoint(world);
                if ((world - curved).sqrMagnitude > .000001f) changed = true;
                points[i] = worldToLocal.MultiplyPoint3x4(curved);
            }
            if (!changed) return;
            var mesh = Instantiate(source);
            mesh.name = source.name + " curved";
            mesh.vertices = points;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            generated.Add(mesh);
        }

        public void PlaceTrain(Vector3 nose)
        {
            float travel = Path.TravelAt(nose.z);
            foreach (var car in cars)
            {
                float center = travel + car.Center.z;
                float across = nose.x + car.Center.x;
                float height = nose.y + car.Center.y;
                Vector3 front = Path.AtTravel(center + HalfBogieSpacing).Point(across, height);
                Vector3 rear = Path.AtTravel(center - HalfBogieSpacing).Point(across, height);
                car.RouteRotation = Quaternion.LookRotation(front - rear);
                car.Lod.transform.SetPositionAndRotation((front + rear) * .5f, car.RouteRotation * car.RestRotation);
            }
        }

        public Vector3 CameraOnTrain(Vector3 nose, Vector3 eye, bool saloon, out Quaternion rotation)
        {
            Car nearest = null;
            float distance = float.MaxValue;
            foreach (var car in cars)
            {
                float gap = Mathf.Abs(eye.z - car.Center.z);
                car.Lod.ForceLOD(saloon && gap < 26 ? 0 : -1);
                if (gap >= distance) continue;
                nearest = car;
                distance = gap;
            }
            rotation = nearest != null ? nearest.RouteRotation : AtOffset(nose.z, eye.z).Rotation;
            return nearest != null ? nearest.Lod.transform.position + rotation * (eye - nearest.Center) :
                AtOffset(nose.z, eye.z).Point(nose.x + eye.x, nose.y + eye.y);
        }

        public void ResetLods()
        {
            foreach (var car in cars) car.Lod.ForceLOD(-1);
        }

        void OnDestroy()
        {
            railway?.Dispose();
            foreach (var mesh in generated) Destroy(mesh);
        }
    }
}
