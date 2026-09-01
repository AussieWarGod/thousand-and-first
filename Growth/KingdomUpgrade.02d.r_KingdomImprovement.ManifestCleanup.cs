using System;
using XRL;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		private const string CurrentContentRemovalReceipt = "improvement-handover:v2";
		private const string LegacyContentRemovalReceipt = "improvement-handover:v1";

		internal static bool TryRetireHandoverContentCustody(GameObject Successor,
			KingdomConstructionJob Job, out string Failure)
		{
			Failure = null;
			KingdomConstructionJob current;
			if (!TryGetExactTerminalContentAuthority(Successor, Job, out current))
				return ManifestFailure(out Failure,
					"Terminal construction authority cannot retire content custody.");
			string phaseKey = ManifestKey("CleanupPhase");
			if (Successor.HasStringProperty(phaseKey))
				return ManifestFailure(out Failure, "Content cleanup phase has the wrong type.");
			bool hasPhase = Successor.HasIntProperty(phaseKey);
			int phase = Successor.GetIntProperty(phaseKey);
			if (hasPhase && (phase < 1 || phase > 4))
				return ManifestFailure(out Failure, "Content cleanup phase is malformed.");
			// Phase remains authoritative until every cleanup header is gone. A crash may
			// therefore leave one half of the terminal tombstone beside phase four.
			if (phase == 4) return FinishManifestCleanup(Successor, Job.Id, out Failure);
			if (HasRetiredManifestEvidence(Successor))
			{
				if (!ExactRetiredManifestOnly(Successor, Job.Id))
					return ManifestFailure(out Failure,
						"Retired content custody retains active, malformed, or foreign evidence.");
				return true;
			}
			// Pre-manifest development saves can be migrated only when their immutable v1
			// removal receipt proves zero items and zero liquid. Non-empty legacy custody
			// remains fail-closed because exact identities/composition cannot be reconstructed.
			if (phase == 0 && ExactZeroContentLegacyAuthority(Successor, current))
				return TryPublishZeroContentLegacyRetirement(Successor, current.Id, out Failure);
			if (phase == 3) return RemoveManifestEvidence(Successor, Job.Id, out Failure);
			HandoverManifestState state;
			if (!TryReadManifest(Successor, out state, out Failure)
				|| state.ConstructionReceipt != Job.Id || state.TargetId != Job.OutputId)
				return false;
			if (phase == 0)
			{
				int items;
				int liquid;
				if (!VerifySettledHandoverContentCustody(Successor, Job.Id, out items,
						out liquid, out Failure) || items != state.Count
					|| items != current.PhysicalIndex || liquid != current.PhysicalAmount)
					return ManifestFailure(out Failure, Failure
						?? "Settled content disagrees with terminal removal authority.");
				Successor.SetIntProperty(phaseKey, 1);
				phase = 1;
			}
			if (!ExactTerminalManifestItems(Successor, state, out Failure)) return false;
			if (phase == 1)
			{
				for (int i = 0; i < state.Count; i++)
				{
					object rooted;
					if (The.Game.ObjectGameState.TryGetValue(state.Roots[i], out rooted))
					{
						GameObject item = rooted as GameObject;
						if (!ExactManifestItem(state, i, item))
							return ManifestFailure(out Failure,
								"Content cleanup root points at foreign custody.");
						The.Game.ObjectGameState.Remove(state.Roots[i]);
					}
				}
				Successor.SetIntProperty(phaseKey, 2);
				phase = 2;
			}
			for (int i = 0; i < state.Count; i++)
				if (The.Game.ObjectGameState.ContainsKey(state.Roots[i]))
					return ManifestFailure(out Failure, "Content custody root could not be retired.");
			if (phase == 2)
			{
				if (!ExactManifestIntOrAbsent(Successor, "CleanupCount", state.Count))
					return ManifestFailure(out Failure,
						"Content cleanup count carries a third value.");
				if (!ExactManifestTextOrAbsent(Successor, "CleanupReceipt", Job.Id))
					return ManifestFailure(out Failure,
						"Content cleanup receipt carries a third value.");
				Successor.SetIntProperty(ManifestKey("CleanupCount"), state.Count);
				Successor.SetStringProperty(ManifestKey("CleanupReceipt"), Job.Id);
				Successor.SetIntProperty(phaseKey, 3);
			}
			return RemoveManifestEvidence(Successor, Job.Id, out Failure);
		}

		private static bool TryGetExactTerminalContentAuthority(GameObject Successor,
			KingdomConstructionJob Job, out KingdomConstructionJob Current)
		{
			Current = null;
			return The.Game != null && GameObject.Validate(Successor) && Job != null
				&& BoundedIdentity(Job.Id) && KingdomConstruction.TryFind(Job.Id, out Current)
				&& Current.Route == KingdomConstructionRoute.Improvement
				&& Current.Phase == KingdomConstructionPhase.Complete
				&& (Current.PhysicalPhase == KingdomPhysicalPhase.FinalRemoved
					|| Current.PhysicalPhase == KingdomPhysicalPhase.EffectsPending
					|| Current.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled)
				&& Current.OutputId == Successor.IDIfAssigned
				&& Current.SourceId == Current.SubjectId
				&& Current.PhysicalIndex >= 0 && Current.PhysicalIndex <= 4096
				&& Current.PhysicalAmount >= 0 && Current.PhysicalSpilled == 0
				&& Current.PhysicalItemId == Current.SubjectId
				&& Current.PhysicalDestinationId == Current.OutputId
				&& r_KingdomScaffold.HasRemovalProof(Successor, Current.SubjectId)
				&& (Current.PhysicalReceipt == CurrentContentRemovalReceipt
					|| ExactZeroContentLegacyAuthority(Successor, Current))
				&& KingdomConstruction.IsCurrent(Current);
		}

		private static bool ExactZeroContentLegacyAuthority(GameObject Successor,
			KingdomConstructionJob Job)
		{
			return Job != null && Job.PhysicalReceipt == LegacyContentRemovalReceipt
				&& Job.PhysicalIndex == 0 && Job.PhysicalAmount == 0 && Job.PhysicalSpilled == 0
				&& Job.PhysicalItemId == Job.SubjectId
				&& Job.PhysicalDestinationId == Job.OutputId
				&& r_KingdomScaffold.HasRemovalProof(Successor, Job.SubjectId);
		}

		private static bool TryPublishZeroContentLegacyRetirement(GameObject Successor,
			string Receipt, out string Failure)
		{
			Failure = null;
			if (!ExactManifestIntOrAbsent(Successor, "CleanupCount", 0)
				|| !ExactManifestTextOrAbsent(Successor, "CleanupReceipt", Receipt)
				|| !LegacyRetirementPrefixSafe(Successor))
				return ManifestFailure(out Failure,
					"Legacy zero-content retirement carries foreign manifest evidence.");
			Successor.SetIntProperty(ManifestKey("CleanupCount"), 0);
			Successor.SetStringProperty(ManifestKey("CleanupReceipt"), Receipt);
			Successor.SetIntProperty(ManifestKey("CleanupPhase"), 4);
			return FinishManifestCleanup(Successor, Receipt, out Failure);
		}

		private static bool LegacyRetirementPrefixSafe(GameObject Owner)
		{
			if (Owner.Property != null) foreach (string key in Owner.Property.Keys)
				if (key.StartsWith(HandoverManifestPrefix, StringComparison.Ordinal)
					&& key != ManifestKey("CleanupReceipt")) return false;
			if (Owner.IntProperty != null) foreach (string key in Owner.IntProperty.Keys)
				if (key.StartsWith(HandoverManifestPrefix, StringComparison.Ordinal)
					&& key != ManifestKey("CleanupCount")) return false;
			return true;
		}

		private static bool ExactTerminalManifestItems(GameObject Successor,
			HandoverManifestState State, out string Failure)
		{
			Failure = null;
			Cell where = Successor.CurrentCell;
			if (where == null || The.Game == null)
				return ManifestFailure(out Failure, "Terminal manifest has no loaded destination.");
			for (int i = 0; i < State.Count; i++)
			{
				GameObject item;
				if (KingdomConstruction.FindGlobalLiveId(State.ItemIds[i], out item)
						!= KingdomPhysicalLookupState.Exact || !ExactManifestItem(State, i, item)
					|| !ExactManifestDestination(item, Successor, where, State))
					return ManifestFailure(out Failure,
						"Terminal manifest item is absent, moved, or replaced.");
			}
			return true;
		}

		private static bool RemoveManifestEvidence(GameObject Successor, string ExpectedReceipt,
			out string Failure)
		{
			Failure = null;
			if (!RequiredManifestInt(Successor, "CleanupPhase", out int phase) || phase != 3
				|| !RequiredManifestInt(Successor, "CleanupCount", out int count)
				|| !KingdomUpgradeContentRules.ManifestCardinalityValid(count)
				|| !RequiredManifestText(Successor, "CleanupReceipt", out string receipt)
				|| receipt != ExpectedReceipt)
				return ManifestFailure(out Failure, "Committed content cleanup lost its bound.");
			for (int i = 0; i < count; i++)
			{
				RemoveManifestText(Successor, ManifestEntryKey(i, "Id"));
				RemoveManifestText(Successor, ManifestEntryKey(i, "Blueprint"));
				RemoveManifestInt(Successor, ManifestEntryKey(i, "Count"));
				RemoveManifestText(Successor, ManifestEntryKey(i, "Root"));
			}
			string[] texts = { "SourceId", "TargetId", "ConstructionReceipt", "DestinationId",
				"Digest", "LiquidTargetId", "LiquidConstructionReceipt", "LiquidComposition",
				"LiquidDigest" };
			string[] ints = { "Schema", "Count", "DestinationKind", "LiquidSchema",
				"LiquidMoved", "LiquidHasVessel", "LiquidVolume", "LiquidCapacity" };
			for (int i = 0; i < texts.Length; i++)
				RemoveManifestText(Successor, ManifestKey(texts[i]));
			for (int i = 0; i < ints.Length; i++)
				RemoveManifestInt(Successor, ManifestKey(ints[i]));
			if (!ManifestPayloadAbsent(Successor, count))
				return ManifestFailure(out Failure,
					"Content cleanup left an active header or manifest entry.");
			Successor.SetIntProperty(ManifestKey("CleanupPhase"), 4);
			return FinishManifestCleanup(Successor, ExpectedReceipt, out Failure);
		}

		private static bool FinishManifestCleanup(GameObject Successor, string ExpectedReceipt,
			out string Failure)
		{
			Failure = null;
			string phaseKey = ManifestKey("CleanupPhase");
			if (!RequiredManifestInt(Successor, "CleanupPhase", out int phase) || phase != 4)
				return ManifestFailure(out Failure, "Content cleanup completion is malformed.");
			string cleanupReceiptKey = ManifestKey("CleanupReceipt");
			if (Successor.HasIntProperty(cleanupReceiptKey))
				return ManifestFailure(out Failure, "Content cleanup receipt has the wrong type.");
			string receipt = Successor.GetStringProperty(cleanupReceiptKey);
			if (receipt == null
				&& !RequiredManifestText(Successor, "RetiredReceipt", out receipt))
				return ManifestFailure(out Failure, "Content cleanup lost its terminal receipt.");
			if (!BoundedIdentity(receipt) || receipt != ExpectedReceipt
				|| !ExactManifestTextOrAbsent(Successor, "RetiredReceipt", receipt)
				|| !ExactManifestIntOrAbsent(Successor, "RetiredSchema", 1))
				return ManifestFailure(out Failure, "Content cleanup tombstone cannot be published.");
			Successor.SetStringProperty(ManifestKey("RetiredReceipt"), receipt);
			Successor.SetIntProperty(ManifestKey("RetiredSchema"), 1);
			Successor.RemoveIntProperty(ManifestKey("CleanupCount"));
			Successor.RemoveStringProperty(cleanupReceiptKey);
			Successor.RemoveIntProperty(phaseKey);
			if (!ExactRetiredManifestOnly(Successor, receipt))
				return ManifestFailure(out Failure, "Content cleanup left durable manifest evidence.");
			return true;
		}

		private static bool ManifestPayloadAbsent(GameObject Owner, int Count)
		{
			if (!KingdomUpgradeContentRules.ManifestCardinalityValid(Count)) return false;
			if (Owner.Property != null) foreach (string key in Owner.Property.Keys)
				if (key.StartsWith(HandoverManifestPrefix, StringComparison.Ordinal)
					&& key != ManifestKey("CleanupReceipt")) return false;
			if (Owner.IntProperty != null) foreach (string key in Owner.IntProperty.Keys)
				if (key.StartsWith(HandoverManifestPrefix, StringComparison.Ordinal)
					&& key != ManifestKey("CleanupPhase")
					&& key != ManifestKey("CleanupCount")) return false;
			return true;
		}

		private static bool ExactRetiredManifestOnly(GameObject Owner, string Receipt)
		{
			if (!RequiredManifestInt(Owner, "RetiredSchema", out int schema) || schema != 1
				|| !RequiredManifestText(Owner, "RetiredReceipt", out string retired)
				|| retired != Receipt) return false;
			if (Owner.Property != null) foreach (string key in Owner.Property.Keys)
				if (key.StartsWith(HandoverManifestPrefix, StringComparison.Ordinal)
					&& key != ManifestKey("RetiredReceipt")) return false;
			if (Owner.IntProperty != null) foreach (string key in Owner.IntProperty.Keys)
				if (key.StartsWith(HandoverManifestPrefix, StringComparison.Ordinal)
					&& key != ManifestKey("RetiredSchema")) return false;
			return true;
		}

		private static bool HasRetiredManifestEvidence(GameObject Owner)
		{
			return Owner != null && (Owner.HasIntProperty(ManifestKey("RetiredSchema"))
				|| Owner.HasStringProperty(ManifestKey("RetiredSchema"))
				|| Owner.HasIntProperty(ManifestKey("RetiredReceipt"))
				|| Owner.HasStringProperty(ManifestKey("RetiredReceipt")));
		}

		private static void RemoveManifestText(GameObject Owner, string Key)
		{
			Owner.RemoveStringProperty(Key);
		}

		private static void RemoveManifestInt(GameObject Owner, string Key)
		{
			Owner.RemoveIntProperty(Key);
		}
	}
}
