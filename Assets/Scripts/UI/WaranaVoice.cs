using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Warana.UI
{
    /// <summary>
    /// A voz do Waraná durante a fase: um lugar só para escrever falas na tela.
    ///
    /// <para>Existe com fila porque, ao contrário do Prólogo e da abertura, os momentos
    /// de fala do Mapa 01 não se coordenam entre si — a recompensa de um abate cai
    /// quando o jogador matar, e a chefe percebe quando ela perceber. Sem fila, a
    /// segunda fala apagaria a primeira no meio da frase, e as duas se perderiam.</para>
    ///
    /// <para>Quem fala encontra este componente por <see cref="Instance"/> em vez de
    /// carregar uma referência de Inspector: o contador de abates e a luta de chefe não
    /// têm por que conhecer a HUD, e referência serializada em cena não sobrevive aos
    /// rebuilds automáticos de que este projeto depende.</para>
    /// </summary>
    [AddComponentMenu("Waraná/UI/Voz do Waraná")]
    public class WaranaVoice : MonoBehaviour
    {
        [Header("Alvo")]
        [SerializeField] private CanvasGroup group;

        [SerializeField] private TMP_Text text;

        [Header("Voz")]
        [Tooltip("Opcional — sem fonte ou sem clipe as falas entram em silêncio.")]
        [SerializeField] private AudioSource voiceSource;

        [SerializeField] private AudioClip voiceClip;

        [Tooltip("A voz toca só na primeira fala de cada bloco. O mesmo clipe curto " +
                 "repetido em toda linha entrega que é um stinger de presença, não fala.")]
        [SerializeField] private bool voiceOnFirstLineOnly = true;

        [Header("Tempos")]
        [Tooltip("Quanto tempo uma fala fica na tela quando quem pediu não disse outro valor.")]
        [SerializeField] private float defaultHold = 3.5f;

        [SerializeField] private float fadeDuration = 0.4f;

        [Tooltip("Silêncio entre duas falas seguidas da fila.")]
        [SerializeField] private float gapBetweenLines = 0.35f;

        private readonly Queue<(string Line, float Hold)> _pending = new Queue<(string, float)>();
        private Coroutine _draining;

        public static WaranaVoice Instance { get; private set; }

        /// <summary>Há fala na tela ou esperando a vez.</summary>
        public bool IsSpeaking => _draining != null;

        private void Awake()
        {
            Instance = this;
            if (group != null) group.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Enfileira uma fala. <paramref name="hold"/> igual a zero usa o tempo padrão.
        /// </summary>
        public void Say(string line, float hold = 0f)
        {
            if (string.IsNullOrEmpty(line) || group == null || text == null) return;

            _pending.Enqueue((line, hold > 0f ? hold : defaultHold));

            if (_draining == null) _draining = StartCoroutine(Drain());
        }

        /// <summary>Enfileira várias falas na ordem, cada uma com o tempo padrão.</summary>
        public void Say(IEnumerable<string> lines)
        {
            if (lines == null) return;

            foreach (string line in lines) Say(line);
        }

        /// <summary>
        /// Espera a fila esvaziar. Para quem precisa falar *antes* de seguir — o fim da
        /// fase não pode cortar para a tela preta com uma frase ainda no ar.
        /// </summary>
        public IEnumerator WaitUntilSilent()
        {
            while (_draining != null) yield return null;
        }

        /// <summary>Esvazia a fila e apaga o que estiver na tela, sem fade.</summary>
        public void Clear()
        {
            _pending.Clear();

            if (_draining != null)
            {
                StopCoroutine(_draining);
                _draining = null;
            }

            if (group != null) group.alpha = 0f;
        }

        private IEnumerator Drain()
        {
            bool first = true;

            while (_pending.Count > 0)
            {
                (string line, float hold) = _pending.Dequeue();

                text.text = line;

                bool playVoice = voiceSource != null && voiceClip != null && (first || !voiceOnFirstLineOnly);
                if (playVoice)
                {
                    voiceSource.clip = voiceClip;
                    voiceSource.Play();
                }

                yield return Fade(1f);

                // A fala nunca sai antes da voz que a acompanha terminar.
                yield return new WaitForSeconds(Mathf.Max(hold, playVoice ? voiceClip.length : 0f));

                yield return Fade(0f);

                if (_pending.Count > 0) yield return new WaitForSeconds(gapBetweenLines);

                first = false;
            }

            _draining = null;
        }

        private IEnumerator Fade(float target)
        {
            float from = group.alpha;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, target, elapsed / fadeDuration);
                yield return null;
            }

            group.alpha = target;
        }
    }
}
