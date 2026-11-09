# Altın Bayrak – 3B Açık Dünya Prototipi

**Hazırlayan:** Mert Demir – Öğrenci No: 2506127004
**Ders:** 3B Oyun Geliştirme Stüdyosu – Teslim: 11.11.2026

**Oyna (WebGL):** https://mertdmrr.github.io/oyuncak-ruyasi/

## Oyun
Yeşiltepe Golf Kulübü'nün Altın Bayrak Turnuvası: 60 saniye içinde sahaya dağılmış 10 dev golf topunu
numaralı 10 deliğe sür. Küp gövdeli robot çim biçiciler (hocanın `RandomCubeMovement` kodu) sahada dolaşır,
yaklaşınca seni kovalar ve çarparsa geri iter.

- **Kontroller:** W A S D / Yön tuşları: yürü · ESC / P: duraklat
- **Kazanma:** 60 saniye dolmadan 10 topun hepsi delikte
- **Kaybetme:** Süre dolar, turnuva kornası çalar

## Ödev yönergesi
- Hocanın T-pose karakter modeli (Blender'da iskelet + golfçü kıyafeti), Animator Controller: Idle ↔ Walk
- Üstten (top-down) üçüncü şahıs kamera, açık dünya (Unity Terrain 200×200 m)
- 10 küre, 10 delik (socket), fiziksel itme, 60 saniye
- Hocanın `RandomCubeMovement.cs` kodu değiştirilmeden kullanıldı (`Assets/_Game/Scripts/Hoca`)

## Teknik
- Unity 6 (6000.2.11f1), URP, Input System, TextMeshPro
- Sahneler editör aracıyla kurulur: **Altın Bayrak > Sahneleri Kur** (`Assets/_Game/Scripts/Editor/GolfKurucu.cs`)
- Blender modelleri: kulüp binası, golf arabası, altın kupa, skor tabelası, giriş kapısı, robot çim biçici,
  ağaçlar, teras masası, bank, kayalar + Mert'in Blender klasöründen geliştirilen 6 model
  (oyuncak kasaba, oyuncak araba, kakao kupası, karton figür, manken eli, el sallayan el)
- Dokular ve sesler Python ile özgün olarak üretildi
