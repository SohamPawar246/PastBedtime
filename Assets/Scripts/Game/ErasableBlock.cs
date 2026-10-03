using UnityEngine;

/// <summary>A floor segment the Eraser can rub out (it fades, crumbles and stops being solid).
/// Restarting the panel draws it back in, so an eaten bridge never locks the way.</summary>
public class ErasableBlock : MonoBehaviour
{
    public const float FullInk = 0.6f;
    public float Ink = FullInk;
    private float _baseY = 1f;
    private bool _measured;

    private void Measure()
    {
        if (_measured) return;
        _measured = true;
        _baseY = transform.localScale.y;
    }

    public void Rub(float dt)
    {
        Measure();
        if (Ink <= 0f) return;
        Ink -= dt;
        SetHeight(Mathf.Lerp(0.15f, 1f, Ink / FullInk));
        if (Ink <= 0f)
        {
            SfxLettering.Spawn("ERASED!", (Vector2)transform.position + Vector2.up * 1.2f, Palette.Paper, 0.8f);
            GameAudio.Play("scrub", 0.6f);
            gameObject.SetActive(false);
        }
    }

    public void Restore()
    {
        Measure();
        Ink = FullInk;
        SetHeight(1f);
        gameObject.SetActive(true);
    }

    private void SetHeight(float k)
    {
        var s = transform.localScale;
        transform.localScale = new Vector3(s.x, k * _baseY, s.z);
    }
}
