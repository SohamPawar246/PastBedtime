using UnityEngine;

/// <summary>
/// Starts the Game scene (GDD section 14). Everything in it is drawn in the scene and waits switched off: the
/// light field, the torch, the page camera, the room with the open comic, the HUD printed on the page, Max, Mom
/// and the page manager. This switches them on one after another, in the order each depends on the last, gives
/// them what only exists once the game runs (the page camera's texture, the upgrades in the save), then opens the
/// first page.
/// </summary>
public class GameDirector : MonoBehaviour
{
    /// <summary>The page to open (set before loading the scene, e.g. by "continue").</summary>
    public static int StartPage = 1;

    [SerializeField] private GameState _state;
    [SerializeField] private LightField _light;
    [SerializeField] private TorchController _torch;
    [SerializeField] private PageCamera _pageCam;
    [Tooltip("The comic's own key light: from the reader's side, up and to the left.")]
    [SerializeField] private ComicLight _key;
    [Tooltip("The bedroom (the title's render), the open comic on Roshan's knees, his hand and the torch, Mom's door.")]
    [SerializeField] private PageView _view;
    [Tooltip("The comic's own HUD, printed on the page: a canvas the page camera draws into the texture.")]
    [SerializeField] private Canvas _pageHud;
    [SerializeField] private GameObject _hero;
    [Tooltip("Roshan's hand and the torch (switched on after Max).")]
    [SerializeField] private GameTorch _hand;
    [SerializeField] private MomDirector _mom;
    [SerializeField] private Finale _finale;
    [SerializeField] private GameMusic _music;
    [SerializeField] private PageManager _pages;

    private void Start()
    {
        UIKit.EnsureEventSystem();
        var A = GameAssets.I;
        if (A == null || A.max == null)
        {
            Debug.LogError("[Game] Missing Resources/Game/GameAssets: run Past Bedtime > Build Game Assets.");
            return;
        }

        var gs = On(_state);
        On(_light);
        var torch = On(_torch);
        // the stars in hand and the Nightstand's upgrades carry over from earlier sittings
        gs.StarsTotal = Settings.Stars;
        gs.Spring = Settings.Spring;
        gs.Gear = Settings.Gear;
        gs.Ratchet = Settings.Ratchet;
        torch.Charge.ApplyUpgrades(gs.Spring, gs.Gear, gs.Ratchet);
        var pageCam = On(_pageCam);
        On(_key);

        // the scene's camera only clears the frame (to the bedroom dark) and draws the overlay UI; the comic itself
        // is drawn once, by the page camera, into the page texture
        var view = On(_view);
        view.Show(pageCam.Texture);
        _pageHud.worldCamera = pageCam.Cam;
        On(_pageHud);
        _hero.SetActive(true);
        On(_hand).Hook();
        On(_mom);
        On(_finale);
        On(_music);

        // from the title: the cover swings open and the view zooms in (the page's title card waits
        // for it); otherwise straight in
        if (OpenFromTitle) StartCoroutine(view.Open());
        else view.SkipOpening();
        OpenFromTitle = false;

        On(_pages).Load(Mathf.Clamp(StartPage, 1, PageManager.LastPage));
    }

    private static T On<T>(T part) where T : Component
    {
        part.gameObject.SetActive(true);
        return part;
    }

    /// <summary>Set by the title as it hands over: the game starts in its framing and opens the comic.</summary>
    public static bool OpenFromTitle;
}
