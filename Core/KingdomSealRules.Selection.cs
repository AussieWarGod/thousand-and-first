using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomSealRules
	{
		/// <summary>
		/// What the engine's two facts about an origin run mean.
		/// <para>
		/// The one automatic crossing is a score with no save: the engine itself scored the run and
		/// then deleted it, which only permadeath's own terminal block does. A score with a save is
		/// a checkpoint death, permadeath switched off, or a cleanup that did not finish, and none
		/// of those is an ending. Neither fact is an orphan &mdash; a stage whose game nobody can
		/// account for &mdash; and an orphan is never taken silently.
		/// </para>
		/// </summary>
		/// <param name="ScoreForOrigin">A scoreboard entry exists for the origin game id.</param>
		/// <param name="OriginSaveStands">A valid primary save still exists for it.</param>
		public static KingdomSealEligibility Judge(bool ScoreForOrigin, bool OriginSaveStands)
		{
			if (ScoreForOrigin)
			{
				return OriginSaveStands ? KingdomSealEligibility.Checkpointed : KingdomSealEligibility.Ended;
			}
			return OriginSaveStands ? KingdomSealEligibility.Living : KingdomSealEligibility.Orphaned;
		}

		/// <summary>
		/// Whether a staged record of this status, with this verdict, may be promoted without
		/// asking anyone.
		/// <para>
		/// Retirement is deliberately absent. It uses <see cref="PromoteRetirement"/> and never
		/// enters the score/save automatic path.
		/// </para>
		/// </summary>
		public static bool MayPromote(KingdomSealStatus Status, KingdomSealEligibility Verdict)
		{
			return Status == KingdomSealStatus.Terminal && Verdict == KingdomSealEligibility.Ended;
		}

		/// <summary>
		/// Chooses the legacy a new world is offered, under the configured policy.
		/// <para>
		/// Deterministic and independent of the order the caller found the files in: latest is the
		/// deepest generation, then the highest revision, then the latest written tick, then the
		/// legacy id in ordinal order. A directory listing is not an ordering, and two players
		/// with the same legacies must be offered the same one.
		/// </para>
		/// </summary>
		/// <param name="Legacies">Promoted legacies. Null or empty selects nothing.</param>
		/// <param name="Spent">Legacy ids already consumed or declined; null means none.</param>
		/// <param name="Policy">The import policy.</param>
		/// <returns>The chosen legacy, or null when there is nothing to offer.</returns>
		public static KingdomSealRecord Select(IList<KingdomSealRecord> Legacies, ICollection<string> Spent, KingdomImportPolicy Policy)
		{
			if (Policy != KingdomImportPolicy.LatestEligible || Legacies == null)
			{
				return null;
			}
			KingdomSealRecord best = null;
			for (int i = 0; i < Legacies.Count; i++)
			{
				KingdomSealRecord candidate = Legacies[i];
				if (candidate == null || candidate.Status != KingdomSealStatus.Promoted || !candidate.IsResolved)
				{
					continue;
				}
				if (Spent != null && Spent.Contains(candidate.LegacyId))
				{
					continue;
				}
				if (best == null || Later(candidate, best))
				{
					best = candidate;
				}
			}
			return best;
		}

		/// <summary>True when <paramref name="A"/> is the later legacy under the selection order.</summary>
		public static bool Later(KingdomSealRecord A, KingdomSealRecord B)
		{
			if (A.Generation != B.Generation)
			{
				return A.Generation > B.Generation;
			}
			if (A.Revision != B.Revision)
			{
				return A.Revision > B.Revision;
			}
			if (A.WrittenTick != B.WrittenTick)
			{
				return A.WrittenTick > B.WrittenTick;
			}
			return string.CompareOrdinal(A.LegacyId, B.LegacyId) > 0;
		}

		/// <summary>
		/// A deep copy. Used by every transition, because a seal's states are derived rather than
		/// mutated: a terminal attempt that a checkpoint undoes must leave the stage untouched.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="Record"/> is null.</exception>
		public static KingdomSealRecord Copy(KingdomSealRecord Record)
		{
			if (Record == null)
			{
				throw new ArgumentNullException("Record");
			}
			KingdomSealRecord copy = new KingdomSealRecord();
			copy.WriterVersion = Record.WriterVersion;
			copy.EngineVersion = Record.EngineVersion;
			copy.Status = Record.Status;
			copy.LineageId = Record.LineageId;
			copy.LegacyId = Record.LegacyId;
			copy.OriginGameId = Record.OriginGameId;
			copy.Generation = Record.Generation;
			copy.Revision = Record.Revision;
			copy.WrittenTick = Record.WrittenTick;
			copy.FounderName = Record.FounderName;
			copy.CauseText = Record.CauseText;
			copy.CauseKind = Record.CauseKind;
			copy.CauseTurn = Record.CauseTurn;
			copy.RealmName = Record.RealmName;
			copy.SettlementName = Record.SettlementName;
			copy.SettlementId = Record.SettlementId;
			copy.RealmId = Record.RealmId;
			copy.RealmSettlementIds = new List<string>(Record.RealmSettlementIds);
			copy.RealmSettlementProvenance =
				new List<string>(Record.RealmSettlementProvenance);
			copy.RealmIdentityVersion = Record.RealmIdentityVersion;
			copy.RealmIdentityOrigin = Record.RealmIdentityOrigin;
			copy.RealmIdentityTransactionId = Record.RealmIdentityTransactionId;
			copy.RealmIdentityLegacyFaction = Record.RealmIdentityLegacyFaction;
			copy.RealmIdentityFoundedTick = Record.RealmIdentityFoundedTick;
			copy.RealmIdentitySeedHigh = Record.RealmIdentitySeedHigh;
			copy.RealmIdentitySeedLow = Record.RealmIdentitySeedLow;
			copy.RealmIdentityFirstClaimedZone = Record.RealmIdentityFirstClaimedZone;
			copy.SettlementIdentityVersion = Record.SettlementIdentityVersion;
			copy.SettlementIdentityOrigin = Record.SettlementIdentityOrigin;
			copy.SettlementIdentityTransactionId = Record.SettlementIdentityTransactionId;
			copy.SettlementIdentityFoundedTick = Record.SettlementIdentityFoundedTick;
			copy.SettlementIdentityFirstClaimedZone =
				Record.SettlementIdentityFirstClaimedZone;
			copy.SettlementIdentityLegacyId = Record.SettlementIdentityLegacyId;
			copy.Vocation = Record.Vocation;
			copy.Style = Record.Style;
			copy.FoundedTick = Record.FoundedTick;
			copy.GroundZoneId = Record.GroundZoneId;
			copy.RegionName = Record.RegionName;
			copy.TerrainBlueprint = Record.TerrainBlueprint;
			copy.Depth = Record.Depth;
			copy.Stage = Record.Stage;
			copy.Population = Record.Population;
			copy.Defence = Record.Defence;
			copy.StoredWater = Record.StoredWater;
			copy.Withered = Record.Withered;
			copy.Vigour = Record.Vigour;
			copy.InterregnumRoll = Record.InterregnumRoll;
			copy.InheritedState = Record.InheritedState;
			copy.WorkKeys = new List<string>(Record.WorkKeys);
			copy.WorkX = new List<int>(Record.WorkX);
			copy.WorkY = new List<int>(Record.WorkY);
			copy.WorkConditions = new List<int>(Record.WorkConditions);
			copy.SpatialVersion = Record.SpatialVersion;
			copy.SpatialWidth = Record.SpatialWidth;
			copy.SpatialHeight = Record.SpatialHeight;
			copy.SpatialEntrySide = Record.SpatialEntrySide;
			copy.SpatialEntryX = Record.SpatialEntryX;
			copy.SpatialEntryY = Record.SpatialEntryY;
			copy.WorkSnapshots = new List<string>(Record.WorkSnapshots);
			copy.WorkSnapshotHashes = new List<string>(Record.WorkSnapshotHashes);
			copy.StreetX = new List<int>(Record.StreetX);
			copy.StreetY = new List<int>(Record.StreetY);
			copy.RollNames = new List<string>(Record.RollNames);
			copy.RollOrigins = new List<string>(Record.RollOrigins);
			copy.RollArrived = new List<string>(Record.RollArrived);
			copy.OriginKeys = new List<string>(Record.OriginKeys);
			copy.OriginCounts = new List<int>(Record.OriginCounts);
			copy.CreedKeys = new List<string>(Record.CreedKeys);
			copy.CreedCounts = new List<int>(Record.CreedCounts);
			copy.Chronicle = new List<string>(Record.Chronicle);
			copy.Outsider = new List<string>(Record.Outsider);
			copy.DeadNames = new List<string>(Record.DeadNames);
			copy.DeadCauses = new List<string>(Record.DeadCauses);
			copy.ProfileSchema = Record.ProfileSchema;
			copy.TechnologyBand = Record.TechnologyBand;
			copy.CanonicalBodyKeys = new List<string>(Record.CanonicalBodyKeys);
			copy.SourceProfileDigest = Record.SourceProfileDigest;
			copy.ProfileProvenanceDigest = Record.ProfileProvenanceDigest;
			return copy;
		}
	}
}
