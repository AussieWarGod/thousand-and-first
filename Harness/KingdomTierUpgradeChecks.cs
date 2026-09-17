using System;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Machine witness for the ordinary (non-heart) building tier upgrade, behaviour-coverage
	/// row 8. Five phases over one real settlement: a production-commissioned tent stands; the
	/// registry's quote for tent -&gt; tentrow is what production computes; the improvement is
	/// refused BY ITS NAMED REASON while the canvas is short; the real settlement pass then pays
	/// and begins it; and the successor stands with its predecessor exactly and provably gone.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED - see <see cref="KingdomTierUpgradeProvider"/>. Nothing here
	/// drives the upgrade: the phases only read what the settlement pass did between advances.
	/// </para>
	/// </summary>
	internal static partial class KingdomTierUpgradeChecks
	{
		/// <summary>The standing design the settlement raises from.</summary>
		internal const string FromKey = "tent";

		/// <summary>The ordinary successor it grows into.</summary>
		internal const string ToKey = "tentrow";

		private static Frame Retained;

		internal static bool Vacant { get { return Retained == null; } }

		internal static string Run(string Verb, XRLGame Game, Zone Zone, out bool Complete)
		{
			Complete = false;
			if (Verb == KingdomTierUpgradeProvider.SetupVerb)
			{
				Require(Retained == null, "a tier-upgrade attempt is already retained");
				Retained = new Frame(Game, Zone);
				Retained.Start();
			}
			else if (Verb == KingdomTierUpgradeProvider.ShortVerb)
			{
				Require(Retained != null, "tier-upgrade setup is absent");
				Retained.Shortfall();
			}
			else
			{
				Require(Retained != null, "tier-upgrade setup is absent");
				Retained.Check();
			}
			Complete = Retained.Done;
			return (Complete ? "native-tier-upgrade cases=1 passed=1 failed=0"
				: "native-tier-upgrade phase=" + Retained.Phase)
				+ "; synthetic-camp=true; synthetic-residents=true; synthetic-drams=true"
				+ "; synthetic-born-provenance=true; synthetic-store-contents=true"
				+ "; synthetic-first-notice=true; synthetic-tent=false"
				+ "; ordinary-reachability=untested; charter=untested; save-load=untested"
				+ Retained.Evidence;
		}

		internal static string Fail(Exception Error)
		{
			if (Retained != null) Retained.Armed = false;
			return "native-tier-upgrade cases=1 passed=0 failed=1; evidence retained: "
				+ KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message)
				+ Retained?.Evidence;
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomTierUpgradeProvider.Require(Value, Failure);
		}

		private sealed partial class Frame
		{
			internal readonly XRLGame Game;
			internal readonly Zone Zone;
			internal KingdomSystem System;

			/// <summary>Object id of the production-built tent, captured while it still stands.
			/// </summary>
			internal string PredecessorId;

			/// <summary>Object id of the tent-row the settlement raised in its place.</summary>
			internal string SuccessorId;

			/// <summary>Id of the improvement job the settlement pass funded, captured at
			/// phase 4 from the predecessor's own construction receipt.</summary>
			internal string ImprovementJobId;

			internal bool Armed, Done;
			internal int Phase;
			internal readonly StringBuilder Evidence = new StringBuilder();

			internal Frame(XRLGame Game, Zone Zone) { this.Game = Game; this.Zone = Zone; }

			internal void Check()
			{
				Require(Armed && !Done, "the tier-upgrade frame is not armed");
				switch (Phase)
				{
					case 1: StandingTent(); break;
					case 3: PaidAndBegun(); break;
					case 4: HandedOverAndRetired(); Done = true; break;
					default: Require(false, "tier-upgrade check ran out of phase order"); break;
				}
				Phase++;
			}

			internal void Shortfall()
			{
				Require(Armed && !Done && Phase == 2,
					"the shortfall leg must follow the standing-tent check exactly once");
				NamedMaterialRefusal();
				Phase++;
			}

			internal static void Require(bool Value, string Failure)
			{
				KingdomTierUpgradeChecks.Require(Value, Failure);
			}
		}
	}
}
