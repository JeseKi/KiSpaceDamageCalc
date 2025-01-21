using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KiSpaceDamageCalc.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static KiSpaceDamageCalc.Common;
using static KiSpaceDamageCalc.KiSpaceDamageCalc;

namespace KiSpaceDamageCalc;

public static class DamageCalcServer{
    public static int AllPlayersTotalDamage = 0;
    public static bool started = false;
    public static DateTime StartTime;
    public static TimeSpan battleDuration;
    public static int EndTime = 0;
    public const int MAX_WAIT_TIME = 60;
    public static bool InReseting = false;
    public static string CurrentLanguage;
    public static List<string> PlayerCurrentLanguages = new List<string>();
    /// <summary>
    /// 各玩家造成的总伤害
    /// </summary>
    public static Dictionary<string, int> PlayerToTotalDamage = new Dictionary<string, int>();
    /// <summary>
    /// 造成过伤害的玩家
    /// </summary>
    public static List<string> PlayersDamaged = new List<string>();
    /// <summary>
    /// 各玩家造成了伤害后，结束战斗时是否收到了该玩家的全部伤害数据
    /// </summary>
    public static List<string> PlayersAllDamageDataReceived = new List<string>();
    /// <summary>
    /// 各玩家造成的伤害具体信息
    /// </summary>
    public static Dictionary<string, Dictionary<string, int>> PlayerToDamagesourceDamages = new Dictionary<string, Dictionary<string, int>>();
    public static void Start(){
        if (started || !Main.dedServ)
            return;

        started = true;
        StartTime = DateTime.Now;
        SendStartToClient();
    }

    public static void StartReset()
    {
        if (InReseting || !Main.dedServ) return; 
        battleDuration = DateTime.Now - StartTime;

        InReseting = true;
        EndTime = MainSystem.ServerTick;
        SendEndToClient();
    }

    public static void UpdateReset()
    {
        if (!InReseting || !Main.dedServ) return;

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
        StartTime = DateTime.Now;
        EndTime = 0;
        battleDuration = TimeSpan.Zero;
        started = false;
        PlayerCurrentLanguages.Clear();
        
        PlayerToTotalDamage.Clear();

        PlayersDamaged.Clear();
        PlayersAllDamageDataReceived.Clear();
        PlayerToDamagesourceDamages.Clear();
    }

    public static bool NeedBroadcast(){
        if (MainSystem.ServerTick - EndTime > MAX_WAIT_TIME) return true;
        
        if (PlayersDamaged.FindIndex(p => !PlayersAllDamageDataReceived.Contains(p)) != -1) return false;
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
        var playerTotalDamages = PlayerToTotalDamage
            .OrderByDescending(p => p.Value)
            .ToList();
        
        string language = DecideLanguage();
        if (language != LanguageManager.Instance.ActiveCulture.Name)
            LanguageManager.Instance.SetLanguage(language);
        
        KiLogger.LogOnMutiMode($"========== {GetKiSpaceDamageCalcText("DamageStatistics")} ==========",Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
        KiLogger.LogOnMutiMode($"{GetKiSpaceDamageCalcText("TotalTeamDamage")}: {AllPlayersTotalDamage}", Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
        KiLogger.LogOnMutiMode($"{GetKiSpaceDamageCalcText("TimeText")}: {FormatBattleTime(battleDuration)}", Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
        KiLogger.LogOnMutiMode($"{GetKiSpaceDamageCalcText("TeamAverageDPS")}: {(int)(AllPlayersTotalDamage / battleDuration.TotalSeconds)}", Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
        
        for (int i = 0; i < playerTotalDamages.Count; i++)
        {
            var player = playerTotalDamages[i];
            float damagePercentage = (float)player.Value / AllPlayersTotalDamage * 100;
            Color rankColor = GetPlayerRankColor(i, playerTotalDamages.Count);
            
            string playerRank = $"{player.Key}: {player.Value} | {damagePercentage:F1}%";
            KiLogger.LogOnMutiMode(playerRank, color: rankColor, logCodePosition: false, logServerTick: false, logPlatform: false);
            KiLogger.LogOnMutiMode($"{GetKiSpaceDamageCalcText("PlayerAverageDPS", player.Key)}: {(int)(player.Value / battleDuration.TotalSeconds)}", rankColor, logCodePosition: false, logServerTick: false, logPlatform: false);
            
            if (PlayerToDamagesourceDamages.TryGetValue(player.Key, out var itemDamages))
            {
                var sortedDamages = itemDamages
                    .OrderByDescending(x => x.Value)
                    .ToList();
                
                    int maxNameWidth = sortedDamages.Max(x => GetDisplayLength(x.Key));
                    int maxDamageLength = sortedDamages.Max(x => x.Value.ToString().Length);

                    for (int j = 0; j < sortedDamages.Count; j++)
                    {
                        var item = sortedDamages[j];
                        float itemPercentage = (float)item.Value / player.Value * 100;
                        string rank = $"#{j + 1}".PadRight(3);
                        string name = PadRightWidth(item.Key, maxNameWidth);
                        string damage = item.Value.ToString().PadLeft(maxDamageLength);
                        
                        KiLogger.LogOnMutiMode(
                            $"{rank} {name}: {damage} | {itemPercentage:F1}%", 
                            rankColor, 
                            logCodePosition: false, 
                            logServerTick: false, 
                            logPlatform: false
                        );
                    }
            }
        }
        KiLogger.LogOnMutiMode("==========================",Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
    }

    private static string DecideLanguage()
    {
        if (PlayerCurrentLanguages.Count == 0)
            return "en-US";
            
        if (PlayerCurrentLanguages.Count == 1)
            return PlayerCurrentLanguages[0];
            
        var languageCounts = PlayerCurrentLanguages
            .GroupBy(x => x)
            .Select(g => new { Language = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();
            
        int maxCount = languageCounts[0].Count;
        
        var mostUsedLanguages = languageCounts
            .Where(x => x.Count == maxCount)
            .Select(x => x.Language)
            .ToList();
            
        if (mostUsedLanguages.Count == 1)
            return mostUsedLanguages[0];
            
        if (mostUsedLanguages.Any(lang => lang.StartsWith("en")))
            return mostUsedLanguages.First(lang => lang.StartsWith("en"));
            
        return mostUsedLanguages.OrderByDescending(x => x).First();
    }
    
    #region 同步

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
        bool itemDamaged = reader.ReadBoolean();

        if (itemDamaged && !PlayersDamaged.Contains(playerName))
            PlayersDamaged.Add(playerName);
    }

    /// <summary>
    /// 接收记录玩家造成的伤害
    /// </summary>
    /// <param name="reader"></param>
    public static void ReceiveAllDamageDataFromClient(BinaryReader reader)
    {
        string playerName = reader.ReadString();
        PlayersAllDamageDataReceived.Add(playerName);

        if (PlayersDamaged.Contains(playerName))
        {
            PlayerToTotalDamage[playerName] = reader.ReadInt32();
            PlayerToDamagesourceDamages[playerName] = new Dictionary<string, int>();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string itemName = reader.ReadString();
                int damage = reader.ReadInt32();
                PlayerToDamagesourceDamages[playerName][itemName] = damage;
            }
        }
    }

    /// <summary>
    /// 接收客户端语言
    /// </summary>
    /// <param name="reader"></param>
    public static void ReceiveClinetLanguageFromServer(BinaryReader reader)
    {
        string language = reader.ReadString();
        PlayerCurrentLanguages.Add(language);
    }
    #endregion
}

public static class DamageCalcClient
{
    public static int LocalTotalDamage = 0;
    public static Dictionary<string, int> DamagesourceToTotalDamage = new Dictionary<string, int>();
    public static bool oldDamaged = false;
    public static bool Damaged = false;
    public static bool started = false;

    public static void AddDamage(int damage){
        if (!started || Main.dedServ)
            return;

        LocalTotalDamage += damage;
    }

    public static void AddDamage(int damage, Projectile proj){
        if (!started || Main.dedServ)
            return;

        if (!DamagesourceToTotalDamage.ContainsKey(proj.Name))
            DamagesourceToTotalDamage[proj.Name] = 0;
        
        DamagesourceToTotalDamage[proj.Name] += damage;
        
        Damaged = true;
        AddDamage(damage);
    }

    public static void AddDamage(int damage, Item item){
        if (!started || Main.dedServ)
            return;

        if (!DamagesourceToTotalDamage.ContainsKey(item.Name))
            DamagesourceToTotalDamage[item.Name] = 0;
        
        DamagesourceToTotalDamage[item.Name] += damage;
        
        Damaged = true;
        AddDamage(damage);
    }

    public static void Start(){
        if (started || Main.dedServ)
            return;

        started = true;
    }

    public static void Reset(){
        if (Main.dedServ) return;

        SendAllDamageDataToServer();
        SendClinetLanguageToServer();

        started = false;
        LocalTotalDamage = 0;
        DamagesourceToTotalDamage.Clear();
        oldDamaged = false;
        Damaged = false;
        return;
    }

    public static void CheckDamaged(){
        if (Main.dedServ || !started) return;
        
        if (oldDamaged != Damaged)
        {
            SendDamagedToServer();
            oldDamaged = Damaged;
        }
    }
    
    #region 同步
    /// <summary>
    /// 使客户端开始记录，避免某些情况下客户端应当开始记录但未开始记录的情况
    /// </summary>
    public static void ReceiveStartFromServer()
    {
        if (Main.dedServ) return;
        Start();
    }

    /// <summary>
    /// 客户端接收结束记录，避免某些情况下客户端未结束记录的情况
    /// </summary>
    public static void ReceiveEndFromServer()
    {
        if (Main.dedServ) return;
        Reset();
    }

    /// <summary>
    /// 发送客户端玩家是否造成了伤害
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
        packet.Write(Damaged);
        
        packet.Send();

    }

    /// <summary>
    /// 发送本地玩家所有伤害数据到服务端
    /// </summary>
    public static void SendAllDamageDataToServer()
    {
        if (!Damaged || Main.dedServ) return;

        ModPacket packet = ThisMod.GetPacket();
        packet.Write((byte)NetMessageType.AllDamageData);
        packet.Write(Main.LocalPlayer.name);

        if (Damaged)
        {
            packet.Write(LocalTotalDamage);
            packet.Write(DamagesourceToTotalDamage.Count);
            foreach (var pair in DamagesourceToTotalDamage)
            {
                packet.Write(pair.Key);
                packet.Write(pair.Value);
            }
        }

        packet.Send();
    }

    /// <summary>
    /// 向服务端发送本地玩家的语言设置
    /// </summary>
    public static void SendClinetLanguageToServer()
    {
        if (Main.dedServ) return;

        ModPacket packet = ThisMod.GetPacket();
        packet.Write((byte)NetMessageType.ClientLanguage);
        packet.Write(LanguageManager.Instance.ActiveCulture.Name);
        packet.Send();
    }
    #endregion
}
#region 单人的伤害统计
public static class DamageCalcSinglePlayer
{
    public static int TotalDamage = 0;
    public static bool started = false;
    public static DateTime StartTime;
    public static TimeSpan battleDuration;
    /// <summary>
    /// 伤害的具体信息
    /// </summary>
    public static Dictionary<string, int> DamagesourceDamages = new Dictionary<string, int>();
    public static void Start(){
        if (started || !Main.netMode.Equals(KiNetmodeID.SinglePlayer))
            return;

        started = true;
        StartTime = DateTime.Now;
    }
    public static void AddDamage(int damage){
        if (!started || Main.dedServ)
            return;

        TotalDamage += damage;
    }

    public static void AddDamage(int damage, Projectile proj){
        if (!started || Main.dedServ)
            return;

        if (!DamagesourceDamages.ContainsKey(proj.Name))
            DamagesourceDamages[proj.Name] = 0;
        
        DamagesourceDamages[proj.Name] += damage;
        
        AddDamage(damage);
    }

    public static void AddDamage(int damage, Item item){
        if (!started || Main.dedServ)
            return;

        if (!DamagesourceDamages.ContainsKey(item.Name))
            DamagesourceDamages[item.Name] = 0;
        
        DamagesourceDamages[item.Name] += damage;
        
        AddDamage(damage);
    }
    public static void Reset(){

        if (!Main.netMode.Equals(KiNetmodeID.SinglePlayer) || !started) return;
        battleDuration = DateTime.Now - StartTime;
        DisplayDamageStatistics();
        
        TotalDamage = 0;
        StartTime = DateTime.Now;
        battleDuration = TimeSpan.Zero;
        started = false;
        DamagesourceDamages.Clear();
    }

    private static void DisplayDamageStatistics()
    {        
        KiLogger.LogOnMutiMode($"========== {GetKiSpaceDamageCalcText("DamageStatistics")} ==========",Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
        KiLogger.LogOnMutiMode($"{GetKiSpaceDamageCalcText("TotalTeamDamage")}: {TotalDamage}", Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
        KiLogger.LogOnMutiMode($"{GetKiSpaceDamageCalcText("TimeText")}: {FormatBattleTime(battleDuration)}", Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
        KiLogger.LogOnMutiMode($"{GetKiSpaceDamageCalcText("PlayerAverageDPS", Main.LocalPlayer.name)}: {(int)(TotalDamage / battleDuration.TotalSeconds)}", Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
        
        var sortedDamages = DamagesourceDamages
            .OrderByDescending(x => x.Value)
            .ToList();
        
        int maxNameWidth = sortedDamages.Max(x => GetDisplayLength(x.Key));
        int maxDamageLength = sortedDamages.Max(x => x.Value.ToString().Length);

        for (int j = 0; j < sortedDamages.Count; j++)
        {
            var item = sortedDamages[j];
            float itemPercentage = (float)item.Value / TotalDamage * 100;
            string rank = $"#{(j + 1)}".PadRight(3);
            string name = PadRightWidth(item.Key, maxNameWidth);
            string damage = item.Value.ToString().PadLeft(maxDamageLength);
            
            KiLogger.LogOnMutiMode(
                $"{rank} {name}: {damage} | {itemPercentage:F1}%", 
                Color.Red, 
                logCodePosition: false, 
                logServerTick: false, 
                logPlatform: false
            );
        }
        KiLogger.LogOnMutiMode("==========================",Color.Purple, logCodePosition: false, logServerTick: false, logPlatform: false);
    }
}
#endregion