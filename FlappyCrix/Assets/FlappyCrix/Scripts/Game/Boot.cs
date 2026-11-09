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
            if (Object.FindObjectOfType<GameRoot>() != null) return;
            // any camera the scene came with is replaced by the game's own
            foreach (var c in Object.FindObjectsOfType<Camera>()) c.gameObject.SetActive(false);
            new GameObject("Flappy Crix").AddComponent<GameRoot>();
        }
    }
}
