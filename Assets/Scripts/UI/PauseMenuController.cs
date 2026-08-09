using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Warana.UI
{
    /// <summary>
    /// A pausa da fase: congela o mundo e oferece as três saídas de sempre —
    /// continuar, voltar ao menu, fechar o jogo.
    ///
    /// O painel se monta sozinho no Awake, como o <see cref="ScreenFader"/>, em vez de
    /// depender de uma árvore de UI montada à mão na cena. Não é preguiça: a fiação por
    /// campo serializado (botão → listener → painel) é justamente o que não sobrevive a
    /// remontagem automatizada da cena, o mesmo motivo já anotado no
    /// <see cref="MainMenuController"/>. Aqui o problema é pior, porque a pausa precisa
    /// existir por cima de *qualquer* estado da fase, inclusive durante a abertura.
    ///
    /// Só o Mapa 01 carrega este componente. O Prólogo é uma cutscene curta e dirigida:
    /// pausar no meio dela não devolve controle nenhum ao jogador, só dá a ele um jeito
    /// de quebrar o ritmo da cena.
    /// </summary>
    [AddComponentMenu("Warana/UI/Menu de Pausa")]
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Navegação")]
        [Tooltip("Cena carregada por 'Menu Principal'.")]
        [SerializeField] private string menuSceneName = "MainMenu";

        [SerializeField] private float fadeDuration = 1f;

        [Header("Aparência")]
        [Tooltip("Placa de madeira dos botões. Mesma arte do menu principal.")]
        [SerializeField] private Sprite buttonSprite;

        [Tooltip("Moldura do painel. Mesma arte do painel de opções do menu principal.")]
        [SerializeField] private Sprite panelSprite;

        [SerializeField] private TMP_FontAsset font;

        [Tooltip("Escurecimento aplicado sobre a fase enquanto o jogo está pausado.")]
        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.72f);

        [Tooltip("Cor do texto dos botões, para contrastar com a madeira clara.")]
        [SerializeField] private Color labelColor = new Color(0.027f, 0f, 0f, 1f);

        private CanvasGroup _group;
        private Button _resumeButton;
        private InputAction _pauseAction;
        private InputActionMap _playerMap;

        /// <summary>Verdadeiro entre o clique em "Menu Principal" e a troca de cena.</summary>
        private bool _leaving;

        public bool IsPaused { get; private set; }

        private void Awake()
        {
            BuildUI();
            EnsureEventSystem();

            // A ação vive aqui, e não no InputSystem_Actions, porque é a única tecla do
            // jogo que precisa funcionar com o mundo congelado — mantê-la fora do mapa
            // "Player" é o que permite desligar aquele mapa inteiro durante a pausa.
            _pauseAction = new InputAction("Pause", InputActionType.Button);
            _pauseAction.AddBinding("<Keyboard>/escape");
            _pauseAction.AddBinding("<Gamepad>/start");
            _pauseAction.Enable();

            _playerMap = InputSystem.actions != null
                ? InputSystem.actions.FindActionMap("Player", throwIfNotFound: false)
                : null;
        }

        private void OnDestroy()
        {
            _pauseAction?.Disable();
            _pauseAction?.Dispose();

            // Sair da cena pausado deixaria a próxima nascer congelada.
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private void Update()
        {
            if (_leaving) return;

            // Esc é a mesma tecla que pula a abertura da fase. Enquanto ela estiver
            // rodando o pedido é dela, senão um toque só pularia a cutscene e abriria
            // a pausa por cima dela no mesmo frame. Já pausado, destravar continua
            // valendo — do contrário a pausa poderia prender o jogador.
            if (!IsPaused && SkipControl.IsSequenceRunning) return;

            if (_pauseAction != null && _pauseAction.WasPressedThisFrame()) Toggle();
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;

            IsPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            SetPanelVisible(true);

            // Sem desligar o mapa, um clique de ataque segurado durante a pausa dispara
            // no primeiro frame depois dela.
            _playerMap?.Disable();

            // Sem uma seleção inicial o controle não tem por onde começar a navegar.
            if (EventSystem.current != null && _resumeButton != null)
                EventSystem.current.SetSelectedGameObject(_resumeButton.gameObject);
        }

        public void Resume()
        {
            if (!IsPaused) return;

            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            SetPanelVisible(false);
            _playerMap?.Enable();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void GoToMainMenu()
        {
            if (_leaving) return;
            _leaving = true;

            // O fade do ScreenFader anda por Time.deltaTime: sem destravar o tempo antes,
            // a tela ficaria parada no meio da transição.
            Resume();
            SetPanelVisible(false);

            ScreenFader.Get().FadeToBlackAndLoad(menuSceneName, fadeDuration);
        }

        public void QuitGame()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetPanelVisible(bool visible)
        {
            if (_group == null) return;

            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }

        // ---------------------------------------------------------------- construção

        private void BuildUI()
        {
            var canvasGO = new GameObject("Pause Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Acima do HUD (100) e da abertura da fase (200), abaixo do ScreenFader (1000),
            // para que a transição de saída cubra o painel e não o contrário.
            canvas.sortingOrder = 900;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = canvasGO.GetComponent<CanvasGroup>();

            // Mesma marca do menu principal: no controle ela é o que diz onde se está,
            // e o jogador não deveria ter que reaprender isso ao abrir a pausa.
            canvasGO.AddComponent<MenuSelectionMarker>();

            var overlay = CreateImage("Overlay", canvasGO.transform, null, overlayColor);
            Stretch((RectTransform)overlay.transform);

            var panel = CreateImage("Panel", canvasGO.transform, panelSprite, Color.white);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(560f, 600f);

            CreateLabel("Title", panelRect, "PAUSA", 64f,
                new Vector2(0f, 210f), new Vector2(480f, 90f), Color.white);

            _resumeButton = CreateButton("ResumeButton", panelRect, "CONTINUAR", new Vector2(0f, 90f), Resume);
            CreateButton("MenuButton", panelRect, "MENU PRINCIPAL", new Vector2(0f, 0f), GoToMainMenu);
            CreateButton("QuitButton", panelRect, "SAIR DO JOGO", new Vector2(0f, -90f), QuitGame);

            SetPanelVisible(false);
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 position,
            UnityEngine.Events.UnityAction onClick)
        {
            var image = CreateImage(name, parent, buttonSprite, Color.white);

            var rect = (RectTransform)image.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(340f, 72f);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            CreateLabel(name + "Label", rect, label, 40f, Vector2.zero, new Vector2(320f, 60f), labelColor);

            return button;
        }

        private Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;

            return image;
        }

        private TMP_Text CreateLabel(string name, Transform parent, string text, float size,
            Vector2 position, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 18f;
            tmp.fontSizeMax = size;

            return tmp;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// A fase não precisava de EventSystem até existir um botão nela. Criar aqui
        /// evita que a pausa dependa de alguém ter lembrado de adicionar o objeto à cena.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();

            InputActionAsset asset = InputSystem.actions;
            if (asset == null) return;

            module.actionsAsset = asset;

            InputActionMap ui = asset.FindActionMap("UI", throwIfNotFound: false);
            if (ui == null) return;

            module.point = Reference(ui, "Point");
            module.leftClick = Reference(ui, "Click");
            module.move = Reference(ui, "Navigate");
            module.submit = Reference(ui, "Submit");
            module.cancel = Reference(ui, "Cancel");
            module.scrollWheel = Reference(ui, "ScrollWheel");

            ui.Enable();
        }

        private static InputActionReference Reference(InputActionMap map, string actionName)
        {
            InputAction action = map.FindAction(actionName, throwIfNotFound: false);
            return action != null ? InputActionReference.Create(action) : null;
        }
    }
}
