using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Warana.Combat;
using Warana.Enemies;
using Warana.Player;

namespace Warana.EditorTools
{
    /// <summary>
    /// Monta o prefab do Mad Ghost a partir dos assets gerados pelo
    /// <see cref="MadGhostAnimationBuilder"/> e sabe colocá-lo na cena aberta.
    /// Idempotente: rodar de novo reescreve o prefab e as instâncias herdam a mudança.
    /// </summary>
    public static class MadGhostBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Enemies/MadGhost.prefab";

        private const string SheetPath =
            "Assets/Animations/Enemies/Horror Enemy Pack/Mad Ghost/Mad Ghost-Sheet Separated tags.png";

        private const string IdleSpriteName = "MadGhost_Idle_0";

        private const string GroundLayerName = "Ground";
        private const string EnemyLayerName = "Enemy";

        /// <summary>
        /// Medidas tiradas da arte: o corpo ocupa ~22 x 36 px dentro do canvas de
        /// 64x64, o que a 64 PPU dá ~0,34 x 0,56 unidades. O collider acompanha o
        /// desenho, e não o canvas — senão o fantasma teria meio metro de ar em volta.
        /// </summary>
        private static readonly Vector2 BodySize = new Vector2(0.34f, 0.54f);

        /// <summary>
        /// O pivô do sprite está na base do canvas, então o transform do inimigo É o
        /// pé dele. O collider sobe a partir daí; sensores e sondas medem do mesmo
        /// ponto, o que torna "onde ele está" e "onde ele pisa" a mesma coisa.
        /// </summary>
        private static Vector2 BodyOffset => new Vector2(0f, BodySize.y * 0.5f);

        /// <summary>Onde o primeiro Mad Ghost nasce na sandbox: trecho plano entre dois alvos de teste.</summary>
        private static readonly Vector3 SandboxSpawn = new Vector3(-35f, 0.4f, 0f);

        [MenuItem("Waraná/Inimigos/Gerar Prefab do Mad Ghost")]
        public static GameObject BuildPrefab()
        {
            AnimatorController controller = MadGhostAnimationBuilder.Build();
            if (controller == null) return null;

            int groundLayer = TestLevelBuilder.EnsureLayer(GroundLayerName);
            int enemyLayer = TestLevelBuilder.EnsureLayer(EnemyLayerName);
            PhysicsMaterial2D noFriction = TestLevelBuilder.EnsureNoFrictionMaterial();

            var ghost = new GameObject("MadGhost") { layer = enemyLayer };

            try
            {
                Compose(ghost, controller, noFriction, groundLayer);

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                AssetDatabase.Refresh();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(ghost, PrefabPath);
                AssetDatabase.SaveAssets();

                Debug.Log($"[MadGhost] Prefab gerado em {PrefabPath}.");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(ghost);
            }
        }

        [MenuItem("Waraná/Inimigos/Adicionar Mad Ghost na Cena")]
        public static void AddToScene()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? BuildPrefab();
            if (prefab == null) return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            // Vai para dentro do root da cena montada, se ele existir, para não ficar
            // solto na raiz e sumir no próximo rebuild do nível.
            GameObject root = GameObject.Find("Scene");
            if (root != null) instance.transform.SetParent(root.transform);

            instance.transform.position = SandboxSpawn;
            instance.name = "MadGhost";

            Undo.RegisterCreatedObjectUndo(instance, "Adicionar Mad Ghost");
            Selection.activeGameObject = instance;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[MadGhost] Instância adicionada em {SandboxSpawn}.");
        }

        // ------------------------------------------------------------- composição

        private static void Compose(
            GameObject ghost, AnimatorController controller, PhysicsMaterial2D material, int groundLayer)
        {
            var body = ghost.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f; // o EnemyController2D aplica a gravidade à mão
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = ghost.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = BodySize;
            collider.offset = BodyOffset;
            collider.sharedMaterial = material; // sem isso ele gruda na parede ao encostar

            var sensor = ghost.AddComponent<GroundSensor2D>();
            SetMask(sensor, "groundLayer", 1 << groundLayer);
            SetVector2(sensor, "offset", new Vector2(0f, -0.02f)); // pivô nos pés: a caixa fica rente ao chão
            SetVector2(sensor, "size", new Vector2(BodySize.x * 0.8f, 0.12f));

            var enemyController = ghost.AddComponent<EnemyController2D>();
            SetMask(enemyController, "groundLayer", 1 << groundLayer);

            var senses = ghost.AddComponent<EnemySenses2D>();
            SetMask(senses, "blockingMask", 1 << groundLayer);
            SetFloat(senses, "eyeHeight", BodySize.y * 0.7f);

            var health = ghost.AddComponent<Health>();
            SetFloat(health, "maxHealth", 3f);
            SetFloat(health, "knockback", 2.5f);
            SetFloat(health, "invulnerability", 0.1f);

            // Crucial: o Health não pode desligar o objeto ao morrer, senão a animação
            // de morte (9 frames) nunca chega a tocar. Quem tira o Mad Ghost de cena
            // é o próprio MadGhost, depois do clip terminar.
            SetBool(health, "disableOnDeath", false);

            BuildVisual(ghost.transform, controller);

            ghost.AddComponent<MadGhostAnimator>();

            var brain = ghost.AddComponent<MadGhost>();

            // O Player está na layer Default; os inimigos, na Enemy. Mirar só a Default
            // é o que impede um Mad Ghost de acertar outro no mesmo aperto de espaço.
            SetMask(brain, "hitMask", 1 << 0);
        }

        private static void BuildVisual(Transform parent, AnimatorController controller)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(parent);

            // Sem deslocamento: o pivô do sprite já está na base do canvas, alinhado
            // com o pé do collider. É o que mantém a arte colada no chão.
            visual.transform.localPosition = Vector3.zero;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadIdleSprite();
            renderer.sortingOrder = 9; // logo atrás de Piatã (10), para não cobrir o jogador

            var animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private static Sprite LoadIdleSprite()
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(SheetPath))
            {
                if (asset is Sprite sprite && sprite.name == IdleSpriteName) return sprite;
            }

            Debug.LogWarning($"[MadGhost] Sprite '{IdleSpriteName}' não encontrado; o prefab fica sem preview.");
            return null;
        }

        // ------------------------------------------------------------- utilidades

        private static void SetMask(Object target, string property, int mask)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string property, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2(Object target, string property, Vector2 value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
