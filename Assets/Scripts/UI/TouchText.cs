using TMPro;
using UnityEngine;

/// <summary>
/// A line that says something else when the game is played by touch: "PRESS ANY KEY TO CONTINUE" becomes "TAP TO
/// CONTINUE", a keyboard hint goes (an empty touch text hides it). It follows the last input used, so a phone with a
/// keyboard plugged in reads the keyboard's line.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TouchText : MonoBehaviour
{
    [Tooltip("What it says when played by touch (empty: nothing).")]
    [TextArea] [SerializeField] private string _touch;
    private TMP_Text _text;
    private string _keys;
    private bool _shown, _touchShown;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _keys = _text.text;
    }

    private void OnEnable() => Apply(force: true);
    private void Update() => Apply(force: false);

    private void Apply(bool force)
    {
        bool touch = GameInput.TouchMode;
        if (!force && _shown && touch == _touchShown) return;
        _shown = true;
        _touchShown = touch;
        _text.text = touch ? _touch : _keys;
    }
}
