using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

[BepInPlugin(
    "com.mistalcry.dynamitetimer",
    "Dynamite Timer",
    "1.0.0")]
public class DynamiteTimerPlugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;

        Harmony harmony =
            new Harmony("com.mistalcry.dynamitetimer");

        harmony.PatchAll();

        gameObject.AddComponent<WorldTimerRenderer>();

        Logger.LogInfo("Dynamite Timer Loaded");
    }
}
