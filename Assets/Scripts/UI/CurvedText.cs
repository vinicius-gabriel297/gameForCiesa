using TMPro;
using UnityEngine;

namespace Warana.UI
{
    /// <summary>
    /// Arqueia um TextMeshProUGUI ao longo de um arco de círculo, tipo logotipo —
    /// usado no "Waraná" do menu principal. Mexe direto na malha gerada pelo TMP em
    /// vez de trocar a fonte ou usar sprites por letra, então qualquer texto curto
    /// e centralizado funciona sem preparo extra.
    ///
    /// Cada caractere é reposicionado ao redor de um círculo de raio <see cref="radius"/>:
    /// o ângulo dele é a distância horizontal até o centro do texto, dividida pelo
    /// raio (comprimento de arco / raio = ângulo em radianos). Raio pequeno = curva
    /// fechada; raio grande = quase reto.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Waraná/UI/Texto Curvo")]
    public class CurvedText : MonoBehaviour
    {
        [Tooltip("Raio do arco. Menor = curva mais fechada (mais \"sorriso\").")]
        [SerializeField] private float radius = 260f;

        [Tooltip("Arqueia para cima (sorriso) ou para baixo.")]
        [SerializeField] private bool curveUpward = true;

        private TMP_Text _text;

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            ApplyCurve();
        }

        private void LateUpdate() => ApplyCurve();

        private void ApplyCurve()
        {
            if (_text == null) return;
            if (radius <= 0.01f) return;

            _text.ForceMeshUpdate();
            TMP_TextInfo textInfo = _text.textInfo;
            float direction = curveUpward ? 1f : -1f;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                Vector3[] verts = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;
                int vIndex = charInfo.vertexIndex;

                // Ponto médio da base do caractere: é em torno dele que o quad gira.
                Vector3 charMid = (verts[vIndex + 0] + verts[vIndex + 2]) / 2f;

                verts[vIndex + 0] -= charMid;
                verts[vIndex + 1] -= charMid;
                verts[vIndex + 2] -= charMid;
                verts[vIndex + 3] -= charMid;

                // Comprimento de arco até aqui, convertido em ângulo (s = r * θ).
                float angle = (charMid.x / radius) * direction;

                Vector3 arcPosition = new Vector3(
                    radius * Mathf.Sin(angle) * direction,
                    radius * (Mathf.Cos(angle) - 1f) * direction,
                    0f);

                Matrix4x4 matrix = Matrix4x4.TRS(
                    charMid + arcPosition,
                    Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg * direction),
                    Vector3.one);

                verts[vIndex + 0] = matrix.MultiplyPoint3x4(verts[vIndex + 0]);
                verts[vIndex + 1] = matrix.MultiplyPoint3x4(verts[vIndex + 1]);
                verts[vIndex + 2] = matrix.MultiplyPoint3x4(verts[vIndex + 2]);
                verts[vIndex + 3] = matrix.MultiplyPoint3x4(verts[vIndex + 3]);
            }

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                _text.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }
        }
    }
}
