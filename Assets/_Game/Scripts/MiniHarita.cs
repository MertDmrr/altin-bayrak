using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sağ alttaki mini harita: oyuncu (altın ok), toplar (beyaz), boş delikler (sarı bayrak),
/// dolu delikler (soluk) ve robot çim biçiciler (turuncu). Kuzey yukarıdadır (kamera da kuzeye bakar).
/// </summary>
public class MiniHarita : MonoBehaviour
{
    public RectTransform area;            // ikonların çizildiği alan
    public Sprite dotSprite;
    public Sprite arrowSprite;
    public Sprite flagSprite;
    public float worldHalfSize = 34f;     // haritanın kapsadığı alan (merkezden ± metre)

    struct Icon { public Transform target; public RectTransform rt; public Image img; public int kind; public Component data; }
    readonly List<Icon> icons = new List<Icon>();
    RectTransform playerIcon;
    Transform player;

    static readonly Color BallCol = Color.white;
    static readonly Color HoleCol = new Color(0.98f, 0.80f, 0.27f);
    static readonly Color HoleDone = new Color(0.98f, 0.80f, 0.27f, 0.28f);
    static readonly Color RobotCol = new Color(1f, 0.55f, 0.15f);

    void Start()
    {
        foreach (var h in FindObjectsByType<HoleSocket>(FindObjectsSortMode.None)) Add(h.transform, flagSprite, 26f, HoleCol, 0, h);
        foreach (var r in FindObjectsByType<CubeChaser>(FindObjectsSortMode.None)) Add(r.transform, dotSprite, 16f, RobotCol, 2, null);
        foreach (var b in FindObjectsByType<Ball>(FindObjectsSortMode.None)) Add(b.transform, dotSprite, 14f, BallCol, 1, b);
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc)
        {
            player = pc.transform;
            playerIcon = MakeImage("Oyuncu", arrowSprite, 26f, new Color(1f, 0.85f, 0.3f)).rectTransform;
        }
    }

    void Add(Transform t, Sprite sp, float size, Color c, int kind, Component data)
    {
        var img = MakeImage(t.name, sp, size, c);
        icons.Add(new Icon { target = t, rt = img.rectTransform, img = img, kind = kind, data = data });
    }

    Image MakeImage(string name, Sprite sp, float size, Color c)
    {
        var go = new GameObject("İkon " + name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(area, false);
        var img = go.GetComponent<Image>();
        img.sprite = sp;
        img.color = c;
        img.raycastTarget = false;
        img.preserveAspect = true;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
        return img;
    }

    Vector2 ToMap(Vector3 w)
    {
        Vector2 half = area.rect.size * 0.5f;
        var p = new Vector2(w.x / worldHalfSize * half.x, w.z / worldHalfSize * half.y);
        p.x = Mathf.Clamp(p.x, -half.x + 6f, half.x - 6f);
        p.y = Mathf.Clamp(p.y, -half.y + 6f, half.y - 6f);
        return p;
    }

    void LateUpdate()
    {
        foreach (var ic in icons)
        {
            if (ic.target == null) continue;
            ic.rt.anchoredPosition = ToMap(ic.target.position);
            if (ic.kind == 0)
            {
                var hs = (HoleSocket)ic.data;
                ic.img.color = hs.IsFilled ? HoleDone : HoleCol;
            }
            else if (ic.kind == 1)
            {
                var b = (Ball)ic.data;
                ic.img.enabled = !b.scored;
            }
        }
        if (playerIcon && player)
        {
            playerIcon.anchoredPosition = ToMap(player.position);
            playerIcon.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
            playerIcon.SetAsLastSibling();
        }
    }
}
