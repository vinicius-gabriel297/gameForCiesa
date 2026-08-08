using UnityEngine;

namespace Warana.Combat
{
    /// <summary>
    /// Implementado por quem controla o próprio Rigidbody2D manualmente (Player, inimigos).
    /// Sem isso, <see cref="Health"/> escreveria a velocidade direto no corpo e o
    /// controller sobrescreveria o empurrão no FixedUpdate seguinte, um frame depois.
    /// </summary>
    public interface IKnockbackReceiver
    {
        /// <param name="velocity">Velocidade inicial do empurrão.</param>
        /// <param name="duration">Por quanto tempo o controle normal fica suspenso.</param>
        void ApplyKnockback(Vector2 velocity, float duration);
    }
}
