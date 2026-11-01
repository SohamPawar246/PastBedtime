using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The comic's HUD (GDD section 13), printed on the page itself: it lives on a canvas the page
/// camera draws into the page texture, so it lies on the open comic and tilts with it. Max's
/// hearts and the Splash meter in a caption box top-left, the stars top-right, the folio
/// ("PAGE 1 / 9") bottom-centre, Max's speech bubbles, the dotted ring where the beam will land
/// while the torch is off, and the cards ("TO BE CONTINUED...", "PAGE CLEARED"). A heart that's
/// lost pops and the box shakes; a star picked up flies up into the counter and bumps it. The reader's
/// things are real objects in the room: the torch in Roshan's hand (<see cref="GameTorch"/>) and
/// Mom's door (<see cref="MomDoorView"/>).
/// </summary>
public class GameHUD : MonoBehaviour
{
    public static GameHUD I { get; private set; }

    [SerializeField] private RectTransform _page;
    [SerializeField] private Image[] _hearts;
    // hearts popping as they're lost (or won back), the box shaking; stars flying up into their counter
    [SerializeField] private float[] _heartPop;
    [SerializeField] private bool[] _heartLost;
    private int _heartsShown = -1;
    [SerializeField] private RectTransform _heartsBox, _starsBox, _starIcon;
    [SerializeField] private Vector2 _heartsHome;
    private float _heartsShake, _starsBump;
    private int _starsInFlight;
    [SerializeField] private Image _splashFill;
    [SerializeField] private TextMeshProUGUI _splashHint, _stars, _folio;
    [SerializeField] private Image _ring;
    [SerializeField] private CanvasGroup _cardGroup;
    [SerializeField] private TextMeshProUGUI _cardText;
    [SerializeField] private RectTransform _countBox, _bossBox;
    [SerializeField] private TextMeshProUGUI _countText;
    [SerializeField] private Image _bossFill;
    private float _countdown, _bossLook;
    private BlotBrain _boss;
    [SerializeField] private RectTransform _bubble, _bubbleTail;
    private bool _bubbleLeft;
    [SerializeField] private TextMeshProUGUI _bubbleText;
    private float _bubbleFor;

    private void Awake() => I = this;
    private void OnDestroy() { if (I == this) I = null; }

    /// <summary>The finale's room-light countdown (0 hides it).</summary>
    public void Countdown(float seconds) => _countdown = seconds;

    private void BossBar()
    {
        if ((_bossLook -= Time.unscaledDeltaTime) <= 0f)
        {
            _bossLook = 0.5f;
            _boss = FindFirstObjectByType<BlotBrain>();
        }
        var hero = HeroController.I;
        bool show = _boss != null && hero != null && _boss.Mode != EnemyBrain.State.Dead && !_boss.Escaping &&
                    hero.transform.position.x > _boss.MinX - 0.5f && hero.transform.position.x < _boss.MaxX + 0.5f &&
                    Mathf.Abs(hero.transform.position.y - _boss.transform.position.y) < 8f;           // his tier, not one above
        _bossBox.gameObject.SetActive(show);
        if (show) _bossFill.fillAmount = _boss.Health.hp / BlotBrain.MaxHp;
    }

    private void Update()
    {
        if (_page == null) return;
        _countBox.gameObject.SetActive(_countdown > 0f);
        if (_countdown > 0f) _countText.text = $"ROOM LIGHT ON: {Mathf.CeilToInt(_countdown)}";
        BossBar();
        TeachTick();
        var hero = HeroController.I;
        var gs = GameState.I;
        if (hero != null) Hearts(hero.GetComponent<Health>());
        if (gs != null)
        {
            _splashFill.fillAmount = gs.Splash / 100f;
            _splashHint.text = gs.Splash >= 100f ? (gs.Page >= 5 ? Bindings.Format("PRESS {SPLASH}: SPLASH PAGE!") : "SPLASH METER FULL") : "";
            // a star still on its way up isn't counted until it lands in the box
            int flying = Mathf.Min(_starsInFlight, gs.StarsThisPage);
            _stars.text = $"{gs.StarsThisPage - flying}/5  <size=70%>({gs.StarsTotal - flying})</size>";
            _folio.text = $"PAGE {gs.Page} / {PageManager.LastPage}";
        }
        if (_starsBump > 0f)                                           // (only while it moves: the canvas rebuilds on a change)
        {
            _starsBump = Mathf.Max(0f, _starsBump - Time.unscaledDeltaTime / 0.28f);
            float bump = _starsBump * _starsBump;
            _starsBox.localScale = Vector3.one * (1f + 0.28f * bump);
        }

        var torch = TorchController.I;
        if (torch != null)
        {
            Color lens = torch.Lenses.Current == Lens.Ghost ? Palette.LensGhost : Palette.LensClear;

            // the dotted ring: where the beam lands when you switch on
            bool off = !torch.On && PageView.I != null && (PageManager.I == null || !PageManager.I.Busy);
            _ring.enabled = off;
            if (off)
            {
                // the beam's size on the page, so the player can line up exactly what it will wake
                float radius = LightField.I != null && LightField.I.BeamRadius > 0.1f ? LightField.I.BeamRadius : 4f;
                _ring.rectTransform.sizeDelta = Vector2.one * (2f * radius * _page.rect.width / PageCamera.ViewWidth);
                _ring.rectTransform.anchoredPosition = ToHolder(torch.Aim);
                _ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 12f);
                _ring.color = lens.Alpha(0.75f);
            }
        }

        if (_bubble.gameObject.activeSelf)
        {
            // words only exist while read: the line waits, unread, while Max is in the dark
            bool lit = hero != null && hero.Light.IsAwake;
            if (lit) _bubbleFor -= Time.deltaTime;
            bool show = lit && _bubbleFor > 0f;
            _bubble.GetComponent<CanvasGroup>().alpha = show ? 1f : 0f;
            if (hero != null)
            {
                Vector2 feet = hero.transform.position;
                // off the edge of the page on this side? hang it on the other
                if (!BubbleFits(feet, _bubbleLeft) && BubbleFits(feet, !_bubbleLeft)) SetBubbleSide(!_bubbleLeft);
                _bubble.anchoredPosition = ToHolder(feet + new Vector2(_bubbleLeft ? -BubbleAt.x : BubbleAt.x, BubbleAt.y));
            }
            if (_bubbleFor <= 0f) _bubble.gameObject.SetActive(false);
        }
    }

    /// <summary>The hearts: red while he has them. One that's lost flashes white and pops as it empties, and the box
    /// shakes; one won back pops in red.</summary>
    private void Hearts(Health h)
    {
        int now = Mathf.Clamp(Mathf.CeilToInt(h.hp), 0, _hearts.Length);
        if (_heartsShown >= 0 && now != _heartsShown)
        {
            for (int i = Mathf.Min(now, _heartsShown); i < Mathf.Max(now, _heartsShown); i++)
            {
                _heartPop[i] = 1f;
                _heartLost[i] = now < _heartsShown;
            }
            if (now < _heartsShown) _heartsShake = 1f;
        }
        _heartsShown = now;
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < _hearts.Length; i++)
        {
            Color c = i < now ? Palette.HeroRed : Palette.Ink.Alpha(0.25f);
            float k = _heartPop[i];
            if (k > 0f)
            {
                _heartPop[i] = Mathf.Max(0f, k - dt / (_heartLost[i] ? 0.4f : 0.3f));
                if (_heartLost[i] && k > 0.82f) c = Palette.Paper;                // the flash of the blow
                else if (_heartLost[i]) c = Color.Lerp(c, Palette.HeroRed, k * 0.8f);
                // (only while it moves: the canvas rebuilds on a change)
                float pop = _heartPop[i];
                _hearts[i].rectTransform.localScale = Vector3.one * (1f + (_heartLost[i] ? 0.75f : 0.4f) * pop * pop);
                _hearts[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, _heartLost[i] ? 18f * pop * Mathf.Sin(pop * 30f) : 0f);
            }
            _hearts[i].color = c;
        }
        if (_heartsShake > 0f)
        {
            _heartsShake = Mathf.Max(0f, _heartsShake - dt / 0.3f);
            float a = 7f * _heartsShake * _heartsShake;
            _heartsBox.anchoredPosition = _heartsHome + (_heartsShake > 0f ? new Vector2(Random.Range(-a, a), Random.Range(-a, a)) : Vector2.zero);
        }
    }

    /// <summary>A star picked up at `lane` flies up into the stars box, which bumps as it lands (and only then counts it).</summary>
    public void StarFound(Vector2 lane)
    {
        if (_page == null) return;
        _starsInFlight++;
        StartCoroutine(StarFlight(ToHolder(lane)));
    }

    private IEnumerator StarFlight(Vector2 from)
    {
        var fly = Instantiate(_flyingStar, _page);
        fly.name = "FlyingStar";
        int page = GameState.I != null ? GameState.I.Page : 0;
        const float seconds = 0.55f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            if (fly == null || (GameState.I != null && GameState.I.Page != page)) break;   // the page turned under it
            float k = t / seconds;
            Vector2 to = (Vector2)_page.InverseTransformPoint(_starIcon.position);
            // up and over in an arc, quick off the page and easing into the box
            float e = 1f - Mathf.Pow(1f - k, 2.2f);
            Vector2 mid = Vector2.Lerp(from, to, 0.35f) + new Vector2(0f, 160f);
            Vector2 p = Vector2.Lerp(Vector2.Lerp(from, mid, e), Vector2.Lerp(mid, to, e), e);
            fly.anchoredPosition = p;
            fly.localScale = Vector3.one * Mathf.Lerp(1.35f, 0.8f, e);
            fly.localRotation = Quaternion.Euler(0f, 0f, -300f * e);
            yield return null;
        }
        if (fly != null) Destroy(fly.gameObject);
        _starsInFlight = Mathf.Max(0, _starsInFlight - 1);
        _starsBump = 1f;
        GameAudio.Play("ping", 0.28f, 0.03f, 1.35f);
    }

    /// <summary>A lane point to anchored coordinates in the page holder.</summary>
    private Vector2 ToHolder(Vector2 lane)
    {
        var uv = PageCamera.I != null ? PageCamera.I.LaneToViewport(lane) : new Vector2(0.5f, 0.5f);
        var r = _page.rect;
        return new Vector2((uv.x - 0.5f) * r.width, (uv.y - 0.5f) * r.height);
    }

    public void HeroSays(string text, float seconds)
    {
        text = Bindings.Format(text);                   // "{FOLLOW}" says what the player has bound
        _bubbleText.text = text;
        // long enough to read: a beat, plus about fifteen letters a second
        _bubbleFor = Mathf.Max(seconds, 2.4f + text.Length * 0.07f);
        _bubble.gameObject.SetActive(true);
        if (_bubble.GetComponent<CanvasGroup>() == null) _bubble.gameObject.AddComponent<CanvasGroup>();
        _bubble.anchorMin = _bubble.anchorMax = new Vector2(0.5f, 0.5f);
        // the balloon hangs on the side Max isn't facing: his fight (and its POW!s) is the other way
        var hero = HeroController.I;
        SetBubbleSide(hero != null && hero.Facing > 0f);
    }

    /// <summary>Takes Max's line back if it's still up (or still waiting to be read).</summary>
    public void Unsay(string text)
    {
        if (_bubble != null && _bubble.gameObject.activeSelf && _bubbleText.text == Bindings.Format(text)) _bubbleFor = 0f;
    }

    private void SetBubbleSide(bool left)
    {
        _bubbleLeft = left;
        _bubble.pivot = new Vector2(left ? 0.85f : 0.15f, 0f);
        _bubbleTail.anchoredPosition = new Vector2(left ? 150f : -150f, -52f);
        _bubbleTail.localRotation = Quaternion.Euler(0f, 0f, left ? 190f : 170f);
    }

    /// <summary>Would the balloon, on this side of Max, stay on the page?</summary>
    private bool BubbleFits(Vector2 feet, bool left)
    {
        var at = ToHolder(feet + new Vector2(left ? -BubbleAt.x : BubbleAt.x, BubbleAt.y));
        float w = _bubble.sizeDelta.x, half = _page.rect.width * 0.5f - 8f;
        float x0 = at.x - (left ? 0.85f : 0.15f) * w;
        return x0 >= -half && x0 + w <= half;
    }

    private static readonly Vector2 BubbleAt = new(0.4f, 2.7f);       // from Max's feet (mirrored on the left)

    /// <summary>Where Max's balloon is on the lane while it's showing, so lettering can keep clear of it.</summary>
    public bool BubbleLane(out Rect lane)
    {
        lane = default;
        var hero = HeroController.I;
        if (_bubble == null || !_bubble.gameObject.activeSelf || _bubbleFor <= 0f || hero == null || !hero.Light.IsAwake) return false;
        float perUnit = _page.rect.width / PageCamera.ViewWidth;
        Vector2 size = _bubble.sizeDelta / perUnit;
        Vector2 at = (Vector2)hero.transform.position + new Vector2(_bubbleLeft ? -BubbleAt.x : BubbleAt.x, BubbleAt.y);
        lane = new Rect(at.x - _bubble.pivot.x * size.x, at.y, size.x, size.y);
        return true;
    }

    public void PageStart(PageDef def)
    {
        _starsInFlight = 0;                                            // a new page: nothing's still on its way up
        StartCoroutine(TitleCard(def));
    }

    private string _act;

    private IEnumerator TitleCard(PageDef def)
    {
        var hero = HeroController.I;
        if (hero != null) hero.Locked = true;                // nobody walks blind under the card
        while (PageView.I != null && PageView.I.Opening) yield return null;   // the comic opens first
        if (!string.IsNullOrEmpty(def.act) && def.act != _act)
        {
            // a new act opens on a splash with the chapter title (GDD section 13)
            _act = def.act;
            int colon = def.act.IndexOf(':');
            string part = colon > 0 ? def.act.Substring(0, colon) : def.act;
            string name = colon > 0 ? def.act.Substring(colon + 1).Trim() : "";
            _cardText.text = $"<size=50%>{part}</size>\n{name}";
            yield return FadeCard(1f, 0.2f);
            yield return new WaitForSecondsRealtime(1.6f);
            yield return FadeCard(0f, 0.2f);
        }
        _cardText.text = $"PAGE {def.number}\n<size=55%>{def.title.ToUpperInvariant()}</size>";
        yield return FadeCard(1f, 0.15f);
        yield return new WaitForSecondsRealtime(1.2f);
        if (hero != null) hero.Locked = false;
        yield return FadeCard(0f, 0.3f);
    }

    public IEnumerator Card(string text, float seconds)
    {
        _cardText.text = text;
        yield return FadeCard(1f, 0.2f);
        yield return BookmarkPause.Wait(seconds);                     // a card stays put while the game is bookmarked
        yield return FadeCard(0f, 0.25f);
    }

    public IEnumerator PageCleared(PageDef def)
    {
        var gs = GameState.I;
        // the style counter, for this page (GDD section 5): Inkie-on-Inkie hits, Mom's visits survived, stars
        string style = gs != null
            ? $"\n<size=40%>CHOREOGRAPHY {gs.ChoreographyThisPage}   LIGHTS OUT {gs.LightsOutThisPage}   STARS {gs.StarsThisPage}/5</size>" : "";
        string lens = def.unlockLens >= 0 ? $"\n<size=40%>NEW LENS: {LensWheel.Name((Lens)def.unlockLens)} ({Bindings.Name(Bindings.Act.Lens)})</size>" : "";
        yield return Card($"PAGE {def.number} DONE!{style}{lens}", 2.4f);
    }

    private IEnumerator FadeCard(float to, float seconds)
    {
        float from = _cardGroup.alpha;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            _cardGroup.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        _cardGroup.alpha = to;
    }

    /// <summary>A tutorial or warning caption across the top of the page (no pause).</summary>
    public void Warn(string text) => StartCoroutine(Banner(Bindings.Format(text), Palette.Yellow, Palette.Ink, 2.6f));

    // ---- a tutorial step: an instruction that stays up until it's done ------------------------------------------

    [Tooltip("The tutorial box between the hearts and the stars (switched off till the first lesson).")]
    [SerializeField] private RectTransform _teach;
    [SerializeField] private TextMeshProUGUI _teachText;
    [SerializeField] private CanvasGroup _teachGroup;
    private string _teachRaw;

    [Header("Popped onto the page (Art/UI)")]
    [SerializeField] private RectTransform _flyingStar;
    [Tooltip("A caption across the top of the page; it's sized to its words and coloured as it's put up.")]
    [SerializeField] private RectTransform _banner;

    /// <summary>Puts up a tutorial instruction at the top of the page that stays until it's done (null takes it
    /// down). "{TORCH}" and the like say the player's own controls.</summary>
    public void Teach(string text)
    {
        if (text == _teachRaw) return;
        _teachRaw = text;
        if (!_teach.gameObject.activeSelf)
        {
            _teach.gameObject.SetActive(true);
            _teach.SetAsLastSibling();                       // over whatever's printed on the page already
        }
        if (text != null)
        {
            _teachText.text = Bindings.Format(text);
            _teach.localScale = Vector3.one * 1.12f;            // each new step lands with a little stamp
        }
    }

    private void TeachTick()
    {
        if (!_teach.gameObject.activeSelf) return;
        float dt = Time.unscaledDeltaTime;
        _teachGroup.alpha = Mathf.MoveTowards(_teachGroup.alpha, _teachRaw != null ? 1f : 0f, dt * 5f);
        _teach.localScale = Vector3.one * Mathf.MoveTowards(_teach.localScale.x, 1f, dt * 1.2f);
    }

    /// <summary>Mom's line through the door: a real-world voice, so it's spoken at the door, not printed on the page.</summary>
    public void MomSays(string text)
    {
        if (MomDoorView.I != null) MomDoorView.I.Say(text);
        else StartCoroutine(Banner(text, Palette.Ink, Palette.Paper, 1.6f));
    }

    private IEnumerator Banner(string text, Color face, Color ink, float seconds)
    {
        var root = Instantiate(_banner, _page);
        root.name = "Banner";
        root.sizeDelta = new Vector2(Mathf.Min(900f, 30f * text.Length + 80f), 70f);
        root.Find("Face").GetComponent<Image>().color = face;
        var t = root.Find("Face/Text").GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.color = ink;
        var group = root.GetComponent<CanvasGroup>();
        for (float a = 0f; a < seconds; a += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Min(1f, a / 0.15f, (seconds - a) / 0.3f);
            root.localScale = Vector3.one * (a < 0.15f ? Mathf.Lerp(0.7f, 1f, a / 0.15f) : 1f);
            yield return null;
        }
        Destroy(root.gameObject);
    }

    /// <summary>The Splash Page: one that strikes anyone is its own splash (ComicFx); one that strikes nobody gets
    /// a card saying so.</summary>
    public void SplashPage(int struck)
    {
        if (struck == 0) StartCoroutine(Card("...MISSED!", 0.8f));
    }
}
