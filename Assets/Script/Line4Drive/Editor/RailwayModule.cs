using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LineFour.EditorTools
{
    internal sealed class RailwayModule
    {
        const string AssetPath = "Assets/Blender/RailwayModule.fbx";
        public const int Length = 40;

        readonly Mesh mesh;
        readonly Material[] materials;

        public RailwayModule()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
            var filter = model ? model.GetComponentInChildren<MeshFilter>() : null;
            if (!filter || !filter.sharedMesh || Mathf.Abs(filter.sharedMesh.bounds.size.z - Length) > .001f)
                throw new InvalidOperationException("Expected a 40 m railway mesh at " + AssetPath);

            mesh = filter.sharedMesh;
            materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
        }

        public void AddSection(Transform parent, int start)
        {
            var section = new GameObject("Track_" + (start + 260).ToString("0000"), typeof(MeshFilter), typeof(MeshRenderer));
            section.transform.SetParent(parent, false);
            SetSection(section.GetComponent<MeshFilter>(), start);
        }

        public void SetSection(MeshFilter section, float start)
        {
            var position = section.transform.localPosition;
            position.z = start - mesh.bounds.min.z;
            section.transform.localPosition = position;
            section.sharedMesh = mesh;

            var renderer = section.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
