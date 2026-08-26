using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		internal static KingdomManifest LegacyManifestSnapshot(
			KingdomTradeManifestState Manifest)
		{
			if (Manifest == null) return null;
			return new KingdomManifest
			{
				OriginName = Manifest.OriginName,
				DestinationName = Manifest.DestinationName,
				Drams = Manifest.EscrowDrams,
				LoadedTick = Manifest.LoadedTick,
				DeadlineTick = Manifest.DeadlineTick,
				TurnedBack = Manifest.TurnedBack
			};
		}

		internal static KingdomManifest LegacyManifestSnapshot(KingdomManifest Manifest)
		{
			if (Manifest == null) return null;
			return new KingdomManifest
			{
				OriginName = Manifest.OriginName,
				DestinationName = Manifest.DestinationName,
				Drams = Manifest.Drams,
				LoadedTick = Manifest.LoadedTick,
				DeadlineTick = Manifest.DeadlineTick,
				TurnedBack = Manifest.TurnedBack
			};
		}

		internal static bool LegacyManifestMatches(KingdomManifest Legacy,
			KingdomTradeManifestState Authoritative)
		{
			if (Legacy == null || Authoritative == null)
				return Legacy == null && Authoritative == null;
			return string.Equals(Legacy.OriginName, Authoritative.OriginName,
				StringComparison.Ordinal)
				&& string.Equals(Legacy.DestinationName, Authoritative.DestinationName,
					StringComparison.Ordinal)
				&& Legacy.Drams == Authoritative.EscrowDrams
				&& Legacy.LoadedTick == Authoritative.LoadedTick
				&& Legacy.DeadlineTick == Authoritative.DeadlineTick
				&& Legacy.TurnedBack == Authoritative.TurnedBack;
		}

	}
}
