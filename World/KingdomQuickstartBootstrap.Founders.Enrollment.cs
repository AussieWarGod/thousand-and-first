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
				if (!TryEnrolFounder(Game, System, Zone, body, Receipt, i, out Failure))
					return false;
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
		private static bool TryEnrolFounder(XRLGame Game, KingdomSystem System, Zone Zone,
			GameObject Body, KingdomQuickstartReceipt Receipt, int Index, out string Failure)
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
			// The origin label and the settlement's origin tally are written together, under one
			// durable identity-bound obligation, and never inferred from each other. The label
			// alone cannot say whether the shared tally already holds this founder, and the tally
			// alone cannot either: ordinary arrivals raise it inside their own protocol, and one of
			// them can coincidentally produce exactly the count this founder intended to leave.
			string accounting;
			KingdomFounderOriginOutcome counted = AccountFounderOrigin(System, Body, Receipt,
				out accounting);
			KingdomCityBook book;
			int residentId;
			if (!KingdomResidents.TryEnsureRow(System, Body, out book, out residentId))
			{
				Failure = "A founding citizen could not be put on the roll.";
				return false;
			}
			// Said only once the roll row actually exists, and said without a number. The refusal
			// means the accounting is UNRESOLVED, not that a count is missing: an interruption
			// after the increment retains it, and this cannot tell that case from one before it.
			// Claiming a deficit would be as much of a guess as claiming there is none.
			if (counted == KingdomFounderOriginOutcome.Quarantined)
				AnnounceFoundersOnce(Game, Receipt, "Founder " + Index
					+ "'s origin accounting is unresolved (" + accounting + "). They are named, "
					+ "enrolled and on your roll; only whether the origin tally already holds them "
					+ "cannot be established, so it is left exactly as it stands and not retried.");
			return true;
		}
	}
}
