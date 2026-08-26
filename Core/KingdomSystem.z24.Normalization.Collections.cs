using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private void NormalizeArchivedAndCollectionState(bool AllowLegacyIdentityMigration)
		{
			bool archiveTransactionActive = ExiledRealmArchive != null &&
				ExiledRealmArchive.Phase != KingdomRealmArchivePhase.None;
			// Once an archive phase exists, only its explicit exact-or-missing mirror CAS may
			// publish or retire mirror fields. Generic load normalization must not promote,
			// clear, allocate, or normalize one half of that transaction.
			if (!archiveTransactionActive)
			{
				if (ExiledStandings == null)
				{
					ExiledStandings = new Dictionary<string, int>();
				}
				if (Exiled)
				{
					// Legacy saves without an archive may promote their sole remembered city.
					if (ExiledSeat == null)
					{
						ExiledSeat = ExiledAway ?? new KingdomSettlement();
						ExiledAway = null;
					}
				}
				else
				{
					ExiledDisplayName = null;
					ExiledDeed = null;
					ExiledSeat = null;
					ExiledAway = null;
					ExiledStandings.Clear();
				}
				ExiledSeat?.Normalize();
				ExiledAway?.Normalize();
			}
			if (ExiledRealmArchive != null && !ExiledRealmArchive.Quarantined)
			{
				string archiveFailure;
				if (!ExiledRealmArchive.Validate(out archiveFailure))
					ExiledRealmArchive.Quarantine(archiveFailure);
			}
			if (RegardSpoken < (int)RealmRegard.Beloved || RegardSpoken > (int)RealmRegard.Repudiated)
			{
				RegardSpoken = (int)RealmRegard.Beloved;
			}
			// Frozen positional roster columns are touched only at the load-normalization bridge.
#pragma warning disable 618
			if (RosterNames == null)
			{
				RosterNames = new List<string>();
			}
			if (RosterOrigins == null)
			{
				RosterOrigins = new List<string>();
			}
			if (RosterArrived == null)
			{
				RosterArrived = new List<string>();
			}
#pragma warning restore 618
			if (DeadNames == null)
			{
				DeadNames = new List<string>();
			}
			if (DeadOrigins == null)
			{
				DeadOrigins = new List<string>();
			}
			if (DeadArrived == null)
			{
				DeadArrived = new List<string>();
			}
			if (DeadCauses == null)
			{
				DeadCauses = new List<string>();
			}
			// Complete legacy rolls seed an empty resident book once; existing rows always win and
			// rewrite these reflected fields as compatibility projections. Ragged evidence is retained.
			Simulation.City.KingdomResidents.AdoptLegacyAuthority(this);
			KingdomSettlement.TruncateParallelRows(
				DeadNames, DeadOrigins, DeadArrived, DeadCauses);
			if (Ledger == null)
			{
				Ledger = new KingdomLedger();
			}
			Ledger.Normalize();
			if (ClaimedZones == null)
			{
				ClaimedZones = new List<string>();
			}
			NormalizeIdentity(AllowLegacyIdentityMigration);
			if (ZoneDistricts == null)
			{
				ZoneDistricts = new Dictionary<string, string>();
			}
			if (ActiveDealKeys == null)
			{
				ActiveDealKeys = new List<string>();
			}
			if (ActiveDealFactions == null)
			{
				ActiveDealFactions = new List<string>();
			}
			if (DealNextTicks == null)
			{
				DealNextTicks = new List<long>();
			}
			NormalizeTradeBook();
			if (ChronicleEntries == null)
			{
				ChronicleEntries = new List<string>();
			}
			if (OutsiderEntries == null)
			{
				OutsiderEntries = new List<string>();
			}
			if (OriginCounts == null)
			{
				OriginCounts = new Dictionary<string, int>();
			}
			if (CultureCounts == null)
			{
				CultureCounts = new Dictionary<string, int>();
			}
			if (SpeciesCounts == null)
			{
				SpeciesCounts = new Dictionary<string, int>();
			}
			if (IdentityCounts == null)
			{
				IdentityCounts = new Dictionary<string, int>();
			}
			if (CreedPastCounts == null)
			{
				CreedPastCounts = new Dictionary<string, int>();
			}
			if (CreedCounts == null)
			{
				CreedCounts = new Dictionary<string, int>();
			}
			if (Standings == null)
			{
				Standings = new Dictionary<string, int>();
			}
		}

	}
}
