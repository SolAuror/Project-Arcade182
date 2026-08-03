using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sol.Editor
{
    [CustomEditor(typeof(BuildingComponent))]
    [CanEditMultipleObjects]
    public sealed class BuildingComponentEditor : UnityEditor.Editor
    {
        private static readonly Room3D.Directions[] HorizontalDirections =
        {
            Room3D.Directions.NORTH,
            Room3D.Directions.SOUTH,
            Room3D.Directions.EAST,
            Room3D.Directions.WEST,
        };

        private Vector3Int selectedCoordinate;
        private Room3D.Directions selectedFace = Room3D.Directions.NORTH;
        private bool setupFoldout = true;
        private bool roofKitFoldout;
        private bool generatorFoldout = true;
        private bool authoringFoldout = true;
        private bool dressingFoldout = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawSetupProperties();
            DrawGeneratorProperties();
            serializedObject.ApplyModifiedProperties();

            if (targets.Length == 1)
            {
                BuildingComponent building = (BuildingComponent)target;
                DrawGeneratorActions(building);

                authoringFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                    authoringFoldout,
                    "Manual Authoring");
                if (authoringFoldout)
                {
                    DrawCellAuthoring(building);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                serializedObject.Update();
                DrawDressingProperties();
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Cell and face authoring is available when one building is selected.",
                    MessageType.Info);
                serializedObject.Update();
                DrawDressingProperties();
                serializedObject.ApplyModifiedProperties();
            }

            if (dressingFoldout)
            {
                DrawWholeBuildingDressing();
            }
        }

        private void DrawSetupProperties()
        {
            setupFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                setupFoldout,
                "Building Kit");
            if (setupFoldout)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cellPrefab"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("upperCellPrefab"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("halfCellPrefab"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cellSpacing"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("halfCellHeight"));

                roofKitFoldout = EditorGUILayout.Foldout(
                    roofKitFoldout,
                    "RoofCell Prefabs",
                    true);
                if (roofKitFoldout)
                {
                    EditorGUI.indentLevel++;
                    DrawProperty("roofCellSloped");
                    DrawProperty("roofCellStepped");
                    DrawProperty("roofCellLeft");
                    DrawProperty("roofCellRight");
                    DrawProperty("roofCellLeftCurve");
                    DrawProperty("roofCellRightCurve");
                    DrawProperty("roofCellBlock");
                    DrawProperty("roofCellHalfBlock");
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawGeneratorProperties()
        {
            generatorFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                generatorFoldout,
                "Random Generator");
            if (generatorFoldout)
            {
                DrawProperty(
                    "generationWidth",
                    "Maximum Width",
                    "Maximum X footprint. Generated buildings may use less.");
                DrawProperty(
                    "generationLength",
                    "Maximum Length",
                    "Maximum Z footprint. Generated buildings may use less.");
                DrawProperty(
                    "generationHeightLimit",
                    "Maximum Storeys",
                    "Maximum full-storey height for towers and wings.");
                DrawProperty(
                    "generationEntranceCount",
                    "Entrance Count",
                    "Number of entrances selected from the grown perimeter.");
                DrawProperty(
                    "generationHalfLayerChance",
                    "Half-Top Chance",
                    "Chance to add one small connected half-height crown.");
                DrawProperty(
                    "generationSeed",
                    "Seed",
                    "Deterministic massing, roof, entrance, and dressing seed.");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDressingProperties()
        {
            dressingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                dressingFoldout,
                "Dressing");
            if (dressingFoldout)
            {
                DrawProperty("dressingSeed");
                DrawProperty("dressOnAwake");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawProperty(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property);
            }
        }

        private void DrawProperty(
            string propertyName,
            string label,
            string tooltip)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(
                    property,
                    new GUIContent(label, tooltip));
            }
        }

        private void DrawGeneratorActions(BuildingComponent building)
        {
            if (!generatorFoldout)
            {
                return;
            }

            bool missingRequiredKit =
                building.CellPrefab == null
                || building.UpperCellPrefab == null
                || !building.HasCompleteRoofCellKit
                || (building.GenerationHalfLayerChance > 0f
                    && building.HalfCellPrefab == null);

            EditorGUILayout.HelpBox(
                "Width, length, and storeys are maximum bounds. The seed grows a connected L, T, tower-house, or organic footprint inside them. Generation replaces registered cells and is fully Undo-able.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(missingRequiredKit))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Generate Building", GUILayout.Height(30f)))
                {
                    RunGenerator(building, false);
                }

                if (GUILayout.Button("Reroll Building", GUILayout.Height(30f)))
                {
                    RunGenerator(building, true);
                }
                EditorGUILayout.EndHorizontal();
            }

            if (missingRequiredKit)
            {
                EditorGUILayout.HelpBox(
                    "Assign the ground, upper, complete RoofCell kit, and enabled half-cell prefab before generating.",
                    MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(building.AuthoredCellCount == 0))
            {
                if (GUILayout.Button("Save As New Building Prefab..."))
                {
                    GameObject prefab =
                        BuildingGeneratorUtility.SaveAsPrefab(building);
                    if (prefab != null)
                    {
                        EditorGUIUtility.PingObject(prefab);
                        Selection.activeObject = prefab;
                    }
                }
            }

            EditorGUILayout.Space();
        }

        private void RunGenerator(
            BuildingComponent building,
            bool advanceSeed)
        {
            BuildingGeneratorUtility.Result result =
                BuildingGeneratorUtility.Generate(building, advanceSeed);

            if (result.Entrances.Count > 0)
            {
                selectedCoordinate = result.Entrances[0].Coordinate;
                selectedFace = result.Entrances[0].Face;
            }
            else
            {
                selectedCoordinate = Vector3Int.zero;
            }

            Selection.activeGameObject = building.gameObject;
            SceneView.RepaintAll();
            Repaint();
        }
        private void DrawCellAuthoring(BuildingComponent building)
        {
            EditorGUILayout.LabelField("Building Authoring", EditorStyles.boldLabel);

            if (building.HalfCellPrefab == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign UpperCell_Half to enable horizontal half-wall layers.",
                    MessageType.Warning);
                if (GUILayout.Button("Assign Labyrinth Half Cell"))
                {
                    AssignDefaultHalfCell(building);
                }
            }

            Room3D[] rooms =
                BuildingGeneratorUtility.GetAuthorableRooms(building).ToArray();
            int unregistered = Mathf.Max(0, rooms.Length - building.AuthoredCellCount);
            if (unregistered > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{unregistered} existing Room3D cell{(unregistered == 1 ? " is" : "s are")} not registered. " +
                    "Register them once so the grid controls can navigate the copied cells.",
                    MessageType.Info);

                if (GUILayout.Button("Register Existing Cells"))
                {
                    TryLearnCellPrefab(rooms);
                    Undo.RecordObject(building, "Register Building Cells");
                    int registered = building.RegisterExistingCells();
                    MarkDirty(building);
                    Debug.Log($"{building.name}: registered {registered} existing building cell(s).", building);
                }
            }

            if (building.AuthoredCellCount > 1
                && GUILayout.Button("Open All Shared Cell Edges"))
            {
                OpenAllSharedEdges(building);
            }

            DrawRegisteredCellPicker(building);

            EditorGUI.BeginChangeCheck();
            Vector3Int editedCoordinate =
                EditorGUILayout.Vector3IntField("Grid Coordinate", selectedCoordinate);
            if (EditorGUI.EndChangeCheck())
            {
                selectedCoordinate = editedCoordinate;
                SceneView.RepaintAll();
            }

            bool hasSelectedCell = building.TryGetCell(selectedCoordinate, out Room3D selectedRoom);

            EditorGUILayout.HelpBox(
                hasSelectedCell
                    ? $"ACTIVE CELL  ({selectedCoordinate.x}, {selectedCoordinate.y}, {selectedCoordinate.z})  —  {selectedRoom.name}"
                    : $"EMPTY GRID POSITION  ({selectedCoordinate.x}, {selectedCoordinate.y}, {selectedCoordinate.z})",
                hasSelectedCell ? MessageType.Info : MessageType.Warning);

            if (hasSelectedCell
                && building.TryGetCellLayer(
                    selectedCoordinate,
                    out BuildingComponent.CellLayerType selectedLayerType))
            {
                EditorGUILayout.LabelField("Layer Height", selectedLayerType.ToString());
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Cell Object",
                    selectedRoom,
                    typeof(Room3D),
                    true);
            }

            DrawCellNavigation(building, hasSelectedCell);

            if (!hasSelectedCell)
            {
                GameObject selectedCellPrefab = building.CellPrefabForCoordinate(
                    selectedCoordinate,
                    BuildingComponent.CellLayerType.Full);
                GameObject selectedHalfPrefab = building.CellPrefabForCoordinate(
                    selectedCoordinate,
                    BuildingComponent.CellLayerType.Half);
                EditorGUILayout.HelpBox(
                    selectedCellPrefab == null && selectedHalfPrefab == null
                        ? "Assign a full or half Cell Prefab above, then add this layer."
                        : "There is no cell at this coordinate.",
                    selectedCellPrefab == null && selectedHalfPrefab == null
                        ? MessageType.Warning
                        : MessageType.None);

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(selectedCellPrefab == null))
                {
                    if (GUILayout.Button("Add Full Cell Here", GUILayout.Height(25f)))
                    {
                        AddCell(
                            building,
                            selectedCoordinate,
                            BuildingComponent.CellLayerType.Full);
                    }
                }

                using (new EditorGUI.DisabledScope(selectedHalfPrefab == null))
                {
                    if (GUILayout.Button("Add Half Cell Here", GUILayout.Height(25f)))
                    {
                        AddCell(
                            building,
                            selectedCoordinate,
                            BuildingComponent.CellLayerType.Half);
                    }
                }
                EditorGUILayout.EndHorizontal();

                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Frame Active Cell"))
            {
                FrameCell(building, selectedCoordinate);
            }

            if (GUILayout.Button("Select Cell Object"))
            {
                Selection.activeGameObject = selectedRoom.gameObject;
                EditorGUIUtility.PingObject(selectedRoom.gameObject);
            }
            EditorGUILayout.EndHorizontal();

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("Remove Active Cell", GUILayout.Height(28f)))
            {
                RemoveCell(building, selectedCoordinate, selectedRoom);
                GUI.backgroundColor = previousBackground;
                return;
            }
            GUI.backgroundColor = previousBackground;

            EditorGUILayout.Space();
            DrawFaceAuthoring(building);
            EditorGUILayout.Space();
            DrawRoofAuthoring(building);
        }

        private void DrawRegisteredCellPicker(BuildingComponent building)
        {
            List<BuildingComponent.AuthoredCell> cells =
                new List<BuildingComponent.AuthoredCell>();
            foreach (BuildingComponent.AuthoredCell cell in building.AuthoredCells)
            {
                if (cell != null && cell.Room != null)
                {
                    cells.Add(cell);
                }
            }

            cells.Sort((a, b) =>
            {
                int floor = a.Coordinate.y.CompareTo(b.Coordinate.y);
                if (floor != 0) return floor;
                int row = a.Coordinate.z.CompareTo(b.Coordinate.z);
                return row != 0 ? row : a.Coordinate.x.CompareTo(b.Coordinate.x);
            });

            string[] options = new string[cells.Count + 1];
            options[0] = "Choose a registered cell…";
            int activeIndex = 0;

            for (int i = 0; i < cells.Count; i++)
            {
                Vector3Int coordinate = cells[i].Coordinate;
                options[i + 1] =
                    $"({coordinate.x}, {coordinate.y}, {coordinate.z})  " +
                    $"[{cells[i].LayerType}]  {cells[i].Room.name}";
                if (coordinate == selectedCoordinate)
                {
                    activeIndex = i + 1;
                }
            }

            int picked = EditorGUILayout.Popup("Active Cell", activeIndex, options);
            if (picked > 0 && picked != activeIndex)
            {
                selectedCoordinate = cells[picked - 1].Coordinate;
                SceneView.RepaintAll();
            }
        }

        private void DrawCellNavigation(BuildingComponent building, bool hasSelectedCell)
        {
            EditorGUILayout.LabelField("Add / Select Neighbour", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Full and half layers stack by their real height. Adding vertically keeps the roof and moves it onto the new top.",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawNavigateButton(building, new Vector3Int(0, 0, 1), "North  +Z", hasSelectedCell);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawNavigateButton(building, new Vector3Int(-1, 0, 0), "West  -X", hasSelectedCell);
            DrawNavigateButton(building, new Vector3Int(1, 0, 0), "East  +X", hasSelectedCell);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawNavigateButton(building, new Vector3Int(0, 0, -1), "South  -Z", hasSelectedCell);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawVerticalAddButton(
                building,
                1,
                BuildingComponent.CellLayerType.Full,
                "Full Above",
                hasSelectedCell);
            DrawVerticalAddButton(
                building,
                1,
                BuildingComponent.CellLayerType.Half,
                "Half Above",
                hasSelectedCell);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawVerticalAddButton(
                building,
                -1,
                BuildingComponent.CellLayerType.Full,
                "Full Below",
                hasSelectedCell);
            DrawVerticalAddButton(
                building,
                -1,
                BuildingComponent.CellLayerType.Half,
                "Half Below",
                hasSelectedCell);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVerticalAddButton(
            BuildingComponent building,
            int yDirection,
            BuildingComponent.CellLayerType layerType,
            string label,
            bool hasSelectedCell)
        {
            Vector3Int destination = selectedCoordinate;
            do
            {
                destination.y += yDirection;
            }
            while (building.TryGetCell(destination, out _));

            using (new EditorGUI.DisabledScope(
                       !hasSelectedCell
                       || building.CellPrefabForCoordinate(destination, layerType) == null))
            {
                if (GUILayout.Button($"{label}  →  Y {destination.y}"))
                {
                    AddCell(building, destination, layerType);
                    selectedCoordinate = destination;
                    SceneView.RepaintAll();
                }
            }
        }

        private void DrawNavigateButton(
            BuildingComponent building,
            Vector3Int offset,
            string label,
            bool hasSelectedCell)
        {
            Vector3Int destination = selectedCoordinate + offset;
            bool destinationExists = building.TryGetCell(destination, out _);
            string action = destinationExists ? "Select" : "Add";
            BuildingComponent.CellLayerType layerType =
                building.TryGetCellLayer(
                    selectedCoordinate,
                    out BuildingComponent.CellLayerType selectedLayer)
                    ? selectedLayer
                    : BuildingComponent.CellLayerType.Full;

            using (new EditorGUI.DisabledScope(
                       !destinationExists
                       && (!hasSelectedCell
                           || building.CellPrefabForCoordinate(destination, layerType) == null)))
            {
                if (GUILayout.Button($"{action} {label}", GUILayout.MinWidth(112f)))
                {
                    if (!destinationExists)
                    {
                        AddCell(building, destination, layerType);
                    }

                    selectedCoordinate = destination;
                    SceneView.RepaintAll();
                }
            }
        }

        private void OnSceneGUI()
        {
            BuildingComponent building = (BuildingComponent)target;
            Color previousColor = Handles.color;
            Matrix4x4 previousMatrix = Handles.matrix;
            CompareFunction previousZTest = Handles.zTest;

            foreach (BuildingComponent.AuthoredCell cell in building.AuthoredCells)
            {
                if (cell == null || cell.Room == null)
                {
                    continue;
                }

                float height = building.GetCellHeight(cell.LayerType);
                Vector3 localCenter =
                    cell.Room.transform.localPosition + Vector3.up * (height * 0.5f);
                Vector3 worldCenter = building.transform.TransformPoint(localCenter);
                float handleSize =
                    HandleUtility.GetHandleSize(worldCenter)
                    * (cell.Coordinate == selectedCoordinate ? 0.16f : 0.105f);

                Handles.color =
                    cell.Coordinate == selectedCoordinate
                        ? new Color(1f, 0.75f, 0.08f, 1f)
                        : cell.LayerType == BuildingComponent.CellLayerType.Half
                            ? new Color(0.25f, 0.85f, 1f, 0.9f)
                            : new Color(0.35f, 0.65f, 1f, 0.82f);

                Handles.zTest = CompareFunction.LessEqual;
                Handles.matrix = building.transform.localToWorldMatrix;
                Handles.DrawWireCube(
                    localCenter,
                    new Vector3(
                        Mathf.Abs(building.CellSpacing.x),
                        height,
                        Mathf.Abs(building.CellSpacing.z)) - Vector3.one * 0.08f);
                Handles.matrix = previousMatrix;

                // Keep the selector visible through dense walls and roofs. The
                // larger target removes the need to hunt for the exact centre.
                Handles.zTest = CompareFunction.Always;
                if (Handles.Button(
                        worldCenter,
                        Quaternion.identity,
                        handleSize,
                        handleSize,
                        Handles.SphereHandleCap))
                {
                    selectedCoordinate = cell.Coordinate;
                    Repaint();
                    SceneView.RepaintAll();
                }

                if (cell.Coordinate == selectedCoordinate
                    || Event.current.shift
                    || building.AuthoredCellCount <= 32)
                {
                    Handles.Label(
                        worldCenter + Vector3.up * handleSize,
                        $"{cell.Coordinate.x},{cell.Coordinate.y},{cell.Coordinate.z} " +
                        $"{(cell.LayerType == BuildingComponent.CellLayerType.Half ? "½" : "F")}");
                }
            }

            if (!building.TryGetCellLayer(
                    selectedCoordinate,
                    out BuildingComponent.CellLayerType activeLayer)
                || !building.TryGetCell(selectedCoordinate, out Room3D room)
                || room == null)
            {
                Handles.color = previousColor;
                Handles.matrix = previousMatrix;
                Handles.zTest = previousZTest;
                return;
            }

            float activeHeight = building.GetCellHeight(activeLayer);
            Vector3 activeLocalCenter =
                room.transform.localPosition + Vector3.up * (activeHeight * 0.5f);
            Vector3 activeWorldCenter =
                building.transform.TransformPoint(activeLocalCenter);

            Vector3 activeSize = new Vector3(
                Mathf.Abs(building.CellSpacing.x),
                activeHeight,
                Mathf.Abs(building.CellSpacing.z));
            DrawActiveCellVolume(
                building.transform,
                activeLocalCenter,
                activeSize);

            Handles.zTest = CompareFunction.Always;
            float beaconSize =
                HandleUtility.GetHandleSize(activeWorldCenter) * 0.18f;
            Vector3 activeTop = building.transform.TransformPoint(
                activeLocalCenter + Vector3.up * (activeHeight * 0.5f));
            Vector3 beaconPosition =
                activeTop + building.transform.up * (beaconSize * 2.25f);
            Handles.color = new Color(1f, 0.58f, 0.03f, 1f);
            Handles.DrawAAPolyLine(5f, activeTop, beaconPosition);
            Handles.SphereHandleCap(
                0,
                beaconPosition,
                Quaternion.identity,
                beaconSize,
                EventType.Repaint);
            Handles.DrawWireDisc(
                activeTop,
                building.transform.up,
                beaconSize * 1.35f);

            foreach (Room3D.Directions face in HorizontalDirections)
            {
                Vector3 localDirection = LocalDirection(face);
                float extent =
                    Mathf.Abs(localDirection.x) > 0f
                        ? Mathf.Abs(building.CellSpacing.x) * 0.5f
                        : Mathf.Abs(building.CellSpacing.z) * 0.5f;
                Vector3 worldPosition = building.transform.TransformPoint(
                    activeLocalCenter + localDirection * (extent + 0.3f));
                float size = HandleUtility.GetHandleSize(worldPosition) * 0.075f;
                Handles.color =
                    face == selectedFace
                        ? new Color(1f, 0.45f, 0.05f, 1f)
                        : new Color(1f, 0.85f, 0.25f, 0.85f);

                if (Handles.Button(
                        worldPosition,
                        Quaternion.LookRotation(
                            WorldDirection(building.transform, face)),
                        size,
                        size,
                        Handles.RectangleHandleCap))
                {
                    selectedFace = face;
                    Repaint();
                    SceneView.RepaintAll();
                }

                Handles.Label(worldPosition + Vector3.up * size, face.ToString()[0].ToString());
            }

            Vector3 direction = WorldDirection(building.transform, selectedFace);
            float arrowSize =
                HandleUtility.GetHandleSize(activeWorldCenter) * 0.65f;
            Handles.color = new Color(1f, 0.45f, 0.05f, 1f);
            Handles.ArrowHandleCap(
                0,
                activeWorldCenter,
                Quaternion.LookRotation(direction),
                arrowSize,
                EventType.Repaint);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = new Color(1f, 0.82f, 0.1f);
            Handles.Label(
                beaconPosition + building.transform.up * (beaconSize * 0.8f),
                $"ACTIVE CELL ({selectedCoordinate.x}, {selectedCoordinate.y}, {selectedCoordinate.z})\nFace: {selectedFace}",
                labelStyle);
            Handles.color = previousColor;
            Handles.matrix = previousMatrix;
            Handles.zTest = previousZTest;
        }

        private static void DrawActiveCellVolume(
            Transform buildingTransform,
            Vector3 localCenter,
            Vector3 size)
        {
            Vector3 extents = size * 0.5f + Vector3.one * 0.045f;
            Vector3[] localCorners =
            {
                localCenter + new Vector3(-extents.x, -extents.y, -extents.z),
                localCenter + new Vector3(extents.x, -extents.y, -extents.z),
                localCenter + new Vector3(extents.x, -extents.y, extents.z),
                localCenter + new Vector3(-extents.x, -extents.y, extents.z),
                localCenter + new Vector3(-extents.x, extents.y, -extents.z),
                localCenter + new Vector3(extents.x, extents.y, -extents.z),
                localCenter + new Vector3(extents.x, extents.y, extents.z),
                localCenter + new Vector3(-extents.x, extents.y, extents.z),
            };
            Vector3[] corners = new Vector3[localCorners.Length];
            for (int i = 0; i < localCorners.Length; i++)
            {
                corners[i] = buildingTransform.TransformPoint(localCorners[i]);
            }

            Color fill = new Color(1f, 0.55f, 0.02f, 0.075f);
            Color outline = new Color(1f, 0.67f, 0.04f, 0.95f);
            int[,] faces =
            {
                { 0, 1, 2, 3 },
                { 4, 7, 6, 5 },
                { 0, 4, 5, 1 },
                { 1, 5, 6, 2 },
                { 2, 6, 7, 3 },
                { 3, 7, 4, 0 },
            };

            Handles.zTest = CompareFunction.Always;
            for (int face = 0; face < faces.GetLength(0); face++)
            {
                Handles.DrawSolidRectangleWithOutline(
                    new[]
                    {
                        corners[faces[face, 0]],
                        corners[faces[face, 1]],
                        corners[faces[face, 2]],
                        corners[faces[face, 3]],
                    },
                    fill,
                    outline);
            }

            int[,] edges =
            {
                { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
                { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
                { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
            };
            Handles.color = outline;
            for (int edge = 0; edge < edges.GetLength(0); edge++)
            {
                Handles.DrawAAPolyLine(
                    5f,
                    corners[edges[edge, 0]],
                    corners[edges[edge, 1]]);
            }
        }

        private static void FrameCell(
            BuildingComponent building,
            Vector3Int coordinate)
        {
            if (!building.TryGetCell(coordinate, out Room3D room)
                || room == null
                || !building.TryGetCellLayer(
                    coordinate,
                    out BuildingComponent.CellLayerType layerType))
            {
                return;
            }

            float height = building.GetCellHeight(layerType);
            Vector3 localCenter =
                room.transform.localPosition + Vector3.up * (height * 0.5f);
            Vector3 worldCenter =
                building.transform.TransformPoint(localCenter);
            Vector3 worldSize = new Vector3(
                Mathf.Abs(building.CellSpacing.x),
                height,
                Mathf.Abs(building.CellSpacing.z));
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.Frame(new Bounds(worldCenter, worldSize), false);
                sceneView.Repaint();
            }
        }

        private void DrawFaceAuthoring(BuildingComponent building)
        {
            EditorGUILayout.LabelField("Selected Cell Face", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            Room3D.Directions editedFace =
                (Room3D.Directions)EditorGUILayout.EnumPopup("Face", selectedFace);
            if (EditorGUI.EndChangeCheck())
            {
                selectedFace = editedFace;
                SceneView.RepaintAll();
            }

            if (selectedFace == Room3D.Directions.NONE)
            {
                selectedFace = Room3D.Directions.NORTH;
            }

            if (!building.TryGetWallSocket(selectedCoordinate, selectedFace, out WallSocket socket))
            {
                EditorGUILayout.HelpBox(
                    $"The {selectedFace} wall has no WallSocket. Use the socket-configured DungeonCell prefab.",
                    MessageType.Warning);
                return;
            }

            Vector3Int neighbourCoordinate = selectedCoordinate + DirectionOffset(selectedFace);
            bool hasNeighbour =
                building.TryGetCell(neighbourCoordinate, out _)
                && building.CanShareInteriorEdge(
                    selectedCoordinate,
                    neighbourCoordinate);
            bool verticallyStackedEntrance =
                socket.AuthoredType == WallSocket.AuthoredWallType.Entrance
                && (IsEntranceFaceAt(
                        building,
                        selectedCoordinate + Vector3Int.up,
                        selectedFace)
                    || IsEntranceFaceAt(
                        building,
                        selectedCoordinate + Vector3Int.down,
                        selectedFace));

            EditorGUILayout.LabelField("Current", Nicify(socket.AuthoredType));
            if (verticallyStackedEntrance)
            {
                EditorGUILayout.HelpBox(
                    "Vertical entrance stack detected. Lower floors use matching stacker piers and the top floor uses their arch.",
                    MessageType.Info);
            }

            if (hasNeighbour)
            {
                EditorGUILayout.HelpBox(
                    "This face touches another authored cell. Use Interior Open for a continuous room, " +
                    "or Solid for an internal divider.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This is a perimeter face. Mark Entrance to make the maze open the adjacent street.",
                    MessageType.None);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Solid"))
            {
                SetFaceType(building, WallSocket.AuthoredWallType.Solid);
            }

            using (new EditorGUI.DisabledScope(!hasNeighbour))
            {
                if (GUILayout.Button("Interior Open"))
                {
                    SetFaceType(building, WallSocket.AuthoredWallType.InteriorOpening);
                }
            }

            using (new EditorGUI.DisabledScope(hasNeighbour))
            {
                if (GUILayout.Button("Entrance"))
                {
                    SetFaceType(building, WallSocket.AuthoredWallType.Entrance);
                }
            }
            EditorGUILayout.EndHorizontal();

            int verticalCellCount = CountVerticalCells(building, selectedCoordinate);
            building.TryGetTopCoordinate(
                selectedCoordinate,
                out Vector3Int entranceTopCoordinate);
            bool halfEntranceTop =
                building.TryGetCellLayer(
                    entranceTopCoordinate,
                    out BuildingComponent.CellLayerType entranceTopLayer)
                && entranceTopLayer == BuildingComponent.CellLayerType.Half;
            using (new EditorGUI.DisabledScope(
                       hasNeighbour || verticalCellCount < 2 || halfEntranceTop))
            {
                if (GUILayout.Button(
                        $"Make Vertical Entrance Through {verticalCellCount} Floors",
                        GUILayout.Height(24f)))
                {
                    SetVerticalEntranceColumn(building);
                }
            }

            if (verticalCellCount < 2)
            {
                EditorGUILayout.HelpBox(
                    "Add a cell above or below to author a vertical entrance stack.",
                    MessageType.None);
            }
            else if (halfEntranceTop)
            {
                EditorGUILayout.HelpBox(
                    "A full-height cell is required above the half stack to hold the arch cap.",
                    MessageType.Info);
            }

            if (GUILayout.Button("Swap / Reroll This Wall"))
            {
                Undo.RegisterFullObjectHierarchyUndo(building.gameObject, "Reroll Building Wall");
                Undo.RecordObjects(new Object[] { building, socket }, "Reroll Building Wall");
                building.AdvanceDressingSeed();
                building.ApplyWallType(selectedCoordinate, selectedFace, socket.AuthoredType);
                MarkDirty(building, socket);
            }
        }

        private void DrawRoofAuthoring(BuildingComponent building)
        {
            EditorGUILayout.LabelField("Selected Cell RoofCell", EditorStyles.miniBoldLabel);

            if (!building.HasCompleteRoofCellKit)
            {
                EditorGUILayout.HelpBox(
                    "Some RoofCell prefabs are unassigned.",
                    MessageType.Warning);
                if (GUILayout.Button("Assign Labyrinth RoofCell Kit"))
                {
                    AssignDefaultRoofCellKit(building);
                }
            }

            if (!building.TryGetRoofState(
                    selectedCoordinate,
                    out BuildingComponent.RoofCellType roofType,
                    out int yawSteps))
            {
                EditorGUILayout.HelpBox(
                    "The selected cell is not registered with this building.",
                    MessageType.Warning);
                return;
            }

            building.TryGetTopCoordinate(
                selectedCoordinate,
                out Vector3Int topCoordinate);
            if (topCoordinate != selectedCoordinate)
            {
                EditorGUILayout.HelpBox(
                    $"This column's roof is attached to top cell {topCoordinate}. " +
                    "Changing it here updates that top automatically.",
                    MessageType.Info);
            }

            EditorGUI.BeginChangeCheck();
            BuildingComponent.RoofCellType editedType =
                (BuildingComponent.RoofCellType)EditorGUILayout.EnumPopup(
                    "RoofCell Prefab",
                    roofType);
            if (EditorGUI.EndChangeCheck())
            {
                SetRoofType(building, editedType, yawSteps);
                roofType = editedType;
            }

            if (!building.HasRoofPrefab(roofType))
            {
                EditorGUILayout.HelpBox(
                    $"The {roofType} prefab is not assigned in the RoofCell Kit section.",
                    MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(
                       roofType == BuildingComponent.RoofCellType.None
                       || !building.HasRoofPrefab(roofType)))
            {
                EditorGUILayout.LabelField("Rotation", $"{yawSteps * 90} degrees");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Rotate Left"))
                {
                    SetRoofType(building, roofType, yawSteps - 1);
                }

                if (GUILayout.Button("Rotate Right"))
                {
                    SetRoofType(building, roofType, yawSteps + 1);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawWholeBuildingDressing()
        {
            BuildingComponent primary = (BuildingComponent)target;
            int socketCount = primary.AuthorableWallSocketCount;

            EditorGUILayout.LabelField("Whole Building", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                socketCount > 0
                    ? $"{socketCount} wall socket{(socketCount == 1 ? string.Empty : "s")} found. " +
                      "Entrances use passages; interior openings stay frameless."
                    : "No WallSockets were found. Assign the maze's socket-configured DungeonCell as Cell Prefab.",
                socketCount > 0 ? MessageType.Info : MessageType.Warning);

            using (new EditorGUI.DisabledScope(socketCount == 0))
            {
                if (GUILayout.Button("Dress / Reroll All Walls", GUILayout.Height(28f)))
                {
                    DressSelectedBuildings();
                }
            }
        }

        private Room3D AddCell(
            BuildingComponent building,
            Vector3Int coordinate,
            BuildingComponent.CellLayerType layerType,
            bool configureImmediately = true)
        {
            GameObject cellPrefab =
                building.CellPrefabForCoordinate(coordinate, layerType);
            if (cellPrefab == null || building.TryGetCell(coordinate, out _))
            {
                return null;
            }

            if (configureImmediately)
            {
                Undo.RegisterFullObjectHierarchyUndo(
                    building.gameObject,
                    "Add Building Cell");
            }
            GameObject instance = PrefabUtility.InstantiatePrefab(
                cellPrefab,
                building.transform) as GameObject;

            if (instance == null)
            {
                instance = Instantiate(cellPrefab, building.transform);
            }

            instance.transform.localPosition =
                building.CellLocalPosition(coordinate, layerType);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = cellPrefab.transform.localScale;
            instance.name = $"Cell_{coordinate.x}_{coordinate.y}_{coordinate.z}";

            Room3D room = instance.GetComponent<Room3D>();
            if (room == null)
            {
                room = instance.GetComponentInChildren<Room3D>(true);
            }

            if (room == null)
            {
                DestroyImmediate(instance);
                Debug.LogError(
                    $"{building.name}: Cell Prefab '{cellPrefab.name}' has no Room3D component.",
                    building);
                return null;
            }

            Undo.RecordObject(building, "Register Building Cell");
            building.RegisterCell(
                coordinate,
                room,
                layerType,
                configureImmediately);

            if (!configureImmediately)
            {
                Undo.RegisterCreatedObjectUndo(instance, "Add Generated Building Cell");
                return room;
            }

            foreach (Room3D.Directions direction in HorizontalDirections)
            {
                building.ApplyWallType(coordinate, direction, WallSocket.AuthoredWallType.Solid);

                Vector3Int neighbourCoordinate = coordinate + DirectionOffset(direction);
                if (!building.TryGetCell(neighbourCoordinate, out _)
                    || !building.CanShareInteriorEdge(
                        coordinate,
                        neighbourCoordinate))
                {
                    continue;
                }

                Room3D.Directions opposite = Opposite(direction);
                building.ApplyWallType(
                    coordinate,
                    direction,
                    WallSocket.AuthoredWallType.InteriorOpening);
                building.ApplyWallType(
                    neighbourCoordinate,
                    opposite,
                    WallSocket.AuthoredWallType.InteriorOpening);
            }

            Undo.RegisterCreatedObjectUndo(instance, "Add Building Cell");
            MarkDirty(building);
            Selection.activeGameObject = building.gameObject;
            return room;
        }

        private void RemoveCell(
            BuildingComponent building,
            Vector3Int coordinate,
            Room3D room)
        {
            Undo.RegisterFullObjectHierarchyUndo(building.gameObject, "Remove Building Cell");
            Undo.RecordObject(building, "Remove Building Cell");

            foreach (Room3D.Directions direction in HorizontalDirections)
            {
                Vector3Int neighbourCoordinate = coordinate + DirectionOffset(direction);
                if (building.TryGetCell(neighbourCoordinate, out _)
                    && building.CanShareInteriorEdge(
                        coordinate,
                        neighbourCoordinate))
                {
                    building.ApplyWallType(
                        neighbourCoordinate,
                        Opposite(direction),
                        WallSocket.AuthoredWallType.Solid);
                }
            }

            building.UnregisterCell(room);
            Undo.DestroyObjectImmediate(room.gameObject);
            MarkDirty(building);
        }

        private void SetFaceType(
            BuildingComponent building,
            WallSocket.AuthoredWallType type)
        {
            if (!building.TryGetWallSocket(selectedCoordinate, selectedFace, out WallSocket socket))
            {
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(building.gameObject, "Change Building Wall");
            Vector3Int neighbourCoordinate = selectedCoordinate + DirectionOffset(selectedFace);
            bool hasNeighbour =
                building.TryGetCell(neighbourCoordinate, out _)
                && building.CanShareInteriorEdge(
                    selectedCoordinate,
                    neighbourCoordinate);

            Undo.RecordObject(socket, "Change Building Wall");
            building.ApplyWallType(selectedCoordinate, selectedFace, type);
            MarkDirty(building, socket);

            if (!hasNeighbour)
            {
                return;
            }

            Room3D.Directions opposite = Opposite(selectedFace);
            if (!building.TryGetWallSocket(neighbourCoordinate, opposite, out WallSocket oppositeSocket))
            {
                return;
            }

            Undo.RecordObject(oppositeSocket, "Change Building Wall");

            // One visible divider owns a shared edge. A continuous opening hides
            // both faces; a solid divider hides only the neighbour's duplicate.
            building.ApplyWallType(
                neighbourCoordinate,
                opposite,
                WallSocket.AuthoredWallType.InteriorOpening);
            MarkDirty(building, oppositeSocket);
        }

        private void SetVerticalEntranceColumn(BuildingComponent building)
        {
            Undo.RegisterFullObjectHierarchyUndo(
                building.gameObject,
                "Create Vertical Building Entrance");
            Undo.RecordObject(building, "Create Vertical Building Entrance");

            int marked = building.ApplyVerticalEntranceColumn(
                selectedCoordinate,
                selectedFace);
            MarkDirty(building);

            foreach (WallSocket socket in building.GetComponentsInChildren<WallSocket>(true))
            {
                MarkDirty(socket);
            }

            Debug.Log(
                $"{building.name}: created a {marked}-floor vertical entrance on " +
                $"{selectedFace} at X {selectedCoordinate.x}, Z {selectedCoordinate.z}.",
                building);
        }

        private static int CountVerticalCells(
            BuildingComponent building,
            Vector3Int coordinate)
        {
            if (!building.TryGetCell(coordinate, out _))
            {
                return 0;
            }

            int count = 1;
            Vector3Int cursor = coordinate + Vector3Int.up;
            while (building.TryGetCell(cursor, out _))
            {
                count++;
                cursor += Vector3Int.up;
            }

            cursor = coordinate + Vector3Int.down;
            while (building.TryGetCell(cursor, out _))
            {
                count++;
                cursor += Vector3Int.down;
            }

            return count;
        }

        private void SetRoofType(
            BuildingComponent building,
            BuildingComponent.RoofCellType type,
            int yawSteps)
        {
            Undo.RegisterFullObjectHierarchyUndo(
                building.gameObject,
                "Change Building RoofCell");
            Undo.RecordObject(building, "Change Building RoofCell");
            building.ApplyRoofType(selectedCoordinate, type, yawSteps);
            MarkDirty(building);
        }

        private static void AssignDefaultRoofCellKit(BuildingComponent building)
        {
            const string root =
                "Assets/0_Jd/Minigames/LabyrinthCrawler/DungeonRooms/";
            (string property, string asset)[] assignments =
            {
                ("roofCellSloped", "RoofCell_Sloped.prefab"),
                ("roofCellStepped", "RoofCell_Stepped.prefab"),
                ("roofCellLeft", "RoofCell_L.prefab"),
                ("roofCellRight", "RoofCell_R.prefab"),
                ("roofCellLeftCurve", "RoofCell_L_Curve.prefab"),
                ("roofCellRightCurve", "RoofCell_R_Curve.prefab"),
                ("roofCellBlock", "RoofCell_Block.prefab"),
                ("roofCellHalfBlock", "RoofCell_HalfBlock.prefab"),
            };

            Undo.RecordObject(building, "Assign Labyrinth RoofCell Kit");
            SerializedObject buildingObject = new SerializedObject(building);
            foreach ((string property, string asset) in assignments)
            {
                SerializedProperty field = buildingObject.FindProperty(property);
                if (field != null)
                {
                    field.objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<GameObject>(root + asset);
                }
            }

            buildingObject.ApplyModifiedProperties();
            MarkDirty(building);
        }

        private static void AssignDefaultHalfCell(BuildingComponent building)
        {
            Undo.RecordObject(building, "Assign Labyrinth Half Cell");
            SerializedObject buildingObject = new SerializedObject(building);
            SerializedProperty field = buildingObject.FindProperty("halfCellPrefab");
            if (field != null)
            {
                field.objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/0_Jd/Minigames/LabyrinthCrawler/DungeonRooms/UpperCell_Half.prefab");
            }

            buildingObject.ApplyModifiedProperties();
            MarkDirty(building);
        }

        private void DressSelectedBuildings()
        {
            Undo.SetCurrentGroupName("Dress Building Walls");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (Object selected in targets)
            {
                BuildingComponent building = (BuildingComponent)selected;
                WallSocket[] sockets = building.GetComponentsInChildren<WallSocket>(true);
                if (sockets.Length == 0)
                {
                    continue;
                }

                Undo.RegisterFullObjectHierarchyUndo(building.gameObject, "Dress Building Walls");
                int count = building.RerollWalls();
                MarkDirty(building);

                foreach (WallSocket socket in sockets)
                {
                    MarkDirty(socket);
                }

                Debug.Log(
                    $"{building.name}: dressed {count} wall socket{(count == 1 ? string.Empty : "s")} " +
                    $"with seed {building.DressingSeed}.",
                    building);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        private static void OpenAllSharedEdges(BuildingComponent building)
        {
            Undo.RegisterFullObjectHierarchyUndo(
                building.gameObject,
                "Open Shared Building Cell Edges");
            building.OpenAllSharedEdges();
            MarkDirty(building);
        }

        // The common starting point is "copy one maze cell, then add the
        // BuildingComponent." Learn that cell's original prefab automatically so
        // the Add buttons are ready immediately after registration.
        private void TryLearnCellPrefab(Room3D[] rooms)
        {
            BuildingComponent building = (BuildingComponent)target;
            if (building.CellPrefab != null || rooms == null || rooms.Length == 0)
            {
                return;
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(
                rooms[0].gameObject);
            if (source == null)
            {
                return;
            }

            serializedObject.Update();
            SerializedProperty prefabProperty = serializedObject.FindProperty("cellPrefab");
            prefabProperty.objectReferenceValue = source;
            serializedObject.ApplyModifiedProperties();
        }

        private static void MarkDirty(params Object[] objects)
        {
            foreach (Object value in objects)
            {
                if (value == null)
                {
                    continue;
                }

                EditorUtility.SetDirty(value);
                PrefabUtility.RecordPrefabInstancePropertyModifications(value);

                if (value is Component component)
                {
                    EditorUtility.SetDirty(component.gameObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject);
                }
            }
        }

        private static Vector3Int DirectionOffset(Room3D.Directions direction)
        {
            switch (direction)
            {
                case Room3D.Directions.NORTH: return new Vector3Int(0, 0, 1);
                case Room3D.Directions.SOUTH: return new Vector3Int(0, 0, -1);
                case Room3D.Directions.EAST: return new Vector3Int(1, 0, 0);
                case Room3D.Directions.WEST: return new Vector3Int(-1, 0, 0);
                default: return Vector3Int.zero;
            }
        }

        private static bool IsEntranceFaceAt(
            BuildingComponent building,
            Vector3Int coordinate,
            Room3D.Directions direction)
        {
            return building.TryGetWallSocket(coordinate, direction, out WallSocket socket)
                && socket.AuthoredType == WallSocket.AuthoredWallType.Entrance;
        }

        private static Vector3 WorldDirection(
            Transform buildingTransform,
            Room3D.Directions direction)
        {
            return buildingTransform.TransformDirection(
                LocalDirection(direction)).normalized;
        }

        private static Vector3 LocalDirection(Room3D.Directions direction)
        {
            switch (direction)
            {
                case Room3D.Directions.NORTH: return Vector3.forward;
                case Room3D.Directions.SOUTH: return Vector3.back;
                case Room3D.Directions.EAST: return Vector3.right;
                case Room3D.Directions.WEST: return Vector3.left;
                default: return Vector3.forward;
            }
        }

        private static Room3D.Directions Opposite(Room3D.Directions direction)
        {
            switch (direction)
            {
                case Room3D.Directions.NORTH: return Room3D.Directions.SOUTH;
                case Room3D.Directions.SOUTH: return Room3D.Directions.NORTH;
                case Room3D.Directions.EAST: return Room3D.Directions.WEST;
                case Room3D.Directions.WEST: return Room3D.Directions.EAST;
                default: return Room3D.Directions.NONE;
            }
        }

        private static string Nicify(WallSocket.AuthoredWallType type)
        {
            return type == WallSocket.AuthoredWallType.InteriorOpening
                ? "Interior Open"
                : ObjectNames.NicifyVariableName(type.ToString());
        }

    }
}
