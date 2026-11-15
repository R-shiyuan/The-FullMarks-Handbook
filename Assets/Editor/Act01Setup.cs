using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace University.EditorTools
{
    /// <summary>
    /// 第一幕（毕业典礼）布景配置。
    /// 所有操作都是幂等的：重复执行会先清掉上一版再重建，不会产生重复物体。
    ///
    /// 场景坐标（Unity）：
    ///   舞台 z ∈ [-2.4, 2.4]   屏幕 z ∈ [-2.1, -0.6]（前表面 -0.587）
    ///   观众席 z ∈ [4, 14]     天花板 y = 4.70   地面 y = 0
    ///   吊顶灯板 y = 4.688，覆盖 x ∈ [-4.375, 4.375]、z ∈ [-1.15, 12.95]
    /// </summary>
    public static class Act01Setup
    {
        const string LightingRoot = "Lighting";

        [MenuItem("Tools/Act01/Lighting/Setup", false, 10)]
        public static void SetupLighting()
        {
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Act01 Setup Lighting");

            // ---------- 1. 清理上一版 ----------
            var old = GameObject.Find(LightingRoot);
            if (old != null) Undo.DestroyObjectImmediate(old);

            // ---------- 2. 环境光：深冷灰，够看清结构但整体压暗 ----------
            // 礼堂是封闭的，Skybox 环境光照不进室内，但 ambient 本身不受遮挡，
            // 所以这里给的是一层极低的基础亮度，防止暗部纯黑。
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.115f, 0.135f, 0.170f);   // 微冷
            RenderSettings.ambientEquatorColor = new Color(0.070f, 0.075f, 0.090f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.035f, 0.042f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.6f;

            // ---------- 3. 关掉模板的平行光 ----------
            // 封闭室内没有天空，方向光只会在墙上留下一个说不清来源的亮斑。
            var dl = GameObject.Find("Directional Light");
            if (dl != null) dl.SetActive(false);

            // ---------- 4. 灯组 ----------
            var root = new GameObject(LightingRoot);
            Undo.RegisterCreatedObjectUndo(root, "Create Lighting");

            // 舞台顶光：吊顶灯板的实体对应。冷白，负责把舞台结构托出来。
            MakePoint(root, "Ceiling_Stage_Front", new Vector3(0f, 4.40f, -0.90f),
                new Color(0.93f, 0.95f, 1.00f), intensity: 7.0f, range: 7.5f);
            MakePoint(root, "Ceiling_Stage_Back", new Vector3(0f, 4.40f, 1.70f),
                new Color(0.93f, 0.95f, 1.00f), intensity: 5.5f, range: 7.0f);

            // 讲台聚光：这一幕的视觉中心。暖色，窄角，有边缘。
            MakeSpot(root, "Spot_Podium", new Vector3(0f, 4.85f, 0.70f),
                new Color(1.00f, 0.94f, 0.84f), intensity: 22.0f, range: 7.0f, spotAngle: 42f, innerRatio: 0.35f);

            // 屏幕辉光：屏幕是这一幕最大的自发光面，它必须能在舞台地板上留下光。
            MakePoint(root, "Screen_Spill", new Vector3(0f, 2.70f, 0.60f),
                new Color(0.82f, 0.88f, 1.00f), intensity: 3.5f, range: 6.0f, shadows: false);

            // 观众席：刻意给得极弱。他们是剪影，只需要一点点轮廓分离。
            MakePoint(root, "Crowd_Fill", new Vector3(0f, 3.60f, 9.50f),
                new Color(0.66f, 0.74f, 0.92f), intensity: 1.6f, range: 13.0f, shadows: false);

            // 后墙 / 出入口方向的一点冷光，避免画面底部完全死掉
            MakePoint(root, "Rear_Cool", new Vector3(0f, 2.20f, 16.50f),
                new Color(0.60f, 0.70f, 0.95f), intensity: 2.0f, range: 9.0f, shadows: false);

            Selection.activeGameObject = root;
            Undo.CollapseUndoOperations(group);
            EditorSceneMarkDirty();
            Debug.Log("[Act01] Lighting setup done.");
        }

        [MenuItem("Tools/Act01/Lighting/Fix Screen Material", false, 11)]
        public static void FixScreenMaterial()
        {
            // 屏幕必须自发光：它是这一幕唯一的光源性叙述装置。
            const string path = "Assets/Art/Materials/M_Screen_Surface.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("[Act01] URP/Lit shader not found.");
                    return;
                }
                mat = new Material(shader);
                System.IO.Directory.CreateDirectory("Assets/Art/Materials");
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", new Color(0.86f, 0.89f, 0.94f));
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.55f, 0.62f, 0.75f) * 1.35f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            var screen = GameObject.Find("Screen_Surface");
            if (screen != null)
            {
                var r = screen.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;
            }
            else
            {
                Debug.LogWarning("[Act01] Screen_Surface not found in scene.");
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            EditorSceneMarkDirty();
            Debug.Log("[Act01] Screen material set: " + path);
        }

        // ============================================================
        //  玩家 Rig：第一幕只有视线自由度，没有位移（位移由后续剧情解锁）
        // ============================================================
        [MenuItem("Tools/Act01/Player/Setup Rig", false, 20)]
        public static void SetupPlayerRig()
        {
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Act01 Setup Player Rig");

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Act01] 找不到 tag 为 MainCamera 的相机。");
                return;
            }
            var camGo = cam.gameObject;

            // 相机如果已经在旧 Rig 里，先提到根级，否则跟着旧 Rig 一起被删掉
            if (camGo.transform.parent != null)
                Undo.SetTransformParent(camGo.transform, null, "Detach Camera");

            var oldRig = GameObject.Find("PlayerRig");
            if (oldRig != null) Undo.DestroyObjectImmediate(oldRig);
            var oldPlaceholder = GameObject.Find("_Placeholder");
            if (oldPlaceholder != null) Undo.DestroyObjectImmediate(oldPlaceholder);

            // ---------- 台面占位 ----------
            // 舞台被抬高了 0.6m（讲台底 0.60、主持人脚 0.70），但 Floor_Wood 顶面在 y=0，
            // 也就是说台面网格缺失。这里补一块占位板，等美术给正式台面后替换掉。
            var phRoot = new GameObject("_Placeholder");
            Undo.RegisterCreatedObjectUndo(phRoot, "Create Placeholder");

            var stageFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stageFloor.name = "Stage_Floor_Placeholder";
            Undo.RegisterCreatedObjectUndo(stageFloor, "Create Stage Floor");
            stageFloor.transform.SetParent(phRoot.transform, false);
            // x: 房间内宽 ±6.1    z: 后墙 -2.4 到幕布 2.17    y: 0 到 0.6
            stageFloor.transform.position = new Vector3(0f, 0.30f, -0.115f);
            stageFloor.transform.localScale = new Vector3(12.2f, 0.6f, 4.57f);

            var floorMat = EnsureMaterial("Assets/Art/Materials/M_Placeholder.mat",
                new Color(0.16f, 0.13f, 0.11f), 0.75f);
            if (floorMat != null) stageFloor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // ---------- 三层 Rig ----------
            var root = new GameObject("PlayerRig");
            Undo.RegisterCreatedObjectUndo(root, "Create PlayerRig");
            // 舞台右侧幕后、幕布之前的空间中部。台面占位顶面 y=0.6。
            root.transform.position = new Vector3(4.30f, 0.60f, 0.90f);

            var yaw = new GameObject("Yaw");
            Undo.RegisterCreatedObjectUndo(yaw, "Create Yaw");
            yaw.transform.SetParent(root.transform, false);
            yaw.transform.localPosition = Vector3.zero;
            // 初始朝向：正对讲台（讲台中心约 (0, 1.4, -0.43)）。
            // 屏幕中心在视野左侧约 10°，一抬头就能看到那块大屏。
            yaw.transform.localRotation = Quaternion.Euler(0f, -107f, 0f);

            var pitch = new GameObject("Pitch");
            Undo.RegisterCreatedObjectUndo(pitch, "Create Pitch");
            pitch.transform.SetParent(yaw.transform, false);
            pitch.transform.localPosition = Vector3.zero;
            pitch.transform.localRotation = Quaternion.identity;

            // ---------- 相机挂到 Pitch 下 ----------
            camGo.transform.SetParent(pitch.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.62f, 0f); // 眼高
            camGo.transform.localRotation = Quaternion.identity;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 120f;
            cam.fieldOfView = 62f;

            // ---------- 挂视线脚本并接好引用 ----------
            var look = root.AddComponent<University.Act01.FirstPersonLook>();
            var so = new SerializedObject(look);
            so.FindProperty("yawPivot").objectReferenceValue = yaw.transform;
            so.FindProperty("pitchPivot").objectReferenceValue = pitch.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            Undo.CollapseUndoOperations(group);
            EditorSceneMarkDirty();
            Debug.Log("[Act01] Player rig placed at (4.30, 0.60, 0.90), yaw -107°.");
        }

        [MenuItem("Tools/Act01/Player/Hide Placeholder", false, 21)]
        public static void TogglePlaceholder()
        {
            var ph = GameObject.Find("_Placeholder");
            if (ph == null) { Debug.LogWarning("[Act01] 没有占位物体。"); return; }
            Undo.RecordObject(ph, "Toggle Placeholder");
            ph.SetActive(!ph.activeSelf);
            EditorSceneMarkDirty();
        }

        static Material EnsureMaterial(string path, Color color, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("[Act01] 找不到 URP/Lit。"); return null; }
            mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            System.IO.Directory.CreateDirectory("Assets/Art/Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------------- helpers ----------------

        static void MakePoint(GameObject parent, string name, Vector3 pos, Color color,
            float intensity, float range, bool shadows = true)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Light");
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;

            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.85f;
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;
            l.renderMode = LightRenderMode.Auto;
        }

        static void MakeSpot(GameObject parent, string name, Vector3 pos, Color color,
            float intensity, float range, float spotAngle, float innerRatio)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Light");
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;
            // Light 的 forward 是 +Z，绕 X 转 90° 让它朝下 (-Y)
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = spotAngle;
            l.innerSpotAngle = spotAngle * innerRatio;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.9f;
            l.shadowBias = 0.04f;
            l.shadowNormalBias = 0.3f;
            l.renderMode = LightRenderMode.Auto;
        }

        static void EditorSceneMarkDirty()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
