using System;
using System.Collections.Generic;
using Bloodbound;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bloodbound.Editor
{
    public sealed class LevelCellBuilderWindow : EditorWindow
    {
        private enum Page { Cell, Place, Camera }
        private enum PlacementMode { None, Enemy, Obstacle, Platform, Ramp, Pit }
        private enum CellSide { North, East, South, West }

        private const string GeneratedRoot = "Assets/Generated/LevelCells";
        private const string MeshFolder = GeneratedRoot + "/Meshes";
        private const string MaterialFolder = "Assets/Materials/LevelCells";

        private static readonly Color accent = new Color(0.48f, 0.83f, 0.82f);
        private Page page;
        private PlacementMode placementMode;
        private Vector2 scroll;
        private float newWidth = 20f;
        private float newDepth = 20f;
        private float newHeight = 8f;
        private float snapStep = 0.5f;
        private Vector3 obstacleSize = new Vector3(2f, 2f, 2f);
        private Vector3 platformSize = new Vector3(6f, 0.5f, 6f);
        private float platformTopHeight = 2f;
        private float rampWidth = 3f;
        private float rampRun = 4f;
        private float rampRise = 2f;
        private float rampYaw;
        private Vector2 pitSize = new Vector2(2f, 2f);

        private LevelCell SelectedCell
        {
            get
            {
                GameObject selected = Selection.activeGameObject;
                return selected != null ? selected.GetComponentInParent<LevelCell>() : null;
            }
        }

        // Adds Scene view placement and selection callbacks while the builder window is open.
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
        }

        // Removes editor callbacks and exits placement mode when the window closes.
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
            placementMode = PlacementMode.None;
        }

        // Repaints the builder after the selected cell changes in the Hierarchy or Scene view.
        private void OnSelectionChanged()
        {
            LevelCell cell = SelectedCell;
            if (cell != null)
            {
                newWidth = cell.width;
                newDepth = cell.depth;
                newHeight = cell.height;
            }
            Repaint();
        }

        // Opens the level-cell builder from the Bloodbound menu.
        [MenuItem("Bloodbound/Level Cell Builder")]
        public static void OpenWindow()
        {
            LevelCellBuilderWindow window = GetWindow<LevelCellBuilderWindow>();
            window.titleContent = new GUIContent("Level Cells");
            window.minSize = new Vector2(340f, 520f);
            window.Show();
        }

        // Draws the builder header, page tabs, and the selected page controls.
        private void OnGUI()
        {
            DrawHeader();
            page = (Page)GUILayout.Toolbar((int)page, new[] { "Cell", "Place", "Camera" }, GUILayout.Height(28f));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(8f);

            switch (page)
            {
                case Page.Cell: DrawCellPage(); break;
                case Page.Place: DrawPlacePage(); break;
                case Page.Camera: DrawCameraPage(); break;
            }

            EditorGUILayout.EndScrollView();
            DrawFooter();
        }

        // Presents a compact title and the selected room status at the top of the window.
        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 17, normal = { textColor = accent } };
            EditorGUILayout.LabelField("BLOODBOUND  /  LEVEL CELLS", titleStyle);
            EditorGUILayout.LabelField(SelectedCell != null ? SelectedCell.name : "Create a room module or select a cell", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5f);
        }

        // Shows cell creation, room sizing, wall openings, surface materials, and cell connections.
        private void DrawCellPage()
        {
            LevelCell cell = SelectedCell;
            if (cell == null)
            {
                DrawSection("CREATE A CELL", "Start with a room module. Its contents remain editable in the Scene view.");
                newWidth = Mathf.Max(2f, EditorGUILayout.FloatField("Width", newWidth));
                newDepth = Mathf.Max(2f, EditorGUILayout.FloatField("Depth", newDepth));
                newHeight = Mathf.Max(3f, EditorGUILayout.FloatField("Room Height", newHeight));
                EditorGUILayout.Space(6f);
                if (GUILayout.Button("Create New Cell", GUILayout.Height(36f)))
                    CreateNewCell();

                EditorGUILayout.HelpBox("Select a Level Cell object to edit its dimensions, openings, and neighboring rooms.", MessageType.Info);
                return;
            }

            DrawSection("ROOM DIMENSIONS", "Size changes apply when you rebuild the cell.");
            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(cell, "Edit Cell Dimensions");
            cell.width = Mathf.Max(2f, EditorGUILayout.FloatField("Width", cell.width));
            cell.depth = Mathf.Max(2f, EditorGUILayout.FloatField("Depth", cell.depth));
            cell.height = Mathf.Max(3f, EditorGUILayout.FloatField("Room Height", cell.height));
            cell.wallHeight = Mathf.Max(2f, EditorGUILayout.FloatField("Wall Height", cell.wallHeight));
            cell.wallThickness = Mathf.Max(0.1f, EditorGUILayout.FloatField("Wall Thickness", cell.wallThickness));
            cell.doorwayWidth = Mathf.Max(0.8f, EditorGUILayout.FloatField("Doorway Width", cell.doorwayWidth));
            if (EditorGUI.EndChangeCheck())
                MarkSceneDirty(cell);

            EditorGUILayout.Space(6f);
            DrawSection("DOOR OPENINGS", "Openings are generated in the room walls.");
            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(cell, "Edit Cell Doorways");
            cell.doorways = DrawDoorToggles(cell.doorways);
            if (EditorGUI.EndChangeCheck())
                MarkSceneDirty(cell);

            EditorGUILayout.Space(6f);
            DrawSection("SURFACES", "Assign your own materials or keep the gothic defaults.");
            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(cell, "Edit Cell Materials");
            cell.floorMaterial = (Material)EditorGUILayout.ObjectField("Floor", cell.floorMaterial, typeof(Material), false);
            cell.wallMaterial = (Material)EditorGUILayout.ObjectField("Walls", cell.wallMaterial, typeof(Material), false);
            if (EditorGUI.EndChangeCheck())
                MarkSceneDirty(cell);

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Rebuild Floor and Walls", GUILayout.Height(34f)))
                RebuildCell(cell);

            EditorGUILayout.Space(8f);
            DrawSection("CONNECT A ROOM", "Create a snapped neighbor with matching door openings.");
            newWidth = Mathf.Max(2f, EditorGUILayout.FloatField("Neighbor Width", newWidth));
            newDepth = Mathf.Max(2f, EditorGUILayout.FloatField("Neighbor Depth", newDepth));
            newHeight = Mathf.Max(3f, EditorGUILayout.FloatField("Neighbor Height", newHeight));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("North")) CreateAdjacentCell(cell, CellSide.North);
            if (GUILayout.Button("East")) CreateAdjacentCell(cell, CellSide.East);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("West")) CreateAdjacentCell(cell, CellSide.West);
            if (GUILayout.Button("South")) CreateAdjacentCell(cell, CellSide.South);
            EditorGUILayout.EndHorizontal();
        }

        // Shows scene placement modes and the dimensions used for obstacles, ramps, and pits.
        private void DrawPlacePage()
        {
            LevelCell cell = SelectedCell;
            if (cell == null)
            {
                EditorGUILayout.HelpBox("Select a Level Cell before placing objects.", MessageType.Warning);
                return;
            }

            DrawSection("SCENE PLACEMENT", "Choose a tool, then click a surface inside the selected room. Pits cut into the ground floor.");
            snapStep = Mathf.Max(0.1f, EditorGUILayout.FloatField("Grid Snap", snapStep));
            EditorGUILayout.Space(6f);
            DrawModeButton(PlacementMode.Enemy, "Place Vampire Enemy", "Capsule enemy with the prototype enemy AI.");
            DrawModeButton(PlacementMode.Obstacle, "Place Obstacle", "Solid block with an adjustable size.");
            DrawModeButton(PlacementMode.Platform, "Place Raised Floor", "Solid walkable platform; set its top height to match a ramp.");
            DrawModeButton(PlacementMode.Ramp, "Place Ramp", "Walkable sloped ramp leading to a raised floor.");
            DrawModeButton(PlacementMode.Pit, "Place Pit", "Cuts an opening into the generated floor; falling into it defeats Valadrin.");

            EditorGUILayout.Space(8f);
            DrawSection("ACTIVE TOOL SETTINGS", "Settings apply to the next placed object.");
            obstacleSize = EditorGUILayout.Vector3Field("Obstacle Size", obstacleSize);
            platformSize = EditorGUILayout.Vector3Field("Raised Floor Size", platformSize);
            platformTopHeight = Mathf.Max(0.25f, EditorGUILayout.FloatField("Rise Above Surface", platformTopHeight));
            if (GUILayout.Button("Match Raised Floor Height to Ramp Rise"))
                platformTopHeight = rampRise;
            rampWidth = Mathf.Max(1f, EditorGUILayout.FloatField("Ramp Width", rampWidth));
            rampRun = Mathf.Max(1f, EditorGUILayout.FloatField("Ramp Run", rampRun));
            rampRise = Mathf.Max(0.5f, EditorGUILayout.FloatField("Ramp Rise", rampRise));
            rampYaw = EditorGUILayout.Slider("Ramp Direction", rampYaw, 0f, 270f);
            pitSize = EditorGUILayout.Vector2Field("Pit Width / Depth", pitSize);
            pitSize.x = Mathf.Max(0.5f, pitSize.x);
            pitSize.y = Mathf.Max(0.5f, pitSize.y);

            if (placementMode != PlacementMode.None)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox($"{placementMode} placement is active. Click inside {cell.name} in the Scene view. Press Escape to cancel.", MessageType.Info);
                if (GUILayout.Button("Finish Placement"))
                    placementMode = PlacementMode.None;
            }
        }

        // Draws a clear on/off button for one Scene view placement tool.
        private void DrawModeButton(PlacementMode mode, string label, string description)
        {
            bool active = placementMode == mode;
            Color previous = GUI.backgroundColor;
            if (active)
                GUI.backgroundColor = accent;
            if (GUILayout.Button(active ? "ACTIVE  ·  " + label : label, GUILayout.Height(30f)))
                placementMode = active ? PlacementMode.None : mode;
            GUI.backgroundColor = previous;
            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
        }

        // Edits the camera anchor pose and can copy or restore a camera composition.
        private void DrawCameraPage()
        {
            LevelCell cell = SelectedCell;
            if (cell == null)
            {
                EditorGUILayout.HelpBox("Select a Level Cell to edit its camera anchor.", MessageType.Warning);
                return;
            }

            DrawSection("CAMERA ANCHOR", "The main camera blends to this position and rotation when Valadrin enters the cell.");
            if (cell.cameraAnchor == null)
            {
                EditorGUILayout.HelpBox("This cell has no camera anchor yet.", MessageType.Warning);
                if (GUILayout.Button("Create Camera Anchor"))
                    CreateCameraAnchor(cell);
                return;
            }

            Transform anchor = cell.cameraAnchor;
            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(anchor, "Edit Camera Anchor");
            anchor.localPosition = EditorGUILayout.Vector3Field("Local Position", anchor.localPosition);
            anchor.localEulerAngles = EditorGUILayout.Vector3Field("Local Rotation", anchor.localEulerAngles);
            if (EditorGUI.EndChangeCheck())
                MarkSceneDirty(cell);

            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(cell, "Edit Camera Blend Time");
            cell.cameraTransitionSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Blend Time", cell.cameraTransitionSeconds));
            if (EditorGUI.EndChangeCheck())
                MarkSceneDirty(cell);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Copy Scene View Camera Pose"))
                CopySceneViewPoseToAnchor(cell);
            if (GUILayout.Button("Reset to Suggested Camera Angle"))
                ResetCameraAnchor(cell);

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Enable Cell Camera on Main Camera", GUILayout.Height(32f)))
                EnsureCameraRig();
            EditorGUILayout.HelpBox("The room camera changes while Valadrin is inside a cell. Frame the room from the Scene view, then copy that pose to the anchor.", MessageType.None);
        }

        // Displays the current placement instruction and access to the prototype room builder.
        private void DrawFooter()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene view is the canvas. Every tool supports Undo / Redo.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        // Draws a reusable section heading and its short explanatory caption.
        private void DrawSection(string title, string caption)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(caption, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        // Presents door-side toggles and returns the newly selected wall openings.
        private CellDoorSides DrawDoorToggles(CellDoorSides doors)
        {
            EditorGUILayout.BeginHorizontal();
            doors = DrawDoorToggle(doors, CellDoorSides.North, "North");
            doors = DrawDoorToggle(doors, CellDoorSides.East, "East");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            doors = DrawDoorToggle(doors, CellDoorSides.West, "West");
            doors = DrawDoorToggle(doors, CellDoorSides.South, "South");
            EditorGUILayout.EndHorizontal();
            return doors;
        }

        // Toggles one doorway flag while preserving the selected values for other walls.
        private CellDoorSides DrawDoorToggle(CellDoorSides doors, CellDoorSides side, string label)
        {
            bool enabled = (doors & side) != 0;
            bool next = GUILayout.Toggle(enabled, label, EditorStyles.miniButton, GUILayout.Height(26f));
            return next ? doors | side : doors & ~side;
        }

        // Creates a new room near the Scene view pivot, then generates its floor, walls, and anchor.
        private void CreateNewCell()
        {
            Vector3 position = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            position.y = 0f;
            LevelCell cell = CreateCellObject("Level Cell", position, Quaternion.identity, newWidth, newDepth, newHeight);
            Selection.activeGameObject = cell.gameObject;
            RebuildCell(cell);
            EnsureCameraRig();
            FrameCell(cell);
        }

        // Creates a neighboring cell with aligned edges and complementary doorway openings.
        private void CreateAdjacentCell(LevelCell source, CellSide side)
        {
            Vector3 offset;
            CellDoorSides sourceDoor;
            CellDoorSides neighborDoor;
            switch (side)
            {
                case CellSide.North:
                    offset = new Vector3(0f, 0f, (source.depth + newDepth) * 0.5f);
                    sourceDoor = CellDoorSides.North; neighborDoor = CellDoorSides.South; break;
                case CellSide.East:
                    offset = new Vector3((source.width + newWidth) * 0.5f, 0f, 0f);
                    sourceDoor = CellDoorSides.East; neighborDoor = CellDoorSides.West; break;
                case CellSide.South:
                    offset = new Vector3(0f, 0f, -(source.depth + newDepth) * 0.5f);
                    sourceDoor = CellDoorSides.South; neighborDoor = CellDoorSides.North; break;
                default:
                    offset = new Vector3(-(source.width + newWidth) * 0.5f, 0f, 0f);
                    sourceDoor = CellDoorSides.West; neighborDoor = CellDoorSides.East; break;
            }

            Vector3 position = source.transform.TransformPoint(offset);
            LevelCell neighbor = CreateCellObject("Level Cell", position, source.transform.rotation, newWidth, newDepth, newHeight);
            neighbor.doorways = neighborDoor;
            Undo.RecordObject(source, "Connect Level Cells");
            source.doorways |= sourceDoor;
            MarkSceneDirty(source);
            RebuildCell(source);
            RebuildCell(neighbor);
            Selection.activeGameObject = neighbor.gameObject;
            EnsureCameraRig();
            FrameCell(neighbor);
        }

        // Creates a cell root, its authoring component, room contents, generated geometry, and camera anchor.
        private static LevelCell CreateCellObject(string baseName, Vector3 position, Quaternion rotation, float width, float depth, float height)
        {
            GameObject root = new GameObject(baseName);
            Undo.RegisterCreatedObjectUndo(root, "Create Level Cell");
            root.transform.SetPositionAndRotation(position, rotation);
            LevelCell cell = Undo.AddComponent<LevelCell>(root);
            cell.width = width;
            cell.depth = depth;
            cell.height = height;
            cell.editorId = Guid.NewGuid().ToString("N");
            cell.floorMaterial = GetOrCreateMaterial("CellFloor", new Color(0.22f, 0.24f, 0.27f));
            cell.wallMaterial = GetOrCreateMaterial("CellWalls", new Color(0.12f, 0.14f, 0.18f));
            CreateChild(root.transform, "Cell Contents");
            CreateChild(root.transform, "Generated Geometry");
            CreateCameraAnchor(cell);
            EditorUtility.SetDirty(cell);
            EditorSceneManager.MarkSceneDirty(root.scene);
            return cell;
        }

        // Generates or refreshes the room floor and wall panels while preserving placed objects.
        private static void RebuildCell(LevelCell cell)
        {
            if (cell == null)
                return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Level Cell");
            Transform oldGeometry = cell.transform.Find("Generated Geometry");
            if (oldGeometry != null)
                Undo.DestroyObjectImmediate(oldGeometry.gameObject);

            Transform geometry = CreateChild(cell.transform, "Generated Geometry");
            Mesh floorMesh = BuildFloorMesh(cell);
            cell.generatedFloor = floorMesh;

            GameObject floor = CreateChild(geometry, "Cell Floor").gameObject;
            floor.AddComponent<MeshFilter>().sharedMesh = floorMesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = cell.floorMaterial;
            floor.AddComponent<MeshCollider>().sharedMesh = floorMesh;

            BuildWalls(cell, geometry);
            EditorUtility.SetDirty(cell);
            EditorSceneManager.MarkSceneDirty(cell.gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
        }

        // Builds a persistent top-surface mesh and omits tiles covered by authored pit openings.
        private static Mesh BuildFloorMesh(LevelCell cell)
        {
            EnsureFolder(MeshFolder);
            if (string.IsNullOrEmpty(cell.editorId))
                cell.editorId = Guid.NewGuid().ToString("N");
            string path = $"{MeshFolder}/{cell.editorId}_Floor.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null && IsFloorMeshUsedByAnotherCell(cell, mesh))
            {
                cell.editorId = Guid.NewGuid().ToString("N");
                path = $"{MeshFolder}/{cell.editorId}_Floor.asset";
                mesh = null;
            }
            if (mesh == null)
            {
                mesh = new Mesh { name = cell.name + " Floor Mesh", indexFormat = IndexFormat.UInt32 };
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(mesh, "Rebuild Cell Floor Mesh");
                mesh.Clear();
            }

            const float targetTileSize = 0.5f;
            int countX = Mathf.Clamp(Mathf.CeilToInt(cell.width / targetTileSize), 1, 128);
            int countZ = Mathf.Clamp(Mathf.CeilToInt(cell.depth / targetTileSize), 1, 128);
            float stepX = cell.width / countX;
            float stepZ = cell.depth / countZ;
            List<LevelCellPit> pits = FindPits(cell);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uv = new List<Vector2>();

            for (int z = 0; z < countZ; z++)
            {
                float z0 = -cell.depth * 0.5f + z * stepZ;
                float z1 = z0 + stepZ;
                for (int x = 0; x < countX; x++)
                {
                    float x0 = -cell.width * 0.5f + x * stepX;
                    float x1 = x0 + stepX;
                    if (TileOverlapsPit(cell, pits, x0, x1, z0, z1))
                        continue;

                    int first = vertices.Count;
                    vertices.Add(new Vector3(x0, 0f, z0));
                    vertices.Add(new Vector3(x1, 0f, z0));
                    vertices.Add(new Vector3(x1, 0f, z1));
                    vertices.Add(new Vector3(x0, 0f, z1));
                    uv.Add(new Vector2(x0, z0)); uv.Add(new Vector2(x1, z0));
                    uv.Add(new Vector2(x1, z1)); uv.Add(new Vector2(x0, z1));
                    triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 1);
                    triangles.Add(first + 1); triangles.Add(first + 3); triangles.Add(first + 2);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            EditorUtility.SetDirty(cell);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        // Prevents duplicated room objects from overwriting a floor mesh asset shared with another room.
        private static bool IsFloorMeshUsedByAnotherCell(LevelCell cell, Mesh mesh)
        {
            foreach (LevelCell other in FindObjectsByType<LevelCell>())
            {
                if (other != cell && other.generatedFloor == mesh)
                    return true;
            }
            return false;
        }

        // Returns all pit markers belonging to this room's authored content hierarchy.
        private static List<LevelCellPit> FindPits(LevelCell cell)
        {
            var pits = new List<LevelCellPit>();
            Transform content = cell.transform.Find("Cell Contents");
            if (content != null)
                pits.AddRange(content.GetComponentsInChildren<LevelCellPit>(true));
            return pits;
        }

        // Tests whether one floor tile overlaps any room-local pit footprint.
        private static bool TileOverlapsPit(LevelCell cell, List<LevelCellPit> pits, float x0, float x1, float z0, float z1)
        {
            foreach (LevelCellPit pit in pits)
            {
                Vector3 center = cell.transform.InverseTransformPoint(pit.transform.position);
                float halfX = pit.pitWidth * 0.5f;
                float halfZ = pit.pitDepth * 0.5f;
                if (x0 < center.x + halfX - 0.001f && x1 > center.x - halfX + 0.001f
                    && z0 < center.z + halfZ - 0.001f && z1 > center.z - halfZ + 0.001f)
                    return true;
            }
            return false;
        }

        // Creates perimeter panels and leaves a walk-through opening wherever the cell has a door flag.
        private static void BuildWalls(LevelCell cell, Transform geometry)
        {
            float doorHeight = Mathf.Min(3f, cell.wallHeight - 0.2f);
            BuildWallSide(cell, geometry, "North Wall", CellDoorSides.North, cell.width, true, cell.depth * 0.5f, doorHeight);
            BuildWallSide(cell, geometry, "South Wall", CellDoorSides.South, cell.width, true, -cell.depth * 0.5f, doorHeight);
            BuildWallSide(cell, geometry, "East Wall", CellDoorSides.East, cell.depth, false, cell.width * 0.5f, doorHeight);
            BuildWallSide(cell, geometry, "West Wall", CellDoorSides.West, cell.depth, false, -cell.width * 0.5f, doorHeight);
        }

        // Creates either one full wall or two wall piers and a header around a doorway.
        private static void BuildWallSide(LevelCell cell, Transform parent, string label, CellDoorSides side, float span, bool horizontal, float edge, float doorHeight)
        {
            bool hasDoor = (cell.doorways & side) != 0;
            float thickness = cell.wallThickness;
            float doorWidth = Mathf.Min(cell.doorwayWidth, span - thickness * 2f);
            if (!hasDoor || doorWidth < 0.8f || doorWidth >= span - thickness)
            {
                Vector3 position = horizontal ? new Vector3(0f, cell.wallHeight * 0.5f, edge) : new Vector3(edge, cell.wallHeight * 0.5f, 0f);
                Vector3 size = horizontal ? new Vector3(span, cell.wallHeight, thickness) : new Vector3(thickness, cell.wallHeight, span);
                CreateBlock(label, position, size, cell.wallMaterial, parent);
                return;
            }

            float pierSpan = (span - doorWidth) * 0.5f;
            float center = (doorWidth + pierSpan) * 0.5f;
            Vector3 firstPosition = horizontal ? new Vector3(-center, cell.wallHeight * 0.5f, edge) : new Vector3(edge, cell.wallHeight * 0.5f, -center);
            Vector3 secondPosition = horizontal ? new Vector3(center, cell.wallHeight * 0.5f, edge) : new Vector3(edge, cell.wallHeight * 0.5f, center);
            Vector3 pierSize = horizontal ? new Vector3(pierSpan, cell.wallHeight, thickness) : new Vector3(thickness, cell.wallHeight, pierSpan);
            CreateBlock(label + " Left", firstPosition, pierSize, cell.wallMaterial, parent);
            CreateBlock(label + " Right", secondPosition, pierSize, cell.wallMaterial, parent);

            float headerHeight = cell.wallHeight - doorHeight;
            if (headerHeight > 0.1f)
            {
                Vector3 headerPosition = horizontal
                    ? new Vector3(0f, doorHeight + headerHeight * 0.5f, edge)
                    : new Vector3(edge, doorHeight + headerHeight * 0.5f, 0f);
                Vector3 headerSize = horizontal
                    ? new Vector3(doorWidth, headerHeight, thickness)
                    : new Vector3(thickness, headerHeight, doorWidth);
                CreateBlock(label + " Header", headerPosition, headerSize, cell.wallMaterial, parent);
            }
        }

        // Creates and colors one editable cube panel under the generated geometry group.
        private static GameObject CreateBlock(string name, Vector3 localPosition, Vector3 localSize, Material material, Transform parent)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(block, "Generate Cell Wall");
            Undo.SetTransformParent(block.transform, parent, "Parent Cell Wall");
            block.name = name;
            block.transform.localPosition = localPosition;
            block.transform.localRotation = Quaternion.identity;
            block.transform.localScale = localSize;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }

        // Creates a child transform using Unity Undo so the hierarchy remains editable.
        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create Cell Object");
            Undo.SetTransformParent(child.transform, parent, "Parent Cell Object");
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child.transform;
        }

        // Creates a camera anchor at a useful default angle without overwriting existing user poses.
        private static void CreateCameraAnchor(LevelCell cell)
        {
            if (cell.cameraAnchor != null)
                return;

            Transform anchor = CreateChild(cell.transform, "Camera Anchor");
            anchor.localPosition = new Vector3(0f, Mathf.Max(8f, cell.depth * 0.5f), -cell.depth * 0.8f);
            Vector3 lookTarget = new Vector3(0f, cell.height * 0.25f, 0f);
            anchor.localRotation = Quaternion.LookRotation(lookTarget - anchor.localPosition, Vector3.up);
            Undo.RecordObject(cell, "Assign Cell Camera Anchor");
            cell.cameraAnchor = anchor;
            EditorUtility.SetDirty(cell);
        }

        // Copies the active Scene view camera position and orientation onto the selected room anchor.
        private static void CopySceneViewPoseToAnchor(LevelCell cell)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null || cell.cameraAnchor == null)
                return;
            Undo.RecordObject(cell.cameraAnchor, "Set Cell Camera Pose");
            cell.cameraAnchor.SetPositionAndRotation(sceneView.camera.transform.position, sceneView.camera.transform.rotation);
            MarkSceneDirty(cell);
        }

        // Restores the default high-angle view looking toward the center of the room.
        private static void ResetCameraAnchor(LevelCell cell)
        {
            CreateCameraAnchor(cell);
            Undo.RecordObject(cell.cameraAnchor, "Reset Cell Camera Anchor");
            cell.cameraAnchor.localPosition = new Vector3(0f, Mathf.Max(8f, cell.depth * 0.5f), -cell.depth * 0.8f);
            Vector3 lookTarget = new Vector3(0f, cell.height * 0.25f, 0f);
            cell.cameraAnchor.localRotation = Quaternion.LookRotation(lookTarget - cell.cameraAnchor.localPosition, Vector3.up);
            MarkSceneDirty(cell);
        }

        // Adds the room-aware camera mover to the active Main Camera if it is not already present.
        private static void EnsureCameraRig()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                mainCamera = FindAnyObjectByType<Camera>();
            if (mainCamera == null)
                return;
            if (mainCamera.GetComponent<CellCameraController>() == null)
                Undo.AddComponent<CellCameraController>(mainCamera.gameObject);
            EditorSceneManager.MarkSceneDirty(mainCamera.gameObject.scene);
        }

        // Creates one enemy, obstacle, ramp, or pit at the snapped Scene view position.
        private void PlaceObject(PlacementMode mode, LevelCell cell, Vector3 localPosition)
        {
            Transform contents = cell.transform.Find("Cell Contents");
            if (contents == null)
                contents = CreateChild(cell.transform, "Cell Contents");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Place " + mode);
            switch (mode)
            {
                case PlacementMode.Enemy: PlaceEnemy(contents, localPosition); break;
                case PlacementMode.Obstacle: PlaceObstacle(contents, localPosition); break;
                case PlacementMode.Platform: PlacePlatform(contents, localPosition); break;
                case PlacementMode.Ramp: PlaceRamp(contents, localPosition, cell); break;
                case PlacementMode.Pit: PlacePit(contents, localPosition, cell); break;
            }

            Undo.CollapseUndoOperations(undoGroup);
            MarkSceneDirty(cell);
            SceneView.RepaintAll();
        }

        // Places a capsule vampire with the prototype enemy behavior at floor level.
        private static void PlaceEnemy(Transform parent, Vector3 localPosition)
        {
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(enemy, "Place Vampire Enemy");
            Undo.SetTransformParent(enemy.transform, parent, "Parent Vampire Enemy");
            enemy.name = "Vampire Thrall";
            enemy.transform.localPosition = new Vector3(localPosition.x, localPosition.y + 1f, localPosition.z);
            enemy.transform.localScale = new Vector3(0.9f, 1.05f, 0.9f);
            enemy.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("CellEnemy", new Color(0.42f, 0.16f, 0.2f));
            Undo.AddComponent<EnemyController>(enemy);
        }

        // Places a solid adjustable stone block centered on the cell floor.
        private void PlaceObstacle(Transform parent, Vector3 localPosition)
        {
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(obstacle, "Place Cell Obstacle");
            Undo.SetTransformParent(obstacle.transform, parent, "Parent Cell Obstacle");
            obstacle.name = "Stone Obstacle";
            obstacleSize = new Vector3(Mathf.Max(0.25f, obstacleSize.x), Mathf.Max(0.25f, obstacleSize.y), Mathf.Max(0.25f, obstacleSize.z));
            obstacle.transform.localPosition = new Vector3(localPosition.x, localPosition.y + obstacleSize.y * 0.5f, localPosition.z);
            obstacle.transform.localScale = obstacleSize;
            obstacle.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("CellObstacle", new Color(0.3f, 0.29f, 0.28f));
        }

        // Places a solid elevated floor slab with its top surface at the chosen height.
        private void PlacePlatform(Transform parent, Vector3 localPosition)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(platform, "Place Raised Cell Floor");
            Undo.SetTransformParent(platform.transform, parent, "Parent Raised Cell Floor");
            platform.name = "Raised Floor Platform";
            platformSize = new Vector3(Mathf.Max(0.5f, platformSize.x), Mathf.Max(0.25f, platformSize.y), Mathf.Max(0.5f, platformSize.z));
            platform.transform.localPosition = new Vector3(localPosition.x, localPosition.y + platformTopHeight - platformSize.y * 0.5f, localPosition.z);
            platform.transform.localScale = platformSize;
            platform.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("CellPlatform", new Color(0.24f, 0.25f, 0.29f));
        }

        // Places a sloped mesh and matching mesh collider for walking to a raised floor.
        private void PlaceRamp(Transform parent, Vector3 localPosition, LevelCell cell)
        {
            GameObject rampObject = new GameObject("Stone Ramp");
            Undo.RegisterCreatedObjectUndo(rampObject, "Place Cell Ramp");
            Undo.SetTransformParent(rampObject.transform, parent, "Parent Cell Ramp");
            rampObject.transform.localPosition = localPosition;
            rampObject.transform.localRotation = Quaternion.Euler(0f, rampYaw, 0f);
            CellRamp ramp = Undo.AddComponent<CellRamp>(rampObject);
            ramp.width = rampWidth;
            ramp.run = rampRun;
            ramp.rise = rampRise;
            Mesh mesh = CreateRampMesh(cell.editorId, ramp.width, ramp.run, ramp.rise);
            ramp.generatedMesh = mesh;
            rampObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            rampObject.AddComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("CellRamp", new Color(0.28f, 0.3f, 0.34f));
            rampObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // Creates and saves the six-sided wedge mesh used by a placed ramp.
        private static Mesh CreateRampMesh(string cellId, float width, float run, float rise)
        {
            EnsureFolder(MeshFolder);
            string path = $"{MeshFolder}/{cellId}_{Guid.NewGuid():N}_Ramp.asset";
            var mesh = new Mesh { name = "Stone Ramp Mesh" };
            float halfWidth = Mathf.Max(1f, width) * 0.5f;
            float halfRun = Mathf.Max(1f, run) * 0.5f;
            float rampHeight = Mathf.Max(0.5f, rise);
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, 0f, -halfRun), new Vector3(halfWidth, 0f, -halfRun),
                new Vector3(-halfWidth, 0f, halfRun), new Vector3(halfWidth, 0f, halfRun),
                new Vector3(-halfWidth, rampHeight, halfRun), new Vector3(halfWidth, rampHeight, halfRun)
            };
            mesh.triangles = new[] { 0, 1, 2, 1, 3, 2, 0, 4, 1, 1, 4, 5, 0, 2, 4, 1, 5, 3, 2, 3, 4, 3, 5, 4 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // Places a floor opening, a dark bottom, and a trigger that defeats the player on falling in.
        private void PlacePit(Transform parent, Vector3 localPosition, LevelCell cell)
        {
            GameObject pit = new GameObject("Pit Opening");
            Undo.RegisterCreatedObjectUndo(pit, "Place Cell Pit");
            Undo.SetTransformParent(pit.transform, parent, "Parent Cell Pit");
            pit.transform.localPosition = new Vector3(localPosition.x, 0f, localPosition.z);
            LevelCellPit marker = Undo.AddComponent<LevelCellPit>(pit);
            marker.pitWidth = Mathf.Min(Mathf.Max(0.5f, pitSize.x), cell.width);
            marker.pitDepth = Mathf.Min(Mathf.Max(0.5f, pitSize.y), cell.depth);

            GameObject bottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(bottom, "Create Pit Bottom");
            Undo.SetTransformParent(bottom.transform, pit.transform, "Parent Pit Bottom");
            bottom.name = "Dark Pit Bottom";
            bottom.transform.localPosition = new Vector3(0f, -3.1f, 0f);
            bottom.transform.localScale = new Vector3(marker.pitWidth, 0.2f, marker.pitDepth);
            bottom.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("CellPit", new Color(0.025f, 0.018f, 0.035f));

            GameObject trigger = new GameObject("Fall Trigger");
            Undo.RegisterCreatedObjectUndo(trigger, "Create Pit Trigger");
            Undo.SetTransformParent(trigger.transform, pit.transform, "Parent Pit Trigger");
            trigger.transform.localPosition = new Vector3(0f, -1.5f, 0f);
            BoxCollider collider = Undo.AddComponent<BoxCollider>(trigger);
            collider.isTrigger = true;
            collider.size = new Vector3(marker.pitWidth, 3f, marker.pitDepth);
            Rigidbody triggerBody = Undo.AddComponent<Rigidbody>(trigger);
            triggerBody.isKinematic = true;
            triggerBody.useGravity = false;
            Undo.AddComponent<CellPitHazard>(trigger);
            RebuildCell(cell);
        }

        // Reads Scene view clicks against the selected cell floor and places the active tool.
        private void OnSceneGUI(SceneView sceneView)
        {
            if (placementMode == PlacementMode.None || SelectedCell == null)
                return;

            LevelCell cell = SelectedCell;
            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                placementMode = PlacementMode.None;
                current.Use();
                Repaint();
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Vector3 localPoint;
            if (placementMode != PlacementMode.Pit && TryGetWalkablePlacementPoint(ray, cell, out Vector3 surfacePoint))
            {
                localPoint = cell.transform.InverseTransformPoint(surfacePoint);
            }
            else
            {
                Plane floorPlane = new Plane(cell.transform.up, cell.transform.position);
                if (!floorPlane.Raycast(ray, out float distance))
                    return;
                localPoint = cell.transform.InverseTransformPoint(ray.GetPoint(distance));
                localPoint.y = 0f;
            }

            if (Mathf.Abs(localPoint.x) > cell.width * 0.5f || Mathf.Abs(localPoint.z) > cell.depth * 0.5f)
                return;

            localPoint.x = Mathf.Clamp(Mathf.Round(localPoint.x / snapStep) * snapStep, -cell.width * 0.5f + 0.25f, cell.width * 0.5f - 0.25f);
            localPoint.z = Mathf.Clamp(Mathf.Round(localPoint.z / snapStep) * snapStep, -cell.depth * 0.5f + 0.25f, cell.depth * 0.5f - 0.25f);
            Vector3 snappedWorldPoint = cell.transform.TransformPoint(localPoint);
            Handles.color = accent;
            Handles.DrawWireDisc(snappedWorldPoint + cell.transform.up * 0.025f, cell.transform.up, 0.35f);
            Handles.Label(snappedWorldPoint + cell.transform.up * 0.08f, placementMode.ToString());
            sceneView.Repaint();
            if (!current.alt)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                if (placementMode == PlacementMode.Pit)
                    localPoint.y = 0f;
                PlaceObject(placementMode, cell, localPoint);
                current.Use();
            }
        }

        // Finds the top face of a floor or raised platform for object placement above ground level.
        private static bool TryGetWalkablePlacementPoint(Ray ray, LevelCell cell, out Vector3 point)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && Vector3.Dot(hit.normal, cell.transform.up) > 0.5f)
            {
                Vector3 localHit = cell.transform.InverseTransformPoint(hit.point);
                if (Mathf.Abs(localHit.x) <= cell.width * 0.5f && Mathf.Abs(localHit.z) <= cell.depth * 0.5f)
                {
                    point = hit.point;
                    return true;
                }
            }

            point = Vector3.zero;
            return false;
        }

        // Marks the selected room scene dirty after a serialized authoring value changes.
        private static void MarkSceneDirty(LevelCell cell)
        {
            EditorUtility.SetDirty(cell);
            EditorSceneManager.MarkSceneDirty(cell.gameObject.scene);
        }

        // Focuses the Scene view on a newly created room module.
        private static void FrameCell(LevelCell cell)
        {
            Selection.activeGameObject = cell.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        // Creates a reusable material asset or returns the existing material with the same name.
        private static Material GetOrCreateMaterial(string name, Color color)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Creates each required generated-asset folder with Unity's AssetDatabase.
        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
