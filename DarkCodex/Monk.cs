﻿using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Items.Armors;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Enums;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Enums;
using Kingmaker.Items;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Components.CasterCheckers;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace DarkCodex
{
    public class Monk
    {
        [PatchInfo(Severity.Create, "Feral Combat Training", "basic feat: Feral Combat Training", true, Requirement: typeof(Patch_FeralCombat))]
        public static void createFeralCombatTraining()
        {
            /*
             Feral Combat Training (Combat)
            You were taught a style of martial arts that relies on the natural weapons from your racial ability or class feature.
            Prerequisite: Improved Unarmed Strike, Weapon Focus with selected natural weapon.
            Benefit: Choose one of your natural weapons. While using the selected natural weapon, you can apply the effects of feats that have Improved Unarmed Strike as a prerequisite.
            Special: If you are a monk, you can use the selected natural weapon with your flurry of blows class feature.
             */
            var unarmedstrike = Helper.ToRef<BlueprintFeatureReference>("7812ad3672a4b9a4fb894ea402095167"); //ImprovedUnarmedStrike
            var weaponfocus = Helper.ToRef<BlueprintFeatureReference>("1e1f627d26ad36f43bbd26cc2bf8ac7e"); //WeaponFocus
            var zenarcher = Helper.ToRef<BlueprintArchetypeReference>("2b1a58a7917084f49b097e86271df21c"); //ZenArcherArchetype

            string name = "Feral Combat Training";
            string description = "While using any natural weapon, you can apply the effects of feats that have Improved Unarmed Strike as a prerequisite. Special: If you are a monk, you can use natural weapons with your flurry of blows class feature.";

            var feature = Helper.CreateBlueprintFeature(
                "FeralCombatTrainingFeature",
                name,
                description,
                group: FeatureGroup.Feat
                ).SetComponents(
                Helper.CreatePrerequisiteFeature(unarmedstrike),
                Helper.CreatePrerequisiteFeature(weaponfocus),
                Helper.CreatePrerequisiteNoArchetype(zenarcher)
                );

            Resource.Cache.FeatureFeralCombat.SetReference(feature);

            Helper.AddFeats(Resource.Cache.FeatureFeralCombat);
        }
    }

    #region Patches

    [PatchInfo(Severity.Harmony, "Patch: Feral Combat", "collection of patches for Feral Combat Training", false)]
    [HarmonyPatch]
    public class Patch_FeralCombat
    {
        //search for: ImprovedUnarmedStrike
        //skip: PummelingCharge, Patch_DeflectArrows_CheckRestriction_Patch
        //OK: AddInitiatorAttackWithWeaponTrigger		AddInitiatorAttackWithWeaponTriggerOrFeralTraining
        //OK: AbilityCasterMainWeaponCheck			AbilityCasterMainWeaponCheckOrFeralCombat
        //OK: MonkNoArmorAndMonkWeaponFeatureUnlock	MonkNoArmorAndMonkWeaponOrFeralCombatFeatureUnlock
        //OK: AdditionalStatBonusOnAttackDamage		AdditionalStatBonusOnAttackDamageOrFeralCombat

        [HarmonyPatch(typeof(AddInitiatorAttackWithWeaponTrigger), nameof(AddInitiatorAttackWithWeaponTrigger.IsSuitable), new Type[] { typeof(RuleAttackWithWeapon) })]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            List<CodeInstruction> list = instr.ToList();
            MethodInfo reference = AccessTools.PropertyGetter(typeof(BlueprintItemWeapon), "Category");
            MethodInfo call = AccessTools.Method(typeof(Patch_FeralCombat), nameof(Call));

            int index = list.FindIndex(f => f.Calls(reference)) + 2; //68
            int label = index - 1;

            for (int i = 64; i <= 70; i++)
                Helper.PrintInstruction(list[i], i.ToString());
            Helper.Print("Patching at " + index);

            list.Insert(index++, new CodeInstruction(OpCodes.Ldarg_0));
            list.Insert(index++, new CodeInstruction(OpCodes.Ldarg_1));
            list.Insert(index++, new CodeInstruction(OpCodes.Call, call));
            list.Insert(index++, new CodeInstruction(OpCodes.Brtrue_S, list[label].operand)); //label=11

            return list;
        }
        public static bool Call(AddInitiatorAttackWithWeaponTrigger instance, RuleAttackWithWeapon evt) //ldarg.1
        {
            return instance.Category == WeaponCategory.UnarmedStrike && evt.Weapon.Blueprint.IsNatural && evt.Initiator.Descriptor.HasFact(Resource.Cache.FeatureFeralCombat);
        }

        [HarmonyPatch(typeof(AbilityCasterMainWeaponCheck), nameof(AbilityCasterMainWeaponCheck.IsCasterRestrictionPassed))]
        [HarmonyPostfix]
        public static void Postfix2(UnitEntityData caster, AbilityCasterMainWeaponCheck __instance, ref bool __result) // stunning fist & co
        {
            __result = __result ||
                caster.Body.PrimaryHand.Weapon.Blueprint.IsNatural
                && __instance.Category.Contains(WeaponCategory.UnarmedStrike)
                && caster.Descriptor.HasFact(Resource.Cache.FeatureFeralCombat);
        }

        [HarmonyPatch(typeof(MonkNoArmorAndMonkWeaponFeatureUnlock), nameof(MonkNoArmorAndMonkWeaponFeatureUnlock.CheckEligibility))]
        [HarmonyPrefix]
        public static bool Prefix3(MonkNoArmorAndMonkWeaponFeatureUnlock __instance) // flurry of blows
        {
            if (!__instance.Owner.Descriptor.HasFact(Resource.Cache.FeatureFeralCombat))
                return true;

            if (__instance.IsZenArcher)
                return true;

            var body = __instance.Owner.Body;

            if (__instance.IsSohei)
            {
                if (!body.SecondaryHand.HasShield
                && (!body.Armor.HasArmor || body.Armor.Armor.Blueprint.ProficiencyGroup == ArmorProficiencyGroup.Light)
                && (body.PrimaryHand.Weapon.Blueprint.IsMonk || body.PrimaryHand.Weapon.Blueprint.IsNatural) || (bool)__instance.Owner.Get<UnitPartWeaponTraining>()?.IsSuitableWeapon(body.PrimaryHand.MaybeWeapon))
                    __instance.AddFact();
                else
                    __instance.RemoveFact();
                return false;
            }

            if (!body.SecondaryHand.HasShield
            && (!body.Armor.HasArmor || !body.Armor.Armor.Blueprint.IsArmor)
            && (body.PrimaryHand.Weapon.Blueprint.IsMonk || body.PrimaryHand.Weapon.Blueprint.IsNatural))
                __instance.AddFact();
            else
                __instance.RemoveFact();

            return false;
        }

        [HarmonyPatch(typeof(AdditionalStatBonusOnAttackDamage), nameof(AdditionalStatBonusOnAttackDamage.CheckConditions))]
        [HarmonyPrefix]
        public static bool Prefix4(RuleCalculateWeaponStats evt, AdditionalStatBonusOnAttackDamage __instance, ref bool __result) // dragon style
        {
            __result = false;

            if (__instance.FullAttack != ConditionEnum.Irrelevant && evt.AttackWithWeapon == null)
                return false;
            if (__instance.FullAttack == ConditionEnum.Only && !evt.AttackWithWeapon.IsFullAttack)
                return false;
            if (__instance.FullAttack == ConditionEnum.Not && evt.AttackWithWeapon.IsFullAttack)
                return false;

            if (__instance.FirstAttack != ConditionEnum.Irrelevant && evt.AttackWithWeapon == null)
                return false;
            if (__instance.FirstAttack == ConditionEnum.Only && !evt.AttackWithWeapon.IsFirstAttack)
                return false;
            if (__instance.FirstAttack == ConditionEnum.Not && evt.AttackWithWeapon.IsFirstAttack)
                return false;

            if (__instance.CheckCategory)
            {
                if (evt.Weapon == null ||
                    (__instance.Category != evt.Weapon.Blueprint.Category && (!evt.Weapon.Blueprint.IsNatural
                        || __instance.Category != WeaponCategory.UnarmedStrike
                        || !evt.Initiator.Descriptor.HasFact(Resource.Cache.FeatureFeralCombat))))
                    return false;
            }

            if (__instance.CheckTwoHanded)
            {
                if (evt.Weapon == null || !evt.Weapon.HoldInTwoHands || !WeaponRangeType.Melee.IsSuitableWeapon(evt.Weapon))
                    return false;
            }

            __result = true;
            return false;

        }

    }

    #endregion
}
