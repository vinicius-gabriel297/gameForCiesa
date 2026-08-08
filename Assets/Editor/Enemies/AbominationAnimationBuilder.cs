using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Warana.Enemies;

namespace Warana.EditorTools
{
    /// <summary>
    /// Corta a folha da Abomination e gera os AnimationClips e o AnimatorController.
    /// Mesmo molde do <see cref="MadGhostAnimationBuilder"/>; muda só o tamanho da
    /// célula (112x48, canvas largo por causa do braço estendido do golpe) e a tabela
    /// de clips. Idempotente.
    /// </summary>
    public static class AbominationAnimationBuilder
    {
        private const string SheetPath =
            "Assets/Animations/Enemies/Horror Enemy Pack/Abomination/Abomination-Sheet Separated Tags.png";

        private const string OutputRoot = "Assets/Animations/Enemies/Abomination";
        private const string ClipFolder = OutputRoot + "/Clips";
        private const string ControllerPath = OutputRoot + "/Abomination.controller";

        private const string SpritePrefix = "Abomination";

        private const int CellWidth = 112;
        private const int CellHeight = 48;

        /// <summary>
        /// Pivô fixo na base do corpo, não no centro do canvas: a célula tem 112 px de
        /// largura só para caber a língua do golpe, e o bicho parado ocupa apenas os
        /// ~49 px da esquerda. Centrar no canvas deixaria a chefe meia unidade ao lado
        /// do próprio collider. 24,5 / 112 ≈ 0,219 é o meio do corpo desenhado.
        /// </summary>
        private static readonly Vector2 Pivot = new Vector2(24.5f / CellWidth, 0f);

        [MenuItem("Waraná/Animação/Gerar Abomination")]
        public static AnimatorController Build()
        {
            if (!SliceSheet()) return null;

            Directory.CreateDirectory(ClipFolder);
            AssetDatabase.Refresh();

            Dictionary<string, Sprite> sprites = LoadSprites();
            var built = new Dictionary<string, AnimationClip>();

            foreach (AbominationAnimation.ClipDef def in AbominationAnimation.Clips)
            {
                AnimationClip clip = BuildClip(def, sprites);
                if (clip != null) built[def.Name] = clip;
            }

            AnimatorController controller = BuildController(built);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Abomination] {built.Count} clips e o Animator gerados em {OutputRoot}.");
            return controller;
        }

        // ------------------------------------------------------------------ corte

        private static bool SliceSheet()
        {
            if (AssetImporter.GetAtPath(SheetPath) is not TextureImporter importer)
            {
                Debug.LogError($"[Abomination] Folha não encontrada em {SheetPath}.");
                return false;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (texture == null)
            {
                Debug.LogError($"[Abomination] Não consegui carregar a textura em {SheetPath}.");
                return false;
            }

            PixelArtTextureDefaults.ApplyQuality(importer);
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelArtTextureDefaults.PixelsPerUnit;
            importer.SaveAndReimport();

            var factories = new SpriteDataProviderFactories();
            factories.Init();

            ISpriteEditorDataProvider provider =
                factories.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(SheetPath));

            if (provider == null)
            {
                Debug.LogError("[Abomination] Sem ISpriteEditorDataProvider para a folha.");
                return false;
            }

            provider.InitSpriteEditorDataProvider();

            var existing = new Dictionary<string, GUID>();
            foreach (SpriteRect current in provider.GetSpriteRects())
                existing[current.name] = current.spriteID;

            int height = texture.height;
            var rects = new List<SpriteRect>();
            var pairs = new List<SpriteNameFileIdPair>();

            for (int row = 0; row < AbominationAnimation.Clips.Length; row++)
            {
                AbominationAnimation.ClipDef def = AbominationAnimation.Clips[row];

                // A folha conta as linhas de cima para baixo; a textura, de baixo para cima.
                int y = height - (row + 1) * CellHeight;

                for (int column = 0; column < def.Frames; column++)
                {
                    string name = SpriteName(def.Name, column);

                    var rect = new SpriteRect
                    {
                        name = name,
                        rect = new Rect(column * CellWidth, y, CellWidth, CellHeight),
                        alignment = SpriteAlignment.Custom,
                        pivot = Pivot,
                        spriteID = existing.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                    };

                    rects.Add(rect);
                    pairs.Add(new SpriteNameFileIdPair(name, rect.spriteID));
                }
            }

            provider.SetSpriteRects(rects.ToArray());

            var nameFileIdProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameFileIdProvider?.SetNameFileIdPairs(pairs);

            provider.Apply();

            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[Abomination] Folha cortada em {rects.Count} sprites de {CellWidth}x{CellHeight}.");
            return true;
        }

        private static string SpriteName(string clip, int frame) => $"{SpritePrefix}_{clip}_{frame}";

        private static Dictionary<string, Sprite> LoadSprites()
        {
            var map = new Dictionary<string, Sprite>();

            foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(SheetPath))
            {
                if (asset is Sprite sprite) map[sprite.name] = sprite;
            }

            return map;
        }

        // ------------------------------------------------------------------ clips

        private static AnimationClip BuildClip(
            AbominationAnimation.ClipDef def, IReadOnlyDictionary<string, Sprite> sprites)
        {
            var frames = new List<Sprite>(def.Frames);

            for (int i = 0; i < def.Frames; i++)
            {
                if (sprites.TryGetValue(SpriteName(def.Name, i), out Sprite sprite))
                {
                    frames.Add(sprite);
                    continue;
                }

                Debug.LogError($"[Abomination] Sprite '{SpriteName(def.Name, i)}' não saiu do corte.");
                return null;
            }

            string clipPath = $"{ClipFolder}/{SpritePrefix}_{def.Name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.ClearCurves();
            clip.frameRate = def.FrameRate;

            var binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite",
            };

            var keys = new ObjectReferenceKeyframe[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe { time = i / def.FrameRate, value = frames[i] };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = def.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorUtility.SetDirty(clip);
            return clip;
        }

        // ------------------------------------------------------------- controller

        private static AnimatorController BuildController(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            else ClearController(controller);

            controller.AddParameter(AbominationAnimation.Param.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(AbominationAnimation.Param.Attack, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(AbominationAnimation.Param.Hit, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(AbominationAnimation.Param.Dead, AnimatorControllerParameterType.Bool);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idle = AddState(machine, clips, AbominationAnimation.State.Idle, new Vector3(300f, 0f));
            AnimatorState walk = AddState(machine, clips, AbominationAnimation.State.Walk, new Vector3(300f, 80f));
            AnimatorState attack = AddState(machine, clips, AbominationAnimation.State.Attack, new Vector3(600f, 40f));
            AnimatorState hit = AddState(machine, clips, AbominationAnimation.State.Hit, new Vector3(0f, 160f));
            AnimatorState death = AddState(machine, clips, AbominationAnimation.State.Death, new Vector3(300f, 240f));

            machine.defaultState = idle;

            Connect(idle, walk).WithGreater(AbominationAnimation.Param.Speed, AbominationAnimation.SpeedThreshold);
            Connect(walk, idle).WithLess(AbominationAnimation.Param.Speed, AbominationAnimation.SpeedThreshold);

            // Morta não golpeia nem apanha: a condição de Dead é o que impede um
            // trigger atrasado de ressuscitar a pose.
            AnyStateTo(machine, attack).WithTrigger(AbominationAnimation.Param.Attack)
                .WithBool(AbominationAnimation.Param.Dead, false);

            AnyStateTo(machine, hit).WithTrigger(AbominationAnimation.Param.Hit)
                .WithBool(AbominationAnimation.Param.Dead, false);

            AnyStateTo(machine, death).WithBool(AbominationAnimation.Param.Dead, true);

            foreach (AnimatorState state in new[] { attack, hit })
                ExitToIdle(state, idle);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ClearController(AnimatorController controller)
        {
            for (int i = controller.parameters.Length - 1; i >= 0; i--)
                controller.RemoveParameter(i);

            for (int i = controller.layers.Length - 1; i > 0; i--)
                controller.RemoveLayer(i);

            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
                return;
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorStateTransition[] anyTransitions = machine.anyStateTransitions;
            for (int i = anyTransitions.Length - 1; i >= 0; i--)
                machine.RemoveAnyStateTransition(anyTransitions[i]);

            AnimatorTransition[] entryTransitions = machine.entryTransitions;
            for (int i = entryTransitions.Length - 1; i >= 0; i--)
                machine.RemoveEntryTransition(entryTransitions[i]);

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
                machine.RemoveStateMachine(child.stateMachine);

            foreach (ChildAnimatorState child in machine.states)
                machine.RemoveState(child.state);
        }

        private static AnimatorState AddState(
            AnimatorStateMachine machine,
            IReadOnlyDictionary<string, AnimationClip> clips,
            string name,
            Vector3 position)
        {
            AnimatorState state = machine.AddState(name, position);
            if (clips.TryGetValue(name, out AnimationClip clip)) state.motion = clip;
            state.writeDefaultValues = false;
            return state;
        }

        private static AnimatorStateTransition AnyStateTo(AnimatorStateMachine machine, AnimatorState to)
        {
            AnimatorStateTransition transition = machine.AddAnyStateTransition(to);
            Configure(transition);
            transition.canTransitionToSelf = false;
            return transition;
        }

        private static void ExitToIdle(AnimatorState from, AnimatorState idle)
        {
            AnimatorStateTransition transition = from.AddTransition(idle);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
        }

        private static AnimatorStateTransition Connect(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            Configure(transition);
            return transition;
        }

        private static void Configure(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
        }

        private static AnimatorStateTransition WithBool(
            this AnimatorStateTransition transition, string parameter, bool expected)
        {
            transition.AddCondition(
                expected ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
            return transition;
        }

        private static AnimatorStateTransition WithTrigger(
            this AnimatorStateTransition transition, string parameter)
        {
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
            return transition;
        }

        private static AnimatorStateTransition WithGreater(
            this AnimatorStateTransition transition, string parameter, float threshold)
        {
            transition.AddCondition(AnimatorConditionMode.Greater, threshold, parameter);
            return transition;
        }

        private static AnimatorStateTransition WithLess(
            this AnimatorStateTransition transition, string parameter, float threshold)
        {
            transition.AddCondition(AnimatorConditionMode.Less, threshold, parameter);
            return transition;
        }
    }
}
