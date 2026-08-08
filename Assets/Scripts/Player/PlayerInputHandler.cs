using UnityEngine;
using UnityEngine.InputSystem;

namespace Warana.Player
{
    /// <summary>
    /// Camada de abstração entre o Input System e a lógica de jogo.
    /// Nenhum outro script precisa conhecer InputAction / bindings.
    /// </summary>
    [DefaultExecutionOrder(-100)] // roda antes do PlayerController2D
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Input Asset")]
        [Tooltip("Deixe vazio para usar o Project-wide Input Actions (Assets/Settings/InputSystem_Actions).")]
        [SerializeField] private InputActionAsset overrideAsset;

        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string attackActionName = "Attack";
        [SerializeField] private string channelActionName = "Channel";

        [Header("Tuning")]
        [Tooltip("Valores de |X| abaixo disso contam como zero (evita drift de analógico).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float deadzone = 0.15f;

        private InputActionMap _map;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _attackAction;
        private InputAction _channelAction;

        private bool _channelHeldPrev;

        // Restrições usadas por cutscenes específicas de mapa (ex.: o Prólogo).
        // Off por padrão, então nenhum outro mapa é afetado.
        private bool _forwardOnly;
        private int _forwardSign = 1;
        private bool _channelLocked;

        /// <summary>Eixo horizontal já com deadzone aplicada, em [-1, 1].</summary>
        public float MoveX { get; private set; }

        public bool JumpHeld { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public bool JumpReleasedThisFrame { get; private set; }

        public bool AttackHeld { get; private set; }
        public bool AttackPressedThisFrame { get; private set; }

        /// <summary>Canalização (RMB / X / RT do controle). É um estado mantido, não um toque.</summary>
        public bool ChannelHeld { get; private set; }
        public bool ChannelPressedThisFrame { get; private set; }
        public bool ChannelReleasedThisFrame { get; private set; }

        private void Awake()
        {
            InputActionAsset asset = overrideAsset != null ? overrideAsset : InputSystem.actions;

            if (asset == null)
            {
                Debug.LogError(
                    "[PlayerInputHandler] Nenhum InputActionAsset encontrado. " +
                    "Defina o asset em Project Settings > Input System Package > Project-wide Actions, " +
                    "ou preencha o campo Override Asset.", this);
                enabled = false;
                return;
            }

            _map = asset.FindActionMap(actionMapName, throwIfNotFound: false);
            if (_map == null)
            {
                Debug.LogError($"[PlayerInputHandler] Action map '{actionMapName}' não existe em '{asset.name}'.", this);
                enabled = false;
                return;
            }

            _moveAction = _map.FindAction(moveActionName, throwIfNotFound: false);
            _jumpAction = _map.FindAction(jumpActionName, throwIfNotFound: false);
            _attackAction = _map.FindAction(attackActionName, throwIfNotFound: false);
            _channelAction = _map.FindAction(channelActionName, throwIfNotFound: false);

            if (_moveAction == null || _jumpAction == null)
            {
                Debug.LogError($"[PlayerInputHandler] Actions '{moveActionName}' / '{jumpActionName}' não encontradas.", this);
                enabled = false;
                return;
            }

            // Ataque é opcional: sem a action o personagem ainda anda e pula.
            if (_attackAction == null)
                Debug.LogWarning($"[PlayerInputHandler] Action '{attackActionName}' não encontrada; o ataque fica desativado.", this);

            if (_channelAction == null)
                Debug.LogWarning($"[PlayerInputHandler] Action '{channelActionName}' não encontrada; a canalização fica desativada.", this);
        }

        private void OnEnable() => _map?.Enable();

        private void OnDisable()
        {
            _map?.Disable();
            ResetState();
        }

        /// <summary>
        /// Restringe o eixo horizontal a uma única direção (cutscene do Prólogo).
        /// <paramref name="sign"/> positivo trava o avanço para a direita, negativo para a esquerda.
        /// </summary>
        public void SetForwardOnly(bool enabled, int sign = 1)
        {
            _forwardOnly = enabled;
            _forwardSign = sign >= 0 ? 1 : -1;
        }

        /// <summary>Bloqueia a entrada em Modo de Canalização sem desligar o resto do input.</summary>
        public void SetChannelLocked(bool locked) => _channelLocked = locked;

        private void Update()
        {
            float rawX = _moveAction.ReadValue<Vector2>().x;
            rawX = Mathf.Abs(rawX) < deadzone ? 0f : rawX;

            // Só permite o eixo quando aponta na direção travada (ou fica parado).
            MoveX = _forwardOnly && Mathf.Sign(rawX) != _forwardSign ? 0f : rawX;

            JumpHeld = _jumpAction.IsPressed();
            JumpPressedThisFrame = _jumpAction.WasPressedThisFrame();
            JumpReleasedThisFrame = _jumpAction.WasReleasedThisFrame();

            if (_attackAction != null)
            {
                AttackHeld = _attackAction.IsPressed();
                AttackPressedThisFrame = _attackAction.WasPressedThisFrame();
            }

            bool actionHeld = _channelAction != null && _channelAction.IsPressed();

            ChannelHeld = !_channelLocked && actionHeld;
            ChannelPressedThisFrame = ChannelHeld && !_channelHeldPrev;
            ChannelReleasedThisFrame = !ChannelHeld && _channelHeldPrev;
            _channelHeldPrev = ChannelHeld;
        }

        private void ResetState()
        {
            MoveX = 0f;
            JumpHeld = false;
            JumpPressedThisFrame = false;
            JumpReleasedThisFrame = false;
            AttackHeld = false;
            AttackPressedThisFrame = false;
            ChannelHeld = false;
            ChannelPressedThisFrame = false;
            ChannelReleasedThisFrame = false;
            _channelHeldPrev = false;
        }
    }
}
