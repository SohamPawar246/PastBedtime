using UnityEngine;

/// <summary>
/// Builds the Game scene (GDD section 14): the light field, the torch, the page camera and the
/// page view, the HUD, Max, and the page manager, then opens the first page.
/// </summary>
public class GameDirector : MonoBehaviour
{
    /// <summary>The page to open (set before loading the scene, e.g. by "continue").</summary>
    public static int StartPage = 1;

    private void Start()
    {
        UIKit.EnsureEventSystem();
        var A = GameAssets.I;
        if (A == null || A.max == null)
        {
            Debug.LogError("[Game] Missing Resources/Game/GameAssets: run Past Bedtime > Build Game Assets.");
            return;
        }

        var gs = new GameObject("GameState").AddComponent<GameState>();
        new GameObject("LightField").AddComponent<LightField>();
        var torch = new GameObject("Torch").AddComponent<TorchController>();
        // the stars in hand and the Nightstand's upgrades carry over from earlier sittings
        gs.StarsTotal = Settings.Stars;
        gs.Spring = Settings.Spring;
        gs.Gear = Settings.Gear;
        gs.Ratchet = Settings.Ratchet;
        torch.Charge.ApplyUpgrades(gs.Spring, gs.Gear, gs.Ratchet);
        var pageCam = new GameObject("PageCamera").AddComponent<PageCamera>();

        // the comic's own key light: from the reader's side, up and to the left
        var key = new GameObject("ComicLight").AddComponent<ComicLight>();
        key.transform.rotation = Quaternion.LookRotation(new Vector3(0.45f, -0.6f, 0.65f));
        key.ambient = 0.36f;

        // the screen: the bedroom-dark backdrop with the page on it. The scene's camera only clears
        // the frame; the comic itself is drawn once, by the page camera, into the page texture.
        var screenCam = Camera.main;
        if (screenCam == null) screenCam = new GameObject("ScreenCamera").AddComponent<Camera>();
        screenCam.clearFlags = CameraClearFlags.SolidColor;
        screenCam.backgroundColor = Palette.Bedroom;
        screenCam.cullingMask = 1 << LayerMask.NameToLayer("UI");    // only the overlay UI, never the comic
        screenCam.depth = -10;

        // the bedroom (the title's render), the open comic on Roshan's knees, his hand and the torch
        var canvas = UIKit.Canvas("RoomCanvas", 0);
        var view = canvas.gameObject.AddComponent<PageView>();
        view.Build(canvas.transform, pageCam.Texture);

        // the comic's own HUD is printed on the page: a canvas the page camera draws into the texture
        var pageHud = PageHudCanvas(pageCam);
        var hud = pageHud.gameObject.AddComponent<GameHUD>();
        hud.Build((RectTransform)pageHud.transform);
        PageBuilder.SetLayer(pageHud.transform);

        var hero = Instantiate(A.max);
        hero.name = "Max";
        InkShadow.Add(hero, 0.95f);
        PageBuilder.SetLayer(hero.transform);

        GameTorch.Build(view.Front);
        new GameObject("Mom").AddComponent<MomDirector>();
        new GameObject("Finale").AddComponent<Finale>();
        new GameObject("Music").AddComponent<GameMusic>();
        MomDoorView.Build(view.DoorLayer, view.Front);

        // from the title: the cover swings open and the view zooms in (the page's title card waits
        // for it); otherwise straight in
        if (OpenFromTitle) StartCoroutine(view.Open());
        else view.SkipOpening();
        OpenFromTitle = false;

        var pages = new GameObject("Pages").AddComponent<PageManager>();
        pages.Load(Mathf.Clamp(StartPage, 1, PageManager.LastPage));
    }

    /// <summary>Set by the title as it hands over: the game starts in its framing and opens the comic.</summary>
    public static bool OpenFromTitle;

    private static Canvas PageHudCanvas(PageCamera pageCam)
    {
        var go = new GameObject("PageHUD", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = pageCam.Cam;
        canvas.planeDistance = 1f;                                  // in front of everything in the comic
        var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(PageCamera.TextureWidth, PageCamera.TextureHeight);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }
}
