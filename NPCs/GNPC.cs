using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static KiSpaceDamageCalc.KiSpaceDamageCalc;

namespace KiSpaceDamageCalc.NPCs
{
    public class GNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            if (PlayerInBossBattle() && Main.dedServ && !DamageCalc.started)
            {
                DamageCalc.Start();
                KiLogger.LogOnMutiMode($"Lifemax: {npc.lifeMax}", logToFile: true);
            }
        }

        public static bool PlayerInBossBattle()
        {
            foreach (NPC npc in Main.npc) {
                if ((npc.active && npc.boss) || npc.type == NPCID.EaterofWorldsHead) {
                    return true;
                }
            }
            return false;
        }
    }

    public static class DamageCalc{
        public static int ServerTotalDamage = 0;
        public static int totalDamage = 0;
        public static Dictionary<string, int> PlayerToTotalDamage = new Dictionary<string, int>();
        public static bool started = false;

        public static void AddDamage(int damage){
            if (!started || Main.dedServ)
                return;

            totalDamage += damage;

            SendTotalDamageToServer(totalDamage, Main.LocalPlayer.name);
        }

        public static void SetPlayerDamageServer(int damage, string playerName){
            PlayerToTotalDamage[playerName] = damage;
        }

        public static void Start(){
            KiLogger.LogOnMutiMode("开始记录", logToFile: true);
            started = true;
            PlayerToTotalDamage.Clear();
            totalDamage = 0;

            if (Main.dedServ)
                SendStartToClient();
        }

        public static void Reset(){
            if (!Main.dedServ)
            {
                started = false;
                PlayerToTotalDamage.Clear();
                totalDamage = 0;
                return;
            }

            foreach (int totalDamage in PlayerToTotalDamage.Values)
            {
                ServerTotalDamage += totalDamage;
            }

            string msg = "Total Team Damage: " + ServerTotalDamage;
            KiLogger.LogOnMutiMode(msg, logToFile: true);
            foreach (var pair in PlayerToTotalDamage)
            {
                KiLogger.LogOnMutiMode($"{pair.Key}'s Total Damage: {pair.Value}", logToFile: true);
            }
            ServerTotalDamage = 0;
            PlayerToTotalDamage.Clear();
            started = false;
            SendEndToClient();
        }
        

        public static void SendTotalDamageToServer(int totalDamage, string playerName)
        {
            if (Main.dedServ) return;

            ModPacket packet = ThisMod.GetPacket();
            packet.Write((byte)NetMessageType.DamageCalc);
            packet.Write(totalDamage);
            packet.Write(playerName);
            // KiLogger.LogOnMutiMode($"发送 - 总伤害值: [{totalDamage}],玩家: {playerName}", packet: packet, logToFile: true);
            packet.Send();
        }

        public static void ReceiveDamageFromClient(BinaryReader reader)
        {
            int totalDamage = reader.ReadInt32();
            string playerName = reader.ReadString();

            // KiLogger.LogOnMutiMode($"接收 - 总伤害值: [{totalDamage}],玩家: {playerName}", logToFile: true,reader: reader);
            SetPlayerDamageServer(totalDamage, playerName);
        }

        public static void SendStartToClient()
        {
            ModPacket packet = ThisMod.GetPacket();
            packet.Write((byte)NetMessageType.StartRecording);
            packet.Send();
        }

        public static void ReceiveStartFromServer()
        {
            Start();
        }

        public static void SendEndToClient()
        {
            if (!Main.dedServ) return;
            ModPacket packet = ThisMod.GetPacket();
            packet.Write((byte)NetMessageType.EndRecording);
            packet.Send();
        }

        public static void ReceiveEndFromServer()
        {
            Reset();
        }
    }
}