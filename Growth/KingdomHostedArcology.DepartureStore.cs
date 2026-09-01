using System;
using XRL;
using XRL.World;
using XRL.World.Parts;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	/// <summary>Four bounded save slots: two authority incarnations by two paid floors.</summary>
	public static partial class KingdomHostedArcology
	{
		private static readonly string[] DepartureSlotKeys = new string[] {
			"r_TAF_HostedDepartureV1:0:ward", "r_TAF_HostedDepartureV1:0:terrace",
			"r_TAF_HostedDepartureV1:1:ward", "r_TAF_HostedDepartureV1:1:terrace"
		};

		private static bool TryFenceDeparture(KingdomSystem System, InteriorZone Interior,
			out KingdomHostedDepartureState State, out string Failure)
		{
			State = null; Failure = null;
			string lot = Interior == null ? "" : KingdomHostedArcologyTopology.HostedLotAt(
				Interior.X, Interior.Y, Interior.Z);
			if (!TryCurrentAuthoritySlot(System, out int slot,
				out KingdomHostedArcologyAuthority authority, out Failure)) return false;
			if (authority.Phase != KingdomHostedAuthorityPhase.Active)
				return DepartureStoreFail("hosted departure authority is not active", out Failure);
			if (string.IsNullOrEmpty(lot))
			{
				FenceExistingProjection(slot, authority,
					KingdomHostedArcologyTopology.WardLotKey, out string ignored);
				FenceExistingProjection(slot, authority,
					KingdomHostedArcologyTopology.TerraceLotKey, out ignored);
				return DepartureStoreFail(
					"hosted departure has no exact paid-floor coordinate", out Failure);
			}
			if (!FenceExistingProjection(slot, authority, lot, out Failure)) return false;
			if (Interior.Instance != authority.CarrierId
				|| !TryCanonicalInterior(Interior, authority.CarrierId, out Failure))
				return DepartureStoreFail(Failure
					?? "hosted departure does not match the active authority", out Failure);
			if (!TryReadDeparture(slot, lot, out State, out Failure)) return false;
			if (State != null) return State.Phase == KingdomHostedDeparturePhase.Pending;
			State = NewDepartureState(slot, authority, lot, Interior.ZoneID,
				KingdomHostedDeparturePhase.Pending, "", 0, 0, 0, false,
				KingdomReach.BandOf(ArcologyKey), false);
			return WriteDeparture(State, out Failure);
		}

		private static bool FenceExistingProjection(int Slot,
			KingdomHostedArcologyAuthority Authority, string LotKey, out string Failure)
		{
			Failure = null;
			if (!TryReadDeparture(Slot, LotKey,
				out KingdomHostedDepartureState prior, out Failure)) return false;
			if (!KingdomHostedDepartureRules.Matches(prior, Slot, Authority, LotKey))
				return ClearHostedProjectionSlot(Slot, LotKey, out Failure);
			KingdomHostedDepartureState pending = prior.Copy();
			pending.Phase = KingdomHostedDeparturePhase.Pending;
			pending.ReceiptRevision = ""; pending.Roof = 0;
			pending.Luxury = 0; pending.Food = 0;
			pending.ObservedTick = Math.Max(0L, The.Game?.TimeTicks ?? 0L);
			return WriteDeparture(pending, out Failure);
		}

		private static bool DepartureAllows(KingdomSystem System, GameObject Work,
			string LotKey, string ReceiptRevision, out string Failure)
		{
			Failure = null;
			if (!TryCurrentAuthoritySlot(System, out int slot,
				out KingdomHostedArcologyAuthority authority, out Failure)) return false;
			if (!GameObject.Validate(Work) || Work.IDIfAssigned != authority.CarrierId
				|| Work.CurrentZone?.ZoneID != authority.ZoneId)
				return DepartureStoreFail("hosted departure authority no longer names this shell",
					out Failure);
			if (!TryReadDeparture(slot, LotKey, out KingdomHostedDepartureState state,
				out Failure)) return false;
			if (!KingdomHostedDepartureRules.Matches(state, slot, authority, LotKey))
				return DepartureStoreFail(
					"hosted departure snapshot is absent or names another authority", out Failure);
			if (state.Phase == KingdomHostedDeparturePhase.Pending)
				return DepartureStoreFail("final hosted suspension observation is pending", out Failure);
			return state.ReceiptRevision == ReceiptRevision || DepartureStoreFail(
				"hosted departure snapshot names another receipt revision", out Failure);
		}

		private static bool PersistFinalDeparture(KingdomSystem System,
			KingdomHostedDepartureEnvelope Envelope, KingdomHostedObservation Observation)
		{
			if (Envelope == null || Observation == null
				|| !PersistObserved(Envelope.Root, Observation)) return false;
			if (!TryCurrentAuthoritySlot(System, out int slot,
				out KingdomHostedArcologyAuthority authority, out string failure)
				|| !TryReadDeparture(slot, Envelope.LotKey,
					out KingdomHostedDepartureState pending, out failure)
				|| !KingdomHostedDepartureRules.Matches(
					pending, slot, authority, Envelope.LotKey)
				|| pending.Phase != KingdomHostedDeparturePhase.Pending)
			{
				Quarantine(Envelope.Root, failure ?? "hosted departure fence is not exact");
				return false;
			}
			KingdomHostedObservation proved;
			if (!TryObservation(Envelope.Root, Envelope.LotKey, out proved, out failure)
				|| KingdomHostedArcologyReceiptCodec.EncodeObservation(proved)
					!= KingdomHostedArcologyReceiptCodec.EncodeObservation(Observation))
			{
				Quarantine(Envelope.Root, failure ?? "final hosted observation CAS was not exact");
				return false;
			}
			int effectiveness = KingdomWear.EffectivenessOf(Envelope.Shell);
			bool sound = string.IsNullOrEmpty(Observation.Fault);
			int roof = sound ? KingdomCatalogueRules.Carried(
				Observation.Roof, effectiveness) : 0;
			int luxury = sound ? KingdomReachRules.Scaled(
				Observation.Luxury, effectiveness) : 0;
			int food = sound && pending.FreshWater ? KingdomCatalogueRules.Carried(
				Observation.Food, effectiveness) : 0;
			KingdomHostedDepartureState settled = NewDepartureState(slot, authority,
				Envelope.LotKey, Envelope.Zone.ZoneID, KingdomHostedDeparturePhase.Settled,
				Envelope.Revision, roof, luxury, food, pending.FreshWater,
				KingdomReach.BandOf(ArcologyKey),
				KingdomReach.IsHeaded(Envelope.Shell));
			if (WriteDeparture(settled, out failure)) return true;
			Quarantine(Envelope.Root, failure); return false;
		}

		internal static bool RefreshHostedProjections(KingdomSystem System, GameObject Shell,
			KingdomBenefitReading Reading, bool FreshWater, out string Failure)
		{
			Failure = null;
			if (!IsOperationalPure(Shell) || Reading?.Designation == null
				|| Reading.Designation.RootId != Shell.IDIfAssigned)
			{
				if (TryCurrentAuthoritySlot(System, out int invalidSlot,
					out KingdomHostedArcologyAuthority invalidAuthority, out string ignored)
					&& invalidAuthority.ZoneId == Shell?.CurrentZone?.ZoneID
					&& invalidAuthority.CarrierId == Shell?.IDIfAssigned)
					ClearHostedProjectionSlots(invalidSlot, out ignored);
				return DepartureStoreFail("hosted reach overlay lacks exact live evidence", out Failure);
			}
			if (!TryCurrentAuthoritySlot(System, out int slot,
				out KingdomHostedArcologyAuthority authority, out Failure)) return false;
			if (authority.Phase != KingdomHostedAuthorityPhase.Active)
				return DepartureStoreFail("hosted reach authority is not active", out Failure);
			return RefreshHostedLot(slot, authority, Shell, Reading,
				KingdomHostedArcologyTopology.WardLotKey, FreshWater, out Failure)
				&& RefreshHostedLot(slot, authority, Shell, Reading,
					KingdomHostedArcologyTopology.TerraceLotKey, FreshWater, out Failure);
		}

		private static bool RefreshHostedLot(int Slot, KingdomHostedArcologyAuthority Authority,
			GameObject Shell, KingdomBenefitReading Reading, string LotKey, bool FreshWater,
			out string Failure)
		{
			Failure = null;
			if (!TryReadDeparture(Slot, LotKey,
				out KingdomHostedDepartureState current, out Failure)) return false;
			if (KingdomHostedDepartureRules.Matches(current, Slot, Authority, LotKey)
				&& current.Phase == KingdomHostedDeparturePhase.Pending) return true;
			r_KingdomArcology root = Shell.GetPart<r_KingdomArcology>();
			if (!TryReceipt(root, LotKey, out KingdomHostedLotReceipt receipt, out Failure))
			{
				ClearHostedProjectionSlot(Slot, LotKey, out string ignored);
				Quarantine(root, Failure); return false;
			}
			if (receipt == null || receipt.Phase != KingdomHostedLotPhase.Active)
				return ClearHostedProjectionSlot(Slot, LotKey, out Failure);
			int roof = 0, luxury = 0, food = 0;
			if (LotKey == KingdomHostedArcologyTopology.WardLotKey)
			{
				if (!TryWardPhysical(Shell, out roof, out luxury, out Failure))
				{
					ClearHostedProjectionSlot(Slot, LotKey, out string ignored);
					Quarantine(root, Failure); return false;
				}
			}
			else if (!TryTerracePhysicalFood(Shell, FreshWater, out food, out Failure))
			{
				ClearHostedProjectionSlot(Slot, LotKey, out string ignored);
				Quarantine(root, Failure); return false;
			}
			int effectiveness = KingdomWear.EffectivenessOf(Shell);
			KingdomHostedDepartureState settled = NewDepartureState(Slot, Authority, LotKey,
				CanonicalInteriorId(Shell, LotKey), KingdomHostedDeparturePhase.Settled,
				KingdomHostedArcologyRules.ReceiptRevision(receipt),
				KingdomCatalogueRules.Carried(roof, effectiveness),
				KingdomReachRules.Scaled(luxury, effectiveness),
				KingdomCatalogueRules.Carried(food, effectiveness), FreshWater,
				KingdomReach.BandOf(Shell, Reading),
				KingdomOffices.Enabled && KingdomReach.IsHeaded(Shell));
			return settled.Valid() && WriteDeparture(settled, out Failure);
		}

		private static KingdomHostedDepartureState NewDepartureState(int Slot,
			KingdomHostedArcologyAuthority Authority, string LotKey, string InteriorZoneId,
			KingdomHostedDeparturePhase Phase, string Revision, int Roof, int Luxury,
			int Food, bool FreshWater,
			ReachBand Band, bool Headed)
		{
			return new KingdomHostedDepartureState { Phase = Phase, AuthoritySlot = Slot,
				RealmId = Authority.RealmId, SettlementId = Authority.SettlementId,
				ExteriorZoneId = Authority.ZoneId, CarrierId = Authority.CarrierId,
				AuthorityJobId = Authority.ConstructionJobId, LotKey = LotKey,
				InteriorZoneId = InteriorZoneId, ReceiptRevision = Revision,
				ObservedTick = Math.Max(0L, The.Game?.TimeTicks ?? 0L),
				Roof = Math.Max(0, Roof), Luxury = Math.Max(0, Luxury),
				Food = Math.Max(0, Food), FreshWater = FreshWater,
				Band = Band, Headed = Headed };
		}

		private static bool TryCurrentAuthoritySlot(KingdomSystem System, out int Slot,
			out KingdomHostedArcologyAuthority Authority, out string Failure)
		{
			Slot = -1; Authority = null; Failure = null;
			if (System == null || The.Game == null || string.IsNullOrEmpty(System.RealmId)
				|| !TryReadAuthoritySlots(out KingdomHostedArcologyAuthority first,
					out KingdomHostedArcologyAuthority second, out Failure)) return false;
			if (first != null && first.RealmId == System.RealmId) { Slot = 0; Authority = first; }
			if (second != null && second.RealmId == System.RealmId)
			{
				if (Authority != null) return DepartureStoreFail(
					"hosted departure authority is duplicated", out Failure);
				Slot = 1; Authority = second;
			}
			return Authority != null || DepartureStoreFail(
				"hosted departure authority is absent", out Failure);
		}

		private static bool TryReadDeparture(int Slot, string LotKey,
			out KingdomHostedDepartureState State, out string Failure)
		{
			State = null; Failure = null;
			int key = DepartureKeyIndex(Slot, LotKey);
			if (The.Game == null || key < 0)
				return DepartureStoreFail("hosted departure slot is invalid", out Failure);
			string encoded = The.Game.GetStringGameState(DepartureSlotKeys[key], "");
			return string.IsNullOrEmpty(encoded)
				|| KingdomHostedDepartureCodec.TryDecode(encoded, out State)
				|| DepartureStoreFail("hosted departure slot is unreadable", out Failure);
		}

		private static bool WriteDeparture(KingdomHostedDepartureState State,
			out string Failure)
		{
			Failure = null; int key = State == null ? -1
				: DepartureKeyIndex(State.AuthoritySlot, State.LotKey);
			string encoded = KingdomHostedDepartureCodec.Encode(State);
			if (The.Game == null || key < 0 || string.IsNullOrEmpty(encoded)
				|| !TryReadDeparture(State.AuthoritySlot, State.LotKey,
					out KingdomHostedDepartureState ignored, out Failure)) return false;
			The.Game.SetStringGameState(DepartureSlotKeys[key], encoded);
			return The.Game.GetStringGameState(DepartureSlotKeys[key], "") == encoded
				|| DepartureStoreFail("hosted departure slot did not persist exactly", out Failure);
		}

		private static int DepartureKeyIndex(int Slot, string LotKey)
		{
			int lot = LotKey == KingdomHostedArcologyTopology.WardLotKey ? 0
				: LotKey == KingdomHostedArcologyTopology.TerraceLotKey ? 1 : -1;
			return Slot < 0 || Slot > 1 || lot < 0 ? -1 : Slot * 2 + lot;
		}

		private static bool TryCanonicalInterior(InteriorZone Interior, string CarrierId,
			out string Failure)
		{
			Failure = null; string world, schema, instance; int wx, wy, x, y, z;
			string id = Interior?.ZoneID;
			return Interior != null && !string.IsNullOrEmpty(id)
				&& ZoneID.Parse(id, out world, out schema, out instance,
					out wx, out wy, out x, out y, out z) && world == "Interior"
				&& schema == KingdomHostedArcologyTopology.Schema && instance == CarrierId
				&& x == Interior.X && y == Interior.Y && z == Interior.Z
				&& id == ZoneID.Assemble(world + "@" + schema + "@" + instance,
					wx, wy, x, y, z) || DepartureStoreFail(
					"hosted departure interior identity is noncanonical", out Failure);
		}

		private static string CanonicalInteriorId(GameObject Shell, string LotKey)
		{
			return KingdomHostedArcologyTopology.TryHostedLotCoordinate(LotKey,
				out KingdomArcologyCoordinate at)
				&& TryNativeInteriorTarget(Shell, at.X, at.Y, at.Z,
					out string target, out string ignored) ? target : "";
		}

		private static bool DepartureStoreFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
