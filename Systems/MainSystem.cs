using KiSpaceDamageCalc.NPCs;
using MonoMod.RuntimeDetour;
using System;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static KiSpaceDamageCalc.KiSpaceDamageCalc;

namespace KiSpaceDamageCalc.Systems
{
    public class MainSystem : ModSystem
    {
        private Hook hookSendData;
        private delegate void orig_SendData(int msgType, int remoteClient = -1, int ignoreClient = -1, NetworkText text = null, int number = 0, float number2 = 0f, float number3 = 0f, float number4 = 0f, int number5 = 0, int number6 = 0, int number7 = 0);        private static FieldInfo currentStrikeField;
        private static FieldInfo lastLegacyStrikeField;

        private static int _serverTick;
        public static int ServerTick { get => _serverTick; set => _serverTick = value; }
        public override void PreUpdateEntities()
        {
            KiLogger.UpdateLogTimer();
            if (Main.netMode != KiNetmodeID.MultiplayerClient)
            {
                ServerTick++;
                return;
            }
            RequestServerData();
        }
        public override void Load()
        {
            Type netMessageType = typeof(Terraria.NetMessage);

            currentStrikeField = netMessageType.GetField("_currentStrike", BindingFlags.NonPublic | BindingFlags.Static);
            lastLegacyStrikeField = netMessageType.GetField("_lastLegacyStrike", BindingFlags.NonPublic | BindingFlags.Static);

            MethodInfo sendDataMethod = netMessageType.GetMethod("SendData", BindingFlags.Public | BindingFlags.Static);
            hookSendData = new Hook(sendDataMethod, SendDataDetour);
        }

        private void SendDataDetour(orig_SendData orig, int msgType, int remoteClient = -1, int ignoreClient = -1, NetworkText text = null, 
            int number = 0, float number2 = 0f, float number3 = 0f, float number4 = 0f, int number5 = 0, int number6 = 0, int number7 = 0)
        {
            if (msgType == 28)
            {
                try
                {
                    var currentStrike = (NPC.HitInfo)currentStrikeField.GetValue(null);
                    var lastLegacyStrike = (NPC.HitInfo)lastLegacyStrikeField.GetValue(null);

                    var hit = number7 == 1 ? currentStrike : lastLegacyStrike;

                    // if (!Main.dedServ) KiLogger.LogOnMutiMode($"伤害值: [{hit.Damage}],本地玩家: {Main.LocalPlayer.name}", logToFile: true);
                    DamageCalc.AddDamage(hit.Damage);
                }
                catch (Exception e)
                {
                    Mod.Logger.Error("Error in SendDataDetour: " + e.Message);
                }
            }

            orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7);
        }

        public override void Unload()
        {
            hookSendData?.Dispose();
            hookSendData = null;
            currentStrikeField = null;
            lastLegacyStrikeField = null;
        }

        public override void PostUpdateNPCs()
        {
            if (!GNPC.PlayerInBossBattle() && DamageCalc.started && Main.dedServ)
                DamageCalc.Reset();
        }

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
    }
}