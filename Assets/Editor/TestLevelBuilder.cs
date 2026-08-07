using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Warana.CameraRig;
using Warana.Player;

namespace Warana.EditorTools
{
    /// <summary>
    /// Monta a sandbox de teste do Player Controller na cena aberta.
    /// Idempotente: rodar de novo apaga e recria o root de teste.
    /// </summary>
    public static class TestLevelBuilder
    {
        private const string RootName = "Scene";
        private const string GroundLayerName = "Ground";
        private const string SpritePath = "Assets/Art/Placeholder/square.png";
        private const string MaterialPath = "Assets/Physics/NoFriction.physicsMaterial2D";

        private static readonly Color GroundColor = new Color(0.29f, 0.36f, 0.31f);

        /// <summary>
        /// Pivô do sprite no pé, collider de 1 unidade centrado na origem:
        /// o visual desce meia unidade para o pé encostar na base do collider.
        /// </summary>
        private static readonly Vector3 VisualOffset = new Vector3(0f, -0.5f, 0f);

        /// <summary>A Pixel Perfect Camera mostra ~5,6 unidades de altura: pouca folga acima.</summary>
        private static readonly Vector2 CameraOffset = new Vector2(0f, 0.6f);

        [MenuItem("Waraná/Build Test Level")]
        public static void Build()
        {
            int groundLayer = EnsureLayer(GroundLayerName);
            Sprite square = EnsureSquareSprite();
            PhysicsMaterial2D noFriction = EnsureNoFrictionMaterial();
            AnimatorController animatorController = WaranaAnimationBuilder.Build();

            GameObject existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);

            GameObject player = BuildPlayer(root.transform, noFriction, groundLayer, animatorController);
            BuildLevel(root.transform, square, groundLayer);
            SetupCamera(player.transform);

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[TestLevelBuilder] Cena de teste montada. Aperte Play.");
        }

        // ---------------------------------------------------------------- player

        /// <summary>
        /// Player completo: corpo, sensor, input, controller e visual animado. Reaproveitado
        /// pelo <see cref="TilemapTestBuilder"/> — o Player é o mesmo nas duas sandboxes.
        /// </summary>
        internal static GameObject BuildPlayer(
            Transform parent,
            PhysicsMaterial2D material,
            int groundLayer,
            AnimatorController animatorController)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(-16f, 2f, 0f);

            var body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f; // o controller aplica a gravidade manualmente
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.95f, 1f);
            collider.sharedMaterial = material;

            player.AddComponent<PlayerInputHandler>();

            var sensor = player.AddComponent<GroundSensor2D>();
            SetPrivateLayerMask(sensor, "groundLayer", 1 << groundLayer);

            var controller = player.AddComponent<PlayerController2D>();

            // A arte vira pelo flipX do SpriteRenderer, não invertendo a escala do root:
            // escala negativa espelharia também colliders e sensores.
            SetPrivateBool(controller, "flipWithScale", false);

            BuildVisual(player.transform, animatorController);

            player.AddComponent<PlayerAttack>();
            player.AddComponent<PlayerAnimator>();

            return player;
        }

        private static void BuildVisual(Transform player, AnimatorController animatorController)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(player);
            visual.transform.localPosition = VisualOffset;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Animations/warana/Idle/frame_000.png");
            renderer.sortingOrder = 10;

            var animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // ----------------------------------------------------------------- level

        private static void BuildLevel(Transform parent, Sprite square, int groundLayer)
        {
            var level = new GameObject("Level");
            level.transform.SetParent(parent);

            // (nome, centro, tamanho) — 1 unidade = 1 metro, topo do chão em y = 0.
            AddPlatform(level.transform, square, groundLayer, "Ground",        new Vector2(0f, -0.5f),    new Vector2(44f, 1f));
            AddPlatform(level.transform, square, groundLayer, "Wall Left",     new Vector2(-22.5f, 3f),   new Vector2(1f, 8f));
            AddPlatform(level.transform, square, groundLayer, "Wall Right",    new Vector2(22.5f, 3f),    new Vector2(1f, 8f));

            // Degraus: pulos curtos, teste de altura variável.
            AddPlatform(level.transform, square, groundLayer, "Step 1",        new Vector2(-10f, 0.5f),   new Vector2(4f, 1f));
            AddPlatform(level.transform, square, groundLayer, "Step 2",        new Vector2(-5.5f, 1.5f),  new Vector2(4f, 1f));

            // No limite do jumpHeight (topo em y = 3.0, jumpHeight padrão 3.2).
            AddPlatform(level.transform, square, groundLayer, "Max Height",    new Vector2(0f, 2.5f),     new Vector2(4f, 1f));

            // Gap de 4.5 unidades: teste do alcance horizontal do pulo.
            AddPlatform(level.transform, square, groundLayer, "Ledge A",       new Vector2(6.5f, 1f),     new Vector2(5f, 1f));
            AddPlatform(level.transform, square, groundLayer, "Ledge B",       new Vector2(15.75f, 1f),   new Vector2(4.5f, 1f));

            // Plataforma alta e estreita: hangtime no ápice + precisão aérea.
            AddPlatform(level.transform, square, groundLayer, "Float",         new Vector2(11f, 4f),      new Vector2(3f, 0.5f));

            // Beirada isolada e alta: sentir o coyote time ao correr para fora.
            AddPlatform(level.transform, square, groundLayer, "Coyote Ledge",  new Vector2(20f, 3.5f),    new Vector2(4f, 1f));
        }

        private static void AddPlatform(Transform parent, Sprite square, int layer, string name, Vector2 center, Vector2 size)
        {
            var platform = new GameObject(name) { layer = layer };
            platform.transform.SetParent(parent);
            platform.transform.position = center;
            platform.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = platform.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = GroundColor;

            platform.AddComponent<BoxCollider2D>();
        }

        // ---------------------------------------------------------------- camera

        internal static void SetupCamera(Transform target)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[TestLevelBuilder] Nenhuma Main Camera na cena; pulei o setup da câmera.");
                return;
            }

            PixelArtProjectSetup.ConfigureCamera(camera); // a Pixel Perfect Camera define o orthographicSize

            var follow = camera.GetComponent<CameraFollow2D>() ?? camera.gameObject.AddComponent<CameraFollow2D>();

            // Reescrito explicitamente: um componente que já existia na cena guarda os
            // valores antigos e não pega os defaults novos do script.
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("offset").vector2Value = CameraOffset;
            so.FindProperty("lookAheadDistance").floatValue = 1.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            camera.transform.position = new Vector3(
                target.position.x + CameraOffset.x,
                target.position.y + CameraOffset.y,
                -10f);
        }

        // ----------------------------------------------------------------- assets

        /// <summary>Garante que a layer existe e devolve o índice dela.</summary>
        internal static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++) // 0–7 são reservadas pela Unity
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }

            Debug.LogError($"[TestLevelBuilder] Sem slots livres para criar a layer '{name}'.");
            return 0;
        }

        private static Sprite EnsureSquareSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));

            const int resolution = 8; // PPU = resolution ⇒ o sprite ocupa exatamente 1 unidade
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            var pixels = new Color32[resolution * resolution];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.Refresh(); // a pasta acabou de ser criada fora do AssetDatabase
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = resolution;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        /// <summary>Fricção zero: sem isso o personagem gruda em paredes ao encostar no ar.</summary>
        internal static PhysicsMaterial2D EnsureNoFrictionMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));

            var material = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();

            return material;
        }

        private static void SetPrivateLayerMask(Object target, string propertyName, int mask)
        {
            var so = new SerializedObject(target);
            so.FindProperty(propertyName).intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateBool(Object target, string propertyName, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(propertyName).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
