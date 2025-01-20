using System;
using System.IO;
using KiSpaceDamageCalc.Systems;
using Terraria.ModLoader;

namespace KiSpaceDamageCalc
{
	public class KiSpaceDamageCalc : Mod
	{
		public static string ModRoot => AppContext.BaseDirectory;
        internal enum NetMessageType : byte
        {
            DamageCalc = 0,
            ServerData = 1,
			StartRecording = 2,
			EndRecording = 3,
			PlayerDamaged = 4,
			AllDamageData = 5,
			ClientLanguage = 6
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
			NetMessageType msgType = (NetMessageType)reader.ReadByte();
			switch (msgType)
			{
				case NetMessageType.DamageCalc:
					DamageCalcServer.ReceiveTotalDamageFromClient(reader);
					break;
				case NetMessageType.ServerData:
					MainSystem.HandelServerData(msgType, reader, false, out _);
					break;
				case NetMessageType.StartRecording:
					DamageCalcClient.ReceiveStartFromServer();
					break;
				case NetMessageType.EndRecording:
					DamageCalcClient.ReceiveEndFromServer();
					break;
				case NetMessageType.PlayerDamaged:
					DamageCalcServer.ReceivePlayerDamaged(reader);
					break;
				case NetMessageType.AllDamageData:
					DamageCalcServer.ReceiveAllDamageDataFromClient(reader);
					break;
				case NetMessageType.ClientLanguage:
					DamageCalcServer.ReceiveClinetLanguageFromServer(reader);
					break;
			}
        }
	}
}
