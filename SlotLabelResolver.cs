using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public static class SlotLabelResolver
{
    private const string GroundLabel = "\u5730\u9762";
    private const string RightHandLabel = "\u53f3\u624b";
    private const string LeftHandLabel = "\u5de6\u624b";
    private const string MouthLabel = "\u53e3\u8154";
    private const string UpperBackLabel = "\u4e0a\u80cc\u90e8";
    private const string MiddleBackLabel = "\u4e2d\u80cc\u90e8";
    private const string LowerBackLabel = "\u4e0b\u80cc\u90e8";
    private const string SlotLabel = "\u69fd";

    private static readonly HashSet<int> loggedBodies =
        new HashSet<int>();

    public static string GetItemLocationLabel(Item item)
    {
        InventorySlot slot =
            GetInventorySlot(item);

        if (slot == null)
            return GroundLabel;

        if (slot.isHand)
        {
            return slot.slot == 0
                ? RightHandLabel
                : LeftHandLabel;
        }

        if (slot.slot == 2 ||
            (slot.limb != null && slot.limb.isHead))
        {
            return MouthLabel;
        }

        if (slot.slot == 3)
            return UpperBackLabel;

        if (slot.slot == 4)
            return MiddleBackLabel;

        if (slot.slot == 5)
            return LowerBackLabel;

        if (slot.limb != null && slot.limb.isAbdomen)
            return LowerBackLabel;

        if (slot.limb != null)
            return UpperBackLabel;

        return SlotLabel + slot.slot;
    }

    public static void LogItemSlot(Item item, Body fallbackBody, string source)
    {
        if (DynamiteTimerPlugin.Log == null)
            return;

        InventorySlot slot =
            GetInventorySlot(item);

        Body body =
            slot != null && slot.body != null
                ? slot.body
                : fallbackBody;

        if (slot == null)
        {
            DynamiteTimerPlugin.Log.LogInfo(
                "Dynamite slot from " + source +
                ": item is not parented to an InventorySlot");
        }
        else
        {
            DynamiteTimerPlugin.Log.LogInfo(
                "Dynamite slot from " + source +
                ": label=" + GetItemLocationLabel(item) +
                ", slot=" + slot.slot +
                ", slotObject=" + slot.name +
                ", isHand=" + slot.isHand +
                ", isMainHand=" + slot.isMainHand +
                ", handSlot=" + GetHandSlot(slot.body) +
                ", spriteSortOrder=" + slot.spriteSortOrder +
                ", dropWhenUnconscious=" + slot.dropWhenUnconscious +
                ", limb=" + GetLimbInfo(slot.limb));
        }

        LogBodySlotsOnce(body);
    }

    private static InventorySlot GetInventorySlot(Item item)
    {
        if (item == null)
            return null;

        return item.GetComponentInParent<InventorySlot>();
    }

    private static int GetHandSlot(Body body)
    {
        if (body == null)
            return -1;

        return Traverse.Create(body)
            .Field("handSlot")
            .GetValue<int>();
    }

    private static void LogBodySlotsOnce(Body body)
    {
        if (body == null ||
            DynamiteTimerPlugin.Log == null)
        {
            return;
        }

        int bodyId =
            body.GetInstanceID();

        if (loggedBodies.Contains(bodyId))
            return;

        loggedBodies.Add(bodyId);

        InventorySlot[] slots =
            Traverse.Create(body)
                .Field("slots")
                .GetValue<InventorySlot[]>();

        if (slots == null)
            return;

        DynamiteTimerPlugin.Log.LogInfo(
            "Dynamite Timer slot dump: handSlot=" + GetHandSlot(body) +
            ", slotCount=" + slots.Length);

        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlot slot =
                slots[i];

            if (slot == null)
            {
                DynamiteTimerPlugin.Log.LogInfo(
                    "Slot " + i + ": null");
                continue;
            }

            DynamiteTimerPlugin.Log.LogInfo(
                "Slot " + i +
                ": componentSlot=" + slot.slot +
                ", object=" + slot.name +
                ", isHand=" + slot.isHand +
                ", isMainHand=" + slot.isMainHand +
                ", spriteSortOrder=" + slot.spriteSortOrder +
                ", dropWhenUnconscious=" + slot.dropWhenUnconscious +
                ", limb=" + GetLimbInfo(slot.limb));
        }
    }

    private static string GetLimbInfo(Limb limb)
    {
        if (limb == null)
            return "null";

        return limb.name +
            "/" + limb.fullName +
            "/" + limb.shortName +
            ", isHead=" + limb.isHead +
            ", isAbdomen=" + limb.isAbdomen +
            ", isArm=" + limb.isArm;
    }
}
