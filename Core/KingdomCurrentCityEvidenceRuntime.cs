using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Exact loaded-city facts shared by D1 and D12. Never loads or mutates a zone.</summary>
	internal static class KingdomCurrentCityEvidenceRuntime
	{
		internal sealed class Context
		{
			internal KingdomSystem System;
			internal Zone Zone;
			internal KingdomSurvey Survey;
			internal string SettlementId;
			internal string Vocation;
			internal string Style;
			internal string Terrain;
			internal string Region;
			internal string FoundingTransactionId;
			internal string FoundingZoneId;
			internal long FoundedTick;
		}

		internal sealed class Work
		{
			internal GameObject Object;
			internal KingdomSiteBuiltWorkEvidence Evidence;
		}

		/// <summary>Detached direct-ground proof used by Refuge; no live object escapes.</summary>
		internal sealed class BuiltWorkSnapshot
		{
			internal string SettlementId { get; }
			internal string DesignKey { get; }
			internal string WorkReceiptId { get; }
			internal string DisplayName { get; }
			internal long CompletedTick { get; }

			internal BuiltWorkSnapshot(KingdomSiteBuiltWorkEvidence value)
			{
				SettlementId = value.SettlementId; DesignKey = value.DesignKey;
				WorkReceiptId = value.WorkReceiptId; DisplayName = value.DisplayName;
				CompletedTick = value.CompletedTick;
			}
		}

		internal static bool TryContext(KingdomSystem system, Zone zone,
			KingdomSurvey survey, bool requireSurvey, out Context context,
			out string failure)
		{
			context = null;
			failure = null;
			if (system == null || zone == null || The.Game == null ||
				!ReferenceEquals(The.Game.GetSystem<KingdomSystem>(), system) ||
				!GameObject.Validate(The.Player) || !ReferenceEquals(The.Player.CurrentZone, zone))
			{
				failure = "The Charter bearer is not standing in this loaded city zone.";
				return false;
			}
			if (!system.TryGetCurrentIdentity(out string _, out string settlementId) ||
				!string.Equals(system.SettlementIdForOwnedZone(zone.ZoneID), settlementId,
					StringComparison.Ordinal) || system.City == null ||
				!string.Equals(system.City.SettlementId, settlementId, StringComparison.Ordinal) ||
				system.ClaimedZones == null || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				failure = "The loaded ground is not the exact current owned city.";
				return false;
			}
			if (requireSurvey && (survey == null || !ReferenceEquals(survey.Ground, zone)))
			{
				failure = "The current city has no matching one-pass ground survey.";
				return false;
			}
			context = new Context
			{
				System = system,
				Zone = zone,
				Survey = survey,
				SettlementId = settlementId,
				Vocation = system.Vocation,
				Style = system.Style,
				Terrain = system.FoundingTerrainBlueprint,
				Region = system.FoundingRegionName,
				FoundingTransactionId = system.SettlementIdentityTransactionId,
				FoundingZoneId = system.SettlementIdentityFirstClaimedZone,
				FoundedTick = system.SettlementIdentityFoundedTick
			};
			return true;
		}

		internal static bool TryBuiltWorks(Context context, out List<Work> works,
			out string failure)
		{
			if (context?.Survey?.Built == null || context.System?.City == null)
			{
				works = null;
				failure = "The current-city work survey is absent.";
				return false;
			}
			return TryBuiltWorksFrom(context, context.Survey.Built, out works, out failure);
		}

		/// <summary>Direct immutable shelter evidence. No survey, migration, identity mint, or log.</summary>
		internal static bool TryBuiltWorksReadOnly(Context context,
			out List<BuiltWorkSnapshot> snapshots,
			out string failure)
		{
			snapshots = null; failure = null;
			if (context?.Zone == null || context.System?.City == null)
			{
				failure = "The current-city ground is absent.";
				return false;
			}
			List<GameObject> roots = context.Zone.GetObjects();
			List<GameObject> built = new List<GameObject>();
			for (int i = 0; i < roots.Count; i++)
				if (GameObject.Validate(roots[i]) && roots[i].GetIntProperty("KingdomBuilt") == 1)
					built.Add(roots[i]);
			if (!TryBuiltWorksFrom(context, built, out List<Work> works, out failure)) return false;
			snapshots = new List<BuiltWorkSnapshot>(works.Count);
			for (int i = 0; i < works.Count; i++)
				snapshots.Add(new BuiltWorkSnapshot(works[i].Evidence));
			return true;
		}

		private static bool TryBuiltWorksFrom(Context context, IList<GameObject> built,
			out List<Work> works, out string failure)
		{
			works = null; failure = null;
			Dictionary<string, int> receiptCounts = CountReceipts(built);
			List<Work> candidates = new List<Work>();
			for (int i = 0; i < built.Count; i++)
			{
				GameObject item = built[i];
				if (!TryWork(context, item, receiptCounts, out Work candidate)) continue;
				candidates.Add(candidate);
			}
			candidates.Sort(CompareWork);
			if (candidates.Count == 0)
			{
				failure = "No exact completed work is jointly proved by ground, city book, and construction receipt.";
				return false;
			}
			works = candidates;
			return true;
		}

		private static Dictionary<string, int> CountReceipts(IList<GameObject> built)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < built.Count; i++)
			{
				GameObject item = built[i];
				if (!GameObject.Validate(item)) continue;
				string receipt = item.GetStringProperty(KingdomConstruction.ReceiptProperty);
				if (string.IsNullOrEmpty(receipt)) continue;
				counts.TryGetValue(receipt, out int count);
				counts[receipt] = count + 1;
			}
			return counts;
		}

		private static bool TryWork(Context context, GameObject item,
			IDictionary<string, int> receiptCounts, out Work result)
		{
			result = null;
			if (!GameObject.Validate(item) || item.GetIntProperty("KingdomBuilt") != 1 ||
				!ReferenceEquals(item.CurrentZone, context.Zone) || item.CurrentCell == null ||
				string.IsNullOrEmpty(item.IDIfAssigned)) return false;
			string receipt = item.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receipt) || !receiptCounts.TryGetValue(receipt, out int count) ||
				count != 1 || !KingdomConstruction.TryFind(receipt, out KingdomConstructionJob job) ||
				!KingdomConstructionRules.ValidJob(job) || job.Phase != KingdomConstructionPhase.Complete ||
				!KingdomConstruction.Owns(context.System, context.Zone, job) ||
				job.OutputId != item.IDIfAssigned || !ClosureProved(job)) return false;
			string designKey = KingdomUpgrade.DesignKeyOf(item);
			if (string.IsNullOrEmpty(designKey) || designKey != job.TargetKey ||
				!OneExactCityRow(context.System.City, item)) return false;
			string display = KingdomUpgrade.DisplayNameOf(designKey);
			if (string.IsNullOrWhiteSpace(display)) return false;
			result = new Work
			{
				Object = item,
				Evidence = new KingdomSiteBuiltWorkEvidence
				{
					SettlementId = context.SettlementId,
					ZoneId = context.Zone.ZoneID,
					ObjectId = "taf:object:" + item.IDIfAssigned,
					DesignKey = designKey,
					WorkReceiptId = "taf:construction:" + receipt,
					DisplayName = display,
					CompletedTick = job.UpdatedTick
				}
			};
			return true;
		}

		private static bool ClosureProved(KingdomConstructionJob job)
		{
			return job.Compacted || KingdomConstructionRules.TerminalClosureSettled(job);
		}

		private static bool OneExactCityRow(KingdomCityBook book, GameObject item)
		{
			if (book.WorkIds == null || book.WorkZoneIds == null || book.WorkAnchorsX == null ||
				book.WorkAnchorsY == null || book.WorkDesignKeys == null) return false;
			int stableId = KingdomCityRules.StableId(item.IDIfAssigned);
			int matches = 0;
			for (int i = 0; i < book.WorkIds.Count; i++)
			{
				if (i >= book.WorkZoneIds.Count || i >= book.WorkAnchorsX.Count ||
					i >= book.WorkAnchorsY.Count || i >= book.WorkDesignKeys.Count) return false;
				if (book.WorkIds[i] == stableId && book.WorkZoneIds[i] == item.CurrentZone.ZoneID &&
					book.WorkAnchorsX[i] == item.CurrentCell.X &&
					book.WorkAnchorsY[i] == item.CurrentCell.Y &&
					book.WorkDesignKeys[i] == item.Blueprint) matches++;
			}
			return matches == 1;
		}

		private static int CompareWork(Work left, Work right)
		{
			int order = string.CompareOrdinal(left.Evidence.WorkReceiptId,
				right.Evidence.WorkReceiptId);
			return order != 0 ? order : string.CompareOrdinal(left.Evidence.ObjectId,
				right.Evidence.ObjectId);
		}
	}
}
