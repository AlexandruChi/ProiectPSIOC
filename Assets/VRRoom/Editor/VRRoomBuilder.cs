using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Builds a simple VR room (floor, walls, ceiling, lights) with a table, two chairs and
/// grabbable objects on the table. Menu: Tools > VR Room.
/// </summary>
public static class VRRoomBuilder
{
    const string MatDir = "Assets/VRRoom/Materials";
    const string ScenePath = "Assets/Scenes/VRRoom.unity";

    const string XROriginPrefab = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";
    const string XROriginFallback = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string InteractablesDir = "Assets/VRTemplateAssets/Prefabs/Interactables/";

    // Room dimensions (metres)
    const float RoomW = 6f, RoomD = 6f, RoomH = 3f, WallT = 0.1f;
    // Table
    static readonly Vector3 TablePos = new Vector3(0f, 0f, 0.5f);
    const float TableH = 0.75f, TableW = 1.6f, TableD = 0.9f, TopT = 0.05f, LegT = 0.07f;

    [MenuItem("Tools/VR Room/Build Room Scene (new scene)")]
    public static void BuildNewScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildRoom();
        AddXROrigin();
        SetupLightingEnvironment();

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[VRRoom] Scene created at {ScenePath}. Press Play (or build) to try it in VR.");
    }

    [MenuItem("Tools/VR Room/Add Room To Current Scene")]
    public static void AddToCurrentScene()
    {
        BuildRoom();
        if (Object.FindAnyObjectByType<XROrigin>() == null) AddXROrigin();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- Room

    static void BuildRoom()
    {
        var mFloor    = Mat("Floor_Wood",   new Color(0.42f, 0.28f, 0.18f), 0.35f);
        var mWall     = Mat("Wall_Paint",   new Color(0.86f, 0.84f, 0.80f), 0.15f);
        var mCeiling  = Mat("Ceiling",      new Color(0.95f, 0.95f, 0.95f), 0.05f);
        var mTrim     = Mat("Trim_Dark",    new Color(0.18f, 0.16f, 0.15f), 0.3f);
        var mTable    = Mat("Table_Oak",    new Color(0.62f, 0.44f, 0.26f), 0.55f);
        var mLegs     = Mat("Metal_Black",  new Color(0.08f, 0.08f, 0.09f), 0.6f, 0.8f);
        var mChair    = Mat("Chair_Walnut", new Color(0.33f, 0.21f, 0.13f), 0.45f);
        var mRug      = Mat("Rug",          new Color(0.22f, 0.32f, 0.45f), 0.0f);
        var mWindow   = Emissive("Window_Light", new Color(0.75f, 0.88f, 1f), 1.6f);
        var mLampShade= Emissive("Lamp_Glow",    new Color(1f, 0.9f, 0.7f), 2.5f);

        var room = new GameObject("VR Room").transform;

        // --- Shell
        var shell = new GameObject("Shell").transform;
        shell.SetParent(room, false);

        var floor = Box("Floor", shell, new Vector3(0, -WallT / 2, 0), new Vector3(RoomW, WallT, RoomD), mFloor);
        MakeTeleportable(floor);

        Box("Ceiling", shell, new Vector3(0, RoomH + WallT / 2, 0), new Vector3(RoomW + 2 * WallT, WallT, RoomD + 2 * WallT), mCeiling);
        Box("Wall_North", shell, new Vector3(0, RoomH / 2,  RoomD / 2 + WallT / 2), new Vector3(RoomW + 2 * WallT, RoomH, WallT), mWall);
        Box("Wall_South", shell, new Vector3(0, RoomH / 2, -RoomD / 2 - WallT / 2), new Vector3(RoomW + 2 * WallT, RoomH, WallT), mWall);
        Box("Wall_East",  shell, new Vector3( RoomW / 2 + WallT / 2, RoomH / 2, 0), new Vector3(WallT, RoomH, RoomD), mWall);
        Box("Wall_West",  shell, new Vector3(-RoomW / 2 - WallT / 2, RoomH / 2, 0), new Vector3(WallT, RoomH, RoomD), mWall);

        // Baseboards (visual only)
        const float bbH = 0.1f, bbT = 0.02f;
        Box("Baseboard_N", shell, new Vector3(0, bbH / 2,  RoomD / 2 - bbT / 2), new Vector3(RoomW, bbH, bbT), mTrim, false);
        Box("Baseboard_S", shell, new Vector3(0, bbH / 2, -RoomD / 2 + bbT / 2), new Vector3(RoomW, bbH, bbT), mTrim, false);
        Box("Baseboard_E", shell, new Vector3( RoomW / 2 - bbT / 2, bbH / 2, 0), new Vector3(bbT, bbH, RoomD), mTrim, false);
        Box("Baseboard_W", shell, new Vector3(-RoomW / 2 + bbT / 2, bbH / 2, 0), new Vector3(bbT, bbH, RoomD), mTrim, false);

        // "Window" on the north wall: glowing panel with a frame
        var window = new GameObject("Window").transform;
        window.SetParent(shell, false);
        window.localPosition = new Vector3(0, 1.6f, RoomD / 2 - 0.01f);
        Box("Glass", window, Vector3.zero, new Vector3(2.2f, 1.3f, 0.01f), mWindow, false);
        Box("Frame_Top",    window, new Vector3(0,  0.675f, -0.02f), new Vector3(2.3f, 0.05f, 0.04f), mTrim, false);
        Box("Frame_Bottom", window, new Vector3(0, -0.675f, -0.04f), new Vector3(2.4f, 0.05f, 0.10f), mTrim, false);
        Box("Frame_Left",   window, new Vector3(-1.125f, 0, -0.02f), new Vector3(0.05f, 1.3f, 0.04f), mTrim, false);
        Box("Frame_Right",  window, new Vector3( 1.125f, 0, -0.02f), new Vector3(0.05f, 1.3f, 0.04f), mTrim, false);
        Box("Frame_Mid",    window, new Vector3(0, 0, -0.015f),      new Vector3(0.03f, 1.3f, 0.03f), mTrim, false);

        // Rug under the table (no collider, so teleport rays hit the floor)
        Box("Rug", room, TablePos + new Vector3(0, 0.003f, 0), new Vector3(2.8f, 0.006f, 2.0f), mRug, false);

        // --- Furniture
        BuildTable(room, mTable, mLegs);
        BuildChair(room, TablePos + new Vector3(-1.05f, 0, 0),  90f, mChair, mLegs);
        BuildChair(room, TablePos + new Vector3( 1.05f, 0, 0), -90f, mChair, mLegs);

        // --- Lights
        BuildLights(room, mLegs, mLampShade);

        // --- Grabbable objects on the table
        PlaceGrabbables(room);
    }

    static void BuildTable(Transform parent, Material top, Material legs)
    {
        var table = new GameObject("Table").transform;
        table.SetParent(parent, false);
        table.localPosition = TablePos;

        Box("Top", table, new Vector3(0, TableH - TopT / 2, 0), new Vector3(TableW, TopT, TableD), top);

        float legH = TableH - TopT;
        float lx = TableW / 2 - LegT / 2 - 0.05f, lz = TableD / 2 - LegT / 2 - 0.05f;
        foreach (var (x, z, n) in new[] { (lx, lz, "FR"), (-lx, lz, "FL"), (lx, -lz, "BR"), (-lx, -lz, "BL") })
            Box("Leg_" + n, table, new Vector3(x, legH / 2, z), new Vector3(LegT, legH, LegT), legs);

        // Apron rails under the top for a sturdier look (visual only)
        Box("Apron_Front", table, new Vector3(0, legH - 0.05f,  lz), new Vector3(TableW - 0.2f, 0.08f, 0.02f), legs, false);
        Box("Apron_Back",  table, new Vector3(0, legH - 0.05f, -lz), new Vector3(TableW - 0.2f, 0.08f, 0.02f), legs, false);
    }

    static void BuildChair(Transform parent, Vector3 pos, float yaw, Material wood, Material metal)
    {
        var chair = new GameObject("Chair").transform;
        chair.SetParent(parent, false);
        chair.localPosition = pos;
        chair.localRotation = Quaternion.Euler(0, yaw, 0); // local +Z faces the table

        const float seatH = 0.45f, seatS = 0.45f, seatT = 0.04f, legT = 0.035f;
        Box("Seat", chair, new Vector3(0, seatH - seatT / 2, 0), new Vector3(seatS, seatT, seatS), wood);

        float o = seatS / 2 - legT / 2 - 0.015f;
        float legH = seatH - seatT;
        foreach (var (x, z, n) in new[] { (o, o, "FR"), (-o, o, "FL"), (o, -o, "BR"), (-o, -o, "BL") })
            Box("Leg_" + n, chair, new Vector3(x, legH / 2, z), new Vector3(legT, legH, legT), metal);

        // Backrest posts + panel
        const float backH = 0.45f;
        Box("BackPost_L", chair, new Vector3(-o, seatH + backH / 2, -o), new Vector3(legT, backH, legT), metal);
        Box("BackPost_R", chair, new Vector3( o, seatH + backH / 2, -o), new Vector3(legT, backH, legT), metal);
        Box("Backrest",   chair, new Vector3(0, seatH + backH - 0.12f, -o), new Vector3(seatS, 0.2f, 0.025f), wood);
    }

    static void BuildLights(Transform parent, Material cordMat, Material glowMat)
    {
        var lights = new GameObject("Lights").transform;
        lights.SetParent(parent, false);

        // Pendant lamp above the table
        var lamp = new GameObject("Pendant Lamp").transform;
        lamp.SetParent(lights, false);
        lamp.localPosition = new Vector3(TablePos.x, 0, TablePos.z);

        const float shadeY = 2.1f;
        var cord = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cord.name = "Cord";
        cord.transform.SetParent(lamp, false);
        float cordLen = RoomH - shadeY;
        cord.transform.localPosition = new Vector3(0, shadeY + cordLen / 2, 0);
        cord.transform.localScale = new Vector3(0.01f, cordLen / 2, 0.01f); // cylinder is 2 units tall
        Object.DestroyImmediate(cord.GetComponent<Collider>());
        cord.GetComponent<Renderer>().sharedMaterial = cordMat;

        var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = "Shade";
        bulb.transform.SetParent(lamp, false);
        bulb.transform.localPosition = new Vector3(0, shadeY, 0);
        bulb.transform.localScale = new Vector3(0.3f, 0.22f, 0.3f);
        Object.DestroyImmediate(bulb.GetComponent<Collider>());
        var bulbR = bulb.GetComponent<Renderer>();
        bulbR.sharedMaterial = glowMat;
        bulbR.shadowCastingMode = ShadowCastingMode.Off;

        var key = new GameObject("Lamp Light").AddComponent<Light>();
        key.transform.SetParent(lamp, false);
        key.transform.localPosition = new Vector3(0, shadeY - 0.18f, 0);
        key.type = LightType.Point;
        key.color = new Color(1f, 0.92f, 0.8f);
        key.intensity = 2.5f;
        key.range = 7f;
        key.shadows = LightShadows.Soft;

        // Soft fill light so corners aren't black (no shadows = cheap on Quest)
        var fill = new GameObject("Fill Light").AddComponent<Light>();
        fill.transform.SetParent(lights, false);
        fill.transform.localPosition = new Vector3(0, RoomH - 0.3f, -1.5f);
        fill.type = LightType.Point;
        fill.color = new Color(0.9f, 0.95f, 1f);
        fill.intensity = 1.2f;
        fill.range = 8f;
        fill.shadows = LightShadows.None;

        // Window light
        var win = new GameObject("Window Light").AddComponent<Light>();
        win.transform.SetParent(lights, false);
        win.transform.localPosition = new Vector3(0, 1.8f, RoomD / 2 - 0.4f);
        win.type = LightType.Point;
        win.color = new Color(0.75f, 0.85f, 1f);
        win.intensity = 1f;
        win.range = 5f;
        win.shadows = LightShadows.None;
    }

    static void PlaceGrabbables(Transform parent)
    {
        var group = new GameObject("Table Objects").transform;
        group.SetParent(parent, false);

        string[] prefabs =
        {
            "Cube Interactable.prefab",
            "Sphere Interactable.prefab",
            "Cylinder Interactable Variant.prefab",
            "Torus Interactable Variant.prefab",
        };
        float y = TableH + 0.12f;
        Vector3[] spots =
        {
            new Vector3(-0.5f, y, -0.15f),
            new Vector3(-0.17f, y, 0.1f),
            new Vector3(0.17f, y, -0.15f),
            new Vector3(0.5f, y, 0.1f),
        };

        int placed = 0;
        for (int i = 0; i < prefabs.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteractablesDir + prefabs[i]);
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            go.transform.position = TablePos + spots[i];
            go.transform.rotation = Quaternion.identity;
            placed++;
        }

        // Fallback: simple grabbable cubes if the template prefabs aren't present
        if (placed == 0)
        {
            var mat = Mat("Grab_Orange", new Color(1f, 0.55f, 0.15f), 0.5f);
            for (int i = 0; i < spots.Length; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Grab Cube " + (i + 1);
                cube.transform.SetParent(group, false);
                cube.transform.position = TablePos + spots[i];
                cube.transform.localScale = Vector3.one * 0.08f;
                cube.GetComponent<Renderer>().sharedMaterial = mat;
                var rb = cube.AddComponent<Rigidbody>();
                rb.mass = 0.3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                cube.AddComponent<XRGrabInteractable>();
            }
        }
    }

    // ---------------------------------------------------------------- XR + environment

    static void AddXROrigin()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefab)
                  ?? AssetDatabase.LoadAssetAtPath<GameObject>(XROriginFallback);
        if (prefab == null)
        {
            Debug.LogWarning("[VRRoom] No XR Origin prefab found. Add one via GameObject > XR > XR Origin (VR).");
            return;
        }
        var origin = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // Stand ~1 m from the long side of the table, facing it
        origin.transform.position = new Vector3(TablePos.x, 0, TablePos.z - TableD / 2 - 0.9f);
        origin.transform.rotation = Quaternion.identity;
    }

    static void SetupLightingEnvironment()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.55f, 0.58f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.40f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.18f, 0.15f);
        RenderSettings.fog = false;

        // Reflection probe so the table/metal pick up the room instead of a black sky
        var probeGO = new GameObject("Reflection Probe");
        probeGO.transform.position = new Vector3(0, 1.5f, 0);
        var probe = probeGO.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(RoomW, RoomH, RoomD);
        probe.center = new Vector3(0, 0, 0);
        probe.resolution = 128;
    }

    static void AddSceneToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------------------------------------------------------- Helpers

    static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(go,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        return go;
    }

    static void MakeTeleportable(GameObject floor)
    {
        var area = floor.AddComponent<TeleportationArea>();
        // The XRI starter teleport interactor only targets the "Teleport" interaction layer
        if (InteractionLayerMask.NameToLayer("Teleport") >= 0)
            area.interactionLayers = InteractionLayerMask.GetMask("Teleport");
    }

    static Shader LitShader()
    {
        var s = Shader.Find("Universal Render Pipeline/Lit");
        return s != null ? s : Shader.Find("Standard");
    }

    static Material Mat(string name, Color color, float smoothness, float metallic = 0f)
    {
        Directory.CreateDirectory(MatDir);
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(LitShader());
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Emissive(string name, Color color, float intensity)
    {
        var m = Mat(name, color, 0.1f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }
}
