# Oyuncak Rüyası

**Hazırlayan:** Mert Demir — Öğrenci No: 2506127004
**Ders:** 3B Oyun Geliştirme Stüdyosu
**Motor:** Unity 6 (6000.2.11f1), Universal Render Pipeline

**Tarayıcıda oyna (WebGL):** https://mertdmrr.github.io/oyuncak-ruyasi/

## Hikaye

Karakter bir gece rüyasında kendini dev oyuncaklarla dolu bir diyarda bulur ve uyurgezer gibi kolları önde yürür. Uyanmak için dev çalar saat 60 saniyeyi doldurmadan 10 topu bayraklı yuvalara yuvarlaması gerekir. Rüyadaki harf küpleri ise yaklaşınca onu kovalar.

## Nasıl oynanır

| Tuş | İşlev |
|---|---|
| W A S D / Yön tuşları | Yürü |
| ESC / P | Duraklat |

Topa doğru yürüyerek kollarınla it ve yuvalara düşür. Küpler sana çarparsa geri itilirsin. 60 saniye içinde 10 topun hepsini yerleştirirsen kazanırsın.

## Ödev gereksinimleri

- **T-Pose karakter:** Hocanın verdiği model. Blender'da humanoid iskelet kuruldu, Unity'de Humanoid avatar olarak ayarlandı.
- **Idle ve Walk:** Animator Controller (State Machine). Idle ↔ Walk geçişi `Speed` parametresine bağlı. Avatar Mask'li ikinci katman kolları öne sabitler (uyurgezer pozu).
- **Kamera:** Tepe açılı 3. şahıs kamera, SmoothDamp ile sarsıntısız takip (`TopDownCamera.cs`).
- **Açık dünya:** 200x200 m Unity Terrain. Terrain Holes ile 10 yuva açıldı.
- **10 küre:** Rigidbody + SphereCollider. Her oyunda rastgele konumlarda doğar (`BallSpawner.cs`).
- **Süre ve skor:** 60 saniyelik geri sayım, anlık "x / 10" skoru, "Kazandınız" ve "Süre Bitti" panelleri (`GameManager.cs`).
- **Hocanın kodu:** `Assets/_Game/Scripts/Hoca/RandomCubeMovement.cs` hiç değiştirilmeden kovalayan küplerde kullanıldı. Kovalama davranışını ayrı bir script olan `CubeChaser.cs` ekler.

## Ek özellikler

Ana menü, nasıl oynanır ekranı, duraklatma menüsü, özgün müzik kutusu müziği ve ses efektleri, konfeti efekti, parlayan yuvalar, kolu süreyle dönen dev çalar saat, rüya atmosferi (Bloom, Vinyet).

## Klasörler

- `Assets/_Game/Scripts`: oyun kodları (Türkçe açıklamalı)
- `Assets/_Game/Scripts/Editor/OyunKurucu.cs`: sahneleri kuran editör aracı (menü: Oyuncak Rüyası > Sahneleri Kur)
- `Assets/_Game/Scenes`: `AnaMenu` ve `Oyun` sahneleri
- `docs/`: WebGL build (GitHub Pages)
