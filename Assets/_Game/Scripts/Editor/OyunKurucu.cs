#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Oyuncak Rüyası sahnelerini kuran editör aracı (Mert Demir - 2506127004).
/// Menü: Oyuncak Rüyası > Sahneleri Kur
/// </summary>
public static class OyunKurucu
{
    const string Root = "Assets/_Game";
    const string GenDir = Root + "/Generated";

    // ---- Oyun alanı ayarları ----
    public const float BallRadius = 0.8f;
    const float HoleRadius = 1.25f;        // yuvanın ağzı
    const float HoleDepth = 1.9f;          // yuvanın derinliği
        const float FunnelRadius = 4.7f;       // huni etkisi
    const float FunnelDepth = 0.45f;       // huni derinliği
    const float TerrainSize = 200f;
    const float TerrainHeight = 30f;
    const float TerrainBase = 3f;          // zemin = dünya y 0

    static readonly Vector2[] HolePositions =
    {
        new Vector2(6.0f, 6.5f), new Vector2(-6.5f, 6.0f), new Vector2(-6.0f, -6.5f), new Vector2(6.5f, -6.0f),
        new Vector2(14f, 3f), new Vector2(4f, 15f), new Vector2(-12f, 12f), new Vector2(-15f, -3f),
        new Vector2(-3f, -15f), new Vector2(12f, -12f),
    };

    static readonly Vector3[] CubePositions =
    {
        new Vector3(-1f, 0f, 10.5f), new Vector3(19f, 0f, -5f), new Vector3(-19f, 0f, 6f),
    };

    static readonly Color[] Pastel =
    {
        new Color(0.94f, 0.33f, 0.31f), new Color(0.26f, 0.52f, 0.96f), new Color(1f, 0.84f, 0.31f),
        new Color(0.40f, 0.73f, 0.42f), new Color(0.67f, 0.28f, 0.74f), new Color(1f, 0.65f, 0.15f),
        new Color(0.15f, 0.78f, 0.85f), new Color(0.94f, 0.38f, 0.57f),
    };

    const string PlayerLayerName = "Oyuncu";
    const string HoleBlockLayerName = "YuvaKapagi";

    static TMP_FontAsset font;
    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    [MenuItem("Oyuncak Rüyası/Sahneleri Kur")]
    public static void BuildAll()
    {
        EnsureDir(GenDir);
        EnsureDir(Root + "/Scenes");
        matCache.Clear();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Baloo2-Bold SDF.asset");
        EnableTerrainHoles();
        SetupLayers();
        BuildGameScene();
        BuildMenuScene();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Root + "/Scenes/AnaMenu.unity", true),
            new EditorBuildSettingsScene(Root + "/Scenes/Oyun.unity", true),
        };
        AssetDatabase.SaveAssets();
        Debug.Log("Oyuncak Rüyası: sahneler kuruldu.");
    }

    // =====================================================================
    //  OYUN SAHNESİ
    // =====================================================================
    public static void BuildGameScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var skybox = MakeSkybox();
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.66f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.82f, 0.68f, 0.76f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.48f, 0.42f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.90f, 0.82f, 0.95f);
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 150f;

        // Işık
        var sun = new GameObject("Güneş (Directional Light)");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.93f, 0.85f);
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.7f;
        sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        RenderSettings.sun = light;

        // Arazi
        var terrain = BuildTerrain();

        // Yuvalar
        var holesRoot = new GameObject("Yuvalar (10 adet)");
        var sockets = new List<HoleSocket>();
        for (int i = 0; i < HolePositions.Length; i++)
            sockets.Add(BuildHole(i, HolePositions[i], holesRoot.transform));

        // Toplar
        var ballsRoot = new GameObject("Toplar (10 küre)");
        var balls = new List<Ball>();
        var ballMat = new PhysicsMaterial("TopFizik") { bounciness = 0.15f, dynamicFriction = 0.45f, staticFriction = 0.5f, bounceCombine = PhysicsMaterialCombine.Average };
        ballMat = SaveAsset(ballMat, Root + "/Physics/TopFizik.asset");
        for (int i = 0; i < 10; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.name = "Top_" + (i + 1).ToString("00");
            b.transform.SetParent(ballsRoot.transform);
            b.transform.localScale = Vector3.one * BallRadius * 2f;
            Vector2 hp = HolePositions[i];
            b.transform.position = new Vector3(hp.x + 4f, BallRadius + 0.05f, hp.y);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/ball_" + (i + 1).ToString("00") + ".png");
            b.GetComponent<Renderer>().sharedMaterial = Mat("Top_" + (i + 1), Color.white, tex, 0.55f);
            var col = b.GetComponent<SphereCollider>();
            col.sharedMaterial = ballMat;
            var rb = b.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.linearDamping = 0.35f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            balls.Add(b.AddComponent<Ball>());
        }

        // Oyuncu
        var player = BuildPlayer(new Vector3(0f, 0.05f, -1.5f));

        // Kamera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 260f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        var cad = camGo.AddComponent<UniversalAdditionalCameraData>();
        cad.renderPostProcessing = true;
        cad.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        var tdc = camGo.AddComponent<TopDownCamera>();
        tdc.target = player.transform;
        tdc.offset = new Vector3(0f, 14f, -10f);
        tdc.smoothTime = 0.2f;
        tdc.SnapToTarget();
        player.GetComponent<PlayerController>().cameraTransform = camGo.transform;

        BuildPostProcess("OyunPostProcess");

        // Kovalayan küpler
        var cubesRoot = new GameObject("Kovalayan Küpler");
        for (int i = 0; i < CubePositions.Length; i++)
            BuildChaserCube(i, CubePositions[i], cubesRoot.transform, player.transform);

        // Dekor
        BuildDecor(terrain);

        // Yöneticiler + arayüz
        var managers = new GameObject("Oyun Yöneticisi");
        var gm = managers.AddComponent<GameManager>();
        gm.player = player.GetComponent<PlayerController>();
        var sfx = managers.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        var music = new GameObject("Müzik").AddComponent<AudioSource>();
        music.transform.SetParent(managers.transform);
        music.clip = Clip("music_box_loop");
        music.loop = true;
        music.volume = 0.35f;
        music.playOnAwake = true;
        gm.sfx = sfx;
        gm.music = music;
        gm.startClip = Clip("start");
        gm.scoreClip = Clip("score");
        gm.tickClip = Clip("tick");
        gm.winClip = Clip("win");
        gm.alarmClip = Clip("alarm");

        var spawner = managers.AddComponent<BallSpawner>();
        spawner.balls = balls.ToArray();
        spawner.holes = sockets.ToArray();
        spawner.terrain = terrain;
        spawner.ballRadius = BallRadius;
        spawner.minDistance = FunnelRadius + 0.1f;
        spawner.maxDistance = FunnelRadius + 0.8f;
        spawner.keepAwayFromHoles = FunnelRadius + 0.1f;
        spawner.areaMin = new Vector2(-24f, -24f);
        spawner.areaMax = new Vector2(24f, 24f);

        var avoid = new List<Transform>();
        foreach (Transform c in cubesRoot.transform) avoid.Add(c);
        spawner.avoidPoints = avoid.ToArray();
        spawner.avoidRadius = 5f;

        BuildGameUI(gm, sfx);

        // Dünya sınırı (görünmez duvarlar)
        var bounds = new GameObject("Dünya Sınırı");
        float e = 88f;
        AddWall(bounds.transform, new Vector3(0, 10, e), new Vector3(2 * e, 40, 2));
        AddWall(bounds.transform, new Vector3(0, 10, -e), new Vector3(2 * e, 40, 2));
        AddWall(bounds.transform, new Vector3(e, 10, 0), new Vector3(2, 40, 2 * e));
        AddWall(bounds.transform, new Vector3(-e, 10, 0), new Vector3(2, 40, 2 * e));

        EditorSceneManager.SaveScene(scene, Root + "/Scenes/Oyun.unity");
    }

    static void AddWall(Transform parent, Vector3 pos, Vector3 size)
    {
        var w = new GameObject("Duvar");
        w.transform.SetParent(parent);
        w.transform.position = pos;
        w.AddComponent<BoxCollider>().size = size;
    }

    // ---------------------------------------------------------------- Arazi
    /// <summary>Huni profili: yuvanın ağzından itibaren eğimli, dışta zemine yumuşakça bağlanır.</summary>
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

    static float GroundHeight(float wx, float wz)
    {
        float r = new Vector2(wx, wz).magnitude;
        float h = 0f;
        // Oyun alanının dışında yumuşak tepeler
        float outer = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 55f, r));
        float hills = (Mathf.PerlinNoise(wx * 0.025f + 10f, wz * 0.025f + 3f) - 0.35f) * 7f;
        h += outer * Mathf.Max(0f, hills);
        // Kenarlarda yükselen sınır tepeleri
        float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(70f, 95f, Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz))));
        h += rim * 14f * (0.7f + 0.3f * Mathf.PerlinNoise(wx * 0.05f, wz * 0.05f));
        h += FunnelOffset(wx, wz);
        return h;
    }

    static Terrain BuildTerrain()
    {
        int res = 513;
        // Arazi verisini her seferinde temiz oluştur (eski doku boyamaları birikmesin)
        string tdPath = GenDir + "/OyunArazisi.asset";
        if (AssetDatabase.LoadMainAssetAtPath(tdPath) != null) AssetDatabase.DeleteAsset(tdPath);
        var td = new TerrainData();
        AssetDatabase.CreateAsset(td, tdPath);
        td.heightmapResolution = res;
        td.alphamapResolution = 512;
        td.baseMapResolution = 512;
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
                    if (Vector2.Distance(new Vector2(wx, wz), h) < HoleRadius + 0.45f) solid = false;
                holes[y, x] = solid;
            }
        td.SetHoles(0, 0, holes);

        // Zemin katmanları
        var layers = new[]
        {
            Layer("Katman_Cimen", "ground_grass", 16f),
            Layer("Katman_OyunMati", "ground_playmat", 20f, new Vector2(5f, 5f)),
            Layer("Katman_Kum", "ground_sand", 6f),
            Layer("Katman_Lila", "ground_lilac", 16f),
        };
        td.terrainLayers = layers;
        int ar = td.alphamapResolution;
        var alpha = new float[ar, ar, layers.Length];
        for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                float wx = -half + (x + 0.5f) / ar * TerrainSize;
                float wz = -half + (y + 0.5f) / ar * TerrainSize;
                float r = new Vector2(wx, wz).magnitude;
                float mat = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(24.7f, 25.3f, Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz))));
                float sand = 0f;
                // dört yöne kum patikalar
                float pathW = Mathf.Min(Mathf.Abs(wx), Mathf.Abs(wz));
                if (Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz)) > 25.3f && r < 75f) sand = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.2f, 2.8f, pathW));
                float hgt = GroundHeight(wx, wz);
                float lilac = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 8f, hgt));
                float grass = Mathf.Max(0f, 1f - mat - sand - lilac);
                float sum = grass + mat + sand + lilac;
                alpha[y, x, 0] = grass / sum;
                alpha[y, x, 1] = mat / sum;
                alpha[y, x, 2] = sand / sum;
                alpha[y, x, 3] = lilac / sum;
            }
        td.SetAlphamaps(0, 0, alpha);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Rüya Arazisi (Terrain)";
        go.transform.position = new Vector3(-half, -TerrainBase, -half);
        var t = go.GetComponent<Terrain>();
        var tmat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
        tmat.EnableKeyword("_ALPHATEST_ON");
        tmat = SaveAsset(tmat, GenDir + "/AraziMateryali.mat");
        t.materialTemplate = tmat;
        t.drawInstanced = true;
        t.heightmapPixelError = 4f;
        t.basemapDistance = 150f;
        return t;
    }

    static TerrainLayer Layer(string name, string tex, float tile, Vector2 offset = default)
    {
        var l = new TerrainLayer();
        l.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + tex + ".png");
        l.tileSize = new Vector2(tile, tile);
        l.tileOffset = offset;
        l.smoothness = 0.1f;
        return SaveAsset(l, GenDir + "/" + name + ".asset");
    }

    // ---------------------------------------------------------------- Yuva
    static HoleSocket BuildHole(int index, Vector2 p, Transform parent)
    {
        float topY = -FunnelDepth;
        var root = new GameObject("Yuva_" + (index + 1).ToString("00"));
        root.transform.SetParent(parent);
        root.transform.position = new Vector3(p.x, topY, p.y);
        var socket = root.AddComponent<HoleSocket>();
        socket.settleDepth = -(BallRadius - 0.08f);

        Color c = Pastel[index % Pastel.Length];

        // Yuvanın iç çanağı (açık silindir + taban)
        var cup = new GameObject("Çanak");
        cup.transform.SetParent(root.transform, false);
        var cupMesh = MeshAsset("Canak", () => MakeCup(HoleRadius, HoleDepth, 32));
        cup.AddComponent<MeshFilter>().sharedMesh = cupMesh;
        cup.AddComponent<MeshRenderer>().sharedMaterial = Mat("CanakIc", new Color(0.36f, 0.28f, 0.45f), null, 0.1f);
        cup.AddComponent<MeshCollider>().sharedMesh = cupMesh;

        // Ağız halkası (zemindeki pürüzleri kapatır)
        var ring = new GameObject("Halka");
        ring.transform.SetParent(root.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        float ringRise = FunnelProfile(HoleRadius + 0.8f) + FunnelDepth;
        var ringMesh = MeshAsset("Halka", () => MakeAnnulus(HoleRadius, HoleRadius + 0.8f, 48, ringRise));
        ring.AddComponent<MeshFilter>().sharedMesh = ringMesh;
        var rr = ring.AddComponent<MeshRenderer>();
        rr.sharedMaterial = Mat("Halka_" + index, Color.Lerp(c, Color.white, 0.25f), null, 0.4f);
        ring.AddComponent<MeshCollider>().sharedMesh = ringMesh;
        socket.ringRenderer = rr;

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

        // Oyuncu kapağı: sadece oyuncu çarpar, toplar içinden geçip yuvaya düşer
        var block = new GameObject("Oyuncu Kapagi");
        block.layer = LayerMask.NameToLayer(HoleBlockLayerName);
        block.transform.SetParent(root.transform, false);
        block.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        block.AddComponent<BoxCollider>().size = new Vector3(HoleRadius * 2.1f, 0.2f, HoleRadius * 2.1f);

        // Bayrak (uzaktan görünen işaret)
        var flag = new GameObject("Bayrak");
        flag.transform.SetParent(root.transform, false);
        flag.transform.localPosition = new Vector3(HoleRadius + 1.0f, FunnelProfile(HoleRadius + 1.0f) + FunnelDepth - 0.05f, 0f);
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Direk";
        Object.DestroyImmediate(pole.GetComponent<Collider>());
        pole.transform.SetParent(flag.transform, false);
        pole.transform.localScale = new Vector3(0.08f, 1.6f, 0.08f);
        pole.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        pole.GetComponent<Renderer>().sharedMaterial = Mat("Beyaz", new Color(0.97f, 0.97f, 0.97f), null, 0.5f);
        var cloth = new GameObject("Kumas");
        cloth.transform.SetParent(flag.transform, false);
        cloth.transform.localPosition = new Vector3(0f, 2.75f, 0f);
        cloth.AddComponent<MeshFilter>().sharedMesh = MeshAsset("BayrakUcgen", MakeFlag);
        cloth.AddComponent<MeshRenderer>().sharedMaterial = Mat("Bayrak_" + index, c, null, 0.3f, c * 0.6f);
        cloth.AddComponent<YavasDon>().rotationSpeed = new Vector3(0f, 25f, 0f);
        socket.flagBeacon = flag;

        // Işık (dolunca yanar)
        var glow = new GameObject("Isik");
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        var gl = glow.AddComponent<Light>();
        gl.type = LightType.Point;
        gl.color = new Color(1f, 0.85f, 0.4f);
        gl.range = 6f;
        gl.intensity = 3f;
        gl.shadows = LightShadows.None;
        socket.glowLight = gl;

        // Konfeti efekti
        socket.scoreEffect = MakeConfetti(root.transform);
        return socket;
    }

    static ParticleSystem MakeConfetti(Transform parent)
    {
        var go = new GameObject("Konfeti");
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
        grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.4f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0.33f), new GradientColorKey(new Color(0.4f, 0.75f, 1f), 0.66f), new GradientColorKey(new Color(0.5f, 0.9f, 0.5f), 1f) },
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
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMat();
        return ps;
    }

    static Material ParticleMat()
    {
        string path = GenDir + "/Mat_Konfeti.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/fx_star.png"));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        return SaveAsset(m, path);
    }

    // ---------------------------------------------------------------- Oyuncu
    static GameObject BuildPlayer(Vector3 pos)
    {
        var root = new GameObject("Oyuncu (Uyurgezer)");
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

        // Kolların önündeki itme alanı: top kolların içinden geçmesin, ellere değsin
        var push = root.AddComponent<BoxCollider>();
        push.center = new Vector3(0f, 1.2f, 0.38f);
        push.size = new Vector3(0.62f, 0.42f, 0.34f);
        push.sharedMaterial = noFriction;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Characters/Karakter_Rigged.fbx");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.name = "Karakter (T-Pose model)";
        model.transform.SetParent(root.transform, false);
        var anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "/Animations/KarakterAnimator.controller");
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;

        var pc = root.AddComponent<PlayerController>();
        pc.animator = anim;
        pc.walkSpeed = 4.0f;
        pc.walkClipSpeed = 1.41f;
        return root;
    }

    // ---------------------------------------------------------------- Küpler
    static void BuildChaserCube(int i, Vector3 pos, Transform parent, Transform player)
    {
        float size = 1.5f;
        var root = new GameObject("Kovalayan Küp " + (i + 1));
        root.transform.SetParent(parent);
        root.transform.position = new Vector3(pos.x, size * 0.5f + 0.02f, pos.z);
        var bc = root.AddComponent<BoxCollider>();
        bc.size = Vector3.one * size;
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.name = "Görsel";
        Object.DestroyImmediate(vis.GetComponent<Collider>());
        vis.GetComponent<MeshFilter>().sharedMesh = MeshAsset("HarfKupu", MakeBlock);
        vis.transform.SetParent(root.transform, false);
        vis.transform.localScale = Vector3.one * size;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/block_" + (i * 3 % 16).ToString("00") + ".png");
        vis.GetComponent<Renderer>().sharedMaterial = Mat("Kup_" + i, Color.white, tex, 0.35f);

        // Hocanın kodu (değiştirilmeden): rastgele sağa-sola dolaşma
        var rcm = root.AddComponent<RandomCubeMovement>();
        rcm.moveSpeed = 2f;
        rcm.moveRange = 2f;
        rcm.changeTime = 2f;

        var alert = new GameObject("Uyari (!)");
        alert.transform.SetParent(root.transform, false);
        alert.transform.localPosition = new Vector3(0f, size + 0.6f, 0f);
        var tmp = alert.AddComponent<TextMeshPro>();
        tmp.text = "!";
        tmp.font = font;
        tmp.fontSize = 9f;
        tmp.color = new Color(1f, 0.25f, 0.3f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = Color.white;
        tmp.rectTransform.sizeDelta = new Vector2(2f, 2f);

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0.8f;
        audio.minDistance = 6f;
        audio.maxDistance = 40f;

        var ch = root.AddComponent<CubeChaser>();
        ch.player = player;
        ch.detectRadius = 5f;
        ch.giveUpRadius = 8.5f;
        ch.chaseSpeed = 2.5f;
        ch.returnSpeed = 3f;
        ch.leashRadius = 10f;
        ch.hitForce = 4f;
        ch.stunTime = 0.4f;
        ch.cooldown = 3f;
        ch.visual = vis.transform;
        ch.alertIcon = alert;
        ch.audioSource = audio;
        ch.alertClip = Clip("alert");
        ch.bonkClip = Clip("bonk");
    }

    // ---------------------------------------------------------------- Dekor
    static void BuildDecor(Terrain terrain)
    {
        var root = new GameObject("Oyuncak Dünyası (Dekor)");
        var rnd = new System.Random(11);

        // Dev çalar saat (nirengi noktası, haritanın kuzeyinde)
        BuildAlarmClock(root.transform, new Vector3(0f, 0f, 31f), terrain);

        // Harf küpleri: tekli ve kuleler
        var blocks = new GameObject("Harf Küpleri");
        blocks.transform.SetParent(root.transform);
        for (int i = 0; i < 46; i++)
        {
            Vector2 p = RandomRing(rnd, 30f, 82f);
            float s = 2f + (float)rnd.NextDouble() * 2.5f;
            int stack = rnd.NextDouble() < 0.35 ? 1 + rnd.Next(3) : 1;
            float y = Ground(terrain, p.x, p.y);
            for (int k = 0; k < stack; k++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "HarfKüpü";
                c.GetComponent<MeshFilter>().sharedMesh = MeshAsset("HarfKupu", MakeBlock);
                c.transform.SetParent(blocks.transform);
                float ss = s * (1f - k * 0.15f);
                c.transform.localScale = Vector3.one * ss;
                c.transform.position = new Vector3(p.x, y + ss * 0.5f, p.y);
                y += ss;
                c.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 90f, 0f);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/block_" + rnd.Next(16).ToString("00") + ".png");
                c.GetComponent<Renderer>().sharedMaterial = Mat("Blok_" + tex.name, Color.white, tex, 0.35f);
                GameObjectUtility.SetStaticEditorFlags(c, StaticEditorFlags.BatchingStatic);
            }
        }

        // Dev boya kalemleri (ağaç gibi dikili)
        var crayons = new GameObject("Boya Kalemleri");
        crayons.transform.SetParent(root.transform);
        var coneMesh = MeshAsset("Koni", () => MakeCone(0.5f, 1f, 24));
        for (int i = 0; i < 34; i++)
        {
            Vector2 p = RandomRing(rnd, 32f, 85f);
            Color c = Pastel[rnd.Next(Pastel.Length)];
            float h = 6f + (float)rnd.NextDouble() * 6f;
            float rad = 0.8f + (float)rnd.NextDouble() * 0.4f;
            var cr = new GameObject("BoyaKalemi");
            cr.transform.SetParent(crayons.transform);
            cr.transform.position = new Vector3(p.x, Ground(terrain, p.x, p.y) - 0.3f, p.y);
            cr.transform.rotation = Quaternion.Euler((float)rnd.NextDouble() * 10f - 5f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 10f - 5f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(cr.transform, false);
            body.transform.localScale = new Vector3(rad * 2f, h * 0.5f, rad * 2f);
            body.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            body.GetComponent<Renderer>().sharedMaterial = Mat("Kalem_" + ColorKey(c), c, null, 0.35f);
            var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(band.GetComponent<Collider>());
            band.transform.SetParent(cr.transform, false);
            band.transform.localScale = new Vector3(rad * 2.05f, h * 0.08f, rad * 2.05f);
            band.transform.localPosition = new Vector3(0f, h * 0.75f, 0f);
            band.GetComponent<Renderer>().sharedMaterial = Mat("Kalem_Bant", new Color(0.98f, 0.96f, 0.9f), null, 0.3f);
            var tip = new GameObject("Uc");
            tip.transform.SetParent(cr.transform, false);
            tip.transform.localPosition = new Vector3(0f, h, 0f);
            tip.transform.localScale = new Vector3(rad * 2f, rad * 2.2f, rad * 2f);
            tip.AddComponent<MeshFilter>().sharedMesh = coneMesh;
            tip.AddComponent<MeshRenderer>().sharedMaterial = Mat("Kalem_" + ColorKey(c), c, null, 0.35f);
            foreach (Transform t in cr.transform) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        // Lolipop ağaçlar
        var lolis = new GameObject("Lolipop Ağaçlar");
        lolis.transform.SetParent(root.transform);
        for (int i = 0; i < 26; i++)
        {
            Vector2 p = RandomRing(rnd, 30f, 80f);
            Color c = Pastel[rnd.Next(Pastel.Length)];
            float h = 5f + (float)rnd.NextDouble() * 3f;
            var l = new GameObject("Lolipop");
            l.transform.SetParent(lolis.transform);
            l.transform.position = new Vector3(p.x, Ground(terrain, p.x, p.y), p.y);
            l.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stick.transform.SetParent(l.transform, false);
            stick.transform.localScale = new Vector3(0.35f, h * 0.5f, 0.35f);
            stick.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            stick.GetComponent<Renderer>().sharedMaterial = Mat("Beyaz", new Color(0.97f, 0.97f, 0.97f), null, 0.5f);
            var candy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            candy.transform.SetParent(l.transform, false);
            candy.transform.localScale = new Vector3(3.6f, 3.6f, 1.1f);
            candy.transform.localPosition = new Vector3(0f, h + 1.3f, 0f);
            candy.GetComponent<Renderer>().sharedMaterial = Mat("Seker_" + ColorKey(c), c, null, 0.8f);
            var swirl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(swirl.GetComponent<Collider>());
            swirl.transform.SetParent(l.transform, false);
            swirl.transform.localScale = new Vector3(1.8f, 1.8f, 1.25f);
            swirl.transform.localPosition = new Vector3(0f, h + 1.3f, 0f);
            swirl.GetComponent<Renderer>().sharedMaterial = Mat("Beyaz", new Color(0.97f, 0.97f, 0.97f), null, 0.5f);
        }

        // Halka kuleleri (oyuncak)
        var torusMesh = MeshAsset("Halka3D", () => MakeTorus(1f, 0.32f, 28, 12));
        var stackers = new GameObject("Halka Kuleleri");
        stackers.transform.SetParent(root.transform);
        for (int i = 0; i < 10; i++)
        {
            Vector2 p = RandomRing(rnd, 34f, 75f);
            var s = new GameObject("HalkaKulesi");
            s.transform.SetParent(stackers.transform);
            s.transform.position = new Vector3(p.x, Ground(terrain, p.x, p.y), p.y);
            var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basePlate.transform.SetParent(s.transform, false);
            basePlate.transform.localScale = new Vector3(4.6f, 0.3f, 4.6f);
            basePlate.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            basePlate.GetComponent<Renderer>().sharedMaterial = Mat("Kalem_" + ColorKey(Pastel[3]), Pastel[3], null, 0.35f);
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.transform.SetParent(s.transform, false);
            pole.transform.localScale = new Vector3(0.5f, 3.5f, 0.5f);
            pole.transform.localPosition = new Vector3(0f, 3.5f, 0f);
            pole.GetComponent<Renderer>().sharedMaterial = Mat("Kalem_" + ColorKey(Pastel[2]), Pastel[2], null, 0.35f);
            for (int k = 0; k < 5; k++)
            {
                var ring = new GameObject("Halka");
                ring.transform.SetParent(s.transform, false);
                float sc = 2.1f - k * 0.3f;
                ring.transform.localScale = new Vector3(sc, sc, sc);
                ring.transform.localPosition = new Vector3(0f, 0.9f + k * 1.0f, 0f);
                ring.AddComponent<MeshFilter>().sharedMesh = torusMesh;
                ring.AddComponent<MeshRenderer>().sharedMaterial = Mat("Kalem_" + ColorKey(Pastel[(k + 4) % Pastel.Length]), Pastel[(k + 4) % Pastel.Length], null, 0.6f);
            }
        }

        // Oyun alanının kenarında birkaç büyük top (dokunulabilir değil, dekor)
        // Bulutlar
        var clouds = new GameObject("Bulutlar");
        clouds.transform.SetParent(root.transform);
        for (int i = 0; i < 14; i++)
        {
            Vector2 p = RandomRing(rnd, 20f, 90f);
            var cl = new GameObject("Bulut");
            cl.transform.SetParent(clouds.transform);
            cl.transform.position = new Vector3(p.x, 26f + (float)rnd.NextDouble() * 12f, p.y);
            int puffs = 3 + rnd.Next(3);
            for (int k = 0; k < puffs; k++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(puff.GetComponent<Collider>());
                puff.transform.SetParent(cl.transform, false);
                float s = 4f + (float)rnd.NextDouble() * 4f;
                puff.transform.localScale = new Vector3(s * 1.3f, s * 0.8f, s);
                puff.transform.localPosition = new Vector3(k * 3.2f - puffs * 1.6f, (float)rnd.NextDouble() * 1.5f, (float)rnd.NextDouble() * 2f);
                var r = puff.GetComponent<Renderer>();
                r.sharedMaterial = Mat("Bulut", new Color(1f, 0.97f, 1f), null, 0.1f, new Color(0.35f, 0.32f, 0.38f));
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            var yd = cl.AddComponent<YavasDon>();
            yd.rotationSpeed = Vector3.zero;
            yd.bobAmount = 0.8f;
            yd.bobSpeed = 0.4f;
        }

        // Sınırda tuğla duvarlar (lego benzeri)
        var bricks = new GameObject("Tuğla Duvar");
        bricks.transform.SetParent(root.transform);
        for (int side = 0; side < 4; side++)
        {
            for (int k = -10; k <= 10; k++)
            {
                float a = k * 8f;
                Vector3 p = side switch
                {
                    0 => new Vector3(a, 0, 86f),
                    1 => new Vector3(a, 0, -86f),
                    2 => new Vector3(86f, 0, a),
                    _ => new Vector3(-86f, 0, a),
                };
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Tuğla";
                b.transform.SetParent(bricks.transform);
                b.transform.localScale = side < 2 ? new Vector3(7.8f, 3.2f, 3.8f) : new Vector3(3.8f, 3.2f, 7.8f);
                b.transform.position = new Vector3(p.x, Ground(terrain, p.x, p.z) + 1.4f, p.z);
                Color c = Pastel[(k + 20 + side) % Pastel.Length];
                b.GetComponent<Renderer>().sharedMaterial = Mat("Kalem_" + ColorKey(c), c, null, 0.5f);
                GameObjectUtility.SetStaticEditorFlags(b, StaticEditorFlags.BatchingStatic);
            }
        }
    }

    static void BuildAlarmClock(Transform parent, Vector3 pos, Terrain terrain)
    {
        var root = new GameObject("Dev Çalar Saat (Nirengi)");
        root.transform.SetParent(parent);
        root.transform.position = new Vector3(pos.x, Ground(terrain, pos.x, pos.z), pos.z);
        var clock = root.AddComponent<AlarmClock>();

        var body = new GameObject("Gövde");
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 6.2f, 0f);
        clock.body = body.transform;

        Color red = new Color(0.94f, 0.33f, 0.36f);
        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.transform.SetParent(body.transform, false);
        rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        rim.transform.localScale = new Vector3(9f, 1.2f, 9f);
        rim.GetComponent<Renderer>().sharedMaterial = Mat("SaatKirmizi", red, null, 0.7f);
        var face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(face.GetComponent<Collider>());
        face.transform.SetParent(body.transform, false);
        face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        face.transform.localPosition = new Vector3(0f, 0f, -0.9f);
        face.transform.localScale = new Vector3(7.8f, 0.4f, 7.8f);
        face.GetComponent<Renderer>().sharedMaterial = Mat("SaatYuz", new Color(1f, 0.98f, 0.92f), null, 0.3f, new Color(0.25f, 0.24f, 0.2f));
        for (int i = 0; i < 12; i++)
        {
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(mark.GetComponent<Collider>());
            mark.transform.SetParent(body.transform, false);
            float ang = i * 30f * Mathf.Deg2Rad;
            mark.transform.localPosition = new Vector3(Mathf.Sin(ang) * 3.3f, Mathf.Cos(ang) * 3.3f, -1.15f);
            mark.transform.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
            mark.transform.localScale = i % 3 == 0 ? new Vector3(0.35f, 0.9f, 0.1f) : new Vector3(0.2f, 0.5f, 0.1f);
            mark.GetComponent<Renderer>().sharedMaterial = Mat("SaatKoyu", new Color(0.25f, 0.2f, 0.35f), null, 0.4f);
        }
        var pivot = new GameObject("Kol Pivot");
        pivot.transform.SetParent(body.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 0f, -1.25f);
        var hand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(hand.GetComponent<Collider>());
        hand.transform.SetParent(pivot.transform, false);
        hand.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        hand.transform.localScale = new Vector3(0.25f, 3.0f, 0.1f);
        hand.GetComponent<Renderer>().sharedMaterial = Mat("SaatKirmizi", red, null, 0.7f);
        var hub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(hub.GetComponent<Collider>());
        hub.transform.SetParent(pivot.transform, false);
        hub.transform.localScale = Vector3.one * 0.6f;
        hub.GetComponent<Renderer>().sharedMaterial = Mat("SaatKoyu", new Color(0.25f, 0.2f, 0.35f), null, 0.4f);
        clock.hand = pivot.transform;

        foreach (var sx in new[] { -1f, 1f })
        {
            var bell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bell.transform.SetParent(body.transform, false);
            bell.transform.localPosition = new Vector3(2.9f * sx, 4.0f, 0f);
            bell.transform.localScale = new Vector3(2.6f, 2.0f, 2.6f);
            bell.transform.localRotation = Quaternion.Euler(0f, 0f, -25f * sx);
            bell.GetComponent<Renderer>().sharedMaterial = Mat("SaatAltin", new Color(1f, 0.82f, 0.35f), null, 0.85f);
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.transform.SetParent(root.transform, false);
            leg.transform.localPosition = new Vector3(2.6f * sx, 1.0f, 0f);
            leg.transform.localRotation = Quaternion.Euler(0f, 0f, 25f * sx);
            leg.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);
            leg.GetComponent<Renderer>().sharedMaterial = Mat("SaatKoyu", new Color(0.25f, 0.2f, 0.35f), null, 0.4f);
        }
        var light = new GameObject("Saat Işığı").AddComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = new Vector3(0f, 7f, -6f);
        light.type = LightType.Point;
        light.range = 14f;
        light.intensity = 2f;
        light.color = new Color(1f, 0.9f, 0.75f);
    }

    static Vector2 RandomRing(System.Random rnd, float rMin, float rMax)
    {
        for (int i = 0; i < 100; i++)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float r = rMin + (float)rnd.NextDouble() * (rMax - rMin);
            var p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            if (Mathf.Abs(p.x) < 4f || Mathf.Abs(p.y) < 4f) continue;     // patikaları boş bırak
            if (Mathf.Abs(p.x) < 9f && p.y > 24f && p.y < 40f) continue;  // saatin önü
            return p;
        }
        return new Vector2(rMax, rMax);
    }

    static float Ground(Terrain t, float x, float z) => t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y;

    // ---------------------------------------------------------------- Post process
    static void BuildPostProcess(string name)
    {
        var vgo = new GameObject("Global Volume (Rüya Efekti)");
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
        bloom.intensity.Override(0.5f);
        bloom.threshold.Override(1.0f);
        bloom.scatter.Override(0.7f);
        Vignette vig; if (!profile.TryGet(out vig)) { vig = profile.Add<Vignette>(true); AssetDatabase.AddObjectToAsset(vig, profile); }
        vig.intensity.Override(0.32f);
        vig.smoothness.Override(0.45f);
        vig.color.Override(new Color(0.32f, 0.18f, 0.42f));
        ColorAdjustments ca; if (!profile.TryGet(out ca)) { ca = profile.Add<ColorAdjustments>(true); AssetDatabase.AddObjectToAsset(ca, profile); }
        ca.saturation.Override(18f);
        ca.contrast.Override(12f);
        ca.postExposure.Override(-0.15f);
        ca.colorFilter.Override(new Color(1f, 0.97f, 1f));
        Tonemapping tm; if (!profile.TryGet(out tm)) { tm = profile.Add<Tonemapping>(true); AssetDatabase.AddObjectToAsset(tm, profile); }
        tm.mode.Override(TonemappingMode.Neutral);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        vol.sharedProfile = profile;
    }

    static Material MakeSkybox()
    {
        string path = GenDir + "/RuyaGokyuzu.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Skybox/Procedural"));
        m.SetFloat("_SunSize", 0.03f);
        m.SetFloat("_AtmosphereThickness", 0.55f);
        m.SetColor("_SkyTint", new Color(0.78f, 0.62f, 0.95f));
        m.SetColor("_GroundColor", new Color(0.95f, 0.75f, 0.85f));
        m.SetFloat("_Exposure", 1.25f);
        return SaveAsset(m, path);
    }

    // =====================================================================
    //  ARAYÜZ (Oyun)
    // =====================================================================
    static void BuildGameUI(GameManager gm, AudioSource sfx)
    {
        var canvas = MakeCanvas("Arayüz (HUD)");
        var click = Clip("click");

        // Süre
        var timerBox = Panel(canvas.transform, "Süre Kutusu", new Color(1f, 1f, 1f, 0.82f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(230f, 130f));
        MakeText(timerBox.transform, "Etiket", "SÜRE", 30, new Color(0.45f, 0.35f, 0.6f), new Vector2(0f, 36f), new Vector2(220f, 40f));
        gm.timerText = MakeText(timerBox.transform, "Süre", "60", 78, new Color(0.3f, 0.22f, 0.45f), new Vector2(0f, -14f), new Vector2(220f, 90f));
        gm.timerNormalColor = new Color(0.3f, 0.22f, 0.45f);

        // Skor
        var scoreBox = Panel(canvas.transform, "Skor Kutusu", new Color(1f, 1f, 1f, 0.82f), new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(270f, 130f));
        MakeText(scoreBox.transform, "Etiket", "YUVADAKİ TOP", 26, new Color(0.45f, 0.35f, 0.6f), new Vector2(0f, 36f), new Vector2(260f, 40f));
        gm.scoreText = MakeText(scoreBox.transform, "Skor", "0 / 10", 64, new Color(0.94f, 0.42f, 0.45f), new Vector2(0f, -14f), new Vector2(260f, 90f));

        // İpucu ve isim
        var hint = MakeText(canvas.transform, "Kontroller", "WASD / Yön Tuşları: Yürü     ESC / P: Duraklat", 26, Color.white, Vector2.zero, new Vector2(800f, 40f));
        Anchor(hint.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -30f));
        hint.alignment = TextAlignmentOptions.Right;
        hint.outlineWidth = 0.2f; hint.outlineColor = new Color(0.3f, 0.2f, 0.4f);
        var credit = MakeText(canvas.transform, "Hazırlayan", "Mert Demir  •  2506127004  •  3B Oyun Geliştirme Stüdyosu", 22, new Color(1f, 1f, 1f, 0.9f), Vector2.zero, new Vector2(900f, 34f));
        Anchor(credit.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 16f));
        credit.alignment = TextAlignmentOptions.Left;
        credit.outlineWidth = 0.2f; credit.outlineColor = new Color(0.3f, 0.2f, 0.4f);

        // Orta mesaj
        var msg = MakeText(canvas.transform, "Mesaj", "", 54, Color.white, new Vector2(0f, 150f), new Vector2(1500f, 90f));
        Anchor(msg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f));
        msg.outlineWidth = 0.28f; msg.outlineColor = new Color(0.45f, 0.25f, 0.6f);
        gm.messageText = msg;

        // Kazanma paneli
        var win = Overlay(canvas.transform, "Kazanma Paneli");
        var winCard = Panel(win.transform, "Kart", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 520f));
        MakeText(winCard.transform, "Başlık", "KAZANDINIZ!", 96, new Color(0.35f, 0.7f, 0.4f), new Vector2(0f, 150f), new Vector2(820f, 120f));
        gm.winDetailText = MakeText(winCard.transform, "Detay", "", 36, new Color(0.35f, 0.28f, 0.45f), new Vector2(0f, 20f), new Vector2(780f, 140f));
        var b1 = MakeButton(winCard.transform, "Tekrar Oyna", new Color(0.4f, 0.73f, 0.42f), new Vector2(-190f, -160f), new Vector2(330f, 100f), sfx, click);
        UnityEventTools.AddPersistentListener(b1.onClick, gm.Restart);
        var b2 = MakeButton(winCard.transform, "Ana Menü", new Color(0.55f, 0.45f, 0.8f), new Vector2(190f, -160f), new Vector2(330f, 100f), sfx, click);
        UnityEventTools.AddPersistentListener(b2.onClick, gm.GoToMenu);
        gm.winPanel = win;
        win.SetActive(false);

        // Kaybetme paneli
        var lose = Overlay(canvas.transform, "Süre Bitti Paneli");
        var loseCard = Panel(lose.transform, "Kart", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 520f));
        MakeText(loseCard.transform, "Başlık", "SÜRE BİTTİ!", 96, new Color(0.94f, 0.36f, 0.4f), new Vector2(0f, 150f), new Vector2(820f, 120f));
        gm.loseDetailText = MakeText(loseCard.transform, "Detay", "", 36, new Color(0.35f, 0.28f, 0.45f), new Vector2(0f, 20f), new Vector2(780f, 140f));
        var b3 = MakeButton(loseCard.transform, "Tekrar Dene", new Color(0.94f, 0.45f, 0.5f), new Vector2(-190f, -160f), new Vector2(330f, 100f), sfx, click);
        UnityEventTools.AddPersistentListener(b3.onClick, gm.Restart);
        var b4 = MakeButton(loseCard.transform, "Ana Menü", new Color(0.55f, 0.45f, 0.8f), new Vector2(190f, -160f), new Vector2(330f, 100f), sfx, click);
        UnityEventTools.AddPersistentListener(b4.onClick, gm.GoToMenu);
        gm.losePanel = lose;
        lose.SetActive(false);

        // Duraklatma paneli
        var pauseMenu = canvas.gameObject.AddComponent<PauseMenu>();
        var pause = Overlay(canvas.transform, "Duraklatma Paneli");
        var pCard = Panel(pause.transform, "Kart", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 600f));
        MakeText(pCard.transform, "Başlık", "DURAKLATILDI", 72, new Color(0.45f, 0.35f, 0.65f), new Vector2(0f, 200f), new Vector2(600f, 100f));
        var p1 = MakeButton(pCard.transform, "Devam Et", new Color(0.4f, 0.73f, 0.42f), new Vector2(0f, 60f), new Vector2(400f, 96f), sfx, click);
        UnityEventTools.AddPersistentListener(p1.onClick, pauseMenu.Resume);
        var p2 = MakeButton(pCard.transform, "Yeniden Başla", new Color(1f, 0.65f, 0.25f), new Vector2(0f, -60f), new Vector2(400f, 96f), sfx, click);
        UnityEventTools.AddPersistentListener(p2.onClick, pauseMenu.RestartGame);
        var p3 = MakeButton(pCard.transform, "Ana Menü", new Color(0.55f, 0.45f, 0.8f), new Vector2(0f, -180f), new Vector2(400f, 96f), sfx, click);
        UnityEventTools.AddPersistentListener(p3.onClick, pauseMenu.BackToMenu);
        pauseMenu.pausePanel = pause;
        pause.SetActive(false);

        MakeEventSystem();
    }

    // =====================================================================
    //  ANA MENÜ SAHNESİ
    // =====================================================================
    public static void BuildMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = MakeSkybox();
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.66f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.82f, 0.68f, 0.76f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.48f, 0.42f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.90f, 0.82f, 0.95f);
        RenderSettings.fogStartDistance = 20f;
        RenderSettings.fogEndDistance = 70f;

        var sun = new GameObject("Güneş").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.3f;
        sun.color = new Color(1f, 0.93f, 0.85f);
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 45f;
        camGo.AddComponent<AudioListener>();
        var cad = camGo.AddComponent<UniversalAdditionalCameraData>();
        cad.renderPostProcessing = true;
        camGo.transform.position = new Vector3(0f, 5f, -14f);
        camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
        BuildPostProcess("OyunPostProcess");

        // Dönen oyuncak vitrini
        var display = new GameObject("Oyuncak Vitrini");
        display.transform.position = new Vector3(0f, 0f, 4f);
        var spin = display.AddComponent<YavasDon>();
        spin.rotationSpeed = new Vector3(0f, 8f, 0f);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.transform.SetParent(display.transform, false);
        floor.transform.localScale = new Vector3(26f, 0.2f, 26f);
        floor.GetComponent<Renderer>().sharedMaterial = Mat("MenuZemin", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/ground_playmat.png"), 0.2f);
        var rnd = new System.Random(5);
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.transform.SetParent(display.transform, false);
            b.transform.localScale = Vector3.one * 1.6f;
            b.transform.localPosition = new Vector3(Mathf.Cos(a) * 8f, 1f, Mathf.Sin(a) * 8f);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/ball_" + (i + 1).ToString("00") + ".png");
            b.GetComponent<Renderer>().sharedMaterial = Mat("Top_" + (i + 1), Color.white, tex, 0.55f);
            var bd = b.AddComponent<YavasDon>(); bd.rotationSpeed = new Vector3(0f, 40f, 20f); bd.bobAmount = 0.3f; bd.bobSpeed = 1.5f;
        }
        for (int i = 0; i < 7; i++)
        {
            float a = (i + 0.5f) / 7f * Mathf.PI * 2f;
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.GetComponent<MeshFilter>().sharedMesh = MeshAsset("HarfKupu", MakeBlock);
            c.transform.SetParent(display.transform, false);
            float s = 1.4f + (float)rnd.NextDouble();
            c.transform.localScale = Vector3.one * s;
            c.transform.localPosition = new Vector3(Mathf.Cos(a) * 4.5f, s * 0.5f + 0.1f, Mathf.Sin(a) * 4.5f);
            c.transform.localRotation = Quaternion.Euler(0f, rnd.Next(90), 0f);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/block_" + (i * 2).ToString("00") + ".png");
            c.GetComponent<Renderer>().sharedMaterial = Mat("Blok_" + tex.name, Color.white, tex, 0.35f);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Characters/Karakter_Rigged.fbx");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.transform.SetParent(display.transform, false);
        model.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        var anim = model.GetComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "/Animations/KarakterAnimator.controller");
        anim.applyRootMotion = false;

        var music = new GameObject("Müzik").AddComponent<AudioSource>();
        music.clip = Clip("music_box_loop");
        music.loop = true;
        music.volume = 0.4f;
        music.playOnAwake = true;
        var sfx = music.gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        var click = Clip("click");

        var canvas = MakeCanvas("Ana Menü Arayüzü");
        var mm = canvas.gameObject.AddComponent<MainMenu>();
        mm.gameSceneName = "Oyun";

        var title = MakeText(canvas.transform, "Başlık", "Oyuncak Rüyası", 150, Color.white, Vector2.zero, new Vector2(1400f, 190f));
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f));
        title.outlineWidth = 0.25f; title.outlineColor = new Color(0.5f, 0.3f, 0.7f);
        title.color = new Color(1f, 0.93f, 0.55f);
        var sub = MakeText(canvas.transform, "Alt Başlık", "Uyurgezerin 60 saniyelik rüyası: uyanmadan 10 topu yuvalarına yerleştir!", 36, Color.white, Vector2.zero, new Vector2(1500f, 60f));
        Anchor(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -275f));
        sub.outlineWidth = 0.22f; sub.outlineColor = new Color(0.45f, 0.3f, 0.6f);

        var bPlay = MakeButton(canvas.transform, "BAŞLA", new Color(0.4f, 0.73f, 0.42f), new Vector2(0f, -40f), new Vector2(440f, 110f), sfx, click);
        UnityEventTools.AddPersistentListener(bPlay.onClick, mm.PlayGame);
        var bHow = MakeButton(canvas.transform, "Nasıl Oynanır", new Color(0.26f, 0.6f, 0.95f), new Vector2(0f, -170f), new Vector2(440f, 96f), sfx, click);
        UnityEventTools.AddPersistentListener(bHow.onClick, mm.OpenHowTo);
        var bQuit = MakeButton(canvas.transform, "Çıkış", new Color(0.94f, 0.42f, 0.48f), new Vector2(0f, -285f), new Vector2(440f, 96f), sfx, click);
        UnityEventTools.AddPersistentListener(bQuit.onClick, mm.QuitGame);
        mm.quitButton = bQuit.gameObject;

        var credit = MakeText(canvas.transform, "Hazırlayan", "Hazırlayan: Mert Demir  •  Öğrenci No: 2506127004  •  3B Oyun Geliştirme Stüdyosu", 28, Color.white, Vector2.zero, new Vector2(1600f, 44f));
        Anchor(credit.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f));
        credit.outlineWidth = 0.22f; credit.outlineColor = new Color(0.4f, 0.25f, 0.55f);

        // Nasıl oynanır paneli
        var how = Overlay(canvas.transform, "Nasıl Oynanır Paneli");
        var card = Panel(how.transform, "Kart", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 780f));
        MakeText(card.transform, "Başlık", "Nasıl Oynanır?", 70, new Color(0.45f, 0.35f, 0.65f), new Vector2(0f, 310f), new Vector2(1100f, 90f));
        var body = MakeText(card.transform, "Metin",
            "Bir gece rüyanda kendini dev oyuncakların arasında buldun. Uyurgezer gibi kolların önde yürüyorsun!\n" +
            "Uyanmak için <b>60 saniye</b> içinde <b>10 topu</b> yuvalarına yuvarlamalısın.\n\n" +
            "<b>W A S D / Yön Tuşları:</b>  Yürü\n" +
            "<b>Topları it:</b>  Topa doğru yürü, kollarınla iterek bayraklı yuvalara düşür\n" +
            "<b>Kovalayan küpler:</b>  Yaklaşınca seni kovalar, çarparsa geri itilirsin\n" +
            "<b>ESC / P:</b>  Duraklat\n\n" +
            "Saat çalmadan bütün topları yerleştirirsen kazanırsın!",
            32, new Color(0.3f, 0.25f, 0.4f), new Vector2(0f, -10f), new Vector2(1080f, 520f));
        body.alignment = TextAlignmentOptions.Left;
        body.lineSpacing = 8f;
        var bClose = MakeButton(card.transform, "Tamam", new Color(0.4f, 0.73f, 0.42f), new Vector2(0f, -320f), new Vector2(320f, 90f), sfx, click);
        UnityEventTools.AddPersistentListener(bClose.onClick, mm.CloseHowTo);
        mm.howToPanel = how;
        how.SetActive(false);

        MakeEventSystem();
        EditorSceneManager.SaveScene(scene, Root + "/Scenes/AnaMenu.unity");
    }

    // =====================================================================
    //  YARDIMCILAR
    // =====================================================================
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
        go.GetComponent<Image>().color = new Color(0.22f, 0.12f, 0.32f, 0.55f);
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

    static string ColorKey(Color c) => ColorUtility.ToHtmlStringRGB(c);

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
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Smoothness", smooth);
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

    static Mesh MakeAnnulus(float inner, float outer, int seg, float outerY = 0f)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(d * inner); uv.Add(new Vector2(i / (float)seg, 0f));
            v.Add(d * outer + Vector3.up * outerY); uv.Add(new Vector2(i / (float)seg, 1f));
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

    /// <summary>Her yüzünde harfin dik ve okunur göründüğü küp (Unity küpünde bazı yüzler ters).</summary>
    static Mesh MakeBlock()
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        var faces = new (Vector3 normal, Vector3 up)[]
        {
            (Vector3.forward, Vector3.up), (Vector3.back, Vector3.up), (Vector3.right, Vector3.up),
            (Vector3.left, Vector3.up), (Vector3.up, Vector3.forward), (Vector3.down, Vector3.back),
        };
        foreach (var f in faces)
        {
            Vector3 right = Vector3.Cross(f.up, -f.normal);
            int b = v.Count;
            Vector3 c = f.normal * 0.5f;
            v.Add(c - right * 0.5f - f.up * 0.5f); uv.Add(new Vector2(0, 0));
            v.Add(c - right * 0.5f + f.up * 0.5f); uv.Add(new Vector2(0, 1));
            v.Add(c + right * 0.5f + f.up * 0.5f); uv.Add(new Vector2(1, 1));
            v.Add(c + right * 0.5f - f.up * 0.5f); uv.Add(new Vector2(1, 0));
            for (int k = 0; k < 4; k++) n.Add(f.normal);
            tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
        var m = new Mesh();
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateTangents(); m.RecalculateBounds();
        return m;
    }

    static Mesh MakeCone(float radius, float height, int seg)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        v.Add(new Vector3(0f, height, 0f));
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        for (int i = 1; i <= seg; i++) tri.AddRange(new[] { 0, i + 1, i });
        var m = new Mesh();
        m.SetVertices(v); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
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

    static Mesh MakeFlag()
    {
        var m = new Mesh();
        m.SetVertices(new List<Vector3> { new Vector3(0, 0.45f, 0), new Vector3(0, -0.45f, 0), new Vector3(1.3f, 0, 0), new Vector3(0, 0.45f, 0), new Vector3(0, -0.45f, 0), new Vector3(1.3f, 0, 0) });
        m.SetTriangles(new[] { 0, 2, 1, 3, 4, 5 }, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
}
#endif
