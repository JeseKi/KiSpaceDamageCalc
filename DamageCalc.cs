using System.Collections.Generic;
using System.IO;
using System.Linq;
using KiSpaceDamageCalc.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using static KiSpaceDamageCalc.KiSpaceDamageCalc;

namespace KiSpaceDamageCalc;

public static class DamageCalcServer{
    public static int AllPlayersTotalDamage = 0;
    public static bool started = false;
    public static int EndTime = 0;
    public const int MAX_WAIT_TIME = 60;
    public static bool InReseting = false;
    /// <summary>
    /// 各玩家造成的总伤害
    /// </summary>
    public static Dictionary<string, int> PlayerToTotalDamage = new Dictionary<string, int>();
    /// <summary>
    /// 造成过投射物伤害的玩家
    /// </summary>
    public static List<string> PlayersProjDamaged = new List<string>();
    /// <summary>
    /// 各玩家造成了投射物伤害后，结束战斗时是否收到了该玩家的数据
    /// </summary>
    public static List<string> PlayerToProjDamageReceived = new List<string>();
    /// <summary>
    /// 各玩家造成的投射物伤害具体信息
    /// </summary>
    public static Dictionary<string, Dictionary<string, int>> PlayerToProjDamage = new Dictionary<string, Dictionary<string, int>>();
    /// <summary>
    /// 造成过道具伤害的玩家
    /// </summary>
    public static List<string> PlayersItemDamaged = new List<string>();
    /// <summary>
    /// 各玩家造成了道具伤害后，结束战斗时是否收到了该玩家的数据
    /// </summary>
    public static List<string> PlayerToItemDamageReceived = new List<string>();
    /// <summary>
    /// 各玩家造成的道具伤害具体信息
    /// </summary>
    public static Dictionary<string, Dictionary<string, int>> PlayerToItemDamage = new Dictionary<string, Dictionary<string, int>>();
    public static void Start(){
        if (started)
            return;

        // KiLogger.LogOnMutiMode("开始记录", logToFile: true);
        started = true;

        SendStartToClient();
    }

    public static void StartReset()
    {
        if (InReseting || !Main.dedServ) return; 

        InReseting = true;
        EndTime = MainSystem.ServerTick;
        SendEndToClient();
        // KiLogger.LogOnMutiMode("开始重置", logToFile: true);
    }

    public static void UpdateReset()
    {
        if (!InReseting) return;

        if (NeedBroadcast())
        {
            InReseting = false;
            Reset();
        }
    }

    public static void Reset(){

        foreach (int totalDamage in PlayerToTotalDamage.Values)
        {
            AllPlayersTotalDamage += totalDamage;
        }

        DisplayDamageStatistics();
        
        AllPlayersTotalDamage = 0;
        EndTime = 0;
        started = false;
        
        PlayerToTotalDamage.Clear();
        
        PlayersProjDamaged.Clear();
        PlayerToProjDamageReceived.Clear();
        PlayerToProjDamage.Clear();

        PlayersItemDamaged.Clear();
        PlayerToItemDamageReceived.Clear();
        PlayerToItemDamage.Clear();
        
        // KiLogger.LogOnMutiMode("重置完成", logToFile: true);
    }

    public static bool NeedBroadcast(){
        // KiLogger.LogWithTimer("检测是否需要播报", logOnMutiMode: true,logToFile: true);
        if (MainSystem.ServerTick - EndTime > MAX_WAIT_TIME) return true;
        
        if (PlayersItemDamaged.FindIndex(p => !PlayerToItemDamageReceived.Contains(p)) != -1) return false;

        if (PlayersProjDamaged.FindIndex(p => !PlayerToProjDamageReceived.Contains(p)) != -1) return false;

        return true;
    }

    private static Color GetPlayerRankColor(int rank, int totalPlayers)
    {
        if (rank == 0) return Color.Red;
        if (rank == 1) return Color.Yellow;
        if (rank == 2) return Color.Green;
        
        float progress = (rank - 3) / (float)(totalPlayers - 3);
        return Color.Lerp(Color.Green, Color.White, progress);
    }

    private static void DisplayDamageStatistics()
    {
        // 计算每个玩家的总伤害并排序
        var playerTotalDamages = PlayerToTotalDamage
            .OrderByDescending(p => p.Value)
            .ToList();
        
        KiLogger.LogOnMutiMode($"========== {MainSystem.GetKiSpaceDamageCalcText("DamageStatistics")} ==========",Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
        KiLogger.LogOnMutiMode($"{MainSystem.GetKiSpaceDamageCalcText("TotalTeamDamage")}: {AllPlayersTotalDamage}", Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
        
        // 显示玩家排名
        for (int i = 0; i < playerTotalDamages.Count; i++)
        {
            var player = playerTotalDamages[i];
            float damagePercentage = (float)player.Value / AllPlayersTotalDamage * 100;
            Color rankColor = GetPlayerRankColor(i, playerTotalDamages.Count);
            
            string playerRank = $"{player.Key}: {player.Value} | {damagePercentage:F1}%";
            KiLogger.LogOnMutiMode(playerRank, color: rankColor, logCodePosition: false, logServerTick: false, logPlatform: false);
            
            // 显示该玩家的伤害来源详情
            if (PlayerToProjDamage.TryGetValue(player.Key, out var projDamages))
            {
                foreach (var proj in projDamages)
                {
                    float projPercentage = (float)proj.Value / player.Value * 100;
                    KiLogger.LogOnMutiMode($"    {proj.Key}: {proj.Value} | {projPercentage:F1}%",rankColor, logCodePosition: false, logServerTick: false, logPlatform: false);
                }
            }
            
            if (PlayerToItemDamage.TryGetValue(player.Key, out var itemDamages))
            {
                foreach (var item in itemDamages)
                {
                    float itemPercentage = (float)item.Value / player.Value * 100;
                    KiLogger.LogOnMutiMode($"    {item.Key}: {item.Value} | {itemPercentage:F1}%",rankColor, logCodePosition: false, logServerTick: false, logPlatform: false);
                }
            }
        }

        KiLogger.LogOnMutiMode("==========================",Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
    }
    
    #region 同步
    /// <summary>
    /// 接收玩家造成的总伤害
    /// </summary>
    /// <param name="totalDamage"></param>
    /// <param name="playerName"></param>
    public static void ReceiveDamageFromClient(BinaryReader reader)
    {
        int totalDamage = reader.ReadInt32();
        string playerName = reader.ReadString();

        // KiLogger.LogOnMutiMode($"接收 - 总伤害值: [{totalDamage}],玩家: {playerName}", logToFile: true,reader: reader);
        PlayerToTotalDamage[playerName] = totalDamage;
    }

    /// <summary>
    /// 通知客户端开始记录，避免某些情况下客户端应当开始记录但未开始记录的情况
    /// </summary>
    public static void SendStartToClient()
    {
        ModPacket packet = ThisMod.GetPacket();
        packet.Write((byte)NetMessageType.StartRecording);
        packet.Send();
    }

    /// <summary>
    /// 通知客户端结束记录，避免某些情况下客户端未结束记录的情况
    /// </summary>
    public static void SendEndToClient()
    {
        ModPacket packet = ThisMod.GetPacket();
        packet.Write((byte)NetMessageType.EndRecording);
        packet.Send();
    }

    /// <summary>
    /// 接收记录玩家是否造成了伤害
    /// </summary>
    /// <param name="playerName"></param>
    /// <param name="projDamaged"></param>
    /// <param name="itemDamaged"></param>
    public static void ReceivePlayerDamaged(BinaryReader reader)
    {
        string playerName = reader.ReadString();
        bool projDamaged = reader.ReadBoolean();
        bool itemDamaged = reader.ReadBoolean();

        if (projDamaged && !PlayersProjDamaged.Contains(playerName))
            PlayersProjDamaged.Add(playerName);
        if (itemDamaged && !PlayersItemDamaged.Contains(playerName))
            PlayersItemDamaged.Add(playerName);

        // KiLogger.LogOnMutiMode($"接收 - 玩家[{Main.LocalPlayer.name}]造成了伤害, 投射物伤害：{projDamaged}, 物品伤害：{itemDamaged}", reader: reader);
    }

    /// <summary>
    /// 接收记录玩家造成的伤害
    /// </summary>
    /// <param name="reader"></param>
    public static void ReceiveAllDamageDataFromClient(BinaryReader reader)
    {
        string playerName = reader.ReadString();
        PlayerToItemDamageReceived.Add(playerName);
        PlayerToProjDamageReceived.Add(playerName);

        if (PlayersProjDamaged.Contains(playerName))
        {
            PlayerToProjDamage[playerName] = new Dictionary<string, int>();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string projName = reader.ReadString();
                int damage = reader.ReadInt32();
                PlayerToProjDamage[playerName][projName] = damage;
            }
        }

        if (PlayersItemDamaged.Contains(playerName))
        {
            PlayerToItemDamage[playerName] = new Dictionary<string, int>();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string itemName = reader.ReadString();
                int damage = reader.ReadInt32();
                PlayerToItemDamage[playerName][itemName] = damage;
            }
        }

        // KiLogger.LogOnMutiMode($"接收 - 玩家[{playerName}]的全部数据", reader: reader);
    }
    #endregion
}

public static class DamageCalcClient
{
    public static int LocalTotalDamage = 0;
    public static Dictionary<string, int> ProjToTotalDamage = new Dictionary<string, int>();
    public static Dictionary<string, int> ItemToTotalDamage = new Dictionary<string, int>();
    public static bool oldProjDamaged = false;
    public static bool ProjDamaged = false;
    public static bool oldItemDamaged = false;
    public static bool ItemDamaged = false;
    public static bool started = false;

    public static void AddDamage(int damage){
        if (!started || Main.dedServ)
            return;

        LocalTotalDamage += damage;

        SendTotalDamageToServer(LocalTotalDamage, Main.LocalPlayer.name);
    }

    public static void AddDamage(int damage, Projectile proj){
        if (!started || Main.dedServ)
            return;

        if (!ProjToTotalDamage.ContainsKey(proj.Name))
            ProjToTotalDamage[proj.Name] = 0;
        
        ProjToTotalDamage[proj.Name] += damage;
        
        ProjDamaged = true;
        AddDamage(damage);
    }

    public static void AddDamage(int damage, Item item){
        if (!started || Main.dedServ)
            return;

        if (!ItemToTotalDamage.ContainsKey(item.Name))
            ItemToTotalDamage[item.Name] = 0;
        
        ItemToTotalDamage[item.Name] += damage;
        
        ItemDamaged = true;
        AddDamage(damage);
    }

    public static void Start(){
        if (started || Main.dedServ)
            return;

        // KiLogger.LogOnMutiMode("开始记录", logToFile: true);
        started = true;
    }

    public static void Reset(){
        if (Main.dedServ) return;

        SendAllDamageDataToServer();

        started = false;
        LocalTotalDamage = 0;
        ProjToTotalDamage.Clear();
        ItemToTotalDamage.Clear();
        oldItemDamaged = false;
        ItemDamaged = false;
        oldProjDamaged = false;
        ProjDamaged = false;
        return;
    }

    public static void CheckDamaged(){
        if (Main.dedServ) return;
        
        if (oldItemDamaged != ItemDamaged || oldProjDamaged != ProjDamaged)
        {
            SendDamagedToServer();
            oldItemDamaged = ItemDamaged;
            oldProjDamaged = ProjDamaged;

            KiLogger.LogOnMutiMode("");
        }
    }
    
    #region 同步
    /// <summary>
    /// 同步客户端玩家造成的总伤害
    /// </summary>
    /// <param name="totalDamage"></param>
    /// <param name="playerName"></param>
    public static void SendTotalDamageToServer(int totalDamage, string playerName)
    {
        ModPacket packet = ThisMod.GetPacket();
        packet.Write((byte)NetMessageType.DamageCalc);
        packet.Write(totalDamage);
        packet.Write(playerName);
        // KiLogger.LogOnMutiMode($"发送 - 总伤害值: [{totalDamage}],玩家: {playerName}", packet: packet, logToFile: true);
        packet.Send();
    }

    /// <summary>
    /// 使客户端开始记录，避免某些情况下客户端应当开始记录但未开始记录的情况
    /// </summary>
    public static void ReceiveStartFromServer()
    {
        Start();
    }

    /// <summary>
    /// 客户端接收结束记录，避免某些情况下客户端未结束记录的情况
    /// </summary>
    public static void ReceiveEndFromServer()
    {
        Reset();
    }

    /// <summary>
    /// 记录客户端玩家是否造成了伤害
    /// </summary>
    /// <param name="playerName"></param>
    /// <param name="projDamaged"></param>
    /// <param name="itemDamaged"></param>
    public static void SendDamagedToServer()
    {
        if (Main.dedServ) return;

        ModPacket packet = ThisMod.GetPacket();
        packet.Write((byte)NetMessageType.PlayerDamaged);
        packet.Write(Main.LocalPlayer.name);
        packet.Write(ProjDamaged);
        packet.Write(ItemDamaged);
        
        // KiLogger.LogOnMutiMode($"发送 - 玩家[{Main.LocalPlayer.name}]造成了伤害, 投射物伤害：{ProjDamaged}, 物品伤害：{ItemDamaged}", packet: packet);
        packet.Send();

    }

    /// <summary>
    /// 发送本地玩家所有伤害数据到服务端
    /// </summary>
    public static void SendAllDamageDataToServer()
    {
        KiLogger.LogOnMutiMode("开始发送伤害数据", logToFile: true);
        if (!ItemDamaged && !ProjDamaged) return;

        ModPacket packet = ThisMod.GetPacket();
        packet.Write((byte)NetMessageType.AllDamageData);
        packet.Write(Main.LocalPlayer.name);

        if (ProjDamaged)
        {
            packet.Write(ProjToTotalDamage.Count);
            foreach (var pair in ProjToTotalDamage)
            {
                packet.Write(pair.Key);
                packet.Write(pair.Value);
            }
        }

        if (ItemDamaged)
        {
            packet.Write(ItemToTotalDamage.Count);
            foreach (var pair in ItemToTotalDamage)
            {
                packet.Write(pair.Key);
                packet.Write(pair.Value);
            }
        }

        // KiLogger.LogOnMutiMode("发送 - 结束发送伤害数据", packet: packet);
        packet.Send();
    }
    #endregion
}