using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sol.Minigames.EditorTools
{
    /// <summary>
    /// Diagnostic: generates real Labyrinth floors (with pits) via the scene
    /// generator and renders each one top-down orthographic plus one eye-level
    /// angle, so a misaligned room is obvious as an offset / wrong-size cell in
    /// the grid. Writes PNGs to [project]/MazeCapture.
    ///
    /// Run closed-editor (needs a GPU - omit -nographics):
    ///   Unity.exe -batchmode -quit -projectPath [project] -executeMethod
    ///   Sol.Minigames.EditorTools.LabyrinthMazeCapture.Run
    /// </summary>
    public static class LabyrinthMazeCapture
    {
        private const string LabyrinthScenePath = "Assets/0_Jd/Scenes/Sc_LabyrinthCrawler.unity";
        private const string OutDir = "Assets/../MazeCapture";
        private static readonly int[] Seeds = { 3, 11, 26 };

        public static void Run()
        {
            EditorSceneManager.OpenScene(LabyrinthScenePath, OpenSceneMode.Single);
            ArcadeGen3D generator = Object.FindFirstObjectByType<ArcadeGen3D>(FindObjectsInactive.Include);
            LabyrinthCrawlerGame game = Object.FindFirstObjectByType<LabyrinthCrawlerGame>(FindObjectsInactive.Include);
            if (generator == null || game == null)
            {
                Debug.LogError("LabyrinthMazeCapture: the scene needs both LabyrinthCrawlerGame and its maze generator.");
                return;
            }

            Directory.CreateDirectory(Path.GetFullPath(OutDir));

            GameObject lightObj = new GameObject("Sun");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            GameObject camObj = new GameObject("Cap");
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);

            foreach (int seed in Seeds)
            {
                Random.InitState(seed * 7919);
                generator.GenerateWithRules(game, new ArcadeMazeRules
                {
                    overrideRoomPrefabs = false,
                    numX = 6,
                    numZ = 6,
                    braidRate = 0.35f,
                    respawnPlayerAtStart = false,
                    activateEndRoomExit = false
                });

                Bounds bounds = ComputeBounds(generator.GeneratedRoomsParent);
                CaptureTopDown(cam, $"seed{seed}_top", bounds);

                // The money shot: stand in a normal room and look across an
                // adjacent pit at the room beyond - the user's exact framing.
                CaptureAcrossPit(cam, generator, seed, bounds);
            }

            Object.DestroyImmediate(lightObj);
            Object.DestroyImmediate(camObj);
            Debug.Log($"LabyrinthMazeCapture: wrote PNGs to {Path.GetFullPath(OutDir)}");
        }

        private static Bounds ComputeBounds(Transform parent)
        {
            Renderer[] renderers = parent.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(parent.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in renderers)
            {
                if (!r.enabled)
                {
                    continue;
                }

                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return b;
        }

        private static void CaptureTopDown(Camera cam, string name, Bounds bounds)
        {
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.05f;
            cam.transform.position = new Vector3(bounds.center.x, bounds.max.y + 20f, bounds.center.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Render(cam, name, 900, 900);
        }

        private static void CaptureAcrossPit(Camera cam, ArcadeGen3D generator, int seed, Bounds bounds)
        {
            Room3D[,] rooms = generator.Rooms;
            int width = rooms.GetLength(0);
            int depth = rooms.GetLength(1);
            int shot = 0;

            for (int x = 0; x < width && shot < 3; x++)
            {
                for (int z = 0; z < depth && shot < 3; z++)
                {
                    Room3D pit = rooms[x, z];
                    if (pit == null || !pit.IsPit)
                    {
                        continue;
                    }

                    // A non-pit neighbour to stand in, looking toward the pit.
                    foreach ((int nx, int nz) in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
                    {
                        if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                        {
                            continue;
                        }

                        Room3D stand = rooms[nx, nz];
                        if (stand == null || stand.IsPit)
                        {
                            continue;
                        }

                        Vector3 eye = stand.transform.position + Vector3.up * 1.8f;
                        cam.orthographic = false;
                        cam.fieldOfView = 75f;
                        cam.transform.position = eye;
                        cam.transform.LookAt(pit.transform.position + Vector3.up * 1.2f);
                        Render(cam, $"seed{seed}_acrosspit{shot}", 1100, 620);
                        shot++;
                        break;
                    }
                }
            }
        }

        private static void Render(Camera cam, string name, int w, int h)
        {
            RenderTexture rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(OutDir + "/" + name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
