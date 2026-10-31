using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hug.EditorTools
{
    /// <summary>Builds the WebGL player into ./web (served by server/server.js).</summary>
    public static class BuildWebGL
    {
        [MenuItem("HUG/Build WebGL")]
        public static void Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(projectRoot, "web");
            Directory.CreateDirectory(output);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"[HUG] WebGL build {summary.result} — {summary.totalSize / (1024f * 1024f):0.0} MB in {summary.totalTime}");

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[HUG] Build failed.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
