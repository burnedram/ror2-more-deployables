using BepInEx;
using RoR2;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System;

namespace R2MoreDeployables
{
    [BepInPlugin("com.burnedram.r2moredeployables", "More Deployables", "0.0.1")]
    public class R2MoreDeployables : BaseUnityPlugin
    {

        public R2MoreDeployables()
        {
            IL.RoR2.CharacterMaster.AddDeployable += ModifyAddDeployable;
            On.RoR2.GenericSkill.SetBonusStockFromBody += BackupMagazineSpecialStock;
        }

        private void BackupMagazineSpecialStock(On.RoR2.GenericSkill.orig_SetBonusStockFromBody orig, GenericSkill self, int newBonusStockFromBody)
        {
            // Make backup magazines work with engi special skill as well
            orig(self, newBonusStockFromBody);
            if (self.skillNameToken == "ENGI_SECONDARY_NAME")
            {
                self.gameObject.GetComponent<SkillLocator>().special.SetBonusStockFromBody(newBonusStockFromBody);
            }
        }

        private int GetDeployableLimit(CharacterMaster self, DeployableSlot slot)
        {
            switch (slot)
            {
                case DeployableSlot.EngiMine:
                    return 10;
                case DeployableSlot.EngiTurret:
                    return 2 + self.inventory.GetItemCount(ItemIndex.SecondarySkillMagazine);
                case DeployableSlot.BeetleGuardAlly:
                    return self.inventory.GetItemCount(ItemIndex.BeetleGland);
                case DeployableSlot.EngiBubbleShield:
                    return 1;
                default:
                    return 0;
            }
        }

        private void ModifyAddDeployable(ILContext il)
        {
            //Logger.LogDebug("Removing switch(slot) IL");
            var cursor = new ILCursor(il).Goto(0);

            cursor.GotoNext(x => x.MatchSwitch(out _));
            var switchStart = cursor.Prev;
            //Logger.LogDebug($"Switch start: {switchStart.OpCode} {switchStart.Offset}");
            // AddDeployable(Deployable deployable, DeployableSlot slot)
            // Argument #0 is this (or self), #1 is deployable, #2 is slot
            if (!switchStart.MatchLdarg(2))
            {
                Logger.LogError("Could not find start of switch(slot) IL");
                return;
            }

            var switchBreak = cursor.Next.Next;
            if (!switchBreak.MatchBr(out _))
            {
                Logger.LogError("Could not find end of switch(slot) IL");
                return;
            }
            var switchBreakLabel = (ILLabel)switchBreak.Operand;
            var switchEnd = switchBreakLabel.Target.Previous;
            //Logger.LogDebug($"Switch end: {switchEnd.OpCode} {switchEnd.Offset}");

            cursor.Goto(switchStart);
            cursor.RemoveRange(cursor.Context.IndexOf(switchEnd) - cursor.Index + 1);

            //Logger.LogDebug("Emitting delagate replacement");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_2);
            cursor.EmitDelegate<Func<CharacterMaster, DeployableSlot, int>>(GetDeployableLimit);
            cursor.Emit(OpCodes.Stloc_1);
        }
    }
}
