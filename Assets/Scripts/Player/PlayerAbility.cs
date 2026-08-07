using UnityEngine;

namespace Warana.Player
{
    /// <summary>
    /// Base para habilidades plugáveis (dash, wall jump, ataque com investida...).
    /// Basta adicionar o componente ao Player: o <see cref="PlayerController2D"/>
    /// coleta todas as habilidades no Awake e as consulta a cada FixedUpdate,
    /// sem precisar ser modificado.
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    public abstract class PlayerAbility : MonoBehaviour
    {
        protected PlayerController2D Controller { get; private set; }
        protected Rigidbody2D Body { get; private set; }
        protected PlayerInputHandler Input { get; private set; }

        /// <summary>True enquanto a habilidade está no controle do personagem.</summary>
        public bool IsActive { get; protected set; }

        /// <summary>
        /// Se true durante <see cref="IsActive"/>, o controller não aplica
        /// movimento horizontal nem gravidade neste frame — a habilidade manda.
        /// </summary>
        public virtual bool OverridesMovement => true;

        /// <summary>Ordem de consulta: menor valor é avaliado primeiro.</summary>
        public virtual int Priority => 0;

        protected virtual void Awake()
        {
            Controller = GetComponent<PlayerController2D>();
            Body = GetComponent<Rigidbody2D>();
            Input = GetComponent<PlayerInputHandler>();
        }

        /// <summary>Condições de ativação. Retorne true para assumir o controle.</summary>
        public virtual bool TryActivate() => false;

        /// <summary>Roda a cada FixedUpdate enquanto ativa. Chame End() para devolver o controle.</summary>
        public virtual void Tick(float deltaTime) { }

        /// <summary>Chamado pelo controller no frame do pouso (reset de cargas).</summary>
        public virtual void OnLanded() { }

        protected void End() => IsActive = false;
    }
}
