using System;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.UI;
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
					// founders and cannot honestly be given the rest or relieved of these. Say so
					// once and stop. Faulted is terminal, so this cannot be reached twice.
					string faulted;
					if (!Restate(Game, ref Receipt,
						KingdomQuickstartFoundersDisposition.Faulted, null, out faulted))
					{
						Failure = faulted;
						return false;
					}
					AnnounceFoundersFault(i);
					Failure = "A named founder was missing or foreign; the cohort was faulted.";
					return false;
				}
				if (!TryEnrolFounder(System, body, Receipt, i, out Failure)) return false;
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
		private static bool TryEnrolFounder(KingdomSystem System, GameObject Body,
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
			// The origin tally is the one counter here that cannot be re-added, so it is written
			// exactly once, behind the property that proves it was written.
			if (string.IsNullOrEmpty(Body.GetStringProperty("KingdomOrigin")))
			{
				Body.SetStringProperty("KingdomOrigin", Receipt.ProfileKey);
				int origins;
				System.OriginCounts.TryGetValue(Receipt.ProfileKey, out origins);
				System.OriginCounts[Receipt.ProfileKey] = origins + 1;
			}
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
		/// Said once, at the single transition into the terminal Faulted state, so the founder is
		/// told the truth about a roll that is short rather than watching it silently stay short.
		/// </summary>
		private static void AnnounceFoundersFault(int Index)
		{
			MetricsManager.LogError("ThousandAndFirst quickstart founders: founder " + Index
				+ " was missing or foreign; the cohort was faulted.");
			Popup.Show("{{W|Your founding party is short.}} One of the four who set out with you "
				+ "is not here, and the settlement will not invent a replacement. Those who did "
				+ "arrive are on your roll and stay there. This is not retried.");
		}
	}
}
