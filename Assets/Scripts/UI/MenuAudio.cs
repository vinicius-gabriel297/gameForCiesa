using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Warana.UI
{
    /// <summary>
    /// Som dos menus: um toque ao mover a seleção e outro ao confirmar.
    ///
    /// Sem isso o menu é mudo, e no controle isso pesa mais do que parece — sem o
    /// clique, mover a seleção não dá nenhuma confirmação de que o aparelho está
    /// respondendo, e o jogador fica testando o direcional para descobrir.
    ///
    /// <para>Liga-se sozinho a todos os <see cref="Selectable"/> abaixo deste objeto,
    /// inclusive os inativos, porque os painéis de opções e de controles nascem
    /// desligados e só aparecem depois. Fiação por lista no Inspector quebraria a cada
    /// botão novo — o mesmo motivo já anotado no <see cref="MainMenuController"/>.</para>
    /// </summary>
    [AddComponentMenu("Waraná/UI/Som do Menu")]
    public class MenuAudio : MonoBehaviour
    {
        [Header("Sons")]
        [Tooltip("Toca ao mover a seleção (teclado, controle) ou passar o mouse.")]
        [SerializeField] private AudioClip navigateClip;

        [Tooltip("Toca ao acionar um botão.")]
        [SerializeField] private AudioClip confirmClip;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.6f;

        private AudioSource _source;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.volume = volume;
            // O menu de pausa liga AudioListener.pause para calar a fase; sem esta
            // linha o próprio menu ficaria mudo justamente enquanto está aberto.
            _source.ignoreListenerPause = true;
            // 2D: um som de menu não tem lugar no mundo, e sem isso ele seria atenuado
            // pela distância entre este objeto e o AudioListener.
            _source.spatialBlend = 0f;

            foreach (Selectable selectable in GetComponentsInChildren<Selectable>(true))
            {
                selectable.gameObject.AddComponent<MenuSelectableAudio>().Bind(this);

                if (selectable is Button button) button.onClick.AddListener(PlayConfirm);
            }
        }

        public void PlayNavigate() => Play(navigateClip);

        public void PlayConfirm() => Play(confirmClip);

        private void Play(AudioClip clip)
        {
            if (clip == null || _source == null) return;

            // PlayOneShot em vez de Play: dois cliques rápidos se sobrepõem em vez de
            // o segundo cortar o primeiro pela metade.
            _source.PlayOneShot(clip, volume);
        }
    }

    /// <summary>
    /// Ponte entre um <see cref="Selectable"/> e o <see cref="MenuAudio"/> do menu.
    /// Existe porque os dois caminhos de "estou apontando para este botão" são eventos
    /// diferentes: <see cref="ISelectHandler"/> é o do controle e do teclado,
    /// <see cref="IPointerEnterHandler"/> é o do mouse. Cobrir só um deles deixaria
    /// metade dos jogadores sem retorno sonoro.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuSelectableAudio : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        private MenuAudio _audio;

        public void Bind(MenuAudio audio) => _audio = audio;

        public void OnSelect(BaseEventData eventData) => _audio?.PlayNavigate();

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Um botão desligado não responde ao clique; soar como se respondesse
            // seria mentira.
            var selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return;

            _audio?.PlayNavigate();
        }
    }
}
