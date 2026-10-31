using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One panel of a drawn page: an arena or platforming room. Everything drawn in it, and its
/// Inkies' <see cref="SpawnPoint"/>s, are its children (at their places on the lane).
/// </summary>
public class PanelLayout : MonoBehaviour
{
    public Rect rect;                         // world rect on the lane
    public int tier, index;
    public Vector2 entry;                     // where Max respawns in this panel
    public float floorLine;                   // the lowest top anything can stand on (world y): below it is a pit
    [Tooltip("What Max says to the reader when he walks into this panel.")]
    public string heroLine;
    public InkFlood flood;                    // Act 3's rising ink, if the panel has it

    public readonly List<EnemyBrain> enemies = new();
    [System.NonSerialized] public bool visited;

    public Transform root => this != null ? transform : null;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(rect.center, rect.size);
        Gizmos.DrawWireSphere(entry, 0.2f);
    }
}
