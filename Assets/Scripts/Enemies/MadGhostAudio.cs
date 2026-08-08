using UnityEngine;
using Warana.CameraRig;
using Warana.Combat;

namespace Warana.Enemies
{
    /// <summary>
    /// Ponte entre o estado do Mad Ghost e o áudio: golpe e morte. Mesmo molde do
    /// <see cref="MadGhostAnimator"/> — só escuta eventos e toca.
    /// </summary>
    [RequireComponent(typeof(MadGhost))]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Waraná/Inimigos/Áudio do Mad Ghost")]
    public class MadGhostAudio : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] private AudioClip attackClip;
        [SerializeField] private AudioClip deathClip;

        [Header("Câmera")]
        [Tooltip("Tremor ao acertar o golpe nele. Força 0 desliga.")]
        [SerializeField] private float hitShakeDuration = 0.06f;
        [SerializeField] private float hitShakeMagnitude = 0.04f;

        [Tooltip("Tremor ao morrer.")]
        [SerializeField] private float deathShakeDuration = 0.12f;
        [SerializeField] private float deathShakeMagnitude = 0.08f;

        private MadGhost _ghost;
        private Health _health;
        private AudioSource _source;

        private void Awake()
        {
            _ghost = GetComponent<MadGhost>();
            _health = GetComponent<Health>();
            _source = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            _ghost.AttackStarted += PlayAttack;

            if (_health == null) return;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _ghost.AttackStarted -= PlayAttack;

            if (_health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void PlayAttack() => PlayOneShot(attackClip);

        private void OnDamaged(float amount, Vector2 direction) =>
            CameraFollow2D.Instance?.Shake(hitShakeDuration, hitShakeMagnitude);

        private void OnDied()
        {
            PlayOneShot(deathClip);
            CameraFollow2D.Instance?.Shake(deathShakeDuration, deathShakeMagnitude);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null) return;
            _source.PlayOneShot(clip);
        }
    }
}
