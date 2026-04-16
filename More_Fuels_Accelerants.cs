#nullable disable
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using Il2CppTLD.IntBackedUnit;
using ModSettings;
using System.Reflection;
using System.Collections.Generic;

[assembly: MelonInfo(typeof(AnimalFatFuel.AnimalFatFuelMain), "MORE_fuel_accelerant", "1.4.0", "hzb1130")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace AnimalFatFuel
{
    public class AnimalFatFuelMain : MelonMod
    {
        public static List<int> tempTinderInstances = new List<int>();
        public static List<int> tempCharcoalInstances = new List<int>();
        public override void OnInitializeMelon()
        {
            Settings.OnLoad();
        }
    }

    internal class AnimalFatFuelSettings : JsonModSettings
    {
        [Section("Tinder Fuel / 火引燃料设置")]
        [Name("Use Tinder as Fuel / 火引可以作为燃料")]
        public bool tinderAsFuel = false;

        [Name("Tinder Burn Time Multiplier / 火引燃烧时间倍率")]
        [Description("Based on original value / 基于原版燃烧时间")]
        [Slider(1, 10)]
        public int tinderBurnMultiplier = 1;

        [Section("Animal Fat Fuel / 动物脂肪燃料设置")]
        [Name("Enable Animal Fat as Fuel / 启用动物脂肪作为燃料")]
        public bool animalFatAsFuel = false;

        [Name("Burn Time Per KG (Min) / 燃烧时间(分钟/kg)")]
        [Slider(10, 120)]
        public int burnMinutesPerKg = 60;

        [Name("Fire Heat Increase / 火堆温度增加")]
        [Slider(1, 20)]
        public int heatIncrease = 5;

        [Section("Charcoal Fuel / 木炭燃料")]
        [Name("Enable Charcoal as Fuel / 启用木炭作为燃料")]
        public bool charcoalAsFuel = false;

        [Name("Burn Time (Minutes) / 燃烧时间(分钟)")]
        [Slider(5, 60)]
        public int charcoalBurnMinutes = 20;

        [Name("Heat Increase / 温度增加")]
        [Slider(1, 20)]
        public int charcoalHeatIncrease = 3;

        [Section("Accelerant Probability / 助燃剂概率消耗")]
        [Name("Enable Probability Consumption / 启用概率消耗")]
        public bool enableAccelerantProbability = false;

        [Name("Consume Chance 1/X / 消耗概率 1/X")]
        [Slider(1, 10)]
        public int accelerantConsumeChance = 2;


        protected override void OnChange(FieldInfo field, object oldValue, object newValue)
        {
            base.OnChange(field, oldValue, newValue);

            if (field.Name == nameof(tinderAsFuel))
                SetFieldVisible(nameof(tinderBurnMultiplier), (bool)newValue);

            if (field.Name == nameof(animalFatAsFuel))
            {
                bool vis = (bool)newValue;
                SetFieldVisible(nameof(burnMinutesPerKg), vis);
                SetFieldVisible(nameof(heatIncrease), vis);
            }
            if (field.Name == nameof(charcoalAsFuel))
            {
                bool vis = (bool)newValue;
                SetFieldVisible(nameof(charcoalBurnMinutes), vis);
                SetFieldVisible(nameof(charcoalHeatIncrease), vis);
            }
            if (field.Name == nameof(enableAccelerantProbability))
                SetFieldVisible(nameof(accelerantConsumeChance), (bool)newValue);
        }
    }

    internal static class Settings
    {
        public static AnimalFatFuelSettings options;
        public static void OnLoad()
        {
            options = new AnimalFatFuelSettings();
            options.AddToModSettings("More fuel ＆ accelerant");

            options.SetFieldVisible(nameof(options.tinderBurnMultiplier), options.tinderAsFuel);
            options.SetFieldVisible(nameof(options.burnMinutesPerKg), options.animalFatAsFuel);
            options.SetFieldVisible(nameof(options.heatIncrease), options.animalFatAsFuel);
            options.SetFieldVisible(nameof(options.charcoalBurnMinutes), options.charcoalAsFuel);
            options.SetFieldVisible(nameof(options.charcoalHeatIncrease), options.charcoalAsFuel);
            options.SetFieldVisible(nameof(options.accelerantConsumeChance), options.enableAccelerantProbability);
        }
    }

    // ==============================================
    // 助燃剂概率消耗
    // ==============================================
    [HarmonyPatch(typeof(Panel_FireStart), nameof(Panel_FireStart.OnStartFire))]
    internal static class Patch_Accelerant_ProbabilityConsume
    {
        private static void Prefix(Panel_FireStart __instance)
        {
            try
            {
                if (!Settings.options.enableAccelerantProbability) return;

                int index = __instance.m_SelectedAccelerantIndex;
                if (index < 0 || __instance.m_AccelerantList == null || index >= __instance.m_AccelerantList.Count) return;

                GearItem gear = __instance.m_AccelerantList[index];
                if (gear == null || gear.name != "GEAR_Accelerant") return;

                FireStarterItem fs = gear.m_FireStarterItem;
                if (fs == null) return;

                int chance = Settings.options.accelerantConsumeChance;
                int roll = UnityEngine.Random.Range(1, chance + 1);

                fs.m_ConsumeOnUse = (roll == 1);
            }
            catch { }
        }
    }


    // ==============================================
    [HarmonyPatch(typeof(GearItem), nameof(GearItem.Awake))]
    internal static class PatchGearItemAwake
    {
        internal static void Postfix(GearItem __instance)
        {
            if (__instance == null) return;

            if (Settings.options.animalFatAsFuel && __instance.name.Contains("GEAR_AnimalFat"))
            {
                if (__instance.m_FuelSourceItem != null) return;
                var fs = __instance.gameObject.AddComponent<FuelSourceItem>();
                FuelCalculator.Apply(__instance, fs);
                __instance.m_FuelSourceItem = fs;
                return;
            }
            var fuel = __instance.m_FuelSourceItem;
            if (fuel != null && fuel.m_IsTinder)
            {
                fuel.m_BurnDurationHours *= Settings.options.tinderBurnMultiplier;
            }
        }
    }

    [HarmonyPatch(typeof(Panel_FeedFire), nameof(Panel_FeedFire.Enable), new[] { typeof(bool) })]
    internal static class Patch_FeedFire_Enable
    {
        private static void Prefix()
        {
            AnimalFatFuelMain.tempTinderInstances.Clear();
            AnimalFatFuelMain.tempCharcoalInstances.Clear();

            var inv = GameManager.GetInventoryComponent();
            if (inv == null) return;

            foreach (var obj in inv.m_Items)
            {
                GearItem gi = obj;
                if (gi == null) continue;

                var fuelSource = gi.m_FuelSourceItem;

                if (Settings.options.tinderAsFuel && fuelSource != null && fuelSource.m_IsTinder)
                {
                    AnimalFatFuelMain.tempTinderInstances.Add(gi.m_InstanceID);
                    fuelSource.m_IsTinder = false;
                }

                if (Settings.options.charcoalAsFuel && gi.name == "GEAR_Charcoal")
                {
                    if (gi.m_FuelSourceItem == null)
                    {
                        var fs = gi.gameObject.AddComponent<FuelSourceItem>();

                        fs.m_BurnDurationHours = Settings.options.charcoalBurnMinutes / 60f;
                        fs.m_HeatIncrease = Settings.options.charcoalHeatIncrease;
                        fs.m_HeatInnerRadius = 2.0f;
                        fs.m_HeatOuterRadius = 5.0f;
                        fs.m_IsTinder = false;

                        gi.m_FuelSourceItem = fs;

                        AnimalFatFuelMain.tempCharcoalInstances.Add(gi.m_InstanceID);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Panel_FeedFire), nameof(Panel_FeedFire.ExitFeedFireInterface))]
    internal static class Patch_FeedFire_Exit
    {
        private static void Postfix()
        {
            var inv = GameManager.GetInventoryComponent();
            if (inv == null)
            {
                AnimalFatFuelMain.tempTinderInstances.Clear();
                return;
            }

            foreach (var obj in inv.m_Items)
            {
                GearItem gi = obj;
                if (gi == null) continue;

                FuelSourceItem fuel = gi.m_FuelSourceItem;
                if (fuel == null) continue;

                if (
                    gi.name is "GEAR_Tinder" or "GEAR_PaperStack" or "GEAR_CattailTinder" or 
                    "GEAR_BarkTinder" or "GEAR_Newsprint" or "GEAR_NewsprintRoll" or "GEAR_CashBundle"
                )
                {
                    fuel.m_IsTinder = true;
                }

                if (gi.name == "GEAR_Charcoal")
                {
                    if (!fuel.m_IsTinder)
                    {
                        UnityEngine.Object.Destroy(fuel);
                        gi.m_FuelSourceItem = null;
                    }
                }
            }

            AnimalFatFuelMain.tempTinderInstances.Clear();
        }
    }

    [HarmonyPatch(typeof(GearItemListEntry), nameof(GearItemListEntry.Update))]
    public static class Patch_GearItemListEntry_AnimalFat
    {
        static void Postfix(GearItemListEntry __instance)
        {
            GearItem gi = __instance.m_GearItem;
            if (gi == null) return;

            UILabel condLabel = __instance.m_ConditionLabel;

            if (gi.name == "GEAR_Charcoal")
            {
                __instance.m_DisplayCondition = false;
                __instance.m_DisplayItemCount = true;

                if (condLabel != null)
                {
                    condLabel.text = "";
                    condLabel.enabled = true;
                }

                return;
            }
            if (gi.name == "GEAR_AnimalFat")
            {
                if (condLabel == null) return;

                if (__instance.m_IsSelected)
                {
                    __instance.m_DisplayCondition = true;
                    __instance.m_DisplayItemCount = false;

                    float norm = gi.GetNormalizedCondition();
                    string conditionText = Mathf.RoundToInt(norm * 100f) + "%";

                    float weightKG = gi.GetItemWeightKG(false) / ItemWeight.FromKilograms(1f);

                    condLabel.text = $"{conditionText} {weightKG:0.0}kg";
                    condLabel.enabled = true;
                }
                else
                {
                    __instance.m_DisplayCondition = false;
                    __instance.m_DisplayItemCount = true;

                    condLabel.text = "";
                    condLabel.enabled = true;
                }

                return;
            }
        }
    }

    // ==============================================
    [HarmonyPatch(typeof(FuelSourceItem), nameof(FuelSourceItem.GetModifiedBurnDurationHours))]
    internal static class PatchBurnDuration
    {
        private static bool Prefix(FuelSourceItem __instance, float normalizedCondition, ref float __result)
        {
            if (!Settings.options.animalFatAsFuel) return true;

            var gear = __instance.GetComponent<GearItem>();
            if (gear == null || !gear.name.Contains("GEAR_AnimalFat")) return true;

            float kg = gear.GetItemWeightKG(false) / ItemWeight.FromKilograms(1f);
            var skill = GameManager.GetSkillFireStarting();
            var arr = skill.m_DurationPercentIncrease;
            int level = skill.GetCurrentTierNumber();
            float multiplier = 1f + arr[level] / 100f;
            __result = (Settings.options.burnMinutesPerKg / 60f) * kg * multiplier;
            return false;
        }
    }

    internal static class FuelCalculator
    {
        public static void Apply(GearItem gear, FuelSourceItem fs)
        {
            float kg = gear.GetItemWeightKG(false) / ItemWeight.FromKilograms(1f);

            fs.m_BurnDurationHours = (Settings.options.burnMinutesPerKg / 60f) * kg;
            fs.m_HeatIncrease = Settings.options.heatIncrease;
            fs.m_HeatInnerRadius = 2.5f;
            fs.m_HeatOuterRadius = 6f;
            fs.m_IsTinder = false;
        }
    }
}