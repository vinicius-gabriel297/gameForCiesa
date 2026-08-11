using UnityEngine;
using Warana.Player;

namespace Warana.Environment
{
    /// <summary>
    /// A árvore sagrada de uma região: sabe quando Piatã chegou e quanto do ritual de
    /// cura ele já entregou.
    ///
    /// <para>Mora separada do <c>EndingSequence</c> porque as duas coisas têm tempos de
    /// vida diferentes: a árvore é um *lugar* na fase, e cada região da floresta vai ter
    /// a sua; a sequência é o roteiro de um final específico. Assim a próxima região
    /// reaproveita este componente sem arrastar junto o epílogo do Mapa 01.</para>
    ///
    /// <para>O gesto é o mesmo do prólogo — segurar Canalizar. A fase inteira ensinou
    /// que canalizar é atacar; aqui o mesmo botão cura, e é por isso que a cura não usa
    /// input novo nenhum.</para>
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Waraná/Ambiente/Árvore Sagrada")]
    public class SacredTree : MonoBehaviour
    {
        [Tooltip("Quanto tempo de canalização a árvore pede para ser restaurada por inteiro.")]
        [SerializeField] private float channelDuration = 4f;

        [Tooltip("Vazio = procura a canalização no objeto com a tag Player.")]
        [SerializeField] private PlayerChannel playerChannel;

        private float _held;
        private bool _ritualOpen;

        /// <summary>Piatã já entrou na área da árvore.</summary>
        public bool PlayerArrived { get; private set; }

        /// <summary>Quanto do ritual foi entregue, de <c>0</c> a <c>1</c>.</summary>
        public float HoldProgress => channelDuration <= 0f ? 1f : Mathf.Clamp01(_held / channelDuration);

        /// <summary>Está canalizando neste instante — para a fala e o VFX acompanharem.</summary>
        public bool IsChanneling => _ritualOpen && playerChannel != null && playerChannel.IsChanneling;

        /// <summary>
        /// Abre o ritual. Antes disso a árvore ignora a canalização: durante a fase o
        /// jogador canaliza o tempo todo, e sem esta porta ele curaria a região por
        /// acidente ao atirar num inimigo perto do tronco.
        /// </summary>
        public void OpenRitual() => _ritualOpen = true;

        private void Start()
        {
            if (playerChannel != null) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerChannel = player.GetComponentInChildren<PlayerChannel>();
        }

        private void Update()
        {
            if (!IsChanneling) return;

            // Só acumula, nunca volta: soltar o botão custa tempo, mas não desfaz cura
            // já entregue. A luz da árvore é dirigida por este valor, e um progresso que
            // recua apagaria a restauração na cara do jogador.
            _held = Mathf.Min(_held + Time.deltaTime, channelDuration);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player")) PlayerArrived = true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var box = GetComponent<Collider2D>();
            if (box == null) return;

            Gizmos.color = new Color(0.6f, 1f, 0.8f, 0.35f);
            Gizmos.DrawCube(box.bounds.center, box.bounds.size);
        }
#endif
    }
}
