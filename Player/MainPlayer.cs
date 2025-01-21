using Terraria;
using Terraria.ModLoader;
using static KiSpaceDamageCalc.Common;
using static Terraria.Player;

namespace KiSpaceDamageCalc.Player
{
    public class MainPlayer : ModPlayer
    {
            public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
            {
            if (Main.myPlayer != Player.whoAmI || !PlayerInBossBattle()) return;
            AddDamage(hit.Damage, item);
            }

            public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
            {
            if (Main.myPlayer != Player.whoAmI || !PlayerInBossBattle()) return;
            AddDamage(hit.Damage, proj);
            }
            
            public override bool FreeDodge(HurtInfo info)
            {
                  if (Main.myPlayer != Player.whoAmI || !PlayerInBossBattle()) return base.FreeDodge(info);
                  else AddHitTakenCount();
                  
                  return base.FreeDodge(info);
            }
    }
}