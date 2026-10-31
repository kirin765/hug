using UnityEngine;

namespace Hug
{
    /// <summary>
    /// Builds and animates a little hug buddy out of primitives: bean body, eyes,
    /// cheeks, two arms that reach forward when hugging, and a billboarded name.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        Transform _body;
        Transform _shoulderL;
        Transform _shoulderR;
        Transform _shadow;
        TextMesh _nameTag;
        TextMesh _nameShadow;

        Material _bodyMat;
        Material _bellyMat;
        Material _limbMat;
        Material _eyeMat;

        Color _color = Color.white;
        string _name = "";
        float _hug;          // 0..1 eased hug pose
        float _walk;         // 0..1 movement amount
        bool _hugTarget;
        float _spawnTime;

        public Transform BodyPivot => _body;

        public void Build(Color color, string playerName)
        {
            _color = color;
            _name = playerName;
            _spawnTime = Time.time;

            _bodyMat = Art.Tint(Art.Lit, color);
            _bellyMat = Art.Tint(Art.Lit, Palette.Lighten(color, 0.55f));
            _limbMat = Art.Tint(Art.Lit, Palette.Darken(color, 0.18f));
            _eyeMat = Art.Tint(Art.Lit, new Color(0.13f, 0.11f, 0.18f));

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);

            // torso
            var torso = Art.Sphere("Torso", _body);
            torso.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            torso.transform.localScale = new Vector3(0.64f, 0.72f, 0.58f);
            torso.GetComponent<MeshRenderer>().sharedMaterial = _bodyMat;

            // belly patch
            var belly = Art.Sphere("Belly", _body);
            belly.transform.localPosition = new Vector3(0f, 0.63f, 0.22f);
            belly.transform.localScale = new Vector3(0.44f, 0.50f, 0.30f);
            belly.GetComponent<MeshRenderer>().sharedMaterial = _bellyMat;

            // eyes
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var eye = Art.Sphere("Eye", _body);
                eye.transform.localPosition = new Vector3(0.17f * side, 0.94f, 0.44f);
                eye.transform.localScale = new Vector3(0.145f, 0.175f, 0.10f);
                eye.GetComponent<MeshRenderer>().sharedMaterial = _eyeMat;

                var glint = Art.Sphere("Glint", _body);
                glint.transform.localPosition = new Vector3(0.205f * side, 1.00f, 0.495f);
                glint.transform.localScale = new Vector3(0.052f, 0.052f, 0.052f);
                glint.GetComponent<MeshRenderer>().sharedMaterial = _bellyMat;
            }

            // cheeks
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var cheek = Art.Sphere("Cheek", _body);
                cheek.transform.localPosition = new Vector3(0.30f * side, 0.80f, 0.40f);
                cheek.transform.localScale = new Vector3(0.11f, 0.075f, 0.05f);
                cheek.GetComponent<MeshRenderer>().sharedMaterial =
                    Art.Tint(Art.Lit, new Color(1f, 0.55f, 0.65f));
            }

            // mouth
            var mouth = Art.Sphere("Mouth", _body);
            mouth.transform.localPosition = new Vector3(0f, 0.79f, 0.50f);
            mouth.transform.localScale = new Vector3(0.15f, 0.06f, 0.05f);
            mouth.GetComponent<MeshRenderer>().sharedMaterial = _eyeMat;

            // arms
            _shoulderL = MakeArm("ShoulderL", -1f);
            _shoulderR = MakeArm("ShoulderR", 1f);

            // feet
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var foot = Art.Sphere("Foot", transform);
                foot.transform.localPosition = new Vector3(0.23f * side, 0.07f, 0.03f);
                foot.transform.localScale = new Vector3(0.26f, 0.14f, 0.34f);
                foot.GetComponent<MeshRenderer>().sharedMaterial = _limbMat;
            }

            // soft blob shadow
            var shadowMat = Art.Tint(Art.Fx, new Color(0.05f, 0.02f, 0.1f, 0.28f));
            shadowMat.mainTexture = Art.Circle(64);
            var shadow = Art.Quad("Shadow", transform, shadowMat);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            shadow.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
            _shadow = shadow.transform;

            BuildNameTag(playerName);
            ApplyHugPose(0f);

            // little pop-in
            transform.localScale = Vector3.zero;
        }

        Transform MakeArm(string name, float side)
        {
            var shoulder = new GameObject(name).transform;
            shoulder.SetParent(_body, false);
            shoulder.localPosition = new Vector3(0.55f * side, 0.82f, 0f);

            var arm = Art.Sphere("Arm", shoulder);
            arm.transform.localPosition = new Vector3(0.02f * side, -0.24f, 0f);
            arm.transform.localScale = new Vector3(0.15f, 0.26f, 0.15f);
            arm.GetComponent<MeshRenderer>().sharedMaterial = _limbMat;

            var hand = Art.Sphere("Hand", shoulder);
            hand.transform.localPosition = new Vector3(0.02f * side, -0.44f, 0f);
            hand.transform.localScale = new Vector3(0.19f, 0.19f, 0.19f);
            hand.GetComponent<MeshRenderer>().sharedMaterial = _bellyMat;

            return shoulder;
        }

        void BuildNameTag(string playerName)
        {
            var tag = new GameObject("NameTag");
            tag.transform.SetParent(transform, false);
            tag.transform.localPosition = new Vector3(0f, 2.02f, 0f);
            _nameTag = tag.AddComponent<TextMesh>();
            ConfigureTag(_nameTag, playerName, new Color(1f, 1f, 1f, 0.95f));
            tag.GetComponent<MeshRenderer>().sharedMaterial = Art.Font.material;
            tag.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var shadow = new GameObject("NameShadow");
            shadow.transform.SetParent(tag.transform, false);
            shadow.transform.localPosition = new Vector3(0.012f, -0.012f, 0.012f);
            _nameShadow = shadow.AddComponent<TextMesh>();
            ConfigureTag(_nameShadow, playerName, new Color(0.08f, 0.05f, 0.14f, 0.8f));
            shadow.GetComponent<MeshRenderer>().sharedMaterial = Art.Font.material;
            shadow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void ConfigureTag(TextMesh tag, string text, Color color)
        {
            tag.text = text;
            tag.font = Art.Font;
            tag.fontSize = 72;
            tag.characterSize = 0.034f;
            tag.anchor = TextAnchor.MiddleCenter;
            tag.alignment = TextAlignment.Center;
            tag.color = color;
        }

        public void SetName(string playerName)
        {
            _name = playerName;
            if (_nameTag != null) _nameTag.text = playerName;
            if (_nameShadow != null) _nameShadow.text = playerName;
        }

        public void SetColor(Color color)
        {
            if (_color == color) return;
            _color = color;
            if (_bodyMat != null) _bodyMat.color = color;
            if (_bellyMat != null) _bellyMat.color = Palette.Lighten(color, 0.55f);
            if (_limbMat != null) _limbMat.color = Palette.Darken(color, 0.18f);
        }

        public void SetHugging(bool hugging)
        {
            _hugTarget = hugging;
        }

        public void SetWalk(float amount)
        {
            _walk = Mathf.Clamp01(amount);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            _hug = Mathf.MoveTowards(_hug, _hugTarget ? 1f : 0f, dt * (1f / 0.18f));

            // squash-and-stretch bob
            float speed = 4.2f + _walk * 7f;
            float bob = Mathf.Sin(Time.time * speed) * (0.018f + _walk * 0.045f);
            if (_body != null)
            {
                _body.localPosition = new Vector3(0f, bob, 0f);
                float squash = _walk * 0.05f + Mathf.Sin(Time.time * speed * 2f) * 0.02f * _walk;
                _body.localScale = new Vector3(1f + squash, 1f - squash, 1f + squash);
            }

            ApplyHugPose(_hug);

            // pop-in
            float grow = Mathf.Clamp01((Time.time - _spawnTime) / 0.28f);
            float ease = 1f - Mathf.Pow(1f - grow, 3f);
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, ease);
        }

        void ApplyHugPose(float t)
        {
            if (_shoulderL == null) return;

            float swing = Mathf.SmoothStep(0f, 1f, t);
            _shoulderL.localRotation = Quaternion.Euler(Mathf.Lerp(-6f, -88f, swing), 0f, Mathf.Lerp(18f, -22f, swing));
            _shoulderR.localRotation = Quaternion.Euler(Mathf.Lerp(-6f, -88f, swing), 0f, Mathf.Lerp(-18f, 22f, swing));

            if (_body != null)
            {
                _body.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -7f, swing), 0f, Mathf.Sin(Time.time * 3f) * 3f * swing);
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null && _nameTag != null)
            {
                _nameTag.transform.rotation = cam.transform.rotation;
                _nameTag.transform.localPosition = new Vector3(0f, 2.02f, 0f);
            }
        }

        void OnDestroy()
        {
            if (_bodyMat != null) Destroy(_bodyMat);
            if (_bellyMat != null) Destroy(_bellyMat);
            if (_limbMat != null) Destroy(_limbMat);
            if (_eyeMat != null) Destroy(_eyeMat);
        }
    }
}
