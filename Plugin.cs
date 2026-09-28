using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace UnlimitedHammer
{
    [BepInPlugin("DogEggz.unlimitedhammer", "Unlimited hammer", "1.0.3")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;

        internal static ConfigEntry<bool> ModEnabled { get; private set; }

        internal static bool IsModEnabled
        {
            get
            {
                return ModEnabled == null || ModEnabled.Value;
            }
        }

        private void Awake()
        {
            ModEnabled = this.Config.Bind(
                "Settings",
                "Mod enabled",
                true,
                "Toggle all Unlimited hammer changes on or off.");

            this.harmony = new Harmony("DogEggz.unlimitedhammer");
            this.harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        private void OnDestroy()
        {
            this.harmony?.UnpatchSelf();
        }
    }

    internal static class ExtraNailableItems
    {
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal)
        {
            "137 model ship junk (big)",
            "138 model ship junk (small)",
            "110 lantern A",
            "111 lantern E yellow",
            "112 lantern E red",
            "113 lantern E green",
            "114 lantern M",
            "133 lantern M big",
            "134 lantern E blu",
            "190 flower pot",
            "191 flower pot 1",
            "192 flower pot 2",
            "193 flower pot 3",
            "194 flower pot 4 (small)",
            "195 flower pot 5 (small)"
        };

        public static bool Contains(ShipItem item)
        {
            if (item == null)
            {
                return false;
            }

            return Names.Contains(NormalizeName(item.transform.name));
        }

        private static string NormalizeName(string itemName)
        {
            const string cloneSuffix = "(Clone)";

            if (string.IsNullOrEmpty(itemName))
            {
                return string.Empty;
            }

            itemName = itemName.Trim();
            while (itemName.EndsWith(cloneSuffix, StringComparison.Ordinal))
            {
                itemName = itemName.Substring(0, itemName.Length - cloneSuffix.Length).Trim();
            }

            return itemName;
        }
    }

    [HarmonyPatch(typeof(ShipItemHammer), nameof(ShipItemHammer.CanNail))]
    internal static class ShipItemHammerCanNailPatch
    {
        private static void Postfix(ShipItem item, ref bool __result)
        {
            if (!Plugin.IsModEnabled)
            {
                return;
            }

            if (__result || item == null || !item.sold)
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
            {
                return true;
            }

            if (item == null)
            {
                return false;
            }

            item.nailed = true;
            UISoundPlayer.instance.PlayUISound(UISounds.winchUnclick, 1f, 0.6f);
            Debug.Log("Nailed " + item.name);

            return false;
        }
    }

    [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowLookText))]
    internal static class LookUIHammerPercentPatch
    {
        private const float NailDurationSeconds = 2f;

        private static readonly FieldInfo CurrentlyNailedItemField =
            AccessTools.Field(typeof(ShipItemHammer), "currentlyNailedItem");

        private static readonly FieldInfo NailTimerField =
            AccessTools.Field(typeof(ShipItemHammer), "nailTimer");

        private static void Postfix(GoPointerButton button, GoPointer ___pointer, TextMesh ___hintText)
        {
            if (!Plugin.IsModEnabled)
            {
                return;
            }

            if (button == null || ___pointer == null || ___hintText == null)
            {
                return;
            }

            ShipItem lookedAtItem = button.GetComponent<ShipItem>();
            if (lookedAtItem == null || lookedAtItem.nailed)
            {
                return;
            }

            PickupableItem heldItem = ___pointer.GetHeldItem();
            ShipItemHammer hammer = heldItem == null ? null : heldItem.GetComponent<ShipItemHammer>();
            if (hammer == null || !hammer.sold)
            {
                return;
            }

            ShipItem currentlyNailedItem = CurrentlyNailedItemField?.GetValue(hammer) as ShipItem;
            if (currentlyNailedItem != lookedAtItem)
            {
                return;
            }

            object timerValue = NailTimerField?.GetValue(hammer);
            float nailTimer = timerValue is float value ? value : 0f;
            if (nailTimer <= 0f)
            {
                return;
            }

            int percent = Mathf.Clamp(Mathf.FloorToInt(nailTimer / NailDurationSeconds * 100f), 0, 100);
            string percentLine = string.Format("<color=white>{0}%</color>", percent);

            ___hintText.text = string.IsNullOrEmpty(___hintText.text)
                ? percentLine
                : ___hintText.text + "\n" + percentLine;
        }
    }

    // Vanilla only lets empty hands point at nailed crates, bottles and beds.
    // Lanterns are added so they can still be toggled and show their fuel percentage.
    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    internal static class GoPointerNailedLanternPatch
    {
        private static readonly MethodInfo TypeEquality =
            AccessTools.Method(typeof(Type), "op_Equality", new[] { typeof(Type), typeof(Type) });

        private static readonly MethodInfo IsBedOrLanternMethod =
            AccessTools.Method(typeof(GoPointerNailedLanternPatch), nameof(IsBedOrLantern));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            bool afterBedToken = false;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldtoken && codes[i].operand as Type == typeof(ShipItemBed))
                {
                    afterBedToken = true;
                }
                else if (afterBedToken && codes[i].Calls(TypeEquality))
                {
                    codes[i].operand = IsBedOrLanternMethod;
                    return codes;
                }
            }

            Debug.LogWarning("Unlimited hammer: nailed lantern patch target not found; locked lanterns stay non-interactable.");
            return codes;
        }

        // Replaces "itemType == typeof(ShipItemBed)" in the nailed-item branch.
        private static bool IsBedOrLantern(Type itemType, Type bedType)
        {
            if (itemType == bedType)
            {
                return true;
            }

            return Plugin.IsModEnabled && typeof(ShipItemLight).IsAssignableFrom(itemType);
        }
    }

    [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowLookText))]
    internal static class LookUINailedLanternPatch
    {
        private static void Postfix(
            GoPointerButton button,
            GoPointer ___pointer,
            TextMesh ___controlsText,
            Renderer ___mouseLIcon,
            TextMesh ___textLicon)
        {
            if (!Plugin.IsModEnabled || !Settings.controlsTextEnabled || button == null || ___pointer == null || ___pointer.GetHeldItem() != null)
            {
                return;
            }

            ShipItemLight lantern = button.GetComponent<ShipItemLight>();
            if (lantern == null || !lantern.sold || !lantern.nailed)
            {
                return;
            }

            if (___controlsText != null)
            {
                ___controlsText.text = "\nuse";
            }

            if (___mouseLIcon != null)
            {
                ___mouseLIcon.enabled = false;
            }

            if (___textLicon != null)
            {
                ___textLicon.text = "";
            }
        }
    }
}
