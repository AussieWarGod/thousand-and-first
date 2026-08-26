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
		private static bool CharterAbilityRemoved()
		{
			GameObject player = The.Player;
			if (player == null) return true;
			int partCount = 0;
			KingdomCharterPart part = null;
			for (int i = 0; i < player.PartsList.Count; i++)
			{
				IPart candidate = player.PartsList[i];
				if (candidate != null && candidate.GetType().Name == "KingdomCharterPart")
				{
					partCount++;
					if (candidate is KingdomCharterPart typed) part = typed;
				}
			}
			if (partCount > 1 || (partCount == 1 && (part == null ||
				!ReferenceEquals(part.ParentObject, player))) ||
				(part != null && part.ActivatedAbilityID != Guid.Empty)) return false;
			System.Collections.Generic.Dictionary<Guid, ActivatedAbilityEntry> abilities =
				player.ActivatedAbilities?.AbilityByGuid;
			if (abilities == null) return true;
			foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in abilities)
				if (row.Value != null && row.Value.Command == KingdomCharterPart.COMMAND) return false;
			return true;
		}

		private bool ExactExileMirrors(KingdomRealmArchive Archive)
		{
			if (Archive == null || ExiledSeat == null || string.IsNullOrEmpty(ExiledFactionName)
				|| ExiledStandings == null) return false;
			string failure;
			if (!Archive.ExactMirrors(ExiledFactionName, ExiledDisplayName, ExiledDeed,
				ExiledTick, ExiledSeat, ExiledAway, ExiledStandings, out failure)) return false;
			if (!ExactArchivedSettlements(Archive.RealmId, ExiledSeat, ExiledAway,
				Archive.SettlementIds)) return false;
			KingdomSettlement currentSeat;
			try { currentSeat = Capture(); }
			catch { return false; }
			object[] currentRoots = { currentSeat, Away, Seceded, Standings, Bindings, Jobs,
				ChronicleEntries, OutsiderEntries, Haul, CarryBook };
			object[] mirrorRoots = { ExiledSeat, ExiledAway, ExiledStandings };
			return KingdomArchivedSettlementCodec.DisjointMutableGraphs(currentRoots,
				mirrorRoots, out failure);
		}

		/// <summary>Clears the published exile mirror by exact-or-cleared CAS. Each assignment may
		/// be a save cut: a retry accepts the archive value or its canonical cleared value only.</summary>
		private bool TryClearExileMirrors(KingdomRealmArchive Archive, out string Failure)
		{
			Failure = null;
			if (Archive == null ||
				!ClearMirrorString(ref ExiledFactionName, Archive.FactionName) ||
				!ClearMirrorString(ref ExiledDisplayName, Archive.DisplayName) ||
				!ClearSettlementMirror(ref ExiledSeat, Archive.Seat, out Failure) ||
				!ClearSettlementMirror(ref ExiledAway, Archive.Away, out Failure) ||
				!ClearStandingsMirror(Archive.Standings, out Failure) ||
				!ClearMirrorString(ref ExiledDeed, Archive.ExileDeed) ||
				!ClearMirrorTick(ref ExiledTick, Archive.ClosedTick))
			{
				Failure = Failure ?? "return cleanup mirror reached a third value";
				return false;
			}
			return true;
		}

		private static bool ClearMirrorString(ref string Current, string Expected)
		{
			if (Current == null) return true;
			if (!string.Equals(Current, Expected, StringComparison.Ordinal)) return false;
			Current = null;
			return true;
		}

		private static bool ClearMirrorTick(ref long Current, long Expected)
		{
			if (Current == 0L) return true;
			if (Current != Expected) return false;
			Current = 0L;
			return true;
		}

		private static bool ClearSettlementMirror(ref KingdomSettlement Current,
			KingdomSettlement Expected, out string Failure)
		{
			Failure = null;
			if (Current == null) return true;
			if (Expected == null ||
				!KingdomArchivedSettlementCodec.ExactGraph(Expected, Current, out Failure) ||
				!KingdomArchivedSettlementCodec.DisjointMutableGraphs(
					new object[] { Expected }, new object[] { Current }, out Failure)) return false;
			Current = null;
			return true;
		}

		private bool ClearStandingsMirror(Dictionary<string, int> Expected,
			out string Failure)
		{
			Failure = null;
			if (ExiledStandings == null)
			{
				Failure = "return cleanup standings mirror is null";
				return false;
			}
			if (ReferenceEquals(Expected, ExiledStandings))
			{
				Failure = "return cleanup standings mirror aliases archive";
				return false;
			}
			if (ExiledStandings.Count == 0) return true;
			if (Expected == null ||
				!KingdomRealmArchive.ExactDictionary(Expected, ExiledStandings))
			{
				Failure = "return cleanup standings mirror reached a third value or alias";
				return false;
			}
			ExiledStandings = new Dictionary<string, int>();
			return true;
		}

		/// <summary>Completes only canonical missing writes from the authoritative TradeClosed
		/// archive. A third scalar, partial collection, or non-equal graph is never overwritten.</summary>
		private bool TryEnsureExileMirrors(KingdomRealmArchive Archive,
			bool AllowCanonicalMissing, out string Failure)
		{
			Failure = null;
			if (Archive == null) { Failure = "exile archive is absent"; return false; }
			if (!EnsureMirrorString(ref ExiledFactionName, Archive.FactionName,
				AllowCanonicalMissing) ||
				!EnsureMirrorString(ref ExiledDisplayName, Archive.DisplayName,
					AllowCanonicalMissing) ||
				!EnsureMirrorString(ref ExiledDeed, Archive.ExileDeed, AllowCanonicalMissing) ||
				!EnsureMirrorTick(ref ExiledTick, Archive.ClosedTick, AllowCanonicalMissing))
			{
				Failure = "exile scalar mirror reached a third value";
				return false;
			}
			if (!EnsureSettlementMirror(ref ExiledSeat, Archive.Seat, AllowCanonicalMissing,
				out Failure) || !EnsureSettlementMirror(ref ExiledAway, Archive.Away,
					AllowCanonicalMissing, out Failure)) return false;
			if (ExiledStandings == null ||
				(AllowCanonicalMissing && ExiledStandings.Count == 0 && Archive.Standings.Count != 0))
			{
				if (!AllowCanonicalMissing)
				{
					Failure = "exile standings mirror is absent";
					return false;
				}
				ExiledStandings = KingdomRealmArchive.CloneStandings(Archive.Standings);
			}
			else if (!KingdomRealmArchive.ExactDictionary(Archive.Standings, ExiledStandings))
			{
				Failure = "exile standings mirror reached a third value";
				return false;
			}
			return true;
		}

		private static bool EnsureMirrorString(ref string Current, string Expected,
			bool AllowCanonicalMissing)
		{
			if (string.Equals(Current, Expected, StringComparison.Ordinal)) return true;
			if (!AllowCanonicalMissing || Current != null) return false;
			Current = Expected;
			return true;
		}

		private static bool EnsureMirrorTick(ref long Current, long Expected,
			bool AllowCanonicalMissing)
		{
			if (Current == Expected) return true;
			if (!AllowCanonicalMissing || Current != 0L) return false;
			Current = Expected;
			return true;
		}

		private static bool EnsureSettlementMirror(ref KingdomSettlement Current,
			KingdomSettlement Expected, bool AllowCanonicalMissing, out string Failure)
		{
			Failure = null;
			if (Expected == null) return Current == null;
			if (Current == null)
			{
				if (!AllowCanonicalMissing)
				{
					Failure = "exile settlement mirror is absent";
					return false;
				}
				return KingdomArchivedSettlementCodec.TryClone(Expected, out Current, out Failure);
			}
			return KingdomArchivedSettlementCodec.ExactGraph(Expected, Current, out Failure);
		}

		private static bool ExactArchivedSettlements(string RealmId,
			KingdomSettlement Seat, KingdomSettlement Away,
			IList<string> ExpectedIds = null)
		{
			List<string> ids = new List<string>();
			if (!ArchivedSettlementMatches(RealmId, Seat, out string seatId))
				return false;
			ids.Add(seatId);
			if (Away != null)
			{
				if (!ArchivedSettlementMatches(RealmId, Away, out string awayId))
					return false;
				ids.Add(awayId);
			}
			KingdomIdentityFault fault;
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, ids, out fault)) return false;
			ids.Sort(StringComparer.Ordinal);
			if (ExpectedIds == null || ids.Count != ExpectedIds.Count) return ExpectedIds == null;
			for (int i = 0; i < ids.Count; i++)
				if (!string.Equals(ids[i], ExpectedIds[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ArchivedSettlementMatches(string RealmId,
			KingdomSettlement Settlement, out string SettlementId)
		{
			SettlementId = Settlement?.City?.SettlementId;
			KingdomIdentityFault fault;
			return Settlement != null && Settlement.ClaimedZones != null &&
				Settlement.ClaimedZones.Contains(Settlement.SettlementIdentityFirstClaimedZone) &&
				KingdomIdentityRules.ReproveSettlement(SettlementId, RealmId,
					Settlement.SettlementIdentityVersion, Settlement.SettlementIdentityOrigin,
					Settlement.SettlementIdentityTransactionId,
					Settlement.SettlementIdentityFoundedTick,
					Settlement.SettlementIdentityFirstClaimedZone, out fault) &&
				Settlement.LifecycleBook != null && !Settlement.LifecycleBook.LegacyIdentity &&
				string.Equals(Settlement.LifecycleBook.SettlementId, SettlementId,
					StringComparison.Ordinal) &&
				KingdomLifecycleRules.CanOwnAuthority(Settlement.LifecycleBook);
		}

	}
}
