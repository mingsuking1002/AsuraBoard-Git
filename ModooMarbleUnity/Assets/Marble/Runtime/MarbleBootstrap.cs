using UnityEngine;

namespace Marble.Unity
{
    public static class MarbleBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindFirstObjectByType<MarbleApp>() == null)
                new GameObject("Marble • Game").AddComponent<MarbleApp>();
        }
    }
}
