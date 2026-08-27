using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		internal static void RetryConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.PurposeConsignment) return;
			KingdomConstructionJob live = Job;
			if (!KingdomPurposeRules.TryDecodeManifest(live.Payload,
				out KingdomPurposeManifest manifest) || live.PhysicalReceipt != live.Payload
				|| manifest.OriginZoneId != Z.ZoneID || live.ZoneId != Z.ZoneID)
			{
				KingdomConstruction.Quarantine(ref live,
					"The purpose consignment manifest is absent, changed, or bound to another source zone.");
				return;
			}
			if (!ExactEndpoints(System, Z, live, manifest,
				out GameObject source, out GameObject destination,
				out KingdomPurposeConnection connection, out bool requiresInspection,
				out string endpointFailure))
			{
				if (requiresInspection)
				{
					KingdomConstruction.Quarantine(ref live, endpointFailure
						?? "The frozen purpose route is physically ambiguous.");
					return;
				}
				// Missing, moved, stale, or dark ground is an actionable stall: restoring the same
				// endpoint proves the frozen route again without choosing a substitute.
				KingdomLog.Log("purpose consignment waits: " + endpointFailure);
				return;
			}
			if (live.Phase == KingdomConstructionPhase.Funded
				|| live.Phase == KingdomConstructionPhase.Outstanding)
			{
				if (!KingdomConstruction.BeginProjection(ref live, out _)) return;
			}
			if (live.Phase != KingdomConstructionPhase.ProjectionPending) return;
			if (!TryEscrowCargo(live, manifest, out GameObject cargo))
			{
				if (!string.IsNullOrEmpty(live.OutputId))
				{
					KingdomConstruction.Quarantine(ref live,
						"The published purpose cargo identity lost its exact escrow root.");
					return;
				}
				cargo = CreateCargo(live, manifest);
				if (!GameObject.Validate(cargo) || !RootCargo(live, cargo))
				{
					KingdomConstruction.FinishProjection(ref live, false, false,
						"The exact purpose cargo could not be created and rooted for retry.");
					return;
				}
			}
			if (string.IsNullOrEmpty(live.OutputId))
			{
				if (!KingdomConstruction.UpdateOutput(ref live, cargo.ID)) return;
			}
			if (!ExactCargo(cargo, live, manifest))
			{
				KingdomConstruction.Quarantine(ref live,
					"The rooted purpose cargo changed before physical transport.");
				return;
			}
			Inventory sourceInventory = source.RequirePart<Inventory>();
			if (live.PhysicalPhase == KingdomPhysicalPhase.None)
			{
				if (!KingdomConstruction.UpdatePhysical(ref live,
					KingdomPhysicalPhase.CargoOutputPending, 0, 1, 0, cargo.ID,
					destination.ID, live.Payload)) return;
			}
			if (live.PhysicalPhase == KingdomPhysicalPhase.CargoOutputPending)
			{
				if (!ExactOwned(cargo, source))
				{
					if (!ExactLoose(cargo, Z, connection.DestinationZone))
					{
						KingdomConstruction.Quarantine(ref live,
							"Purpose cargo output has an ambiguous owner before source settlement.");
						return;
					}
					GameObject accepted;
					try { accepted = sourceInventory.AddObject(cargo, null,
						Silent: true, NoStack: true); }
					catch (Exception ex)
					{
						ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
						KingdomConstruction.FinishProjection(ref live, false, false,
							"Source-gate cargo placement threw before exact settlement: " + ex.Message);
						return;
					}
					ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
					KingdomSurvey.ObserveAddResultInActive(Z, cargo, accepted);
					if (!ReferenceEquals(accepted, cargo) || !ExactOwned(cargo, source))
					{
						KingdomConstruction.Quarantine(ref live,
							"Source-gate cargo placement replaced or moved the exact object.");
						return;
					}
				}
				if (!KingdomConstruction.UpdatePhysical(ref live,
					KingdomPhysicalPhase.CargoOutputSettled, 0, 1, 0, cargo.ID,
					destination.ID, live.Payload)) return;
			}
			if (live.PhysicalPhase == KingdomPhysicalPhase.CargoOutputSettled)
			{
				if (!ExactOwned(cargo, source))
				{
					KingdomConstruction.Quarantine(ref live,
						"Settled purpose cargo is no longer in its exact source gate.");
					return;
				}
				if (!KingdomConstruction.UpdatePhysical(ref live,
					KingdomPhysicalPhase.CargoTransferPending, 0, 1, 0, cargo.ID,
					destination.ID, live.Payload)) return;
			}
			if (live.PhysicalPhase == KingdomPhysicalPhase.CargoTransferPending)
			{
				if (ExactOwned(cargo, destination))
				{
					SettleDelivery(System, ref live, manifest, cargo, destination);
					return;
				}
				if (ExactOwned(cargo, source))
				{
					bool removed;
					try { removed = sourceInventory.RemoveObjectFromInventory(cargo, null,
						Silent: true, NoStack: true); }
					catch (Exception ex)
					{
						ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
						if (ExactOwned(cargo, source))
						{
							KingdomConstruction.UpdatePhysical(ref live,
								KingdomPhysicalPhase.CargoOutputSettled, 0, 1, 0,
								cargo.ID, destination.ID, live.Payload, ex.Message);
							return;
						}
						if (!ExactLoose(cargo, Z, connection.DestinationZone))
						{
							KingdomConstruction.Quarantine(ref live,
								"Source removal threw after cargo ownership became ambiguous.");
							return;
						}
						removed = true;
					}
					ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
					if (!removed && ExactOwned(cargo, source))
					{
						KingdomConstruction.UpdatePhysical(ref live,
							KingdomPhysicalPhase.CargoOutputSettled, 0, 1, 0, cargo.ID,
							destination.ID, live.Payload,
							"Source gate refused exact cargo removal before changing ownership.");
						return;
					}
				}
				if (!ExactLoose(cargo, Z, connection.DestinationZone))
				{
					if (ExactOwned(cargo, destination))
					{
						SettleDelivery(System, ref live, manifest, cargo, destination);
						return;
					}
					KingdomConstruction.Quarantine(ref live,
						"Purpose cargo left its source without one exact rooted loose object.");
					return;
				}
				GameObject placed = null;
				try { placed = destination.Inventory.AddObject(cargo, null,
					Silent: true, NoStack: true); }
				catch (Exception ex)
				{
					ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
					if (ExactOwned(cargo, destination))
					{
						SettleDelivery(System, ref live, manifest, cargo, destination);
						return;
					}
					RestoreSourceOrQuarantine(ref live, cargo, source, destination,
						"Destination stockpile placement threw: " + ex.Message);
					return;
				}
				ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
				KingdomSurvey.ObserveAddResultInActive(connection.DestinationZone, cargo, placed);
				if (!ReferenceEquals(placed, cargo) || !ExactOwned(cargo, destination))
				{
					RestoreSourceOrQuarantine(ref live, cargo, source, destination,
						"Destination stockpile replaced or rejected the exact cargo object.");
					return;
				}
				SettleDelivery(System, ref live, manifest, cargo, destination);
				return;
			}
			if (live.PhysicalPhase == KingdomPhysicalPhase.CargoDelivered)
				SettleDelivery(System, ref live, manifest, cargo, destination);
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (Job == null || Job.Route != KingdomConstructionRoute.PurposeConsignment) return;
			if (Job.Phase == KingdomConstructionPhase.Complete)
			{
				KingdomConstructionJob live = Job;
				if (!KingdomPurposeRules.TryDecodeManifest(live.Payload,
					out KingdomPurposeManifest manifest)
					|| !ExactDeliveredCargo(live, manifest, out _, out _))
				{
					KingdomConstruction.Quarantine(ref live,
						"The terminal purpose consignment no longer has one exact delivered cargo object.");
					return;
				}
				EnsureDeliveryOutbox(System, ref live, manifest);
				return;
			}
			RetryConstruction(System, Z, Job);
		}

	}
}
