// 임시 검증 스크립트 — UnityMeshSimplifier로 SkinnedMesh decimate 시 본 웨이트(rig) 보존 확인.
// ★Blender 우회(OBJ=rig손실) 대신 Unity 내 decimate로 rig 100% 보존이 목표.
using UnityEngine;
using UnityEditor;
using UnityMeshSimplifier;

public static class VenosaurDecimateTest
{
    public static string Run(float ratio)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 12/Venosaur/Prefabs/Venosaur_Tint_Green.prefab");
        if (prefab == null) return "프리팹 없음";
        var inst = Object.Instantiate(prefab);
        var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh = smr.sharedMesh;
        int beforeTri = mesh.triangles.Length / 3;
        int beforeBW = mesh.boneWeights.Length;
        int beforeBind = mesh.bindposes.Length;

        var ms = new MeshSimplifier();
        ms.Initialize(mesh);
        ms.SimplifyMesh(ratio);
        var newMesh = ms.ToMesh();
        newMesh.RecalculateNormals();

        int afterTri = newMesh.triangles.Length / 3;
        int afterBW = newMesh.boneWeights.Length;
        int afterBind = newMesh.bindposes.Length;
        int afterVerts = newMesh.vertexCount;
        bool rigOk = (afterBW == afterVerts && afterBind > 0);

        Object.DestroyImmediate(inst);
        return $"decimate tris {beforeTri}->{afterTri} | boneWeights {beforeBW}->{afterBW} (verts={afterVerts}) | bindposes {beforeBind}->{afterBind} | ★RIG보존={rigOk}";
    }

    // 통합 미리보기: decimate(rig 보존) + flat 셰이더 + 공격 포즈 → 씬 배치
    public static string RunPreview(float ratio)
    {
        var old = GameObject.Find("VenosaurLowPolyPreview");
        if (old != null) Object.DestroyImmediate(old);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 12/Venosaur/Prefabs/Venosaur_Tint_Green.prefab");
        var inst = Object.Instantiate(prefab);
        inst.name = "VenosaurLowPolyPreview";
        inst.transform.position = Vector3.zero;
        var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
        var origMat = smr.sharedMaterial;

        // decimate (rig 보존)
        var ms = new MeshSimplifier();
        ms.Initialize(smr.sharedMesh);
        ms.SimplifyMesh(ratio);
        var newMesh = ms.ToMesh();
        newMesh.RecalculateNormals();
        smr.sharedMesh = newMesh;

        // flat shading 셰이더 + 알베도
        var flat = new Material(Shader.Find("ZombieCrush/MonsterFlatStylized"));
        var alb = origMat != null && origMat.HasProperty("_BaseMap") ? origMat.GetTexture("_BaseMap") : null;
        if (alb != null && flat.HasProperty("_BaseMap")) flat.SetTexture("_BaseMap", alb);
        smr.sharedMaterial = flat;

        // 공격 포즈 SampleAnimation (애니=본 변형 작동 확인)
        string clipPath = "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 12/Venosaur/FBX Files/Venosaur@2HitComboClawsAttack.fbx";
        AnimationClip clip = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(clipPath))
            if (o is AnimationClip c && !c.name.StartsWith("__")) { clip = c; break; }
        string poseInfo = "no clip(T-pose)";
        if (clip != null) { clip.SampleAnimation(inst, clip.length * 0.5f); poseInfo = "posed:" + clip.name; }

        Selection.activeGameObject = inst;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        return $"preview: tris={newMesh.triangles.Length / 3} | {poseInfo} | flat shader OK";
    }
}
