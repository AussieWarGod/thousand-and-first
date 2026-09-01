using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static void WriteLodgeTerminal(BinaryWriter w, KingdomLifecycleOperation op,
			KingdomLifecycleLodgeTerminalReceipt r, int wireVersion)
		{
			w.Write(r != null);
			if (r == null) return;
			w.Write(r.Version); S(w, r.OperationId, true); S(w, r.ReceiptId, true);
			S(w, r.PlanHash, true); S(w, r.SettlementId, true); S(w, r.ObjectId, true);
			S(w, r.Blueprint, false); w.Write(r.ResidentId); S(w, r.ResidentName, false);
			S(w, r.ResidentOrigin, false); S(w, r.ResidentArrived, false);
			w.Write(r.ResidentArrivedTick); S(w, r.ResidentBoundZoneId, false);
			if (wireVersion >= KingdomLifecycleRules.LodgeMarketSourceLifecycleFormatVersion)
			{
				w.Write(r.MarketSourcePrepared); S(w, r.MarketSourceBodyObjectId, false);
				w.Write(r.MarketSourceResidentId); w.Write(r.MarketTier);
				S(w, r.MarketIntent, false); S(w, r.MarketSourceProofId, true);
			}
			w.Write((byte)r.State); w.Write(r.DeathCause); w.Write(r.TerminalTick);
			S(w, r.SourceProofId, true);
			S(w, wireVersion == KingdomLifecycleRules.LodgeTerminalLifecycleFormatVersion
				&& r.DeathProofId != null
				? KingdomLifecycleRules.LodgeDeathProofForV9Wire(op, r) : r.DeathProofId, true);
		}

		private static KingdomLifecycleLodgeTerminalReceipt ReadLodgeTerminal(BinaryReader r,
			int wireVersion)
		{
			if (!ReadExactBoolean(r)) return null;
			KingdomLifecycleLodgeTerminalReceipt value = new KingdomLifecycleLodgeTerminalReceipt
			{
				Version = r.ReadInt32(), OperationId = S(r, true), ReceiptId = S(r, true),
				PlanHash = S(r, true), SettlementId = S(r, true), ObjectId = S(r, true),
				Blueprint = S(r, false), ResidentId = r.ReadInt32(), ResidentName = S(r, false),
				ResidentOrigin = S(r, false), ResidentArrived = S(r, false),
				ResidentArrivedTick = r.ReadInt64(), ResidentBoundZoneId = S(r, false)
			};
			if (wireVersion >= KingdomLifecycleRules.LodgeMarketSourceLifecycleFormatVersion)
			{
				value.MarketSourcePrepared = r.ReadInt32();
				value.MarketSourceBodyObjectId = S(r, false);
				value.MarketSourceResidentId = r.ReadInt32(); value.MarketTier = r.ReadInt32();
				value.MarketIntent = S(r, false); value.MarketSourceProofId = S(r, true);
			}
			value.State = (KingdomLifecycleLodgeTerminalState)r.ReadByte();
			value.DeathCause = r.ReadByte(); value.TerminalTick = r.ReadInt64();
			value.SourceProofId = S(r, true); value.DeathProofId = S(r, true);
			return value;
		}
	}
}
