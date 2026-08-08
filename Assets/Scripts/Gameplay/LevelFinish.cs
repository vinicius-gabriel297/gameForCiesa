using UnityEngine;
using Warana.UI;

namespace Warana.Gameplay
{
    /// <summary>
    /// Gatilho de fim de fase. Ao tocar o Piatã, escurece a tela e volta ao menu
    /// principal — o mesmo painel de fade usado por qualquer outra transição.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Warana/Gameplay/Fim de Fase")]
    public class LevelFinish : MonoBehaviour
    {
        [Tooltip("Cena carregada depois do fade.")]
        [SerializeField] private string menuSceneName = "MainMenu";

        [SerializeField] private float fadeDuration = 1f;

        private bool _triggered;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered || !other.CompareTag("Player")) return;

            _triggered = true;
            ScreenFader.Get().FadeToBlackAndLoad(menuSceneName, fadeDuration);
        }
    }
}
