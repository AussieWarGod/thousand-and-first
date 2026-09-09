using System;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Stage B: the irreversible half of the founding cohort. Forward-only, in index order, and
	/// idempotent per founder &#8212; every step has an exact, cheap witness that it has already
	/// been applied, so a wake after a crash repeats nothing and no tally can be inflated.
	/// </summary>
	public static partial class KingdomQuickstartBootstrap
	{
		private static bool TryEnrolFounders(XRLGame Game, KingdomSystem System, Zone Zone,
			ref KingdomQuickstartReceipt Receipt, out string Failure)
		{
			Failure = "";
			for (int i = 0; i < KingdomQuickstartRules.FounderCount; i++)
			{
				GameObject body = Zone?.FindObjectByID(Receipt.FounderObjectIds[i]);
				if (!FounderIsExact(Zone, body, Receipt, i)
					|| !ReceiptOwns(body, Receipt.FounderObjectIds[i]))
				{
					// Neither completable nor reversible: the world holds one to three enrolled
					// founders and cannot honestly be given the rest or relieved of these. Publish
					// the fault, then let the ONE announce-once path say it. Faulted is terminal,
					// so no later wake reaches this at all.
					string faulted;
					if (!Restate(Game, ref Receipt,
						KingdomQuickstartFoundersDisposition.Faulted, null, out faulted))
					{
						Failure = faulted;
						return false;
					}
					Failure = "Founder " + i + " was not here when the roll was written, and the "
						+ "settlement will not invent a replacement. Those who did arrive are on "
						+ "your roll and stay there. This is not retried.";
					return false;
				}
				if (!TryEnrolFounder(System, Zone, body, Receipt, i, out Failure)) return false;
			}
			// One publish closes the cohort. Population reads four off the roll from here with no
			// seeder bookkeeping of its own.
			return Advance(Game, ref Receipt, KingdomQuickstartPhase.FoundersSeeded, "",
				KingdomQuickstartAdvisorDisposition.Unresolved, out Failure)
				&& VerifyFounders(Zone, Receipt, out Failure);
		}

		/// <summary>
		/// One founder, in the order the ordinary arrival transaction uses: name, citizenship,
		/// birth mark, origin, then the roll row (which binds on the body's current zone and so
		/// must come last).
		/// </summary>
		private static bool TryEnrolFounder(KingdomSystem System, Zone Zone, GameObject Body,
			KingdomQuickstartReceipt Receipt, int Index, out string Failure)
		{
			Failure = "";
			if (string.IsNullOrEmpty(Body.GetStringProperty("KingdomName")))
			{
				string given = FounderName(Receipt.ProfileKey, Index);
				Body.GiveProperName(given, Force: true);
				Body.SetStringProperty("KingdomName", given);
			}
			if (Body.GetIntProperty("KingdomCitizen") != 1
				&& !KingdomFounding.EnrollCitizen(Body,
					KingdomCitizenshipEnrollmentReason.Founding))
			{
				Failure = "A founding citizen's enrolment was refused.";
				return false;
			}
			Body.SetIntProperty("KingdomBorn", 1);
			if (string.IsNullOrEmpty(Body.GetStringProperty("KingdomOrigin")))
				Body.SetStringProperty("KingdomOrigin", Receipt.ProfileKey);
			// The tally is DERIVED from the founders that carry the origin, never incremented as
			// each one is written. An increment beside a property write has a gap: an interruption
			// between the two would make the retry see the property, skip the counter, and lose
			// that count forever. Recomputing is idempotent by construction, so it is correct on
			// the first pass, on a resumed pass, and on a pass that ends in a fault.
			ReconcileFounderOrigins(System, Zone, Receipt);
			KingdomCityBook book;
			int residentId;
			if (!KingdomResidents.TryEnsureRow(System, Body, out book, out residentId))
			{
				Failure = "A founding citizen could not be put on the roll.";
				return false;
			}
			return true;
		}

		/// <summary>
		/// Sets this camp's origin tally to the number of founders that provably carry its origin.
		/// Never lowers a tally somebody else raised: it only ever closes a shortfall its own
		/// founders account for.
		/// </summary>
		private static void ReconcileFounderOrigins(KingdomSystem System, Zone Zone,
			KingdomQuickstartReceipt Receipt)
		{
			int carried = 0;
			for (int i = 0; i < KingdomQuickstartRules.FounderCount; i++)
			{
				GameObject body = Zone?.FindObjectByID(Receipt.FounderObjectIds[i]);
				if (GameObject.Validate(body) && string.Equals(
					body.GetStringProperty("KingdomOrigin"), Receipt.ProfileKey,
					StringComparison.Ordinal)) carried++;
			}
			int recorded;
			System.OriginCounts.TryGetValue(Receipt.ProfileKey, out recorded);
			if (recorded < carried) System.OriginCounts[Receipt.ProfileKey] = carried;
		}
	}
}
