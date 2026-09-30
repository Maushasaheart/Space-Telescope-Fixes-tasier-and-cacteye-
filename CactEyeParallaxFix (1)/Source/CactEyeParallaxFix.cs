using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyVersion("0.2.1.0")]
[assembly: AssemblyFileVersion("0.2.1.0")]
[assembly: AssemblyDescription("CactEye aware Parallax Scaled texture residency. No material swaps.")]

[KSPAddon(KSPAddon.Startup.Instantly, true)]
public sealed class CactEyeParallaxFix : MonoBehaviour
{
    const string Owner = "CactEyeParallaxFix.textureResidency.v2";
    const float GraceSeconds = 2.0f;
    sealed class Entry
    {
        internal object scaled;
        internal CelestialBody body;
        internal string name;
        internal float holdUntil = -1;
        internal float retryAfter;
        internal bool reportedTexture;
    }
    static readonly Dictionary<object, Entry> entries = new Dictionary<object, Entry>();
    static Harmony harmony;
    static FieldInfo bodyTable, componentBody, pendingUnload, planetName, scaledMaterial;
    static PropertyInfo loaded, loading;
    static MethodInfo load;
    static int renderDepth;
    static bool ready, announcedCamera, failed;
    static IDictionary cachedTable;
    static int cachedCount = -1;
    bool cameraSubscribed;
    bool sceneSubscribed;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        try
        {
            Type cameraType = AccessTools.TypeByName("CactEye2.CactEyeCamera");
            Type configType = AccessTools.TypeByName("Parallax.ConfigLoader");
            Type demandType = AccessTools.TypeByName("Parallax.Scaled_System.ScaledOnDemandComponent");
            if (cameraType == null || configType == null || demandType == null)
                throw new NotSupportedException("CactEye or Parallax Scaled runtime type missing.");
            MethodInfo render = null;
            foreach (MethodInfo m in cameraType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                ParameterInfo[] p = m.GetParameters();
                if (m.Name == "UpdateTexture" && p.Length == 3 && p[1].ParameterType == typeof(RenderTexture)
                    && p[2].ParameterType == typeof(Texture2D)) { render = m; break; }
            }
            MethodInfo visibility = AccessTools.Method(demandType, "UpdateVisibility");
            bodyTable = AccessTools.Field(configType, "parallaxScaledBodies");
            componentBody = AccessTools.Field(demandType, "scaledBody");
            pendingUnload = AccessTools.Field(demandType, "pendingUnload");
            if (render == null || visibility == null || bodyTable == null || componentBody == null || pendingUnload == null)
                throw new NotSupportedException("Required camera or texture loader hook missing.");
            Type scaledType = componentBody.FieldType;
            loaded = AccessTools.Property(scaledType, "Loaded");
            loading = AccessTools.Property(scaledType, "IsLoading");
            load = AccessTools.Method(scaledType, "Load", Type.EmptyTypes);
            planetName = AccessTools.Field(scaledType, "planetName");
            scaledMaterial = AccessTools.Field(scaledType, "scaledMaterial");
            if (loaded == null || loading == null || load == null || planetName == null || scaledMaterial == null)
                throw new NotSupportedException("Required Parallax Scaled body API missing.");

            harmony = new Harmony(Owner);
            harmony.Patch(render, prefix: new HarmonyMethod(typeof(CactEyeParallaxFix), "BeginRender"),
                finalizer: new HarmonyMethod(typeof(CactEyeParallaxFix), "EndRender"));
            harmony.Patch(visibility, prefix: new HarmonyMethod(typeof(CactEyeParallaxFix), "KeepVisibleTextures"));
            Camera.onPreCull += BeforeCamera;
            cameraSubscribed = true;
            GameEvents.onGameSceneLoadRequested.Add(SceneChanging);
            sceneSubscribed = true;
            ready = true;
            Debug.Log("[CactEyeParallaxFix v2.1] Ready; Scaled rendering stays enabled. CactEye="
                + cameraType.Assembly.GetName().Version + "; Parallax=" + configType.Assembly.GetName().Version);
        }
        catch (Exception e)
        {
            ready = false;
            Unsubscribe();
            if (harmony != null) harmony.UnpatchAll(Owner);
            Debug.LogError("[CactEyeParallaxFix v2.1] Inactive: " + e);
        }
    }

    static void BeginRender(out int __state)
    {
        __state = renderDepth;
        renderDepth++;
    }
    // Finalizer also runs when CactEye throws; preserve its original exception.
    static Exception EndRender(Exception __exception, int __state)
    {
        renderDepth = __state;
        return __exception;
    }
    static bool KeepVisibleTextures(object __instance)
    {
        if (!ready || failed || !HighLogic.LoadedSceneIsFlight) return true;
        object scaled = componentBody.GetValue(__instance);
        Entry entry;
        if (scaled == null || !entries.TryGetValue(scaled, out entry) ||
            Time.realtimeSinceStartup >= entry.holdUntil) return true;
        // The normal loader uses only the main camera. Leave its shadow Update
        // and any async load coroutine running, but cancel its pending unload.
        pendingUnload.SetValue(__instance, false);
        return false;
    }
    static void RefreshEntries()
    {
        IDictionary table = bodyTable.GetValue(null) as IDictionary;
        if (table == null) return;
        if (ReferenceEquals(table, cachedTable) && table.Count == cachedCount) return;
        entries.Clear();
        cachedTable = table;
        cachedCount = table.Count;
        foreach (DictionaryEntry pair in table)
        {
            object scaled = pair.Value;
            if (scaled == null) continue;
            string name = planetName.GetValue(scaled) as string;
            CelestialBody body = FlightGlobals.GetBodyByName(name);
            if (body == null || body.scaledBody == null) continue;
            entries[scaled] = new Entry { scaled = scaled, body = body, name = name };
        }
        if (table.Count == 0)
            Debug.LogWarning("[CactEyeParallaxFix v2.1] No Parallax Scaled bodies. Remove the old disabling CFG and restart KSP.");
    }
    static void BeforeCamera(Camera camera)
    {
        if (!ready || failed || renderDepth == 0 || !HighLogic.LoadedSceneIsFlight || camera == null) return;
        if (camera.name.IndexOf("CactEye", StringComparison.OrdinalIgnoreCase) < 0 ||
            camera.name.IndexOf("ScaledSpace", StringComparison.OrdinalIgnoreCase) < 0) return;
        try
        {
            if (!announcedCamera)
            {
                announcedCamera = true;
                Debug.Log("[CactEyeParallaxFix v2.1] Telescope render detected: " + camera.name
                    + "; renderingPath=" + camera.actualRenderingPath);
            }
            RefreshEntries();
            int height = camera.targetTexture != null ? camera.targetTexture.height : camera.pixelHeight;
            Vector3 origin = camera.transform.position;
            Vector3 right = camera.transform.right;
            Vector3 up = camera.transform.up;
            Vector3 forward = camera.transform.forward;
            foreach (Entry entry in entries.Values)
            {
                if (entry.body == null || entry.body.scaledBody == null) continue;
                Vector3 offset = entry.body.scaledBody.transform.position - origin;
                double radius = entry.body.Radius * ScaledSpace.InverseScaleFactor;
                if (!ScopeVisibility.NeedsTextures(Vector3.Dot(offset, right), Vector3.Dot(offset, up),
                    Vector3.Dot(offset, forward), radius, camera.fieldOfView, camera.aspect, height,
                    camera.nearClipPlane, camera.farClipPlane)) continue;

                float now = Time.realtimeSinceStartup;
                if (now < entry.retryAfter) continue;
                bool newlyHeld = now >= entry.holdUntil;
                entry.holdUntil = now + GraceSeconds;
                if (newlyHeld)
                    Debug.Log("[CactEyeParallaxFix v2.1] Holding telescope textures: " + entry.name);
                try
                {
                    bool isLoaded = (bool)loaded.GetValue(entry.scaled, null);
                    bool isLoading = (bool)loading.GetValue(entry.scaled, null);
                    if (!isLoaded && !isLoading)
                    {
                        // Use Parallax's own loader. Never interrupt an async load
                        // or manually assign textures, materials, shaders or cameras.
                        load.Invoke(entry.scaled, null);
                        entry.reportedTexture = false;
                    }
                    entry.holdUntil = Time.realtimeSinceStartup + GraceSeconds;
                    if ((bool)loaded.GetValue(entry.scaled, null) && !entry.reportedTexture)
                    {
                        Material material = scaledMaterial.GetValue(entry.scaled) as Material;
                        Texture color = material != null && material.HasProperty("_ColorMap")
                            ? material.GetTexture("_ColorMap") : null;
                        string details = color == null ? "no _ColorMap available" :
                            "_ColorMap=" + color.width + "x" + color.height;
                        Debug.Log("[CactEyeParallaxFix v2.1] Textures ready: " + entry.name + "; " + details);
                        entry.reportedTexture = true;
                    }
                }
                catch (Exception e)
                {
                    // Do not retain a failed body or continuously retry per frame.
                    entry.holdUntil = -1;
                    entry.retryAfter = Time.realtimeSinceStartup + 5;
                    Debug.LogError("[CactEyeParallaxFix v2.1] Could not load " + entry.name + ": " + e);
                }
            }
        }
        catch (Exception e)
        {
            failed = true;
            entries.Clear();
            Debug.LogError("[CactEyeParallaxFix v2.1] Paused after error; normal texture control restored: " + e);
        }
    }
    // KSP EventData inspects delegate.Target.GetType() during registration.
    // This handler must be an instance method: a static delegate has no target.
    void SceneChanging(GameScenes scene)
    {
        entries.Clear();
        cachedTable = null;
        cachedCount = -1;
        renderDepth = 0;
        announcedCamera = false;
    }
    void OnDestroy()
    {
        ready = false;
        Unsubscribe();
        entries.Clear();
        if (harmony != null) harmony.UnpatchAll(Owner);
    }
    void Unsubscribe()
    {
        if (cameraSubscribed)
        {
            Camera.onPreCull -= BeforeCamera;
            cameraSubscribed = false;
        }
        if (sceneSubscribed)
        {
            GameEvents.onGameSceneLoadRequested.Remove(SceneChanging);
            sceneSubscribed = false;
        }
    }
}
