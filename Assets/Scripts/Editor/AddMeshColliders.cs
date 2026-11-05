using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de editor para el Laboratorio 3 (OVRPlayerController).
///
/// Menu:  Tools > Lab3 > Anadir Mesh Colliders a la seleccion
///
/// Recorre el/los GameObject(s) seleccionado(s) en la jerarquia y TODOS sus hijos
/// (incluidos los inactivos) y agrega un Mesh Collider NON-CONVEX a cada objeto que
/// tenga malla (MeshFilter con sharedMesh) y que todavia NO tenga ningun Collider.
/// Los objetos que ya tienen collider se dejan sin tocar.
///
/// Pensada para geometria estatica importada desde Revit: con esto el jugador deja de
/// atravesar muros, puertas, suelos y pilares, y no se cae al vacio.
///
/// La operacion completa queda bajo un unico paso de Undo (Ctrl+Z).
/// </summary>
public static class AddMeshColliders
{
    private const string MenuPath = "Tools/Lab3/Anadir Mesh Colliders a la seleccion";

    [MenuItem(MenuPath, false, 1000)]
    private static void Run()
    {
        GameObject[] roots = Selection.gameObjects;

        if (roots == null || roots.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Anadir Mesh Colliders - Lab3",
                "No hay ningun GameObject seleccionado.\n\n" +
                "Selecciona el objeto raiz (por ejemplo la casa importada) en la jerarquia " +
                "y vuelve a ejecutar la herramienta.",
                "Entendido");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Lab3 - Anadir Mesh Colliders (non-convex)");
        int undoGroup = Undo.GetCurrentGroup();

        int added = 0;
        int skippedHasCollider = 0;
        int skippedNoMesh = 0;
        int meshFiltersChecked = 0;

        foreach (GameObject root in roots)
        {
            if (root == null)
                continue;

            // includeInactive = true
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

            foreach (MeshFilter mf in filters)
            {
                meshFiltersChecked++;

                if (mf.sharedMesh == null)
                {
                    skippedNoMesh++;
                    continue;
                }

                if (mf.GetComponent<Collider>() != null)
                {
                    skippedHasCollider++;
                    continue;
                }

                MeshCollider mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.convex = false;      // NON-CONVEX
                mc.isTrigger = false;
                mc.sharedMesh = mf.sharedMesh;
                EditorUtility.SetDirty(mf.gameObject);
                added++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        string summary =
            "Mesh Colliders anadidos:            " + added + "\n" +
            "Ya tenian collider (sin cambios):   " + skippedHasCollider + "\n" +
            "MeshFilter sin malla (ignorados):   " + skippedNoMesh + "\n" +
            "MeshFilter revisados:               " + meshFiltersChecked + "\n" +
            "Raices seleccionadas:               " + roots.Length;

        Debug.Log("[Lab3] Anadir Mesh Colliders a la seleccion\n" + summary);
        EditorUtility.DisplayDialog("Anadir Mesh Colliders - Lab3", summary, "OK");
    }

    // Deshabilita la entrada de menu cuando no hay nada seleccionado.
    [MenuItem(MenuPath, true)]
    private static bool RunValidate()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }
}
