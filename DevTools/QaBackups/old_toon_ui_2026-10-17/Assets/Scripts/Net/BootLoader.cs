using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildingEscape.Net
{
    /// <summary>Boot scene: the persistent Net object has woken up, so continue to the menu.</summary>
    public class BootLoader : MonoBehaviour
    {
        /// <summary>
        /// A networked game must keep running when its window is not in front: the project's own setting is "off", so a host who alt-tabs
        /// (to chat, to Steam) would freeze the whole team, and voice chat would stop. Set before the first scene loads, so it also holds
        /// when a scene other than Boot is played in the Editor.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void KeepRunningInTheBackground() => Application.runInBackground = true;

        IEnumerator Start()
        {
            yield return null;
            SceneManager.LoadScene(NetConst.MenuScene);
        }
    }
}