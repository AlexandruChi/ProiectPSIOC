using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ShapeType
{
    Cube,
    Sphere,
    Cylinder,
    Tetrahedron,
    Star
}

public class ShapeSortingGameBuilder : MonoBehaviour
{
    [SerializeField] private bool autoBuildOnPlay = true;

    private readonly List<ShapeSortingPiece> _placedPieces = new List<ShapeSortingPiece>();
    private readonly List<ShapeSortingSlot> _slots = new List<ShapeSortingSlot>();
    private bool _isBuilt;

    private const float BoxWidth = 0.72f;
    private const float BoxDepth = 0.46f;
    private const float BoxHeight = 0.22f;
    private const float WallThickness = 0.02f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBuildIfNeeded()
    {
        if (FindAnyObjectByType<ShapeSortingGameBuilder>(true) != null)
            return;

        var go = new GameObject("Shape Sorting Game");
        go.AddComponent<ShapeSortingGameBuilder>();
    }

#if UNITY_EDITOR
    [MenuItem("Tools/VR Room/Build Shape Sorting Game")]
    private static void BuildFromMenu()
    {
        var builder = FindAnyObjectByType<ShapeSortingGameBuilder>(true);
        if (builder == null)
        {
            var go = new GameObject("Shape Sorting Game");
            builder = go.AddComponent<ShapeSortingGameBuilder>();
        }

        builder.BuildGame();
    }
#endif

    private void Awake()
    {
        if (autoBuildOnPlay)
        {
            BuildGame();
        }
    }

    public void BuildGame()
    {
        if (_isBuilt)
            return;

        _isBuilt = true;

        var table = FindTable();
        if (table == null)
        {
            Debug.LogWarning("[ShapeSortingGame] No Table object found in the scene. The game was not created.");
            return;
        }

        var root = transform;
        root.SetParent(table, false);
        root.localPosition = Vector3.zero;

        float topY = GetTableTopWorldY(table);
        root.position = new Vector3(table.position.x, topY + 0.12f, table.position.z);

        var box = CreateSortingBox(root, Vector3.zero);
        var manager = root.gameObject.AddComponent<ShapeSortingGameManager>();
        manager.Initialize(_slots);

        CreateShapePieces(root, box, manager);
    }

    private Transform FindTable()
    {
        var table = GameObject.Find("Table");
        if (table != null)
            return table.transform;

        var room = GameObject.Find("VR Room");
        if (room != null)
        {
            var roomTable = room.transform.Find("Table");
            if (roomTable != null)
                return roomTable;
        }

        return null;
    }

    private float GetTableTopWorldY(Transform table)
    {
        var top = table.Find("Top");
        if (top != null)
        {
            var localTopCenter = top.localPosition + Vector3.up * (top.localScale.y * 0.5f);
            return table.TransformPoint(localTopCenter).y;
        }

        return table.position.y + 0.72f;
    }

    private GameObject CreateSortingBox(Transform parent, Vector3 localPosition)
    {
        var boxRoot = new GameObject("Sorting Box");
        boxRoot.transform.SetParent(parent, false);
        boxRoot.transform.localPosition = localPosition;

        var body = new GameObject("Body");
        body.transform.SetParent(boxRoot.transform, false);

        var bottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bottom.name = "Bottom";
        bottom.transform.SetParent(body.transform, false);
        bottom.transform.localScale = new Vector3(BoxWidth, WallThickness, BoxDepth);
        bottom.transform.localPosition = new Vector3(0f, -BoxHeight * 0.5f + WallThickness * 0.5f, 0f);
        var bottomRenderer = bottom.GetComponent<Renderer>();
        bottomRenderer.sharedMaterial = CreateMaterial(new Color(0.75f, 0.9f, 0.98f));
        Object.DestroyImmediate(bottom.GetComponent<Collider>());

        CreateWall(body.transform, new Vector3(0f, 0f, -BoxDepth * 0.5f - WallThickness * 0.5f), new Vector3(BoxWidth, BoxHeight, WallThickness));
        CreateWall(body.transform, new Vector3(0f, 0f, BoxDepth * 0.5f + WallThickness * 0.5f), new Vector3(BoxWidth, BoxHeight, WallThickness));
        CreateWall(body.transform, new Vector3(-BoxWidth * 0.5f - WallThickness * 0.5f, 0f, 0f), new Vector3(WallThickness, BoxHeight, BoxDepth));
        CreateWall(body.transform, new Vector3(BoxWidth * 0.5f + WallThickness * 0.5f, 0f, 0f), new Vector3(WallThickness, BoxHeight, BoxDepth));

        var topHolder = new GameObject("Top Surface");
        topHolder.transform.SetParent(body.transform, false);
        topHolder.transform.localPosition = new Vector3(0f, BoxHeight * 0.5f - WallThickness * 0.5f, 0f);

        var topMesh = ProceduralMeshFactory.CreateTopSurfaceMesh(BoxWidth - WallThickness * 0.5f, BoxDepth - WallThickness * 0.5f,
            new[]
            {
                new HoleDefinition(ShapeType.Cube, new Vector2(-0.18f, 0.08f), 0.11f, 0.11f),
                new HoleDefinition(ShapeType.Sphere, new Vector2(-0.02f, 0.08f), 0.085f, 0.085f),
                new HoleDefinition(ShapeType.Cylinder, new Vector2(0.18f, 0.08f), 0.115f, 0.115f),
                new HoleDefinition(ShapeType.Tetrahedron, new Vector2(-0.09f, -0.12f), 0.12f, 0.12f),
                new HoleDefinition(ShapeType.Star, new Vector2(0.15f, -0.12f), 0.12f, 0.12f)
            }, 0.008f);

        var filter = topHolder.AddComponent<MeshFilter>();
        filter.sharedMesh = topMesh;

        var renderer = topHolder.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateMaterial(new Color(0.82f, 0.92f, 1f));

        var topCollider = topHolder.AddComponent<MeshCollider>();
        topCollider.sharedMesh = topMesh;
        topCollider.convex = false;

        CreateHoleSlots(boxRoot.transform, boxRoot.transform.position + new Vector3(0f, 0.015f, 0f));

        return boxRoot;
    }

    private void CreateWall(Transform parent, Vector3 localPosition, Vector3 localScale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localScale = localScale;
        var renderer = wall.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateMaterial(new Color(0.73f, 0.88f, 0.96f));
        Object.DestroyImmediate(wall.GetComponent<Collider>());
    }

    private void CreateHoleSlots(Transform parent, Vector3 absoluteBoxPosition)
    {
        var slotDefinitions = new[]
        {
            new SlotDefinition(ShapeType.Cube, new Vector3(-0.18f, 0.04f, 0.08f), Quaternion.identity),
            new SlotDefinition(ShapeType.Sphere, new Vector3(-0.02f, 0.04f, 0.08f), Quaternion.identity),
            new SlotDefinition(ShapeType.Cylinder, new Vector3(0.18f, 0.04f, 0.08f), Quaternion.identity),
            new SlotDefinition(ShapeType.Tetrahedron, new Vector3(-0.09f, 0.04f, -0.12f), Quaternion.Euler(0f, 0f, 0f)),
            new SlotDefinition(ShapeType.Star, new Vector3(0.15f, 0.04f, -0.12f), Quaternion.Euler(0f, 0f, 0f))
        };

        foreach (var slotDefinition in slotDefinitions)
        {
            var slotObject = new GameObject(slotDefinition.ShapeType + " Slot");
            slotObject.transform.SetParent(parent, false);
            slotObject.transform.localPosition = slotDefinition.LocalPosition;
            slotObject.transform.localRotation = slotDefinition.LocalRotation;

            var trigger = slotObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.08f;

            var slot = slotObject.AddComponent<ShapeSortingSlot>();
            slot.AcceptedShape = slotDefinition.ShapeType;
            slot.AcceptanceRadius = 0.08f;
            slot.PlacementPosition = slotObject.transform.position + Vector3.up * 0.01f;
            slot.PlacementRotation = slotObject.transform.rotation;
            _slots.Add(slot);
        }
    }

    private void CreateShapePieces(Transform root, GameObject box, ShapeSortingGameManager manager)
    {
        var piecePositions = new[]
        {
            new Vector3(-0.22f, 0.12f, 0.08f),
            new Vector3(-0.02f, 0.12f, 0.08f),
            new Vector3(0.22f, 0.12f, 0.08f),
            new Vector3(-0.12f, 0.12f, -0.12f),
            new Vector3(0.16f, 0.12f, -0.12f)
        };

        var definitions = new[]
        {
            new ShapeDefinition(ShapeType.Cube, Color.red, new Vector3(0.09f, 0.09f, 0.09f)),
            new ShapeDefinition(ShapeType.Sphere, Color.yellow, new Vector3(0.09f, 0.09f, 0.09f)),
            new ShapeDefinition(ShapeType.Cylinder, Color.green, new Vector3(0.09f, 0.12f, 0.09f)),
            new ShapeDefinition(ShapeType.Tetrahedron, new Color(1f, 0.55f, 0.15f), new Vector3(0.13f, 0.13f, 0.13f)),
            new ShapeDefinition(ShapeType.Star, new Color(0.57f, 0.24f, 0.85f), new Vector3(0.14f, 0.14f, 0.14f))
        };

        for (int i = 0; i < definitions.Length; i++)
        {
            var definition = definitions[i];
            var piece = CreatePiece(definition.ShapeType, definition.Color, definition.Scale, piecePositions[i], root);
            piece.RegisterForManager(manager);
        }
    }

    private ShapeSortingPiece CreatePiece(ShapeType type, Color color, Vector3 scale, Vector3 localPosition, Transform parent)
    {
        var go = new GameObject(type + " Piece");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 180f), Random.Range(-30f, 30f));

        var mesh = ProceduralMeshFactory.CreateShapeMesh(type);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateMaterial(color);

        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = true;

        var rig = go.AddComponent<Rigidbody>();
        rig.mass = 0.3f;
        rig.drag = 0.1f;
        rig.angularDrag = 0.2f;
        rig.interpolation = RigidbodyInterpolation.Interpolate;
        rig.useGravity = true;

        var interactable = go.AddComponent<XRGrabInteractable>();
        interactable.trackRotation = true;
        interactable.throwOnDetach = true;
        interactable.movementType = XRBaseInteractable.MovementType.VelocityTracking;

        go.transform.localScale = scale;

        var piece = go.AddComponent<ShapeSortingPiece>();
        piece.ShapeType = type;
        piece.GrabInteractable = interactable;
        piece.Rigidbody = rig;

        return piece;
    }

    private static Material CreateMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader)
        {
            color = color,
            name = "Generated Material"
        };

        return material;
    }

    private struct ShapeDefinition
    {
        public ShapeDefinition(ShapeType shapeType, Color color, Vector3 scale)
        {
            ShapeType = shapeType;
            Color = color;
            Scale = scale;
        }

        public ShapeType ShapeType { get; }
        public Color Color { get; }
        public Vector3 Scale { get; }
    }

    private struct SlotDefinition
    {
        public SlotDefinition(ShapeType shapeType, Vector3 localPosition, Quaternion localRotation)
        {
            ShapeType = shapeType;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
        }

        public ShapeType ShapeType { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
    }
}

public class ShapeSortingGameManager : MonoBehaviour
{
    private readonly List<ShapeSortingSlot> _slots = new List<ShapeSortingSlot>();
    private readonly HashSet<ShapeSortingPiece> _placed = new HashSet<ShapeSortingPiece>();

    public void Initialize(List<ShapeSortingSlot> slots)
    {
        _slots.Clear();
        _slots.AddRange(slots);
    }

    public void RegisterPiece(ShapeSortingPiece piece)
    {
        if (piece == null || _placed.Contains(piece))
            return;

        foreach (var slot in _slots)
        {
            if (slot.AcceptedShape == piece.ShapeType)
            {
                slot.RegisterPiece(piece);
                break;
            }
        }
    }

    public void TryPlace(ShapeSortingPiece piece, ShapeSortingSlot slot)
    {
        if (piece == null || slot == null)
            return;

        if (piece.ShapeType != slot.AcceptedShape)
            return;

        if (Vector3.Distance(piece.transform.position, slot.PlacementPosition) > slot.AcceptanceRadius + 0.04f)
            return;

        if (_placed.Contains(piece))
            return;

        piece.Place(slot);
        _placed.Add(piece);

        if (_placed.Count == 5)
        {
            Debug.Log("[ShapeSortingGame] All shapes have been placed correctly. Puzzle complete!");
        }
    }
}

public class ShapeSortingSlot : MonoBehaviour
{
    public ShapeType AcceptedShape { get; set; }
    public float AcceptanceRadius { get; set; }
    public Vector3 PlacementPosition { get; set; }
    public Quaternion PlacementRotation { get; set; }

    private ShapeSortingGameManager _manager;

    private void Start()
    {
        _manager = GetComponentInParent<ShapeSortingGameManager>();
        PlacementPosition = transform.position + Vector3.up * 0.01f;
        PlacementRotation = Quaternion.identity;
    }

    private void OnTriggerEnter(Collider other)
    {
        var piece = other.GetComponentInParent<ShapeSortingPiece>();
        if (piece == null)
            return;

        if (piece.ShapeType != AcceptedShape)
            return;

        if (_manager == null)
            _manager = GetComponentInParent<ShapeSortingGameManager>();

        _manager.TryPlace(piece, this);
    }

    public void RegisterPiece(ShapeSortingPiece piece)
    {
    }
}

public class ShapeSortingPiece : MonoBehaviour
{
    public ShapeType ShapeType { get; set; }
    public XRGrabInteractable GrabInteractable { get; set; }
    public Rigidbody Rigidbody { get; set; }
    public bool Placed { get; private set; }

    public void RegisterForManager(ShapeSortingGameManager manager)
    {
        manager.RegisterPiece(this);
    }

    public void Place(ShapeSortingSlot slot)
    {
        if (Placed)
            return;

        Placed = true;
        transform.position = slot.PlacementPosition;
        transform.rotation = slot.PlacementRotation;
        transform.SetParent(slot.transform.parent, true);

        if (Rigidbody != null)
        {
            Rigidbody.isKinematic = true;
            Rigidbody.useGravity = false;
            Rigidbody.velocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
        }

        if (GrabInteractable != null)
        {
            GrabInteractable.enabled = false;
        }
    }
}

public static class ProceduralMeshFactory
{
    public static Mesh CreateShapeMesh(ShapeType type)
    {
        switch (type)
        {
            case ShapeType.Cube:
                return CreateCubeMesh();
            case ShapeType.Sphere:
                return CreateSphereMesh(18, 22);
            case ShapeType.Cylinder:
                return CreateCylinderMesh(18, 0.5f, 1f);
            case ShapeType.Tetrahedron:
                return CreateTetrahedronMesh();
            case ShapeType.Star:
                return CreateStarMesh(0.5f, 0.22f, 0.9f);
            default:
                return CreateCubeMesh();
        }
    }

    public static Mesh CreateTopSurfaceMesh(float width, float depth, HoleDefinition[] holes, float cellSize)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        float halfWidth = width * 0.5f;
        float halfDepth = depth * 0.5f;

        int xCells = Mathf.CeilToInt(width / cellSize);
        int zCells = Mathf.CeilToInt(depth / cellSize);

        for (int xIndex = 0; xIndex < xCells; xIndex++)
        {
            for (int zIndex = 0; zIndex < zCells; zIndex++)
            {
                float x0 = -halfWidth + xIndex * cellSize;
                float x1 = Mathf.Min(x0 + cellSize, halfWidth);
                float z0 = -halfDepth + zIndex * cellSize;
                float z1 = Mathf.Min(z0 + cellSize, halfDepth);

                var center = new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
                if (IsInsideAnyHole(center, holes))
                    continue;

                int baseIndex = vertices.Count;
                vertices.Add(new Vector3(x0, 0f, z0));
                vertices.Add(new Vector3(x1, 0f, z0));
                vertices.Add(new Vector3(x1, 0f, z1));
                vertices.Add(new Vector3(x0, 0f, z1));

                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }
        }

        var mesh = new Mesh();
        mesh.name = "Procedural Sorting Box Top";
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.Optimize();
        return mesh;
    }

    private static bool IsInsideAnyHole(Vector2 point, HoleDefinition[] holes)
    {
        for (int i = 0; i < holes.Length; i++)
        {
            if (holes[i].Contains(point))
                return true;
        }

        return false;
    }

    private static Mesh CreateCubeMesh()
    {
        var mesh = new Mesh();

        var vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f)
        };

        int[] triangles =
        {
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            4, 5, 1, 4, 1, 0,
            3, 2, 6, 3, 6, 7,
            1, 5, 6, 1, 6, 2,
            4, 0, 3, 4, 3, 7
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateSphereMesh(int latSegments, int lonSegments)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        for (int lat = 0; lat <= latSegments; lat++)
        {
            float v = lat / (float)latSegments;
            float theta = v * Mathf.PI;
            float y = Mathf.Cos(theta);
            float r = Mathf.Sin(theta);

            for (int lon = 0; lon <= lonSegments; lon++)
            {
                float u = lon / (float)lonSegments;
                float phi = u * Mathf.PI * 2f;
                float x = r * Mathf.Cos(phi);
                float z = r * Mathf.Sin(phi);
                vertices.Add(new Vector3(x, y, z));
            }
        }

        for (int lat = 0; lat < latSegments; lat++)
        {
            for (int lon = 0; lon < lonSegments; lon++)
            {
                int a = lat * (lonSegments + 1) + lon;
                int b = a + lonSegments + 1;
                int c = a + 1;
                int d = b + 1;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);

                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
            }
        }

        var mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateCylinderMesh(int segments, float radius, float height)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, -height * 0.5f, z));
            vertices.Add(new Vector3(x, height * 0.5f, z));
            normals.Add(new Vector3(x, 0f, z).normalized);
            normals.Add(new Vector3(x, 0f, z).normalized);
        }

        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            int b = ((i + 1) % segments) * 2;
            int c = a + 1;
            int d = b + 1;

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);

            triangles.Add(b);
            triangles.Add(d);
            triangles.Add(c);
        }

        int topCenterIndex = vertices.Count;
        vertices.Add(new Vector3(0f, height * 0.5f, 0f));
        normals.Add(Vector3.up);

        int bottomCenterIndex = vertices.Count;
        vertices.Add(new Vector3(0f, -height * 0.5f, 0f));
        normals.Add(Vector3.down);

        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            int b = ((i + 1) % segments) * 2;
            int c = a + 1;
            int d = b + 1;

            triangles.Add(topCenterIndex);
            triangles.Add(d);
            triangles.Add(c);

            triangles.Add(bottomCenterIndex);
            triangles.Add(a);
            triangles.Add(b);
        }

        var mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateTetrahedronMesh()
    {
        var vertices = new[]
        {
            new Vector3(0f, 0.5f, 0f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0f, -0.5f, -0.5f)
        };

        int[] triangles =
        {
            0, 1, 2,
            0, 2, 3,
            0, 3, 1,
            1, 3, 2
        };

        var mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateStarMesh(float outerRadius, float innerRadius, float depth)
    {
        var points = new List<Vector2>();
        for (int i = 0; i < 10; i++)
        {
            float angle = -Mathf.PI / 2f + i * Mathf.PI / 5f;
            float radius = i % 2 == 0 ? outerRadius : innerRadius;
            points.Add(new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
        }

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var next = points[(i + 1) % points.Count];
            float halfDepth = depth * 0.5f;

            vertices.Add(new Vector3(p.x, -halfDepth, p.y));
            vertices.Add(new Vector3(p.x, halfDepth, p.y));
            vertices.Add(new Vector3(next.x, -halfDepth, next.y));
            vertices.Add(new Vector3(next.x, halfDepth, next.y));

            int index = i * 4;
            triangles.Add(index + 0);
            triangles.Add(index + 2);
            triangles.Add(index + 1);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }

        float centerHalfDepth = depth * 0.5f;
        int centerFrontIndex = vertices.Count;
        vertices.Add(new Vector3(0f, -centerHalfDepth, 0f));
        int centerBackIndex = vertices.Count;
        vertices.Add(new Vector3(0f, centerHalfDepth, 0f));

        for (int i = 0; i < points.Count; i++)
        {
            int a = i * 4;
            int b = ((i + 1) % points.Count) * 4;

            triangles.Add(centerFrontIndex);
            triangles.Add(a + 2);
            triangles.Add(a + 0);

            triangles.Add(centerBackIndex);
            triangles.Add(a + 1);
            triangles.Add(a + 3);
        }

        var mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

public struct HoleDefinition
{
    public HoleDefinition(ShapeType type, Vector2 center, float majorRadius, float minorRadius)
    {
        Type = type;
        Center = center;
        MajorRadius = majorRadius;
        MinorRadius = minorRadius;
    }

    public ShapeType Type { get; }
    public Vector2 Center { get; }
    public float MajorRadius { get; }
    public float MinorRadius { get; }

    public bool Contains(Vector2 point)
    {
        var local = point - Center;

        switch (Type)
        {
            case ShapeType.Cube:
                return Mathf.Abs(local.x) <= MajorRadius * 0.5f && Mathf.Abs(local.y) <= MinorRadius * 0.5f;
            case ShapeType.Sphere:
            case ShapeType.Cylinder:
                return local.sqrMagnitude <= (MajorRadius * 0.5f) * (MajorRadius * 0.5f);
            case ShapeType.Tetrahedron:
                return PointInPolygon(local, new[]
                {
                    new Vector2(-MajorRadius * 0.5f, -MinorRadius * 0.25f),
                    new Vector2(0f, MinorRadius * 0.5f),
                    new Vector2(MajorRadius * 0.5f, -MinorRadius * 0.25f)
                });
            case ShapeType.Star:
                return PointInPolygon(local, CreateStarPolygon(MajorRadius * 0.5f, MinorRadius * 0.5f));
            default:
                return false;
        }
    }

    private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 pi = polygon[i];
            Vector2 pj = polygon[j];

            if (Mathf.Abs(pj.y - pi.y) < 0.000001f)
                continue;

            bool intersects = ((pi.y > point.y) != (pj.y > point.y)) &&
                (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x);
            if (intersects)
                inside = !inside;
        }

        return inside;
    }

    private static Vector2[] CreateStarPolygon(float outerRadius, float innerRadius)
    {
        var points = new List<Vector2>();
        for (int i = 0; i < 10; i++)
        {
            float angle = -Mathf.PI / 2f + i * Mathf.PI / 5f;
            float radius = i % 2 == 0 ? outerRadius : innerRadius;
            points.Add(new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
        }

        return points.ToArray();
    }
}
