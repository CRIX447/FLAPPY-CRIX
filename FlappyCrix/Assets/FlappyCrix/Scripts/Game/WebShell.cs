// Phones and tablets: the app IS the website game, so it's an exact copy that's never behind.
// After the CRIX STUDIOS screen, the real Flappy Crix site opens full screen inside the app
// (gree/unity-webview, zlib licence, installed through Packages/manifest.json). Every website
// update reaches the app straight away. Your own sites stay inside the app; anything else
// (Discord invites, YouTube...) opens in the phone's browser.
//   Android back button: back a page, or press it twice to leave.
//   No connection: a "can't reach the servers" screen that tries again.
// Computers keep the Unity game (HD) and the launcher's web version.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using UnityEngine;
using UnityEngine.UI;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using Gree.UnityWebView;
#endif

namespace FlappyCrix
{
    public sealed class WebShell : MonoBehaviour
    {
        /// <summary>Phones and tablets run the website inside the app.</summary>
        public static bool Wanted
        {
            get
            {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        const string Site = "https://flappycrix.vercel.app/";
        // these load inside the app: the game's sites and the sign-in pages they use
        const string Inside = @"^https?://((((www\.)?flappycrix\.io|flappycrix\.vercel\.app|(www\.)?crixgamingvr\.com|app\.crixgamingvr\.com|[a-z0-9-]+\.firebaseapp\.com|api\.itisnow\.net)([/:?#]|$))|discord\.com/(api/)?(oauth2|login)|discordapp\.com/(api/)?oauth2)";

        Text status;
        GameObject cover;
        float shownAt, backAt = -9;
        bool loaded;
        Rect lastSafe;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        WebViewObject view;
#endif

        void Start()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Ui.Ink;

            // the CRIX STUDIOS screen while the site loads
            var canvasGo = new GameObject("Cover", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            cover = canvasGo;
            var bg = new GameObject("Bg", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(canvasGo.transform, false);
            bg.color = Color.black;      // the splash image is on black
            Ui.Fill(bg.rectTransform);
            var logoTex = Resources.Load<Texture2D>(Res.Root + "Images/crix-studios");   // your CRIX STUDIOS / Made with Unity splash
            if (logoTex != null)
            {
                var logo = new GameObject("Logo", typeof(RectTransform)).AddComponent<RawImage>();
                logo.transform.SetParent(canvasGo.transform, false);
                logo.texture = logoTex;
                float a = logoTex.width / (float)logoTex.height;
                Ui.Place(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 1000 / a));
            }
            var font = Resources.Load<Font>(Res.Root + "font");
            var st = new GameObject("Status", typeof(RectTransform)).AddComponent<Text>();
            st.transform.SetParent(canvasGo.transform, false);
            st.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            st.fontSize = 40; st.alignment = TextAnchor.MiddleCenter; st.color = new Color(1, 1, 1, 0.7f);
            st.horizontalOverflow = HorizontalWrapMode.Wrap;
            Ui.Place(st.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 300));
            st.text = "LOADING FLAPPY CRIX…";
            status = st;
            var tap = canvasGo.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.targetGraphic = bg;
            tap.onClick.AddListener(() => { if (!loaded) Load(); });
            shownAt = Time.time;

            Open();
        }

        void Open()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            view = new GameObject("Website").AddComponent<WebViewObject>();
            view.transform.SetParent(transform, false);
            view.Init(
                cb: msg => { },
                err: msg => Offline(msg),
                httpErr: msg => { if (!loaded) Offline("the server answered " + msg); },
                started: url => { },
                hooked: url => Application.OpenURL(url),
                ld: url =>
                {
                    loaded = true;
                    // lets the site know it's inside the app
                    view.EvaluateJS("window.CRIX_APP={platform:'" + (Application.platform == RuntimePlatform.IPhonePlayer ? "ios" : "android") +
                                    "',version:'" + Application.version + "'};try{sessionStorage.setItem('crix_app','1')}catch(e){}");
                    if (cover != null) cover.SetActive(false);
                    view.SetVisibility(true);
                },
                transparent: false,
                zoom: false,
                androidForceDarkMode: 1,
                enableWKWebView: true,
                wkAllowsBackForwardNavigationGestures: false);
            // everything that isn't the game opens in the phone's browser instead
            view.SetURLPattern("", "", "^(?!" + Inside.Substring(1) + ")https?://");
            Fit();
            view.SetVisibility(false);
            Load();
#else
            status.text = "THE WEBSITE APP RUNS ON PHONES AND TABLETS.";
#endif
        }

        void Load()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            status.text = "LOADING FLAPPY CRIX…";
            if (cover != null) cover.SetActive(true);
            view.LoadURL(Site + "?app=unity");
#endif
        }

        void Offline(string why)
        {
            loaded = false;
            if (cover != null) cover.SetActive(true);
            status.text = "CAN'T REACH THE SERVERS\n<size=28>Check your connection. Tap to try again.</size>";
            status.supportRichText = true;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (view != null) view.SetVisibility(false);
#endif
            Debug.Log("[FlappyCrix] website: " + why);
        }

        /// <summary>Keeps the site inside the phone's safe area (notches, rounded corners, home bar).</summary>
        void Fit()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            var s = Screen.safeArea;
            lastSafe = s;
            view.SetMargins((int)s.xMin, (int)(Screen.height - s.yMax), (int)(Screen.width - s.xMax), (int)s.yMin);
#endif
        }

        void Update()
        {
            if (Screen.safeArea != lastSafe) Fit();
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android back: a page back, or twice quickly to leave
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (view != null && view.CanGoBack()) view.GoBack();
                else if (Time.unscaledTime - backAt < 2f) Application.Quit();
                else backAt = Time.unscaledTime;
            }
#endif
            // still nothing after 25 s: say so (a very slow or missing connection)
            if (!loaded && Time.time - shownAt > 25 && status.text.StartsWith("LOADING")) Offline("timed out");
        }

        void OnApplicationPause(bool paused)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            // coming back after a long time with no page: try again
            if (!paused && !loaded && view != null) Load();
#endif
        }
    }
}
