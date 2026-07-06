using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

public static class DynamiteTimerStarter
{
    private const string DynamiteId = "dynamite";
    private const string ExplodeMethodName = "DynamiteExplode";
    private const float DefaultFuseSeconds = 5f;

    public static void TryStartFromUseItem(
        Item item,
        Body body,
        string source)
    {
        if (item == null ||
            item.id != DynamiteId ||
            IsAlreadyLit(item))
        {
            return;
        }

        StartTimer(item, body, source, DefaultFuseSeconds);
    }

    public static void TryStartFromScheduledInvoke(
        MonoBehaviour behaviour,
        string methodName,
        float fuseSeconds)
    {
        Item item;

        if (!TryGetInvokedDynamite(behaviour, methodName, out item))
            return;

        StartTimer(item, null, "ScheduledInvoke", fuseSeconds);
    }

    private static bool IsAlreadyLit(Item item)
    {
        CustomItemBehaviour behaviour =
            item.GetComponent<CustomItemBehaviour>();

        return behaviour != null &&
            behaviour.data != null &&
            behaviour.data.Length != 0 &&
            behaviour.data[0] is bool &&
            (bool)behaviour.data[0];
    }

    private static bool TryGetInvokedDynamite(
        MonoBehaviour behaviour,
        string methodName,
        out Item item)
    {
        item = null;

        if (behaviour == null ||
            methodName != ExplodeMethodName)
        {
            return false;
        }

        return TryGetDynamiteBehaviour(behaviour, out item);
    }

    private static bool TryGetDynamiteBehaviour(
        MonoBehaviour behaviour,
        out Item item)
    {
        item = null;

        if (behaviour == null)
            return false;

        CustomItemBehaviour customBehaviour =
            behaviour as CustomItemBehaviour;

        if (customBehaviour == null)
            return false;

        item =
            customBehaviour.GetComponent<Item>();

        return item != null &&
            item.id == DynamiteId;
    }

    private static void StartTimer(
        Item item,
        Body body,
        string source,
        float fuseSeconds)
    {
        if (item == null)
            return;

        if (item.id != DynamiteId)
            return;

        fuseSeconds =
            SanitizeFuseSeconds(fuseSeconds);

        DynamiteTimer timer =
            item.gameObject.GetComponent<DynamiteTimer>();

        if (timer == null)
        {
            timer =
                item.gameObject.AddComponent<DynamiteTimer>();
        }

        timer.EndTime =
            Time.time + fuseSeconds;

        if (DynamiteTimerPlugin.Log != null)
        {
            DynamiteTimerPlugin.Log.LogInfo(
                "Dynamite timer started from " + source +
                " with fuse " + fuseSeconds.ToString("0.###") + "s");
        }

        SlotLabelResolver.LogItemSlot(item, body, source);
    }

    private static float SanitizeFuseSeconds(float fuseSeconds)
    {
        if (float.IsNaN(fuseSeconds) ||
            float.IsInfinity(fuseSeconds) ||
            fuseSeconds < 0f)
        {
            return 0f;
        }

        return fuseSeconds;
    }
}

[HarmonyPatch(typeof(Body), "UseItem")]
public class BodyUseItemPatch
{
    static void Prefix(Body __instance, Item item)
    {
        DynamiteTimerStarter.TryStartFromUseItem(
            item,
            __instance,
            "UseItem");
    }
}

[HarmonyPatch(typeof(Body), "UseItemInHand")]
public class BodyUseItemInHandPatch
{
    static void Prefix(Body __instance)
    {
        int handSlot =
            Traverse.Create(__instance)
                .Field("handSlot")
                .GetValue<int>();

        if (!__instance.conscious)
            return;

        if (!__instance.HoldingItem(handSlot))
            return;

        Item item =
            __instance.GetItem(handSlot);

        if (item == null ||
            !item.Stats.usable ||
            !item.Stats.usableWithLMB)
        {
            return;
        }

        DynamiteTimerStarter.TryStartFromUseItem(
            item,
            __instance,
            "UseItemInHand");
    }
}

[HarmonyPatch]
public class DynamiteUseActionInvokePatch
{
    private const string ClosureTypeName = "<>c";
    private const string UseActionMethodName = "<SetupItems>b__40_210";
    private const string ExplodeMethodName = "DynamiteExplode";

    private static MethodBase targetMethod;

    static bool Prepare()
    {
        targetMethod =
            FindTargetMethod();

        if (targetMethod == null &&
            DynamiteTimerPlugin.Log != null)
        {
            DynamiteTimerPlugin.Log.LogWarning(
                "Dynamite useAction patch target was not found; " +
                "falling back to the default 5s timer.");
        }

        return targetMethod != null;
    }

    static MethodBase TargetMethod()
    {
        return targetMethod ??
            FindTargetMethod();
    }

    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator)
    {
        List<CodeInstruction> codes =
            new List<CodeInstruction>(instructions);

        LocalBuilder behaviourLocal =
            generator.DeclareLocal(typeof(MonoBehaviour));

        LocalBuilder methodNameLocal =
            generator.DeclareLocal(typeof(string));

        LocalBuilder fuseSecondsLocal =
            generator.DeclareLocal(typeof(float));

        MethodInfo timerMethod =
            AccessTools.Method(
                typeof(DynamiteTimerStarter),
                "TryStartFromScheduledInvoke");

        bool patched = false;

        for (int i = 0; i < codes.Count; i++)
        {
            if (!patched &&
                IsInvokeCall(codes[i]) &&
                HasDynamiteExplodeArgument(codes, i))
            {
                List<Label> labels =
                    codes[i].labels;

                codes[i].labels =
                    new List<Label>();

                yield return WithLabels(
                    new CodeInstruction(OpCodes.Stloc, fuseSecondsLocal),
                    labels);

                yield return new CodeInstruction(
                    OpCodes.Stloc,
                    methodNameLocal);

                yield return new CodeInstruction(
                    OpCodes.Stloc,
                    behaviourLocal);

                yield return new CodeInstruction(
                    OpCodes.Ldloc,
                    behaviourLocal);

                yield return new CodeInstruction(
                    OpCodes.Ldloc,
                    methodNameLocal);

                yield return new CodeInstruction(
                    OpCodes.Ldloc,
                    fuseSecondsLocal);

                yield return new CodeInstruction(
                    OpCodes.Call,
                    timerMethod);

                yield return new CodeInstruction(
                    OpCodes.Ldloc,
                    behaviourLocal);

                yield return new CodeInstruction(
                    OpCodes.Ldloc,
                    methodNameLocal);

                yield return new CodeInstruction(
                    OpCodes.Ldloc,
                    fuseSecondsLocal);

                patched = true;
            }

            yield return codes[i];
        }

        if (!patched &&
            DynamiteTimerPlugin.Log != null)
        {
            DynamiteTimerPlugin.Log.LogWarning(
                "Dynamite useAction Invoke call was not found; " +
                "falling back to the default 5s timer.");
        }
    }

    private static MethodBase FindTargetMethod()
    {
        Type closureType =
            AccessTools.Inner(typeof(Item), ClosureTypeName);

        if (closureType == null)
            return null;

        return AccessTools.Method(
            closureType,
            UseActionMethodName);
    }

    private static bool IsInvokeCall(CodeInstruction code)
    {
        MethodInfo method =
            code.operand as MethodInfo;

        return code.opcode == OpCodes.Callvirt &&
            method != null &&
            method.Name == "Invoke" &&
            method.DeclaringType == typeof(MonoBehaviour);
    }

    private static bool HasDynamiteExplodeArgument(
        List<CodeInstruction> codes,
        int index)
    {
        if (index < 2)
            return false;

        CodeInstruction methodNameInstruction =
            codes[index - 2];

        return methodNameInstruction.opcode == OpCodes.Ldstr &&
            (string)methodNameInstruction.operand == ExplodeMethodName;
    }

    private static CodeInstruction WithLabels(
        CodeInstruction instruction,
        List<Label> labels)
    {
        instruction.labels.AddRange(labels);

        return instruction;
    }
}
