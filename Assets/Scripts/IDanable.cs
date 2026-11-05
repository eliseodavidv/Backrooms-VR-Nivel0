using UnityEngine;

// Todo lo que las armas pueden danar. La entidad principal NO lo implementa a proposito (no se puede matar);
// ademas las armas ignoran explicitamente cualquier collider que cuelgue de un EntityAI.
public interface IDanable
{
    void RecibirDanio(int cantidad, Vector3 puntoImpacto);
}
