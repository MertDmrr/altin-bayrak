#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Geliştirme testi: Play modunda klavyeyi taklit eden basit bir otomatik oyuncu.
/// 60 saniyenin yeterli olup olmadığını ölçmek için kullanılır (oyuna dahil değildir).
/// </summary>
public static class OtoPilotTest
{
    static Ball target;
    static HoleSocket hole;
    static float stuckTimer;
    static Vector3 lastPos;

    [MenuItem("Oyuncak Rüyası/Test/Oto Pilotu Başlat (Play modunda)")]
    public static void Begin()
    {
        var s = InputSystem.settings;
        s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Application.runInBackground = true;
        target = null; hole = null;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log("[OtoPilot] Başladı");
    }

    public static void End()
    {
        EditorApplication.update -= Tick;
        if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        var s = InputSystem.settings;
        s.backgroundBehavior = InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
        s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { End(); return; }
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.IsOver)
        {
            Debug.Log($"[OtoPilot] Bitti: kazandi={gm.Won} skor={gm.Scored}/10 kullanilan_sure={gm.gameDuration - gm.TimeLeft:0.0} sn");
            End();
            return;
        }
        var pc = Object.FindFirstObjectByType<PlayerController>();
        var sp = Object.FindFirstObjectByType<BallSpawner>();
        if (pc == null || sp == null) return;
        Vector3 pp = pc.transform.position; pp.y = 0f;

        if (target == null || target.scored)
        {
            target = null; float best = 1e9f;
            foreach (var b in sp.balls)
            {
                if (b.scored) continue;
                float d = Flat(b.transform.position - pp);
                if (d < best) { best = d; target = b; }
            }
            hole = null;
        }
        if (target == null) { Press(Vector3.zero); return; }
        Vector3 bp = target.transform.position; bp.y = 0f;
        if (hole == null || hole.IsFilled)
        {
            float best = 1e9f;
            foreach (var h in sp.holes)
            {
                if (h.IsFilled) continue;
                float d = Flat(h.transform.position - bp);
                if (d < best) { best = d; hole = h; }
            }
        }
        Vector3 hp = hole.transform.position; hp.y = 0f;
        Vector3 toHole = (hp - bp); toHole.y = 0f; toHole.Normalize();
        Vector3 approach = bp - toHole * 1.6f;
        Vector3 playerToBall = bp - pp;
        float dist = playerToBall.magnitude;
        float align = Vector3.Dot(playerToBall.normalized, toHole);

        Vector3 desired;
        if (align > 0.88f && dist < 2.8f)
            desired = (bp + toHole * 0.6f - pp).normalized;           // it
        else if (dist < 2.3f)
        {
            Vector3 side = Vector3.Cross(Vector3.up, toHole);
            if (Vector3.Dot(-playerToBall, side) < 0f) side = -side;
            desired = (side - toHole * 0.7f).normalized;               // topun etrafından dolaş
        }
        else
            desired = (approach - pp).normalized;                      // arkasına geç

        // Takılırsa (küp vb.) biraz yana kay
        if (Flat(pp - lastPos) < 0.01f) stuckTimer += Time.unscaledDeltaTime; else stuckTimer = 0f;
        lastPos = pp;
        if (stuckTimer > 0.6f) desired = Vector3.Cross(Vector3.up, desired);
        Press(desired);
    }

    static void Press(Vector3 dir)
    {
        var keys = new List<Key>();
        if (dir.sqrMagnitude > 0.01f)
        {
            float ang = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float s = Mathf.Round(ang / 45f) * 45f;
            Vector3 q = Quaternion.Euler(0f, s, 0f) * Vector3.forward;
            if (q.z > 0.3f) keys.Add(Key.W);
            if (q.z < -0.3f) keys.Add(Key.S);
            if (q.x > 0.3f) keys.Add(Key.D);
            if (q.x < -0.3f) keys.Add(Key.A);
        }
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys.ToArray()));
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
}
#endif
