using Terraria;
using Terraria.ModLoader;

using KiSpaceDamageCalc.Systems;

namespace KiSpaceDamageCalc.Player
{
    public class MainPlayer : ModPlayer
    {
		public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
		{
            if (Main.myPlayer != Player.whoAmI || !MainSystem.PlayerInBossBattle()) return;
            DamageCalcClient.AddDamage(hit.Damage, item);
		}

		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
            if (Main.myPlayer != Player.whoAmI || !MainSystem.PlayerInBossBattle()) return;
            DamageCalcClient.AddDamage(hit.Damage, proj);
		}
    }
}