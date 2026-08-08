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

        [Header("Ações")]
        [SerializeField] private AudioClip jumpClip;
        [SerializeField] private AudioClip damageClip;
        [SerializeField] private AudioClip deathClip;

        [Header("Câmera")]
        [Tooltip("Tremor ao levar dano (duração, força). Força 0 desliga.")]
        [SerializeField] private float damageShakeDuration = 0.15f;
        [SerializeField] private float damageShakeMagnitude = 0.12f;

        [Tooltip("Tremor ao morrer.")]
        [SerializeField] private float deathShakeDuration = 0.25f;
        [SerializeField] private float deathShakeMagnitude = 0.2f;

        private PlayerController2D _controller;
        private Health _health;
        private AudioSource _source;
        private float _stepTimer;

        private void Awake()
        {
            _controller = GetComponent<PlayerController2D>();
            _health = GetComponent<Health>();
            _source = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            _controller.Jumped += PlayJump;

            if (_health == null) return;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _controller.Jumped -= PlayJump;

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
            PlayRandom(footstepClips, footstepPitchVariation);
        }

        private void PlayJump() => PlayOneShot(jumpClip);

        private void OnDamaged(float amount, Vector2 direction)
        {
            PlayOneShot(damageClip);
            CameraFollow2D.Instance?.Shake(damageShakeDuration, damageShakeMagnitude);
        }

        private void OnDied()
        {
            PlayOneShot(deathClip);
            CameraFollow2D.Instance?.Shake(deathShakeDuration, deathShakeMagnitude);
        }

        private void PlayRandom(AudioClip[] clips, float pitchVariation)
        {
            if (clips == null || clips.Length == 0) return;

            _source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            _source.PlayOneShot(clips[Random.Range(0, clips.Length)]);
            _source.pitch = 1f;
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null) return;
            _source.pitch = 1f;
            _source.PlayOneShot(clip);
        }
    }
}
