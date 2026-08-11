using System;
using UnityEditor;
using UnityEngine;

namespace Warana.EditorTools
{
    /// <summary>
    /// Pasta e guarda das cenas de rascunho.
    ///
    /// Os builders de cena deste projeto começam com <c>NewSceneSetup.EmptyScene</c> e montam
    /// tudo do zero. Isso era inofensivo enquanto eles gravavam em cenas próprias, mas
    /// <c>Fase01_Floresta</c> e <c>TilemapTest</c> viraram <c>Mapa_01</c> e <c>Prologo</c> num
    /// rename, e essas duas foram editadas à mão desde então — quase 24 mil células pintadas.
    /// Apontar um builder para elas apagaria o nível inteiro.
    ///
    /// Daí a regra: rascunho grava em <see cref="Folder"/>, que não está no Build Settings, e
    /// <see cref="CanWrite"/> recusa qualquer caminho que esteja.
    /// </summary>
    public static class ScratchScene
    {
        public const string Folder = "Assets/Scenes/Rascunho";

        /// <summary>Falso (e reclama no console) se o caminho for uma cena do Build Settings.</summary>
        public static bool CanWrite(string scenePath)
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase)) continue;

                Debug.LogError($"[Rascunho] '{scenePath}' está no Build Settings e seria " +
                               "reconstruída do zero. Recusando: este builder só grava em " +
                               $"{Folder}.");
                return false;
            }

            return true;
        }

        public static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;

            int slash = Folder.LastIndexOf('/');
            AssetDatabase.CreateFolder(Folder[..slash], Folder[(slash + 1)..]);
        }
    }
}
