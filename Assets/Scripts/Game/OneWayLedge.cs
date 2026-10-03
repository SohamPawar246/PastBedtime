using UnityEngine;

/// <summary>
/// A ledge Max jumps up through and lands on (speech bubbles, invisible ink): solid to him only while
/// his feet are above its top and he isn't rising. Runs before Max moves each frame.
/// </summary>
[DefaultExecutionOrder(-50)]
public class OneWayLedge : MonoBehaviour
{
    private Collider _col;

    private void Awake() => _col = GetComponent<Collider>();

    private void Update()
    {
        var hero = HeroController.I;
        if (hero == null || hero.Body == null || _col == null || !_col.enabled || !hero.Body.enabled) return;
        bool above = hero.transform.position.y >= _col.bounds.max.y - 0.1f;
        bool solid = above && hero.Velocity.y <= 0.5f;
        Physics.IgnoreCollision(hero.Body, _col, !solid);   // re-asserted every frame: toggling a collider resets it
    }
}
