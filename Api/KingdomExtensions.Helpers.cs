using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomExtensions
	{
		private static int Record(KingdomSystem system, Binding binding, KingdomNotice[] notices, string label, bool here, long nowTick, long sinceTick, int spare)
		{
			int pushed = 0;
			int kept = 0;
			for (int i = 0; notices != null && i < notices.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxNoticesPerSource; i++)
			{
				KingdomNotice notice = notices[i];
				string telling = KingdomApiRules.Trim(notice.Telling);
				string kind = KingdomApiRules.Kind(notice.Kind);
				if (string.IsNullOrEmpty(telling) || string.IsNullOrEmpty(kind))
				{
					continue;
				}
				// The city does not report the future, and it does not re-report what it already
				// told: a notice outside the window this lane was asked about is dropped rather
				// than filed with a wrong date.
				if (notice.Tick > nowTick || notice.Tick <= sinceTick)
				{
					continue;
				}
				kept++;
				KingdomChronicle.Record(system, telling);
				string spoken = KingdomApiRules.Trim(notice.Notice);
				if (pushed < spare && !string.IsNullOrEmpty(spoken))
				{
					KingdomWord.Ambient(system, label, here, spoken);
					pushed++;
				}
				KingdomLog.Log("extension happening: " + binding.ModName + " kind=" + kind + " tick=" + notice.Tick);
			}
			return pushed;
		}

		private static void Keep(KingdomAsk[] source, int limit, string modName, KingdomCityReading reading, List<KingdomAsk> into)
		{
			string prefix = string.IsNullOrEmpty(modName) ? "" : (KingdomApiRules.Slug(modName) + ":");
			int kept = 0;
			for (int i = 0; source != null && i < source.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall && kept < limit; i++)
			{
				KingdomAsk ask = source[i];
				string kind = KingdomApiRules.Kind(ask.Kind);
				string title = KingdomApiRules.Trim(ask.Title);
				if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(title))
				{
					continue;
				}
				kept++;
				into.Add(new KingdomAsk(
					prefix + kind,
					title,
					KingdomApiRules.Trim(ask.Want),
					Held(reading, ask.ZoneId),
					// Clamped DOWN, never up: an undefined weight is not a claim of urgency, and
					// clamping garbage to Grave would make it the loudest line on the board.
					(ask.Weight > KingdomAskWeight.Grave) ? KingdomAskWeight.Passing : ask.Weight));
			}
		}

		/// <summary>The ask's ground when the city actually holds it, and null otherwise. A board
		/// that named ground the city does not hold would send the founder somewhere that is not
		/// theirs, and the name would be fetched from the world to do it.</summary>
		private static string Held(KingdomCityReading reading, string zoneId)
		{
			if (reading == null || string.IsNullOrEmpty(zoneId))
			{
				return null;
			}
			for (int i = 0; i < reading.ZoneCount; i++)
			{
				KingdomZoneReading zone;
				if (reading.TryZone(i, out zone) && zone.ZoneId == zoneId)
				{
					return zoneId;
				}
			}
			return null;
		}

		private static void Fault(string owner, string lane, string status)
		{
			string line = owner + " stalled its own " + lane + " (" + status + "). The city is unaffected.";
			MetricsManager.LogError("ThousandAndFirst API: " + line);
			KingdomLog.Log("extension fault: " + owner + " lane=" + lane + " status=" + status);
			if (The.Game == null) return;
			if (AnnouncedFaults == null)
			{
				AnnouncedFaults = new HashSet<string>(StringComparer.Ordinal);
			}
			string key = (owner ?? "") + "|" + (lane ?? "");
			if (AnnouncedFaults.Count < MaxRuntimeFaultAnnouncements && AnnouncedFaults.Add(key))
			{
				MessageQueue.AddPlayerMessage("{{r|" + owner + " stalled its own " + lane
					+ ". The city is unaffected; the log names the fault.}}");
			}
		}

	}
}
