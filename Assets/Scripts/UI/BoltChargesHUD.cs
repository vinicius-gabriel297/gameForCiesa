using UnityEngine;
using Warana.Combat;

namespace Warana.UI
{
    /// <summary>
    /// Os relâmpagos do Waraná, logo abaixo dos corações. Um ícone por slot.
    ///
    /// Diferente dos corações, este lê por frame em vez de por evento: o slot em
    /// recarga acende aos poucos, e essa animação é a única forma de o jogador saber
    /// quando o próximo raio volta.
    /// </summary>
    [AddComponentMenu("Waraná/UI/Cargas do Raio")]
    public class BoltChargesHUD : IconRowHUD
    {
        [Header("Vinculação")]
        [Tooltip("Cargas acompanhadas. Vazio = procura o BoltCharges da cena.")]
        [SerializeField] private BoltCharges charges;

        private void OnEnable()
        {
            if (charges == null) charges = FindAnyObjectByType<BoltCharges>();

            if (charges == null)
                Debug.LogWarning("[BoltChargesHUD] Nenhum BoltCharges encontrado; os ícones ficam vazios.", this);
        }

        private void Update()
        {
            if (charges == null) return;

            if (IconCount != charges.Max) Build(charges.Max);

            Paint(charges.Current, charges.RefillProgress);
        }
    }
}
