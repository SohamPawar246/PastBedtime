using UnityEngine;

/// <summary>Where one of a panel's Inkies is drawn in (a child of its <see cref="PanelLayout"/>): it
/// spawns here when the page opens and again when the panel restarts.</summary>
public class SpawnPoint : MonoBehaviour
{
    public EnemyKind kind;
    public bool faceRight;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 0.9f, 0.8f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.6f, new Vector3(0.8f, 1.2f, 0.1f));
        Gizmos.DrawLine(transform.position + Vector3.up * 0.6f, transform.position + new Vector3(faceRight ? 0.7f : -0.7f, 0.6f, 0f));
    }
}
