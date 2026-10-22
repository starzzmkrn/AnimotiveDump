using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;

namespace AnimotiveDump
{
    [BepInPlugin("local.animotive.dump", "Animotive Dump", "2.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        int stage;

        static readonly string[] Keywords =
        {
            "VrIK", "IKSolverVR", "PlayerTrackingEmbodied", "PlayerTrackingVrIk", "PlayerTrackingWith"
        };

        void Update()
        {
            float t = Time.realtimeSinceStartup;
            if (stage == 0 && t > 25f) { stage = 1; Run("animotive_dump2.txt", true); }
            else if (stage == 1 && t > 70f) { stage = 2; Run("animotive_devices.txt", false); }
        }

        void Run(string file, bool types)
        {
            try
            {
                var sb = new StringBuilder();
                if (types) DumpTypes(sb);
                DumpDevices(sb);
                string path = Path.Combine(Paths.BepInExRootPath, file);
                File.WriteAllText(path, sb.ToString());
                Logger.LogInfo("Wrote " + path + " (" + sb.Length + " chars)");
            }
            catch (Exception e) { Logger.LogError(e); }
        }

        static Type[] SafeTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
        }

        void DumpTypes(StringBuilder sb)
        {
            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n = asm.GetName().Name;
                if (n.StartsWith("System") || n.StartsWith("mscorlib") || n.StartsWith("Mono.")) continue;
                foreach (var t in SafeTypes(asm))
                {
                    if (t.FullName == null) continue;
                    if (!Keywords.Any(k => t.FullName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    sb.AppendLine("=== " + t.FullName + " : " + (t.BaseType != null ? t.BaseType.FullName : "") + "  [" + n + "]");
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
        }

        static string Prop(object o, string name)
        {
            try
            {
                var p = o.GetType().GetProperty(name);
                if (p == null) return "";
                var v = p.GetValue(o, null);
                return v == null ? "" : v.ToString();
            }
            catch { return "?"; }
        }

        void DumpDevices(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("##### INPUT DEVICES at t=" + Time.realtimeSinceStartup.ToString("F0") + "s");
            var isType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
            if (isType == null) { sb.AppendLine("InputSystem type not found"); return; }
            var devs = isType.GetProperty("devices", BindingFlags.Public | BindingFlags.Static).GetValue(null, null) as IEnumerable;
            foreach (var d in devs)
            {
                var dt = d.GetType();
                var usagesObj = dt.GetProperty("usages").GetValue(d, null) as IEnumerable;
                string usages = string.Join(",", usagesObj.Cast<object>().Select(x => x.ToString()).ToArray());
                string line = "DEV " + dt.FullName + " layout=" + Prop(d, "layout") + " name=" + Prop(d, "name") + " usages=[" + usages + "]";
                try
                {
                    var posProp = dt.GetProperty("devicePosition");
                    if (posProp != null)
                    {
                        var ctrl = posProp.GetValue(d, null);
                        var rv = ctrl.GetType().GetMethod("ReadValue", Type.EmptyTypes);
                        line += " pos=" + rv.Invoke(ctrl, null);
                    }
                }
                catch { }
                sb.AppendLine(line);
            }
        }
    }
}
