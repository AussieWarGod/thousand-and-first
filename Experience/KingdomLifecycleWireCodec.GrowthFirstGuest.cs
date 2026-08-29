using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static void WriteGrowthFirstGuest(BinaryWriter w,
			KingdomGrowthFirstGuestOpportunity x, int wireVersion)
		{
			w.Write(x != null);
			if (x == null) return;
			if (wireVersion < KingdomLifecycleRules.FirstGuestPhysicalGrowthFormatVersion
				&& x.RulesVersion != 1)
				throw new InvalidDataException(
					"historical growth cannot encode physical first-guest rules");
			w.Write(x.RulesVersion); S(w, x.OpportunityId, true); S(w, x.CauseId, true);
			w.Write(x.CauseTick); w.Write(x.OfferedTick); w.Write(x.CadenceTicks);
			w.Write((byte)x.FactsState); w.Write(x.CohortSize);
			w.Write(x.PopulationBefore); w.Write(x.PopulationCap);
			w.Write(x.SupportedLevel); w.Write(x.SupportCap);
			w.Write(x.WaterAvailable); w.Write(x.WaterRequired);
			w.Write((byte)x.ChoiceState); w.Write(x.DeferredTick);
			S(w, x.DeferredReceiptId, true); w.Write(x.DecisionTick);
			S(w, x.DecisionReceiptId, true); S(w, x.BodyReservationId, true);
			S(w, x.BodyRealmId, true);
			w.Write((byte)x.BodyOptionKind); w.Write(x.BodyEnableEpoch);
			w.Write(x.BodyReservedTick); w.Write((byte)x.BodyLeaseState);
			if (wireVersion >= KingdomLifecycleRules.FirstGuestPhysicalGrowthFormatVersion)
			{
				w.Write((byte)x.GuestPhase); w.Write((byte)x.GuestTerminalState);
				w.Write(x.GuestActionTick); S(w, x.GuestActionReceiptId, true);
				w.Write(x.GuestTerminalTick); S(w, x.GuestTerminalReceiptId, true);
			}
		}

		private static KingdomGrowthFirstGuestOpportunity ReadGrowthFirstGuest(BinaryReader r,
			int wireVersion)
		{
			if (!ReadExactBoolean(r)) return null;
			KingdomGrowthFirstGuestOpportunity x = new KingdomGrowthFirstGuestOpportunity
			{
				RulesVersion = r.ReadInt32(), OpportunityId = S(r, true), CauseId = S(r, true),
				CauseTick = r.ReadInt64(), OfferedTick = r.ReadInt64(),
				CadenceTicks = r.ReadInt64(),
				FactsState = (KingdomGrowthFirstGuestFactsState)r.ReadByte(),
				CohortSize = r.ReadInt32(), PopulationBefore = r.ReadInt32(),
				PopulationCap = r.ReadInt32(), SupportedLevel = r.ReadInt32(),
				SupportCap = r.ReadInt32(), WaterAvailable = r.ReadInt32(),
				WaterRequired = r.ReadInt32(),
				ChoiceState = (KingdomGrowthFirstGuestChoiceState)r.ReadByte(),
				DeferredTick = r.ReadInt64(), DeferredReceiptId = S(r, true),
				DecisionTick = r.ReadInt64(), DecisionReceiptId = S(r, true),
				BodyReservationId = S(r, true),
				BodyRealmId = S(r, true),
				BodyOptionKind = (KingdomExperienceOptionKind)r.ReadByte(),
				BodyEnableEpoch = r.ReadInt64(), BodyReservedTick = r.ReadInt64(),
				BodyLeaseState = (KingdomGrowthFirstGuestBodyLeaseState)r.ReadByte()
			};
			if (wireVersion >= KingdomLifecycleRules.FirstGuestPhysicalGrowthFormatVersion)
			{
				x.GuestPhase = (KingdomGrowthFirstGuestGuestPhase)r.ReadByte();
				x.GuestTerminalState = (KingdomGrowthFirstGuestTerminalState)r.ReadByte();
				x.GuestActionTick = r.ReadInt64(); x.GuestActionReceiptId = S(r, true);
				x.GuestTerminalTick = r.ReadInt64(); x.GuestTerminalReceiptId = S(r, true);
			}
			else if (x.RulesVersion != 1)
				throw new InvalidDataException(
					"historical growth carried physical first-guest rules");
			return x;
		}

		private static void WriteGrowthFirstGuestTerminal(BinaryWriter w,
			KingdomGrowthFirstGuestTerminalReceipt x, int wireVersion)
		{
			w.Write(x != null); if (x == null) return;
			w.Write(wireVersion < KingdomLifecycleRules.FirstGuestPhysicalGrowthFormatVersion
				? KingdomGrowthFirstGuestTerminalReceipt.LegacyVersion : x.Version);
			S(w, x.ReceiptId, true); S(w, x.SettlementId, true);
			S(w, x.CandidateId, true); S(w, x.CandidateObjectId, true);
			S(w, x.Blueprint, false); S(w, x.PersonName, false);
			S(w, x.PersonOrigin, false); S(w, x.PersonCreed, false);
			w.Write(x.ResidentId); w.Write((byte)x.Result);
			S(w, x.ArrivalOperationId, true); S(w, x.ArrivalOutboxEventId, true);
			w.Write(x.TerminalTick); WriteGrowthFirstGuest(w, x.Opportunity, wireVersion);
		}

		private static KingdomGrowthFirstGuestTerminalReceipt
			ReadGrowthFirstGuestTerminal(BinaryReader r, int wireVersion)
		{
			if (!ReadExactBoolean(r)) return null;
			KingdomGrowthFirstGuestTerminalReceipt result =
				new KingdomGrowthFirstGuestTerminalReceipt
			{
				Version = r.ReadInt32(), ReceiptId = S(r, true),
				SettlementId = S(r, true), CandidateId = S(r, true),
				CandidateObjectId = S(r, true), Blueprint = S(r, false),
				PersonName = S(r, false), PersonOrigin = S(r, false),
				PersonCreed = S(r, false), ResidentId = r.ReadInt32(),
				Result = (KingdomGrowthArrivalDisposition)r.ReadByte(),
				ArrivalOperationId = S(r, true), ArrivalOutboxEventId = S(r, true),
				TerminalTick = r.ReadInt64(), Opportunity = ReadGrowthFirstGuest(r, wireVersion)
			};
			if (wireVersion < KingdomLifecycleRules.FirstGuestPhysicalGrowthFormatVersion
				&& result.Version != KingdomGrowthFirstGuestTerminalReceipt.LegacyVersion)
				throw new InvalidDataException(
					"historical growth carried a physical first-guest terminal version");
			if (wireVersion < KingdomLifecycleRules.FirstGuestPhysicalGrowthFormatVersion)
				result.Version = KingdomGrowthFirstGuestTerminalReceipt.CurrentVersion;
			return result;
		}
	}
}
