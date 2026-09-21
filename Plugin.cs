using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace UnlimitedHammer;

[BepInPlugin("DogEggz.unlimitedhammer", "Unlimited hammer", "1.0.1")]
public class Plugin : BaseUnityPlugin
{
    private Harmony harmony;

    internal static ConfigEntry<bool> ModEnabled { get; private set; }

    internal static bool IsModEnabled
    {
        get
        {
            if (ModEnabled != null)
                return ModEnabled.Value;

            return true;
        }
    }

    private void Awake()
    {
        ModEnabled = Config.Bind(
            "Settings",
            "Mod enabled",
            true,
            "Toggle all Unlimited hammer changes on or off.");

        harmony = new Harmony("DogEggz.unlimitedhammer");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }
}

[HarmonyPatch(typeof(ShipItemHammer), "CanNail")]
internal static class ShipItemHammerCanNailPatch
{
    private static void Postfix(ShipItem item, ref bool __result)
    {
        if (!Plugin.IsModEnabled)
            return;

        if (__result ||
            item == null ||
            !item.sold)
        {
            return;
        }

        __result = ExtraNailableItems.Contains(item);
    }
}

[HarmonyPatch(typeof(ShipItemHammer), "NailItem")]
internal static class ShipItemHammerNailItemPatch
{
    private static bool Prefix(ShipItem item)
    {
        if (!Plugin.IsModEnabled)
            return true;

        if (item == null)
            return false;

        item.nailed = true;
        UISoundPlayer.instance.PlayUISound(
            UISounds.winchUnclick,
            1f,
            0.6f);
        Debug.Log("Nailed " + item.name);

        return false;
    }
}

[HarmonyPatch(typeof(LookUI), "ShowLookText")]
internal static class LookUIHammerPercentPatch
{
    private const float NailDurationSeconds = 2f;

    private static readonly FieldInfo CurrentlyNailedItemField =
        AccessTools.Field(typeof(ShipItemHammer), "currentlyNailedItem");

    private static readonly FieldInfo NailTimerField =
        AccessTools.Field(typeof(ShipItemHammer), "nailTimer");

    private static void Postfix(
        GoPointerButton button,
        GoPointer ___pointer,
        TextMesh ___hintText)
    {
        if (!Plugin.IsModEnabled)
            return;

        if (button == null ||
            ___pointer == null ||
            ___hintText == null)
        {
            return;
        }

        ShipItem item = button.GetComponent<ShipItem>();

        if (item == null || item.nailed)
            return;

        PickupableItem heldItem = ___pointer.GetHeldItem();
        ShipItemHammer hammer = heldItem == null
            ? null
            : heldItem.GetComponent<ShipItemHammer>();

        if (hammer == null || !hammer.sold)
            return;

        if (CurrentlyNailedItemField?.GetValue(hammer) as ShipItem != item)
            return;

        object timerValue = NailTimerField?.GetValue(hammer);
        float nailTimer = timerValue is float value ? value : 0f;

        if (nailTimer <= 0f)
            return;

        int percentage = Mathf.Clamp(
            Mathf.FloorToInt(nailTimer / NailDurationSeconds * 100f),
            0,
            100);

        string percentageText = string.Format(
            "<color=white>{0}%</color>",
            percentage);

        ___hintText.text = string.IsNullOrEmpty(___hintText.text)
            ? percentageText
            : ___hintText.text + "\n" + percentageText;
    }
}
