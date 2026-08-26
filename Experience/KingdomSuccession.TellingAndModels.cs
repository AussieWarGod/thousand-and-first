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

			internal HeirRuntime(KingdomHeir Rule)
			{
				this.Rule = Rule;
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
