using UnityEngine;

/// <summary>
/// Puts things on a drawn page (the pages themselves are prefabs, Art/Pages/PageNN): the Inkies at their
/// panels' spawn points (each held inside its own panel), and the Comic layer the page camera draws.
/// </summary>
public static class PageBuilder
{
    /// <summary>Instantiates a panel's Inkies at its spawn points (used again when a panel restarts).</summary>
    public static void SpawnEnemies(PanelLayout pl, Transform parent)
    {
        foreach (var s in pl.GetComponentsInChildren<SpawnPoint>(true)) Spawn(s.kind, s.transform.position, pl, parent, s.faceRight);
    }

    /// <summary>One Inkie into a panel, its arena (Baron Blot summons Smudges this way too).</summary>
    public static EnemyBrain Spawn(EnemyKind kind, Vector2 at, PanelLayout pl, Transform parent, bool faceRight = false)
    {
        var A = GameAssets.I;
        var prefab = A != null ? A.Enemy(kind) : null;
        if (prefab == null) return null;
        var go = Object.Instantiate(prefab, new Vector3(at.x, at.y, 0f), Quaternion.identity, parent);
        go.name = kind.ToString();
        SetLayer(go.transform);
        var brain = go.GetComponent<EnemyBrain>();
        if (brain == null) return null;
        brain.MinX = pl.rect.xMin;
        brain.MaxX = pl.rect.xMax;
        brain.Facing = faceRight ? 1f : -1f;
        brain.Panel = pl;
        brain.Home = at;
        if (brain is DotShotBrain ds && A != null) ds.PelletMaterial = A.ink;
        var body = go.GetComponent<CharacterController>();
        InkShadow.Add(go, body != null ? body.radius * 2.7f : 1f);
        pl.enemies.Add(brain);
        return brain;
    }

    public static void SetLayer(Transform t)
    {
        int layer = LayerMask.NameToLayer("Comic");
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = layer;
    }
}
