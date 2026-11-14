using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LineFour.Oido;
using Object=UnityEngine.Object;

namespace LineFour.Streets {
public static class MergeValidation {
    public static void Run(){
        var scene=EditorSceneManager.OpenScene("Assets/Scene/Oido_Run.unity");
        var sim=Object.FindFirstObjectByType<LineFour.Oido.Line4Drive>();
        if(!sim || sim.doorCars.Length!=30 || sim.doorCars.Any(r=>!r||r.sharedMesh.blendShapeCount!=2))
            throw new InvalidDataException("Lost sliding train");
        var doors=new SerializedObject(sim).FindProperty("screenDoors");
        if(doors==null||doors.arraySize<4)throw new InvalidDataException("Missing friend's screen door array");
        for(int i=0;i<4;i++)if(!doors.GetArrayElementAtIndex(i).objectReferenceValue)throw new InvalidDataException("Screen doors missing "+i);
        foreach(string name in new[]{"Line4_Jeongwang_Game","Line4_SingilOncheon_Game","Line4_Ansan_Game"})
            if(!Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(t=>t.name==name))
                throw new InvalidDataException("Missing friend station "+name);
        if(!Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(t=>t.name=="ApprovedConsoles"))
            throw new InvalidDataException("Lost cab mesh");
        // The incoming station models already include their original architecture.
        var duplicate=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Original station architecture");
        if(duplicate)Object.DestroyImmediate(duplicate);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save");
        File.WriteAllText("Library/ExperienceCheck.txt","Merged 116bd0d: three new stations, four screen door controllers, new cab and city retained.\n");
        SessionState.SetInt("ExperienceStage",1);
        EditorApplication.isPlaying=true;
    }
}}
