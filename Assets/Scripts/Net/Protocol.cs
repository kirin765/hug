using System;
using UnityEngine;

namespace Hug
{
    /// <summary>
    /// Wire format shared with server/protocol.js. Field names are deliberately
    /// short — snapshots are sent 20 times per second.
    /// </summary>
    public static class Proto
    {
        public const int Version = 1;

        // Gameplay tuning (mirrored on the server).
        public const float ArenaRadius = 30f;
        public const float HugRange = 3.4f;
        public const float Speed = 7f;
        public const float DashSpeed = 21f;
        public const float DashDuration = 0.16f;
        public const float DashCooldown = 2.2f;
        public const int TickHz = 20;

        public const string FallbackUrl = "ws://localhost:8080/ws";
    }

    [Serializable]
    public class JoinOut
    {
        public string t = "join";
        public string n;
        // Optional spawn point (used by ?x=&z= links and by the browser tests).
        public bool s;
        public float x;
        public float z;
    }

    [Serializable]
    public class PosOut
    {
        public string t = "pos";
        public float x;
        public float z;
        public float r;
    }

    [Serializable]
    public class HugOut
    {
        public string t = "hug";
        public int to;
    }

    [Serializable]
    public class PingOut
    {
        public string t = "ping";
        public long ts;
    }

    [Serializable]
    public class WelcomeMsg
    {
        public string t;
        public int v;
        public int i;
        public string n;
        public int c;
        public float a;
        public float range;
        public float speed;
        public int rate;
    }

    [Serializable]
    public class PlayerState
    {
        public int i;
        public string n;
        public int c;
        public float x;
        public float z;
        public float r;
        public int s;
        public int h;
    }

    [Serializable]
    public class SnapMsg
    {
        public string t;
        public PlayerState[] p;
    }

    [Serializable]
    public class HugMsg
    {
        public string t;
        public int f;
        public int o;
        public float x;
        public float z;
        public int fs;
        public int os;
        public int fd; // score change applied to the hugger
        public int od; // score change applied to the hugged player
    }

    [Serializable]
    public class JoinedMsg
    {
        public string t;
        public int i;
        public string n;
        public int c;
    }

    [Serializable]
    public class LeftMsg
    {
        public string t;
        public int i;
        public string n;
    }

    [Serializable]
    public class NopeMsg
    {
        public string t;
        public string m;
    }

    [Serializable]
    public class PongMsg
    {
        public string t;
        public long ts;
    }

    /// <summary>Player colours, index-aligned with COLORS in server/protocol.js.</summary>
    public static class Palette
    {
        public static readonly Color[] Colors =
        {
            Hex("ff6b9d"), // pink
            Hex("ffd166"), // yellow
            Hex("6ee7ff"), // cyan
            Hex("a78bfa"), // purple
            Hex("7cf29c"), // mint
            Hex("ff9f6e"), // orange
            Hex("8ab4ff"), // blue
            Hex("ff8fab"), // rose
            Hex("c4f24d"), // lime
            Hex("f9a8d4"), // blush
        };

        public static Color Get(int index)
        {
            if (Colors.Length == 0) return Color.white;
            return Colors[((index % Colors.Length) + Colors.Length) % Colors.Length];
        }

        static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.white;
        }

        public static Color Darken(Color c, float amount = 0.35f)
        {
            return new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);
        }

        public static Color Lighten(Color c, float amount = 0.35f)
        {
            return new Color(
                Mathf.Lerp(c.r, 1f, amount),
                Mathf.Lerp(c.g, 1f, amount),
                Mathf.Lerp(c.b, 1f, amount),
                c.a);
        }
    }
}
