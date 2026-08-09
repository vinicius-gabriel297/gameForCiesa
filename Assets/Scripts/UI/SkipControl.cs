using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Warana.UI
{
    /// <summary>
    /// O botão de pular das cenas dirigidas, com o aviso na tela que o anuncia.
    ///
    /// Um jogo de jam é jogado em sessões curtas e repetidas — quem morre no meio da
    /// fase não deveria ter que assistir a abertura inteira de novo para tentar outra
    /// vez. Sem uma saída, a cutscene deixa de ser abertura e vira pedágio.
    ///
    /// <para>A ação é criada aqui, e não no <c>InputSystem_Actions</c>, pelo mesmo
    /// motivo do menu de pausa: ela precisa funcionar com o mapa "Player" desligado,
    /// que é o estado normal durante uma cena dirigida.</para>
    ///
    /// <para>Enquanto existir um <see cref="SkipControl"/> vivo, <see cref="IsSequenceRunning"/>
    /// fica verdadeiro. É isso que impede que o mesmo Esc abra o menu de pausa e pule a
    /// cena no mesmo frame no Mapa 01, onde os dois convivem.</para>
    /// </summary>
    public sealed class SkipControl : IDisposable
    {
        private static int _activeCount;

        /// <summary>Verdadeiro enquanto alguma cena dirigida aceita ser pulada.</summary>
        public static bool IsSequenceRunning => _activeCount > 0;

        private readonly InputAction _action;
        private GameObject _promptRoot;
        private bool _disposed;

        /// <param name="font">Fonte do aviso. Sem ela o TMP usa a padrão do projeto.</param>
        /// <param name="showPrompt">
        /// Falso monta só o input, para quem já desenha o próprio aviso.
        /// </param>
        public SkipControl(TMP_FontAsset font = null, bool showPrompt = true)
        {
            _action = new InputAction("Skip", InputActionType.Button);
            _action.AddBinding("<Keyboard>/escape");
            _action.AddBinding("<Gamepad>/start");
            _action.Enable();

            _activeCount++;

            if (showPrompt) BuildPrompt(font);
        }

        /// <summary>Verdadeiro no frame em que o jogador pediu para pular.</summary>
        public bool Requested => !_disposed && _action != null && _action.WasPressedThisFrame();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _activeCount--;

            _action?.Disable();
            _action?.Dispose();

            if (_promptRoot != null) UnityEngine.Object.Destroy(_promptRoot);
        }

        /// <summary>
        /// Canto inferior direito, discreto: quem quer ver a cena não deve ser puxado
        /// para fora dela por um aviso brilhante no meio da tela.
        /// </summary>
        private void BuildPrompt(TMP_FontAsset font)
        {
            _promptRoot = new GameObject("Skip Prompt", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));

            var canvas = _promptRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Acima da abertura da fase (200) para não ficar atrás do texto dela,
            // e abaixo do menu de pausa (900) e do ScreenFader (1000).
            canvas.sortingOrder = 300;

            var scaler = _promptRoot.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(_promptRoot.transform, false);

            var rect = (RectTransform)labelGO.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-48f, 40f);
            rect.sizeDelta = new Vector2(420f, 48f);

            var tmp = labelGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = GameControls.SkipHint;
            tmp.fontSize = 28f;
            tmp.alignment = TextAlignmentOptions.Right;
            tmp.color = new Color(1f, 1f, 1f, SkipPromptPulse.MaxAlpha);
            tmp.raycastTarget = false;

            labelGO.AddComponent<SkipPromptPulse>();
        }
    }

    /// <summary>
    /// Faz o aviso de pular respirar devagar.
    ///
    /// Parado no canto, ele se confunde com a interface e some da atenção de quem está
    /// olhando para a cena; um brilho que vai e volta é percebido pela visão periférica
    /// sem exigir que o jogador desvie o olhar do que importa.
    ///
    /// <para>Lento e sem apagar de todo: o ciclo é longo e a opacidade nunca chega a
    /// zero, para o aviso pulsar em vez de piscar — piscada rápida puxa o olho para
    /// fora da cutscene, que é justamente o que ela não deveria fazer.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class SkipPromptPulse : MonoBehaviour
    {
        public const float MinAlpha = 0.28f;
        public const float MaxAlpha = 0.75f;

        /// <summary>Segundos de um ciclo completo (claro → apagado → claro).</summary>
        private const float Period = 2.2f;

        private TMP_Text _text;

        private void Awake() => _text = GetComponent<TMP_Text>();

        private void Update()
        {
            if (_text == null) return;

            // unscaledTime porque o menu de pausa zera o timeScale, e um aviso congelado
            // no meio do fade pareceria a tela ter travado.
            float wave = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Period) + 1f) * 0.5f;

            Color color = _text.color;
            color.a = Mathf.Lerp(MinAlpha, MaxAlpha, wave);
            _text.color = color;
        }
    }
}
