using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Builds the VR room and its shape-sorting game. Menu: Tools > VR Room.
/// </summary>
[InitializeOnLoad]
public static class VRRoomBuilder
{
    // Make the VR room "the game": Play always starts there, and builds start there.
    static VRRoomBuilder()
    {
        EditorApplication.delayCall += UseVRRoomAsStartScene;
        EditorApplication.delayCall += OpenVRRoomInsteadOfSample;
    }

    // If the template's demo scene is open, switch the editor to the VR room.
    static void OpenVRRoomInsteadOfSample()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity") return;
        if (!File.Exists(ScenePath)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Tools/VR Room/Use VR Room As Start Scene")]
    public static void UseVRRoomAsStartScene()
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (sceneAsset == null) return;

        // Pressing Play in the editor always starts in the VR room, whatever scene is open.
        EditorSceneManager.playModeStartScene = sceneAsset;

        // The built game starts with the first scene in Build Settings.
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int index = scenes.FindIndex(sc => sc.path == ScenePath);
        if (index == 0 && scenes[0].enabled) return;
        if (index > 0) scenes.RemoveAt(index);
        if (index != 0) scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        else scenes[0] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[VRRoom] VR Room is now the start scene.");
    }

    const string MatDir = "Assets/VRRoom/Materials";
    const string ScenePath = "Assets/Scenes/VRRoom.unity";

    const string XROriginPrefab = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";
    const string XROriginFallback = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
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

    [MenuItem("Tools/VR Room/Build Shape Sorting Game On Existing Table")]
    public static void BuildShapeSortingGameOnExistingTable()
    {
        var table = GameObject.Find("Table");
        if (table == null)
        {
            Debug.LogWarning("[VRRoom] No object named 'Table' was found in the active scene.");
            return;
        }

        var oldGame = table.transform.Find("Shape Sorting Game");
        if (oldGame != null)
            Object.DestroyImmediate(oldGame.gameObject);

        var room = GameObject.Find("VR Room");
        var oldTableObjects = room != null ? room.transform.Find("Table Objects") : null;
        if (oldTableObjects != null)
            Object.DestroyImmediate(oldTableObjects.gameObject);

        BuildShapeSortingGame(table.transform);
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

        // --- Shape sorting game on the table
        var table = room.Find("Table");
        if (table != null)
            BuildShapeSortingGame(table);
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

    const float SortingBoxW = 0.72f;
    const float SortingBoxD = 0.46f;
    const float SortingBoxH = 0.22f;
    const float SortingWallT = 0.02f;

    enum SortingShape
    {
        Cube,
        Sphere,
        Cylinder,
        Tetrahedron,
        Star
    }

    struct SortingHole
    {
        public SortingHole(SortingShape shape, Vector2 center, float width, float height)
        {
            Shape = shape;
            Center = center;
            Width = width;
            Height = height;
        }

        public SortingShape Shape;
        public Vector2 Center;
        public float Width;
        public float Height;
    }

    struct SortingPieceDefinition
    {
        public SortingPieceDefinition(SortingShape shape, PrimitiveType primitive, Color color, Vector3 size, Vector3 position)
        {
            Shape = shape;
            Primitive = primitive;
            Color = color;
            Size = size;
            Position = position;
        }

        public SortingShape Shape;
        public PrimitiveType Primitive;
        public Color Color;
        public Vector3 Size;
        public Vector3 Position;
    }

    static readonly SortingHole[] SortingHoles =
    {
        new SortingHole(SortingShape.Cube, new Vector2(-0.18f, 0.08f), 0.11f, 0.11f),
        new SortingHole(SortingShape.Sphere, new Vector2(-0.02f, 0.08f), 0.085f, 0.085f),
        new SortingHole(SortingShape.Cylinder, new Vector2(0.18f, 0.08f), 0.115f, 0.115f),
        new SortingHole(SortingShape.Tetrahedron, new Vector2(-0.09f, -0.12f), 0.12f, 0.12f),
        new SortingHole(SortingShape.Star, new Vector2(0.15f, -0.12f), 0.12f, 0.12f)
    };

    static void BuildShapeSortingGame(Transform table)
    {
        var root = new GameObject("Shape Sorting Game").transform;
        root.SetParent(table, false);
        root.localPosition = new Vector3(0f, GetTableTopLocalY(table), 0f);

        var box = new GameObject("Sorting Box").transform;
        box.SetParent(root, false);
        box.localPosition = Vector3.up * (SortingBoxH * 0.5f);

        var boxMat = Mat("SortingBox_Blue", new Color(0.73f, 0.88f, 0.96f), 0.25f);
        var bottom = Box("Bottom", box, new Vector3(0f, -SortingBoxH * 0.5f + SortingWallT * 0.5f, 0f),
            new Vector3(SortingBoxW, SortingWallT, SortingBoxD), boxMat);
        GameObjectUtility.SetStaticEditorFlags(bottom, 0);

        Box("Wall_Front", box, new Vector3(0f, 0f, -SortingBoxD * 0.5f + SortingWallT * 0.5f),
            new Vector3(SortingBoxW, SortingBoxH, SortingWallT), boxMat);
        Box("Wall_Back", box, new Vector3(0f, 0f, SortingBoxD * 0.5f - SortingWallT * 0.5f),
            new Vector3(SortingBoxW, SortingBoxH, SortingWallT), boxMat);
        Box("Wall_Left", box, new Vector3(-SortingBoxW * 0.5f + SortingWallT * 0.5f, 0f, 0f),
            new Vector3(SortingWallT, SortingBoxH, SortingBoxD), boxMat);
        Box("Wall_Right", box, new Vector3(SortingBoxW * 0.5f - SortingWallT * 0.5f, 0f, 0f),
            new Vector3(SortingWallT, SortingBoxH, SortingBoxD), boxMat);

        var top = new GameObject("Top With Openings");
        top.transform.SetParent(box, false);
        top.transform.localPosition = new Vector3(0f, SortingBoxH * 0.5f - SortingWallT * 0.5f, 0f);
        var topMesh = CreateSortingTopMesh(SortingBoxW - SortingWallT, SortingBoxD - SortingWallT, SortingHoles);
        top.AddComponent<MeshFilter>().sharedMesh = topMesh;
        top.AddComponent<MeshRenderer>().sharedMaterial = Mat("SortingBox_Top", new Color(0.82f, 0.92f, 1f), 0.25f);
        var topCollider = top.AddComponent<MeshCollider>();
        topCollider.sharedMesh = topMesh;
        topCollider.convex = false;

        var pieces = new[]
        {
            new SortingPieceDefinition(SortingShape.Cube, PrimitiveType.Cube, new Color(0.9f, 0.12f, 0.12f), new Vector3(0.1f, 0.1f, 0.1f), new Vector3(-0.55f, 0.08f, -0.27f)),
            new SortingPieceDefinition(SortingShape.Sphere, PrimitiveType.Sphere, new Color(1f, 0.78f, 0.05f), new Vector3(0.076f, 0.076f, 0.076f), new Vector3(-0.55f, 0.08f, 0f)),
            new SortingPieceDefinition(SortingShape.Cylinder, PrimitiveType.Cylinder, new Color(0.12f, 0.65f, 0.22f), new Vector3(0.105f, 0.06f, 0.105f), new Vector3(-0.55f, 0.08f, 0.27f)),
            new SortingPieceDefinition(SortingShape.Tetrahedron, PrimitiveType.Cube, new Color(1f, 0.55f, 0.15f), new Vector3(0.105f, 0.105f, 0.105f), new Vector3(0.55f, 0.08f, -0.14f)),
            new SortingPieceDefinition(SortingShape.Star, PrimitiveType.Cube, new Color(0.57f, 0.24f, 0.85f), new Vector3(0.11f, 0.055f, 0.11f), new Vector3(0.55f, 0.08f, 0.14f))
        };

        for (int i = 0; i < pieces.Length; i++)
            CreateSortingPiece(root, pieces[i], i);

        for (int i = 0; i < SortingHoles.Length; i++)
            CreateSortingSocket(box, SortingHoles[i], i);
    }

    static float GetTableTopLocalY(Transform table)
    {
        var top = table.Find("Top");
        return top != null ? top.localPosition.y + top.localScale.y * 0.5f : TableH;
    }

    static void CreateSortingPiece(Transform parent, SortingPieceDefinition definition, int index)
    {
        GameObject piece;
        Mesh mesh;
        if (definition.Shape == SortingShape.Tetrahedron || definition.Shape == SortingShape.Star)
        {
            piece = new GameObject(definition.Shape + " Piece");
            mesh = definition.Shape == SortingShape.Tetrahedron ? CreateTetrahedronMesh() : CreateStarPrismMesh();
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterial = Mat("SortingShape_" + definition.Shape,
                definition.Color, 0.35f);
        }
        else
        {
            piece = GameObject.CreatePrimitive(definition.Primitive);
            piece.name = definition.Shape + " Piece";
            mesh = piece.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(piece.GetComponent<Collider>());
            piece.GetComponent<Renderer>().sharedMaterial = Mat("SortingShape_" + definition.Shape,
                definition.Color, 0.35f);
        }

        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = definition.Position;
        piece.transform.localScale = definition.Size;

        var meshCollider = piece.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;

        var body = piece.AddComponent<Rigidbody>();
        body.mass = 0.3f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var grab = piece.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = true;
        grab.trackRotation = true;
        grab.interactionLayers = new InteractionLayerMask { value = (1 << 0) | (1 << (index + 1)) };
    }

    static void CreateSortingSocket(Transform box, SortingHole hole, int index)
    {
        var socketObject = new GameObject(hole.Shape + " Opening Socket");
        socketObject.transform.SetParent(box, false);
        socketObject.transform.localPosition = new Vector3(hole.Center.x, SortingBoxH * 0.5f, hole.Center.y);

        var trigger = socketObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(hole.Width, hole.Height) * 0.55f;

        var attach = new GameObject("Attach");
        attach.transform.SetParent(socketObject.transform, false);
        attach.transform.localPosition = Vector3.down * (SortingBoxH * 0.45f);

        var socket = socketObject.AddComponent<XRSocketInteractor>();
        socket.attachTransform = attach.transform;
        socket.interactionLayers = new InteractionLayerMask { value = 1 << (index + 1) };
    }

    static Mesh CreateSortingTopMesh(float width, float depth, SortingHole[] holes)
    {
        const float cellSize = 0.003f;
        int xCells = Mathf.CeilToInt(width / cellSize);
        int zCells = Mathf.CeilToInt(depth / cellSize);
        var vertices = new Vector3[(xCells + 1) * (zCells + 1)];
        var triangles = new List<int>();
        var holePolygons = new Vector2[holes.Length][];
        float halfWidth = width * 0.5f;
        float halfDepth = depth * 0.5f;

        for (int i = 0; i < holes.Length; i++)
        {
            if (holes[i].Shape == SortingShape.Tetrahedron || holes[i].Shape == SortingShape.Star)
                holePolygons[i] = CreateHolePolygon(holes[i]);
        }

        for (int z = 0; z <= zCells; z++)
        {
            float localZ = Mathf.Lerp(-halfDepth, halfDepth, z / (float)zCells);
            for (int x = 0; x <= xCells; x++)
            {
                float localX = Mathf.Lerp(-halfWidth, halfWidth, x / (float)xCells);
                vertices[z * (xCells + 1) + x] = new Vector3(localX, 0f, localZ);
            }
        }

        for (int z = 0; z < zCells; z++)
        {
            for (int x = 0; x < xCells; x++)
            {
                var center = new Vector2(
                    Mathf.Lerp(-halfWidth, halfWidth, (x + 0.5f) / xCells),
                    Mathf.Lerp(-halfDepth, halfDepth, (z + 0.5f) / zCells));
                if (IsInsideSortingHole(center, holes, holePolygons))
                    continue;

                int a = z * (xCells + 1) + x;
                int b = a + 1;
                int c = a + xCells + 1;
                int d = c + 1;
                triangles.Add(a); triangles.Add(d); triangles.Add(b);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
        }

        var mesh = new Mesh { name = "Sorting Box Top With Five Openings" };
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static bool IsInsideSortingHole(Vector2 point, SortingHole[] holes, Vector2[][] holePolygons)
    {
        for (int i = 0; i < holes.Length; i++)
        {
            var hole = holes[i];
            var local = point - hole.Center;
            if (hole.Shape == SortingShape.Cube)
            {
                if (Mathf.Abs(local.x) <= hole.Width * 0.5f && Mathf.Abs(local.y) <= hole.Height * 0.5f)
                    return true;
            }
            else if (hole.Shape == SortingShape.Sphere || hole.Shape == SortingShape.Cylinder)
            {
                float radius = hole.Width * 0.5f;
                if (local.sqrMagnitude <= radius * radius)
                    return true;
            }
            else if (PointInPolygon(local, holePolygons[i]))
            {
                return true;
            }
        }

        return false;
    }

    static Vector2[] CreateHolePolygon(SortingHole hole)
    {
        int count = hole.Shape == SortingShape.Tetrahedron ? 3 : 10;
        var points = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            if (hole.Shape == SortingShape.Tetrahedron)
            {
                points[i] = i == 0 ? new Vector2(-0.5f, -0.3f) :
                    i == 1 ? new Vector2(0.5f, -0.3f) : new Vector2(0f, 0.6f);
                points[i] = Vector2.Scale(points[i], new Vector2(hole.Width, hole.Height));
            }
            else
            {
                float angle = -Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = (i % 2 == 0 ? 0.5f : 0.22f);
                points[i] = new Vector2(Mathf.Cos(angle) * radius * hole.Width,
                    Mathf.Sin(angle) * radius * hole.Height);
            }
        }

        return points;
    }

    static bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }

        return inside;
    }

    static Mesh CreateTetrahedronMesh()
    {
        var mesh = new Mesh { name = "Procedural Tetrahedron" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.29f, -0.3f), new Vector3(0.5f, -0.29f, -0.3f),
            new Vector3(0f, -0.29f, 0.6f), new Vector3(0f, 0.58f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 3, 1, 1, 3, 2, 2, 3, 0 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh CreateStarPrismMesh()
    {
        const int pointCount = 10;
        const float depth = 0.5f;
        var points = new Vector2[pointCount];
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        for (int i = 0; i < pointCount; i++)
        {
            float angle = -Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float radius = i % 2 == 0 ? 0.5f : 0.22f;
            points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        int frontCenter = 0;
        vertices.Add(new Vector3(0f, -depth * 0.5f, 0f));
        int backCenter = 1;
        vertices.Add(new Vector3(0f, depth * 0.5f, 0f));
        for (int i = 0; i < pointCount; i++)
        {
            vertices.Add(new Vector3(points[i].x, -depth * 0.5f, points[i].y));
            vertices.Add(new Vector3(points[i].x, depth * 0.5f, points[i].y));
        }

        for (int i = 0; i < pointCount; i++)
        {
            int current = 2 + i * 2;
            int next = 2 + ((i + 1) % pointCount) * 2;
            triangles.Add(frontCenter); triangles.Add(current); triangles.Add(next);
            triangles.Add(backCenter); triangles.Add(next + 1); triangles.Add(current + 1);
            triangles.Add(current); triangles.Add(current + 1); triangles.Add(next);
            triangles.Add(next); triangles.Add(current + 1); triangles.Add(next + 1);
        }

        var mesh = new Mesh { name = "Procedural Five Point Star" };
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
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
