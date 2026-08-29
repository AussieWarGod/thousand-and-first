using System;
using System.Collections.Generic;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomMirrorGate
	{
		// ==================================================================================
		// The register
		// ==================================================================================

		/// <summary>
		/// The realm's arches, read out of game state and repaired if it needs it.
		/// <para>
		/// A row that cannot be read is dropped, the repaired register is written back, and the
		/// founder is told once &mdash; which is once and only once because the repair makes the
		/// condition non-recurring, so no latch is needed anywhere to hold it to that.
		/// </para>
		/// </summary>
		/// <param name="System">Told when a row had to be dropped. Null asks nothing and says
		/// nothing, which is what the read-only callers want.</param>
		private static KingdomGateRow[] Register(KingdomSystem System)
		{
			if (The.Game == null)
			{
				return new KingdomGateRow[0];
			}
			KingdomGateRow[] rows;
			int dropped;
			KingdomMirrorGateRules.TryParseRegister(The.Game.GetStringGameState(KingdomMirrorGateRules.RegisterStateKey, ""), out rows, out dropped);
			if (dropped <= 0)
			{
				return rows;
			}
			Write(rows);
			KingdomLog.Log("mirror-gate: dropped " + dropped + " unreadable register row(s)");
			if (System != null && System.Founded)
			{
				System.Ledger.Note("{{r|The realm's record of its arches was damaged, and " + dropped + " of them could not be read back. Those arches are standing but unkeyed; key them again.}}");
			}
			return rows;
		}

		private static void Write(KingdomGateRow[] rows)
		{
			The.Game?.SetStringGameState(KingdomMirrorGateRules.RegisterStateKey, KingdomMirrorGateRules.FormatRegister(rows));
		}

		/// <summary>
		/// Points every arch in the realm at the capital's, and says what changed.
		/// <para>
		/// Called by the crown the moment a capital is made or moved (Addendum 22 A2: the network
		/// is hubbed at the capital), and this completes the formerly postponed QB-1 retrofit. Nothing
		/// here loads a zone, visits an arch, or rebuilds anything: the register carries the
		/// pairing, so re-keying the realm is a rewrite of one column and the arches find out the
		/// next time each is anchored &mdash; which is before every crossing, every dedication and
		/// every day's draw, so no arch can act on a stale partner.
		/// </para>
		/// <para>
		/// One live object is the exception worth taking: an arch standing in the zone the founder
		/// is in has a <c>DestinationKey</c> in memory right now and a description they may be
		/// reading, so any loaded arch is re-anchored here rather than at some later event.
		/// </para>
		/// </summary>
		/// <param name="System">The realm, for the telling. Never null in practice.</param>
		/// <param name="Capital">The city keeping the crown.</param>
		internal static void Hub(KingdomSystem System, string Capital)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(Capital) || The.Game == null)
			{
				return;
			}
			KingdomGateRow[] rows = Register(System);
			if (rows.Length == 0)
			{
				// Not applicable rather than blocked: a realm that has never keyed an arch is not
				// being stopped from anything, and 7b's first kind says nothing, correctly.
				return;
			}
			KingdomGateRow[] next;
			int rekeyed;
			string hubKey;
			KingdomGateVerdict verdict = KingdomMirrorGateRules.TryHub(rows, Capital, out next, out rekeyed, out hubKey);
			if (verdict == KingdomGateVerdict.RefusedUnkeyed)
			{
				System.Ledger.Note(KingdomMirrorGateRules.NoArchAtCapitalLine(Capital));
				MessageQueue.AddPlayerMessage(KingdomMirrorGateRules.NoArchAtCapitalLine(Capital));
				return;
			}
			if (verdict != KingdomGateVerdict.Joined && verdict != KingdomGateVerdict.Offered)
			{
				KingdomLog.Log("mirror-gate hub refused for " + Capital + ": " + verdict);
				return;
			}
			Write(next);
			ReAnchorHere();
			if (rekeyed <= 0)
			{
				return;
			}
			string line = KingdomMirrorGateRules.HubbedLine(Capital, rekeyed);
			System.Ledger.Note(line);
			MessageQueue.AddPlayerMessage(line);
			KingdomChronicle.Record(System, KingdomMirrorGateRules.HubbedTelling(Capital));
			KingdomLog.Log("mirror-gate hub=" + Capital + " rekeyed=" + rekeyed + " rows=" + next.Length);
		}

		/// <summary>Re-reads the register into whatever arch is standing in the zone the founder is
		/// in. Every other arch reads it for itself the next time it is anchored.</summary>
		private static void ReAnchorHere()
		{
			Zone active = The.ZoneManager?.ActiveZone;
			if (active == null)
			{
				return;
			}
			foreach (GameObject found in active.GetObjects())
			{
				r_KingdomMirrorGate arch = found?.GetPart<r_KingdomMirrorGate>();
				if (arch != null)
				{
					Anchor(arch);
				}
			}
		}

		private static void Release(r_KingdomMirrorGate Gate, KingdomSystem System, KingdomGateRow[] rows, string city)
		{
			if (Popup.ShowYesNo("Unkey the arch at " + city + "?\n\nIt will stand exactly where it stands and cost nothing at all; the crossing simply stops answering.") != DialogResult.Yes)
			{
				return;
			}
			KingdomGateRow[] next;
			string orphan;
			KingdomGateVerdict verdict = KingdomMirrorGateRules.TryRelease(rows, Gate.LocationKey, out next, out orphan);
			if (verdict != KingdomGateVerdict.Released)
			{
				Popup.Show(KingdomMirrorGateRules.RefusalLine(verdict, city));
				return;
			}
			Write(next);
			Anchor(Gate);
			Gate.Dark = false;
			System.Ledger.Note("{{y|" + KingdomMirrorGateRules.ReleasedLine(KingdomPresentation.Rich(city)) + "}}");
			if (orphan.Length > 0)
			{
				// The other end was unkeyed by the same act and its own city is nowhere near: told
				// here, because there is no other moment at which the founder would find out.
				System.Ledger.Note("{{y|" + KingdomMirrorGateRules.OrphanedLine(KingdomPresentation.Rich(CityNamed(next, orphan))) + "}}");
			}
		}

		private static void GoDark(r_KingdomMirrorGate Gate, KingdomSystem System, string city)
		{
			if (Gate.Dark)
			{
				return;
			}
			Gate.Dark = true;
			System.Ledger.Note("{{r|" + KingdomMirrorGateRules.WentDarkLine(KingdomPresentation.Rich(city)) + "}}");
			KingdomChronicle.Record(System, KingdomMirrorGateRules.WentDarkTelling(KingdomPresentation.Rich(city), KingdomPresentation.Rich(System.KingdomDisplayName)));
		}

		/// <summary>The city keeping the arch under this key, or null when nothing does.</summary>
		private static string CityNamed(KingdomGateRow[] rows, string key)
		{
			int at = KingdomMirrorGateRules.IndexOfKey(rows, key);
			return (at < 0) ? null : rows[at].City;
		}

		/// <summary>
		/// Which of the realm's cities holds this ground, or null when the realm does not hold it at
		/// all. Delegated to <c>KingdomCrown.CityOf</c>, which is the one copy: the crown lane needs
		/// exactly this read and two of them would eventually disagree about which city an arch
		/// stands in, which is the one thing the register may never be wrong about.
		/// </summary>
		private static string CityOf(KingdomSystem System, string ZoneId)
		{
			return KingdomCrown.CityOf(System, ZoneId);
		}	}
}
