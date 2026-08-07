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
    /// Corta a folha de sprites do Mad Ghost e gera os AnimationClips e o
    /// AnimatorController a partir dela. Idempotente: rodar de novo refaz tudo
    /// a partir do PNG, sem duplicar asset.
    ///
    /// A folha usada é a "Separated tags": 9 x 7 células de 64x64, uma linha por
    /// animação, na mesma ordem de <see cref="MadGhostAnimation.Clips"/>. A linha tem
    /// 9 colunas porque a maior animação (Death) tem 9 frames; as células que sobram
    /// nas outras linhas estão vazias e não viram sprite.
    ///
    /// Os assets gerados ficam FORA da pasta do pacote comprado: o vendor continua
    /// intocado (só as import settings mudam), e o que é nosso vive em
    /// <see cref="OutputRoot"/>.
    /// </summary>
    public static class MadGhostAnimationBuilder
    {
        private const string SheetPath =
            "Assets/Animations/Enemies/Horror Enemy Pack/Mad Ghost/Mad Ghost-Sheet Separated tags.png";

        private const string OutputRoot = "Assets/Animations/Enemies/Mad Ghost";
        private const string ClipFolder = OutputRoot + "/Clips";
        private const string ControllerPath = OutputRoot + "/MadGhost.controller";

        private const string SpritePrefix = "MadGhost";

        /// <summary>Lado da célula na folha, em pixels. É o canvas do personagem.</summary>
        private const int Cell = 64;

        /// <summary>
        /// Pivô uniforme, no centro da base do canvas.
        ///
        /// De propósito NÃO é calculado a partir dos pixels desenhados, como no
        /// WaranaAnimationBuilder: lá cada animação vinha de um export separado, aqui o
        /// artista já desenhou tudo alinhado dentro do mesmo canvas de 64x64. Medir o
        /// conteúdo frame a frame *destruiria* esse alinhamento — o corpo se desloca de
        /// propósito entre Idle e Move, e o efeito do golpe passa por fora da silhueta.
        /// Um pivô fixo preserva a intenção da arte e elimina qualquer pulo ao trocar
        /// de estado.
        /// </summary>
        private static readonly Vector2 Pivot = new Vector2(0.5f, 0f);

        [MenuItem("Waraná/Animação/Gerar Mad Ghost")]
        public static AnimatorController Build()
        {
            if (!SliceSheet()) return null;

            Directory.CreateDirectory(ClipFolder);
            AssetDatabase.Refresh();

            Dictionary<string, Sprite> sprites = LoadSprites();
            var built = new Dictionary<string, AnimationClip>();

            foreach (MadGhostAnimation.ClipDef def in MadGhostAnimation.Clips)
            {
                AnimationClip clip = BuildClip(def, sprites);
                if (clip != null) built[def.Name] = clip;
            }

            AnimatorController controller = BuildController(built);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MadGhost] {built.Count} clips e o Animator gerados em {OutputRoot}.");
            return controller;
        }

        // ------------------------------------------------------------------ corte

        /// <summary>
        /// Reescreve os retângulos da folha, um por frame. Só as células realmente
        /// usadas viram sprite, então não sobra lixo transparente nomeado no asset.
        ///
        /// O corte passa pelo ISpriteEditorDataProvider, e não pelo antigo
        /// <c>TextureImporter.spritesheet</c>: na Unity 6 aquela propriedade não é só
        /// obsoleta, ela deixou de ter efeito — escrever nela compila e não corta nada.
        /// </summary>
        private static bool SliceSheet()
        {
            if (AssetImporter.GetAtPath(SheetPath) is not TextureImporter importer)
            {
                Debug.LogError($"[MadGhost] Folha não encontrada em {SheetPath}.");
                return false;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (texture == null)
            {
                Debug.LogError($"[MadGhost] Não consegui carregar a textura em {SheetPath}.");
                return false;
            }

            // O modo Multiple precisa valer ANTES de pedir o provider: em modo Single
            // ele devolve um único retângulo e ignora o que a gente escrever.
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
                Debug.LogError("[MadGhost] Sem ISpriteEditorDataProvider para a folha.");
                return false;
            }

            provider.InitSpriteEditorDataProvider();

            // Reaproveitar o spriteID de quem já existe é o que mantém vivas as
            // referências de clips e prefabs ao regerar: um GUID novo a cada execução
            // apagaria o sprite de todo lugar que já apontava para ele.
            var existing = new Dictionary<string, GUID>();
            foreach (SpriteRect current in provider.GetSpriteRects())
                existing[current.name] = current.spriteID;

            int height = texture.height;
            var rects = new List<SpriteRect>();
            var pairs = new List<SpriteNameFileIdPair>();

            for (int row = 0; row < MadGhostAnimation.Clips.Length; row++)
            {
                MadGhostAnimation.ClipDef def = MadGhostAnimation.Clips[row];

                // A folha conta as linhas de cima para baixo; a textura, de baixo para
                // cima. Daí a inversão — errar aqui embaralha as animações entre si.
                int y = height - (row + 1) * Cell;

                for (int column = 0; column < def.Frames; column++)
                {
                    string name = SpriteName(def.Name, column);

                    var rect = new SpriteRect
                    {
                        name = name,
                        rect = new Rect(column * Cell, y, Cell, Cell),
                        alignment = SpriteAlignment.Custom,
                        pivot = Pivot,
                        spriteID = existing.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                    };

                    rects.Add(rect);
                    pairs.Add(new SpriteNameFileIdPair(name, rect.spriteID));
                }
            }

            provider.SetSpriteRects(rects.ToArray());

            // A tabela nome -> fileId anda junto: sem atualizá-la, o asset guarda
            // nomes velhos e os sprites reaparecem duplicados no próximo import.
            var nameFileIdProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameFileIdProvider?.SetNameFileIdPairs(pairs);

            provider.Apply();

            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[MadGhost] Folha cortada em {rects.Count} sprites de {Cell}x{Cell}.");
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
            MadGhostAnimation.ClipDef def, IReadOnlyDictionary<string, Sprite> sprites)
        {
            var frames = new List<Sprite>(def.Frames);

            for (int i = 0; i < def.Frames; i++)
            {
                if (sprites.TryGetValue(SpriteName(def.Name, i), out Sprite sprite))
                {
                    frames.Add(sprite);
                    continue;
                }

                Debug.LogError($"[MadGhost] Sprite '{SpriteName(def.Name, i)}' não saiu do corte.");
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
        /// O grafo é deliberadamente burro: cada fase do ataque tem o seu próprio
        /// trigger, e quem encadeia é o <see cref="MadGhost"/>, não o Animator.
        ///
        /// Encadear por exit time aqui deixaria a duração do comportamento escondida
        /// dentro de um asset binário — e o script precisa saber exatamente quando o
        /// golpe sai. Com a FSM no controle, os tempos vêm todos de
        /// <see cref="MadGhostAnimation.Clips"/>, um lugar só, legível em diff.
        /// As transições por exit time que sobram são só rede de segurança, para
        /// nenhum estado ficar preso se um trigger se perder.
        /// </summary>
        private static AnimatorController BuildController(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            // Reaproveitar o asset mantém vivo o vínculo dos Animators já na cena:
            // recriar do zero deixaria o campo nulo mesmo com o GUID intacto no disco.
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            else ClearController(controller);

            controller.AddParameter(MadGhostAnimation.Param.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(MadGhostAnimation.Param.Unsheathe, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(MadGhostAnimation.Param.Attack, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(MadGhostAnimation.Param.Sheathe, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(MadGhostAnimation.Param.Hit, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(MadGhostAnimation.Param.Dead, AnimatorControllerParameterType.Bool);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idle = AddState(machine, clips, MadGhostAnimation.State.Idle, new Vector3(300f, 0f));
            AnimatorState move = AddState(machine, clips, MadGhostAnimation.State.Move, new Vector3(300f, 80f));
            AnimatorState unsheathe = AddState(machine, clips, MadGhostAnimation.State.Unsheathe, new Vector3(600f, 0f));
            AnimatorState attack = AddState(machine, clips, MadGhostAnimation.State.Attack, new Vector3(600f, 80f));
            AnimatorState sheathe = AddState(machine, clips, MadGhostAnimation.State.Sheathe, new Vector3(600f, 160f));
            AnimatorState hit = AddState(machine, clips, MadGhostAnimation.State.Hit, new Vector3(0f, 160f));
            AnimatorState death = AddState(machine, clips, MadGhostAnimation.State.Death, new Vector3(300f, 240f));

            machine.defaultState = idle;

            Connect(idle, move).WithGreater(MadGhostAnimation.Param.Speed, MadGhostAnimation.SpeedThreshold);
            Connect(move, idle).WithLess(MadGhostAnimation.Param.Speed, MadGhostAnimation.SpeedThreshold);

            // Morto não saca, não apanha e não anda: a condição de Dead em cada
            // transição é o que impede um trigger atrasado de ressuscitar a pose.
            AnyStateTo(machine, unsheathe).WithTrigger(MadGhostAnimation.Param.Unsheathe)
                .WithBool(MadGhostAnimation.Param.Dead, false);

            AnyStateTo(machine, attack).WithTrigger(MadGhostAnimation.Param.Attack)
                .WithBool(MadGhostAnimation.Param.Dead, false);

            AnyStateTo(machine, sheathe).WithTrigger(MadGhostAnimation.Param.Sheathe)
                .WithBool(MadGhostAnimation.Param.Dead, false);

            AnyStateTo(machine, hit).WithTrigger(MadGhostAnimation.Param.Hit)
                .WithBool(MadGhostAnimation.Param.Dead, false);

            AnyStateTo(machine, death).WithBool(MadGhostAnimation.Param.Dead, true);

            // Rede de segurança: sem isso, um trigger perdido deixaria o inimigo
            // congelado no último frame do clip para sempre.
            foreach (AnimatorState state in new[] { unsheathe, attack, sheathe, hit })
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

            // As de Any State primeiro: RemoveState limpa as que chegam num estado,
            // mas não as que saem do Any State para ele.
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

        /// <summary>Troca seca: sprite não interpola, então blend só atrasaria a resposta.</summary>
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
