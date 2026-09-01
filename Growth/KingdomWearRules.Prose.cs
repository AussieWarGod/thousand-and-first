using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomWearRules
	{
		// ==================================================================================
		// Prose. Every line unique to this file (a damage event naming its cause, a completed
		// mending, a queued-behind-another-job wait, the Status/NextNeed summaries) is composed
		// once here and asserted directly. The condition wording itself
		// (<c>ConditionWord</c>/<c>ConditionPercent</c>) is quoted from
		// <c>KingdomMaterialRules</c> rather than re-derived, so the two systems never describe
		// the same work two different ways.
		// ==================================================================================

		public static string DamagedLine(string WorkName, WearCause Cause, int Wear)
		{
			return WorkName + " was " + CauseVerb(Cause) + ": " + KingdomMaterialRules.ConditionWord(Wear)
				+ " now, and runs " + KingdomMaterialRules.ConditionPercent(Wear) + " parts in a hundred until it is mended.";
		}

		public static string ReasonLine(RepairVerdict Verdict, string WorkName)
		{
			switch (Verdict)
			{
			case RepairVerdict.NoHands:
				return WorkName + " stands damaged, and there are no hands free to mend it.";
			case RepairVerdict.NoMaterials:
				return WorkName + " stands damaged, and the stockpiles are short of what mending it wants.";
			case RepairVerdict.OtherWorkUnderway:
				return WorkName + " stands damaged, and waits its turn while another mending has this pass's hands.";
			default:
				return null;
			}
		}

		public static string RepairBegunLine(string WorkName)
		{
			return "Mending begins on " + WorkName + ".";
		}

		public static string RepairCompleteLine(string WorkName)
		{
			return WorkName + " is mended, and runs at its full measure again.";
		}

		/// <summary>
		/// The one line a live water or charge store gets when the founder is first told it is
		/// losing what it holds (STANDARDS 7b). Food remains a persisted compatibility kind only;
		/// its text describes retirement and never announces loss.
		/// </summary>
		public static string LeakBegunLine(string WorkName, LeakKind Kind)
		{
			switch (Kind)
			{
			case LeakKind.Charge:
				return WorkName + " has gone cold at the seams, and the night's charge bleeds out of it.";
			case LeakKind.Food:
				return WorkName + " keeps every pantry item; its old food-loss record is retired.";
			default:
				return WorkName + " weeps down its east face, and what it holds runs away into the ground.";
			}
		}

		/// <summary>The unsaying: mending restores function, so the leak is over the moment the
		/// work is whole. The consequence is of damage, not of history (Addendum 10(b)).</summary>
		public static string LeakStoppedLine(string WorkName, LeakKind Kind)
		{
			switch (Kind)
			{
			case LeakKind.Charge:
				return WorkName + " is sealed again, and keeps its heat overnight.";
			case LeakKind.Food:
				return WorkName + " keeps every pantry item while it is mended.";
			default:
				return WorkName + " is sealed again, and holds every dram it is given.";
			}
		}

		/// <summary>The Status report's own line for how many works stand damaged, or empty when
		/// none do. Mirrors <c>KingdomReports</c>' own inline idle-works phrasing so a founder
		/// reads the two the same way.</summary>
		public static string StatusSuffix(int DamagedWorks)
		{
			return (DamagedWorks > 0) ? ("  {{r|" + DamagedWorks + " works stand damaged and run reduced}}") : "";
		}

		/// <summary>The NextNeed advice line for damaged works, or empty when none stand damaged.</summary>
		public static string NextNeedLine(int DamagedWorks)
		{
			return (DamagedWorks > 0) ? (DamagedWorks + " of the works stand damaged and run reduced. Mending queues on its own, out of what the stores can spare.") : "";
		}
	}
}
