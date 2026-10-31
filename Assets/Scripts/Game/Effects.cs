using System.Collections.Generic;
using UnityEngine;

namespace Hug
{
    /// <summary>Hearts, ring pulses and floating "+1" text. All pooled.</summary>
    public class Effects : MonoBehaviour
    {
        class Particle
        {
            public Transform Tr;
            public Renderer Renderer;
            public MaterialPropertyBlock Block;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Spin;
            public Color Color;
            public bool Active;
        }

        class Popup
        {
            public TextMesh Text;
            public Transform Tr;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public Color Color;
            public bool Active;
        }

        Camera _camera;
        Material _heartMaterial;
        Material _ringMaterial;
        Material _puffMaterial;
        Texture2D _heartTexture;
        Texture2D _ringTexture;
        Texture2D _puffTexture;

        readonly List<Particle> _hearts = new List<Particle>();
        readonly List<Particle> _puffs = new List<Particle>();
        readonly List<Particle> _pulses = new List<Particle>();
        readonly List<Popup> _popups = new List<Popup>();

        public void Setup()
        {
            _camera = Camera.main;

            _heartTexture = Art.Heart(64);
            _ringTexture = Art.Ring(128, 0.78f, 3f);

            _heartMaterial = Art.Tint(Art.Fx, Color.white);
            _heartMaterial.mainTexture = _heartTexture;

            _ringMaterial = Art.Tint(Art.Fx, Color.white);
            _ringMaterial.mainTexture = _ringTexture;

            _puffTexture = Art.Circle(64, 10f);
            _puffMaterial = Art.Tint(Art.Fx, Color.white);
            _puffMaterial.mainTexture = _puffTexture;
        }

        // ------------------------------------------------------------ spawning

        public void HugBurst(Vector3 position, Color color, int count = 9)
        {
            for (int i = 0; i < count; i++)
            {
                var p = GetParticle(_hearts, _heartMaterial, 12);
                if (p == null) break;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float spread = Random.Range(0.6f, 2.2f);
                p.Tr.position = position + new Vector3(
                    Mathf.Cos(angle) * Random.Range(0f, 0.5f),
                    Random.Range(0.4f, 1.6f),
                    Mathf.Sin(angle) * Random.Range(0f, 0.5f));
                p.Velocity = new Vector3(Mathf.Cos(angle) * spread, Random.Range(1.6f, 3.4f), Mathf.Sin(angle) * spread);
                p.MaxLife = Random.Range(0.9f, 1.5f);
                p.Life = p.MaxLife;
                p.Size = Random.Range(0.30f, 0.55f);
                p.Spin = Random.Range(-140f, 140f);
                p.Color = i % 3 == 0 ? Palette.Lighten(color, 0.6f) : color;
                p.Active = true;
                p.Tr.gameObject.SetActive(true);
            }
        }

        public void Pulse(Vector3 position, Color color)
        {
            var p = GetParticle(_pulses, _ringMaterial, 4);
            if (p == null) return;
            p.Tr.position = new Vector3(position.x, 0.06f, position.z);
            p.Tr.rotation = Quaternion.Euler(90f, 0f, 0f);
            p.Velocity = Vector3.zero;
            p.MaxLife = 0.55f;
            p.Life = p.MaxLife;
            p.Size = 1f;
            p.Spin = 0f;
            p.Color = color;
            p.Active = true;
            p.Tr.gameObject.SetActive(true);
        }

        /// <summary>Dust kicked up behind a dash.</summary>
        public void DashPuff(Vector3 position, Vector3 direction)
        {
            for (int i = 0; i < 7; i++)
            {
                var p = GetParticle(_puffs, _puffMaterial, 12);
                if (p == null) break;

                Vector3 back = -direction.normalized;
                p.Tr.position = position + new Vector3(
                    Random.Range(-0.35f, 0.35f),
                    Random.Range(0.05f, 0.45f),
                    Random.Range(-0.35f, 0.35f));
                p.Velocity = back * Random.Range(1.5f, 4f) + Vector3.up * Random.Range(0.4f, 1.4f);
                p.MaxLife = Random.Range(0.28f, 0.5f);
                p.Life = p.MaxLife;
                p.Size = Random.Range(0.28f, 0.55f);
                p.Spin = 0f;
                p.Color = new Color(1f, 1f, 1f, 0.85f);
                p.Active = true;
                p.Tr.gameObject.SetActive(true);
            }
        }

        public void PopText(Vector3 position, string text, Color color, float scale = 1f)
        {
            Popup pop = null;
            for (int i = 0; i < _popups.Count; i++)
            {
                if (!_popups[i].Active)
                {
                    pop = _popups[i];
                    break;
                }
            }

            if (pop == null)
            {
                var go = new GameObject("PopText");
                go.transform.SetParent(transform, false);
                var tm = go.AddComponent<TextMesh>();
                tm.font = Art.Font;
                tm.fontSize = 80;
                tm.characterSize = 0.03f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                go.GetComponent<MeshRenderer>().sharedMaterial = Art.Font.material;
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pop = new Popup { Text = tm, Tr = go.transform };
                _popups.Add(pop);
            }

            pop.Tr.position = position + Vector3.up * 1.9f;
            pop.Tr.localScale = Vector3.one * scale;
            pop.Velocity = new Vector3(Random.Range(-0.25f, 0.25f), 1.9f, 0f);
            pop.MaxLife = 1.1f;
            pop.Life = pop.MaxLife;
            pop.Color = color;
            pop.Text.text = text;
            pop.Text.color = color;
            pop.Active = true;
            pop.Tr.gameObject.SetActive(true);
        }

        // -------------------------------------------------------------- update

        void Update()
        {
            float dt = Time.deltaTime;
            if (_camera == null) _camera = Camera.main;
            Quaternion billboard = _camera != null ? _camera.transform.rotation : Quaternion.identity;

            for (int i = 0; i < _hearts.Count; i++)
            {
                var p = _hearts[i];
                if (!p.Active) continue;

                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Active = false;
                    p.Tr.gameObject.SetActive(false);
                    continue;
                }

                p.Velocity += Vector3.down * 4.2f * dt;
                p.Tr.position += p.Velocity * dt;
                p.Tr.rotation = billboard * Quaternion.Euler(0f, 0f, Time.time * p.Spin);

                float t = p.Life / p.MaxLife;
                float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) * 5f));
                float alpha = Mathf.Clamp01(t * 2.2f);
                p.Tr.localScale = Vector3.one * (p.Size * (0.5f + grow * 0.5f));
                SetColor(p, new Color(p.Color.r, p.Color.g, p.Color.b, p.Color.a * alpha));
            }

            for (int i = 0; i < _puffs.Count; i++)
            {
                var p = _puffs[i];
                if (!p.Active) continue;

                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Active = false;
                    p.Tr.gameObject.SetActive(false);
                    continue;
                }

                p.Velocity += Vector3.down * 1.2f * dt;
                p.Velocity *= 1f - Mathf.Clamp01(4f * dt);
                p.Tr.position += p.Velocity * dt;
                p.Tr.rotation = billboard;

                float pt = p.Life / p.MaxLife;
                float pGrow = Mathf.Lerp(1f, 1.8f, 1f - pt);
                p.Tr.localScale = Vector3.one * p.Size * pGrow;
                SetColor(p, new Color(p.Color.r, p.Color.g, p.Color.b, p.Color.a * pt));
            }

            for (int i = 0; i < _pulses.Count; i++)
            {
                var p = _pulses[i];
                if (!p.Active) continue;

                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Active = false;
                    p.Tr.gameObject.SetActive(false);
                    continue;
                }

                float t = 1f - p.Life / p.MaxLife;
                float size = Mathf.Lerp(1.2f, 4.2f, t);
                p.Tr.localScale = new Vector3(size, size, 1f);
                SetColor(p, new Color(p.Color.r, p.Color.g, p.Color.b, (1f - t) * 0.75f));
            }

            for (int i = 0; i < _popups.Count; i++)
            {
                var pop = _popups[i];
                if (!pop.Active) continue;

                pop.Life -= dt;
                if (pop.Life <= 0f)
                {
                    pop.Active = false;
                    pop.Tr.gameObject.SetActive(false);
                    continue;
                }

                pop.Velocity += Vector3.down * 1.6f * dt;
                pop.Tr.position += pop.Velocity * dt;
                pop.Tr.rotation = billboard;
                pop.Tr.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.85f, 1f - pop.Life / pop.MaxLife);

                float a = Mathf.Clamp01(pop.Life / pop.MaxLife * 2f);
                pop.Text.color = new Color(pop.Color.r, pop.Color.g, pop.Color.b, a);
            }
        }

        static void SetColor(Particle p, Color color)
        {
            if (p.Block == null) p.Block = new MaterialPropertyBlock();
            p.Block.SetColor("_BaseColor", color);
            p.Renderer.SetPropertyBlock(p.Block);
        }

        Particle GetParticle(List<Particle> list, Material material, int preallocate)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Active) return list[i];
            }

            if (list.Count >= 48) return null;

            var go = Art.Quad("Fx", transform, material);
            var renderer = go.GetComponent<MeshRenderer>();
            var particle = new Particle
            {
                Tr = go.transform,
                Renderer = renderer,
                Block = new MaterialPropertyBlock(),
            };
            list.Add(particle);
            go.SetActive(false);
            return particle;
        }

        void OnDestroy()
        {
            if (_heartTexture != null) Destroy(_heartTexture);
            if (_ringTexture != null) Destroy(_ringTexture);
            if (_puffTexture != null) Destroy(_puffTexture);
            if (_heartMaterial != null) Destroy(_heartMaterial);
            if (_ringMaterial != null) Destroy(_ringMaterial);
            if (_puffMaterial != null) Destroy(_puffMaterial);
        }
    }
}
