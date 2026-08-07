using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Warana.Player;

namespace Warana.EditorTools
{
    /// <summary>
    /// Transforma as pastas de frames exportadas em Assets/Animations/warana
    /// em sprites configurados, AnimationClips e um AnimatorController.
    /// Idempotente: rodar de novo regenera tudo a partir dos PNGs.
    /// </summary>
    public static class WaranaAnimationBuilder
    {
        private const string SourceRoot = "Assets/Animations/warana";
        private const string ClipFolder = "Assets/Animations/Warana/Clips";
        private const string ControllerPath = "Assets/Animations/Warana/Warana.controller";

        /// <summary>Alpha acima disso conta como pixel desenhado ao procurar o chão do sprite.</summary>
        private const byte OpaqueThreshold = 8;

        /// <summary>Uma animação: de onde vêm os frames e como eles tocam.</summary>
        private readonly struct ClipDef
        {
            public readonly string Name;
            public readonly string Folder;
            public readonly int FirstFrame;
            public readonly int FrameCount; // 0 = até o fim da pasta
            public readonly float FrameRate;
            public readonly bool Loop;

            public ClipDef(string name, string folder, int firstFrame, int frameCount, float frameRate, bool loop)
            {
                Name = name;
                Folder = folder;
                FirstFrame = firstFrame;
                FrameCount = frameCount;
                FrameRate = frameRate;
                Loop = loop;
            }
        }

        // A pasta 'jumping' é um arco completo (agacha, sobe, ápice, aterrissa).
        // Frames 0-3 são a subida; 4-5 são o ápice, que serve de queda em loop.
        private static readonly ClipDef[] Clips =
        {
            new ClipDef(WaranaAnimation.State.Idle,   "Idle_true",    0, 0, 8f,    true),
            new ClipDef(WaranaAnimation.State.Run,    "Running_true", 0, 0, 14f,   true),
            new ClipDef(WaranaAnimation.State.Walk,   "Walking",      0, 0, 10f,   true),
            new ClipDef(WaranaAnimation.State.Jump,   "jumping",      0, 4, 14f,   false),
            new ClipDef(WaranaAnimation.State.Fall,   "jumping",      4, 2, 8f,    true),
            new ClipDef(WaranaAnimation.State.Attack, "attack_true",  0, 0, 12.5f, false), // 5 frames = 0.4s, igual ao PlayerAttack
            new ClipDef(WaranaAnimation.State.Death,  "dying",        0, 0, 10f,   false),
        };

        [MenuItem("Waraná/Animação/Gerar Clips e Animator")]
        public static AnimatorController Build()
        {
            ConfigureSprites();

            Directory.CreateDirectory(ClipFolder);
            AssetDatabase.Refresh();

            var built = new Dictionary<string, AnimationClip>();
            foreach (ClipDef def in Clips)
            {
                AnimationClip clip = BuildClip(def);
                if (clip != null) built[def.Name] = clip;
            }

            AnimatorController controller = BuildController(built);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Waraná] {built.Count} clips e o Animator gerados em Assets/Animations/Warana.");
            return controller;
        }

        // ---------------------------------------------------------------- sprites

        /// <summary>
        /// Aplica PPU do projeto e ancora o pivô no chão de cada animação, para o
        /// personagem não pular de altura ao trocar de estado.
        /// </summary>
        [MenuItem("Waraná/Animação/Configurar Sprites do Waraná")]
        public static void ConfigureSprites()
        {
            // Recursivo: alguns exports (ex.: attack_1) aninham os frames em subpastas por direção.
            string[] folders = Directory.GetDirectories(SourceRoot, "*", SearchOption.AllDirectories);
            int configured = 0;

            foreach (string folder in folders)
            {
                string[] frames = Directory.GetFiles(folder, "frame_*.png");
                if (frames.Length == 0) continue;

                float pivotY = ComputeGroundPivotY(frames);

                foreach (string frame in frames)
                {
                    string assetPath = frame.Replace('\\', '/');
                    if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer) continue;

                    PixelArtTextureDefaults.ApplyQuality(importer);

                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMode = (int)SpriteImportMode.Single;
                    settings.spritePixelsPerUnit = PixelArtTextureDefaults.PixelsPerUnit;
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(0.5f, pivotY);
                    importer.SetTextureSettings(settings);

                    importer.SaveAndReimport();
                    configured++;
                }
            }

            Debug.Log($"[Waraná] {configured} sprite(s) configurados a {PixelArtTextureDefaults.PixelsPerUnit} PPU.");
        }

        /// <summary>
        /// Devolve, em coordenadas normalizadas, a linha do pixel mais baixo desenhado
        /// em toda a pasta — o ponto em que o personagem toca o chão.
        /// </summary>
        private static float ComputeGroundPivotY(IReadOnlyList<string> frames)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            int lowestRow = int.MaxValue;
            int height = 0;

            try
            {
                foreach (string frame in frames)
                {
                    if (!texture.LoadImage(File.ReadAllBytes(frame))) continue;

                    height = texture.height;
                    Color32[] pixels = texture.GetPixels32(); // linha 0 = base da imagem

                    for (int y = 0; y < height && y < lowestRow; y++)
                    {
                        int rowStart = y * texture.width;
                        for (int x = 0; x < texture.width; x++)
                        {
                            if (pixels[rowStart + x].a <= OpaqueThreshold) continue;

                            lowestRow = y;
                            break;
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            if (height == 0 || lowestRow == int.MaxValue) return 0.5f;

            return (float)lowestRow / height;
        }

        // ------------------------------------------------------------------ clips

        private static AnimationClip BuildClip(ClipDef def)
        {
            var sprites = new List<Sprite>();
            int count = def.FrameCount > 0 ? def.FrameCount : int.MaxValue;

            for (int i = def.FirstFrame; sprites.Count < count; i++)
            {
                string path = $"{SourceRoot}/{def.Folder}/frame_{i:D3}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) break;

                sprites.Add(sprite);
            }

            if (sprites.Count == 0)
            {
                Debug.LogError($"[Waraná] Nenhum frame encontrado para '{def.Name}' em {SourceRoot}/{def.Folder}.");
                return null;
            }

            string clipPath = $"{ClipFolder}/Warana_{def.Name}.anim";
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

            // Uma key por frame. A Unity já estende o clip por 1/frameRate depois da
            // última key, então o comprimento sai exatamente em frames/frameRate.
            var keys = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe { time = i / def.FrameRate, value = sprites[i] };
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
            // Reaproveitar o asset é essencial: apagar e recriar destrói o objeto em
            // memória, e todo Animator de cena que apontava para ele fica com o campo
            // nulo ("Animator is not playing an AnimatorController") mesmo com o GUID
            // intacto no disco. Limpar no lugar mantém o vínculo vivo.
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            else ClearController(controller);

            controller.AddParameter(WaranaAnimation.Param.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(WaranaAnimation.Param.VelocityY, AnimatorControllerParameterType.Float);
            controller.AddParameter(WaranaAnimation.Param.Grounded, AnimatorControllerParameterType.Bool);
            controller.AddParameter(WaranaAnimation.Param.Walk, AnimatorControllerParameterType.Bool);
            controller.AddParameter(WaranaAnimation.Param.Dead, AnimatorControllerParameterType.Bool);
            controller.AddParameter(WaranaAnimation.Param.Attack, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idle = AddState(machine, clips, WaranaAnimation.State.Idle, new Vector3(320f, 0f));
            AnimatorState run = AddState(machine, clips, WaranaAnimation.State.Run, new Vector3(320f, 90f));
            AnimatorState walk = AddState(machine, clips, WaranaAnimation.State.Walk, new Vector3(320f, 180f));
            AnimatorState jump = AddState(machine, clips, WaranaAnimation.State.Jump, new Vector3(640f, 0f));
            AnimatorState fall = AddState(machine, clips, WaranaAnimation.State.Fall, new Vector3(640f, 90f));
            AnimatorState attack = AddState(machine, clips, WaranaAnimation.State.Attack, new Vector3(320f, 300f));
            AnimatorState death = AddState(machine, clips, WaranaAnimation.State.Death, new Vector3(320f, 390f));

            machine.defaultState = idle;

            AnimatorState[] grounded = { idle, run, walk };

            // Chão -> ar. Grounded IfNot evita disparar no frame do impulso, em que o
            // sensor ainda vê o chão mas a velocidade vertical já subiu.
            foreach (AnimatorState state in grounded)
            {
                Connect(state, jump)
                    .WithBool(WaranaAnimation.Param.Grounded, false)
                    .WithGreater(WaranaAnimation.Param.VelocityY, WaranaAnimation.VerticalThreshold);

                Connect(state, fall)
                    .WithBool(WaranaAnimation.Param.Grounded, false)
                    .WithLess(WaranaAnimation.Param.VelocityY, WaranaAnimation.VerticalThreshold);
            }

            // Locomoção no chão. Running é o padrão; Walking só com o bool ligado.
            Connect(idle, run)
                .WithGreater(WaranaAnimation.Param.Speed, WaranaAnimation.SpeedThreshold)
                .WithBool(WaranaAnimation.Param.Walk, false);

            Connect(idle, walk)
                .WithGreater(WaranaAnimation.Param.Speed, WaranaAnimation.SpeedThreshold)
                .WithBool(WaranaAnimation.Param.Walk, true);

            Connect(run, idle).WithLess(WaranaAnimation.Param.Speed, WaranaAnimation.SpeedThreshold);
            Connect(walk, idle).WithLess(WaranaAnimation.Param.Speed, WaranaAnimation.SpeedThreshold);
            Connect(run, walk).WithBool(WaranaAnimation.Param.Walk, true);
            Connect(walk, run).WithBool(WaranaAnimation.Param.Walk, false);

            // Aterrissagem. Vem antes de jump -> fall e exige velocidade negativa,
            // então nunca compete com a subida.
            foreach (AnimatorState air in new[] { jump, fall })
            {
                Connect(air, run)
                    .WithBool(WaranaAnimation.Param.Grounded, true)
                    .WithLess(WaranaAnimation.Param.VelocityY, WaranaAnimation.VerticalThreshold)
                    .WithGreater(WaranaAnimation.Param.Speed, WaranaAnimation.SpeedThreshold)
                    .WithBool(WaranaAnimation.Param.Walk, false);

                Connect(air, walk)
                    .WithBool(WaranaAnimation.Param.Grounded, true)
                    .WithLess(WaranaAnimation.Param.VelocityY, WaranaAnimation.VerticalThreshold)
                    .WithGreater(WaranaAnimation.Param.Speed, WaranaAnimation.SpeedThreshold)
                    .WithBool(WaranaAnimation.Param.Walk, true);

                Connect(air, idle)
                    .WithBool(WaranaAnimation.Param.Grounded, true)
                    .WithLess(WaranaAnimation.Param.VelocityY, WaranaAnimation.VerticalThreshold);
            }

            Connect(jump, fall)
                .WithBool(WaranaAnimation.Param.Grounded, false)
                .WithLess(WaranaAnimation.Param.VelocityY, WaranaAnimation.VerticalThreshold);

            // Ataque interrompe qualquer coisa, menos a morte.
            AnimatorStateTransition toAttack = machine.AddAnyStateTransition(attack);
            Configure(toAttack);
            toAttack.canTransitionToSelf = false;
            toAttack
                .WithTrigger(WaranaAnimation.Param.Attack)
                .WithBool(WaranaAnimation.Param.Dead, false);

            // Sai no fim do clip para Idle; de lá o grafo reencaminha sozinho.
            AnimatorStateTransition attackExit = attack.AddTransition(idle);
            attackExit.hasExitTime = true;
            attackExit.exitTime = 1f;
            attackExit.hasFixedDuration = true;
            attackExit.duration = 0f;

            AnimatorStateTransition toDeath = machine.AddAnyStateTransition(death);
            Configure(toDeath);
            toDeath.canTransitionToSelf = false;
            toDeath.WithBool(WaranaAnimation.Param.Dead, true);

            Connect(death, idle).WithBool(WaranaAnimation.Param.Dead, false); // respawn

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Esvazia o controller — parâmetros, camadas extras, estados e transições —
        /// deixando só a Base Layer com um state machine vazio, pronto para reconstruir.
        /// Usa as APIs de Remove* em vez de trocar objetos, para que os sub-assets
        /// antigos sejam destruídos e o .controller não vá inchando a cada geração.
        /// </summary>
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

            // As transições de Any State primeiro: RemoveState limpa as que chegam num
            // estado, mas não as que saem do Any State para ele.
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
