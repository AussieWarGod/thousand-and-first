using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		public static bool StampAdopted(GameObject Adopted, KingdomRules.BuildEntry Entry)
		{
			if (Adopted == null || Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				return false;
			}
			Cell cell = Adopted.CurrentCell;
			if (cell == null || !KingdomPlotRules.TryDimensions(spec.Size, out var width, out var height))
			{
				return false;
			}
			KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(
				cell.X - (width - 1) / 2, cell.Y - (height - 1) / 2,
				cell.X - (width - 1) / 2 + width - 1, cell.Y - (height - 1) / 2 + height - 1);
			string plotId = "adopted:" + Adopted.ID;
			try
			{
				// Schema/ownership marker last: a reader never treats partial geometry as an
				// adoption-owned plot, and release never clears somebody else's coordinates.
				Adopted.RemoveIntProperty(AdoptedPlotProperty);
				StampRect(Adopted, rect);
				Adopted.SetStringProperty(PlotIdProperty, plotId);
				Adopted.SetIntProperty(AdoptedPlotProperty, 1);
			}
			catch
			{
				try
				{
					Adopted.RemoveIntProperty(PlotX2Property);
					Adopted.RemoveIntProperty(PlotX1Property);
					Adopted.RemoveIntProperty(PlotY1Property);
					Adopted.RemoveIntProperty(PlotY2Property);
					Adopted.RemoveStringProperty(PlotIdProperty);
					Adopted.RemoveIntProperty(AdoptedPlotProperty);
				}
				catch { }
				return false;
			}
			bool exact = Adopted.GetIntProperty(AdoptedPlotProperty) == 1
				&& Adopted.GetStringProperty(PlotIdProperty) == plotId
				&& TryReadRect(Adopted, out var observed) && SameRect(observed, rect);
			if (!exact) ReleaseAdoptedPlot(Adopted);
			return exact;
		}

		/// <summary>Releases only plot geometry minted by <see cref="StampAdopted"/>. The positive
		/// ownership marker prevents release from erasing coordinates another system or mod owns.
		/// The rect's presence field is removed first and the ownership marker last, so partial
		/// cleanup cannot leave a ghost reservation and remains recognisably adoption-owned.</summary>
		public static void ReleaseAdoptedPlot(GameObject Adopted)
		{
			if (Adopted == null || Adopted.GetIntProperty(AdoptedPlotProperty) != 1) return;
			Adopted.RemoveIntProperty(PlotX2Property);
			Adopted.RemoveIntProperty(PlotX1Property);
			Adopted.RemoveIntProperty(PlotY1Property);
			Adopted.RemoveIntProperty(PlotY2Property);
			Adopted.RemoveStringProperty(PlotIdProperty);
			Adopted.RemoveIntProperty(AdoptedPlotProperty);
		}

		// --- Staking foresight ------------------------------------------------------------

		/// <summary>The tier actually staked: the founder's choice, floored at the ground the
		/// design itself asks for. A plot is never staked smaller than the building on it.</summary>
		public static KingdomPlotRules.PlotSize StakedSize(KingdomPlotRules.PlotSpec Spec, KingdomPlotRules.PlotSize Stake)
		{
			if (Spec == null)
			{
				return Stake;
			}
			return (Stake == KingdomPlotRules.PlotSize.None || Stake < Spec.Size) ? Spec.Size : Stake;
		}

		/// <summary>
		/// Every tier a design will ever grow into, in order, with the ground each one stands on.
		/// This is what the founder is shown before the stake goes in: the whole chain's
		/// footprints, so staking wide or staking tight is a decision made with the ceiling in
		/// view rather than discovered years later.
		/// <para>
		/// Walks the improvement chain by key and stops the moment it repeats one, so a
		/// third-party chain that rings does not hang the commission screen. The catalogue
		/// validator reports the ring separately; this just refuses to walk it.
		/// </para>
		/// </summary>
		/// <returns>An empty list for a design that is not a plot at all.</returns>
		public static List<KingdomPlotRules.ChainStep> ChainOf(KingdomRules.BuildEntry Entry)
		{
			List<KingdomPlotRules.ChainStep> steps = new List<KingdomPlotRules.ChainStep>();
			List<string> walked = new List<string>();
			KingdomRules.BuildEntry at = Entry;
			while (at != null && !walked.Contains(at.Key))
			{
				walked.Add(at.Key);
				if (!TryGetSpec(at.Key, out var spec) || !KingdomPlotRules.TryFootprint(spec, out var width, out var height))
				{
					break;
				}
				steps.Add(new KingdomPlotRules.ChainStep(at.Key, at.Name, width, height, spec.Roof));
				if (!KingdomUpgrade.TryGetChain(at.Key, out var chain) || chain == null || !chain.Defined
					|| !KingdomData.TryGetBuilding(chain.SuccessorKey, out var next))
				{
					break;
				}
				at = next;
			}
			return steps;
		}

		/// <summary>The tiers of plot a founder may stake for this design right now, smallest
		/// first, for a picker. Empty when the design is not a plot or the settlement cannot lay
		/// one yet, in which case the ordinary stage refusal says why.</summary>
		public static List<KingdomPlotRules.PlotSize> StakeableSizes(KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			if (System == null || Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				return new List<KingdomPlotRules.PlotSize>();
			}
			List<KingdomPlotRules.PlotSize> sizes = KingdomPlotRules.StakeableSizes(
				spec.Size, System.Stage, ChainOf(Entry));
			// A size shown in the picker is a promise of one exact typed plan. Missing exact-size
			// architecture is never offered and never falls back to the design's minimum map.
			for (int i = sizes.Count - 1; i >= 0; i--)
			{
				ArchitectureLotSize architectureSize;
				switch (sizes[i])
				{
				case KingdomPlotRules.PlotSize.Small:
					architectureSize = ArchitectureLotSize.Small;
					break;
				case KingdomPlotRules.PlotSize.Medium:
					architectureSize = ArchitectureLotSize.Medium;
					break;
				case KingdomPlotRules.PlotSize.Large:
					architectureSize = ArchitectureLotSize.Large;
					break;
				case KingdomPlotRules.PlotSize.Huge:
					architectureSize = ArchitectureLotSize.Huge;
					break;
				default:
					sizes.RemoveAt(i);
					continue;
				}
				KingdomArchitectureMapping mapping;
				if (!KingdomArchitecture.TryGetMapping(
					Entry.Key, Entry.Category, architectureSize, out mapping))
				{
					sizes.RemoveAt(i);
				}
			}
			return sizes;
		}

		/// <summary>What the founder reads before choosing how much ground to stake: this plot's
		/// span, the whole chain's footprints, and where the ceiling falls. Null for a design that
		/// is not a plot.</summary>
		public static string ForesightFor(KingdomRules.BuildEntry Entry, KingdomPlotRules.PlotSize Stake)
		{
			if (Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				return null;
			}
			return KingdomPlotRules.ForesightLine(StakedSize(spec, Stake), ChainOf(Entry));
		}

		// --- Growing in place -------------------------------------------------------------

		/// <summary>
		/// Restamps plot metadata from a frozen authored successor after its exact scenery delta has
		/// settled. It never reads the current building catalogue and never runs procedural growth.
		/// </summary>
	}
}
