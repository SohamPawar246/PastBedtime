using UnityEngine;

/// <summary>
/// "How to read": the controls from GDD section 3 as a two-column comic page (UI/HowToRead).
/// Left hand plays Max, right hand plays the reader. The keys are the player's own
/// (<see cref="Bindings"/>), re-read whenever the page opens or the controls change (<see cref="HowToReadLive"/>).
/// </summary>
public static class HowToReadPanel
{
    /// <summary>"KEY .......... what it does": the dots run out to a column (and shrink for long names).</summary>
    public static string Line(string key, string what) =>
        "<b>" + key + " " + new string('.', Mathf.Max(2, 14 - key.Length)) + "</b> " + what;
}
