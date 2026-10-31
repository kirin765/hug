using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hug
{
    /// <summary>Builds the floating hug island: sky, light, floor, boundary, clouds.</summary>
    public static class World
    {
        public static readonly Color Sky = new Color(0.58f, 0.82f, 1f);

        public static CameraRig Build(Transform parent, float arenaRadius)
        {
            BuildLighting();
            var rig = BuildCamera(parent);
            BuildIsland(parent, arenaRadius);
            return rig;
        }

        static void BuildLighting()
        {
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 0.88f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.42f;
            light.shadowBias = 0.04f;
            lightGo.transform.rotation = Quaternion.Euler(48f, -38f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.66f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.56f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.30f, 0.42f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0072f;
            RenderSettings.fogColor = Sky;
        }

        static CameraRig BuildCamera(Transform parent)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(parent, false);

            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 260f;
            cam.allowHDR = false;
            cam.allowMSAA = true;

            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;

            camGo.AddComponent<AudioListener>();

            var rig = camGo.AddComponent<CameraRig>();
            return rig;
        }

        static void BuildIsland(Transform parent, float arenaRadius)
        {
            var island = new GameObject("Island").transform;
            island.SetParent(parent, false);

            // floor
            var floorMat = new Material(Art.Ground) { name = "IslandFloor" };
            var floorTex = Art.Checker(256,
                new Color(0.66f, 0.92f, 0.60f),
                new Color(0.57f, 0.85f, 0.52f),
                new Color(0.50f, 0.78f, 0.46f));
            floorTex.wrapMode = TextureWrapMode.Repeat;
            floorTex.filterMode = FilterMode.Bilinear;
            floorMat.mainTexture = floorTex;
            floorMat.mainTextureScale = new Vector2(11f, 11f);

            var floor = new GameObject("Floor");
            floor.transform.SetParent(island, false);
            var mf = floor.AddComponent<MeshFilter>();
            mf.sharedMesh = Art.Disc(arenaRadius + 3f, 110);
            var mr = floor.AddComponent<MeshRenderer>();
            mr.sharedMaterial = floorMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            // rim / soil wall
            var rimMat = Art.Tint(Art.Ground, new Color(0.72f, 0.62f, 0.86f));
            var rim = new GameObject("Rim");
            rim.transform.SetParent(island, false);
            rim.AddComponent<MeshFilter>().sharedMesh = Art.Rim(arenaRadius + 3f, 1.7f, 110);
            var rimRenderer = rim.AddComponent<MeshRenderer>();
            rimRenderer.sharedMaterial = rimMat;
            rimRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // glowing boundary
            var ringGo = new GameObject("Boundary");
            ringGo.transform.SetParent(island, false);
            var line = ringGo.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 128;
            line.widthMultiplier = 0.14f;
            line.numCapVertices = 2;
            line.material = Art.Tint(Art.Fx, new Color(1f, 0.42f, 0.66f, 0.95f));
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int i = 0; i < 128; i++)
            {
                float a = i / 128f * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(
                    Mathf.Cos(a) * (arenaRadius + 0.15f),
                    0.06f,
                    Mathf.Sin(a) * (arenaRadius + 0.15f)));
            }

            BuildDecor(island, arenaRadius);
            BuildClouds(island);
        }

        static void BuildDecor(Transform parent, float arenaRadius)
        {
            var decor = new GameObject("Decor").transform;
            decor.SetParent(parent, false);

            // one material per palette colour instead of one per flower
            var paletteMats = new Material[Palette.Colors.Length];
            for (int i = 0; i < paletteMats.Length; i++)
            {
                paletteMats[i] = Art.Tint(Art.Lit, Palette.Colors[i]);
            }

            // little flowers / pebbles in the ring outside the play area
            for (int i = 0; i < 110; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(arenaRadius + 0.9f, arenaRadius + 2.8f);
                float size = Random.Range(0.12f, 0.3f);

                var flower = Art.Sphere("Flower", decor);
                flower.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    size * 0.5f,
                    Mathf.Sin(angle) * radius);
                flower.transform.localScale = Vector3.one * size;
                var renderer = flower.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = paletteMats[Random.Range(0, paletteMats.Length)];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            // bushes
            var leaf = Art.Tint(Art.Lit, new Color(0.46f, 0.78f, 0.52f));
            for (int i = 0; i < 14; i++)
            {
                float angle = (i / 14f) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
                float radius = Random.Range(arenaRadius + 1.4f, arenaRadius + 2.6f);
                var bushRoot = new GameObject("Bush").transform;
                bushRoot.SetParent(decor, false);
                bushRoot.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                for (int b = 0; b < 3; b++)
                {
                    var puff = Art.Sphere("Puff", bushRoot);
                    puff.transform.localPosition = new Vector3(
                        Random.Range(-0.3f, 0.3f),
                        Random.Range(0.25f, 0.45f),
                        Random.Range(-0.3f, 0.3f));
                    float s = Random.Range(0.45f, 0.8f);
                    puff.transform.localScale = new Vector3(s, s * 0.85f, s);
                    puff.GetComponent<MeshRenderer>().sharedMaterial = leaf;
                }
            }
        }

        static void BuildClouds(Transform parent)
        {
            var clouds = new GameObject("Clouds").transform;
            clouds.SetParent(parent, false);

            var cloudMat = Art.Tint(Art.Lit, new Color(1f, 1f, 1f));

            for (int i = 0; i < 14; i++)
            {
                float angle = (i / 14f) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                float radius = Random.Range(48f, 82f);
                float height = Random.Range(7f, 20f);

                var root = new GameObject("Cloud").transform;
                root.SetParent(clouds, false);
                root.localPosition = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);

                int puffs = Random.Range(3, 6);
                for (int p = 0; p < puffs; p++)
                {
                    var puff = Art.Sphere("Puff", root);
                    puff.transform.localPosition = new Vector3(
                        Random.Range(-2.6f, 2.6f),
                        Random.Range(-0.5f, 0.6f),
                        Random.Range(-1.6f, 1.6f));
                    float s = Random.Range(1.8f, 3.6f);
                    puff.transform.localScale = new Vector3(s * 1.3f, s * 0.75f, s);
                    var r = puff.GetComponent<MeshRenderer>();
                    r.sharedMaterial = cloudMat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
        }
    }
}
