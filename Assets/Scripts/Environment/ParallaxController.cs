using System.Collections.Generic;
using UnityEngine;

namespace Warana.Environment
{
    /// <summary>
    /// Gerenciador central de parallax. Coloque num objeto único (ex.: "Parallax") dentro da
    /// Scene e liste quantas camadas quiser em <see cref="layers"/> — não há limite de
    /// background/foreground, cada slot é livre para apontar a qualquer Transform da cena.
    ///
    /// Substitui a necessidade de um script por camada (<see cref="ParallaxLayer"/>): aqui
    /// tudo é configurado em uma lista só, direto no Inspector deste objeto.
    /// </summary>
    [ExecuteAlways]
    public class ParallaxController : MonoBehaviour
    {
        [System.Serializable]
        public class Layer
        {
            [Tooltip("Nome só para identificar o slot no Inspector.")]
            public string name = "Layer";

            [Tooltip("Transform da camada (background ou foreground) a mover.")]
            public Transform target;

            [Tooltip("0 = preso ao mundo (foreground). 1 = preso à câmera (fundo infinito).")]
            [Range(0f, 1f)]
            public float parallaxFactor = 0.8f;

            [Tooltip("Mantém a faixa cobrindo a tela repetindo o sprite. Exige Draw Mode = Tiled.")]
            public bool infiniteHorizontal = true;

            [Tooltip("A camada acompanha o Y da câmera (Y inicial vira deslocamento). Off = Y fixo.")]
            public bool followVertical = true;

            [System.NonSerialized] public bool initialized;
            [System.NonSerialized] public float startX;
            [System.NonSerialized] public float startY;
            [System.NonSerialized] public float startZ;
            [System.NonSerialized] public float camStartX;
            [System.NonSerialized] public SpriteRenderer spriteRenderer;
        }

        [Tooltip("Slots de camadas de parallax. Adicione quantos precisar.")]
        [SerializeField] private List<Layer> layers = new List<Layer>();

        private Transform _camera;

        private void OnEnable()
        {
            foreach (Layer layer in layers) layer.initialized = false;
            ResolveCamera();
        }

        private void LateUpdate()
        {
            if (_camera == null && !ResolveCamera()) return;

            Vector3 camPos = _camera.position;

            foreach (Layer layer in layers)
            {
                if (layer.target == null) continue;

                if (!layer.initialized)
                {
                    layer.startX = layer.target.position.x;
                    layer.startY = layer.target.position.y;
                    layer.startZ = layer.target.position.z;
                    layer.camStartX = camPos.x;
                    layer.spriteRenderer = layer.target.GetComponent<SpriteRenderer>();
                    layer.initialized = true;
                }

                float x = layer.startX + (camPos.x - layer.camStartX) * layer.parallaxFactor;
                float y = layer.followVertical ? camPos.y + layer.startY : layer.startY;

                if (layer.infiniteHorizontal)
                {
                    float tile = TileWidth(layer);
                    if (tile > 0f)
                    {
                        while (x < camPos.x - tile) { layer.startX += tile; x += tile; }
                        while (x > camPos.x + tile) { layer.startX -= tile; x -= tile; }
                    }
                }

                layer.target.position = new Vector3(x, y, layer.startZ);
            }
        }

        /// <summary>Largura de um sprite (um período do padrão) em unidades de mundo.</summary>
        private static float TileWidth(Layer layer)
        {
            if (layer.spriteRenderer == null || layer.spriteRenderer.sprite == null) return 0f;
            return layer.spriteRenderer.sprite.bounds.size.x * Mathf.Abs(layer.target.lossyScale.x);
        }

        private bool ResolveCamera()
        {
            Camera main = Camera.main;
            _camera = main != null ? main.transform : null;
            return _camera != null;
        }
    }
}
