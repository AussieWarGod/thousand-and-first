using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;
using XRL.World.Tinkering;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		internal readonly struct SuccessionResidentView
		{
			internal readonly int ResidentId;
			internal readonly string Name;
			internal readonly string CityName;
			internal readonly string HomeName;
			internal readonly string ArrivedLabel;
			internal readonly int ServiceMarks;
			internal readonly int StudyMarks;

			internal SuccessionResidentView(int residentId, string name, string cityName,
				string homeName, string arrivedLabel, int serviceMarks, int studyMarks)
			{
				ResidentId = residentId;
				Name = name ?? "resident";
				CityName = cityName ?? "the realm";
				HomeName = homeName ?? "no recorded home";
				ArrivedLabel = arrivedLabel ?? "tenure unrecorded";
				ServiceMarks = serviceMarks;
				StudyMarks = studyMarks;
			}

			internal string Label => KingdomPresentation.Rich(Name) + " — "
				+ KingdomPresentation.Rich(HomeName) + ", " + KingdomPresentation.Rich(CityName)
				+ "; " + KingdomPresentation.Rich(ArrivedLabel) + "; resident " + ResidentId;

			internal string GroomingLabel => Label + "; "
				+ KingdomGroomingRules.Progress(ServiceMarks, StudyMarks);
		}

		private static void TryTellFailure(string Text)
		{
			try
			{
				Popup.Show(Text);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: succession failure telling failed", ex);
			}
		}

		private static void TryTell(string Text)
		{
			try
			{
				Popup.Show(Text);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: succession telling failed", ex);
			}
		}

		private sealed class HeirRuntime
		{
			internal readonly KingdomHeir Rule;
			internal readonly string CityName;
			internal readonly string HomeName;
			internal readonly string ArrivedLabel;
			internal readonly int ServiceMarks;
			internal readonly int StudyMarks;

			internal HeirRuntime(KingdomHeir Rule, string CityName, string HomeName,
				string ArrivedLabel, int ServiceMarks, int StudyMarks)
			{
				this.Rule = Rule;
				this.CityName = CityName ?? "the realm";
				this.HomeName = HomeName ?? "no recorded home";
				this.ArrivedLabel = ArrivedLabel ?? "tenure unrecorded";
				this.ServiceMarks = ServiceMarks;
				this.StudyMarks = StudyMarks;
			}
		}

		private sealed class JournalSnapshot
		{
			internal readonly IBaseJournalEntry Entry;
			private readonly bool Revealed;
			private readonly string LearnedFrom;
			private readonly List<string> Attributes;

			internal JournalSnapshot(IBaseJournalEntry Entry)
			{
				this.Entry = Entry;
				Revealed = Entry.Revealed;
				LearnedFrom = Entry.LearnedFrom;
				Attributes = Entry.Attributes == null ? null : new List<string>(Entry.Attributes);
			}

			internal void Restore()
			{
				Entry.Revealed = Revealed;
				Entry.LearnedFrom = LearnedFrom;
				Entry.Attributes = Attributes == null ? null : new List<string>(Attributes);
				Entry.Updated();
			}
		}
	}
}
