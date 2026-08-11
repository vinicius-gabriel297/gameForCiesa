using UnityEngine;
using Warana.CameraRig;
using Warana.Combat;

namespace Warana.Player
{
    /// <summary>
    /// Ponte entre o estado do Piatã e o áudio: passos, pulo, dano e morte. Segue o
    /// mesmo molde do <see cref="PlayerAnimator"/> — só escuta eventos e toca, nenhuma
    /// decisão de gameplay mora aqui.
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Waraná/Player/Áudio do Piatã")]
    public class PlayerAudio : MonoBehaviour
    {
        [Header("Passos")]
        [SerializeField] private AudioClip[] footstepClips;

        [Tooltip("Intervalo entre passos correndo no chão, em segundos.")]
        [SerializeField] private float stepInterval = 0.24f;

        [Tooltip("Variação de pitch por passo, para não soar repetitivo.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float footstepPitchVariation = 0.08f;

        [Tooltip("Passo é o som mais repetido do jogo, então toca bem abaixo do resto.")]
        [Range(0f, 1f)]
        [SerializeField] private float footstepVolume = 0.35f;

        [Header("Ações")]
        [SerializeField] private AudioClip jumpClip;

        [Tooltip("Sopro da lança cortando o ar, no início da estocada.")]
        [SerializeField] private AudioClip attackClip;

        [Tooltip("Esforço do Piatã na estocada. Camada por cima do sopro, não substituto.")]
        [SerializeField] private AudioClip attackVoiceClip;

        // Grunhido em toda estocada cansa rápido — o jogador ataca dezenas de vezes por
        // sala. Tocar só em parte dos golpes, com pitch variado, mantém a voz como
        // acento do esforço em vez de tique da animação.
        [Tooltip("Chance de o esforço sair em cada estocada. 1 = sempre.")]
        [Range(0f, 1f)]
        [SerializeField] private float attackVoiceChance = 0.6f;

        [Tooltip("Variação de pitch da voz, para dois grunhidos seguidos não soarem iguais.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float attackVoicePitchVariation = 0.07f;

        [Tooltip("Silêncio mínimo entre dois esforços, em segundos, mesmo com a chance alta.")]
        [SerializeField] private float attackVoiceMinInterval = 0.9f;

        [Tooltip("Impacto da ponta no inimigo. Toca por cima do sopro, não no lugar dele.")]
        [SerializeField] private AudioClip attackHitClip;

        [SerializeField] private AudioClip damageClip;
        [SerializeField] private AudioClip deathClip;

        [Header("Volume por som")]
        [Tooltip("Ganho de cada ação sobre o volume do AudioSource, que fica em 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float jumpVolume = 0.7f;

        [Range(0f, 1f)]
        [SerializeField] private float attackVolume = 0.6f;

        [Tooltip("A voz fica abaixo do sopro: ela acompanha o golpe, quem marca o golpe é a lança.")]
        [Range(0f, 1f)]
        [SerializeField] private float attackVoiceVolume = 0.45f;

        [Range(0f, 1f)]
        [SerializeField] private float attackHitVolume = 0.8f;

        [Range(0f, 1f)]
        [SerializeField] private float damageVolume = 0.9f;

        [Range(0f, 1f)]
        [SerializeField] private float deathVolume = 1f;

        [Header("Câmera")]
        [Tooltip("Tremor ao levar dano (duração, força). Força 0 desliga.")]
        [SerializeField] private float damageShakeDuration = 0.15f;
        [SerializeField] private float damageShakeMagnitude = 0.12f;

        [Tooltip("Tremor ao morrer.")]
        [SerializeField] private float deathShakeDuration = 0.25f;
        [SerializeField] private float deathShakeMagnitude = 0.2f;

        [Tooltip("Tremor ao acertar a lança. Bem curto: é pontuação do acerto, não susto.")]
        [SerializeField] private float attackHitShakeDuration = 0.07f;
        [SerializeField] private float attackHitShakeMagnitude = 0.05f;

        private PlayerController2D _controller;
        private PlayerAttack _attack;
        private Health _health;
        private AudioSource _source;
        private AudioSource _voiceSource;
        private float _stepTimer;
        private float _lastAttackVoiceTime = -999f;

        private void Awake()
        {
            _controller = GetComponent<PlayerController2D>();
            _attack = GetComponent<PlayerAttack>();
            _health = GetComponent<Health>();
            _source = GetComponent<AudioSource>();
            _voiceSource = CreateVoiceSource();
        }

        /// <summary>
        /// Fonte separada só para a voz. O pitch do <see cref="AudioSource"/> vale para
        /// todos os one-shots ainda tocando nele, então variar o pitch na fonte comum
        /// desafinaria o sopro da lança que está no ar — e devolver o pitch a 1 logo em
        /// seguida cancelaria a variação. Com fonte própria, a voz varia sozinha e
        /// continua misturando com o sopro, que é o efeito pedido.
        /// </summary>
        private AudioSource CreateVoiceSource()
        {
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = _source.spatialBlend;
            voice.outputAudioMixerGroup = _source.outputAudioMixerGroup;
            voice.rolloffMode = _source.rolloffMode;
            voice.minDistance = _source.minDistance;
            voice.maxDistance = _source.maxDistance;
            return voice;
        }

        private void OnEnable()
        {
            _controller.Jumped += PlayJump;

            if (_attack != null)
            {
                _attack.Started += PlayAttack;
                _attack.Hit += OnAttackHit;
            }

            if (_health == null) return;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _controller.Jumped -= PlayJump;

            if (_attack != null)
            {
                _attack.Started -= PlayAttack;
                _attack.Hit -= OnAttackHit;
            }

            if (_health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void Update()
        {
            TickFootsteps();
        }

        private void TickFootsteps()
        {
            bool moving = _controller.IsGrounded && Mathf.Abs(_controller.Body.linearVelocity.x) > 0.1f;
            if (!moving)
            {
                _stepTimer = 0f;
                return;
            }

            _stepTimer -= Time.deltaTime;
            if (_stepTimer > 0f) return;

            _stepTimer = stepInterval;
            PlayRandom(footstepClips, footstepPitchVariation, footstepVolume);
        }

        private void PlayJump() => PlayOneShot(jumpClip, jumpVolume);

        private void PlayAttack()
        {
            PlayOneShot(attackClip, attackVolume);
            PlayAttackVoice();
        }

        /// <summary>Esforço do Piatã, sobreposto ao sopro da lança e não no lugar dele.</summary>
        private void PlayAttackVoice()
        {
            if (attackVoiceClip == null) return;
            if (Time.time - _lastAttackVoiceTime < attackVoiceMinInterval) return;
            if (Random.value > attackVoiceChance) return;

            _lastAttackVoiceTime = Time.time;

            _voiceSource.pitch = 1f + Random.Range(-attackVoicePitchVariation, attackVoicePitchVariation);
            _voiceSource.PlayOneShot(attackVoiceClip, attackVoiceVolume);
        }

        private void OnAttackHit(Vector2 point)
        {
            PlayOneShot(attackHitClip, attackHitVolume);
            CameraFollow2D.Instance?.Shake(attackHitShakeDuration, attackHitShakeMagnitude);
        }

        private void OnDamaged(float amount, Vector2 direction)
        {
            PlayOneShot(damageClip, damageVolume);
            CameraFollow2D.Instance?.Shake(damageShakeDuration, damageShakeMagnitude);
        }

        private void OnDied()
        {
            PlayOneShot(deathClip, deathVolume);
            CameraFollow2D.Instance?.Shake(deathShakeDuration, deathShakeMagnitude);
        }

        private void PlayRandom(AudioClip[] clips, float pitchVariation, float volume)
        {
            if (clips == null || clips.Length == 0) return;

            _source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            _source.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
            _source.pitch = 1f;
        }

        // O volume vive aqui, por som, e não no AudioSource: a mesma fonte toca passo,
        // pulo, dano e morte, então abaixar a fonte para o passo não estourar deixava
        // dano e morte quase inaudíveis — o que aparecia como "som baixo" no build.
        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip == null) return;
            _source.pitch = 1f;
            _source.PlayOneShot(clip, volume);
        }
    }
}
