using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The day AFTER the rung was raised (issue #162). The rung-2 phases proved the climb itself;
	/// this one proves the settlement is still ALIVE on that ground afterwards.
	///
	/// <para>WHY IT EXISTS. Native run 9 was green by the persona's own expectations while
	/// Player.log carried "construction: founding heart recovery requires inspection" and
	/// "seal: settlement pass was not staged" once each, immediately after the raise: the heart's
	/// sealed recovery cannot follow a root the improvement route replaced, and
	/// <c>KingdomConstruction.Settlement</c> then aborts every later pass on that ground. The
	/// persona could not see it because nothing after the raise asked production a question whose
	/// answer depends on the pass still running.</para>
	///
	/// <para>WHAT IT ASKS. Production's own recovery predicate, the one the settlement pass itself
	/// calls before it will do anything (<c>KingdomConstruction.Settlement.cs</c>), asked once on
	/// the ground the rung was raised on. It is idempotent by construction -- the pass calls it
	/// every tick -- so asking it here drives nothing that would not have happened anyway. On a
	/// tree where the defect stands this reads false and the persona goes RED; on a fixed tree it
	/// reads true. The city book's own work row is read beside it and journaled, because a row
	/// still naming the retired root is the other half of the same failure.</para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private void Phase3()
			{
				RecordJobProgress();
				GameObject standing = StandingHeart();
				Require(KingdomUpgrade.DesignKeyOf(standing) == SecondRungKey,
					"taf-camp-after-raise-rung-lost: the standing heart is no longer the "
						+ "waterstone; key=" + KingdomUpgrade.DesignKeyOf(standing));
				Require(KingdomPlots.HeartRung(Zone) == 2,
					"taf-camp-after-raise-rung-unread: the recorded rung is "
						+ KingdomPlots.HeartRung(Zone));
				RecordBookRow(standing);
				// The predicate the settlement pass gates itself on. A false here is exactly the
				// "founding heart recovery requires inspection" halt, seen from inside the game
				// instead of from the log.
				bool recovered = KingdomPlots.RecoverFoundingHeart(System, Zone);
				Evidence.Append("\nphase3 tick=").Append(Game.TimeTicks)
					.Append("; turns=").Append(Game.Turns)
					.Append("; standing=").Append(standing.IDIfAssigned)
					.Append("; key=").Append(KingdomUpgrade.DesignKeyOf(standing))
					.Append("; zone rung read=").Append(KingdomPlots.HeartRung(Zone))
					.Append("; founding heart recovered=").Append(recovered);
				Require(recovered, "taf-camp-after-raise-heart-unrecovered: the settlement cannot "
					+ "recover its founding heart on the day after the rung was raised, so every "
					+ "later settlement pass on this ground refuses before it begins (issue #162)");
				RequireStoreIdentity();
			}

			/// <summary>The city book's own work row for the standing heart, read and journaled.
			/// A row still naming the retired root is what the spatial seal refuses on, so the
			/// blueprint and the folded work id are recorded whether they agree or not. This
			/// reads; it never rebuilds the book.</summary>
			private void RecordBookRow(GameObject Standing)
			{
				try
				{
					Simulation.City.KingdomCityBook book = System?.City;
					if (book == null || book.WorkIds == null)
					{
						Evidence.Append("\nphase3 book=absent");
						return;
					}
					int wanted = Simulation.City.KingdomCityRules.StableId(Standing.IDIfAssigned);
					bool matched = false;
					int rows = 0;
					for (int i = 0; i < book.WorkIds.Count; i++)
					{
						if (i >= book.WorkZoneIds.Count || book.WorkZoneIds[i] != Zone.ZoneID
							|| i >= book.WorkDesignKeys.Count || i >= book.WorkAnchorsX.Count
							|| i >= book.WorkAnchorsY.Count) continue;
						rows++;
						bool mine = book.WorkIds[i] == wanted;
						matched |= mine;
						if (rows > 8) continue;
						Evidence.Append("\nphase3 book-row work=").Append(book.WorkIds[i])
							.Append("; design=").Append(book.WorkDesignKeys[i])
							.Append("; at=").Append(book.WorkAnchorsX[i]).Append(',')
							.Append(book.WorkAnchorsY[i]).Append("; crew=")
							.Append(i < book.WorkCrews.Count ? book.WorkCrews[i] : -1)
							.Append("; is the standing heart=").Append(mine);
					}
					Evidence.Append("\nphase3 book rows=").Append(rows)
						.Append("; standing work id=").Append(wanted)
						.Append("; row names the standing heart=").Append(matched);
				}
				catch (Exception error)
				{
					// Diagnostics must never replace the production answer below them.
					Evidence.Append("\nphase3 book-read-error=").Append(
						KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message));
				}
			}
		}
	}
}
