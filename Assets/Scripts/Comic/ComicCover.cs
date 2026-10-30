using UnityEngine;

/// <summary>
/// The cover art of Max Voltage #1 (Resources/Comic/cover.png): the posed comic set
/// (Art/Comic/CoverSet, built by Past Bedtime > Build Comic Look) printed in black and white.
/// Shared by every screen that shows the comic (title, Bookmark pause).
/// </summary>
public static class ComicCover
{
    private static Texture2D _shared;

    public static Texture Shared => _shared != null ? _shared : _shared = Resources.Load<Texture2D>("Comic/cover");
}
