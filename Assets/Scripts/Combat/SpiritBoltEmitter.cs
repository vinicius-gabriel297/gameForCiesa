using System.Collections.Generic;
using UnityEngine;
using Warana.Companion;
using Warana.Player;

namespace Warana.Combat
{
    /// <summary>
    /// A arma do Waraná. Vive no orbe, não em Piatã: enquanto a canalização estiver
    /// ativa, o olho procura o inimigo mais próximo dentro do alcance e na direção
    /// que Piatã encara, e descarrega um raio espiritual a cada intervalo.
    ///
    /// O disparo é hitscan — não há projétil viajando. Para um raio de curto alcance
    /// isso é o certo: o dano acontece no mesmo frame do VFX, então o feedback nunca
    /// mente sobre o que acertou.
    /// </summary>
    [AddComponentMenu("Waraná/Combate/Raio Espiritual")]
    public class SpiritBoltEmitter : MonoBehaviour
    {
        /// <summary>Eixo LOCAL do prefab de VFX ao longo do qual o raio se estende.</summary>
        public enum BoltAxis
        {
            Down,
            Up,
            Right,
            Left,
            Forward,
        }

        [Header("Fonte do estado")]
        [Tooltip("Quem decide se estamos canalizando. Vazio = procura o PlayerChannel da cena.")]
        [SerializeField] private PlayerChannel channel;

        [Tooltip("De onde o raio sai visualmente. Vazio = este transform (o próprio olho).")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Os slots do raio. Vazio = procura na cena. Sem BoltCharges o disparo é ilimitado.")]
        [SerializeField] private BoltCharges charges;

        [Header("Alcance")]
        [Tooltip("Distância máxima do alvo até Piatã, em unidades. Curto de propósito.")]
        [SerializeField] private float range = 4.5f;

        [Tooltip("Meia-abertura do cone de mira, em graus, em torno da direção encarada.")]
        [Range(5f, 90f)]
        [SerializeField] private float coneHalfAngle = 55f;

        [Tooltip("Camadas que contam como inimigo.")]
        [SerializeField] private LayerMask targetMask = ~0;

        [Tooltip("Camadas que cortam a linha de visão (paredes, chão).")]
        [SerializeField] private LayerMask blockingMask;

        [Header("Disparo")]
        [SerializeField] private float damage = 1f;

        [Tooltip("Intervalo entre dois raios, em segundos.")]
        [SerializeField] private float fireInterval = 0.35f;

        [Tooltip("Atraso do primeiro raio ao entrar em canalização — o tempo de 'carregar'.")]
        [SerializeField] private float windUp = 0.18f;

        [Tooltip("O raio atravessa e fere todos os inimigos na linha, não só o primeiro.")]
        [SerializeField] private bool pierce = true;

        [Header("Telegrafia")]
        [Tooltip("O olho que carrega antes do tiro. Vazio = o GuaranaEye deste GameObject.")]
        [SerializeField] private GuaranaEye eye;

        [Tooltip("Quanto tempo antes do tiro a carga do olho começa. O brilho cai no frame do disparo.")]
        [SerializeField] private float chargeTime = 0.18f;

        [Header("VFX")]
        [Tooltip("Prefab do raio. Sugestão: Vefects/Zap VFX URP/.../VFX_Zap_03_Yellow.")]
        [SerializeField] private GameObject boltPrefab;

        [Tooltip("Eixo local ao longo do qual o prefab desenha o raio. Zap VFX da Vefects: Up.")]
        [SerializeField] private BoltAxis boltAxis = BoltAxis.Up;

        [Tooltip("Escala do prefab: define a espessura do raio e o tamanho do clarão. Não mexe no comprimento.")]
        [SerializeField] private float boltThickness = 0.22f;

        [Tooltip("Ajuste fino do comprimento. 0,72 é o valor medido para o Zap VFX da Vefects.")]
        [Range(0.2f, 2f)]
        [SerializeField] private float boltLengthCalibration = 0.72f;

        [Tooltip("Segundos até destruir a instância do VFX.")]
        [SerializeField] private float boltLifetime = 1.2f;

        [Tooltip("Sorting order aplicado a todos os renderers do VFX, para ficar à frente da arte.")]
        [SerializeField] private int boltSortingOrder = 30;

        [Header("Áudio")]
        [Tooltip("Um clipe é sorteado a cada disparo. Vazio = sem som.")]
        [SerializeField] private AudioClip[] fireSounds;

        [Range(0f, 1f)]
        [SerializeField] private float fireVolume = 0.5f;

        private readonly List<Collider2D> _candidates = new List<Collider2D>();
        private readonly List<RaycastHit2D> _lineHits = new List<RaycastHit2D>();
        private ContactFilter2D _targetFilter;
        private float _cooldownLeft;
        private bool _wasChanneling;
        private bool _charging;

        private Transform Muzzle => muzzle != null ? muzzle : transform;

        private void Awake()
        {
            // Triggers contam: um inimigo não precisa ter colisão física para ser alvo.
            _targetFilter = new ContactFilter2D { useTriggers = true };
            _targetFilter.SetLayerMask(targetMask);

            if (channel == null) channel = FindAnyObjectByType<PlayerChannel>();
            if (eye == null) eye = GetComponent<GuaranaEye>();
            if (charges == null) charges = FindAnyObjectByType<BoltCharges>();

            if (channel == null)
                Debug.LogWarning("[SpiritBolt] Nenhum PlayerChannel na cena; o raio nunca dispara.", this);

            if (boltPrefab == null)
                Debug.LogWarning("[SpiritBolt] Sem prefab de VFX; o dano acontece, mas sem raio visível.", this);
        }

        private void Update()
        {
            bool channeling = channel != null && channel.IsChanneling;

            // Entrar em canalização recarrega o wind-up: soltar e segurar de novo não
            // é atalho para cadência maior.
            if (channeling && !_wasChanneling) _cooldownLeft = windUp;

            // Sair da canalização no meio da carga deixaria o olho travado no brilho.
            if (!channeling && _wasChanneling) CancelCharge();

            _wasChanneling = channeling;

            if (!channeling) return;

            _cooldownLeft -= Time.deltaTime;

            TickCharge();

            if (_cooldownLeft > 0f) return;

            // O cooldown reinicia mesmo sem alvo: senão o primeiro inimigo a entrar no
            // alcance levaria um raio instantâneo, sem o tempo de leitura do wind-up.
            _cooldownLeft = fireInterval;
            _charging = false;

            if (!HasCharge) return;

            Transform target = FindTarget();
            if (target == null) return;

            // Só cobra o slot quando o raio de fato sai: mirar o vazio é de graça.
            if (charges != null && !charges.TrySpend()) return;

            Fire(target);
        }

        /// <summary>Sem componente de cargas o raio é ilimitado — o comportamento antigo.</summary>
        private bool HasCharge => charges == null || charges.CanSpend;

        // ------------------------------------------------------------- telegrafia

        /// <summary>
        /// Manda o olho começar a carga quando faltar exatamente <see cref="chargeTime"/>
        /// para o tiro. Só carrega se já houver alvo: um olho que brilha no vazio
        /// promete um raio que não vem, e o jogador aprende a ignorar o aviso.
        /// </summary>
        private void TickCharge()
        {
            if (_charging || eye == null || chargeTime <= 0f) return;

            // Sem slot não há tiro, e o olho não deve prometer um.
            if (!HasCharge) return;

            // Com cadência mais rápida que a carga, o olho passa o tempo todo carregando —
            // é o comportamento certo, e não uma animação atropelada pela metade.
            float lead = Mathf.Min(chargeTime, fireInterval);
            if (_cooldownLeft > lead) return;

            if (FindTarget() == null) return;

            _charging = true;
            eye.PlayAttack(Mathf.Max(_cooldownLeft, 0f));
        }

        private void CancelCharge()
        {
            if (!_charging) return;

            _charging = false;
            if (eye != null) eye.StopAttack();
        }

        private void OnDisable() => CancelCharge();

        // ----------------------------------------------------------------- mira

        /// <summary>
        /// Inimigo mais próximo dentro do alcance, à frente de Piatã e com linha de
        /// visão limpa. A mira parte de Piatã (não do orbe) porque o alcance é um
        /// contrato com o jogador sobre onde ele *plantou os pés*.
        /// </summary>
        private Transform FindTarget()
        {
            Vector2 origin = channel.AimOrigin;
            Vector2 facing = channel.AimDirection;

            _candidates.Clear();
            Physics2D.OverlapCircle(origin, range, _targetFilter, _candidates);

            float bestDistance = float.MaxValue;
            Transform best = null;

            foreach (Collider2D candidate in _candidates)
            {
                if (candidate == null) continue;

                var damageable = candidate.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                Vector2 point = candidate.bounds.center;
                Vector2 delta = point - origin;

                // Atrás das costas nunca conta, nem que esteja colado. A folga evita
                // rejeitar por ruído de ponto flutuante quando o alvo está tão perto
                // que delta.x beira zero (quase alinhado no eixo vertical).
                if (delta.x * facing.x < -0.001f) continue;
                if (Vector2.Angle(facing, delta) > coneHalfAngle) continue;

                float distance = delta.magnitude;
                if (distance >= bestDistance) continue;

                if (blockingMask != 0 &&
                    Physics2D.Raycast(origin, delta.normalized, distance, blockingMask))
                    continue;

                bestDistance = distance;
                best = candidate.transform;
            }

            return best;
        }

        // --------------------------------------------------------------- disparo

        private void Fire(Transform target)
        {
            Vector2 from = Muzzle.position;
            Vector2 to = TargetPoint(target);

            ApplyDamage(from, to, target);
            SpawnBolt(from, to);
            PlaySound(to);
        }

        private static Vector2 TargetPoint(Transform target)
        {
            var collider = target.GetComponent<Collider2D>();
            return collider != null ? (Vector2)collider.bounds.center : (Vector2)target.position;
        }

        private void ApplyDamage(Vector2 from, Vector2 to, Transform primary)
        {
            Vector2 direction = (to - from).normalized;

            if (!pierce)
            {
                Hit(primary, to, direction);
                return;
            }

            // Um raio que atravessa premia o jogador que alinha os inimigos — é o que
            // transforma o posicionamento em decisão, e não só em custo.
            _lineHits.Clear();
            Physics2D.Linecast(from, to, _targetFilter, _lineHits);

            bool hitPrimary = false;
            foreach (RaycastHit2D hit in _lineHits)
            {
                if (hit.collider == null) continue;

                if (hit.collider.transform == primary) hitPrimary = true;
                Hit(hit.collider.transform, hit.point, direction);
            }

            // O alvo escolhido leva dano mesmo que o Linecast o perca — pode acontecer
            // quando o orbe já está dentro do collider dele.
            if (!hitPrimary) Hit(primary, to, direction);
        }

        private void Hit(Transform victim, Vector2 point, Vector2 direction)
        {
            if (victim == null) return;

            var damageable = victim.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            damageable.TakeDamage(damage, point, direction);
        }

        // ------------------------------------------------------------------ VFX

        /// <summary>
        /// O encaixe em si mora em <see cref="BoltVfx"/>: o Prólogo dispara este mesmo
        /// raio na emboscada, e a conta do comprimento precisa ser uma só.
        /// </summary>
        private void SpawnBolt(Vector2 from, Vector2 to)
        {
            BoltVfx.Spawn(
                boltPrefab,
                from,
                to,
                LocalAxis(boltAxis),
                boltThickness,
                boltLengthCalibration,
                boltSortingOrder,
                boltLifetime);
        }

        private static Vector3 LocalAxis(BoltAxis axis)
        {
            switch (axis)
            {
                case BoltAxis.Up: return Vector3.up;
                case BoltAxis.Right: return Vector3.right;
                case BoltAxis.Left: return Vector3.left;
                case BoltAxis.Forward: return Vector3.forward;
                default: return Vector3.down;
            }
        }

        private void PlaySound(Vector2 at)
        {
            if (fireSounds == null || fireSounds.Length == 0) return;

            AudioClip clip = fireSounds[Random.Range(0, fireSounds.Length)];
            if (clip == null) return;

            // PlayClipAtPoint usa spatialBlend 3D por padrão, e a câmera fica a 10
            // unidades de distância em Z do plano de jogo: a atenuação por distância
            // deixava o raio quase inaudível mesmo com o alvo colado na câmera.
            // Como som 2D não há esse problema.
            var go = new GameObject("SpiritBoltSFX");
            go.transform.position = at;

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.volume = fireVolume;
            source.Play();

            Destroy(go, clip.length);
        }

        // --------------------------------------------------------------- gizmos

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Permite mexer na máscara com o jogo rodando e ver o efeito na hora.
            _targetFilter.useTriggers = true;
            _targetFilter.SetLayerMask(targetMask);
        }

        private void OnDrawGizmosSelected()
        {
            if (channel == null) return;

            Vector3 origin = channel.AimOrigin;
            Vector3 facing = channel.AimDirection;
            if (facing == Vector3.zero) facing = Vector3.right;

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(origin, range);

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, 0f, coneHalfAngle) * facing * range);
            Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, 0f, -coneHalfAngle) * facing * range);
        }
#endif
    }
}
