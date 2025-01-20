using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static KiSpaceDamageCalc.Common;
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
            if (PlayerInBossBattle())
            {
                StartDamageCalc();
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
            DamageCalcServer.UpdateReset();
            
            if (!PlayerInBossBattle())
            {
                EndDamageCalc();
            }
        }
        #region 辅助方法

        private void StartDamageCalc() {
            switch (Main.netMode) {
                case KiNetmodeID.SinglePlayer:
                    if (!DamageCalcSinglePlayer.started) DamageCalcSinglePlayer.Start();
                    break;
                case KiNetmodeID.MultiplayerClient:
                    if (!DamageCalcClient.started) DamageCalcClient.Start();
                    break;
                case KiNetmodeID.Server:
                    if (!DamageCalcServer.started) DamageCalcServer.Start();
                    break;
            }
        }
        private void EndDamageCalc() {
            switch (Main.netMode) {
                case KiNetmodeID.SinglePlayer:
                    if (DamageCalcSinglePlayer.started) DamageCalcSinglePlayer.Reset();
                    break;
                case KiNetmodeID.MultiplayerClient:
                    if (DamageCalcClient.started) DamageCalcClient.Reset();
                    break;
                case KiNetmodeID.Server:
                    if (DamageCalcServer.started) DamageCalcServer.StartReset();
                    break;
            }
        }
        
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