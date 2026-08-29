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

		public bool Founded => !string.IsNullOrEmpty(KingdomFactionName);

		/// <summary>Whether a realm has put the founder out and is remembered here.</summary>
		public bool Exiled => !string.IsNullOrEmpty(ExiledFactionName);

		/// <summary>How many cities the expelled-from realm holds, or 0 if there is none.</summary>
		public int ExiledSettlementCount => !Exiled ? 0 :
			1 + (ExiledSettlementTopology?.Count ?? 0);

		/// <summary>
		/// The seated settlement's name for prose. Falls back to the realm's display name for a
		/// save written before a city could be named apart from its realm.
		/// </summary>
		public string SeatName => string.IsNullOrEmpty(SettlementName) ? KingdomDisplayName : SettlementName;

		/// <summary>
		/// The realm's simulation seed, composed from its two stored halves.
		/// <para>
		/// Internal rather than public because <c>KernelSeed128</c> is the simulation slice's own
		/// value type and the kernel is deliberate about it: identity travels one way, through the
		/// canonical encoder, and a seed handed out on a public surface is a seed somebody keys a
		/// collection by. The two halves are the public, serialized surface.
		/// </para>
		/// </summary>
		internal Simulation.Kernel.KernelSeed128 SimulationSeed => new Simulation.Kernel.KernelSeed128(SimulationSeedHigh, SimulationSeedLow);

		/// <summary>The exact seated-city id, or null when any realm/city provenance or topology
		/// cannot be reproved whole. Callers must fail closed; there is deliberately no name fold.</summary>
		internal string CurrentSettlementId
		{
			get
			{
				string realm;
				string settlement;
				return TryGetCurrentIdentity(out realm, out settlement) ? settlement : null;
			}
		}

		/// <summary>The exact realm id under the same whole-topology proof as the current city.</summary>
		internal string CurrentRealmId
		{
			get
			{
				string realm;
				string settlement;
				return TryGetCurrentIdentity(out realm, out settlement) ? realm : null;
			}
		}

		internal bool TryGetCurrentIdentity(out string ExactRealmId,
			out string ExactSettlementId)
		{
			ExactRealmId = null;
			ExactSettlementId = null;
			if (RealmTransitionActive()) return false;
			List<string> settlements;
			string failure = null;
			if (!TryExactSettlementIds(RequirePublishedClaims: true, out settlements,
				out failure) || City == null || !settlements.Contains(City.SettlementId))
			{
				return false;
			}
			ExactRealmId = RealmId;
			ExactSettlementId = City.SettlementId;
			return true;
		}

		internal bool TryCaptureSealIdentity(out KingdomSealIdentity Identity,
			out string Failure)
		{
			Identity = null;
			Failure = null;
			if (!TryGetCurrentIdentity(out string realm, out string settlement) ||
				!TryExactSettlementIds(RequirePublishedClaims: true, out List<string> settlements,
					out Failure))
			{
				Failure = Failure ?? "current immutable realm topology cannot be proved";
				return false;
			}
			KingdomSettlement seat;
			try { seat = Capture(); }
			catch (Exception ex) { Failure = ex.Message; return false; }
			settlements.Sort(StringComparer.Ordinal);
			if (!TryBuildSealSettlementProvenance(settlements, seat,
				NonSeatSettlements(),
				out List<string> provenance, out Failure)) return false;
			KingdomSealIdentity candidate = new KingdomSealIdentity
			{
				RealmId = realm,
				SettlementId = settlement,
				SettlementIds = new List<string>(settlements),
				SettlementProvenanceRows = provenance,
				RealmIdentityVersion = RealmIdentityVersion,
				RealmIdentityOrigin = RealmIdentityOrigin,
				RealmIdentityTransactionId = RealmIdentityTransactionId,
				RealmIdentityLegacyFaction = RealmIdentityLegacyFaction,
				RealmIdentityFoundedTick = RealmIdentityFoundedTick,
				RealmIdentitySeedHigh = RealmIdentitySeedHigh,
				RealmIdentitySeedLow = RealmIdentitySeedLow,
				RealmIdentityFirstClaimedZone = RealmIdentityFirstClaimedZone,
				SettlementIdentityVersion = seat.SettlementIdentityVersion,
				SettlementIdentityOrigin = seat.SettlementIdentityOrigin,
				SettlementIdentityTransactionId = seat.SettlementIdentityTransactionId,
				SettlementIdentityFoundedTick = seat.SettlementIdentityFoundedTick,
				SettlementIdentityFirstClaimedZone = seat.SettlementIdentityFirstClaimedZone,
				SettlementIdentityLegacyId = seat.SettlementIdentityLegacyId
			};
			if (!KingdomSealRules.ExactIdentity(candidate, seat))
			{
				Failure = "current seal identity provenance cannot be reproved";
				return false;
			}
			Identity = candidate;
			return true;
		}

		private static bool TryBuildSealSettlementProvenance(IList<string> SettlementIds,
			KingdomSettlement Seat, IList<KingdomSettlement> NonSeat,
			out List<string> Rows,
			out string Failure)
		{
			Rows = new List<string>();
			Failure = null;
			if (SettlementIds == null || Seat?.City == null || NonSeat == null)
			{
				Failure = "seal settlement topology is absent";
				return false;
			}
			for (int i = 0; i < SettlementIds.Count; i++)
			{
				KingdomSettlement source = null;
				if (Seat.City.SettlementId == SettlementIds[i]) source = Seat;
				for (int j = 0; j < NonSeat.Count; j++)
				{
					if (NonSeat[j]?.City?.SettlementId != SettlementIds[i]) continue;
					if (source != null)
					{
						Failure = "seal settlement topology has duplicate city identity";
						return false;
					}
					source = NonSeat[j];
				}
				if (source == null || !KingdomSealRules.TryBuildSettlementProvenance(
					SettlementIds[i], source.SettlementIdentityVersion,
					source.SettlementIdentityOrigin, source.SettlementIdentityTransactionId,
					source.SettlementIdentityFoundedTick,
					source.SettlementIdentityFirstClaimedZone,
					source.SettlementIdentityLegacyId, out string row))
				{
					Failure = "seal settlement provenance cannot be bounded";
					return false;
				}
				Rows.Add(row);
			}
			return true;
		}

		internal bool SealIdentityStillMatches(KingdomSealIdentity Expected)
		{
			if (Expected == null || !TryCaptureSealIdentity(out KingdomSealIdentity current,
				out string _)) return false;
			if (Expected.RealmId != current.RealmId ||
				Expected.SettlementId != current.SettlementId ||
				Expected.RealmIdentityVersion != current.RealmIdentityVersion ||
				Expected.RealmIdentityOrigin != current.RealmIdentityOrigin ||
				Expected.RealmIdentityTransactionId != current.RealmIdentityTransactionId ||
				Expected.RealmIdentityLegacyFaction != current.RealmIdentityLegacyFaction ||
				Expected.RealmIdentityFoundedTick != current.RealmIdentityFoundedTick ||
				Expected.RealmIdentitySeedHigh != current.RealmIdentitySeedHigh ||
				Expected.RealmIdentitySeedLow != current.RealmIdentitySeedLow ||
				Expected.RealmIdentityFirstClaimedZone != current.RealmIdentityFirstClaimedZone ||
				Expected.SettlementIdentityVersion != current.SettlementIdentityVersion ||
				Expected.SettlementIdentityOrigin != current.SettlementIdentityOrigin ||
				Expected.SettlementIdentityTransactionId != current.SettlementIdentityTransactionId ||
				Expected.SettlementIdentityFoundedTick != current.SettlementIdentityFoundedTick ||
				Expected.SettlementIdentityFirstClaimedZone !=
					current.SettlementIdentityFirstClaimedZone ||
				Expected.SettlementIdentityLegacyId != current.SettlementIdentityLegacyId ||
				Expected.SettlementIds == null || current.SettlementIds == null ||
				Expected.SettlementIds.Count != current.SettlementIds.Count ||
				Expected.SettlementProvenanceRows == null ||
				current.SettlementProvenanceRows == null ||
				Expected.SettlementProvenanceRows.Count !=
					current.SettlementProvenanceRows.Count) return false;
			for (int i = 0; i < Expected.SettlementIds.Count; i++)
				if (Expected.SettlementIds[i] != current.SettlementIds[i] ||
					Expected.SettlementProvenanceRows[i] !=
						current.SettlementProvenanceRows[i]) return false;
			return true;
		}

	}
}
