using UnityEngine;

namespace Warana.Combat
{
    /// <summary>
    /// Contrato mínimo de qualquer coisa que possa levar dano. O emissor do raio
    /// só conhece esta interface — nunca o inimigo concreto.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <param name="amount">Dano bruto.</param>
        /// <param name="point">Onde o golpe chegou, em coordenadas de mundo.</param>
        /// <param name="direction">Direção do golpe, normalizada. Serve para knockback.</param>
        void TakeDamage(float amount, Vector2 point, Vector2 direction);
    }
}
