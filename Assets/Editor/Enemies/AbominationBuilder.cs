using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Warana.Combat;
using Warana.Enemies;
using Warana.Player;

namespace Warana.EditorTools
{
    /// <summary>
    /// Monta o prefab da Abomination a partir dos assets do
    /// <see cref="AbominationAnimationBuilder"/>. Idempotente.
    ///
    /// Diferente do Mad Ghost, o aumento de tamanho da chefe vive na escala do filho
    /// Visual, não na raiz: as caixas de dano da FSM são medidas em unidades de mundo
    /// a partir do pivô, e escalar a raiz as deixaria menores que a arte.
    /// </summary>
    public static class AbominationBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Enemies/Abomination.prefab";

        private const string SheetPath =
            "Assets/Animations/Enemies/Horror Enemy Pack/Abomination/Abomination-Sheet Separated Tags.png";

        private const string IdleSpriteName = "Abomination_Idle_0";

        private const string GroundLayerName = "Ground";
        private const string EnemyLayerName = "Enemy";

        /// <summary>
        /// Quanto a arte é ampliada. A célula tem 112x48 px a 64 PPU, mas o corpo
        /// desenhado ocupa só ~50x48 — a 1x a chefe ficaria menor que um Mad Ghost.
        /// </summary>
        private const float VisualScale = 2.5f;

        /// <summary>
        /// Corpo medido na arte (~50 x 48 px dentro do canvas), já em unidades de
        /// mundo. O collider fica um pouco menor que o desenho: encostar na silhueta
        /// de um bicho irregular não deve contar como bater na parede.
        /// </summary>
        private static readonly Vector2 BodySize = new Vector2(1.5f, 1.8f);

        private static Vector2 BodyOffset => new Vector2(0f, BodySize.y * 0.5f);

        [MenuItem("Waraná/Inimigos/Gerar Prefab da Abomination")]
        public static GameObject BuildPrefab()
        {
            AnimatorController controller = AbominationAnimationBuilder.Build();
            if (controller == null) return null;

            int groundLayer = TestLevelBuilder.EnsureLayer(GroundLayerName);
            int enemyLayer = TestLevelBuilder.EnsureLayer(EnemyLayerName);
            PhysicsMaterial2D noFriction = TestLevelBuilder.EnsureNoFrictionMaterial();

            var boss = new GameObject("Abomination") { layer = enemyLayer };

            try
            {
                Compose(boss, controller, noFriction, groundLayer);

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                AssetDatabase.Refresh();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(boss, PrefabPath);
                AssetDatabase.SaveAssets();

                Debug.Log($"[Abomination] Prefab gerado em {PrefabPath}.");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(boss);
            }
        }

        private static void Compose(
            GameObject boss, AnimatorController controller, PhysicsMaterial2D material, int groundLayer)
        {
            var body = boss.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f; // o EnemyController2D aplica a gravidade à mão
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = boss.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = BodySize;
            collider.offset = BodyOffset;
            collider.sharedMaterial = material;

            var sensor = boss.AddComponent<GroundSensor2D>();
            SetMask(sensor, "groundLayer", 1 << groundLayer);
            SetVector2(sensor, "offset", new Vector2(0f, -0.02f));
            SetVector2(sensor, "size", new Vector2(BodySize.x * 0.8f, 0.12f));

            var enemyController = boss.AddComponent<EnemyController2D>();
            SetMask(enemyController, "groundLayer", 1 << groundLayer);

            // As sondas padrão foram medidas para o Mad Ghost, que cabe num canvas de
            // 64 px. Um corpo três vezes mais largo precisa enxergar parede e beirada
            // mais longe, senão a chefe encosta no cenário antes de perceber.
            SetFloat(enemyController, "wallProbeHeight", 0.6f);
            SetFloat(enemyController, "wallProbeDistance", 0.85f);
            SetFloat(enemyController, "ledgeProbeAhead", 0.95f);
            SetFloat(enemyController, "acceleration", 14f); // massa: ela arranca devagar
            SetFloat(enemyController, "deceleration", 22f);

            var senses = boss.AddComponent<EnemySenses2D>();
            SetMask(senses, "blockingMask", 1 << groundLayer);
            SetFloat(senses, "eyeHeight", 1.2f);
            SetFloat(senses, "sightRange", 9f);
            SetFloat(senses, "loseRange", 16f);
            SetFloat(senses, "verticalTolerance", 4f);

            var health = boss.AddComponent<Health>();
            SetFloat(health, "maxHealth", 5f);
            SetFloat(health, "invulnerability", 0.15f);

            // Sem empurrão: uma chefe que recua a cada raio poderia ser empurrada para
            // fora da arena, e o combate viraria "afaste o problema" em vez de "resista".
            SetFloat(health, "knockback", 0f);

            // A animação de morte tem 9 frames; desligar o objeto no golpe final não
            // deixaria nenhum deles aparecer.
            SetBool(health, "disableOnDeath", false);

            BuildVisual(boss.transform, controller);

            boss.AddComponent<AbominationAnimator>();

            var brain = boss.AddComponent<Abomination>();

            // O Player está na layer Default; os inimigos, na Enemy.
            SetMask(brain, "hitMask", 1 << 0);

            // A língua do golpe alcança ~86 px à frente do pivô, quase três unidades
            // depois da escala. A caixa cobre o traço inteiro, rente ao chão.
            SetVector2(brain, "hitboxOffset", new Vector2(1.85f, 0.5f));
            SetVector2(brain, "hitboxSize", new Vector2(3f, 1f));
            SetFloat(brain, "attackRange", 2.7f);
        }

        private static void BuildVisual(Transform parent, AnimatorController controller)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(parent);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * VisualScale;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadIdleSprite();
            renderer.sortingOrder = 9; // logo atrás de Piatã (10)

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

            Debug.LogWarning($"[Abomination] Sprite '{IdleSpriteName}' não encontrado; o prefab fica sem preview.");
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
