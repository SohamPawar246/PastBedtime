using TMPro;
using UnityEngine;

/// <summary>A gold star (5 per page): Max picks it up when he touches it, awake.</summary>
public class StarPickup : MonoBehaviour
{
    private Lightable _light;
    private float _spin;
    /// <summary>Which of its page's stars this is (stars found once aren't drawn again).</summary>
    public int Index;

    private void Awake() => _light = GetComponent<Lightable>();

    private void Update()
    {
        float dt = _light.Delta;
        _spin += dt * 120f;
        transform.rotation = Quaternion.Euler(0f, _spin, 0f);
        var hero = HeroController.I;
        if (hero == null || !hero.Light.IsAwake) return;
        Vector2 d = (Vector2)hero.transform.position + Vector2.up * 0.9f - (Vector2)transform.position;
        if (Mathf.Abs(d.x) < 0.7f && Mathf.Abs(d.y) < 1.1f)
        {
            var gs = GameState.I;
            gs?.FindStar(Index);
            GameHUD.I?.StarFound(transform.position);                   // it flies up into the counter
            SfxLettering.Spawn("TWINKLE!", (Vector2)transform.position + Vector2.up, Palette.Yellow, 0.8f);
            GameAudio.Play("star", 0.7f);
            Destroy(gameObject);
        }
    }
}
