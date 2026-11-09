using TMPro;
using UnityEngine;

/// <summary>
/// Sahadaki dev skor tabelasının canlı yazısı: oyuncu, delikteki top ve kalan süre.
/// </summary>
public class SkorPanosu : MonoBehaviour
{
    public TMP_Text text;
    public string playerName = "MERT DEMİR";

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || text == null) return;
        int sec = Mathf.CeilToInt(gm.TimeLeft);
        string state = gm.IsOver ? (gm.Won ? "<color=#F6C945>KAZANDI!</color>" : "<color=#FF7A6B>SÜRE DOLDU</color>") : "OYUNDA";
        text.text =
            "OYUNCU     <color=#FFFFFF>" + playerName + "</color>\n" +
            "DELİK       <color=#F6C945>" + gm.Scored + " / " + gm.totalBalls + "</color>\n" +
            "SÜRE         <color=#FFFFFF>" + sec + " sn</color>\n" +
            "DURUM      " + state;
    }
}
