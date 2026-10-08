using UnityEngine;

namespace BallClash
{
    /// <summary>Creates the self-contained duel screen in any scene, including the supplied HDRP template scene.</summary>
    public static class BallClashBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Object.FindFirstObjectByType<BallClashGame>() != null) return;
            var root = new GameObject("Ball Clash — Ricochet Duel");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<BallClashGame>();
        }
    }
}
