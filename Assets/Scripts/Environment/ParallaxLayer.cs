using UnityEngine;

namespace Warana.Environment
{
    /// <summary>
    /// Uma camada de fundo com parallax. Colocada em cada camada de trás; lê a câmera
    /// principal e desliza a camada a uma fração do movimento dela, medido a partir da
    /// posição em que a câmera estava quando a camada acordou (por isso funciona mesmo com
    /// o spawn longe da origem do mundo).
    ///
    /// <para><see cref="parallaxFactor"/> vai de 0 a 1: 0 fixa a camada ao mundo (como o
    /// chão), 1 fixa à câmera (como se estivesse infinitamente longe). Céu ~0.9, linha de
    /// árvores ~0.6.</para>
    ///
    /// Com <see cref="infiniteHorizontal"/> ligado, a faixa nunca descobre a tela: sempre
    /// que se afasta mais de uma largura de sprite da câmera, o ponto de origem salta uma
    /// largura — invisível porque o <see cref="SpriteRenderer"/> está em
    /// <see cref="SpriteDrawMode.Tiled"/> (o padrão da imagem se repete a cada largura).
    ///
    /// <para>Só roda em play mode, de propósito: com <c>ExecuteAlways</c> o script
    /// reescrevia a posição da camada a cada frame do editor usando a posição atual da
    /// câmera, e era essa posição deslocada que ia para o arquivo da cena — a cada reload
    /// a camada partia de um ponto diferente do autorado. Sem ele, o que está salvo na
    /// cena é sempre a posição de autoria.</para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("0 = preso ao mundo (foreground). 1 = preso à câmera (fundo infinito).")]
        [Range(0f, 1f)]
        [SerializeField] private float parallaxFactor = 0.8f;

        [Tooltip("Mantém a faixa cobrindo a tela repetindo o sprite. Exige Draw Mode = Tiled.")]
        [SerializeField] private bool infiniteHorizontal = true;

        [Tooltip("A camada acompanha o Y da câmera (Y inicial vira deslocamento). Off = Y fixo.")]
        [SerializeField] private bool followVertical = true;

        private Transform _camera;
        private SpriteRenderer _renderer;
        private bool _initialized;
        private float _startX;
        private float _startY;
        private float _startZ;
        private float _camStartX;

        private void OnEnable()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _initialized = false;
            ResolveCamera();
        }

        private void LateUpdate()
        {
            if (_camera == null && !ResolveCamera()) return;

            // Ancora no primeiro frame em que a câmera existe: guarda a posição da camada e
            // da câmera para medir tudo em deslocamento relativo.
            if (!_initialized)
            {
                _startX = transform.position.x;
                _startY = transform.position.y;
                _startZ = transform.position.z;
                _camStartX = _camera.position.x;
                _initialized = true;
            }

            Vector3 camPos = _camera.position;

            float x = _startX + (camPos.x - _camStartX) * parallaxFactor;
            float y = followVertical ? camPos.y + _startY : _startY;

            if (infiniteHorizontal)
            {
                float tile = TileWidth();
                if (tile > 0f)
                {
                    // Recentra em passos de uma largura de sprite (salto imperceptível no
                    // padrão tiled), mantendo a faixa sempre em torno da câmera.
                    while (x < camPos.x - tile) { _startX += tile; x += tile; }
                    while (x > camPos.x + tile) { _startX -= tile; x -= tile; }
                }
            }

            transform.position = new Vector3(x, y, _startZ);
        }

        /// <summary>Largura de um sprite (um período do padrão) em unidades de mundo.</summary>
        private float TileWidth()
        {
            if (_renderer == null || _renderer.sprite == null) return 0f;
            return _renderer.sprite.bounds.size.x * Mathf.Abs(transform.lossyScale.x);
        }

        private bool ResolveCamera()
        {
            Camera main = Camera.main;
            _camera = main != null ? main.transform : null;
            return _camera != null;
        }
    }
}
