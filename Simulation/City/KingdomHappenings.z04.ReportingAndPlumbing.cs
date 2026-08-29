using System;

using XRL;
using XRL.UI;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomHappenings
	{

		// ==================================================================================
		// The homecoming report — what the ring adds up to
		// ==================================================================================

		/// <summary>
		/// One line a piece for what the founder missed, out of the told-log ring and nowhere
		/// else. Written into the ledger's ordinary note lane, under the brink lines, because a
		/// wedding is not an arrestable window.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="book">The city's book.</param>
		/// <param name="sinceTick">Told lines older than this are last visit's news.</param>
		public static void Digest(KingdomSystem System, KingdomCityBook book, long sinceTick)
		{
			if (!Enabled || System == null || book == null)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!book.TryRead(out state, out fault))
			{
				return;
			}
			int weddings = 0;
			int funerals = 0;
			int festivals = 0;
			int breakdowns = 0;
			for (int i = 0; i < state.ToldCount; i++)
			{
				KingdomToldRow row;
				if (!state.TryTold(i, out row) || row.Tick < sinceTick)
				{
					continue;
				}
				switch (row.Kind)
				{
				case KingdomToldKind.Wedding:
					weddings++;
					break;
				case KingdomToldKind.Funeral:
					funerals++;
					break;
				case KingdomToldKind.Festival:
					festivals++;
					break;
				case KingdomToldKind.Breakdown:
					if (!KingdomHappeningRules.IsMending(row.Outcome))
					{
						breakdowns++;
					}
					break;
				}
			}
			// ONE note, not four. KingdomLedger.Note caps the ordinary lane at twelve lines and
			// drops the rest silently, and the happenings arrive last of everything on the pass -
			// four lines of ours could push four of the settlement's own arithmetic off the end of
			// the report. Joined into a sentence, they cost one.
			string joined = Join(KingdomToldKind.Festival, festivals, KingdomToldKind.Wedding, weddings,
				KingdomToldKind.Funeral, funerals, KingdomToldKind.Breakdown, breakdowns);
			if (!string.IsNullOrEmpty(joined))
			{
				System.Ledger.Note("{{K|" + joined + "}}");
			}
		}

		private static string Join(KingdomToldKind a, int countA, KingdomToldKind b, int countB, KingdomToldKind c, int countC, KingdomToldKind d, int countD)
		{
			System.Text.StringBuilder builder = new System.Text.StringBuilder();
			Append(builder, a, countA);
			Append(builder, b, countB);
			Append(builder, c, countC);
			Append(builder, d, countD);
			return builder.ToString();
		}

		private static void Append(System.Text.StringBuilder builder, KingdomToldKind kind, int count)
		{
			string line = KingdomHappeningRules.ToldLine(kind, count);
			if (string.IsNullOrEmpty(line))
			{
				return;
			}
			if (builder.Length > 0)
			{
				builder.Append(' ');
			}
			builder.Append(line);
		}

		// ==================================================================================
		// Shared plumbing
		// ==================================================================================

		private static KingdomCityState Refresh(KingdomCityBook book,
			KingdomCityState fallback)
		{
			return book != null && book.TryRead(out KingdomCityState current,
				out KingdomCityFault ignored) ? current : fallback;
		}

		private static bool HasTold(KingdomCityState state, KingdomToldKind kind, long tick,
			int subjectA, int subjectB, int outcome)
		{
			if (state == null) return false;
			for (int i = 0; i < state.ToldCount; i++)
				if (state.TryTold(i, out KingdomToldRow row) && row.Kind == kind
					&& row.Tick == tick && row.SubjectA == subjectA && row.SubjectB == subjectB
					&& row.Outcome == outcome) return true;
			return false;
		}

		private static string DatedReport(long tick, string line)
		{
			long safe = tick < 0L ? 0L : tick;
			return "a dated report for the " + Calendar.GetDay(safe) + " of "
				+ Calendar.GetMonth(safe) + ", " + Calendar.GetYear(safe) + " AR said that "
				+ line;
		}

		private static long CurrentTick(long fallback)
		{
			return The.Game != null && The.Game.TimeTicks > 0L ? The.Game.TimeTicks : fallback;
		}

		/// <summary>
		/// Writes a brownout into the city's ring.
		/// <para>
		/// W7. The ANNOUNCE-ONCE latch is not here and must not be: it lives on the object that
		/// went quiet, so that recovery can unsay it (Addendum 12(c)) and the next failure can be
		/// told again. What the ring is for is the other half &mdash; the dated line a founder
		/// three zones away reads at the homecoming, and the digest reads afterwards. The ring
		/// forgets by age, which is right for history and wrong for a latch, and this is the
		/// history.
		/// </para>
		/// </summary>
		/// <param name="WorkId">The work that stopped.</param>
		/// <param name="Tier">The brownout ladder rung it stopped on, so the ring remembers how far
		/// down the city had to go and not only that the lights went out.</param>
		internal static void TellBrownout(KingdomSystem System, int WorkId, int Tier, string ZoneId, long TimeTicks)
		{
			if (!Enabled || System == null || !System.Founded || System.City == null || TimeTicks <= 0L)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!System.City.TryRead(out state, out fault))
			{
				return;
			}
			KingdomCityState next;
			if (!state.TryTell(new KingdomToldRow(KingdomToldKind.Brownout, TimeTicks, WorkId, 0, ZoneId, Tier), out next, out fault))
			{
				KingdomLog.Log("city: brownout refused (" + fault + "); the ring is unchanged");
				return;
			}
			if (!System.City.TryPublish(next, out fault))
			{
				KingdomLog.Log("city: brownout refused (" + fault + "); the book is unchanged");
			}
		}

		private static KingdomCityState Tell(KingdomCityState state, KingdomHappening happening)
		{
			KingdomCityState next;
			KingdomCityFault fault;
			return state.TryTell(happening.ToldRow, out next, out fault) ? next : state;
		}

		/// <summary>
		/// One draw, keyed so a reload never re-rolls a happening the founder has already read
		/// about. LIVING-CITY-ARCHITECTURE &sect;2.4.
		/// </summary>
		private static bool Drawn(string settlementId, string stream, uint kind, uint index,
			ulong ordinal, int chancePercent)
		{
			SemanticEventKey key;
			KernelFaultCode fault;
			ulong value;
			if (SemanticEventKey.TryCreate(HappeningRulesVersion, settlementId, stream, kind,
				ordinal, out key, out fault)
				&& CounterRandom.TryDrawBelow(HappeningSeed, key, index, 100uL, out value, out fault))
			{
				return (int)value < chancePercent;
			}
			// The kernel refused - no settlement name yet, or this machine's crypto provider is
			// failing. A happening that cannot be drawn reproducibly does not happen: silence is
			// the honest answer, and unlike flavour text a wedding is not something to fall back
			// to an unstable roll for.
			KingdomLog.Log("happening: draw refused (" + fault + ") on " + stream);
			return false;
		}

		private static int OnTheRoll(KingdomCityState state)
		{
			int count = 0;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (state.TryResident(i, out row) && KingdomResidentRules.OnTheRoll(row))
				{
					count++;
				}
			}
			return count;
		}

		private static int ResidentIdOf(KingdomCityState state, string name)
		{
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (state.TryResident(i, out row) && string.Equals(row.Name, name, StringComparison.Ordinal))
				{
					return row.ResidentId;
				}
			}
			return 0;
		}

		private static string Named(string name)
		{
			return string.IsNullOrEmpty(name) ? "a settler" : KingdomPresentation.Rich(name);
		}
	}
}
