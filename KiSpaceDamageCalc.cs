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
            ServerData = 0,
			StartRecording = 1,
			EndRecording = 2,
			PlayerDamaged = 3,
			AllDamageData = 4,
			ClientLanguage = 5
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
			NetMessageType msgType = (NetMessageType)reader.ReadByte();
			switch (msgType)
			{
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
