// Starts the game in whatever scene is open: press Play in the editor, or run a build.
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using UnityEngine;

namespace FlappyCrix
{
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindObjectOfType<GameRoot>() != null || Object.FindObjectOfType<WebShell>() != null) return;
            // any camera the scene came with is replaced by the game's own
            foreach (var c in Object.FindObjectsOfType<Camera>()) c.gameObject.SetActive(false);
            // phones and tablets run the real website inside the app (WebShell.cs); computers the Unity game
            if (WebShell.Wanted) new GameObject("Flappy Crix").AddComponent<WebShell>();
            else new GameObject("Flappy Crix").AddComponent<GameRoot>();
        }
    }
}
