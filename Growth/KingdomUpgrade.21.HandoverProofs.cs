using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		private static bool ExactHandoverEndpointsAfterCallback(GameObject Predecessor,
			GameObject Successor, Cell Cell, string SuccessorKey, r_KingdomImprovement Intent,
			KingdomConstructionJob Job)
		{
			KingdomSystem system = The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			Zone zone = GameObject.Validate(Predecessor) ? Predecessor.CurrentZone : null;
			GameObject exactPredecessor;
			GameObject exactSuccessor;
			return GameObject.Validate(Predecessor) && GameObject.Validate(Successor)
				&& Intent != null && Predecessor.GetPart<r_KingdomImprovement>() == Intent
				&& Predecessor.IDIfAssigned == Intent.HandoverSourceId
				&& Successor.IDIfAssigned == Intent.HandoverTargetId
				&& Predecessor.CurrentCell == Cell && Successor.CurrentCell == Cell
				&& Successor.GetIntProperty(BuiltProperty) == 1
				&& Successor.GetStringProperty(BuildKeyProperty) == SuccessorKey
				&& KingdomConstruction.FindExactId(zone, Predecessor.IDIfAssigned,
					out exactPredecessor) == KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exactPredecessor, Predecessor)
				&& KingdomConstruction.FindExactId(zone, Successor.IDIfAssigned,
					out exactSuccessor) == KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exactSuccessor, Successor)
				&& (Job == null || (KingdomConstruction.Owns(system, zone, Job)
					&& KingdomConstruction.HasReceipt(Predecessor, Job)
					&& r_KingdomScaffold.IsExactSuccessor(Successor,
						Predecessor.CurrentZone, Cell, Job, Intent.SuccessorBlueprint)
					&& KingdomConstruction.IsCurrent(Job)));
		}

		private static bool ExactCarriedMarks(GameObject Predecessor, GameObject Successor,
			string SuccessorKey)
		{
			if (!GameObject.Validate(Predecessor) || !GameObject.Validate(Successor)
				|| Successor.GetIntProperty(BuiltProperty) != 1
				|| Successor.GetStringProperty(BuildKeyProperty) != SuccessorKey) return false;
			// The scalar marks (larder, stores, certification, given name, adoption, yielding) are
			// checked by the same table CarryMarks writes from; yielding stays one-directional.
			if (!KingdomUpgradeRules.FounderMarksSettled(ReadFounderMarks(Predecessor),
				ReadFounderMarks(Successor), Successor.Inventory != null,
				Successor.GetPart<LiquidVolume>() != null)) return false;
			if (KingdomPlots.TryReadRect(Predecessor, out _))
			{
				if (!KingdomPlots.TryReadRect(Successor, out _)) return false;
				string plot = Predecessor.GetStringProperty(KingdomPlots.PlotIdProperty);
				if (!string.IsNullOrEmpty(plot)
					&& Successor.GetStringProperty(KingdomPlots.PlotIdProperty) != plot) return false;
			}
			return KingdomWear.SameStableState(Predecessor, Successor);
		}

		/// <summary>
		/// Pours the predecessor's liquid into the successor, mixture and all. Every component is
		/// moved at its real dram count &mdash; <c>ComponentLiquids</c> holds parts per thousand,
		/// not drams &mdash; and both ends are measured rather than assumed, because
		/// <c>AddDrams</c> clamps silently and <c>UseDrams</c> reports something else entirely.
		/// </summary>
		/// <returns>Drams actually moved.</returns>
		public static int CarryLiquid(GameObject Predecessor, GameObject Successor)
		{
			r_KingdomImprovement receipt = Predecessor?.GetPart<r_KingdomImprovement>();
			int moved;
			return r_KingdomImprovement.CarryLiquidDurable(Predecessor, Successor, receipt,
				out moved) ? moved : 0;
		}

		/// <summary>
		/// Moves every object out of the predecessor. Anything the successor will not take is put
		/// down in the cell rather than destroyed.
		/// </summary>
		/// <returns>Objects actually moved or set down.</returns>
		public static int CarryInventory(GameObject Predecessor, GameObject Successor, Cell Where)
		{
			r_KingdomImprovement receipt = Predecessor?.GetPart<r_KingdomImprovement>();
			int moved;
			return r_KingdomImprovement.CarryInventoryDurable(Predecessor, Successor, Where,
				receipt, out moved) ? moved : 0;
		}

		/// <summary>
		/// Carries the settlement's own marks onto the improved work. The scaffold has already
		/// set what the new DESIGN says it is &mdash; built, staffed, defended, and dedicated if
		/// it holds liquid. This carries what the FOUNDER decided about the old one, which the
		/// scaffold cannot know: a larder dedication (which the scaffold keys off one blueprint
		/// and would silently drop), a stores dedication, an adoption, and a machine's grid
		/// certification.
		/// </summary>
	}
}
