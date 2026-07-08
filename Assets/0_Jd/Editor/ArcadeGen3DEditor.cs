using Sol;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ArcadeGen3D))]
[CanEditMultipleObjects]
public class ArcadeGen3DEditor : Editor
{
    private const string ArcadeHubScenePath = "Assets/Sc_ArcadeHub.unity";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Regenerate Maze"))
        {
            RegenerateSelectedMazes();
        }
    }

    private void RegenerateSelectedMazes()
    {
        foreach (Object selectedTarget in targets)
        {
            if (selectedTarget is not ArcadeGen3D generator)
            {
                continue;
            }

            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Regenerate Maze");
            if (!generator.RegenerateMazeFromInspector())
            {
                continue;
            }

            EditorUtility.SetDirty(generator);
            if (generator.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
        }
    }

    public static void RegenerateArcadeHubMazeFromCommandLine()
    {
        EditorSceneManager.OpenScene(ArcadeHubScenePath);

        ArcadeGen3D[] generators =
            Object.FindObjectsByType<ArcadeGen3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (ArcadeGen3D generator in generators)
        {
            SerializedObject serializedGenerator = new SerializedObject(generator);
            serializedGenerator.FindProperty("autoGenerateOnStart").boolValue = false;
            serializedGenerator.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Regenerate Maze");
            generator.RegenerateMazeFromInspector();
            EditorUtility.SetDirty(generator);
        }

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }
}
