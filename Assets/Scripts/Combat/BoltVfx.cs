using UnityEngine;

namespace Warana.Combat
{
    /// <summary>
    /// Encaixa uma instância do prefab de raio entre dois pontos.
    ///
    /// Mora fora do <see cref="SpiritBoltEmitter"/> porque o Prólogo dispara o mesmo
    /// raio: na emboscada é o Waraná se fazendo físico para defender Piatã, e o
    /// jogador precisa reconhecer ali a arma que vai carregar a fase inteira. Com a
    /// conta duplicada nos dois lugares, o primeiro ajuste de espessura ou de
    /// calibragem separaria os dois raios sem ninguém perceber.
    /// </summary>
    public static class BoltVfx
    {
        /// <summary>
        /// A rotação alinha o eixo do raio com a direção do tiro, e a escala do
        /// transform define só a espessura. O comprimento não vem da escala: o raio é
        /// uma billboard esticada (Stretched Billboard), e o tamanho dela é o
        /// startSize.y da partícula multiplicado pelo lengthScale do renderer —
        /// escalar o transform engorda o raio sem encurtá-lo. Por isso o comprimento
        /// é escrito direto na fonte, partícula por partícula.
        /// </summary>
        /// <param name="localAxis">Eixo LOCAL do prefab ao longo do qual o raio se
        /// estende. Nos Zap VFX da Vefects é <see cref="Vector3.up"/>.</param>
        public static GameObject Spawn(
            GameObject prefab,
            Vector2 from,
            Vector2 to,
            Vector3 localAxis,
            float thickness,
            float lengthCalibration,
            int sortingOrder,
            float lifetime)
        {
            if (prefab == null) return null;

            Vector3 delta = (Vector3)(to - from);
            float distance = delta.magnitude;
            if (distance < 0.001f) return null;

            GameObject instance = Object.Instantiate(prefab, from, Quaternion.identity);

            Vector3 direction = delta / distance;
            instance.transform.rotation = Quaternion.FromToRotation(localAxis, direction);

            float safeThickness = Mathf.Max(0.001f, thickness);
            instance.transform.localScale = Vector3.one * safeThickness;

            float wanted = distance * lengthCalibration;

            foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer == null || renderer.renderMode != ParticleSystemRenderMode.Stretch) continue;

                ParticleSystem.MainModule main = system.main;
                main.startSizeYMultiplier = wanted / (Mathf.Max(0.001f, renderer.lengthScale) * safeThickness);
            }

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.sortingOrder = sortingOrder;

            Object.Destroy(instance, lifetime);
            return instance;
        }
    }
}
