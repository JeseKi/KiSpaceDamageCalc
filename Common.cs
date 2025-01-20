using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace KiSpaceDamageCalc;

public static class Common
{
    public static class KiNetmodeID
    {
        public const int SinglePlayer = 0;
        public const int MultiplayerClient = 1;
        public const int Server = 2;
        public const int MultiplayerMode = 3;
        public const int Any = 4;
    }
    public static Mod ThisMod => ModLoader.GetMod("KiSpaceDamageCalc");
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

    public static void AddDamage(int damage, Projectile proj)
    {
        if (Main.netMode.Equals(KiNetmodeID.MultiplayerClient))
        {
            DamageCalcClient.AddDamage(damage, proj);
        }
        else if (Main.netMode.Equals(KiNetmodeID.SinglePlayer))
        {
            DamageCalcSinglePlayer.AddDamage(damage, proj);
        }
    }

    public static void AddDamage(int damage, Item item)
    {
        if (Main.netMode.Equals(KiNetmodeID.MultiplayerClient))
        {
            DamageCalcClient.AddDamage(damage, item);
        }
        else if (Main.netMode.Equals(KiNetmodeID.SinglePlayer))
        {
            DamageCalcSinglePlayer.AddDamage(damage, item);
        }
    }

    public static int GetDisplayLength(string text)
    {
        int length = 0;
        foreach (char c in text)
        {
            // 根据Unicode范围判断是否是全角字符
            // CJK统一汉字、全角标点符号等
            if ((c >= 0x4E00 && c <= 0x9FFF) ||   // CJK统一汉字
                (c >= 0x3000 && c <= 0x303F) ||   // CJK标点符号
                (c >= 0xFF00 && c <= 0xFFEF))     // 全角ASCII、全角标点
            {
                length += 2;
            }
            else
            {
                length += 1;
            }
        }
        return length;
    }

    public static string PadRightWidth(string text, int width)
    {
        int currentWidth = GetDisplayLength(text);
        return text + new string(' ', width - currentWidth);
    }
}