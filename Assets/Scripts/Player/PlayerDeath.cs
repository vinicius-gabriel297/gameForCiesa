using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Warana.Combat;

namespace Warana.Player
{
    /// <summary>
    /// Ao zerar a vida do Piatã, deita o corpo no chão e reinicia a fase. O
    /// <see cref="Health"/> em cena não desliga o objeto ao morrer (disableOnDeath =
    /// false), justamente para dar tempo da animação e do áudio de morte tocarem antes
    /// do reload.
    ///
    /// Morrer é o único estado do jogo em que o personagem não responde a nada, então
    /// ele não pode continuar sujeito ao que o mantinha vivo: as habilidades saem de
    /// cena, o empurrão do golpe fatal é descartado, e o corpo cai reto até encostar no
    /// chão. Sem esse encerramento explícito, o cadáver herdava a velocidade do golpe e
    /// ficava boiando na altura em que morreu enquanto o clip de morte tocava.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("Warana/Player/Morte do Piatã")]
    public class PlayerDeath : MonoBehaviour
    {
        [Tooltip("Espera entre o corpo assentar no chão e o recarregamento da fase.")]
        [SerializeField] private float restartDelay = 1.2f;

        [Tooltip("Teto para a queda do cadáver. Quem morre caindo fora do mapa nunca " +
                 "encosta em chão nenhum, e sem o teto a fase não reiniciaria.")]
        [SerializeField] private float maxFallTime = 1.5f;

        private Health _health;
        private PlayerController2D _controller;
        private PlayerAnimator _animator;
        private readonly List<PlayerAbility> _abilities = new List<PlayerAbility>();

        private void Awake()
        {
            _health = GetComponent<Health>();
            _controller = GetComponent<PlayerController2D>();
            _animator = GetComponent<PlayerAnimator>();

            GetComponents(_abilities);
        }

        private void OnEnable() => _health.Died += OnDied;

        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied()
        {
            // As habilidades saem antes do congelamento, e não depois: o OnDisable de
            // cada uma devolve o controle (FreezeControl(false)) ao ser interrompida no
            // meio de um golpe, e essa devolução não pode chegar depois do congelamento
            // da morte — senão o cadáver volta a andar.
            for (int i = 0; i < _abilities.Count; i++)
                _abilities[i].enabled = false;

            if (_controller != null) _controller.FreezeControl(true);
            if (_animator != null) _animator.SetDead(true);

            StartCoroutine(SettleThenRestart());
        }

        private IEnumerator SettleThenRestart()
        {
            yield return Settle();

            yield return new WaitForSeconds(restartDelay);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Deixa o corpo cair até o chão e o prende lá. O empurrão lateral do golpe
        /// fatal é descartado na hora — um cadáver que continua deslizando de lado lê
        /// como personagem ainda vivo — mas a queda é preservada, porque ela é o que
        /// leva o corpo do ar até o chão.
        /// </summary>
        private IEnumerator Settle()
        {
            if (_controller == null) yield break;

            Rigidbody2D body = _controller.Body;
            body.linearVelocity = new Vector2(0f, Mathf.Min(0f, body.linearVelocity.y));

            float waited = 0f;
            while (!_controller.IsGrounded && waited < maxFallTime)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            // Desligar o controller junto com o corpo: a gravidade dele é manual, e
            // continuaria empurrando o cadáver contra o chão a cada passo de física.
            body.linearVelocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;
            _controller.enabled = false;
        }
    }
}
