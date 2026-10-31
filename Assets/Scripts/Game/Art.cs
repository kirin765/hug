using System.Collections.Generic;
using UnityEngine;

namespace Hug
{
    /// <summary>
    /// Everything visual is generated at runtime or loaded from Resources, so the
    /// project stays tiny and the WebGL build loads fast.
    /// </summary>
    public static class Art
    {
        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/VarelaRound");
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        // ------------------------------------------------------------ materials

        static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        static Material Load(string resource, string shaderName, Color color)
        {
            if (_cache.TryGetValue(resource, out var cached) && cached != null) return cached;

            var mat = Resources.Load<Material>(resource);
            if (mat == null)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                mat = new Material(shader) { name = resource };
                Debug.LogWarning($"[HUG] Missing material asset Resources/{resource}, using a runtime fallback.");
            }
            _cache[resource] = mat;
            return mat;
        }

        public static Material Lit => Load("Materials/Player", "Universal Render Pipeline/Lit", Color.white);
        public static Material Ground => Load("Materials/Ground", "Universal Render Pipeline/Lit", Color.white);
        public static Material Fx => Load("Materials/Fx", "Universal Render Pipeline/Unlit", Color.white);

        /// <summary>A tinted, independent copy of a base material.</summary>
        public static Material Tint(Material baseMaterial, Color color)
        {
            var mat = new Material(baseMaterial) { color = color };
            return mat;
        }

        // -------------------------------------------------------------- textures

        static Texture2D NewTexture(int size, bool linear = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2,
            };
            return tex;
        }

        /// <summary>Soft checkerboard for the island floor.</summary>
        public static Texture2D Checker(int size, Color a, Color b, Color line)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            const int cells = 4;
            float cellSize = size / (float)cells;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = (int)(x / cellSize);
                    int cy = (int)(y / cellSize);
                    bool odd = ((cx + cy) & 1) == 1;
                    Color c = odd ? b : a;

                    // subtle dotted texture inside each cell
                    float fx = (x % cellSize) / cellSize - 0.5f;
                    float fy = (y % cellSize) / cellSize - 0.5f;
                    float d = Mathf.Sqrt(fx * fx + fy * fy);
                    if (d < 0.09f) c = Color.Lerp(c, line, 0.55f);

                    // grid lines
                    float gx = Mathf.Min(x % cellSize, cellSize - (x % cellSize));
                    float gy = Mathf.Min(y % cellSize, cellSize - (y % cellSize));
                    if (gx < 1.2f || gy < 1.2f) c = Color.Lerp(c, line, 0.7f);

                    px[y * size + x] = c;
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Heart shape with a soft edge. 0,0 is bottom-left.</summary>
        public static Texture2D Heart(int size)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cover = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float u = (x + 0.25f + sx * 0.5f - half) / half;
                            float v = (y + 0.25f + sy * 0.5f - half) / half;
                            // heart implicit: (u² + v² − 1)³ − u²v³ ≤ 0
                            u *= 1.18f;
                            v *= 1.18f;
                            v += 0.12f;
                            float t = u * u + v * v - 1f;
                            if (t * t * t - u * u * v * v * v <= 0f) cover += 0.25f;
                        }
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        public static Texture2D Circle(int size, float softness = 2.5f)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half + 0.5f) * (x - half + 0.5f) + (y - half + 0.5f) * (y - half + 0.5f));
                    float a = Mathf.Clamp01((half - d) / softness);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        public static Texture2D Ring(int size, float innerFraction, float softness = 2f)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            float inner = half * innerFraction;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half + 0.5f) * (x - half + 0.5f) + (y - half + 0.5f) * (y - half + 0.5f));
                    float a = Mathf.Clamp01((d - inner) / softness) * Mathf.Clamp01((half - d) / softness);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Rounded rectangle usable as a 9-sliced UI sprite.</summary>
        public static Sprite RoundedSprite(int radius = 24)
        {
            int size = radius * 2 + 8;
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float r = radius;
            Vector2 halfSize = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(
                        Mathf.Abs(x - halfSize.x) - (halfSize.x - r),
                        Mathf.Abs(y - halfSize.y) - (halfSize.y - r));
                    float d = Mathf.Min(Mathf.Max(p.x, p.y), 0f) +
                              Mathf.Sqrt(Mathf.Max(p.x, 0f) * Mathf.Max(p.x, 0f) + Mathf.Max(p.y, 0f) * Mathf.Max(p.y, 0f)) - r;
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true);

            int b = radius + 2;
            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(b, b, b, b));
        }

        public static Sprite TextureSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---------------------------------------------------------------- meshes

        /// <summary>Flat disc facing up, with UVs laid out radially.</summary>
        public static Mesh Disc(float radius, int segments = 96)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            verts.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * radius;
                float z = Mathf.Sin(a) * radius;
                verts.Add(new Vector3(x, 0f, z));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }

            for (int i = 1; i <= segments; i++)
            {
                tris.Add(0);
                tris.Add(i + 1);
                tris.Add(i);
            }

            var mesh = new Mesh { name = "Disc" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Vertical wall around the island rim.</summary>
        public static Mesh Rim(float radius, float depth, int segments = 96)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * radius;
                float z = Mathf.Sin(a) * radius;
                verts.Add(new Vector3(x, 0f, z));
                verts.Add(new Vector3(x, -depth, z));
                float u = i / (float)segments * 8f;
                uvs.Add(new Vector2(u, 1f));
                uvs.Add(new Vector2(u, 0f));
            }

            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.Add(b);
                tris.Add(b + 2);
                tris.Add(b + 1);
                tris.Add(b + 2);
                tris.Add(b + 3);
                tris.Add(b + 1);
            }

            var mesh = new Mesh { name = "Rim" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // --------------------------------------------------------------- objects

        static Mesh _sphereMesh;
        static Mesh _quadMesh;

        /// <summary>Unit sphere (radius 0.5) with smooth normals, no collider.</summary>
        public static Mesh SphereMesh
        {
            get
            {
                if (_sphereMesh != null) return _sphereMesh;

                const int segments = 20;
                const int rings = 14;
                var verts = new List<Vector3>();
                var normals = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();

                for (int y = 0; y <= rings; y++)
                {
                    float v = y / (float)rings;
                    float phi = v * Mathf.PI;
                    for (int x = 0; x <= segments; x++)
                    {
                        float u = x / (float)segments;
                        float theta = u * Mathf.PI * 2f;
                        var n = new Vector3(
                            Mathf.Sin(phi) * Mathf.Cos(theta),
                            Mathf.Cos(phi),
                            Mathf.Sin(phi) * Mathf.Sin(theta));
                        verts.Add(n * 0.5f);
                        normals.Add(n);
                        uvs.Add(new Vector2(u, 1f - v));
                    }
                }

                for (int y = 0; y < rings; y++)
                {
                    for (int x = 0; x < segments; x++)
                    {
                        int a = y * (segments + 1) + x;
                        int b = a + segments + 1;
                        tris.Add(a);
                        tris.Add(a + 1);
                        tris.Add(b);
                        tris.Add(a + 1);
                        tris.Add(b + 1);
                        tris.Add(b);
                    }
                }

                _sphereMesh = new Mesh { name = "HugSphere" };
                _sphereMesh.SetVertices(verts);
                _sphereMesh.SetNormals(normals);
                _sphereMesh.SetUVs(0, uvs);
                _sphereMesh.SetTriangles(tris, 0);
                _sphereMesh.RecalculateBounds();
                return _sphereMesh;
            }
        }

        public static Mesh QuadMesh
        {
            get
            {
                if (_quadMesh != null) return _quadMesh;

                _quadMesh = new Mesh { name = "HugQuad" };
                _quadMesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                });
                _quadMesh.SetNormals(new List<Vector3>
                {
                    Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                });
                _quadMesh.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f),
                });
                _quadMesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
                _quadMesh.RecalculateBounds();
                return _quadMesh;
            }
        }

        /// <summary>Sphere primitive without a collider, ready to be a visual only.</summary>
        public static GameObject Sphere(string name, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = SphereMesh;
            go.AddComponent<MeshRenderer>();
            return go;
        }

        public static GameObject Quad(string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }
    }
}
