using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Génère le prefab PlayerCameraRig et la scène de test CameraSandbox.
/// Idempotent : peut être relancé pour régénérer les assets (les GUID sont conservés).
///
/// Ligne de commande :
///   Unity -batchmode -nographics -quit -projectPath "..." -executeMethod CameraRigBuilder.BuildAll
/// </summary>
public static class CameraRigBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/Camera";
    private const string PrefabPath = PrefabFolder + "/PlayerCameraRig.prefab";
    private const string SandboxFolder = "Assets/Sandbox/CameraSandbox";
    private const string MaterialFolder = SandboxFolder + "/Materials";
    private const string ScenePath = SandboxFolder + "/CameraSandbox.unity";

    [MenuItem("Tools/Camera/Rebuild Camera Rig And Sandbox")]
    public static void BuildAll()
    {
        BuildPrefab();
        BuildSandboxScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CameraRigBuilder] Prefab et scène générés.");
    }

    public static void BuildPrefabOnly()
    {
        BuildPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CameraRigBuilder] Prefab généré.");
    }

    // ------------------------------------------------------------------ Prefab

    private static GameObject BuildPrefab()
    {
        EnsureFolder(PrefabFolder);

        GameObject rig = new GameObject("PlayerCameraRig");
        try
        {
            rig.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            rig.AddComponent<CameraLook>();

            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.tag = "MainCamera";

            // Réglages repris de la Main Camera de SampleScene (sauf FOV et near clip demandés).
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.19215687f, 0.3019608f, 0.4745098f, 0f);
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            cam.depth = -1f;
            cam.cullingMask = ~0;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.allowDynamicResolution = false;
            cam.useOcclusionCulling = true;

            cameraObject.AddComponent<AudioListener>();

            UniversalAdditionalCameraData urp = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            urp.renderShadows = true;
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.None;
            urp.antialiasingQuality = AntialiasingQuality.High;
            urp.volumeLayerMask = 1;
            urp.stopNaN = false;
            urp.dithering = false;

            GameObject hands = new GameObject("HandsAnchor");
            hands.transform.SetParent(cameraObject.transform, false);

            return PrefabUtility.SaveAsPrefabAsset(rig, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(rig);
        }
    }

    // ------------------------------------------------------------------- Scene

    private static void BuildSandboxScene()
    {
        EnsureFolder(MaterialFolder);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            throw new FileNotFoundException("Prefab introuvable : " + PrefabPath);
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        RenderSettings.ambientMode = AmbientMode.Skybox;

        Material floorMat = GetMaterial("Floor", new Color(0.35f, 0.38f, 0.35f));
        Material wallMat = GetMaterial("Wall", new Color(0.75f, 0.72f, 0.65f));
        Material redMat = GetMaterial("PillarRed", new Color(0.85f, 0.2f, 0.2f));
        Material blueMat = GetMaterial("PillarBlue", new Color(0.2f, 0.35f, 0.9f));
        Material greenMat = GetMaterial("PillarGreen", new Color(0.2f, 0.75f, 0.3f));
        Material yellowMat = GetMaterial("PillarYellow", new Color(0.95f, 0.8f, 0.15f));

        // Lumière
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        light.intensity = 1.2f;
        lightObject.AddComponent<UniversalAdditionalLightData>();
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Décor
        GameObject environment = new GameObject("Environment");
        CreateBlock(environment.transform, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), floorMat);

        CreateBlock(environment.transform, "Wall_North", new Vector3(0f, 2f, 40f), new Vector3(80f, 4f, 1f), wallMat);
        CreateBlock(environment.transform, "Wall_South", new Vector3(0f, 2f, -40f), new Vector3(80f, 4f, 1f), wallMat);
        CreateBlock(environment.transform, "Wall_East", new Vector3(40f, 2f, 0f), new Vector3(1f, 4f, 80f), wallMat);
        CreateBlock(environment.transform, "Wall_West", new Vector3(-40f, 2f, 0f), new Vector3(1f, 4f, 80f), wallMat);

        CreateBlock(environment.transform, "Pillar_Red_5m", new Vector3(3f, 1.5f, 5f), new Vector3(1f, 3f, 1f), redMat);
        CreateBlock(environment.transform, "Pillar_Blue_10m", new Vector3(-4f, 2f, 10f), new Vector3(1.5f, 4f, 1.5f), blueMat);
        CreateBlock(environment.transform, "Pillar_Green_20m", new Vector3(6f, 2.5f, 20f), new Vector3(2f, 5f, 2f), greenMat);
        CreateBlock(environment.transform, "Pillar_Yellow_30m", new Vector3(-8f, 3f, 30f), new Vector3(2f, 6f, 2f), yellowMat);
        CreateBlock(environment.transform, "Pillar_Red_Left", new Vector3(-12f, 1.5f, -5f), new Vector3(1f, 3f, 1f), redMat);
        CreateBlock(environment.transform, "Pillar_Blue_Right", new Vector3(12f, 2f, -8f), new Vector3(1.5f, 4f, 1.5f), blueMat);
        CreateBlock(environment.transform, "Pillar_Green_Back", new Vector3(0f, 2.5f, -25f), new Vector3(2f, 5f, 2f), greenMat);
        CreateBlock(environment.transform, "Crate_Yellow", new Vector3(2f, 0.5f, -3f), new Vector3(1f, 1f, 1f), yellowMat);

        // DummyPlayer : racine vide avec le pivot aux pieds (comme un vrai Player),
        // Rigidbody + CapsuleCollider, un mesh de capsule visuel et le prefab de caméra.
        GameObject player = new GameObject("DummyPlayer");
        player.transform.position = new Vector3(0f, 0.05f, 0f);

        Rigidbody body = player.AddComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;

        CapsuleCollider capsule = player.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 1f, 0f);
        capsule.height = 2f;
        capsule.radius = 0.5f;
        capsule.direction = 1;

        player.AddComponent<DummyPlayerMover>();

        GameObject bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyVisual.name = "BodyVisual";
        Object.DestroyImmediate(bodyVisual.GetComponent<CapsuleCollider>());
        bodyVisual.transform.SetParent(player.transform, false);
        bodyVisual.transform.localPosition = new Vector3(0f, 1f, 0f);
        bodyVisual.GetComponent<Renderer>().sharedMaterial = GetMaterial("Player", new Color(0.9f, 0.9f, 0.95f));

        GameObject rigInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        rigInstance.transform.SetParent(player.transform, false);

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.position = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().sharedMaterial = material;
        block.isStatic = true;
        return block;
    }

    // ----------------------------------------------------------------- Helpers

    private static Material GetMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new FileNotFoundException("Shader URP/Lit introuvable.");
            }
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
    }
}
