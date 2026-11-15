using UnityEditor;
using UnityEngine;

namespace LineFour.Oido.EditorTools
{
    [CustomEditor(typeof(TrackAlignment))]
    sealed class TrackAlignmentEditor : Editor
    {
        Vector3[] railLines;
        Vector3[] sleeperLines;
        bool editInScene;
        int selectedKnot = -1;

        void OnEnable()
        {
            SceneView.duringSceneGui += DrawPreview;
            Undo.undoRedoPerformed += Refresh;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= DrawPreview;
            Undo.undoRedoPerformed -= Refresh;
        }

        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (DrawDefaultInspector()) Refresh();
                if (GUILayout.Toggle(editInScene, "Edit route in Scene view", "Button") != editInScene)
                {
                    editInScene = !editInScene;
                    Refresh();
                }
                if (editInScene && GUILayout.Button("Frame Oido–Jeongwang"))
                {
                    var path = ((TrackAlignment)target).Build();
                    var bounds = new Bounds(Vector3.zero, Vector3.one);
                    for (float s = 0; s < path.FirstLength; s += 10)
                        bounds.Encapsulate(path.ApproachFrame(s).Position);
                    bounds.Encapsulate(path.ApproachFrame(path.FirstLength).Position);
                    var view = SceneView.lastActiveSceneView;
                    if (view) view.LookAt(bounds.center, Quaternion.Euler(90, 0, 0),
                        Mathf.Max(bounds.size.x, bounds.size.z) * .69f, true);
                }
            }
        }

        void Refresh()
        {
            railLines = sleeperLines = null;
            CurveRoute.ResetPath();
            SceneView.RepaintAll();
        }

        void DrawPreview(SceneView view)
        {
            if (!editInScene || EditorApplication.isPlayingOrWillChangePlaymode || !target) return;
            serializedObject.Update();
            var knots = serializedObject.FindProperty("knots");
            if (knots.arraySize < 2) return;
            if (railLines == null) BuildPreview(((TrackAlignment)target).Build());

            Handles.color = new Color(.25f, .72f, 1f);
            Handles.DrawLines(railLines);
            Handles.color = new Color(.8f, .58f, .32f);
            Handles.DrawLines(sleeperLines);
            Vector3 start = knots.GetArrayElementAtIndex(0).vector3Value;
            Handles.color = Color.white;
            for (int i = 1; i < knots.arraySize; i++)
            {
                var knot = knots.GetArrayElementAtIndex(i);
                Vector3 point = knot.vector3Value - start;
                if (i != selectedKnot)
                {
                    float size = HandleUtility.GetHandleSize(point) * .042f;
                    if (Handles.Button(point, Quaternion.identity, size, size, Handles.DotHandleCap)) selectedKnot = i;
                    continue;
                }
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(point, Quaternion.identity);
                if (EditorGUI.EndChangeCheck()) knot.vector3Value = moved + start;
            }
            if (serializedObject.ApplyModifiedProperties()) Refresh();
        }

        void BuildPreview(TrackPath path)
        {
            float end = path.TravelAt(RouteSchedule.Stops[1]);
            int steps = Mathf.CeilToInt(end / 2);
            railLines = new Vector3[steps * 8];
            int line = 0;
            TrackFrame previous = path.AtTravel(0);
            for (int i = 1; i <= steps; i++)
            {
                TrackFrame next = path.AtTravel(end * i / steps);
                for (int track = 0; track < 2; track++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = track * TrackPath.ParallelOffset + side * TrackMesh.RailCenter;
                    railLines[line++] = previous.Point(x, TrackMesh.RailTop);
                    railLines[line++] = next.Point(x, TrackMesh.RailTop);
                }
                previous = next;
            }
            TrackMesh.SleeperRange(path, 0, end, false, out int first, out int last);
            TrackMesh.SleeperRange(path, 0, end, true, out int secondFirst, out int secondLast);
            sleeperLines = new Vector3[(last - first + secondLast - secondFirst) * 2];
            line = 0;
            for (int track = 0; track < 2; track++)
            {
                bool parallel = track == 1;
                for (int i = parallel ? secondFirst : first; i < (parallel ? secondLast : last); i++)
                {
                    TrackFrame frame = path.AtTravel(i * path.SleeperSpacing, parallel);
                    sleeperLines[line++] = frame.Point(-TrackMesh.SleeperWidth * .5f, TrackMesh.RailSeat);
                    sleeperLines[line++] = frame.Point(TrackMesh.SleeperWidth * .5f, TrackMesh.RailSeat);
                }
            }
        }
    }
}
