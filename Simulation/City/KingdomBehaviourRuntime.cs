using System;
using System.Collections.Generic;

using XRL.World;

using ThousandAndFirst.Api;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Engine edge for the public API-v3 behaviour model. The rules and extension callbacks stay
	/// frozen and engine-free; this class owns the two live effects they cannot perform: replacing
	/// one settlement's durable sidecar and landing one owed work object on attended ground.
	/// </summary>
	internal static class KingdomBehaviourRuntime
	{
		/// <summary>Marker on a work output while its exact placement is being proved.</summary>
		internal const string MaterialisationMarker = "KingdomExtensionMaterialisation";

		/// <summary>
		/// Advances one settlement's sidecar through the same heartbeat/check-in path as the closed
		/// city model. Malformed authority is retained by <see cref="KingdomExtensions"/> and a
		/// faulted owner cannot prevent later owners from running.
		/// </summary>
		internal static void Reckon(KingdomSystem System, KingdomCityBook Book, string Label)
		{
			if (System == null || !System.Founded || Book == null)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!Book.TryRead(out state, out fault))
			{
				KingdomLog.Log("city: behaviour sidecar found the book unreadable (" + fault + ")");
				return;
			}
			string before = Book.ExtensionModel ?? "";
			KingdomCityReading reading = KingdomReadingRules.Project(Label, state, before);
			string after = KingdomExtensions.AdvanceBehaviourModel(System, reading, before);
			if (!string.Equals(after, before, StringComparison.Ordinal))
			{
				Book.ExtensionModel = after;
			}
		}

		/// <summary>
		/// Lands at most one exact, takeable, non-creature object owed by a work in the attended
		/// zone. The acknowledged replacement wire is encoded before ground mutation, then published
		/// only after <c>AddObject</c> leaves that exact object on that exact work cell. One landing is
		/// one medium reify unit and shares the turn-wide city allowance.
		/// </summary>
		internal static void Materialise(KingdomSystem System, KingdomCityBook Book, Zone Z,
			long TimeTicks)
		{
			if (System == null || !System.Founded || Book == null || Z == null || TimeTicks <= 0L
				|| string.IsNullOrEmpty(Book.ExtensionModel))
			{
				return;
			}
			RollAllowance(System, TimeTicks);
			if (System.ReifyThirdsSpent > KingdomCatchUpRules.BudgetThirdsPerTurn
				- KingdomCatchUpRules.ThirdsPerUnit)
			{
				return;
			}
			KingdomBehaviourState state;
			if (!KingdomBehaviourRules.TryDecode(Book.ExtensionModel, out state))
			{
				return;
			}
			Dictionary<int, GameObject> works = KingdomStations.Index(Z);
			for (int i = 0; i < state.WorkCount; i++)
			{
				KingdomWorkBehaviourReading owed;
				GameObject work;
				if (!state.TryWork(i, out owed) || owed.OwedCount <= 0
					|| string.IsNullOrEmpty(owed.OwedBlueprint)
					|| !works.TryGetValue(owed.WorkId, out work)
					|| !GameObject.Validate(work) || work.CurrentCell == null)
				{
					continue;
				}

				// Prove the next authority before touching the world. If the bounded codec cannot
				// represent it, no object is minted and the old debt remains authoritative.
				KingdomBehaviourState acknowledged;
				string replacement;
				if (!KingdomBehaviourRules.TryAcknowledgeMaterialisation(state,
					owed.BehaviourKey, owed.WorkId, owed.OwedBlueprint, 1, out acknowledged)
					|| !KingdomBehaviourRules.TryEncode(acknowledged, out replacement))
				{
					continue;
				}
				string marker = KingdomBehaviourRules.MaterialisationReceipt(owed);
				string legacyMarker = owed.MaterialisationSequence == 0L
					? owed.BehaviourKey + "|" + owed.WorkId + "|" + owed.OwedCount : null;
				GameObject recovered = FindLandedReceipt(work.CurrentCell, owed.OwedBlueprint, marker,
					legacyMarker);
				if (GameObject.Validate(recovered))
				{
					// A save/callback may have interrupted the earlier attempt after AddObject proved
					// ground but before authority published. Exact behaviour/work/generation/count,
					// blueprint, object cardinality, and cell evidence make that same object the receipt.
					// Publish acknowledgement before retiring its marker; a second interruption then
					// sees either live debt + marker or acknowledged debt + harmless stale marker.
					Book.ExtensionModel = replacement;
					System.ReifyThirdsSpent += KingdomCatchUpRules.ThirdsPerUnit;
					try { recovered.RemoveStringProperty(MaterialisationMarker); }
					catch (Exception ex)
					{
						KingdomLog.Log("extension materialisation receipt cleanup threw: " + ex.Message);
					}
					KingdomLog.Log("extension materialisation recovered: " + owed.BehaviourKey
						+ " work=" + owed.WorkId + " found " + owed.OwedBlueprint);
					return;
				}

				GameObject item = null;
				GameObject accepted = null;
				bool landed = false;
				try
				{
					item = GameObject.Create(owed.OwedBlueprint);
					if (!GameObject.Validate(item) || item.Blueprint != owed.OwedBlueprint
						|| item.IsCreature || item.IsPlayer() || item.Physics == null
						|| !item.Physics.Takeable)
					{
						Retire(item, Z);
						KingdomLog.Log("extension materialisation refused: "
							+ owed.OwedBlueprint + " is not one exact takeable object");
						continue;
					}
					item.Count = 1;
					item.SetStringProperty(MaterialisationMarker, marker);
					accepted = work.CurrentCell.AddObject(item, NoStack: true, Silent: true);
					landed = ReferenceEquals(accepted, item)
						&& ReferenceEquals(item.CurrentCell, work.CurrentCell)
						&& item.Count == 1
						&& item.GetStringProperty(MaterialisationMarker) == marker;
				}
				catch (Exception ex)
				{
					// AddObject callbacks may throw after applying their effect. Re-prove exact ground
					// rather than assuming either success or failure from the exception.
					landed = GameObject.Validate(item)
						&& ReferenceEquals(item.CurrentCell, work.CurrentCell)
						&& item.Count == 1
						&& item.GetStringProperty(MaterialisationMarker) == marker;
					KingdomLog.Log("extension materialisation callback threw: " + ex.Message);
				}
				KingdomSurvey.ObserveAddResultInActive(Z, item, accepted);
				if (!landed)
				{
					Retire(item, Z);
					continue;
				}
				// Materialisation runs after the attended survey was bound. Publish the exact
				// landed root before any later same-pass station, trade, or growth decision.
				Book.ExtensionModel = replacement;
				System.ReifyThirdsSpent += KingdomCatchUpRules.ThirdsPerUnit;
				try { item.RemoveStringProperty(MaterialisationMarker); }
				catch (Exception ex)
				{
					KingdomLog.Log("extension materialisation receipt cleanup threw: " + ex.Message);
				}
				KingdomLog.Log("extension materialisation: " + owed.BehaviourKey + " work="
					+ owed.WorkId + " landed " + owed.OwedBlueprint);
				return;
			}
		}

		/// <summary>Finds only the exact already-landed receipt for this exact debt generation. This
		/// is the recovery edge for interruption after ground mutation and before sidecar publication;
		/// a stale generation, moved object, stack, creature, or untakeable object cannot acknowledge.</summary>
		private static GameObject FindLandedReceipt(Cell cell, string blueprint, string marker,
			string legacyMarker)
		{
			if (cell == null || string.IsNullOrEmpty(blueprint) || string.IsNullOrEmpty(marker)) return null;
			foreach (GameObject item in cell.GetObjects())
			{
				if (GameObject.Validate(item) && ReferenceEquals(item.CurrentCell, cell)
					&& item.Blueprint == blueprint && item.Count == 1 && !item.IsCreature
					&& !item.IsPlayer() && item.Physics != null && item.Physics.Takeable)
				{
					string found = item.GetStringProperty(MaterialisationMarker);
					if (found == marker || (!string.IsNullOrEmpty(legacyMarker) && found == legacyMarker))
						return item;
				}
			}
			return null;
		}

		private static void RollAllowance(KingdomSystem System, long TimeTicks)
		{
			if (System.ReifyTick == TimeTicks)
			{
				return;
			}
			System.ReifyTick = TimeTicks;
			System.ReifyThirdsSpent = 0;
			System.ReifyHeavySpent = 0;
		}

		private static void Retire(GameObject Item, Zone ObservedZone)
		{
			if (GameObject.Validate(Item))
			{
				Zone before = ObservedZone ?? Item.CurrentZone;
				try
				{
					Item.Obliterate();
				}
				finally
				{
					// Obliterate callbacks may remove first and throw second.
					KingdomSurvey.ObserveCurrentTopologyInActive(before, Item);
				}
			}
		}
	}
}
