using UnityEngine;

/// <summary>
/// Stand-in for the Game scene until the bedroom and page 1 exist: proves the
/// flow Title → Game → Bookmark pause → Title works end to end. Delete this
/// component from the Game scene once real gameplay is in.
/// </summary>
public class GamePlaceholder : MonoBehaviour
{
    private void Start()
    {
        UIKit.EnsureEventSystem();
        var canvas = UIKit.Canvas("PlaceholderCanvas", 0);
        var root = canvas.transform;

        var bg = UIKit.Image(root, "Night", Palette.Bedroom);
        UIKit.Stretch(bg.rectTransform);

        var page = UIKit.Panel(root, "Page", new Vector2(0f, -10f), new Vector2(1100f, 620f), Palette.Paper, shadow: 14f, tilt: -2f);
        UIKit.Lettering(page.transform, "Title", "PAGE 1", 120f, Palette.Yellow, new Vector2(0f, 130f),
            new Vector2(900f, 150f), outline: 0.24f, shadow: 8f, tilt: -3f);
        var body = UIKit.Text(page.transform, "Body",
            "Rooftop at night. Max Voltage moves only in the light.\n<size=70%>(gameplay goes here — press ESC for the Bookmark pause)</size>",
            UIKit.Body, 40f, Palette.Ink);
        UIKit.Place(body.rectTransform, new Vector2(0f, -90f), new Vector2(950f, 220f));
    }
}
