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
        [SerializeField] private string gameplaySceneName = "TilemapTest";

        [Header("Botões")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button backButton;

        [Header("Opções")]
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Slider volumeSlider;

        private void Awake()
        {
            // targetGraphic não sobrevive bem a fiação por ferramenta (AddComponent
            // não auto-liga o Image, diferente do wizard "UI > Button" do Editor),
            // então garantimos aqui em vez de depender do Inspector.
            WireTargetGraphic(startButton);
            WireTargetGraphic(optionsButton);
            WireTargetGraphic(quitButton);
            WireTargetGraphic(backButton);

            if (startButton != null) startButton.onClick.AddListener(StartGame);
            if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
            if (backButton != null) backButton.onClick.AddListener(CloseOptions);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

            if (volumeSlider != null)
            {
                // O slider reflete o volume salvo, não o que estiver serializado nele —
                // senão a escolha do jogador se perde a cada abertura do menu.
                volumeSlider.SetValueWithoutNotify(MasterVolume.Value);
                volumeSlider.onValueChanged.AddListener(SetMasterVolume);
            }

            if (optionsPanel != null) optionsPanel.SetActive(false);
        }

        private static void WireTargetGraphic(Button button)
        {
            if (button == null || button.targetGraphic != null) return;
            button.targetGraphic = button.GetComponent<Image>();
        }

        public void StartGame() => SceneManager.LoadScene(gameplaySceneName);

        public void OpenOptions()
        {
            if (optionsPanel != null) optionsPanel.SetActive(true);
        }

        public void CloseOptions()
        {
            MasterVolume.Flush();
            if (optionsPanel != null) optionsPanel.SetActive(false);
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
