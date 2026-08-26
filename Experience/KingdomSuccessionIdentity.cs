using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomSuccessionRules
	{

		/// <summary>Stable, exact identity for one founder death. The object id is encoded whole rather
		/// than hashed, so two founders can never alias through a collision.</summary>
		public static string FounderDeathToken(int Succession, long DeathTick, string FounderId)
		{
			int ordinal = (Succession < 1) ? 1 : Succession;
			long tick = (DeathTick < 0L) ? 0L : DeathTick;
			string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(FounderId ?? ""));
			return "v1:" + ordinal.ToString(CultureInfo.InvariantCulture) + ":"
				+ tick.ToString(CultureInfo.InvariantCulture) + ":" + encoded;
		}

		/// <summary>The journal attribute for one exact founder-death token.</summary>
		public static string FounderAttribute(string DeathToken)
		{
			return FounderAttributePrefix + (DeathToken ?? "");
		}

		/// <summary>Whether an entry's attribute names this founder's lost knowledge.</summary>
		public static bool StampedBy(string Attribute, string Wanted)
		{
			return !string.IsNullOrEmpty(Attribute) && string.Equals(Attribute, Wanted, StringComparison.Ordinal);
		}

		/// <summary>Pure idempotence gate for the synchronous death handler.</summary>
		public static SuccessionAttemptVerdict JudgeAttempt(string Wanted, string Pending, string Completed)
		{
			if (string.IsNullOrEmpty(Wanted))
			{
				return SuccessionAttemptVerdict.Invalid;
			}
			if (string.Equals(Wanted, Completed, StringComparison.Ordinal))
			{
				return SuccessionAttemptVerdict.AlreadyCompleted;
			}
			if (string.IsNullOrEmpty(Pending))
			{
				return SuccessionAttemptVerdict.Begin;
			}
			return string.Equals(Wanted, Pending, StringComparison.Ordinal)
				? SuccessionAttemptVerdict.DuplicatePending
				: SuccessionAttemptVerdict.Conflict;
		}

		// ==================================================================================
		// The price of choosing (Addendum 22 C13)
		// ==================================================================================

		/// <summary>
		/// Whether this accession costs the realm its seat. C13 defines config A by its price:
		/// the law's heir is free, and choosing one is not. The orchestrator's ruling under the
		/// author's delegation is that the price is on by default, because choice being free and
		/// consequence not is Qud's own posture.
		/// </summary>
		public static bool CostsTheSeat(HeirChoice Choice, bool SeatCostEnabled)
		{
			return Choice == HeirChoice.Chosen && SeatCostEnabled;
		}

	}
}
