using System.Collections;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>Play Mode: visits every level and counts magenta in pictures of it (overview + three eye-level views).</summary>
    public partial class NetDevRunner
    {
        IEnumerator FloorsScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            var top = new GameObject("FloorsTop").AddComponent<Camera>();
            top.clearFlags = CameraClearFlags.SolidColor; top.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            top.orthographic = true; top.enabled = false;
            var eye = new GameObject("FloorsEye").AddComponent<Camera>();
            eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = new Color(0.2f, 0.22f, 0.26f);
            eye.fieldOfView = 70f; eye.enabled = false;
            int fails = 0, shots = 0;
            foreach (int floor in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 })
            {
                TowerManager.Instance.GoToLevel(floor);
                yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == floor);
                yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == floor, 90, $"floor {floor}");
                yield return new WaitForSeconds(1.2f);
                var info = TowerManager.Instance.CurrentInfo;
                float cs = gen.cellSize;
                var centre = new Vector3(info.cols * cs / 2f, 0f, info.rows * cs / 2f);
                top.transform.position = centre + Vector3.up * 150f;
                top.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                top.orthographicSize = Mathf.Max(info.cols * cs / 2f / 1.7f, info.rows * cs / 2f) + 6f;
                ShotCam(top, $"floor{floor}_top"); shots++;
                // three eye-level views from corners of different rooms
                for (int k = 0; k < 3; k++)
                {
                    var cell = new Vector2Int(Mathf.Min(info.cols - 1, 1 + k * 2), Mathf.Min(info.rows - 1, k % info.rows));
                    var rc = new Vector3((cell.x + 0.5f) * cs, 0f, (cell.y + 0.5f) * cs);
                    eye.transform.position = rc + new Vector3(-7.4f, 2.4f, -7.4f);
                    eye.transform.LookAt(rc + new Vector3(2f, 0.7f, 2f));
                    yield return null;
                    ShotCam(eye, $"floor{floor}_eye{k}"); shots++;
                }
                Log($"[Floors] floor {floor} ok ({info.cols}x{info.rows} cells)");
            }
            Log($"[Floors] DONE shots={shots}");
            Quit(0);
        }
    }
}