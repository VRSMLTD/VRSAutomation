using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

public static class Sounds
{
    private const string SnapFile = "Pipe_Snap_Point.ogg";
    private const string UndoFile = "Pipe_Snap_Point_Undo.ogg";
    private const string InputFile = "Pipe_Input_Connection.ogg";
    private const string OutputFile = "Pipe_Output_Connection.ogg";
    private const string DisconnectFile = "Disconnect_Pipe_Socket.ogg";
    private const string PlaceFile = "Conveyor_Combiner_Splitter_Placement_Sound.ogg";

    private const float SnapVolume = 0.30f;
    private const float UndoVolume = 0.30f;
    private const float InputVolume = 0.19f;
    private const float OutputVolume = 0.22f;
    private const float DisconnectVolume = 0.25f;
    private const float PlaceVolume = 0.45f;

    private const float PitchMin = 0.92f;
    private const float PitchMax = 1.08f;
    private const float VolumeVariance = 0.05f;

    private const float MinDistance = 1f;
    private const float MaxDistance = 5f;

    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("VRSSounds");
    private static readonly Dictionary<string, object> Clips = new Dictionary<string, object>();
    private static readonly System.Random Rng = new System.Random();

    private static Type audioSourceType;
    private static Type audioTypeEnum;
    private static Type requestType;
    private static Type handlerType;
    private static GameObject host;
    private static bool started;

    private static Type Find(string fullName, string assembly)
    {
        return Type.GetType(fullName + ", " + assembly, false) ?? HarmonyLib.AccessTools.TypeByName(fullName);
    }

    public static void Start()
    {
        if (started && host != null) return;
        started = true;

        audioSourceType = Find("UnityEngine.AudioSource", "UnityEngine.AudioModule");
        audioTypeEnum = Find("UnityEngine.AudioType", "UnityEngine.AudioModule");
        requestType = Find("UnityEngine.Networking.UnityWebRequestMultimedia", "UnityEngine.UnityWebRequestAudioModule");
        handlerType = Find("UnityEngine.Networking.DownloadHandlerAudioClip", "UnityEngine.UnityWebRequestAudioModule");

        if (audioSourceType == null || audioTypeEnum == null || requestType == null || handlerType == null)
        {
            Log.LogWarning("Audio classes not found, sounds disabled");
            return;
        }

        host = new GameObject("VRSAutomation_Sounds");
        UnityEngine.Object.DontDestroyOnLoad(host);
        SoundHost h = host.AddComponent<SoundHost>();

        string dir = VRSAutomation.Plugin.AssetDirectory();
        Ensure(h, dir, SnapFile);
        Ensure(h, dir, UndoFile);
        Ensure(h, dir, InputFile);
        Ensure(h, dir, OutputFile);
        Ensure(h, dir, DisconnectFile);
        Ensure(h, dir, PlaceFile);
    }

    private static void Ensure(SoundHost h, string dir, string file)
    {
        if (Clips.ContainsKey(file)) return;
        string path = VRSAutomation.Assets.FilePath(file);
        if (path == null || !File.Exists(path))
        {
            Log.LogWarning("Sound file missing: " + file);
            return;
        }
        h.StartCoroutine(Load(file, path));
    }

    private static IEnumerator Load(string file, string path)
    {
        string uri = new Uri(path).AbsoluteUri;
        object oggType = Enum.Parse(audioTypeEnum, "OGGVORBIS");
        MethodInfo get = requestType.GetMethod("GetAudioClip", new Type[] { typeof(string), audioTypeEnum });
        if (get == null)
        {
            Log.LogWarning("GetAudioClip not found");
            yield break;
        }
        object request = get.Invoke(null, new object[] { uri, oggType });
        MethodInfo send = request.GetType().GetMethod("SendWebRequest", Type.EmptyTypes);
        object op = send.Invoke(request, null);
        PropertyInfo isDone = op.GetType().GetProperty("isDone");
        while (!(bool)isDone.GetValue(op, null)) yield return null;

        MethodInfo content = handlerType.GetMethod("GetContent", new Type[] { request.GetType() });
        object clip = content != null ? content.Invoke(null, new object[] { request }) : null;
        if (clip == null)
        {
            Log.LogWarning("Failed to load " + file);
            yield break;
        }
        Clips[file] = clip;
        Log.LogInfo("Loaded sound " + file);
        IDisposable d = request as IDisposable;
        if (d != null) d.Dispose();
    }

    private static void Set(object target, string name, object value)
    {
        PropertyInfo p = target.GetType().GetProperty(name);
        if (p == null || !p.CanWrite) return;
        try
        {
            if (value is int && p.PropertyType.IsEnum) value = Enum.ToObject(p.PropertyType, (int)value);
            p.SetValue(target, value, null);
        }
        catch { }
    }

    private static float Between(float min, float max)
    {
        return min + (float)Rng.NextDouble() * (max - min);
    }

    private static void Play(string file, Vector3 position, float volume, bool vary)
    {
        if (audioSourceType == null) return;
        object clip;
        if (!Clips.TryGetValue(file, out clip) || clip == null) return;

        float pitch = 1f;
        if (vary)
        {
            pitch = Between(PitchMin, PitchMax);
            volume *= Between(1f - VolumeVariance, 1f + VolumeVariance);
        }

        GameObject go = new GameObject("VRS_Sound");
        go.transform.position = position;
        Component src = go.AddComponent(audioSourceType);
        Set(src, "clip", clip);
        Set(src, "volume", volume);
        Set(src, "pitch", pitch);
        Set(src, "spatialBlend", 1f);
        Set(src, "rolloffMode", 1);
        Set(src, "minDistance", MinDistance);
        Set(src, "maxDistance", MaxDistance);
        Set(src, "dopplerLevel", 0f);
        Set(src, "reverbZoneMix", 0f);
        Set(src, "playOnAwake", false);

        MethodInfo play = audioSourceType.GetMethod("Play", Type.EmptyTypes);
        if (play != null) play.Invoke(src, null);

        float length = 1f;
        PropertyInfo lp = clip.GetType().GetProperty("length");
        if (lp != null) length = (float)lp.GetValue(clip, null);
        UnityEngine.Object.Destroy(go, length / Mathf.Max(0.1f, pitch) + 0.5f);
    }

    public static void Snap(Vector3 position) { Play(SnapFile, position, SnapVolume, true); }
    public static void Undo(Vector3 position) { Play(UndoFile, position, UndoVolume, true); }
    public static void Input(Vector3 position) { Play(InputFile, position, InputVolume, false); }
    public static void Output(Vector3 position) { Play(OutputFile, position, OutputVolume, false); }
    public static void Disconnect(Vector3 position) { Play(DisconnectFile, position, DisconnectVolume, false); }
    public static void Place(Vector3 position) { Play(PlaceFile, position, PlaceVolume, true); }

    private class SoundHost : MonoBehaviour
    {
    }
}

[HarmonyPatch(typeof(Player), "Awake")]
internal static class SoundAwakePatch
{
    internal static void Postfix()
    {
        Sounds.Start();
    }
}