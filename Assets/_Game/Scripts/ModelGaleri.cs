using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ana menüdeki "Blender Modellerim" galerisi (Mert Demir - 2506127004).
/// Mert'in Blender klasöründen geliştirilen modelleri dönen bir kaide üzerinde tek tek sergiler:
/// model adı, orijinal .blend dosyası ve oyunda nerede kullanıldığı yazar.
/// Sol/Sağ ok tuşları veya butonlarla gezilir, ESC ile menüye dönülür.
/// </summary>
public class ModelGaleri : MonoBehaviour
{
    public Transform turntable;
    public GameObject[] models;
    public string[] titles;
    [TextArea] public string[] details;

    public TMP_Text titleText;
    public TMP_Text detailText;
    public TMP_Text counterText;
    public GameObject galleryUI;
    public GameObject menuUI;

    public Transform cam;
    public Vector3 galleryCamPos;
    public Vector3 galleryLookAt;
    public float spinSpeed = 22f;

    int index;
    bool open;
    Vector3 menuPos;
    Quaternion menuRot;

    void Start()
    {
        if (cam == null && Camera.main) cam = Camera.main.transform;
        menuPos = cam.position;
        menuRot = cam.rotation;
        if (galleryUI) galleryUI.SetActive(false);
        foreach (var m in models) if (m) m.SetActive(false);
    }

    public void Open()
    {
        open = true;
        if (galleryUI) galleryUI.SetActive(true);
        if (menuUI) menuUI.SetActive(false);
        Show(index);
    }

    public void Close()
    {
        open = false;
        if (galleryUI) galleryUI.SetActive(false);
        if (menuUI) menuUI.SetActive(true);
    }

    public void Next() { Show(index + 1); }
    public void Prev() { Show(index - 1); }

    void Show(int i)
    {
        index = (i % models.Length + models.Length) % models.Length;
        for (int k = 0; k < models.Length; k++) if (models[k]) models[k].SetActive(k == index);
        if (titleText) titleText.text = titles[index];
        if (detailText) detailText.text = details[index];
        if (counterText) counterText.text = (index + 1) + " / " + models.Length;
        turntable.localRotation = Quaternion.Euler(0f, 200f, 0f);
    }

    void Update()
    {
        if (open)
        {
            turntable.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Next();
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Prev();
                if (kb.escapeKey.wasPressedThisFrame) Close();
            }
        }

        // Kamera menü ile galeri arasında yumuşakça geçer
        Vector3 targetPos = open ? galleryCamPos : menuPos;
        Quaternion targetRot = open ? Quaternion.LookRotation(galleryLookAt - galleryCamPos) : menuRot;
        float k2 = 1f - Mathf.Exp(-5f * Time.deltaTime);
        if ((cam.position - targetPos).sqrMagnitude > 400f) { cam.position = targetPos; cam.rotation = targetRot; }   // uzak geçişte kesme
        else
        {
            cam.position = Vector3.Lerp(cam.position, targetPos, k2);
            cam.rotation = Quaternion.Slerp(cam.rotation, targetRot, k2);
        }
    }
}
