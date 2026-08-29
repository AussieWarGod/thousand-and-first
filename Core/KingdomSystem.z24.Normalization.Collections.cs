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
				if (ExiledSettlementTopology == null)
					ExiledSettlementTopology = new KingdomSettlementTopology();
				if (ExiledStandings == null)
				{
					ExiledStandings = new Dictionary<string, int>();
				}
				if (ExiledRealmPolicyToward == null)
					ExiledRealmPolicyToward = new Dictionary<string, int>();
				if (ExiledRegardSpilloverRemainders == null)
					ExiledRegardSpilloverRemainders = new Dictionary<string, int>();
				if (ExiledRegardSpilloverObservedReputation == null)
					ExiledRegardSpilloverObservedReputation =
						new Dictionary<string, int>();
				if (Exiled)
				{
					// Legacy saves without an archive may promote their sole remembered city.
				#pragma warning disable 618
					KingdomSettlement legacyExiled = ExiledAway;
				#pragma warning restore 618
					if (ExiledSeat == null)
					{
						ExiledSeat = legacyExiled ?? new KingdomSettlement();
						ExiledSettlementTopology = new KingdomSettlementTopology();
					}
					else if (ExiledSettlementTopology.Count == 0 && legacyExiled != null)
					{
						if (!ExiledSettlementTopology.TryAdoptLegacy(legacyExiled,
							out string migrationFailure)) QuarantineIdentity(migrationFailure);
					}
					else if (legacyExiled != null && ExiledSettlementTopology.Count > 0 &&
						!ReferenceEquals(legacyExiled, ExiledSettlementTopology.Get(0)) &&
						!KingdomArchivedSettlementCodec.ExactGraph(legacyExiled,
							ExiledSettlementTopology.Get(0), out string projectionFailure))
					{
						QuarantineIdentity("legacy ExiledAway projection differs from topology: " +
							projectionFailure);
					}
				}
				else
				{
					ExiledDisplayName = null;
					ExiledDeed = null;
					ExiledSeat = null;
					ExiledSettlementTopology = new KingdomSettlementTopology();
					ExiledStandings.Clear();
					ExiledRealmPolicyToward.Clear();
					ExiledRegardSpilloverRemainders.Clear();
					ExiledRegardSpilloverObservedReputation.Clear();
				}
				ExiledSeat?.Normalize();
				ExiledSettlementTopology.NormalizeMembers();
				SynchronizeLegacyExiledProjection();
			}
			if (ExiledRealmArchive != null && !ExiledRealmArchive.Quarantined &&
				!ExiledRealmArchive.RequiresDirectionalStandingMigration)
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
			ValidateSettlementTopology();
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
			if (PolityLedger == null) PolityLedger = new KingdomPolityLedger();
			KingdomPolityRules.Normalize(PolityLedger);
			KingdomPolityRealmTransitionRuntime.Normalize(this);
			if (Founded && string.IsNullOrEmpty(IdentityFault) &&
				PolityLedger.SchemaState == KingdomPolitySchemaState.Compatible)
			{
				// Additive saves have no trustworthy new-game option snapshot. Freeze Off; the
				// inheritance owner remains sole authority for any already-applied historical site.
				// A different non-empty owner cannot be silently relabelled across exile/refounding.
				if (!KingdomPolityRules.TryRebindEmptyIdentity(PolityLedger, RealmId,
					KingdomPolityImportPolicy.Off, out string polityIdentityFailure))
					KingdomPolityRules.Quarantine(PolityLedger, polityIdentityFailure);
			}
			if (Experience == null) Experience = new KingdomExperienceLedger();
			KingdomExperienceRules.Normalize(Experience);
			if (Founded && string.IsNullOrEmpty(IdentityFault)
				&& Experience.SchemaState == KingdomExperienceSchemaState.Compatible
				&& !KingdomExperienceRules.TryRebindEmptyIdentity(Experience, RealmId,
					out string experienceIdentityFailure))
				KingdomExperienceRules.Quarantine(Experience, experienceIdentityFailure);
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
			if (RealmPolicyToward == null)
				RealmPolicyToward = new Dictionary<string, int>();
			if (RegardSpilloverRemainders == null)
				RegardSpilloverRemainders = new Dictionary<string, int>();
			if (RegardSpilloverObservedReputation == null)
				RegardSpilloverObservedReputation = new Dictionary<string, int>();
			NormalizeDirectionalStandingState();
		}

	}
}
