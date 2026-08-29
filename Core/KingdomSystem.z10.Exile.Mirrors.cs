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
				|| ExiledStandings == null || ExiledRealmPolicyToward == null ||
				ExiledRegardSpilloverRemainders == null ||
				ExiledRegardSpilloverObservedReputation == null) return false;
			string failure;
			if (!Archive.ExactMirrors(ExiledFactionName, ExiledDisplayName, ExiledDeed,
				ExiledTick, ExiledSeat, ExiledSettlementTopology, ExiledStandings,
				ExiledRealmPolicyToward, ExiledRegardSpilloverRemainders,
				ExiledRegardSpilloverObservedReputation,
				out failure)) return false;
			if (!ExactArchivedSettlements(Archive.RealmId, ExiledSeat,
				ExiledSettlementTopology,
				Archive.SettlementIds)) return false;
			KingdomSettlement currentSeat;
			try { currentSeat = Capture(); }
			catch { return false; }
			List<object> currentRoots = new List<object> { currentSeat };
			for (int i = 0; i < NonSeatSettlementCount; i++)
				currentRoots.Add(NonSeatSettlementAt(i));
			currentRoots.AddRange(new object[] { Seceded, RegardForRealm,
				RealmPolicyToward, RegardSpilloverRemainders,
				RegardSpilloverObservedReputation, Bindings, Jobs,
				ChronicleEntries, OutsiderEntries, Haul, CarryBook });
			List<object> mirrorRoots = new List<object> { ExiledSeat };
			for (int i = 0; i < (ExiledSettlementTopology?.Count ?? 0); i++)
				mirrorRoots.Add(ExiledSettlementTopology.Get(i));
			mirrorRoots.AddRange(new object[] { ExiledStandings, ExiledRealmPolicyToward,
				ExiledRegardSpilloverRemainders,
				ExiledRegardSpilloverObservedReputation });
			return KingdomArchivedSettlementCodec.DisjointMutableGraphs(currentRoots.ToArray(),
				mirrorRoots.ToArray(), out failure);
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
				!ClearTopologyMirror(Archive.SettlementTopology, out Failure) ||
				!ClearStandingsMirror(ref ExiledStandings, Archive.Standings,
					"regard", out Failure) ||
				!ClearStandingsMirror(ref ExiledRealmPolicyToward,
					Archive.RealmPolicyToward, "policy", out Failure) ||
				!ClearStandingsMirror(ref ExiledRegardSpilloverRemainders,
					Archive.RegardSpilloverRemainders, "spillover", out Failure) ||
				!ClearStandingsMirror(ref ExiledRegardSpilloverObservedReputation,
				Archive.RegardSpilloverObservedReputation, "spillover observation",
					out Failure) ||
				!ClearMirrorString(ref ExiledDeed, Archive.ExileDeed) ||
				!ClearMirrorTick(ref ExiledTick, Archive.ClosedTick))
			{
				Failure = Failure ?? "return cleanup mirror reached a third value";
				return false;
			}
			SynchronizeLegacyExiledProjection();
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

		private bool ClearTopologyMirror(KingdomSettlementTopology Expected,
			out string Failure)
		{
			Failure = null;
			if (ExiledSettlementTopology == null)
			{
				Failure = "return cleanup topology mirror is null";
				return false;
			}
			if (ExiledSettlementTopology.Count == 0 &&
				!ExiledSettlementTopology.HasOpaqueEvidence) return true;
			if (!ExactTopologyMirror(Expected, ExiledSettlementTopology, out Failure))
				return false;
			ExiledSettlementTopology = new KingdomSettlementTopology();
			return true;
		}

		private static bool ClearStandingsMirror(ref Dictionary<string, int> Current,
			Dictionary<string, int> Expected, string Label, out string Failure)
		{
			Failure = null;
			if (Current == null)
			{
				Failure = "return cleanup " + Label + " mirror is null";
				return false;
			}
			if (ReferenceEquals(Expected, Current))
			{
				Failure = "return cleanup " + Label + " mirror aliases archive";
				return false;
			}
			if (Current.Count == 0) return true;
			if (Expected == null ||
				!KingdomRealmArchive.ExactDictionary(Expected, Current))
			{
				Failure = "return cleanup " + Label + " mirror reached a third value or alias";
				return false;
			}
			Current = new Dictionary<string, int>();
			return true;
		}

		/// <summary>Completes only canonical missing writes from the authoritative TradeClosed
		/// archive. A third scalar, partial collection, or non-equal graph is never overwritten.</summary>
		private bool TryEnsureExileMirrors(KingdomRealmArchive Archive,
			bool AllowCanonicalMissing, bool AllowDirectionalMissing, out string Failure)
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
				out Failure) || !EnsureTopologyMirror(Archive.SettlementTopology,
					AllowCanonicalMissing, out Failure)) return false;
			if (!EnsureStandingsMirror(ref ExiledStandings, Archive.Standings,
				"regard", AllowCanonicalMissing, out Failure) ||
				!EnsureStandingsMirror(ref ExiledRealmPolicyToward,
					Archive.RealmPolicyToward, "policy",
					AllowCanonicalMissing || AllowDirectionalMissing,
					out Failure) ||
				!EnsureStandingsMirror(ref ExiledRegardSpilloverRemainders,
					Archive.RegardSpilloverRemainders, "spillover",
					AllowCanonicalMissing || AllowDirectionalMissing,
					out Failure) ||
				!EnsureStandingsMirror(ref ExiledRegardSpilloverObservedReputation,
					Archive.RegardSpilloverObservedReputation, "spillover observation",
					AllowCanonicalMissing || AllowDirectionalMissing,
					out Failure)) return false;
			SynchronizeLegacyExiledProjection();
			return true;
		}

		private static bool EnsureStandingsMirror(ref Dictionary<string, int> Current,
			Dictionary<string, int> Expected, string Label, bool AllowCanonicalMissing,
			out string Failure)
		{
			Failure = null;
			if (Expected == null) { Failure = "archive " + Label + " is absent"; return false; }
			if (Current == null ||
				(AllowCanonicalMissing && Current.Count == 0 && Expected.Count != 0))
			{
				if (!AllowCanonicalMissing)
				{
					Failure = "exile " + Label + " mirror is absent";
					return false;
				}
				Current = KingdomRealmArchive.CloneStandings(Expected);
				return true;
			}
			if (ReferenceEquals(Current, Expected) ||
				!KingdomRealmArchive.ExactDictionary(Expected, Current))
			{
				Failure = "exile " + Label + " mirror reached a third value";
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

		private bool EnsureTopologyMirror(KingdomSettlementTopology Expected,
			bool AllowCanonicalMissing, out string Failure)
		{
			Failure = null;
			bool missing = ExiledSettlementTopology == null ||
				(ExiledSettlementTopology.Count == 0 &&
				 !ExiledSettlementTopology.HasOpaqueEvidence && Expected?.Count > 0);
			if (missing)
			{
				if (!AllowCanonicalMissing || Expected == null)
				{
					Failure = "exile topology mirror is absent";
					return false;
				}
				return Expected.TryClone(out ExiledSettlementTopology, out Failure);
			}
			return ExactTopologyMirror(Expected, ExiledSettlementTopology, out Failure);
		}

	}
}
