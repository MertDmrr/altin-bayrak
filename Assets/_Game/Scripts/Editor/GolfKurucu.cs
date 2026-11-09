#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// ALTIN BAYRAK – Yeşiltepe Golf Kulübü sahnelerini kuran editör aracı (Mert Demir - 2506127004).
/// Menü: Altın Bayrak > Sahneleri Kur
/// Hocanın yönergesi: hocanın karakteri (Idle + Walk), üstten kamera, açık dünya, 10 küre, 10 delik, 60 saniye,
/// küplerde hocanın RandomCubeMovement kodu (değiştirilmeden).
/// </summary>
public static class GolfKurucu
{
    const string Root = "Assets/_Game";
    const string GenDir = Root + "/Generated";

    // ---- Oyun alanı ayarları (oynanış ölçüleri) ----
    public const float BallRadius = 0.8f;
    const float HoleRadius = 1.25f;        // deliğin ağzı
    const float HoleDepth = 1.9f;          // deliğin derinliği
    const float FunnelRadius = 4.7f;       // green eğimi (top deliğe doğru yuvarlanır)
    const float FunnelDepth = 0.45f;
    const float FairwayTile = 22f;        // fairway dokusunun tekrar boyu (delik kenarı aynı desende)
    const float RimWidth = 1.0f;          // delik kenar halkasının genişliği
    const float TerrainSize = 200f;
    const float TerrainHeight = 30f;
    const float TerrainBase = 3f;          // zemin = dünya y 0

    static readonly Vector2[] HolePositions =
    {
        new Vector2(6.0f, 6.5f), new Vector2(-6.5f, 6.0f), new Vector2(-6.0f, -6.5f), new Vector2(6.5f, -6.0f),
        new Vector2(14f, 3f), new Vector2(4f, 15f), new Vector2(-12f, 12f), new Vector2(-15f, -3f),
        new Vector2(-3f, -15f), new Vector2(12f, -12f),
    };

    // Robot çim biçiciler (hocanın küpleri)
    static readonly Vector3[] CubePositions =
    {
        new Vector3(-1f, 0f, 10.5f), new Vector3(19f, 0f, -5f), new Vector3(-19f, 0f, 6f),
    };

    // Kum bunkerleri: (x, z, yarıçap)
    static readonly Vector3[] Bunkers =
    {
        new Vector3(20f, 20f, 2.8f), new Vector3(20f, -4f, 2.5f), new Vector3(7f, -20f, 2.5f), new Vector3(-17f, 20f, 2.7f),
        new Vector3(-18f, -12f, 2.7f), new Vector3(14f, 12f, 2.1f), new Vector3(-4f, 20.5f, 2.3f),
    };

    // ---- Dünya yerleşimi ----
    const float RingHalf = 30f, RingCorner = 10f, PathHalfWidth = 1.5f;
    static readonly Vector2 PondCenter = new Vector2(-46f, 2f);
    const float PondRadius = 9f;
    static readonly Vector3 ClubPos = new Vector3(3f, 0f, 44f);
    static readonly Vector3 TownPos = new Vector3(0f, 0f, -74f);
    static readonly Vector3 GatePos = new Vector3(0f, 0f, -42f);
    static readonly Vector3 ScoreboardPos = new Vector3(-28f, 0f, 36f);
    static readonly Vector3 TrophyPos = new Vector3(20f, 0f, 36f);
    static readonly Vector2 ParkingCenter = new Vector2(41f, -9f), ParkingHalf = new Vector2(7f, 7f);

    // Modellerin ön yüz düzeltmesi (derece): modeli "ön yüzü verilen yöne baksın" diye çevirirken eklenir
    static readonly Dictionary<string, float> FrontOffset = new Dictionary<string, float>
    {
        { "KartonFigur", 90f }, { "SallayanEl", -90f },
    };

    const string PlayerLayerName = "Oyuncu";
    const string HoleBlockLayerName = "YuvaKapagi";

    // Renkler
    static readonly Color UiGreen = new Color(0.07f, 0.25f, 0.16f, 0.9f);
    static readonly Color UiGold = new Color(0.98f, 0.80f, 0.36f);
    static readonly Color BtnGreen = new Color(0.20f, 0.56f, 0.33f);
    static readonly Color BtnGold = new Color(0.90f, 0.66f, 0.20f);
    static readonly Color BtnNavy = new Color(0.17f, 0.27f, 0.45f);
    static readonly Color BtnRed = new Color(0.84f, 0.32f, 0.28f);
    static readonly Color CardText = new Color(0.12f, 0.22f, 0.17f);

    static TMP_FontAsset font;
    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    [MenuItem("Altın Bayrak/Sahneleri Kur")]
    public static void BuildAll()
    {
        EnsureDir(GenDir);
        EnsureDir(Root + "/Scenes");
        matCache.Clear();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Baloo2-Bold SDF.asset");
        PlayerSettings.productName = "Altın Bayrak";
        PlayerSettings.companyName = "Mert Demir";
        EnableTerrainHoles();
        SetupLayers();
        SetupTextures();
        SetupModels();
        SetupCharacter();
        BuildGameScene();
        BuildMenuScene();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Root + "/Scenes/AnaMenu.unity", true),
            new EditorBuildSettingsScene(Root + "/Scenes/Oyun.unity", true),
        };
        AssetDatabase.SaveAssets();
        Debug.Log("Altın Bayrak: sahneler kuruldu.");
    }

    // =====================================================================
    //  OYUN SAHNESİ
    // =====================================================================
    public static void BuildGameScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupAtmosphere(70f, 200f);

        var sun = new GameObject("Güneş (Directional Light)");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.86f);
        light.intensity = 1.35f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.8f;
        sun.transform.rotation = Quaternion.Euler(48f, -40f, 0f);
        RenderSettings.sun = light;

        var terrain = BuildTerrain();

        // Delikler
        var holesRoot = new GameObject("Delikler (10 adet)");
        var sockets = new List<HoleSocket>();
        for (int i = 0; i < HolePositions.Length; i++)
            sockets.Add(BuildHole(i, HolePositions[i], holesRoot.transform, terrain));

        // Dev golf topları (10 küre)
        var ballsRoot = new GameObject("Golf Topları (10 küre)");
        var balls = new List<Ball>();
        var ballMat = new PhysicsMaterial("TopFizik") { bounciness = 0.15f, dynamicFriction = 0.45f, staticFriction = 0.5f, bounceCombine = PhysicsMaterialCombine.Average };
        ballMat = SaveAsset(ballMat, Root + "/Physics/TopFizik.asset");
        for (int i = 0; i < 10; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.name = "GolfTopu_" + (i + 1).ToString("00");
            b.transform.SetParent(ballsRoot.transform);
            b.transform.localScale = Vector3.one * BallRadius * 2f;
            Vector2 hp = HolePositions[i];
            b.transform.position = new Vector3(hp.x + 4f, BallRadius + 0.05f, hp.y);
            b.GetComponent<Renderer>().sharedMaterial = GolfBallMat(i);
            b.GetComponent<SphereCollider>().sharedMaterial = ballMat;
            var rb = b.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.linearDamping = 0.35f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            balls.Add(b.AddComponent<Ball>());
        }

        var player = BuildPlayer(new Vector3(0f, 0.05f, -1.5f));

        // Kamera (üstten - geriden takip)
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 280f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        var cad = camGo.AddComponent<UniversalAdditionalCameraData>();
        cad.renderPostProcessing = true;
        cad.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        var tdc = camGo.AddComponent<TopDownCamera>();
        tdc.target = player.transform;
        tdc.offset = new Vector3(0f, 12.5f, -9f);
        tdc.smoothTime = 0.2f;
        tdc.SnapToTarget();
        player.GetComponent<PlayerController>().cameraTransform = camGo.transform;

        BuildPostProcess("GolfPostProcess");

        // Robot çim biçiciler (hocanın RandomCubeMovement kodlu küpleri)
        var cubesRoot = new GameObject("Robot Çim Biçiciler (Küpler)");
        for (int i = 0; i < CubePositions.Length; i++)
            BuildMower(i, CubePositions[i], cubesRoot.transform, player.transform);

        // Dünya
        BuildWorld(terrain, player.transform);

        // Yöneticiler + ses
        var managers = new GameObject("Oyun Yöneticisi");
        var gm = managers.AddComponent<GameManager>();
        gm.player = player.GetComponent<PlayerController>();
        var sfx = managers.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        var music = new GameObject("Müzik").AddComponent<AudioSource>();
        music.transform.SetParent(managers.transform);
        music.clip = Clip("golf_music_loop");
        music.loop = true;
        music.volume = 0.3f;
        music.playOnAwake = true;
        var amb = new GameObject("Ortam Sesi (Kuşlar)").AddComponent<AudioSource>();
        amb.transform.SetParent(managers.transform);
        amb.clip = Clip("golf_ambience");
        amb.loop = true;
        amb.volume = 0.55f;
        amb.playOnAwake = true;
        gm.sfx = sfx;
        gm.music = music;
        gm.startClip = Clip("golf_start");
        gm.scoreClip = Clip("golf_score");
        gm.tickClip = Clip("tick");
        gm.winClip = Clip("golf_win");
        gm.hornClip = Clip("golf_horn");

        var spawner = managers.AddComponent<BallSpawner>();
        spawner.balls = balls.ToArray();
        spawner.holes = sockets.ToArray();
        spawner.terrain = terrain;
        spawner.ballRadius = BallRadius;
        spawner.minDistance = FunnelRadius + BallRadius + 0.4f;
        spawner.maxDistance = FunnelRadius + BallRadius + 1.3f;
        spawner.keepAwayFromHoles = FunnelRadius + BallRadius + 0.4f;
        spawner.areaMin = new Vector2(-24f, -24f);
        spawner.areaMax = new Vector2(24f, 24f);
        var avoid = new List<Transform>();
        foreach (Transform c in cubesRoot.transform) avoid.Add(c);
        spawner.avoidPoints = avoid.ToArray();
        spawner.avoidRadius = 5f;

        miniMapSprite = MakeMiniMapSprite(terrain, MiniMapHalf);
        BuildGameUI(gm, sfx);

        // Dünya sınırı (görünmez duvarlar)
        var bounds = new GameObject("Dünya Sınırı");
        float e = 90f;
        AddWall(bounds.transform, new Vector3(0, 10, e), new Vector3(2 * e, 40, 2));
        AddWall(bounds.transform, new Vector3(0, 10, -e), new Vector3(2 * e, 40, 2));
        AddWall(bounds.transform, new Vector3(e, 10, 0), new Vector3(2, 40, 2 * e));
        AddWall(bounds.transform, new Vector3(-e, 10, 0), new Vector3(2, 40, 2 * e));

        // Yansıma kutusu (altın kupa, araba boyası parlasın)
        var probeGo = new GameObject("Yansıma (Reflection Probe)");
        probeGo.transform.position = new Vector3(0f, 6f, 10f);
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(160f, 50f, 160f);
        probe.resolution = 128;

        EditorSceneManager.SaveScene(scene, Root + "/Scenes/Oyun.unity");
    }

    static void SetupAtmosphere(float fogStart, float fogEnd)
    {
        RenderSettings.skybox = MakeSkybox();
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.64f, 0.75f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.72f, 0.78f, 0.74f);
        RenderSettings.ambientGroundColor = new Color(0.34f, 0.40f, 0.30f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.76f, 0.85f, 0.94f);
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
    }

    static void AddWall(Transform parent, Vector3 pos, Vector3 size)
    {
        var w = new GameObject("Duvar");
        w.transform.SetParent(parent);
        w.transform.position = pos;
        w.AddComponent<BoxCollider>().size = size;
    }

    // =====================================================================
    //  ARAZİ: şerit biçilmiş fairway, greenler, bunkerler, yollar, gölet, tepeler
    // =====================================================================
    static float FunnelProfile(float d)
    {
        if (d >= FunnelRadius) return 0f;
        float t = Mathf.Clamp01((d - HoleRadius) / (FunnelRadius - HoleRadius));
        return -FunnelDepth * Mathf.Pow(1f - t, 1.6f);
    }

    static float FunnelOffset(float wx, float wz)
    {
        float off = 0f;
        foreach (var h in HolePositions)
            off = Mathf.Min(off, FunnelProfile(Vector2.Distance(new Vector2(wx, wz), h)));
        return off;
    }

    static float SdRoundRect(Vector2 p, float half, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(half - r, half - r);
        Vector2 mq = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return mq.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    static float SdBox(Vector2 p, Vector2 c, Vector2 half)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half;
        Vector2 md = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
        return md.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
    }

    static float PondEdge(float ang) => PondRadius + 1.6f * Mathf.Sin(3f * ang + 0.7f) + 0.8f * Mathf.Cos(5f * ang);

    static float SdPond(Vector2 p)
    {
        Vector2 d = p - PondCenter;
        return d.magnitude - PondEdge(Mathf.Atan2(d.y, d.x));
    }

    static float SdCourse(Vector2 p)
    {
        return SdRoundRect(p, 24.5f, 9f) + (Mathf.PerlinNoise(p.x * 0.11f + 3f, p.y * 0.11f + 7f) - 0.5f) * 2.4f;
    }

    static float SdBunkers(Vector2 p)
    {
        float best = 1e9f;
        for (int i = 0; i < Bunkers.Length; i++)
        {
            var b = Bunkers[i];
            Vector2 c = new Vector2(b.x, b.y);
            float a = i * 1.7f;
            Vector2 c2 = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * b.z * 0.9f;
            float wob = (Mathf.PerlinNoise(p.x * 0.6f + i * 5f, p.y * 0.6f) - 0.5f) * 0.5f;
            float d = Mathf.Min(Vector2.Distance(p, c) - b.z, Vector2.Distance(p, c2) - b.z * 0.65f) + wob;
            best = Mathf.Min(best, d);
        }
        return best;
    }

    /// <summary>Yol ağı: sahayı çevreleyen araç yolu, kasaba yolu, kulübe bağlantı, otopark ve meydanlar.</summary>
    static float SdPaths(Vector2 p)
    {
        float ring = Mathf.Abs(SdRoundRect(p, RingHalf, RingCorner)) - PathHalfWidth;
        float road = SdBox(p, new Vector2(0f, -50f), new Vector2(2.8f, 21f));
        float club = SdBox(p, new Vector2(ClubPos.x, 33f), new Vector2(2.2f, 3.5f));
        float park = SdBox(p, ParkingCenter, ParkingHalf);
        float parkLink = SdBox(p, new Vector2(33f, -9f), new Vector2(3.5f, 2.2f));
        float trophy = Vector2.Distance(p, new Vector2(TrophyPos.x, TrophyPos.z)) - 4f;
        float board = SdBox(p, new Vector2(ScoreboardPos.x, ScoreboardPos.z - 1.5f), new Vector2(6f, 3f));
        float shop = SdBox(p, new Vector2(-13.5f, 35.5f), new Vector2(3.5f, 3f));
        float d = Mathf.Min(ring, Mathf.Min(road, Mathf.Min(club, Mathf.Min(park, parkLink))));
        return Mathf.Min(d, Mathf.Min(trophy, Mathf.Min(board, shop)));
    }

    static float TownFlat(float wx, float wz)
    {
        float d = SdBox(new Vector2(wx, wz), new Vector2(TownPos.x, TownPos.z), new Vector2(26f, 12f));
        float road = SdBox(new Vector2(wx, wz), new Vector2(0f, -50f), new Vector2(7f, 22f));
        d = Mathf.Min(d, road);
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / 8f));
    }

    static float GroundHeight(float wx, float wz)
    {
        var p = new Vector2(wx, wz);
        float r = p.magnitude;
        float h = 0f;
        // Sahanın dışında yumuşak tepeler (ağaçlık alan)
        float outer = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(52f, 72f, r));
        float hills = (Mathf.PerlinNoise(wx * 0.025f + 10f, wz * 0.025f + 3f) - 0.3f) * 8f;
        h += outer * Mathf.Max(0f, hills);
        // Dünyanın kenarındaki yüksek tepeler
        float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(72f, 96f, Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz))));
        h += rim * 16f * (0.7f + 0.3f * Mathf.PerlinNoise(wx * 0.05f, wz * 0.05f));
        h *= 1f - TownFlat(wx, wz);
        // Gölet çanağı
        float sp = SdPond(p);
        if (sp < 2.5f) h -= 0.75f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.5f, 0f, sp)) + 1.7f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, -5f, sp));
        // Greenlerdeki deliğe doğru eğim
        h += FunnelOffset(wx, wz);
        return h;
    }

    static Terrain BuildTerrain()
    {
        int res = 513;
        string tdPath = GenDir + "/GolfArazisi.asset";
        if (AssetDatabase.LoadMainAssetAtPath(tdPath) != null) AssetDatabase.DeleteAsset(tdPath);
        var td = new TerrainData();
        AssetDatabase.CreateAsset(td, tdPath);
        td.heightmapResolution = res;
        td.alphamapResolution = 1024;
        td.baseMapResolution = 1024;
        td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);

        float half = TerrainSize * 0.5f;
        var heights = new float[res, res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = -half + x / (float)(res - 1) * TerrainSize;
                float wz = -half + y / (float)(res - 1) * TerrainSize;
                heights[y, x] = Mathf.Clamp01((GroundHeight(wx, wz) + TerrainBase) / TerrainHeight);
            }
        td.SetHeights(0, 0, heights);

        // Delikler (Terrain Holes)
        int hr = td.holesResolution;
        var holes = new bool[hr, hr];
        for (int y = 0; y < hr; y++)
            for (int x = 0; x < hr; x++)
            {
                float wx = -half + (x + 0.5f) / hr * TerrainSize;
                float wz = -half + (y + 0.5f) / hr * TerrainSize;
                bool solid = true;
                foreach (var h in HolePositions)
                    if (Vector2.Distance(new Vector2(wx, wz), h) < HoleRadius + 0.3f) solid = false;
                holes[y, x] = solid;
            }
        td.SetHoles(0, 0, holes);

        // Zemin katmanları: 0 rough, 1 fairway, 2 kum, 3 yol (delik çevresi de sahayla aynı desende)
        var layers = new[]
        {
            Layer("Katman_Rough", "golf_rough", 12f, 0.05f),
            Layer("Katman_Fairway", "golf_fairway", FairwayTile, 0.15f),
            Layer("Katman_Kum", "golf_sand", 7f, 0.05f),
            Layer("Katman_Yol", "golf_path", 9f, 0.1f),
        };
        td.terrainLayers = layers;
        int ar = td.alphamapResolution;
        var alpha = new float[ar, ar, layers.Length];
        for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                float wx = -half + (x + 0.5f) / ar * TerrainSize;
                float wz = -half + (y + 0.5f) / ar * TerrainSize;
                var p = new Vector2(wx, wz);
                float fair = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.6f, 0.6f, SdCourse(p)));
                float sand = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.25f, 0.25f, SdBunkers(p)));
                float sp = SdPond(p);
                sand = Mathf.Max(sand, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1.8f, sp)));
                float path = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.2f, 0.2f, SdPaths(p)));
                float wPath = path;
                float wSand = sand * (1f - path);
                float wFair = fair * (1f - path) * (1f - sand);
                float wRough = Mathf.Max(0f, 1f - wPath - wSand - wFair);
                float sum = wRough + wFair + wSand + wPath;
                alpha[y, x, 0] = wRough / sum;
                alpha[y, x, 1] = wFair / sum;
                alpha[y, x, 2] = wSand / sum;
                alpha[y, x, 3] = wPath / sum;
            }
        td.SetAlphamaps(0, 0, alpha);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Golf Sahası (Terrain)";
        go.transform.position = new Vector3(-half, -TerrainBase, -half);
        var t = go.GetComponent<Terrain>();
        var tmat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
        tmat.EnableKeyword("_ALPHATEST_ON");
        tmat = SaveAsset(tmat, GenDir + "/GolfAraziMateryali.mat");
        t.materialTemplate = tmat;
        t.drawInstanced = true;
        t.heightmapPixelError = 3f;
        t.basemapDistance = 160f;
        return t;
    }

    /// <summary>Delik kenarı: arazi yüksekliğini izleyen halka; UV'ler terrain'in fairway katmanıyla birebir aynı.</summary>
    static Mesh MakeHoleRim(Terrain terrain, Vector2 c, Vector3 origin, float lift)
    {
        int seg = 96, rows = 7;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        Vector3 tp = terrain.transform.position;
        for (int j = 0; j < rows; j++)
        {
            float r = HoleRadius + RimWidth * j / (rows - 1);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float wx = c.x + Mathf.Cos(a) * r, wz = c.y + Mathf.Sin(a) * r;
                float wy = Ground(terrain, wx, wz) + lift * (j == rows - 1 ? 0.6f : 1f);
                v.Add(new Vector3(wx, wy, wz) - origin);
                uv.Add(new Vector2((wx - tp.x) / FairwayTile, (wz - tp.z) / FairwayTile));
            }
        }
        int w = seg + 1;
        for (int j = 0; j < rows - 1; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = j * w + i, b = a + 1, c2 = a + w, d = c2 + 1;
                tri.AddRange(new[] { a, c2, b, b, c2, d });
            }
        var m = new Mesh();
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        if (m.normals[0].y < 0f) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); }
        m.RecalculateTangents(); m.RecalculateBounds();
        return m;
    }

    static Material FairwayRimMat()
    {
        var m = Mat("Delik_Kenar_Saha", Color.white, Tex("golf_fairway"), 0.15f);
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.SetTextureOffset("_BaseMap", Vector2.zero);
        return m;
    }

    /// <summary>Mini harita arka planı: terrain doku ağırlıklarından üstten görünüm resmi üretir.</summary>
    static Sprite MakeMiniMapSprite(Terrain terrain, float half)
    {
        int N = 256;
        var td = terrain.terrainData;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Color[] layerCols = { new Color(0.22f, 0.40f, 0.17f), new Color(0.38f, 0.62f, 0.27f), new Color(0.92f, 0.84f, 0.62f), new Color(0.80f, 0.79f, 0.75f) };
        int ar = td.alphamapResolution;
        float[,,] al = td.GetAlphamaps(0, 0, ar, ar);
        Vector3 tp = terrain.transform.position;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float wx = -half + (x + 0.5f) / N * half * 2f, wz = -half + (y + 0.5f) / N * half * 2f;
                int ax = Mathf.Clamp(Mathf.FloorToInt((wx - tp.x) / td.size.x * ar), 0, ar - 1);
                int az = Mathf.Clamp(Mathf.FloorToInt((wz - tp.z) / td.size.z * ar), 0, ar - 1);
                Color c = Color.black;
                for (int k = 0; k < layerCols.Length && k < td.alphamapLayers; k++) c += layerCols[k] * al[az, ax, k];
                if (SdPond(new Vector2(wx, wz)) < 0f) c = new Color(0.30f, 0.55f, 0.75f);
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        string path = GenDir + "/MiniHarita.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static TerrainLayer Layer(string name, string tex, float tile, float smooth)
    {
        var l = new TerrainLayer();
        l.diffuseTexture = Tex(tex);
        l.tileSize = new Vector2(tile, tile);
        l.smoothness = smooth;
        return SaveAsset(l, GenDir + "/" + name + ".asset");
    }

    // =====================================================================
    //  DELİK (green + numaralı bayrak)
    // =====================================================================
    static HoleSocket BuildHole(int index, Vector2 p, Transform parent, Terrain terrain)
    {
        float topY = -FunnelDepth;
        var root = new GameObject("Delik_" + (index + 1).ToString("00"));
        root.transform.SetParent(parent);
        root.transform.position = new Vector3(p.x, topY, p.y);
        var socket = root.AddComponent<HoleSocket>();
        socket.settleDepth = -(BallRadius - 0.08f);
        socket.filledColor = new Color(1f, 0.80f, 0.28f);

        // Delik ağzının gerçek yüksekliği (arazinin eğimli green'i)
        float rimY = 0f;
        for (int k = 0; k < 16; k++)
        {
            float a = k / 16f * Mathf.PI * 2f;
            rimY += Ground(terrain, p.x + Mathf.Cos(a) * HoleRadius, p.y + Mathf.Sin(a) * HoleRadius);
        }
        rimY = rimY / 16f - topY;   // köke göre

        // Deliğin içi: koyu toprak kap
        var cup = new GameObject("Delik İçi");
        cup.transform.SetParent(root.transform, false);
        cup.transform.localPosition = new Vector3(0f, rimY, 0f);
        var cupMesh = MeshAsset("Canak", () => MakeCup(HoleRadius, HoleDepth + rimY, 48));
        cup.AddComponent<MeshFilter>().sharedMesh = cupMesh;
        cup.AddComponent<MeshRenderer>().sharedMaterial = Mat("Delik_Ic", new Color(0.16f, 0.13f, 0.11f), null, 0.05f);
        cup.AddComponent<MeshCollider>().sharedMesh = cupMesh;

        // Beyaz golf kabı astarı (gerçek deliklerdeki plastik kap) + yuvarlak dudak
        var liner = new GameObject("Kap Astarı");
        liner.transform.SetParent(root.transform, false);
        liner.transform.localPosition = new Vector3(0f, rimY - 0.05f, 0f);
        liner.AddComponent<MeshFilter>().sharedMesh = MeshAsset("Astar", () => MakeTube(HoleRadius - 0.012f, 0.42f, 48));
        liner.AddComponent<MeshRenderer>().sharedMaterial = Mat("Delik_Astar", new Color(0.95f, 0.95f, 0.94f), null, 0.45f);
        var lip = new GameObject("Kap Dudağı");
        lip.transform.SetParent(root.transform, false);
        lip.transform.localPosition = new Vector3(0f, rimY - 0.05f, 0f);
        lip.AddComponent<MeshFilter>().sharedMesh = MeshAsset("DelikDudak", () => MakeTorus(HoleRadius - 0.012f, 0.03f, 64, 10));
        var lipR = lip.AddComponent<MeshRenderer>();
        lipR.sharedMaterial = Mat("Delik_Dudak", new Color(0.97f, 0.97f, 0.96f), null, 0.6f);
        socket.ringRenderer = lipR;

        // Kenar halkası: arazinin şekline oturur, fairway ile aynı dokuyu aynı dünya koordinatında kullanır
        // (terrain deliğinin kare tırtıklarını kapatır, sahadan ayırt edilmez)
        var rim = new GameObject("Kenar (Saha Deseni)");
        rim.transform.SetParent(root.transform, false);
        var rimMesh = MeshAsset("DelikKenar_" + (index + 1), () => MakeHoleRim(terrain, p, root.transform.position, 0.012f));
        rim.AddComponent<MeshFilter>().sharedMesh = rimMesh;
        var rimR = rim.AddComponent<MeshRenderer>();
        rimR.sharedMaterial = FairwayRimMat();
        rim.AddComponent<MeshCollider>().sharedMesh = rimMesh;

        // Algılayıcı (trigger)
        var trig = new GameObject("Algilayici");
        trig.transform.SetParent(root.transform, false);
        trig.transform.localPosition = new Vector3(0f, -(HoleDepth - BallRadius), 0f);
        var sc = trig.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.55f;
        trig.AddComponent<HoleTrigger>().socket = socket;

        // Kapak (dolunca açılır)
        var lid = new GameObject("Kapak");
        lid.transform.SetParent(root.transform, false);
        lid.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        var lidCol = lid.AddComponent<BoxCollider>();
        lidCol.size = new Vector3(HoleRadius * 2f, 0.1f, HoleRadius * 2f);
        lidCol.enabled = false;
        socket.lidCollider = lidCol;

        // Oyuncu kapağı: sadece oyuncu çarpar, toplar içinden geçip deliğe düşer
        var block = new GameObject("Oyuncu Kapagi");
        block.layer = LayerMask.NameToLayer(HoleBlockLayerName);
        block.transform.SetParent(root.transform, false);
        block.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        block.AddComponent<BoxCollider>().size = new Vector3(HoleRadius * 2.1f, 0.2f, HoleRadius * 2.1f);

        // Numaralı bayrak (golf bayrağı, top girince alınır)
        var flag = new GameObject("Bayrak " + (index + 1));
        flag.transform.SetParent(root.transform, false);
        flag.transform.localPosition = new Vector3(HoleRadius + 0.9f, FunnelProfile(HoleRadius + 0.9f) + FunnelDepth - 0.05f, 0.4f);
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Direk";
        Object.DestroyImmediate(pole.GetComponent<Collider>());
        pole.transform.SetParent(flag.transform, false);
        pole.transform.localScale = new Vector3(0.09f, 1.7f, 0.09f);
        pole.transform.localPosition = new Vector3(0f, 1.7f, 0f);
        pole.GetComponent<Renderer>().sharedMaterial = Mat("Bayrak_Direk", new Color(0.97f, 0.97f, 0.96f), null, 0.6f);
        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(tip.GetComponent<Collider>());
        tip.transform.SetParent(flag.transform, false);
        tip.transform.localScale = Vector3.one * 0.16f;
        tip.transform.localPosition = new Vector3(0f, 3.42f, 0f);
        tip.GetComponent<Renderer>().sharedMaterial = Mat("Altin_Parlak", new Color(1f, 0.78f, 0.3f), null, 0.85f);
        var cloth = new GameObject("Kumaş");
        cloth.transform.SetParent(flag.transform, false);
        cloth.transform.localPosition = new Vector3(0.05f, 2.75f, 0f);
        cloth.AddComponent<MeshFilter>().sharedMesh = MeshAsset("BayrakKumas", MakeFlagQuad);
        var fm = Mat("Bayrak_" + (index + 1), Color.white, Tex("flag_" + (index + 1).ToString("00")), 0.2f);
        cloth.AddComponent<MeshRenderer>().sharedMaterial = fm;
        cloth.AddComponent<BayrakDalga>();
        socket.flagBeacon = flag;

        // Işık (dolunca yanar)
        var glow = new GameObject("Isik");
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        var gl = glow.AddComponent<Light>();
        gl.type = LightType.Point;
        gl.color = new Color(1f, 0.82f, 0.4f);
        gl.range = 6f;
        gl.intensity = 3f;
        gl.shadows = LightShadows.None;
        socket.glowLight = gl;

        socket.scoreEffect = MakeConfetti(root.transform);
        return socket;
    }

    static Material GolfBallMat(int i)
    {
        var m = Mat("GolfTopu_" + (i + 1), Color.white, Tex("golfball_" + (i + 1).ToString("00")), 0.62f);
        var nm = Tex("golfball_normal");
        if (nm != null)
        {
            m.SetTexture("_BumpMap", nm);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        return m;
    }

    static ParticleSystem MakeConfetti(Transform parent)
    {
        var go = new GameObject("Kutlama (Konfeti)");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.2f;
        main.maxParticles = 120;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.82f, 0.3f), 0f), new GradientColorKey(Color.white, 0.4f), new GradientColorKey(new Color(0.45f, 0.85f, 0.45f), 0.7f), new GradientColorKey(new Color(1f, 0.65f, 0.2f), 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.6f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Mat_Konfeti", "fx_star", false);
        return ps;
    }

    static Material ParticleMat(string name, string tex, bool additive)
    {
        string path = GenDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", Tex(tex));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material UnlitTransparent(string name, string tex, Color tint)
    {
        string path = GenDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
        if (tex != null) m.SetTexture("_BaseMap", Tex(tex));
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    // =====================================================================
    //  OYUNCU: hocanın karakteri, golfçü kıyafetiyle
    // =====================================================================
    static GameObject SpawnGolfer(Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Characters/Karakter_Golfcu.fbx");
        if (prefab == null) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Characters/Karakter_Rigged.fbx");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.name = "Golfçü (hocanın T-Pose modeli)";
        model.transform.SetParent(parent, false);
        var anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "/Animations/KarakterAnimator.controller");
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // Golfçü kıyafeti renkleri
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.updateWhenOffscreen = true;
            string n = smr.name;
            Material m = null;
            if (n == "outfit_top") m = Mat("Golf_Polo", new Color(0.96f, 0.96f, 0.94f), null, 0.25f);
            else if (n == "outfit_bottom") m = Mat("Golf_Pantolon", new Color(0.78f, 0.68f, 0.50f), null, 0.2f);
            else if (n == "outfit_shoes") m = Mat("Golf_Ayakkabi", new Color(0.97f, 0.97f, 0.95f), null, 0.55f);
            else if (n == "Golf_Yelek") m = Mat("Golf_Yelek", Color.white, Tex("golf_argyle"), 0.1f);
            else if (n == "Golf_Havlu") m = Mat("Golf_Havlu", Color.white, Tex("golf_towel"), 0.05f);
            else if (n == "Golf_Sapkasi") { SetCapMats(smr); continue; }
            else if (n == "Polo_Yaka" || n == "Polo_Dugmeler") m = Mat("Golf_Yaka", new Color(0.93f, 0.94f, 0.95f), null, 0.3f);
            else if (n == "Kemer") m = Mat("Golf_Kemer", new Color(0.30f, 0.19f, 0.12f), null, 0.5f);
            else if (n == "Kemer_Toka") m = Mat("Golf_Altin", new Color(0.95f, 0.75f, 0.30f), null, 0.8f);
            else if (n == "AvatarBody")
            {
                // sol elde beyaz golf eldiveni
                var arrB = smr.sharedMaterials;
                for (int i = 0; i < arrB.Length; i++)
                    if (arrB[i] != null && arrB[i].name.Contains("Eldiven")) arrB[i] = Mat("Golf_Eldiven", new Color(0.97f, 0.97f, 0.96f), null, 0.3f);
                smr.sharedMaterials = arrB;
                continue;
            }
            if (m != null)
            {
                var arr = smr.sharedMaterials;
                for (int i = 0; i < arr.Length; i++) arr[i] = m;
                smr.sharedMaterials = arr;
            }
        }
        return model;
    }

    static void SetCapMats(Renderer r)
    {
        var arr = r.sharedMaterials;
        for (int i = 0; i < arr.Length; i++)
        {
            string src = arr[i] != null ? arr[i].name : "";
            if (src.Contains("Siper")) arr[i] = Mat("Golf_Sapka_Siper", new Color(0.12f, 0.19f, 0.36f), null, 0.3f);
            else if (src.Contains("Dikis")) arr[i] = Mat("Golf_Sapka_Dikis", new Color(0.80f, 0.82f, 0.86f), null, 0.2f);
            else if (src.Contains("Altin")) { var g = Mat("Golf_Altin", new Color(0.95f, 0.75f, 0.30f), null, 0.7f); g.SetFloat("_Metallic", 0.55f); arr[i] = g; }
            else arr[i] = Mat("Golf_Sapka", new Color(0.97f, 0.97f, 0.96f), null, 0.3f);
        }
        r.sharedMaterials = arr;
    }

    static GameObject BuildPlayer(Vector3 pos)
    {
        var root = new GameObject("Oyuncu (Golfçü)");
        root.tag = "Player";
        root.layer = LayerMask.NameToLayer(PlayerLayerName);
        root.transform.position = pos;

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 60f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var noFriction = new PhysicsMaterial("OyuncuFizik") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0f };
        noFriction = SaveAsset(noFriction, Root + "/Physics/OyuncuFizik.asset");
        var cap = root.AddComponent<CapsuleCollider>();
        cap.radius = 0.35f;
        cap.height = 1.95f;
        cap.center = new Vector3(0f, 0.975f, 0f);
        cap.sharedMaterial = noFriction;

        // Kolların önündeki itme alanı: dev golf topu ellere değsin
        var push = root.AddComponent<BoxCollider>();
        push.center = new Vector3(0f, 1.2f, 0.38f);
        push.size = new Vector3(0.62f, 0.42f, 0.34f);
        push.sharedMaterial = noFriction;

        var model = SpawnGolfer(root.transform);
        var anim = model.GetComponent<Animator>();

        // Okunurluk halkası (altın)
        var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ring.name = "Oyuncu Halkasi";
        Object.DestroyImmediate(ring.GetComponent<Collider>());
        ring.transform.SetParent(root.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = Vector3.one * 1.9f;
        var rr = ring.GetComponent<Renderer>();
        rr.sharedMaterial = UnlitTransparent("Mat_OyuncuHalkasi", "player_ring_golf", new Color(1f, 1f, 1f, 0.9f));
        rr.shadowCastingMode = ShadowCastingMode.Off;
        rr.receiveShadows = false;

        var pc = root.AddComponent<PlayerController>();
        pc.animator = anim;
        pc.walkSpeed = 4.0f;
        pc.walkClipSpeed = 1.41f;
        return root;
    }

    // =====================================================================
    //  ROBOT ÇİM BİÇİCİ (hocanın RandomCubeMovement kodu ile hareket eden küp)
    // =====================================================================
    static void BuildMower(int i, Vector3 pos, Transform parent, Transform player)
    {
        float size = 1.5f;
        var root = new GameObject("Robot Çim Biçici " + (i + 1) + " (Küp)");
        root.transform.SetParent(parent);
        root.transform.position = new Vector3(pos.x, size * 0.5f + 0.02f, pos.z);
        var bc = root.AddComponent<BoxCollider>();
        bc.size = Vector3.one * size;
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var vis = new GameObject("Görsel");
        vis.transform.SetParent(root.transform, false);
        var model = PlaceModel("RobotCimBicici", vis.transform, Vector3.zero, 0f, 1f, false, false);
        model.transform.localPosition = Vector3.zero;

        // Biçilen çim parçacıkları (hareket ettikçe)
        var grass = new GameObject("Biçilen Çim");
        grass.transform.SetParent(root.transform, false);
        grass.transform.localPosition = new Vector3(0f, -0.65f, 0f);
        var ps = grass.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.62f, 0.25f), new Color(0.55f, 0.78f, 0.35f));
        var em = ps.emission; em.rateOverTime = 0f; em.rateOverDistance = 8f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.6f;
        grass.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Mat_Cim", "fx_soft", false);

        // Hocanın kodu (değiştirilmeden): rastgele sağa-sola dolaşma
        var rcm = root.AddComponent<RandomCubeMovement>();
        rcm.moveSpeed = 2f;
        rcm.moveRange = 2f;
        rcm.changeTime = 2f;

        var alert = new GameObject("Uyari (!)");
        alert.transform.SetParent(root.transform, false);
        alert.transform.localPosition = new Vector3(0f, size + 0.7f, 0f);
        var tmp = alert.AddComponent<TextMeshPro>();
        tmp.text = "!";
        tmp.font = font;
        tmp.fontSize = 9f;
        tmp.color = new Color(1f, 0.55f, 0.15f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = Color.white;
        tmp.rectTransform.sizeDelta = new Vector2(2f, 2f);

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0.9f;
        audio.minDistance = 6f;
        audio.maxDistance = 40f;
        var motor = new GameObject("Motor Sesi").AddComponent<AudioSource>();
        motor.transform.SetParent(root.transform, false);
        motor.clip = Clip("robot_motor_loop");
        motor.loop = true;
        motor.playOnAwake = true;
        motor.volume = 0.18f;
        motor.spatialBlend = 1f;
        motor.rolloffMode = AudioRolloffMode.Linear;
        motor.minDistance = 3f;
        motor.maxDistance = 22f;

        // Tehlike halkası: robotun fark etme alanı yerde görünür (oyuncu nereden geçeceğini planlayabilir)
        var ringGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ringGo.name = "Tehlike Halkası";
        Object.DestroyImmediate(ringGo.GetComponent<Collider>());
        ringGo.transform.SetParent(root.transform, false);
        ringGo.transform.localPosition = new Vector3(0f, -size * 0.5f + 0.05f, 0f);
        ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ringGo.transform.localScale = Vector3.one * 10f;
        var ringR = ringGo.GetComponent<Renderer>();
        ringR.sharedMaterial = UnlitTransparent("Mat_RobotHalkasi", "robot_ring", new Color(1f, 0.62f, 0.2f, 0.55f));
        ringR.shadowCastingMode = ShadowCastingMode.Off;
        ringR.receiveShadows = false;

        // Tepe lambası ışığı (kovalarken çakar)
        var beacon = new GameObject("Tepe Lambası Işığı").AddComponent<Light>();
        beacon.transform.SetParent(vis.transform, false);
        beacon.transform.localPosition = new Vector3(0f, size * 0.5f + 1.2f, 0f);
        beacon.type = LightType.Point;
        beacon.color = new Color(1f, 0.4f, 0.1f);
        beacon.range = 6f;
        beacon.intensity = 2f;
        beacon.shadows = LightShadows.None;

        // Çarpınca saçılan çim parçaları
        var burstGo = new GameObject("Çarpma Efekti");
        burstGo.transform.SetParent(root.transform, false);
        burstGo.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        var burst = burstGo.AddComponent<ParticleSystem>();
        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var bm = burst.main;
        bm.playOnAwake = false; bm.loop = false; bm.duration = 0.5f;
        bm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        bm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
        bm.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        bm.gravityModifier = 1.6f;
        bm.simulationSpace = ParticleSystemSimulationSpace.World;
        bm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.62f, 0.25f), new Color(1f, 0.85f, 0.35f));
        var be = burst.emission; be.rateOverTime = 0f; be.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
        var bs = burst.shape; bs.shapeType = ParticleSystemShapeType.Sphere; bs.radius = 0.6f;
        burstGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Mat_Cim", "fx_soft", false);

        var ch = root.AddComponent<CubeChaser>();
        ch.player = player;
        ch.detectRadius = 5f;
        ch.giveUpRadius = 8.5f;
        ch.chaseSpeed = 3f;
        ch.acceleration = 4f;
        ch.returnSpeed = 3f;
        ch.leashRadius = 10f;
        ch.alertTime = 0.55f;
        ch.leadTime = 0.45f;
        ch.hitForce = 4f;
        ch.stunTime = 0.4f;
        ch.cooldown = 3f;
        ch.dangerRing = ringR;
        ch.beaconLight = beacon;
        ch.hitBurst = burst;
        ch.visual = vis.transform;
        ch.alertIcon = alert;
        ch.audioSource = audio;
        ch.alertClip = Clip("robot_alert");
        ch.bonkClip = Clip("robot_bonk");
        ch.motorAudio = motor;
    }

    // =====================================================================
    //  DÜNYA: kulüp binası, tabela, kupa, gölet, yollar, ağaçlar ve Blender modelleri
    // =====================================================================
    static float Ground(Terrain t, float x, float z) => t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y;

    static float YawToward(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from; d.y = 0f;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    static GameObject Face(string model, Transform parent, Vector3 pos, float yaw, float scale, bool collide, bool snap = true)
    {
        float off;
        FrontOffset.TryGetValue(model, out off);
        return PlaceModel(model, parent, pos, yaw + off, scale, collide, snap);
    }

    static void BuildWorld(Terrain terrain, Transform player)
    {
        var world = new GameObject("Yeşiltepe Golf Kulübü (Dünya)").transform;
        Vector3 center = Vector3.zero;

        // ---- Kulüp binası (kuzeyde, terası sahaya bakar)
        var cp = ClubPos; cp.y = Ground(terrain, cp.x, cp.z);
        var club = Face("KulupBinasi", world, cp, 180f, 1f, true, false);
        club.transform.position = cp;
        var clubSign = Find(club, "TabelaNoktasi");
        if (clubSign) WorldText(club.transform, "Tabela Yazısı", "YEŞİLTEPE GOLF KULÜBÜ", clubSign.position, FrontDir(club.transform, clubSign.position), 0.62f, UiGold, 4.2f);
        var shopSign = Find(club, "ShopTabelaNoktasi");
        if (shopSign) WorldText(club.transform, "Pro Shop Yazısı", "PRO SHOP", shopSign.position, FrontDir(club.transform, shopSign.position), 0.62f, Color.white, 3.8f);
        LampLights(club, 2.2f, 7f);

        // Teras masaları + kakao kupası (masanın üstünde)
        var terrace = Find(club, "TerasNoktasi");
        if (terrace)
        {
            Vector3 right = club.transform.right;
            Vector3 deck = terrace.position;
            Vector3 tFront = FrontDir(club.transform, clubSign ? clubSign.position : deck);
            float[] offs = { 12.5f, 0f, -3.2f };
            for (int k = 0; k < 3; k++)
            {
                Vector3 tp = deck + right * offs[k] + tFront * 0.2f;
                var table = PlaceModel("TerasMasasi", world, tp, k * 25f, 1f, true, false);
                table.transform.position = tp;
                if (k == 1)
                {
                    var mug = PlaceModel("KakaoKupasi", table.transform, new Vector3(0.05f, 0.77f, 0f), 30f, 0.13f, false);
                    var steam = MakeSteam(mug.transform, 0.12f);
                    var rs = mug.GetComponentsInChildren<Renderer>();
                    Bounds cupB = rs[0].bounds;
                    foreach (var r in rs) if (r.name.Contains("Kupa")) cupB = r.bounds;
                    steam.transform.position = new Vector3(cupB.center.x, cupB.max.y + 0.02f, cupB.center.z);
                }
            }
        }

        // ---- Pro Shop önündeki ahşap manken eli (vitrin heykeli)
        {
            var pp = new Vector3(-13.5f, 0f, 36.2f); pp.y = Ground(terrain, pp.x, pp.z);
            var ped = Pedestal(world, "Manken Eli Kaidesi", pp, new Vector3(2.2f, 0.9f, 2.2f));
            var hand = Face("DevMankenEli", world, pp + Vector3.up * 0.9f, 180f, 1.35f, false);
            AnimateModel(hand, "DevMankenEli");
            WorldText(ped.transform, "Kaide Yazısı", "GOLF ELDİVENLERİ\nPRO SHOP'TA", pp + new Vector3(0f, 0.45f, -1.12f), Vector3.back, 0.22f, UiGold, 2f);
        }

        // ---- Dev altın kupa heykeli
        {
            var tp = TrophyPos; tp.y = Ground(terrain, tp.x, tp.z);
            Pedestal(world, "Kupa Kaidesi", tp, new Vector3(3.2f, 1.4f, 3.2f));
            var trophy = Face("AltinKupa", world, tp + Vector3.up * 1.4f, 180f, 6.5f, false);
            var col = trophy.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.55f, 0f); col.radius = 0.25f; col.height = 1.1f;
            WorldText(world, "Kupa Yazısı", "ALTIN BAYRAK KUPASI", tp + new Vector3(0f, 0.72f, -1.62f), Vector3.back, 0.32f, UiGold, 3f);
        }

        // ---- Skor tabelası + turnuva saati + canlı skor
        {
            var sp = ScoreboardPos; sp.y = Ground(terrain, sp.x, sp.z);
            var board = Face("SkorTabelasi", world, sp, 180f, 1f, true, false);
            board.transform.position = sp;
            var title = Find(board, "BaslikNoktasi");
            var clockPt = Find(board, "SaatNoktasi");
            var list = Find(board, "ListeNoktasi");
            Vector3 f = FrontDir(board.transform, title ? title.position : sp);
            if (title) WorldText(board.transform, "Başlık", "ALTIN BAYRAK TURNUVASI", title.position, f, 0.7f, UiGold, 7.6f);
            if (list)
            {
                var t = WorldText(board.transform, "Canlı Skor", "", list.position + Vector3.up * 0.1f, f, 0.42f, Color.white, 5.8f);
                t.alignment = TextAlignmentOptions.Left;
                t.lineSpacing = 12f;
                t.rectTransform.sizeDelta = new Vector2(5.8f, 3f);
                board.AddComponent<SkorPanosu>().text = t;
            }
            if (clockPt) BuildClock(board.transform, clockPt.position, f);
        }

        // ---- Giriş kapısı (güneyde, kasabaya açılır)
        {
            var gp = GatePos; gp.y = Ground(terrain, gp.x, gp.z);
            var gate = Face("GirisKapisi", world, gp, 180f, 1f, true, false);
            gate.transform.position = gp;
            var s = Find(gate, "TabelaNoktasi");
            if (s)
            {
                Vector3 f = FrontDir(gate.transform, s.position);
                WorldText(gate.transform, "Giriş Yazısı", "YEŞİLTEPE GOLF KULÜBÜ", s.position, f, 0.62f, UiGold, 8f);
                WorldText(gate.transform, "Arka Yazı", "İYİ OYUNLAR!", s.position - f * 0.36f, -f, 0.7f, Color.white, 8f);
            }
            LampLights(gate, 1.6f, 6f);
        }

        // ---- Oyuncak Kasaba (Mahalle.blend): kulübün güneyinde, yolun ucunda
        {
            var tp = TownPos; tp.y = Ground(terrain, tp.x, tp.z) + 0.02f;
            var town = PlaceModel("OyuncakKasaba", world, tp, 0f, 1f, true);
            LampLights(town, 2.2f, 8f);
            TownCars(town, true);
        }

        // ---- Karşılama heykeli: el sallayan dev el (EL.blend)
        {
            var hp = new Vector3(10f, 0f, -36.5f); hp.y = Ground(terrain, hp.x, hp.z);
            Pedestal(world, "El Kaidesi", hp, new Vector3(2.4f, 0.8f, 2.4f));
            var wave = Face("SallayanEl", world, hp + Vector3.up * 0.8f, YawToward(hp, new Vector3(0f, 0f, -10f)), 1.15f, false);
            AnimateModel(wave, "SallayanEl");
            WorldText(world, "Hoş Geldiniz", "HOŞ GELDİNİZ!", hp + new Vector3(0f, 0.4f, 1.22f), Vector3.forward, 0.3f, UiGold, 2.4f);
        }

        // ---- Otopark: senin oyuncak arabaların + park etmiş golf arabaları
        {
            var lines = new GameObject("Otopark Çizgileri").transform;
            lines.SetParent(world);
            var white = Mat("Cizgi_Beyaz", new Color(0.95f, 0.95f, 0.93f), null, 0.3f);
            for (int k = 0; k <= 4; k++)
            {
                float x = 35f + k * 3.2f;
                var q = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(lines);
                q.transform.position = new Vector3(x, Ground(terrain, x, -12f) + 0.01f, -12f);
                q.transform.localScale = new Vector3(0.15f, 0.02f, 5f);
                q.GetComponent<Renderer>().sharedMaterial = white;
            }
            var carCols = new[] { new Color(0.86f, 0.18f, 0.16f), new Color(0.20f, 0.45f, 0.85f), new Color(0.97f, 0.80f, 0.28f) };
            for (int k = 0; k < 3; k++)
            {
                var p = new Vector3(36.6f + k * 3.2f, 0f, -12.2f); p.y = Ground(terrain, p.x, p.z);
                var car = PlaceModel("OyuncakAraba", world, p, 0f, 0.62f, true);
                SwapMaterial(car, "M_Araba_Boya", Mat("Araba_Boya_" + k, carCols[k], null, 0.8f));
                HideChildren(car, "Kurma_Anahtari");
            }
            for (int k = 0; k < 2; k++)
            {
                var p = new Vector3(37f + k * 3f, 0f, -4.5f); p.y = Ground(terrain, p.x, p.z);
                PlaceModel("GolfArabasi", world, p, 90f, 1f, true);
            }
        }

        // ---- Saha ipleri ve karton seyirci panoları (oyun alanının kenarı)
        {
            var gal = new GameObject("Saha İpleri ve Karton Seyirciler").transform;
            gal.SetParent(world);
            var postM = Mat("Ip_Direk", new Color(0.96f, 0.96f, 0.95f), null, 0.5f);
            var ropeM = Mat("Ip", new Color(0.95f, 0.75f, 0.30f), null, 0.4f);
            RopeLine(gal, terrain, new Vector3(26.5f, 0, -13f), new Vector3(26.5f, 0, 15f), postM, ropeM);
            RopeLine(gal, terrain, new Vector3(-26.5f, 0, -13f), new Vector3(-26.5f, 0, 15f), postM, ropeM);
            RopeLine(gal, terrain, new Vector3(-13f, 0, 26.5f), new Vector3(13f, 0, 26.5f), postM, ropeM);

            // Karton seyirci panoları (Kemik.blend) – hareket etmeyen dekor
            var shirts = new[] { new Color(0.40f, 0.70f, 0.95f), new Color(0.98f, 0.80f, 0.30f), new Color(0.95f, 0.95f, 0.93f), new Color(0.80f, 0.50f, 0.90f), new Color(1f, 0.62f, 0.30f), new Color(0.95f, 0.45f, 0.42f), new Color(0.55f, 0.80f, 0.45f), new Color(0.25f, 0.35f, 0.6f) };
            var fans = new List<Vector3>();
            foreach (float z in new[] { -9f, -3f, 3f, 9f }) { fans.Add(new Vector3(27.7f, 0, z)); fans.Add(new Vector3(-27.7f, 0, z + 1.5f)); }
            foreach (float x in new[] { -8f, -2f, 4f, 10f }) fans.Add(new Vector3(x, 0, 27.7f));
            for (int k = 0; k < fans.Count; k++)
            {
                var fp = fans[k]; fp.y = Ground(terrain, fp.x, fp.z);
                var f = Face("KartonFigur", gal, fp, YawToward(fp, center), 0.4f, false);
                SwapMaterial(f, "M_Karton_Gomlek", Mat("Karton_Gomlek_" + k, shirts[k % shirts.Length], null, 0.15f));
                AddBoxCollider(f, 0.8f);
            }
        }

        // ---- Banklar (yol kenarında, sahaya bakar)
        {
            var benches = new[] { new Vector3(32.2f, 0, -22f), new Vector3(32.2f, 0, 16f), new Vector3(-32.2f, 0, -18f), new Vector3(-32.2f, 0, 20f), new Vector3(-16f, 0, -32.2f), new Vector3(16f, 0, -32.2f) };
            foreach (var b in benches)
            {
                var p = b; p.y = Ground(terrain, p.x, p.z);
                Face("Bank", world, p, YawToward(p, center), 1f, true);
            }
        }

        // ---- Gölet + fıskiye + kayalar
        BuildPond(world, terrain);

        // ---- Ağaçlar
        BuildTrees(world, terrain);
    }

    /// <summary>Oyuncak Kasaba'nın sokaklarına Mert'in oyuncak arabasını yerleştirir (Blender'daki ArabaNoktasi boş noktalarına).</summary>
    static void TownCars(GameObject town, bool collide)
    {
        var cols = new[] { new Color(0.86f, 0.18f, 0.16f), new Color(0.20f, 0.45f, 0.85f), new Color(0.97f, 0.80f, 0.28f), new Color(0.30f, 0.65f, 0.40f) };
        for (int i = 0; i < 12; i++)
        {
            var a = Find(town, "ArabaNoktasi_" + i);
            var b = Find(town, "ArabaYonu_" + i);
            if (a == null || b == null) break;
            Vector3 d = b.position - a.position; d.y = 0f;
            float len = d.magnitude * 2f;                       // dünya ölçeğinde araba boyu
            var car = PlaceModel("OyuncakAraba", town.transform, Vector3.zero, 0f, 1f, collide, false);
            car.name = "Kasaba Arabası " + (i + 1);
            car.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
            car.transform.localScale = Vector3.one * (len / 5.9f) / town.transform.lossyScale.x;
            car.transform.position = a.position;
            var rs = car.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                car.transform.position += Vector3.up * (a.position.y - bb.min.y);
            }
            SwapMaterial(car, "M_Araba_Boya", Mat("Araba_Boya_Kasaba_" + i, cols[i % cols.Length], null, 0.8f));
            HideChildren(car, "Kurma_Anahtari");
        }
    }

    static Transform Find(GameObject root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static void HideChildren(GameObject root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) t.gameObject.SetActive(false);
    }

    /// <summary>Modelin önüne (boş noktanın olduğu tarafa) bakan yatay yön.</summary>
    static Vector3 FrontDir(Transform model, Vector3 pointInFront)
    {
        Vector3 d = pointInFront - model.position; d.y = 0f;
        return d.sqrMagnitude < 1e-4f ? Vector3.back : d.normalized;
    }

    static TextMeshPro WorldText(Transform parent, string name, string text, Vector3 pos, Vector3 facing, float size, Color color, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = pos + facing * 0.03f;
        go.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
        go.transform.localScale = Vector3.one;
        // ebeveyn ölçeği yazıyı bozmasın
        Vector3 ls = go.transform.lossyScale;
        go.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.font = font;
        t.fontSize = size * 10f;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMin = 1f;
        t.fontSizeMax = size * 10f;
        t.rectTransform.sizeDelta = new Vector2(width, size * 2.4f * (1 + text.Split('\n').Length - 1));
        return t;
    }

    static GameObject Pedestal(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos + Vector3.up * size.y * 0.5f;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = Mat("Tas_Kaide", new Color(0.74f, 0.71f, 0.66f), null, 0.15f);
        var trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(trim.GetComponent<Collider>());
        trim.name = "Altın Şerit";
        trim.transform.SetParent(go.transform, false);
        trim.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        trim.transform.localScale = new Vector3(1.03f, 0.08f, 1.03f);
        var g = Mat("Altin_Parlak", new Color(1f, 0.78f, 0.3f), null, 0.7f);
        g.SetFloat("_Metallic", 0.55f);
        trim.GetComponent<Renderer>().sharedMaterial = g;
        return go;
    }

    static void LampLights(GameObject model, float intensity, float range)
    {
        foreach (var t in model.GetComponentsInChildren<Transform>())
            if (t.name.StartsWith("IsikNoktasi"))
            {
                var l = t.gameObject.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.84f, 0.58f);
                l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            }
    }

    static void BuildClock(Transform parent, Vector3 pos, Vector3 facing)
    {
        var body = new GameObject("Turnuva Saati");
        body.transform.SetParent(parent, true);
        body.transform.position = pos + facing * 0.08f;
        body.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
        var clock = body.AddComponent<TurnuvaSaati>();
        clock.body = body.transform;
        var dark = Mat("Saat_Koyu", new Color(0.12f, 0.2f, 0.16f), null, 0.4f);
        for (int i = 0; i < 12; i++)
        {
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(mark.GetComponent<Collider>());
            mark.transform.SetParent(body.transform, false);
            float ang = i * 30f * Mathf.Deg2Rad;
            mark.transform.localPosition = new Vector3(Mathf.Sin(ang) * 0.66f, Mathf.Cos(ang) * 0.66f, -0.01f);
            mark.transform.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
            mark.transform.localScale = i % 3 == 0 ? new Vector3(0.07f, 0.2f, 0.02f) : new Vector3(0.04f, 0.11f, 0.02f);
            mark.GetComponent<Renderer>().sharedMaterial = dark;
        }
        var pivot = new GameObject("Kol Pivot");
        pivot.transform.SetParent(body.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 0f, -0.04f);
        var hand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(hand.GetComponent<Collider>());
        hand.transform.SetParent(pivot.transform, false);
        hand.transform.localPosition = new Vector3(0f, 0.28f, 0f);
        hand.transform.localScale = new Vector3(0.06f, 0.62f, 0.02f);
        hand.GetComponent<Renderer>().sharedMaterial = Mat("Saat_Kol", new Color(0.88f, 0.22f, 0.2f), null, 0.5f);
        var hub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(hub.GetComponent<Collider>());
        hub.transform.SetParent(pivot.transform, false);
        hub.transform.localScale = Vector3.one * 0.12f;
        hub.GetComponent<Renderer>().sharedMaterial = dark;
        clock.hand = pivot.transform;
        // tepe yazısı
        var t = WorldText(body.transform, "60 sn", "60 SN", pos + Vector3.up * 1.15f, facing, 0.3f, UiGold, 2f);
        t.enableAutoSizing = false;
    }

    static List<Vector3> RingPoints(float half, float corner, int n)
    {
        // Yuvarlatılmış karenin çevresi boyunca eşit aralıklı noktalar (saat yönünün tersine)
        var pts = new List<Vector3>();
        float straight = 2f * (half - corner);
        float arc = Mathf.PI * 0.5f * corner;
        float per = 4f * (straight + arc);
        for (int i = 0; i < n; i++)
        {
            float s = per * i / n;
            int side = Mathf.FloorToInt(s / (straight + arc));
            float u = s - side * (straight + arc);
            Vector2 p;
            if (u < straight) p = new Vector2(half, -(half - corner) + u);
            else
            {
                float a = (u - straight) / corner;
                p = new Vector2(half - corner + Mathf.Cos(a) * corner, (half - corner) + Mathf.Sin(a) * corner);
            }
            // yanı döndür (0: doğu, 1: kuzey, 2: batı, 3: güney)
            for (int k = 0; k < side; k++) p = new Vector2(-p.y, p.x);
            pts.Add(new Vector3(p.x, 0f, p.y));
        }
        return pts;
    }

    static void RopeLine(Transform parent, Terrain terrain, Vector3 a, Vector3 b, Material postM, Material ropeM)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / 3.5f));
        Vector3 prev = Vector3.zero;
        for (int i = 0; i <= n; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, i / (float)n);
            p.y = Ground(terrain, p.x, p.z);
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.name = "İp Direği";
            post.transform.SetParent(parent);
            post.transform.position = p + Vector3.up * 0.5f;
            post.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);
            post.GetComponent<Renderer>().sharedMaterial = postM;
            Vector3 top = p + Vector3.up * 0.9f;
            if (i > 0)
            {
                var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(rope.GetComponent<Collider>());
                rope.name = "İp";
                rope.transform.SetParent(parent);
                Vector3 mid = (prev + top) * 0.5f - Vector3.up * 0.06f;
                rope.transform.position = mid;
                rope.transform.rotation = Quaternion.FromToRotation(Vector3.up, (top - prev).normalized);
                rope.transform.localScale = new Vector3(0.04f, Vector3.Distance(prev, top) * 0.5f, 0.04f);
                rope.GetComponent<Renderer>().sharedMaterial = ropeM;
            }
            prev = top;
        }
    }

    static GameObject MakeSteam(Transform parent, float scale)
    {
        var steam = new GameObject("Buhar");
        steam.transform.SetParent(parent, false);
        steam.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = steam.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.playOnAwake = true;
        main.startLifetime = 2.6f; main.startSpeed = 0.35f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f); main.maxParticles = 30;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 1f, 1f, 0.6f);
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 7f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10f; sh.radius = 0.04f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        steam.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Mat_Buhar", "fx_puff", false);
        return steam;
    }

    static void BuildPond(Transform world, Terrain terrain)
    {
        var root = new GameObject("Gölet (Fıskiyeli)").transform;
        root.SetParent(world);
        float waterY = -0.5f;
        // Su yüzeyi
        var water = new GameObject("Su");
        water.transform.SetParent(root);
        water.transform.position = new Vector3(PondCenter.x, waterY, PondCenter.y);
        water.AddComponent<MeshFilter>().sharedMesh = MeshAsset("SuDiski", () => MakeDisc(PondRadius + 4f, 64));
        var wm = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Su.mat");
        if (wm == null) { wm = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(wm, Root + "/Materials/Su.mat"); }
        wm.SetColor("_BaseColor", new Color(0.22f, 0.50f, 0.62f, 0.86f));
        wm.SetFloat("_Smoothness", 0.95f);
        wm.SetFloat("_Metallic", 0.1f);
        wm.SetTexture("_BumpMap", Tex("water_normal"));
        wm.SetFloat("_BumpScale", 0.35f);
        wm.SetTextureScale("_BumpMap", new Vector2(4f, 4f));
        wm.EnableKeyword("_NORMALMAP");
        wm.SetFloat("_Surface", 1f); wm.SetFloat("_Blend", 0f);
        wm.SetOverrideTag("RenderType", "Transparent");
        wm.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); wm.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); wm.SetInt("_ZWrite", 0);
        wm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); wm.renderQueue = 3000;
        EditorUtility.SetDirty(wm);
        var mr = water.AddComponent<MeshRenderer>();
        mr.sharedMaterial = wm;
        mr.shadowCastingMode = ShadowCastingMode.Off;

        // Kenar: kayalar ve görünmez korkuluk (top ve oyuncu göle düşmesin)
        var rnd = new System.Random(4);
        int seg = 28;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
            Vector2 p0 = PondCenter + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * (PondEdge(a0) + 1.0f);
            Vector2 p1 = PondCenter + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * (PondEdge(a1) + 1.0f);
            var wall = new GameObject("Kenar Engeli");
            wall.transform.SetParent(root);
            Vector2 mid = (p0 + p1) * 0.5f;
            wall.transform.position = new Vector3(mid.x, 0.8f, mid.y);
            Vector2 d = p1 - p0;
            wall.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0f, d.y));
            wall.AddComponent<BoxCollider>().size = new Vector3(0.6f, 2.4f, d.magnitude + 0.4f);
            if (i % 3 == 0)
            {
                Vector2 rp = PondCenter + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * (PondEdge(a0) + 0.6f);
                var rock = PlaceModel("Kayalar", root, new Vector3(rp.x, Ground(terrain, rp.x, rp.y) - 0.15f, rp.y), (float)rnd.NextDouble() * 360f, 0.8f + (float)rnd.NextDouble() * 0.6f, false, false);
                rock.transform.position = new Vector3(rp.x, Ground(terrain, rp.x, rp.y) - 0.2f, rp.y);
            }
        }

        // Fıskiye ve taş havuzu
        var basin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(basin.GetComponent<Collider>());
        basin.name = "Fıskiye Taşı";
        basin.transform.SetParent(root);
        basin.transform.position = new Vector3(PondCenter.x, waterY, PondCenter.y);
        basin.transform.localScale = new Vector3(1.6f, 0.25f, 1.6f);
        basin.GetComponent<Renderer>().sharedMaterial = Mat("Tas_Kaide", new Color(0.74f, 0.71f, 0.66f), null, 0.15f);
        var fount = new GameObject("Fıskiye");
        fount.transform.SetParent(root);
        fount.transform.position = new Vector3(PondCenter.x, waterY + 0.3f, PondCenter.y);
        fount.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = fount.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.gravityModifier = 1f;
        main.maxParticles = 600;
        main.startColor = new Color(0.92f, 0.97f, 1f, 0.85f);
        var em = ps.emission; em.rateOverTime = 260f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 11f; sh.radius = 0.2f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.93f, 1f), 1f) },
                  new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        fount.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Mat_Su", "fx_soft", false);
        var src = fount.AddComponent<AudioSource>();
        src.clip = Clip("fountain_loop"); src.loop = true; src.playOnAwake = true; src.volume = 0.5f;
        src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 4f; src.maxDistance = 30f;
    }

    static bool TreeAllowed(Vector2 p)
    {
        if (SdRoundRect(p, RingHalf, RingCorner) < 4f) return false;        // saha + yol
        if (SdPaths(p) < 3f) return false;
        if (SdPond(p) < 4f) return false;
        if (SdBox(p, new Vector2(ClubPos.x - 3f, ClubPos.z), new Vector2(17f, 9f)) < 3f) return false;
        if (SdBox(p, new Vector2(ScoreboardPos.x, ScoreboardPos.z), new Vector2(6f, 3f)) < 3f) return false;
        if (Vector2.Distance(p, new Vector2(TrophyPos.x, TrophyPos.z)) < 7f) return false;
        if (SdBox(p, ParkingCenter, ParkingHalf) < 3f) return false;
        if (SdBox(p, new Vector2(TownPos.x, TownPos.z), new Vector2(25f, 11f)) < 3f) return false;
        if (Vector2.Distance(p, new Vector2(10f, -36.5f)) < 5f) return false;
        if (Mathf.Abs(p.x) < 7f && p.y < -28f) return false;
        return true;
    }

    static void BuildTrees(Transform world, Terrain terrain)
    {
        var root = new GameObject("Ağaçlar").transform;
        root.SetParent(world);
        var rnd = new System.Random(21);
        string[] kinds = { "Agac_Mese", "Agac_Mese", "Agac_Kavak", "Agac_Cam" };
        int placed = 0;
        var used = new List<Vector2>();
        for (int tries = 0; tries < 4000 && placed < 120; tries++)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float r = 36f + (float)rnd.NextDouble() * 52f;
            var p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            if (Mathf.Abs(p.x) > 88f || Mathf.Abs(p.y) > 88f) continue;
            if (!TreeAllowed(p)) continue;
            bool near = false;
            foreach (var u in used) if ((u - p).sqrMagnitude < 20f) { near = true; break; }
            if (near) continue;
            used.Add(p);
            string kind = kinds[rnd.Next(kinds.Length)];
            float s = 0.85f + (float)rnd.NextDouble() * 0.6f;
            var t = PlaceModel(kind, root, new Vector3(p.x, Ground(terrain, p.x, p.y) - 0.05f, p.y), (float)rnd.NextDouble() * 360f, s, false, false);
            var col = t.AddComponent<CapsuleCollider>();
            col.radius = 0.35f; col.height = 4f; col.center = new Vector3(0f, 2f, 0f);
            placed++;
        }
        // Kasaba yolunda kavak sırası
        for (float z = -34f; z > -62f; z -= 6f)
            foreach (float x in new[] { -5.5f, 5.5f })
            {
                if (Vector2.Distance(new Vector2(x, z), new Vector2(10f, -36.5f)) < 4f) continue;
                if (Mathf.Abs(z - GatePos.z) < 3f) continue;
                PlaceModel("Agac_Kavak", root, new Vector3(x, Ground(terrain, x, z) - 0.05f, z), z * 13f, 0.9f, false, false);
            }
    }

    // =====================================================================
    //  MODELLER (FBX + materyal JSON)
    // =====================================================================
    [System.Serializable] class MatDef { public string name; public float r, g, b, smooth, metal, er, eg, eb, es; }
    [System.Serializable] class MatList { public MatDef[] materials; }

    static readonly string[] ModelNames =
    {
        // Mert'in Blender klasöründen geliştirilen modeller
        "OyuncakKasaba", "OyuncakAraba", "KakaoKupasi", "KartonFigur", "DevMankenEli", "SallayanEl",
        // Golf sahası için Blender'da üretilen modeller
        "KulupBinasi", "GolfArabasi", "AltinKupa", "TerasMasasi", "Bank", "SkorTabelasi", "GirisKapisi",
        "RobotCimBicici", "Kayalar", "Agac_Mese", "Agac_Kavak", "Agac_Cam",
    };

    public static void SetupModels()
    {
        EnsureDir(Root + "/Materials/Model");
        foreach (var n in ModelNames)
        {
            string p = Root + "/Models/" + n + ".fbx";
            var mi = AssetImporter.GetAtPath(p) as ModelImporter;
            if (mi == null) { Debug.LogWarning("Model yok: " + p); continue; }
            bool anim = n == "DevMankenEli" || n == "SallayanEl";
            mi.animationType = anim ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            mi.importAnimation = anim;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            string jp = Root + "/Models/" + n + "_materials.json";
            if (File.Exists(jp))
            {
                var list = JsonUtility.FromJson<MatList>(File.ReadAllText(jp));
                foreach (var d in list.materials)
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), d.name), ModelMat(d));
            }
            mi.SaveAndReimport();
            if (anim)
            {
                var clips = mi.defaultClipAnimations;
                foreach (var c in clips) { c.loopTime = true; c.name = n + "_Anim"; }
                mi.clipAnimations = clips;
                mi.SaveAndReimport();
            }
        }
    }

    /// <summary>Golfçü karakter: hocanın modeli + şapka/yaka (Blender). Humanoid ayarları eski karakterden kopyalanır.</summary>
    static void SetupCharacter()
    {
        string newPath = Root + "/Characters/Karakter_Golfcu.fbx";
        var ni = AssetImporter.GetAtPath(newPath) as ModelImporter;
        var oi = AssetImporter.GetAtPath(Root + "/Characters/Karakter_Rigged.fbx") as ModelImporter;
        if (ni == null) { Debug.LogWarning("Golfçü karakter bulunamadı, eski karakter kullanılacak."); return; }
        if (ni.animationType == ModelImporterAnimationType.Human && ni.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel && ni.humanDescription.human != null && ni.humanDescription.human.Length > 10) return;
        ni.animationType = ModelImporterAnimationType.Human;
        ni.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        if (oi != null) ni.humanDescription = oi.humanDescription;
        ni.importAnimation = false;
        ni.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        ni.SaveAndReimport();
    }

    static void SetupTextures()
    {
        foreach (var n in new[] { "golfball_normal", "water_normal" })
        {
            var ti = AssetImporter.GetAtPath(Root + "/Textures/" + n + ".png") as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        }
        foreach (var n in new[] { "icon_flag", "icon_clock", "icon_ball", "ui_trophy", "ui_panel", "ui_button", "ui_dot", "ui_arrow" })
        {
            var ti = AssetImporter.GetAtPath(Root + "/Textures/" + n + ".png") as TextureImporter;
            if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }
        foreach (var n in new[] { "robot_ring" })
        {
            var ti = AssetImporter.GetAtPath(Root + "/Textures/" + n + ".png") as TextureImporter;
            if (ti != null && (!ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp)) { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        }
        foreach (var n in new[] { "golf_fairway", "golf_rough", "golf_green", "golf_sand", "golf_path" })
        {
            var ti = AssetImporter.GetAtPath(Root + "/Textures/" + n + ".png") as TextureImporter;
            if (ti != null && ti.anisoLevel < 4) { ti.anisoLevel = 8; ti.SaveAndReimport(); }
        }
        for (int i = 1; i <= 10; i++)
        {
            var ti = AssetImporter.GetAtPath(Root + "/Textures/flag_" + i.ToString("00") + ".png") as TextureImporter;
            if (ti != null && ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        }
    }

    static Material ModelMat(MatDef d)
    {
        string path = Root + "/Materials/Model/" + d.name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", new Color(d.r, d.g, d.b, 1f));
        m.SetFloat("_Smoothness", Mathf.Clamp01(d.smooth * 0.9f));
        // Gerçek zamanlı yansıma olmadan da parlak görünsün: metalleri yarı metal yap
        m.SetFloat("_Metallic", d.metal > 0.5f ? 0.55f : d.metal);
        if (d.metal > 0.5f) m.SetFloat("_Smoothness", 0.6f);
        m.enableInstancing = true;
        if (d.es > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(d.er, d.eg, d.eb) * Mathf.Min(3f, d.es * 0.6f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject PlaceModel(string n, Transform parent, Vector3 pos, float yRot, float scale, bool collide, bool snapToGround = true)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + n + ".fbx");
        var wrap = new GameObject(n);
        wrap.transform.SetParent(parent, false);
        wrap.transform.localPosition = pos;
        wrap.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        wrap.transform.localScale = Vector3.one * scale;
        if (prefab == null) { Debug.LogWarning("Model yok: " + n); return wrap; }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.SetParent(wrap.transform, false);
        if (snapToGround)
        {
            var rs = inst.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                inst.transform.position += Vector3.up * (wrap.transform.position.y - b.min.y);
            }
        }
        if (collide)
        {
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            foreach (var r in inst.GetComponentsInChildren<Renderer>()) GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
        }
        return wrap;
    }

    static void AnimateModel(GameObject wrap, string n)
    {
        var anim = wrap.GetComponentInChildren<Animator>();
        if (anim == null) anim = wrap.transform.GetChild(0).gameObject.AddComponent<Animator>();
        string cpath = Root + "/Animations/" + n + ".controller";
        AnimationClip clip = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Root + "/Models/" + n + ".fbx"))
            if (o is AnimationClip c && !c.name.StartsWith("__preview")) clip = c;
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(cpath);
        if (ctrl == null && clip != null) ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(cpath, clip);
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        foreach (var smr in wrap.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
    }

    static void SwapMaterial(GameObject wrap, string prefix, Material m)
    {
        foreach (var r in wrap.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].name.StartsWith(prefix)) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }

    static void AddBoxCollider(GameObject wrap, float shrink = 0.9f)
    {
        var rs = wrap.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        var bc = wrap.AddComponent<BoxCollider>();
        bc.center = wrap.transform.InverseTransformPoint(b.center);
        var s = wrap.transform.lossyScale;
        bc.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z) * shrink;
    }

    // =====================================================================
    //  POST PROCESS + GÖKYÜZÜ
    // =====================================================================
    static void BuildPostProcess(string name)
    {
        var vgo = new GameObject("Global Volume (Güneşli Öğleden Sonra)");
        var vol = vgo.AddComponent<Volume>();
        vol.isGlobal = true;
        string path = GenDir + "/" + name + ".asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        Bloom bloom; if (!profile.TryGet(out bloom)) { bloom = profile.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(bloom, profile); }
        bloom.intensity.Override(0.35f);
        bloom.threshold.Override(1.1f);
        bloom.scatter.Override(0.65f);
        Vignette vig; if (!profile.TryGet(out vig)) { vig = profile.Add<Vignette>(true); AssetDatabase.AddObjectToAsset(vig, profile); }
        vig.intensity.Override(0.2f);
        vig.smoothness.Override(0.45f);
        vig.color.Override(new Color(0.04f, 0.08f, 0.05f));
        ColorAdjustments ca; if (!profile.TryGet(out ca)) { ca = profile.Add<ColorAdjustments>(true); AssetDatabase.AddObjectToAsset(ca, profile); }
        ca.saturation.Override(4f);
        ca.contrast.Override(10f);
        ca.postExposure.Override(0f);
        ca.colorFilter.Override(Color.white);
        WhiteBalance wb; if (!profile.TryGet(out wb)) { wb = profile.Add<WhiteBalance>(true); AssetDatabase.AddObjectToAsset(wb, profile); }
        wb.temperature.Override(6f);
        Tonemapping tm; if (!profile.TryGet(out tm)) { tm = profile.Add<Tonemapping>(true); AssetDatabase.AddObjectToAsset(tm, profile); }
        tm.mode.Override(TonemappingMode.Neutral);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        vol.sharedProfile = profile;
    }

    static Material MakeSkybox()
    {
        string path = GenDir + "/GolfGokyuzu.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_SunSize", 0.035f);
        m.SetFloat("_SunSizeConvergence", 6f);
        m.SetFloat("_AtmosphereThickness", 0.9f);
        m.SetColor("_SkyTint", new Color(0.45f, 0.58f, 0.80f));
        m.SetColor("_GroundColor", new Color(0.52f, 0.60f, 0.50f));
        m.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // =====================================================================
    //  ARAYÜZ (Oyun)
    // =====================================================================
    const float MiniMapHalf = 34f;
    static Sprite miniMapSprite;

    static void BuildGameUI(GameManager gm, AudioSource sfx)
    {
        var canvas = MakeCanvas("Arayüz (HUD)");
        var click = Clip("click");

        // Süre
        var timerBox = Panel(canvas.transform, "Süre Kutusu", UiGreen, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(250f, 130f));
        Icon(timerBox.transform, "icon_clock", new Vector2(-82f, 36f), 34f, UiGold);
        var tl = MakeText(timerBox.transform, "Etiket", "SÜRE", 28, UiGold, new Vector2(14f, 36f), new Vector2(160f, 40f));
        gm.timerLabel = tl;
        gm.timerText = MakeText(timerBox.transform, "Süre", "60", 76, Color.white, new Vector2(0f, -16f), new Vector2(240f, 90f));
        gm.timerNormalColor = Color.white;
        gm.timerDangerColor = new Color(1f, 0.45f, 0.38f);

        // Skor
        var scoreBox = Panel(canvas.transform, "Skor Kutusu", UiGreen, new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(300f, 130f));
        Icon(scoreBox.transform, "icon_flag", new Vector2(-112f, 36f), 34f, UiGold);
        MakeText(scoreBox.transform, "Etiket", "DELİKTEKİ TOP", 26, UiGold, new Vector2(16f, 36f), new Vector2(240f, 40f));
        gm.scoreText = MakeText(scoreBox.transform, "Skor", "0 / 10", 64, Color.white, new Vector2(0f, -16f), new Vector2(280f, 90f));

        // İpucu ve isim
        var hint = MakeText(canvas.transform, "Kontroller", "WASD / Yön Tuşları: Yürü     ESC / P: Duraklat", 26, Color.white, Vector2.zero, new Vector2(800f, 40f));
        Anchor(hint.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -30f));
        hint.alignment = TextAlignmentOptions.Right;
        hint.outlineWidth = 0.22f; hint.outlineColor = new Color(0.05f, 0.18f, 0.1f);
        gm.hintText = hint;

        // Mini harita (sağ alt)
        var mapBox = Panel(canvas.transform, "Mini Harita", UiGreen, new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(290f, 318f));
        MakeText(mapBox.transform, "Etiket", "SAHA", 24, UiGold, new Vector2(0f, 136f), new Vector2(260f, 34f));
        var mapImg = new GameObject("Harita", typeof(RectTransform), typeof(Image));
        mapImg.transform.SetParent(mapBox.transform, false);
        var mi = mapImg.GetComponent<Image>();
        mi.sprite = miniMapSprite;
        mi.raycastTarget = false;
        var mrt = mapImg.GetComponent<RectTransform>();
        mrt.anchoredPosition = new Vector2(0f, -14f);
        mrt.sizeDelta = new Vector2(262f, 262f);
        var mh = mapBox.AddComponent<MiniHarita>();
        mh.area = mrt;
        mh.dotSprite = SpriteAt("ui_dot");
        mh.arrowSprite = SpriteAt("ui_arrow");
        mh.flagSprite = SpriteAt("icon_flag");
        mh.worldHalfSize = MiniMapHalf;
        var credit = MakeText(canvas.transform, "Hazırlayan", "Mert Demir  •  2506127004  •  3B Oyun Geliştirme Stüdyosu", 22, new Color(1f, 1f, 1f, 0.92f), Vector2.zero, new Vector2(900f, 34f));
        Anchor(credit.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 16f));
        credit.alignment = TextAlignmentOptions.Left;
        credit.outlineWidth = 0.22f; credit.outlineColor = new Color(0.05f, 0.18f, 0.1f);

        // Orta mesaj
        var msg = MakeText(canvas.transform, "Mesaj", "", 56, Color.white, Vector2.zero, new Vector2(1500f, 90f));
        Anchor(msg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f));
        msg.outlineWidth = 0.3f; msg.outlineColor = new Color(0.06f, 0.24f, 0.13f);
        gm.messageText = msg;

        // Kazanma paneli (altın kupa)
        var win = Overlay(canvas.transform, "Kazanma Paneli");
        var winCard = Panel(win.transform, "Kart", new Color(0.99f, 0.98f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 560f));
        var trophy = Icon(winCard.transform, "ui_trophy", new Vector2(-330f, 30f), 300f, Color.white);
        trophy.preserveAspect = true;
        MakeText(winCard.transform, "Başlık", "TEBRİKLER!", 92, new Color(0.85f, 0.6f, 0.12f), new Vector2(110f, 190f), new Vector2(640f, 110f));
        gm.winDetailText = MakeText(winCard.transform, "Detay", "", 32, CardText, new Vector2(110f, 30f), new Vector2(640f, 190f));
        var b1 = MakeButton(winCard.transform, "Tekrar Oyna", BtnGreen, new Vector2(-50f, -175f), new Vector2(320f, 100f), sfx, click);
        UnityEventTools.AddPersistentListener(b1.onClick, gm.Restart);
        var b2 = MakeButton(winCard.transform, "Ana Menü", BtnNavy, new Vector2(290f, -175f), new Vector2(320f, 100f), sfx, click);
        UnityEventTools.AddPersistentListener(b2.onClick, gm.GoToMenu);
        gm.winPanel = win;
        win.SetActive(false);

        // Kaybetme paneli
        var lose = Overlay(canvas.transform, "Süre Doldu Paneli");
        var loseCard = Panel(lose.transform, "Kart", new Color(0.99f, 0.98f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 520f));
        MakeText(loseCard.transform, "Başlık", "SÜRE DOLDU!", 96, BtnRed, new Vector2(0f, 150f), new Vector2(820f, 120f));
        gm.loseDetailText = MakeText(loseCard.transform, "Detay", "", 36, CardText, new Vector2(0f, 20f), new Vector2(780f, 140f));
        var b3 = MakeButton(loseCard.transform, "Tekrar Dene", BtnGreen, new Vector2(-190f, -160f), new Vector2(330f, 100f), sfx, click);
        UnityEventTools.AddPersistentListener(b3.onClick, gm.Restart);
        var b4 = MakeButton(loseCard.transform, "Ana Menü", BtnNavy, new Vector2(190f, -160f), new Vector2(330f, 100f), sfx, click);
        UnityEventTools.AddPersistentListener(b4.onClick, gm.GoToMenu);
        gm.losePanel = lose;
        lose.SetActive(false);

        // Duraklatma paneli
        var pauseMenu = canvas.gameObject.AddComponent<PauseMenu>();
        var pause = Overlay(canvas.transform, "Duraklatma Paneli");
        var pCard = Panel(pause.transform, "Kart", new Color(0.99f, 0.98f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 600f));
        MakeText(pCard.transform, "Başlık", "DURAKLATILDI", 72, new Color(0.10f, 0.36f, 0.22f), new Vector2(0f, 200f), new Vector2(600f, 100f));
        var p1 = MakeButton(pCard.transform, "Devam Et", BtnGreen, new Vector2(0f, 60f), new Vector2(400f, 96f), sfx, click);
        UnityEventTools.AddPersistentListener(p1.onClick, pauseMenu.Resume);
        var p2 = MakeButton(pCard.transform, "Yeniden Başla", BtnGold, new Vector2(0f, -60f), new Vector2(400f, 96f), sfx, click);
        UnityEventTools.AddPersistentListener(p2.onClick, pauseMenu.RestartGame);
        var p3 = MakeButton(pCard.transform, "Ana Menü", BtnNavy, new Vector2(0f, -180f), new Vector2(400f, 96f), sfx, click);
        UnityEventTools.AddPersistentListener(p3.onClick, pauseMenu.BackToMenu);
        pauseMenu.pausePanel = pause;
        pause.SetActive(false);

        MakeEventSystem();
    }

    // =====================================================================
    //  ANA MENÜ SAHNESİ: dönen putting green adası
    // =====================================================================
    public static void BuildMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupAtmosphere(40f, 140f);

        var sun = new GameObject("Güneş").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.35f;
        sun.color = new Color(1f, 0.95f, 0.86f);
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        RenderSettings.sun = sun;

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 45f;
        cam.farClipPlane = 300f;
        camGo.AddComponent<AudioListener>();
        var cad = camGo.AddComponent<UniversalAdditionalCameraData>();
        cad.renderPostProcessing = true;
        cad.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        camGo.transform.position = new Vector3(-4.5f, 5.6f, -16.5f);
        camGo.transform.rotation = Quaternion.Euler(12f, 3f, 0f);
        BuildPostProcess("GolfPostProcess");

        // Arka plan: geniş çimen + kulüp binası + ağaçlar
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Çimen";
        ground.transform.position = new Vector3(0f, -0.62f, 40f);
        ground.transform.localScale = new Vector3(40f, 1f, 40f);
        var gm = Mat("Menu_Rough", Color.white, Tex("golf_rough"), 0.05f);
        gm.SetTextureScale("_BaseMap", new Vector2(40f, 40f));
        ground.GetComponent<Renderer>().sharedMaterial = gm;
        var bg = new GameObject("Arka Plan").transform;
        PlaceModel("KulupBinasi", bg, new Vector3(-4f, -0.62f, 40f), 180f, 1f, false, false);
        var rnd = new System.Random(3);
        string[] kinds = { "Agac_Mese", "Agac_Kavak", "Agac_Cam" };
        for (int i = 0; i < 26; i++)
        {
            float x = -55f + i * 4.4f + (float)rnd.NextDouble() * 2f;
            float z = 52f + (float)rnd.NextDouble() * 18f;
                if (Mathf.Abs(x + 4f) < 15f && z < 60f) z += 14f;
            if (Mathf.Abs(x - 13.5f) < 6f && z < 62f) z += 12f;
            PlaceModel(kinds[rnd.Next(3)], bg, new Vector3(x, -0.62f, z), (float)rnd.NextDouble() * 360f, 0.9f + (float)rnd.NextDouble() * 0.5f, false, false);
        }
        foreach (var x in new[] { -18f, -26f })
            PlaceModel(kinds[rnd.Next(3)], bg, new Vector3(x, -0.62f, 18f + (float)rnd.NextDouble() * 10f), (float)rnd.NextDouble() * 360f, 1.1f, false, false);
        var handPos = new Vector3(13.5f, -0.62f, 17f);
        var hped = Pedestal(bg, "El Kaidesi", handPos, new Vector3(2.4f, 0.8f, 2.4f));
        var wave = Face("SallayanEl", bg, handPos + Vector3.up * 0.8f, YawToward(handPos, camGo.transform.position), 1.2f, false);
        AnimateModel(wave, "SallayanEl");
        // Oyuncak Kasaba (Mahalle.blend) arka planda
        var menuTown = PlaceModel("OyuncakKasaba", bg, new Vector3(17f, -0.62f, 36f), -18f, 0.42f, false);
        TownCars(menuTown, false);

        // Dönen putting green adası
        var display = new GameObject("Putting Green Adası");
        display.transform.position = new Vector3(0f, 0f, 4f);
        var spin = display.AddComponent<YavasDon>();
        spin.rotationSpeed = new Vector3(0f, 7f, 0f);
        var island = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        island.name = "Ada";
        island.transform.SetParent(display.transform, false);
        island.transform.localScale = new Vector3(19f, 0.3f, 19f);
        island.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        var fm = Mat("Menu_Fairway", Color.white, Tex("golf_fairway"), 0.15f);
        fm.SetTextureScale("_BaseMap", new Vector2(1.2f, 1.2f));
        island.GetComponent<Renderer>().sharedMaterial = fm;
        var menuLip = new GameObject("Kap Dudağı");
        menuLip.transform.SetParent(display.transform, false);
        menuLip.transform.localPosition = new Vector3(3f, 0.04f, 2f);
        menuLip.transform.localScale = Vector3.one * (0.8f / (HoleRadius - 0.012f));
        menuLip.AddComponent<MeshFilter>().sharedMesh = MeshAsset("DelikDudak", () => MakeTorus(HoleRadius - 0.012f, 0.03f, 64, 10));
        menuLip.AddComponent<MeshRenderer>().sharedMaterial = Mat("Delik_Dudak", new Color(0.97f, 0.97f, 0.96f), null, 0.6f);
        var holeDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(holeDisc.GetComponent<Collider>());
        holeDisc.transform.SetParent(display.transform, false);
        holeDisc.transform.localScale = new Vector3(1.6f, 0.02f, 1.6f);
        holeDisc.transform.localPosition = new Vector3(3f, 0.03f, 2f);
        holeDisc.GetComponent<Renderer>().sharedMaterial = Mat("Delik_Ic", new Color(0.13f, 0.11f, 0.10f), null, 0.05f);
        // bayrak 1
        var flag = new GameObject("Bayrak");
        flag.transform.SetParent(display.transform, false);
        flag.transform.localPosition = new Vector3(4.4f, 0f, 2.3f);
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(pole.GetComponent<Collider>());
        pole.transform.SetParent(flag.transform, false);
        pole.transform.localScale = new Vector3(0.09f, 1.7f, 0.09f);
        pole.transform.localPosition = new Vector3(0f, 1.7f, 0f);
        pole.GetComponent<Renderer>().sharedMaterial = Mat("Bayrak_Direk", new Color(0.97f, 0.97f, 0.96f), null, 0.6f);
        var cloth = new GameObject("Kumaş");
        cloth.transform.SetParent(flag.transform, false);
        cloth.transform.localPosition = new Vector3(0.05f, 2.75f, 0f);
        cloth.AddComponent<MeshFilter>().sharedMesh = MeshAsset("BayrakKumas", MakeFlagQuad);
        cloth.AddComponent<MeshRenderer>().sharedMaterial = Mat("Bayrak_1", Color.white, Tex("flag_01"), 0.2f);
        cloth.AddComponent<BayrakDalga>();
        // golf topları
        var ballPos = new[] { new Vector3(0.6f, 0.8f, 3.6f), new Vector3(5.6f, 0.8f, -0.6f), new Vector3(-4.5f, 0.8f, -3.5f) };
        for (int i = 0; i < ballPos.Length; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(display.transform, false);
            b.transform.localScale = Vector3.one * 1.6f;
            b.transform.localPosition = ballPos[i];
            b.transform.localRotation = Quaternion.Euler(0f, i * 70f, 0f);
            b.GetComponent<Renderer>().sharedMaterial = GolfBallMat(i * 3);
        }
        // golfçü
        var golfer = SpawnGolfer(display.transform);
        golfer.transform.localPosition = new Vector3(-0.8f, 0f, -2.6f);
        golfer.transform.localRotation = Quaternion.Euler(0f, 160f, 0f);
        // golf arabası, kupa, robot, bank, ağaçlar
        PlaceModel("GolfArabasi", display.transform, new Vector3(-5.5f, 0f, 3.5f), 125f, 1f, false);
        var ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(ped.GetComponent<Collider>());
        ped.transform.SetParent(display.transform, false);
        ped.transform.localScale = new Vector3(1.3f, 0.35f, 1.3f);
        ped.transform.localPosition = new Vector3(6.2f, 0.35f, -3.6f);
        ped.GetComponent<Renderer>().sharedMaterial = Mat("Tas_Kaide", new Color(0.74f, 0.71f, 0.66f), null, 0.15f);
        PlaceModel("AltinKupa", display.transform, new Vector3(6.2f, 0.7f, -3.6f), 200f, 2.4f, false, false);
        PlaceModel("RobotCimBicici", display.transform, new Vector3(-2.5f, 0.75f, 6.5f), 210f, 1f, false, false);
        PlaceModel("Bank", display.transform, new Vector3(2f, 0f, 7.6f), 180f, 1f, false);
        // Mert'in modelleri adada: oyuncak araba, masada kakao kupası, kaidede ahşap manken eli
        var mcar = PlaceModel("OyuncakAraba", display.transform, new Vector3(-7.2f, 0f, 0.4f), 60f, 0.55f, false);
        SwapMaterial(mcar, "M_Araba_Boya", Mat("Araba_Boya_0", new Color(0.86f, 0.18f, 0.16f), null, 0.8f));
        var mtable = PlaceModel("TerasMasasi", display.transform, new Vector3(2.8f, 0f, -6.4f), 20f, 1f, false);
        var mmug = PlaceModel("KakaoKupasi", mtable.transform, new Vector3(0.05f, 0.77f, 0f), 30f, 0.2f, false);
        var mrs = mmug.GetComponentsInChildren<Renderer>();
        Bounds mb = mrs[0].bounds; foreach (var r in mrs) if (r.name.Contains("Kupa")) mb = r.bounds;
        var msteam = MakeSteam(mmug.transform, 0.2f);
        msteam.transform.position = new Vector3(mb.center.x, mb.max.y + 0.02f, mb.center.z);
        var mped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(mped.GetComponent<Collider>());
        mped.transform.SetParent(display.transform, false);
        mped.transform.localScale = new Vector3(1.4f, 0.3f, 1.4f);
        mped.transform.localPosition = new Vector3(7.4f, 0.3f, 3f);
        mped.GetComponent<Renderer>().sharedMaterial = Mat("Tas_Kaide", new Color(0.74f, 0.71f, 0.66f), null, 0.15f);
        var mhand = PlaceModel("DevMankenEli", display.transform, new Vector3(7.4f, 0.6f, 3f), 200f, 1.0f, false, false);
        AnimateModel(mhand, "DevMankenEli");

        // Karton figür (Kemik.blend): adada golfçünün yanında seyirci panosu
        var mfig = Face("KartonFigur", display.transform, new Vector3(-3.4f, 0f, -1.2f), 200f, 0.42f, false);
        SwapMaterial(mfig, "M_Karton_Gomlek", Mat("Karton_Gomlek_1", new Color(0.98f, 0.80f, 0.30f), null, 0.15f));
        mmug.transform.localScale = Vector3.one * 0.26f;

        // ---- Galeri sahnesi (Blender Modellerim): menüden uzakta, kendi ışığıyla
        var gal = BuildGalleryStage(out Transform turntable, out GameObject[] galModels);

        var music = new GameObject("Müzik").AddComponent<AudioSource>();
        music.clip = Clip("golf_music_loop");
        music.loop = true;
        music.volume = 0.42f;
        music.playOnAwake = true;
        var ambS = music.gameObject.AddComponent<AudioSource>();
        ambS.clip = Clip("golf_ambience"); ambS.loop = true; ambS.volume = 0.4f; ambS.playOnAwake = true;
        var sfx = new GameObject("Efekt").AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        var click = Clip("click");

        var canvas = MakeCanvas("Ana Menü Arayüzü");
        var mm = canvas.gameObject.AddComponent<MainMenu>();
        mm.gameSceneName = "Oyun";

        var menuUI = new GameObject("Menü İçeriği", typeof(RectTransform));
        menuUI.transform.SetParent(canvas.transform, false);
        var murt = menuUI.GetComponent<RectTransform>();
        murt.anchorMin = Vector2.zero; murt.anchorMax = Vector2.one; murt.offsetMin = Vector2.zero; murt.offsetMax = Vector2.zero;
        var mt = menuUI.transform;

        var title = MakeText(mt, "Başlık", "Altın Bayrak", 156, UiGold, Vector2.zero, new Vector2(1400f, 190f));
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f));
        title.outlineWidth = 0.28f; title.outlineColor = new Color(0.06f, 0.24f, 0.13f);
        var sub = MakeText(mt, "Alt Başlık", "Yeşiltepe Golf Kulübü  •  60 saniyede 10 dev topu 10 deliğe sok!", 36, Color.white, Vector2.zero, new Vector2(1600f, 60f));
        Anchor(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -240f));
        sub.outlineWidth = 0.25f; sub.outlineColor = new Color(0.06f, 0.24f, 0.13f);

        var bPlay = MakeButton(mt, "TURNUVA (60 sn)", BtnGreen, new Vector2(-560f, 85f), new Vector2(460f, 106f), sfx, click);
        UnityEventTools.AddPersistentListener(bPlay.onClick, mm.PlayGame);
        var bFree = MakeButton(mt, "Serbest Dolaşma", BtnNavy, new Vector2(-560f, -25f), new Vector2(460f, 92f), sfx, click);
        UnityEventTools.AddPersistentListener(bFree.onClick, mm.PlayFreeRoam);
        var bModels = MakeButton(mt, "Blender Modellerim", new Color(0.45f, 0.30f, 0.62f), new Vector2(-560f, -130f), new Vector2(460f, 92f), sfx, click);
        var bHow = MakeButton(mt, "Nasıl Oynanır", BtnGold, new Vector2(-560f, -235f), new Vector2(460f, 92f), sfx, click);
        UnityEventTools.AddPersistentListener(bHow.onClick, mm.OpenHowTo);
        var bQuit = MakeButton(mt, "Çıkış", BtnRed, new Vector2(-560f, -340f), new Vector2(460f, 92f), sfx, click);
        var best = MakeText(mt, "En İyi Süre", "", 30, UiGold, new Vector2(-560f, 175f), new Vector2(460f, 46f));
        best.outlineWidth = 0.25f; best.outlineColor = new Color(0.06f, 0.24f, 0.13f);
        mm.bestTimeText = best;
        UnityEventTools.AddPersistentListener(bQuit.onClick, mm.QuitGame);
        mm.quitButton = bQuit.gameObject;

        var credit = MakeText(canvas.transform, "Hazırlayan", "Hazırlayan: Mert Demir  •  Öğrenci No: 2506127004  •  3B Oyun Geliştirme Stüdyosu", 28, Color.white, Vector2.zero, new Vector2(1600f, 44f));
        Anchor(credit.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f));
        credit.outlineWidth = 0.25f; credit.outlineColor = new Color(0.06f, 0.24f, 0.13f);

        // Galeri arayüzü
        var galUI = new GameObject("Galeri Arayüzü", typeof(RectTransform));
        galUI.transform.SetParent(canvas.transform, false);
        var gurt = galUI.GetComponent<RectTransform>();
        gurt.anchorMin = Vector2.zero; gurt.anchorMax = Vector2.one; gurt.offsetMin = Vector2.zero; gurt.offsetMax = Vector2.zero;
        var gHead = MakeText(galUI.transform, "Başlık", "Blender Modellerim", 92, UiGold, Vector2.zero, new Vector2(1400f, 120f));
        Anchor(gHead.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f));
        gHead.outlineWidth = 0.28f; gHead.outlineColor = new Color(0.06f, 0.24f, 0.13f);
        var gSub = MakeText(galUI.transform, "Alt Başlık", "Mert Demir'in Blender klasöründen geliştirilip oyuna eklenen modeller", 32, Color.white, Vector2.zero, new Vector2(1600f, 50f));
        Anchor(gSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f));
        gSub.outlineWidth = 0.25f; gSub.outlineColor = new Color(0.06f, 0.24f, 0.13f);
        var info = Panel(galUI.transform, "Bilgi Kartı", new Color(0.99f, 0.98f, 0.95f, 0.96f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1320f, 300f));
        var gTitle = MakeText(info.transform, "Model Adı", "", 52, new Color(0.10f, 0.36f, 0.22f), new Vector2(0f, 104f), new Vector2(900f, 66f));
        var gCount = MakeText(info.transform, "Sayaç", "", 30, new Color(0.45f, 0.30f, 0.62f), new Vector2(570f, 108f), new Vector2(160f, 44f));
        var gDetail = MakeText(info.transform, "Açıklama", "", 26, CardText, new Vector2(0f, -32f), new Vector2(1250f, 200f));
        var bPrev = MakeButton(galUI.transform, "<", BtnGreen, new Vector2(-760f, 60f), new Vector2(130f, 130f), sfx, click);
        var bNext = MakeButton(galUI.transform, ">", BtnGreen, new Vector2(760f, 60f), new Vector2(130f, 130f), sfx, click);
        var bBack = MakeButton(galUI.transform, "Menüye Dön", BtnNavy, new Vector2(-740f, 440f), new Vector2(330f, 86f), sfx, click);

        var mg = canvas.gameObject.AddComponent<ModelGaleri>();
        mg.turntable = turntable;
        mg.models = galModels;
        mg.titles = GalleryTitles;
        mg.details = GalleryDetails;
        mg.titleText = gTitle; mg.detailText = gDetail; mg.counterText = gCount;
        mg.galleryUI = galUI; mg.menuUI = menuUI;
        mg.cam = camGo.transform;
        mg.galleryCamPos = GalleryPos + new Vector3(0f, 3.3f, -9.8f);
        mg.galleryLookAt = GalleryPos + new Vector3(0f, 0.55f, 0f);
        UnityEventTools.AddPersistentListener(bModels.onClick, mg.Open);
        UnityEventTools.AddPersistentListener(bPrev.onClick, mg.Prev);
        UnityEventTools.AddPersistentListener(bNext.onClick, mg.Next);
        UnityEventTools.AddPersistentListener(bBack.onClick, mg.Close);
        galUI.SetActive(false);

        // Nasıl oynanır paneli
        var how = Overlay(canvas.transform, "Nasıl Oynanır Paneli");
        var card = Panel(how.transform, "Kart", new Color(0.99f, 0.98f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1220f, 860f));
        MakeText(card.transform, "Başlık", "Nasıl Oynanır?", 70, new Color(0.10f, 0.36f, 0.22f), new Vector2(0f, 352f), new Vector2(1100f, 90f));
        var body = MakeText(card.transform, "Metin",
            "Yeşiltepe Golf Kulübü'nün <b>Altın Bayrak Turnuvası</b>'ndasın! Bu turnuvada sopa yok: dev golf toplarını elinle sürüp <b>60 saniye</b> içinde <b>10 numaralı deliğe</b> sokmalısın.\n\n" +
            "<b>W A S D / Yön Tuşları:</b>  Yürü\n" +
            "<b>Topu it:</b>  Topa doğru yürü, kollarınla iterek bayraklı deliğe yuvarla\n" +
            "<b>Robot çim biçiciler:</b>  Turuncu halkalarına girersen fark eder, gözleri kırmızıya döner ve seni kovalar\n" +
            "<b>ESC / P:</b>  Duraklat\n" +
            "<b>Serbest Dolaşma:</b>  Süre yok, robotlar kovalamaz; Shift ile hızlı yürü\n\n" +
            "Korna çalmadan 10 topu da deliğe sokarsan <b>Altın Bayrak Kupası</b> senin!",
            30, CardText, new Vector2(0f, 0f), new Vector2(1120f, 560f));
        body.alignment = TextAlignmentOptions.Left;
        body.lineSpacing = 8f;
        var bClose = MakeButton(card.transform, "Tamam", BtnGreen, new Vector2(0f, -355f), new Vector2(320f, 90f), sfx, click);
        UnityEventTools.AddPersistentListener(bClose.onClick, mm.CloseHowTo);
        mm.howToPanel = how;
        how.SetActive(false);

        MakeEventSystem();
        EditorSceneManager.SaveScene(scene, Root + "/Scenes/AnaMenu.unity");
    }

    // =====================================================================
    //  GALERİ: Mert'in Blender modelleri (ana menü)
    // =====================================================================
    static readonly Vector3 GalleryPos = new Vector3(90f, 0f, 0f);
    static readonly string[] GalleryModels = { "OyuncakKasaba", "OyuncakAraba", "KakaoKupasi", "KartonFigur", "DevMankenEli", "SallayanEl" };
    static readonly string[] GalleryTitles = { "Oyuncak Kasaba", "Oyuncak Araba", "Kakao Kupası", "Karton Figür", "Ahşap Manken Eli", "El Sallayan El" };
    static readonly string[] GalleryDetails =
    {
        "<b>Dosya:</b> Mahalle.blend\nEvler, apartmanlar, ağaçlar, sokak lambaları ve yollar; pencere, kapı, çatı ve ışık detaylarıyla geliştirildi.\n<b>Oyunda:</b> Kulübün güneyinde, yolun ucunda; bozuk arabaları kaldırıldı, sokaklarına oyuncak arabalar kondu.",
        "<b>Dosya:</b> Başlıksız.blend\nKurma anahtarlı oyuncak araba; farlar, stop lambaları, jantlar, tampon ve cam detayları eklendi.\n<b>Oyunda:</b> Kulübün otoparkında, kasabanın sokaklarında ve ana menüdeki adada.",
        "<b>Dosya:</b> untitled.blend\nMarshmallowlu kakao kupası ve kurabiye tabağı; parlak sır ve kalp deseni eklendi.\n<b>Oyunda:</b> Kulüp terasındaki masanın üstünde (buharı tüter) ve ana menüdeki masada.",
        "<b>Dosya:</b> Kemik.blend\nArkası destekli karton insan figürü; boyalı bölgeler, yüz ve destek ayağı eklendi.\n<b>Oyunda:</b> Saha ipinin arkasında hareketsiz seyirci panoları ve kasabanın kaldırımlarında.",
        "<b>Dosya:</b> Hand2.blend\nEklemli ahşap manken eli; ahşap materyaller ve parmakların kıvrıldığı animasyonuyla.\n<b>Oyunda:</b> Pro Shop vitrininin önünde kaide üzerinde ve ana menüdeki adada.",
        "<b>Dosya:</b> EL.blend\nOyuncak bebek teninde, yumuşatılmış ve el sallayan animasyonlu el.\n<b>Oyunda:</b> Giriş kapısında karşılama heykeli ve ana menünün arka planında.",
    };

    static GameObject BuildGalleryStage(out Transform turntable, out GameObject[] models)
    {
        var root = new GameObject("Galeri (Blender Modellerim)");
        root.transform.position = GalleryPos;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Zemin";
        floor.transform.SetParent(root.transform, false);
        floor.transform.localScale = new Vector3(6f, 1f, 6f);
        floor.GetComponent<Renderer>().sharedMaterial = Mat("Galeri_Zemin", new Color(0.20f, 0.36f, 0.24f), null, 0.15f);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Fon";
        wall.transform.SetParent(root.transform, false);
        wall.transform.localPosition = new Vector3(0f, 7f, 10f);
        wall.transform.localScale = new Vector3(50f, 16f, 0.5f);
        wall.GetComponent<Renderer>().sharedMaterial = Mat("Galeri_Fon", new Color(0.09f, 0.22f, 0.15f), null, 0.1f);
        var ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ped.name = "Dönen Kaide";
        ped.transform.SetParent(root.transform, false);
        ped.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        ped.transform.localScale = new Vector3(6f, 0.25f, 6f);
        ped.GetComponent<Renderer>().sharedMaterial = Mat("Tas_Kaide", new Color(0.74f, 0.71f, 0.66f), null, 0.15f);
        var trim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trim.name = "Altın Şerit";
        trim.transform.SetParent(root.transform, false);
        trim.transform.localPosition = new Vector3(0f, 0.40f, 0f);
        trim.transform.localScale = new Vector3(6.12f, 0.04f, 6.12f);
        var g = Mat("Altin_Parlak", new Color(1f, 0.78f, 0.3f), null, 0.7f);
        g.SetFloat("_Metallic", 0.55f);
        trim.GetComponent<Renderer>().sharedMaterial = g;
        turntable = new GameObject("Döner Tabla").transform;
        turntable.SetParent(root.transform, false);
        turntable.localPosition = new Vector3(0f, 0.5f, 0f);

        var key = new GameObject("Ana Işık").AddComponent<Light>();
        key.transform.SetParent(root.transform, false);
        key.type = LightType.Spot; key.spotAngle = 60f; key.range = 24f; key.intensity = 12f; key.color = new Color(1f, 0.95f, 0.85f);
        key.shadows = LightShadows.Soft;
        key.transform.localPosition = new Vector3(-3f, 8f, -6f);
        key.transform.rotation = Quaternion.LookRotation((GalleryPos + Vector3.up * 1.2f) - key.transform.position);
        var rim = new GameObject("Arka Işık").AddComponent<Light>();
        rim.transform.SetParent(root.transform, false);
        rim.type = LightType.Spot; rim.spotAngle = 70f; rim.range = 20f; rim.intensity = 8f; rim.color = new Color(0.75f, 0.85f, 1f);
        rim.transform.localPosition = new Vector3(4f, 6f, 6f);
        rim.transform.rotation = Quaternion.LookRotation((GalleryPos + Vector3.up * 1.2f) - rim.transform.position);

        models = new GameObject[GalleryModels.Length];
        for (int i = 0; i < GalleryModels.Length; i++)
        {
            string n = GalleryModels[i];
            bool wide = n == "OyuncakKasaba";
            var m = ShowcaseModel(n, turntable, wide ? 3.0f : 2.6f, wide ? 6.4f : (n == "SallayanEl" ? 9f : 4.4f));
            if (n == "DevMankenEli" || n == "SallayanEl") AnimateModel(m, n);
            if (n == "OyuncakKasaba") TownCars(m, false);
            models[i] = m;
        }
        return root;
    }

    /// <summary>Modeli döner tablanın ortasına oturtur ve belirli bir boyuta sığdırır.</summary>
    static GameObject ShowcaseModel(string n, Transform turntable, float maxH, float maxW)
    {
        var w = PlaceModel(n, turntable, Vector3.zero, 0f, 1f, false, false);
        var rs = w.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return w;
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        float s = Mathf.Min(maxH / b.size.y, maxW / Mathf.Max(b.size.x, b.size.z));
        w.transform.localScale = Vector3.one * s;
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        Vector3 tp = turntable.position;
        w.transform.position += new Vector3(tp.x - b.center.x, tp.y - b.min.y + 0.01f, tp.z - b.center.z);
        foreach (var smr in w.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
        return w;
    }

    // =====================================================================
    //  YARDIMCILAR
    // =====================================================================
    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png");

    static Canvas MakeCanvas(string name)
    {
        var go = new GameObject(name);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    static void MakeEventSystem()
    {
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    static Sprite SpriteAt(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Textures/" + name + ".png");

    static Image Icon(Transform parent, string sprite, Vector2 pos, float size, Color color)
    {
        var go = new GameObject("İkon_" + sprite, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = SpriteAt(sprite);
        img.color = color;
        img.raycastTarget = false;
        img.preserveAspect = true;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        return img;
    }

    static GameObject Panel(Transform parent, string name, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = SpriteAt("ui_panel");
        img.type = Image.Type.Sliced;
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return go;
    }

    static GameObject Overlay(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.02f, 0.12f, 0.06f, 0.6f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return go;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, Color color, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        return t;
    }

    static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
    }

    static Button MakeButton(Transform parent, string label, Color color, Vector2 pos, Vector2 size, AudioSource sfx, AudioClip click)
    {
        var go = new GameObject("Buton_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = SpriteAt("ui_button");
        img.type = Image.Type.Sliced;
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var b = go.GetComponent<Button>();
        var cb = b.colors;
        cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        b.colors = cb;
        var t = MakeText(go.transform, "Yazı", label, size.y * 0.42f, Color.white, Vector2.zero, size);
        t.outlineWidth = 0.15f;
        t.outlineColor = new Color(0f, 0f, 0f, 0.25f);
        if (sfx != null && click != null)
            UnityEventTools.AddObjectPersistentListener<AudioClip>(b.onClick, sfx.PlayOneShot, click);
        return b;
    }

    static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + name + ".wav");

    static Material Mat(string name, Color color, Texture2D tex, float smooth, Color? emission = null)
    {
        if (matCache.TryGetValue(name, out var cached)) return cached;
        string path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        matCache[name] = m;
        return m;
    }

    static T SaveAsset<T>(T obj, string path) where T : Object
    {
        EnsureDir(Path.GetDirectoryName(path).Replace("\\", "/"));
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(obj, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(obj, path);
        return obj;
    }

    static Mesh MeshAsset(string name, System.Func<Mesh> make)
    {
        string path = GenDir + "/Mesh_" + name + ".asset";
        var fresh = make();
        fresh.name = name;
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m != null)
        {
            EditorUtility.CopySerialized(fresh, m);
            EditorUtility.SetDirty(m);
            return m;
        }
        AssetDatabase.CreateAsset(fresh, path);
        return fresh;
    }

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void SetupLayers()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layersProp = tm.FindProperty("layers");
        void Ensure(string name, int preferred)
        {
            for (int i = 8; i < 32; i++) if (layersProp.GetArrayElementAtIndex(i).stringValue == name) return;
            for (int i = preferred; i < 32; i++)
            {
                var el = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue)) { el.stringValue = name; break; }
            }
        }
        Ensure(PlayerLayerName, 8);
        Ensure(HoleBlockLayerName, 9);
        tm.ApplyModifiedProperties();
        int player = LayerMask.NameToLayer(PlayerLayerName);
        int block = LayerMask.NameToLayer(HoleBlockLayerName);
        for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(block, i, i != player);
    }

    static void EnableTerrainHoles()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
            var so = new SerializedObject(asset);
            var p = so.FindProperty("m_SupportsTerrainHoles");
            if (p != null && !p.boolValue) { p.boolValue = true; so.ApplyModifiedProperties(); }
        }
    }

    // ---- Prosedürel meshler ----
    static Mesh MakeCup(float radius, float depth, int seg)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(dir * radius); n.Add(-dir); uv.Add(new Vector2(i / (float)seg, 1f));
            v.Add(dir * radius + Vector3.down * depth); n.Add(-dir); uv.Add(new Vector2(i / (float)seg, 0f));
        }
        for (int i = 0; i < seg; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tri.AddRange(new[] { a, b, c, b, d, c });
        }
        int center = v.Count;
        v.Add(Vector3.down * depth); n.Add(Vector3.up); uv.Add(new Vector2(0.5f, 0.5f));
        int ringStart = v.Count;
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Cos(a) * radius, -depth, Mathf.Sin(a) * radius)); n.Add(Vector3.up); uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }
        for (int i = 0; i < seg; i++) tri.AddRange(new[] { center, ringStart + i + 1, ringStart + i });
        var m = new Mesh();
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Altı açık iç yüzey (golf kabının beyaz astarı).</summary>
    static Mesh MakeTube(float radius, float depth, int seg)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(dir * radius); n.Add(-dir); uv.Add(new Vector2(i / (float)seg, 1f));
            v.Add(dir * radius + Vector3.down * depth); n.Add(-dir); uv.Add(new Vector2(i / (float)seg, 0f));
        }
        for (int i = 0; i < seg; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tri.AddRange(new[] { a, b, c, b, d, c });
        }
        var m = new Mesh();
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    static Mesh MakeAnnulus(float inner, float outer, int seg, float outerY = 0f)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(d * inner); uv.Add(new Vector2(d.x * inner * 0.2f, d.z * inner * 0.2f));
            v.Add(d * outer + Vector3.up * outerY); uv.Add(new Vector2(d.x * outer * 0.2f, d.z * outer * 0.2f));
        }
        for (int i = 0; i < seg; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tri.AddRange(new[] { a, b, c, c, b, d });
        }
        var m = new Mesh();
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        if (m.normals[0].y < 0f) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); }
        return m;
    }

    static Mesh MakeTorus(float R, float r, int seg, int side)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            var c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int j = 0; j <= side; j++)
            {
                float b = j / (float)side * Mathf.PI * 2f;
                var nn = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                v.Add(c * R + nn * r); n.Add(nn);
            }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < side; j++)
            {
                int a = i * (side + 1) + j, b = a + side + 1;
                tri.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }
        var m = new Mesh();
        m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    static Mesh MakeDisc(float radius, int seg)
    {
        var v = new List<Vector3> { Vector3.zero }; var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) }; var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }
        for (int i = 1; i <= seg; i++) tri.AddRange(new[] { 0, i + 1, i });
        var m = new Mesh();
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        return m;
    }

    /// <summary>Çift yüzlü dikdörtgen bayrak: numara iki taraftan da düz okunur.</summary>
    static Mesh MakeFlagQuad()
    {
        float w = 1.15f, h = 0.8f;
        var v = new List<Vector3>
        {
            new Vector3(0, -h / 2, 0), new Vector3(0, h / 2, 0), new Vector3(w, h / 2, 0), new Vector3(w, -h / 2, 0),
            new Vector3(0, -h / 2, 0), new Vector3(0, h / 2, 0), new Vector3(w, h / 2, 0), new Vector3(w, -h / 2, 0),
        };
        var uv = new List<Vector2>
        {
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0),
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0),
        };
        var tri = new List<int> { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 };
        var m = new Mesh();
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
}
#endif
