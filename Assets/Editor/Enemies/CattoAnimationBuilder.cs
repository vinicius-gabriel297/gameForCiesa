using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Warana.Enemies;

namespace Warana.EditorTools
{
    /// <summary>
    /// Gera os AnimationClips e o AnimatorController do Catto a partir da folha já
    /// cortada pelo fornecedor ("Cat monster-Sheet Separated tags.png", sprites
    /// "..._0" a "..._33", tamanhos não uniformes). Diferente do MadGhostAnimationBuilder,
    /// não recorta nada: só lê os sub-sprites que já existem, pelos índices em
    /// <see cref="CattoAnimation.Clips"/>.
    ///
    /// Idempotente: rodar de novo regenera clips e controller a partir da folha.
    /// </summary>
    public static class CattoAnimationBuilder
    {
        private const string SheetPath =
            "Assets/Animations/Enemies/Horror Enemy Pack/Catto/Cat monster-Sheet Separated tags.png";

        private const string OutputRoot = "Assets/Animations/Enemies/Catto";
        private const string ClipFolder = OutputRoot + "/Clips";
        private const string ControllerPath = OutputRoot + "/Catto.controller";

        private const string SpriteNamePrefix = "Cat monster-Sheet Separated tags_";

        [MenuItem("Waraná/Animação/Gerar Catto")]
        public static AnimatorController Build()
        {
            EnsureMultipleSpriteMode();

            Dictionary<int, Sprite> sprites = LoadSprites();
            if (sprites.Count == 0)
            {
                Debug.LogError($"[Catto] Nenhum sprite encontrado em {SheetPath}.");
                return null;
            }

            Directory.CreateDirectory(ClipFolder);
            AssetDatabase.Refresh();

            var built = new Dictionary<string, AnimationClip>();
            foreach (CattoAnimation.ClipDef def in CattoAnimation.Clips)
            {
                AnimationClip clip = BuildClip(def, sprites);
                if (clip != null) built[def.Name] = clip;
            }

            AnimatorController controller = BuildController(built);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Catto] {built.Count} clips e o Animator gerados em {OutputRoot}.");
            return controller;
        }

        // ------------------------------------------------------------------ sprites

        /// <summary>
        /// A folha do fornecedor já tem os 34 retângulos gravados no .meta, mas o modo
        /// ficou em Single (só o sprite inteiro é usado). Trocar para Multiple e
        /// reimportar reativa os sub-sprites sem precisar recalcular nenhum retângulo.
        /// </summary>
        private static void EnsureMultipleSpriteMode()
        {
            if (AssetImporter.GetAtPath(SheetPath) is not TextureImporter importer) return;
            if (importer.spriteImportMode == SpriteImportMode.Multiple) return;

            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();
        }

        private static Dictionary<int, Sprite> LoadSprites()
        {
            var map = new Dictionary<int, Sprite>();

            foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(SheetPath))
            {
                if (asset is not Sprite sprite) continue;
                if (!sprite.name.StartsWith(SpriteNamePrefix)) continue;

                string suffix = sprite.name.Substring(SpriteNamePrefix.Length);
                if (int.TryParse(suffix, out int index)) map[index] = sprite;
            }

            return map;
        }

        // ------------------------------------------------------------------ clips

        private static AnimationClip BuildClip(CattoAnimation.ClipDef def, IReadOnlyDictionary<int, Sprite> sprites)
        {
            var frames = new List<Sprite>(def.Frames);

            for (int i = 0; i < def.Frames; i++)
            {
                int index = def.FirstIndex + i;
                if (!sprites.TryGetValue(index, out Sprite sprite))
                {
                    Debug.LogError($"[Catto] Sprite de índice {index} não encontrado para '{def.Name}'.");
                    return null;
                }

                frames.Add(sprite);
            }

            string clipPath = $"{ClipFolder}/Catto_{def.Name}.anim";
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
                path = string.Empty, // SpriteRenderer no mesmo GameObject do Animator
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

        /// <summary>
        /// Sem parâmetros nem transições: a cutscene do Prólogo é quem manda tocar
        /// cada clip via <c>Animator.Play</c>, direto. Um grafo com FSM seria
        /// complexidade sem uso — o Catto aparece uma única vez, num roteiro fixo.
        /// </summary>
        private static AnimatorController BuildController(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            else ClearController(controller);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState walk = AddState(machine, clips, CattoAnimation.State.Walk, new Vector3(300f, 0f));
            AnimatorState idleSit = AddState(machine, clips, CattoAnimation.State.IdleSit, new Vector3(300f, 90f));
            AnimatorState attack = AddState(machine, clips, CattoAnimation.State.Attack, new Vector3(300f, 180f));

            machine.defaultState = walk;

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
    }
}
