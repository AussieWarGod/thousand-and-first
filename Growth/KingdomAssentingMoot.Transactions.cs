using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMoot
	{
		internal static bool EnsureAuthority(KingdomSystem System, GameObject Building,
			out KingdomAssentingMootContext Context, out string Failure)
		{
			Context = null;
			if (!KingdomMaster.NewWorkAllowed(System))
				return Fail("The realm master option pauses new moot work.", out Failure);
			if (!TryContext(System, Building, out Context, out Failure) || !Context.Owned)
				return false;
			KingdomAssentingMootReceipt receipt = Context.Book.AssentingMoot;
			if (!KingdomAssentingMootRules.Validate(receipt, out Failure))
				return Quarantine(Context.Book, receipt, Failure, out Failure);
			if (receipt.Phase == KingdomAssentingMootPhase.Quarantined)
			{
				Failure = receipt.Fault;
				return false;
			}
			string lot = Building.GetStringProperty(KingdomPlots.PlotIdProperty);
			string id = Building.IDIfAssigned;
			int hp = Building.HasStat("Hitpoints") ? Building.baseHitpoints : 0;
			if (Building.GetIntProperty("KingdomBuilt") != 1 || string.IsNullOrEmpty(lot)
				|| string.IsNullOrEmpty(id) || hp <= 0)
				return Fail("Only one finished, stamped assenting moot can own a ward.", out Failure);
			if (receipt.Phase == KingdomAssentingMootPhase.None)
			{
				KingdomAssentingMootReceipt prepared;
				if (!KingdomAssentingMootRules.TryPrepare(Context.RealmId, Context.SettlementId,
					Context.SettlementName, Context.Zone.ZoneID, id, lot, hp, 1, Now(),
					out prepared, out Failure)) return false;
				Context.Book.AssentingMoot = prepared;
			}
			else if (!string.Equals(receipt.BuildingObjectId, id, StringComparison.Ordinal))
			{
				if (TryExactBuilding(receipt, out GameObject old))
					return Fail("Another exact moot already carries this city's voices.", out Failure);
				if (!SuspendBook(Context.Book, receipt,
					"The prior exact moot building is absent.", out Failure)) return false;
				if (!KingdomAssentingMootRules.TryRebind(Context.Book.AssentingMoot,
					Context.Zone.ZoneID, id, lot, hp, Now(), out KingdomAssentingMootReceipt rebound,
					out Failure)) return false;
				Context.Book.AssentingMoot = rebound;
			}
			return Reconcile(System, Context.Book, Building, true, out Failure);
		}

		internal static bool TryChangeMember(KingdomAssentingMootContext Context,
			KingdomAssentingMootRole Role, bool Add, int ResidentId, out string Failure)
		{
			Failure = null;
			if (Context == null || Context.Book == null
				|| !KingdomMaster.NewWorkAllowed(Context.System))
				return Fail("The realm master option pauses new moot work.", out Failure);
			KingdomAssentingMootReceipt receipt = Context.Book.AssentingMoot;
			string name = "";
			string bodyId = "";
			if (Add)
			{
				KingdomResidentRow resident;
				if (!KingdomResidents.TryResident(Context.Book, ResidentId, out resident)
					|| resident.Standing != KingdomResidentStanding.Resident)
					return Fail("That person is not a standing resident of this city.", out Failure);
				GameObject body;
				string zoneId;
				if (!KingdomResidents.TryResolveBoundBody(Context.System, ResidentId, true,
					out body, out zoneId) || body == null || !BookOwnsZone(Context, zoneId))
					return Fail("The exact named resident body cannot presently be resolved.", out Failure);
				r_KingdomAssentingMootMember marker =
					body.GetPart<r_KingdomAssentingMootMember>();
				if (marker != null && !MarkerAuthorityMatches(marker, receipt, body))
					return Fail("A different moot authority already marks that body.", out Failure);
				name = resident.Name;
				bodyId = body.IDIfAssigned;
			}
			if (!KingdomAssentingMootRules.TryChangeMember(receipt, Role, Add, ResidentId,
				name, bodyId, Now(), out KingdomAssentingMootReceipt changed, out Failure))
				return false;
			if (!Suspend(Context, receipt, "Membership is being revised.", out Failure))
				return false;
			Context.Book.AssentingMoot = changed;
			return Reconcile(Context.System, Context.Book, Context.Building, true, out Failure);
		}

		internal static void SuspendForBuilding(GameObject Building, string Reason)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (!TryContext(system, Building, out KingdomAssentingMootContext context,
				out string _)) return;
			KingdomAssentingMootReceipt receipt = context.Book.AssentingMoot;
			if (receipt == null || receipt.Phase == KingdomAssentingMootPhase.None
				|| !string.Equals(receipt.BuildingObjectId, Building.IDIfAssigned,
					StringComparison.Ordinal)) return;
			string failure;
			Suspend(context, receipt, Reason, out failure);
		}
	}
}
