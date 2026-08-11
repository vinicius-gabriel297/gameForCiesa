using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Warana.Environment
{
    /// <summary>
    /// O estado da floresta em um número só: <c>0</c> é a mata que Jurupari corrompeu,
    /// <c>1</c> é a região devolvida ao equilíbrio.
    ///
    /// <para>Tudo o que muda de aparência entre os dois estados — o tinto das árvores e
    /// do fundo em camadas, a árvore guardiã, a luz global, o pós-processamento, as
    /// folhas que caem e as partículas de corrupção — passa por
    /// <see cref="SetProgress"/>. A alternativa era um script por efeito, todos
    /// escrevendo nos mesmos renderers em ordem indeterminada. Com um valor único
    /// qualquer momento do jogo pode pedir um estado *parcial* — a queda da chefe alivia,
    /// a árvore sagrada cura — sem ninguém precisar combinar nada com ninguém.</para>
    ///
    /// <para>O estado corrompido não é escrito aqui: é o que já está autorado na cena.
    /// Quem retintar a mata no Editor não precisa vir corrigir um segundo valor.</para>
    /// </summary>
    [AddComponentMenu("Waraná/Ambiente/Restauração da Floresta")]
    public class ForestRestoration : MonoBehaviour
    {
        /// <summary>
        /// Um conjunto de sprites que revive junto. O alvo não é uma cor escrita à mão:
        /// é a própria cor autorada, com saturação e brilho puxados para cima. Assim as
        /// 23 árvores da cena continuam sendo dois campos, e não 23 referências
        /// arrastadas uma a uma.
        /// </summary>
        [System.Serializable]
        private class TintedGroup
        {
            [Tooltip("Raiz do grupo. Todos os SpriteRenderer abaixo dela entram, inclusive os inativos.")]
            public Transform root;

            [Range(0f, 1f)] public float saturationBoost = 0.3f;

            [Range(0f, 1f)] public float brightnessBoost = 0.25f;

            [System.NonSerialized] public SpriteRenderer[] Renderers;
            [System.NonSerialized] public Color[] Corrupted;
            [System.NonSerialized] public Color[] Restored;
        }

        [Header("Sprites que revivem")]
        [Tooltip("Grupos de sprites tintados. Tipicamente Scene/Trees e Scene/Parallax.")]
        [SerializeField]
        private TintedGroup[] tintedGroups =
        {
            new TintedGroup { saturationBoost = 0.35f, brightnessBoost = 0.30f },
            new TintedGroup { saturationBoost = 0.15f, brightnessBoost = 0.26f },
        };

        [Header("Árvore guardiã")]
        [Tooltip("A castanheira morta — o que a cena mostra corrompida. Sai do azul frio da " +
                 "corrupção para um branco quente.")]
        [SerializeField] private SpriteRenderer guardianVisual;

        [Tooltip("A mesma castanheira viva, desenhada à frente da morta. Entra por alpha, de 0 a 1.\n\n" +
                 "As duas não fazem alpha cruzado: fundir uma saindo com a outra entrando deixaria o " +
                 "cenário aparecer através das duas metades no meio do caminho, e a árvore leria como " +
                 "fantasma. Com a morta opaca por baixo, o que se vê é a copa preenchendo sobre os " +
                 "galhos secos — que é o que a cura é.")]
        [SerializeField] private SpriteRenderer guardianAliveVisual;

        [SerializeField] private Color guardianRestoredTint = new Color(1f, 0.96f, 0.86f);

        [Tooltip("Luz da árvore. O brilho sobe pelo multiplicador do pulso, para a respiração dela não parar.")]
        [SerializeField] private PulsingLight2D guardianGlow;

        [SerializeField] private float guardianGlowScale = 1.9f;

        [SerializeField] private float guardianGlowRadius = 11f;

        [Header("Luz global")]
        [SerializeField] private Light2D globalLight;

        [Tooltip("Tom que a luz global assume restaurada — o amanhecer depois da noite corrompida.")]
        [SerializeField] private Color globalRestoredColor = new Color(1f, 0.92f, 0.80f);

        [SerializeField] private float globalRestoredIntensity = 1.25f;

        [Header("Pós-processamento")]
        [Tooltip("O Volume global da fase. Vazio = o pós-processamento não muda.")]
        [SerializeField] private Volume volume;

        [SerializeField] private float restoredSaturation = 20f;

        [SerializeField] private float restoredTemperature = 10f;

        [SerializeField] private float restoredTint = 2f;

        [SerializeField] private float restoredVignette = 0.10f;

        [SerializeField] private float restoredBloom = 0.9f;

        [Header("Folhas que caem")]
        [Tooltip("O sistema de folhas preso à câmera. O mesmo sistema conta os dois estados — " +
                 "ligar e desligar dois sistemas diferentes leria como dois efeitos, não como uma mudança.")]
        [SerializeField] private ParticleSystem fallingLeaves;

        [SerializeField] private Material corruptedLeafMaterial;

        [SerializeField] private Material restoredLeafMaterial;

        [SerializeField] private Color corruptedLeafTint = new Color(0.45f, 0.34f, 0.20f);

        [Tooltip("Progresso em que a folha morta vira folha viva. No meio do caminho, e não no fim, " +
                 "porque a troca é instantânea e precisa acontecer enquanto a luz ainda está subindo.")]
        [Range(0f, 1f)]
        [SerializeField] private float leafSwapProgress = 0.55f;

        [Header("Corrupção")]
        [Tooltip("Partículas escuras à deriva. A emissão cai junto com o progresso até parar de vez.")]
        [SerializeField] private ParticleSystem corruptionFx;

        private Color _guardianCorruptedTint;
        private Color _guardianAliveCorruptedTint;
        private float _guardianCorruptedRadius;
        private Color _globalCorruptedColor;
        private float _globalCorruptedIntensity;
        private Color _restoredLeafTint;
        private float _corruptionRate;

        private ColorAdjustments _colorAdjustments;
        private WhiteBalance _whiteBalance;
        private Vignette _vignette;
        private Bloom _bloom;

        private float _corruptedSaturation;
        private float _corruptedTemperature;
        private float _corruptedTint;
        private float _corruptedVignette;
        private float _corruptedBloom;

        private bool _leavesRestored;

        /// <summary>Último valor aplicado. Começa em zero: a fase abre corrompida.</summary>
        public float Progress { get; private set; }

        private void Awake()
        {
            CacheGroups();

            if (guardianVisual != null) _guardianCorruptedTint = guardianVisual.color;

            // A cor autorada da árvore viva vale mesmo com o alpha dela zerado na cena:
            // só o RGB é lido, o alpha quem escreve é o progresso.
            if (guardianAliveVisual != null) _guardianAliveCorruptedTint = guardianAliveVisual.color;

            if (guardianGlow != null)
            {
                var light = guardianGlow.GetComponent<Light2D>();
                if (light != null) _guardianCorruptedRadius = light.pointLightOuterRadius;
            }

            if (globalLight != null)
            {
                _globalCorruptedColor = globalLight.color;
                _globalCorruptedIntensity = globalLight.intensity;
            }

            if (fallingLeaves != null)
            {
                _restoredLeafTint = fallingLeaves.main.startColor.color;
                _leavesRestored = true; // é o estado autorado; SetProgress(0) faz a primeira troca
            }

            if (corruptionFx != null) _corruptionRate = corruptionFx.emission.rateOverTime.constant;

            CacheVolume();
        }

        // A fase precisa nascer corrompida antes do primeiro frame visível. Start, e não
        // Awake, porque o LevelIntroController abre a cena em preto e só clareia depois.
        private void Start() => SetProgress(0f);

        /// <summary>
        /// Aplica o estado da floresta. <paramref name="progress"/> vai de <c>0</c>
        /// (corrompida) a <c>1</c> (restaurada); valores intermediários são estados
        /// legítimos — a queda da chefe para na metade do caminho.
        /// </summary>
        public void SetProgress(float progress)
        {
            Progress = Mathf.Clamp01(progress);

            ApplyGroups(Progress);
            ApplyGuardian(Progress);
            ApplyGlobalLight(Progress);
            ApplyVolume(Progress);
            ApplyLeaves(Progress);
            ApplyCorruption(Progress);
        }

        private void CacheGroups()
        {
            if (tintedGroups == null) return;

            foreach (TintedGroup group in tintedGroups)
            {
                if (group == null || group.root == null)
                {
                    if (group != null) group.Renderers = new SpriteRenderer[0];
                    continue;
                }

                group.Renderers = group.root.GetComponentsInChildren<SpriteRenderer>(true);
                group.Corrupted = new Color[group.Renderers.Length];
                group.Restored = new Color[group.Renderers.Length];

                for (int i = 0; i < group.Renderers.Length; i++)
                {
                    Color authored = group.Renderers[i].color;
                    group.Corrupted[i] = authored;
                    group.Restored[i] = Boost(authored, group.saturationBoost, group.brightnessBoost);
                }
            }
        }

        private void CacheVolume()
        {
            if (volume == null) return;

            // .profile, e nunca .sharedProfile: o segundo é o asset em disco, e escrever
            // nele em play mode grava a floresta curada no projeto para sempre.
            VolumeProfile runtime = volume.profile;

            if (runtime.TryGet(out _colorAdjustments)) _corruptedSaturation = _colorAdjustments.saturation.value;
            if (runtime.TryGet(out _whiteBalance))
            {
                _corruptedTemperature = _whiteBalance.temperature.value;
                _corruptedTint = _whiteBalance.tint.value;
            }
            if (runtime.TryGet(out _vignette)) _corruptedVignette = _vignette.intensity.value;
            if (runtime.TryGet(out _bloom)) _corruptedBloom = _bloom.intensity.value;
        }

        private void ApplyGroups(float t)
        {
            if (tintedGroups == null) return;

            foreach (TintedGroup group in tintedGroups)
            {
                if (group?.Renderers == null) continue;

                for (int i = 0; i < group.Renderers.Length; i++)
                {
                    if (group.Renderers[i] == null) continue;
                    group.Renderers[i].color = Color.Lerp(group.Corrupted[i], group.Restored[i], t);
                }
            }
        }

        private void ApplyGuardian(float t)
        {
            if (guardianVisual != null)
            {
                guardianVisual.color = Color.Lerp(_guardianCorruptedTint, guardianRestoredTint, t);

                // Desligada só quando a viva já a cobre por inteiro. Enquanto houver
                // qualquer transparência na copa nova, os galhos secos por trás são o
                // que dá volume à passagem de uma para a outra.
                guardianVisual.enabled = t < 1f;
            }

            if (guardianAliveVisual != null)
            {
                Color alive = Color.Lerp(_guardianAliveCorruptedTint, guardianRestoredTint, t);
                alive.a = t;

                guardianAliveVisual.color = alive;
                guardianAliveVisual.enabled = t > 0f;
            }

            if (guardianGlow == null) return;

            guardianGlow.IntensityScale = Mathf.Lerp(1f, guardianGlowScale, t);

            var light = guardianGlow.GetComponent<Light2D>();
            if (light != null)
                light.pointLightOuterRadius = Mathf.Lerp(_guardianCorruptedRadius, guardianGlowRadius, t);
        }

        private void ApplyGlobalLight(float t)
        {
            if (globalLight == null) return;

            globalLight.color = Color.Lerp(_globalCorruptedColor, globalRestoredColor, t);
            globalLight.intensity = Mathf.Lerp(_globalCorruptedIntensity, globalRestoredIntensity, t);
        }

        private void ApplyVolume(float t)
        {
            if (_colorAdjustments != null)
                _colorAdjustments.saturation.value = Mathf.Lerp(_corruptedSaturation, restoredSaturation, t);

            if (_whiteBalance != null)
            {
                _whiteBalance.temperature.value = Mathf.Lerp(_corruptedTemperature, restoredTemperature, t);
                _whiteBalance.tint.value = Mathf.Lerp(_corruptedTint, restoredTint, t);
            }

            if (_vignette != null)
                _vignette.intensity.value = Mathf.Lerp(_corruptedVignette, restoredVignette, t);

            if (_bloom != null)
                _bloom.intensity.value = Mathf.Lerp(_corruptedBloom, restoredBloom, t);
        }

        private void ApplyLeaves(float t)
        {
            if (fallingLeaves == null) return;

            ParticleSystem.MainModule main = fallingLeaves.main;
            main.startColor = Color.Lerp(corruptedLeafTint, _restoredLeafTint, t);

            bool restored = t >= leafSwapProgress;
            if (restored == _leavesRestored) return;

            _leavesRestored = restored;

            Material wanted = restored ? restoredLeafMaterial : corruptedLeafMaterial;
            if (wanted == null) return;

            // sharedMaterial troca a *referência* do renderer, não o asset — trocar por
            // .material criaria uma instância nova a cada volta do progresso.
            var renderer = fallingLeaves.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) renderer.sharedMaterial = wanted;
        }

        private void ApplyCorruption(float t)
        {
            if (corruptionFx == null) return;

            ParticleSystem.EmissionModule emission = corruptionFx.emission;
            emission.rateOverTime = _corruptionRate * (1f - t);
        }

        /// <summary>Mesma cor, com saturação e brilho somados — a versão viva dela.</summary>
        private static Color Boost(Color source, float saturation, float brightness)
        {
            Color.RGBToHSV(source, out float h, out float s, out float v);

            Color boosted = Color.HSVToRGB(h, Mathf.Clamp01(s + saturation), Mathf.Clamp01(v + brightness));
            boosted.a = source.a;

            return boosted;
        }
    }
}
