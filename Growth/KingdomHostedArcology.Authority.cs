using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Loaded-ground half of the one-per-realm arcology authority.</summary>
	public static partial class KingdomHostedArcology
	{
		public const string ArcologyKey = "arcology";
		private const int MaxReconciliationObjects = 16384;
		private static readonly string[] AuthoritySlotKeys = new string[] {
			"r_TAF_HostedArcologyAuthorityV1:0",
			"r_TAF_HostedArcologyAuthorityV1:1"
		};

		public static bool CanReserveAt(KingdomSystem System, string ZoneId, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded || string.IsNullOrEmpty(ZoneId)
				|| string.IsNullOrEmpty(System.RealmId)
				|| string.IsNullOrEmpty(System.SettlementIdForOwnedZone(ZoneId)))
			{
				Failure = "The hosted shell needs exact founded ground.";
				return false;
			}
			if (!KingdomCrown.CrownedOn(System, ZoneId))
			{
				Failure = "The hosted shell can close only around the capital's great court.";
				return false;
			}
			KingdomHostedArcologyAuthority authority;
			if (!TryReadAuthority(System, out authority, out Failure)) return false;
			if (authority != null)
			{
				if (authority.RealmId == System.RealmId
					&& authority.SettlementId == System.SettlementIdForOwnedZone(ZoneId)
					&& authority.ZoneId == ZoneId
					&& authority.Phase != KingdomHostedAuthorityPhase.Quarantined) return true;
				Failure = authority.Phase == KingdomHostedAuthorityPhase.Quarantined
					? "The realm's hosted-shell authority is quarantined for inspection."
					: "This realm already has a hosted arcology authority; its shell is not duplicated.";
				return false;
			}
			return true;
		}

		public static bool TryReserve(KingdomSystem System, Zone Z, GameObject Carrier,
			string JobId, out string Failure)
		{
			Failure = null;
			if (Z == null || !GameObject.Validate(Carrier) || Carrier.CurrentZone != Z
				|| string.IsNullOrEmpty(Carrier.IDIfAssigned)
				|| string.IsNullOrEmpty(JobId) || !CanReserveAt(System, Z.ZoneID, out Failure))
				return false;
			string settlement = System.SettlementIdForOwnedZone(Z.ZoneID);
			KingdomHostedArcologyAuthority existing;
			if (!TryReadAuthority(System, out existing, out Failure)) return false;
			KingdomHostedAuthorityAction action = KingdomHostedArcologyRules.AuthorityAction(
				existing, System.RealmId, settlement, Z.ZoneID, Carrier.IDIfAssigned);
			if (action == KingdomHostedAuthorityAction.Confirm)
				return existing.ConstructionJobId == JobId
					|| Fail("The hosted shell is reserved by another exact job.", out Failure);
			if (action != KingdomHostedAuthorityAction.Reserve)
				return Fail("The hosted shell already has another exact carrier.", out Failure);
			KingdomHostedArcologyAuthority row = new KingdomHostedArcologyAuthority {
				Phase = KingdomHostedAuthorityPhase.Reserved, RealmId = System.RealmId,
				SettlementId = settlement, ZoneId = Z.ZoneID, CarrierId = Carrier.IDIfAssigned,
				ConstructionJobId = JobId
			};
			return WriteExact(System, row, out Failure);
		}

		public static void ReleaseCleanReservation(KingdomSystem System, Zone Z,
			GameObject Carrier, string JobId)
		{
			KingdomHostedArcologyAuthority row;
			string ignored;
			if (System == null || Z == null || !GameObject.Validate(Carrier)
				|| !TryReadAuthority(System, out row, out ignored) || row == null
				|| row.Phase != KingdomHostedAuthorityPhase.Reserved || row.ZoneId != Z.ZoneID
				|| row.CarrierId != Carrier.IDIfAssigned || row.ConstructionJobId != JobId) return;
			ClearExactAuthority(System, row);
		}

		public static bool BindAuthority(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job, GameObject Root, out string Failure)
		{
			Failure = null;
			if (System == null || Z == null || Job == null || !GameObject.Validate(Root)
				|| string.IsNullOrEmpty(Root.IDIfAssigned)
				|| Root.CurrentZone != Z || Job.Route != KingdomConstructionRoute.Improvement
				|| Job.TargetKey != ArcologyKey || Job.ZoneId != Z.ZoneID)
				return Fail("The hosted shell lacks its exact improvement handover.", out Failure);
			KingdomHostedArcologyAuthority row;
			if (!TryReadAuthority(System, out row, out Failure) || row == null
				|| row.Phase != KingdomHostedAuthorityPhase.Reserved
				|| row.RealmId != System.RealmId || row.ZoneId != Z.ZoneID
				|| row.CarrierId != Job.SubjectId || row.ConstructionJobId != Job.Id)
				return Fail("The hosted shell reservation no longer names this handover.", out Failure);
			row.CarrierId = Root.IDIfAssigned;
			row.Phase = KingdomHostedAuthorityPhase.Active;
			return WriteExact(System, row, out Failure);
		}

		internal static bool ReconcileRoot(GameObject Root, out string Failure)
		{
			Failure = null;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			Zone zone = Root?.CurrentZone;
			if (system == null || zone == null || string.IsNullOrEmpty(Root.IDIfAssigned))
				return Fail("The shell is outside exact realm ground.", out Failure);
			// A shell from an exiled or earlier realm is a loaded, inert foreign landmark. It
			// must not be relabelled or permanently quarantined by the founder's current realm.
			if (!system.Founded || system.ClaimedZones == null
				|| !system.ClaimedZones.Contains(zone.ZoneID)) return true;
			KingdomHostedArcologyAuthority row;
			if (!TryReadAuthority(system, out row, out Failure)) return false;
			if (row != null && row.Phase == KingdomHostedAuthorityPhase.Active
				&& row.RealmId == system.RealmId && row.ZoneId == zone.ZoneID
				&& row.CarrierId == Root.IDIfAssigned)
			{
				r_KingdomArcology hosted = Root.GetPart<r_KingdomArcology>();
				if (hosted == null || !string.IsNullOrEmpty(hosted.QuarantineReason))
				{
					if (hosted != null) Quarantine(hosted, hosted.QuarantineReason);
					return Fail("The hosted shell carries quarantined local state.", out Failure);
				}
				return true;
			}
			string receipt = Root.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomConstructionJob job;
			if (row != null && KingdomConstruction.TryFind(receipt, out job)
				&& BindAuthority(system, zone, job, Root, out Failure)) return true;
			KingdomConstructionJob legacy;
			if (row == null && KingdomUpgrade.DesignKeyOf(Root) == ArcologyKey
				&& string.IsNullOrEmpty(Root.GetPart<r_KingdomArcology>()?.QuarantineReason)
				&& !string.IsNullOrEmpty(receipt)
				&& KingdomConstruction.TryFind(receipt, out legacy)
				&& legacy.Route == KingdomConstructionRoute.Improvement
				&& legacy.TargetKey == ArcologyKey
				&& KingdomConstruction.CanSupersedeTerminalReceipt(
					system, zone, Root, legacy))
			{
				string settlement = system.SettlementIdForOwnedZone(zone.ZoneID);
				row = new KingdomHostedArcologyAuthority { Phase = KingdomHostedAuthorityPhase.Active,
					RealmId = system.RealmId, SettlementId = settlement, ZoneId = zone.ZoneID,
					CarrierId = Root.IDIfAssigned, ConstructionJobId = receipt };
				return WriteExact(system, row, out Failure);
			}
			return Fail("Another carrier or malformed receipt owns the hosted-shell authority.", out Failure);
		}

		/// <summary>Pure authority proof for reports and automatic passes. It never binds,
		/// migrates, quarantines, writes game state, or creates the kingdom system.</summary>
		internal static bool IsOperationalPure(GameObject Root)
		{
			r_KingdomArcology hosted = Root?.GetPart<r_KingdomArcology>();
			Zone zone = Root?.CurrentZone;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (!GameObject.Validate(Root) || hosted == null || zone == null
				|| !string.IsNullOrEmpty(hosted.QuarantineReason) || system == null
				|| !system.Founded || string.IsNullOrEmpty(system.RealmId)
				|| system.ClaimedZones == null || !system.ClaimedZones.Contains(zone.ZoneID)
				|| !KingdomCrown.CrownedOn(system, zone.ZoneID)) return false;
			string settlement = system.SettlementIdForOwnedZone(zone.ZoneID);
			KingdomHostedArcologyAuthority authority;
			string failure;
			return !string.IsNullOrEmpty(settlement)
				&& TryReadAuthority(system, out authority, out failure) && authority != null
				&& authority.Phase == KingdomHostedAuthorityPhase.Active
				&& authority.RealmId == system.RealmId
				&& authority.SettlementId == settlement
				&& authority.ZoneId == zone.ZoneID
				&& authority.CarrierId == Root.IDIfAssigned;
		}

		/// <summary>Finds the sole loaded shell candidate for the ordered system guards.
		/// It is a bounded, read-only scan and never chooses between ambiguous carriers.</summary>
		internal static bool TryReconciliationRoot(Zone Z, out GameObject Root,
			out r_KingdomArcology Part, out string Failure)
		{
			Root = null; Part = null; Failure = null;
			if (Z == null) return true;
			List<GameObject> objects = Z.GetObjects();
			if (objects == null || objects.Count > MaxReconciliationObjects)
				return Fail("The hosted-shell reconciliation scan is unbounded.", out Failure);
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				r_KingdomArcology hosted = item?.GetPart<r_KingdomArcology>();
				if (!GameObject.Validate(item) || item.CurrentZone != Z || hosted == null) continue;
				if (Root != null)
				{
					Root = null; Part = null;
					return Fail("Hosted-shell reconciliation found ambiguous carriers.", out Failure);
				}
				Root = item; Part = hosted;
			}
			return true;
		}

		private static bool TryReadAuthority(KingdomSystem System,
			out KingdomHostedArcologyAuthority Authority, out string Failure)
		{
			Authority = null; Failure = null;
			if (The.Game == null || System == null || string.IsNullOrEmpty(System.RealmId))
				return Fail("The realm has no hosted-shell store.", out Failure);
			KingdomHostedArcologyAuthority first;
			KingdomHostedArcologyAuthority second;
			if (!TryReadAuthoritySlots(out first, out second, out Failure)) return false;
			if (first != null && first.RealmId == System.RealmId) Authority = first;
			if (second != null && second.RealmId == System.RealmId)
			{
				if (Authority != null)
					return Fail("The hosted-shell authority is duplicated across fixed slots.", out Failure);
				Authority = second;
			}
			return true;
		}

		private static bool WriteExact(KingdomSystem System,
			KingdomHostedArcologyAuthority Authority, out string Failure)
		{
			Failure = null; string encoded = KingdomHostedArcologyReceiptCodec.EncodeAuthority(Authority);
			if (The.Game == null || System == null || Authority == null
				|| Authority.RealmId != System.RealmId || string.IsNullOrEmpty(encoded))
				return Fail("The hosted-shell authority is invalid.", out Failure);
			KingdomHostedArcologyAuthority first;
			KingdomHostedArcologyAuthority second;
			if (!TryReadAuthoritySlots(out first, out second, out Failure)) return false;
			string retained = System.ExiledRealmArchive == null
				? null : System.ExiledRealmArchive.RealmId;
			int slot = KingdomHostedArcologyRules.AuthoritySlotForWrite(first, second,
				System.RealmId, retained);
			if (slot < 0)
				return Fail("The bounded hosted-shell authority slots require inspection.", out Failure);
			string key = AuthoritySlotKeys[slot]; The.Game.SetStringGameState(key, encoded);
			if (The.Game.GetStringGameState(key, "") != encoded)
				return Fail("The hosted-shell authority did not persist exactly.", out Failure);
			return true;
		}

		private static bool TryReadAuthoritySlots(out KingdomHostedArcologyAuthority First,
			out KingdomHostedArcologyAuthority Second, out string Failure)
		{
			First = null; Second = null; Failure = null;
			if (The.Game == null) return Fail("The hosted-shell store is absent.", out Failure);
			if (!TryReadAuthoritySlot(0, out First) || !TryReadAuthoritySlot(1, out Second))
				return Fail("A fixed hosted-shell authority slot cannot be read; it was left untouched.",
					out Failure);
			if (First != null && Second != null && First.RealmId == Second.RealmId)
				return Fail("The hosted-shell authority is duplicated across fixed slots.", out Failure);
			return true;
		}

		private static bool TryReadAuthoritySlot(int Slot,
			out KingdomHostedArcologyAuthority Authority)
		{
			Authority = null;
			string encoded = The.Game.GetStringGameState(AuthoritySlotKeys[Slot], "");
			return string.IsNullOrEmpty(encoded)
				|| KingdomHostedArcologyReceiptCodec.TryDecodeAuthority(encoded, out Authority);
		}

		private static void ClearExactAuthority(KingdomSystem System,
			KingdomHostedArcologyAuthority Expected)
		{
			KingdomHostedArcologyAuthority first;
			KingdomHostedArcologyAuthority second;
			string ignored;
			if (Expected == null || !TryReadAuthoritySlots(out first, out second, out ignored)) return;
			int slot = first != null && first.RealmId == Expected.RealmId ? 0
				: second != null && second.RealmId == Expected.RealmId ? 1 : -1;
			if (slot < 0 || KingdomHostedArcologyReceiptCodec.EncodeAuthority(
				slot == 0 ? first : second) != KingdomHostedArcologyReceiptCodec.EncodeAuthority(Expected))
				return;
			The.Game.SetStringGameState(AuthoritySlotKeys[slot], "");
		}

		private static bool Fail(string Text, out string Failure) { Failure = Text; return false; }
	}
}
