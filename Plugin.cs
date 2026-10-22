using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;

namespace AnimotiveDump
{
    [BepInPlugin("local.animotive.dump", "Animotive Dump", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        bool done;
        const float DelaySeconds = 25f;

        static readonly string[] Keywords =
        {
            "SteamVrTracker", "TrackersStatus", "VrIkWithSteamVr", "HTCViveTracker",
            "FootAndAnkle", "TrackerSmoother", "SteamVRCalibration", "TrackedPoseDriver"
        };

        void Update()
        {
            if (done || Time.realtimeSinceStartup < DelaySeconds) return;
            done = true;
            try { Dump(); } catch (Exception e) { Logger.LogError(e); }
        }

        void Dump()
        {
            var sb = new StringBuilder();
            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith("Animotive") && !asm.GetName().Name.Contains("Unity.InputSystem")
                    && !asm.GetName().Name.Contains("OpenXR")) continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

                foreach (var t in types)
                {
                    bool match = Keywords.Any(k => t.FullName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!match) continue;

                    sb.AppendLine("=== " + t.FullName + " : " + (t.BaseType != null ? t.BaseType.FullName : "") + "  [" + asm.GetName().Name + "]");
                    foreach (var f in t.GetFields(flags))
                        sb.AppendLine("  F " + f.FieldType.Name + " " + f.Name + (f.IsStatic ? " (static)" : ""));
                    foreach (var p in t.GetProperties(flags))
                        sb.AppendLine("  P " + p.PropertyType.Name + " " + p.Name);
                    foreach (var m in t.GetMethods(flags))
                    {
                        if (m.IsSpecialName) continue;
                        sb.AppendLine("  M " + m.ReturnType.Name + " " + m.Name + "(" +
                            string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name).ToArray()) + ")");
                    }
                }
            }

            // enum values containing Chest/Waist (body parts etc.)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith("Animotive")) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types.Where(x => x.IsEnum))
                {
                    var names = Enum.GetNames(t);
                    if (names.Any(n => n.IndexOf("Chest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       n.IndexOf("Waist", StringComparison.OrdinalIgnoreCase) >= 0))
                        sb.AppendLine("ENUM " + t.FullName + ": " + string.Join(", ", names));
                }
            }

            string path = Path.Combine(Paths.BepInExRootPath, "animotive_dump.txt");
            File.WriteAllText(path, sb.ToString());
            Logger.LogInfo("Dump written to " + path + " (" + sb.Length + " chars)");
        }
    }
}
