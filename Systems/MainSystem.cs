using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static KiSpaceDamageCalc.KiSpaceDamageCalc;

namespace KiSpaceDamageCalc.Systems
{
    public class MainSystem : ModSystem
    {
        private static int _serverTick;
        public static int ServerTick { get => _serverTick; set => _serverTick = value; }
        public override void PreUpdateEntities()
        {
            KiLogger.UpdateLogTimer();
            if (PlayerInBossBattle() && !(DamageCalcServer.started || DamageCalcClient.started))
            {
                DamageCalcServer.Start();
                DamageCalcClient.Start();
            }
            DamageCalcClient.CheckDamaged();
            if (Main.netMode != KiNetmodeID.MultiplayerClient)
            {
                ServerTick++;
                return;
            }
            RequestServerData();
        }

        public override void PostUpdateNPCs()
        {
            if (!PlayerInBossBattle() && (DamageCalcServer.started || DamageCalcClient.started))
            {
                DamageCalcServer.StartReset();
                DamageCalcClient.Reset();
            }

            DamageCalcServer.UpdateReset();
        }
        #region 辅助方法
        public static bool PlayerInBossBattle()
        {
            foreach (NPC npc in Main.npc) {
                if ((npc.active && npc.boss) || npc.type == NPCID.EaterofWorldsHead) {
                    return true;
                }
            }
            return false;
        }

        public static string GetKiSpaceDamageCalcText(string key, params object[] args) => Language.GetTextValue($"Mods.KiSpaceDamageCalc.{key}", args);
        
        #endregion
        #region 同步
        public static void RequestServerData()
        {
            if (Main.netMode != KiNetmodeID.MultiplayerClient) return;

            ModPacket packet = ThisMod.GetPacket();
            packet.Write((byte)NetMessageType.ServerData);
            packet.Send(-1, -1);
        }

        internal static void HandelServerData(NetMessageType type, BinaryReader reader, bool hasHandled, out bool handled)
        {
            handled = true;
            if (hasHandled) return;
            if (type != NetMessageType.ServerData)
            {
                handled = false;
                return;
            }
            if (Main.netMode == KiNetmodeID.Server)
            {
                SendServerData();
                return;
            }
            ServerTick = reader.ReadInt32();
        }

        private static void SendServerData() {
            ModPacket packet = ThisMod.GetPacket();
            packet.Write((byte)NetMessageType.ServerData);
            packet.Write(ServerTick);
            packet.Send(-1, -1);
        }
        #endregion
    }
}