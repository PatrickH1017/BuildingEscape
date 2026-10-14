using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildingEscape.Net
{
    /// <summary>Boot scene: the persistent Net object has woken up, so continue to the menu.</summary>
    public class BootLoader : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;
            SceneManager.LoadScene(NetConst.MenuScene);
        }
    }
}