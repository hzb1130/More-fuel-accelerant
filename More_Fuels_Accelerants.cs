#nullable disable
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using Il2CppTLD.IntBackedUnit;
using ModSettings;
using System.Reflection;
using System.Collections.Generic;

[assembly: MelonInfo(typeof(AnimalFatFuel.AnimalFatFuelMain), "MORE_fuel_accelerant", "1.2.0", "HZB1130")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace AnimalFatFuel
{
    public class AnimalFatFuelMain : MelonMod
    {
        public static List<int> tempTinderInstances = new List<int>();
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

        [Section("Accelerant Custom Consume / 助燃剂自定义消耗")]

        [Name("Enable Accelerant Condition / 启用助燃剂耐久消耗")]
        [Description("Takes effect after reloading save. Take out accelerants from bag first. May have bugs. / 重新加载存档生效。建议先把背包中助燃剂取出。可能会有bug。")]        public bool enableCustomAccelerant = false;

        [Name("Condition Loss Per Use % / 每次点火消耗耐久%")]
        [Slider(1, 100)]
        public int accelerantConditionLoss = 25;

        protected override void OnChange(FieldInfo field, object oldValue, object newValue)
        {
            base.OnChange(field, oldValue, newValue);

            if (field.Name == nameof(tinderAsFuel))
            {
                SetFieldVisible(nameof(tinderBurnMultiplier), (bool)newValue);
            }

            if (field.Name == nameof(animalFatAsFuel))
            {
                bool vis = (bool)newValue;
                SetFieldVisible(nameof(burnMinutesPerKg), vis);
                SetFieldVisible(nameof(heatIncrease), vis);
            }
            if (field.Name == nameof(enableCustomAccelerant))
            {
                SetFieldVisible(nameof(accelerantConditionLoss), (bool)newValue);
            }
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
            options.SetFieldVisible(nameof(options.accelerantConditionLoss), options.enableCustomAccelerant);
        }
    }

//
// ==============================================
// 游戏早期：把助燃剂原型永久改为不可堆叠
// ==============================================
[HarmonyPatch(typeof(GameManager), nameof(GameManager.Awake))]
internal static class Patch_MakeAccelerantNonStackable
{
    private static void Postfix()
    {
        try
        {
            GearItem prefab = GearItem.LoadGearItemPrefab("GEAR_Accelerant");
            if (prefab == null) return;

            // 重量永远 0.1kg
            prefab.WeightKG = ItemWeight.FromKilograms(0.1f);

            if (Settings.options.enableCustomAccelerant)
            {
                // ============== 开启：删除堆叠 = 不可堆叠 ==============
                StackableItem stack = prefab.GetComponent<StackableItem>();
                if (stack != null)
                {
                    UnityEngine.Object.DestroyImmediate(stack);
                    // MelonLogger.Msg("[模组] 助燃剂 → 已删除堆叠组件");
                }
            }
            else
            {
                // ============== 关闭：恢复堆叠 = 可堆叠 ==============
                StackableItem existingStack = prefab.GetComponent<StackableItem>();
                if (existingStack == null)
                {
                    StackableItem newStack = prefab.gameObject.AddComponent<StackableItem>();
                    newStack.m_DefaultUnitsInItem = 1;
                    newStack.m_Units = 1;
                    // MelonLogger.Msg("[模组] 助燃剂 → 已恢复堆叠组件");
                }
            }
        }
        catch (Exception e)
        {
            MelonLogger.Error("错误: " + e.ToString());
        }
    }
}


[HarmonyPatch(typeof(Panel_FireStart), nameof(Panel_FireStart.OnStartFire))]
internal static class Patch_Accelerant_CustomConsume
{
    private static void Prefix(Panel_FireStart __instance)
    {
        try
        {
            // 不启用则直接跳过
            if (!Settings.options.enableCustomAccelerant)
                return;

            int index = __instance.m_SelectedAccelerantIndex;
            if (index < 0 || __instance.m_AccelerantList == null || index >= __instance.m_AccelerantList.Count)
                return;

            GearItem original = __instance.m_AccelerantList[index];
            if (original == null || original.name != "GEAR_Accelerant")
                return;

            // MelonLogger.Msg("[自定义消耗] 助燃剂 -1，生成低耐久版");

            // 移除旧的
            GameManager.GetInventoryComponent().RemoveGear(original.gameObject, true);

            // 生成新的
            GearItem newGear = GearItem.InstantiateGearItem("GEAR_Accelerant");
            if (newGear == null) return;

            newGear.SkipSpawnChanceRollInitialDecayAndAutoEvolve();

            // 使用设置中的百分比
            float loss = Settings.options.accelerantConditionLoss / 100f;
            float current = original.GetNormalizedCondition();
            float newCondition = Mathf.Max(0f, current - loss);

            newGear.SetNormalizedHP(newCondition, true);
            GameManager.GetInventoryComponent().AddGear(newGear, true);

            // MelonLogger.Msg($"[已生成] 助燃剂 {newCondition * 100:F0}%");
        }
        catch { }
    }
}


// [HarmonyPatch(typeof(Panel_FireStart), nameof(Panel_FireStart.OnStartFire))]
// internal static class Patch_Accelerant_CustomConsume
// {
//     private const float ACCELERANT_CONDITION_LOSS = 25f;

//     private static void Prefix(Panel_FireStart __instance)
//     {
//         try
//         {
//             if (__instance.m_SelectedAccelerantIndex < 0) return;
//             if (__instance.m_AccelerantList == null || __instance.m_AccelerantList.Count == 0) return;
//             if (__instance.m_SelectedAccelerantIndex >= __instance.m_AccelerantList.Count) return;

//             GearItem gear = __instance.m_AccelerantList[__instance.m_SelectedAccelerantIndex];
//             if (gear == null) return;

//             FireStarterItem fireStarter = gear.GetComponent<FireStarterItem>();
//             if (fireStarter == null || !fireStarter.m_IsAccelerant) return;

//             // 只处理标准助燃剂
//             if (gear.name != "GEAR_Accelerant") return;

//             MelonLogger.Msg($"[自定义消耗] 处理: {gear.name}");

//             // 扣除 25% 耐久
//             float currentPercent = gear.GetNormalizedCondition();
//             float newPercent = Mathf.Max(0f, currentPercent - 0.25f);
//             gear.SetNormalizedHP(newPercent, true);

//             MelonLogger.Msg($"[耐久消耗] -25% | 当前 {newPercent * 100:F0}%");

//             // 阻止原版删除
//             fireStarter.m_ConsumeOnUse = false;
//         }
//         catch { }
//     }
// }

// // ------------------------------
// // ✅ 核心：耐久 > 0 拦截销毁，耐久 = 0 允许消失
// // ------------------------------
// [HarmonyPatch(typeof(GearItem), nameof(GearItem.MarkForNextUpdateDestroy))]
// internal static class Patch_Prevent_Accelerant_Remove
// {
//     private static bool Prefix(GearItem __instance, bool value)
//     {
//         try
//         {
//             // 只针对标准助燃剂
//             if (value && __instance.name == "GEAR_Accelerant")
//             {
//                 // 耐久 > 0 → 拦截销毁（不消失）
//                 // 耐久 = 0 → 允许销毁（消失）
//                 return __instance.GetNormalizedCondition() <= 0f;
//             }
//         }
//         catch { }
        
//         // 其他物品正常逻辑
//         return true;
//     }
// }



    // 给动物脂肪添加燃料组件
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

    // 打开火堆界面时临时取消火引标记
    [HarmonyPatch(typeof(Panel_FeedFire), nameof(Panel_FeedFire.Enable), new[] { typeof(bool) })]
    internal static class Patch_FeedFire_Enable
    {
        private static void Prefix()
        {
            AnimalFatFuelMain.tempTinderInstances.Clear();

            if (!Settings.options.tinderAsFuel) return;

            var inv = GameManager.GetInventoryComponent();
            if (inv == null) return;

            foreach (var obj in inv.m_Items)
            {
                GearItem gi = obj;
                var fuelSource = gi.m_FuelSourceItem;
                if (fuelSource == null) continue;

                if (fuelSource.m_IsTinder)
                {
                    AnimalFatFuelMain.tempTinderInstances.Add(gi.m_InstanceID);
                    fuelSource.m_IsTinder = false;
                }
            }
        }
    }

    // 关闭界面恢复火引
    [HarmonyPatch(typeof(Panel_FeedFire), nameof(Panel_FeedFire.ExitFeedFireInterface))]
    internal static class Patch_FeedFire_Exit_Debug
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
                    gi.name == "GEAR_Tinder" ||
                    gi.name == "GEAR_PaperStack" ||
                    gi.name == "GEAR_CattailTinder" ||
                    gi.name == "GEAR_BarkTinder" ||
                    gi.name == "GEAR_Newsprint" ||
                    gi.name == "GEAR_NewsprintRoll" ||
                    gi.name == "GEAR_CashBundle"
                )
                {
                    fuel.m_IsTinder = true;
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

        var condLabel = __instance.m_ConditionLabel;
        if (condLabel == null) return;

        // ==========================================
        // 动物脂肪 → 自定义显示
        // ==========================================
        if (gi.name == "GEAR_AnimalFat")
        {
            if (__instance.m_IsSelected)
            {
                __instance.m_DisplayCondition = true;
                __instance.m_DisplayItemCount = false;

                float norm = gi.GetNormalizedCondition();
                string conditionText = Mathf.RoundToInt(norm * 100f) + "%";
                float weightKG = gi.GetItemWeightKG(false) / ItemWeight.FromKilograms(1f);
                string weightText = $"{weightKG:0.0}kg";

                condLabel.text = conditionText + " " + weightText;
                condLabel.enabled = true;
                condLabel.SetDirty();
            }
            else
            {
                __instance.m_DisplayCondition = false;
                condLabel.text = "";
                condLabel.enabled = false;
            }
        }

        // ==========================================
        // 火把 → 恢复原生显示（自动显示XX%）
        // ==========================================
        else if (gi.name == "GEAR_Torch")
        {
            if (__instance.m_IsSelected)
            {
                float norm = gi.GetNormalizedCondition();
                condLabel.text = Mathf.RoundToInt(norm * 100f) + "%";
                condLabel.enabled = true;
                condLabel.SetDirty();
            }
            else
            {
                condLabel.text = "";
                condLabel.enabled = false;
            }
        }

        // ==========================================
        // 其他物品 → 完全不处理
        // ==========================================
    }
}

    // 自定义燃烧时间
    [HarmonyPatch(typeof(FuelSourceItem), nameof(FuelSourceItem.GetModifiedBurnDurationHours))]
    internal static class PatchBurnDuration
    {
        private static bool Prefix(FuelSourceItem __instance, float normalizedCondition, ref float __result)
        {
            if (!Settings.options.animalFatAsFuel) return true;

            var gear = __instance.GetComponent<GearItem>();
            if (gear == null || !gear.name.Contains("GEAR_AnimalFat")) return true;

            float kg = gear.GetItemWeightKG(false) / ItemWeight.FromKilograms(1f);
            __result = (Settings.options.burnMinutesPerKg / 60f) * kg;
            return false;
        }
    }

    // 燃料属性设置
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