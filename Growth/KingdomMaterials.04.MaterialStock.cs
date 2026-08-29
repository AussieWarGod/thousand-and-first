using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomMaterials
	{

		/// <summary>
		/// What the dedicated stockpiles on one ground hold, and the means to spend it and fill
		/// it. A snapshot: spending and delivering through this object keep its tally correct,
		/// but an item placed after it was taken does not retroactively appear in it &mdash; the
		/// same contract <c>KingdomSurvey</c> holds for water and food.
		/// </summary>
		public sealed class MaterialStock
		{
			/// <summary>Units of each material the dedicated stockpiles hold.</summary>
			public readonly KingdomMaterialTally Tally = new KingdomMaterialTally();

			/// <summary>Bits the dedicated stockpiles are worth, by tier. Not a separate store and
			/// never was: bits are whatever tinkering stock the founder put in with everything
			/// else, counted the way the workshop would count it.</summary>
			public readonly KingdomBitTally Bits = new KingdomBitTally();

			/// <summary>Rare finds the dedicated stockpiles hold.</summary>
			public readonly KingdomExoticTally Exotics = new KingdomExoticTally();

			/// <summary>The dedicated containers, in the order found. Spending walks these, so
			/// nothing is ever drawn from something the founder did not dedicate.</summary>
			public readonly List<GameObject> Stockpiles = new List<GameObject>();

			/// <summary>Zone this stock was taken from. Null for an empty stock.</summary>
			public Zone Zone;

			/// <summary>Exact routed-input exclusions captured with this physical snapshot.</summary>
			internal KingdomConstructionInputLeaseSnapshot InputLeases;
			internal bool InputLeaseAuthorityExact;
			internal string InputLeaseFailure;

			/// <summary>True when the founder has dedicated no stockpile here at all, which is a
			/// different thing from having dedicated an empty one.</summary>
			public bool None => Stockpiles.Count == 0;

			/// <summary>
			/// Destroys up to Units of one material from the stockpiles, in the order found.
			/// Counts what actually left rather than what was asked for: an item whose
			/// destruction something else vetoes stops the draw instead of being counted as
			/// spent.
			/// </summary>
			/// <returns>Units actually taken, never more than were there.</returns>
			public int Take(KingdomMaterial Material, int Units)
			{
				KingdomConstructionInputLeaseSnapshot leases;
				string leaseFailure;
				if (Units <= 0 || !KingdomConstructionInputLeaseAuthority.TryCapture(
					out leases, out leaseFailure)) return 0;
				int remaining = Units;
				for (int i = 0; i < Stockpiles.Count && remaining > 0; i++)
				{
					GameObject container = Stockpiles[i];
					if (container.Inventory == null)
					{
						continue;
					}
					// Snapshot first: destroying an item below removes it from this same
					// Inventory list, and mutating a collection mid-foreach throws.
					List<GameObject> held = new List<GameObject>(container.Inventory.Objects);
					bool changed = false;
					for (int j = 0; j < held.Count && remaining > 0; j++)
					{
						GameObject item = held[j];
						if (!KingdomConstructionInputLeaseAuthority.CanUseMaterial(leases, item)
							|| !TryOrdinaryMaterialOf(item, out var kind) || kind != Material)
						{
							continue;
						}
						// Destroy() on a stack of more than one decrements it by exactly one and
						// leaves the object in place (Stacker.HandleEvent(BeforeDestroyObjectEvent));
						// only the last unit removes it. The count is read before and after rather
						// than inferred from the call, so a refused destruction never reads as a
						// unit spent.
						while (remaining > 0 && GameObject.Validate(item))
						{
							string mutationLeaseFailure;
							if (!KingdomConstructionInputLeaseAuthority
								.TryObjectAvailableForLocalDebit(item,
									out mutationLeaseFailure)) break;
							int before = item.Count;
							try { item.Destroy(null, Silent: true); }
							catch
							{
								KingdomSurvey.ObserveCurrentTopologyInActive(Zone, container);
								throw;
							}
							if (GameObject.Validate(item) && item.Count >= before)
							{
								break;
							}
							changed = true;
							remaining--;
						}
					}
					if (changed) KingdomSurvey.ObserveChangedInActive(Zone, container);
				}
				int taken = Units - remaining;
				Tally.Add(Material, -taken);
				return taken;
			}

			/// <summary>
			/// Legacy immediate material-only draw. New work should reserve a composite
			/// <see cref="KingdomMaterialDebit"/> before making its durable job. This wrapper returns
			/// true only for an exact commit; any engine veto is measured and logged by the receipt,
			/// never described as an all-or-nothing refusal after a terminal source vanished.
			/// </summary>
			public bool Spend(KingdomMaterialTally Cost)
			{
				KingdomMaterialDebit debit = KingdomMaterialDebit.Reserve(this,
					new KingdomMaterialDebitCost(Cost, null, null));
				KingdomMaterialDebitResult result = debit.Commit();
				LogLegacyPartial("material", result);
				return result.Exact;
			}

			/// <summary>
			/// Legacy immediate bit-only receipt. Bits are not held loose &mdash; the settlement
			/// holds SCRAP, and the keepers break up whatever answers the price, exactly as a
			/// tinker would. Dynamic vetoes remain explicitly classified by the receipt.
			/// <para>
			/// A piece broken up for one bit gives up whatever else was in it, and that surplus is
			/// gone. That is honest and it is the reason a design is priced in cheap tiers wherever
			/// it can be: nobody breaks an AI master unit for the tier-zero bit in it if there is a
			/// bent metal sheet on the shelf, and this walks the shelf cheapest-first so it does not
			/// either.
			/// </para>
			/// </summary>
			/// <returns>True only when the exact receipt committed.</returns>
			public bool SpendBits(KingdomBitTally Cost)
			{
				KingdomMaterialDebit debit = KingdomMaterialDebit.Reserve(this,
					new KingdomMaterialDebitCost(null, Cost, null));
				KingdomMaterialDebitResult result = debit.Commit();
				LogLegacyPartial("bit", result);
				return result.Exact;
			}

			/// <summary>
			/// Legacy immediate exotic-only receipt. A gemstone is a gemstone: the keepers take
			/// the first one that answers, because nothing here is worth more to a wall than any
			/// other of its kind. Dynamic vetoes remain explicitly classified by the receipt.
			/// </summary>
			/// <returns>True only when the exact receipt committed.</returns>
			public bool SpendExotics(KingdomExoticTally Cost)
			{
				KingdomMaterialDebit debit = KingdomMaterialDebit.Reserve(this,
					new KingdomMaterialDebitCost(null, null, Cost));
				KingdomMaterialDebitResult result = debit.Commit();
				LogLegacyPartial("exotic", result);
				return result.Exact;
			}

			private static void LogLegacyPartial(string Lane, KingdomMaterialDebitResult Result)
			{
				if (Result != null && Result.Partial)
				{
					KingdomLog.Log("materials: legacy " + Lane + " draw ended " + Result.Outcome
						+ "; outstanding=" + Result.Outstanding.ToClaimString());
				}
			}

			/// <summary>
			/// Puts real items into the stockpiles, and onto the ground when there is nowhere
			/// else for them. Material is never held in the abstract: what the settlement earned
			/// exists somewhere a founder can walk to and pick up.
			/// </summary>
			/// <param name="Material">Which material.</param>
			/// <param name="Units">How many units. Zero and negative do nothing.</param>
			/// <param name="Fallback">Cell the overflow is dropped in when no stockpile can take
			/// it. Null discards the overflow rather than losing track of it, and is only ever
			/// passed by a caller with no ground to drop on.</param>
			/// <returns>Units that went on the ground instead of into a stockpile.</returns>
			public int Put(KingdomMaterial Material, int Units, Cell Fallback)
			{
				if (Units <= 0)
				{
					return 0;
				}
				string blueprint = BlueprintFor(Material);
				if (string.IsNullOrEmpty(blueprint))
				{
					return 0;
				}
				GameObject container = null;
				for (int i = 0; i < Stockpiles.Count; i++)
				{
					if (Stockpiles[i].Inventory != null)
					{
						container = Stockpiles[i];
						break;
					}
				}
				int placed = 0;
				int spilled = 0;
				int remaining = Units;
				while (remaining > 0)
				{
					GameObject item = GameObject.Create(blueprint);
					if (item == null)
					{
						break;
					}
					int batch = 1;
					if (item.HasPart("Stacker") && remaining > 1)
					{
						batch = remaining;
						item.Count = batch;
					}
					if (container != null)
					{
						GameObject accepted = null;
						// A deposit must never merge into an exact stack another durable receipt
						// owns. NoStack keeps both identities observable across engine callbacks.
						try { accepted = container.Inventory.AddObject(item, null,
							Silent: true, NoStack: true); }
						catch
						{
							KingdomSurvey.ObserveCurrentTopologyInActive(Zone, container);
							KingdomSurvey.ObserveAddResultInActive(Zone, item, accepted);
							throw;
						}
						KingdomSurvey.ObserveChangedInActive(Zone, container);
						KingdomSurvey.ObserveAddResultInActive(Zone, item, accepted);
						placed += batch;
					}
					else if (Fallback != null)
					{
						GameObject accepted;
						try { accepted = Fallback.AddObject(item); }
						catch
						{
							KingdomSurvey.ObserveAddResultInActive(Zone, item, null);
							throw;
						}
						KingdomSurvey.ObserveAddResultInActive(Zone, item, accepted);
						spilled += batch;
					}
					else
					{
						item.Obliterate();
					}
					remaining -= batch;
				}
				Tally.Add(Material, placed + spilled);
				return spilled;
			}

			/// <summary>Puts a whole tally away, reporting how much of it ended up on the
			/// ground.</summary>
			public int PutAll(KingdomMaterialTally Yield, Cell Fallback)
			{
				int spilled = 0;
				if (Yield == null)
				{
					return 0;
				}
				for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				{
					KingdomMaterial material = (KingdomMaterial)i;
					spilled += Put(material, Yield.Get(material), Fallback);
				}
				return spilled;
			}
		}
	}
}
