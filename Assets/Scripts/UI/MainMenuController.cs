using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Warana.Audio;

namespace Warana.UI
{
    /// <summary>
    /// Fiação do menu principal: liga os três botões (Iniciar, Opções, Sair) e o
    /// painel de opções. Tudo em código no Awake, e não por listener persistente
    /// no Inspector — assim o menu pode ser remontado por ferramenta sem depender
    /// de referências serializadas dentro de UnityEvent, que não sobrevivem bem a
    /// reconstrução automatizada da cena.
    /// </summary>
    [AddComponentMenu("Waraná/UI/Menu Principal")]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Navegação")]
        [Tooltip("Cena carregada ao clicar em Iniciar.")]
        [SerializeField] private string gameplaySceneName = "Prologo";

        [Header("Botões")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button controlsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button backButton;

        [Header("Opções")]
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Slider volumeSlider;

        [Header("Controles")]
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private Button controlsBackButton;

        [Tooltip("Preenchido em tempo de execução a partir de GameControls — não escreva " +
                 "a lista à mão aqui, ou ela sai do ar assim que um binding mudar.")]
        [SerializeField] private TMP_Text controlsText;

        private void Awake()
        {
            // targetGraphic não sobrevive bem a fiação por ferramenta (AddComponent
            // não auto-liga o Image, diferente do wizard "UI > Button" do Editor),
            // então garantimos aqui em vez de depender do Inspector.
            WireTargetGraphic(startButton);
            WireTargetGraphic(optionsButton);
            WireTargetGraphic(controlsButton);
            WireTargetGraphic(quitButton);
            WireTargetGraphic(backButton);
            WireTargetGraphic(controlsBackButton);

            if (startButton != null) startButton.onClick.AddListener(StartGame);
            if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
            if (backButton != null) backButton.onClick.AddListener(CloseOptions);
            if (controlsButton != null) controlsButton.onClick.AddListener(OpenControls);
            if (controlsBackButton != null) controlsBackButton.onClick.AddListener(CloseControls);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

            // A lista vem de GameControls, a mesma que a abertura da fase usa: escrita
            // à mão em cada tela, ela já tinha ficado desatualizada uma vez.
            if (controlsText != null) controlsText.text = GameControls.Panel();

            if (volumeSlider != null)
            {
                // O slider reflete o volume salvo, não o que estiver serializado nele —
                // senão a escolha do jogador se perde a cada abertura do menu.
                volumeSlider.SetValueWithoutNotify(MasterVolume.Value);
                volumeSlider.onValueChanged.AddListener(SetMasterVolume);
            }

            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);
        }

        private static void WireTargetGraphic(Button button)
        {
            if (button == null || button.targetGraphic != null) return;
            button.targetGraphic = button.GetComponent<Image>();
        }

        public void StartGame() => SceneManager.LoadScene(gameplaySceneName);

        // Os dois painéis ocupam o mesmo lugar da tela, então abrir um fecha o outro —
        // senão eles se sobrepõem e o de baixo continua clicável.

        public void OpenOptions()
        {
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(true);
        }

        public void CloseOptions()
        {
            MasterVolume.Flush();
            if (optionsPanel != null) optionsPanel.SetActive(false);
        }

        public void OpenControls()
        {
            // Fechar opções por aqui em vez de por CloseOptions pularia a gravação do
            // volume, e o jogador perderia o ajuste que acabou de fazer.
            if (optionsPanel != null && optionsPanel.activeSelf) CloseOptions();
            if (controlsPanel != null) controlsPanel.SetActive(true);
        }

        public void CloseControls()
        {
            if (controlsPanel != null) controlsPanel.SetActive(false);
        }

        public void SetMasterVolume(float value) => MasterVolume.Value = value;

        public void QuitGame()
        {
            MasterVolume.Flush();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
