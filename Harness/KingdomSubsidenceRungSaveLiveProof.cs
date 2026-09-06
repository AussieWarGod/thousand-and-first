using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Exact proofs shared by the warm D5 rung save side and the cold load witness: the LIVE
	/// typed designation of the one work, the bounded telling digest, and the dated reports one
	/// recovery owes. Every check here reads real live or persisted state.</summary>
	internal static class KingdomSubsidenceRungSaveLiveProof
	{
		internal const int MaxTellingLines = 65536;
		internal const int MaxTellingLineChars = 65536;

		/// <summary>Aggregate cap on the framed digest text. The per-line bounds alone would admit
		/// four gibibytes; one mebibyte is far past anything this fixture can tell.</summary>
		internal const int MaxTellingChars = 1048576;

		/// <summary>Works one breakpoint of this witness can plan: the synthetic hut, and the actual
		/// completed founding heart when production's own ruin draw takes it too.</summary>
		internal const int MaxPlannedWorks = 2;

		/// <summary>The dated reports one rung owes: its stage line, its closing departure summary,
		/// and one telling line for each planned work &mdash; the named ruin production allows per
		/// breakpoint, then the one line that carries whatever it did not name.</summary>
		internal static int ExpectedReports(KingdomSubsidenceRungPlan Plan)
		{
			Check(Plan != null && Plan.Works != null && Plan.Works.Count >= 1
				&& Plan.Works.Count <= MaxPlannedWorks,
				"the rung plan carries no bounded planned work set for its dated reports");
			return 2 + Plan.Works.Count;
		}

		internal static int WearCopies(GameObject Work)
		{
			int copies = 0;
			if (Work != null && Work.PartsList != null)
				foreach (IPart part in Work.PartsList) if (part is r_KingdomWear) copies++;
			return copies;
		}

		/// <summary>The live typed designation, proved against the frozen row: KingdomBuilt present in
		/// the int table and absent from the string table, its raw value, the plot id and build key
		/// present exactly when the row carries one, and the one referenced wear part.</summary>
		internal static void ProveDesignation(GameObject Work, r_KingdomWear Wear, string PlotId,
			string DesignStamp, string Failure)
		{
			Check(GameObject.Validate(Work) && Wear != null && WearCopies(Work) == 1
				&& ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear)
				&& ReferenceEquals(Wear.ParentObject, Work)
				&& Work.HasIntProperty(KingdomAdopt.BuiltProperty)
				&& !Work.HasStringProperty(KingdomAdopt.BuiltProperty)
				&& Work.GetIntProperty(KingdomAdopt.BuiltProperty) == 1
				&& Raw(Work, KingdomPlots.PlotIdProperty, PlotId == "" ? null : PlotId)
				&& Raw(Work, KingdomUpgrade.BuildKeyProperty, DesignStamp), Failure);
		}

		/// <summary>The completed heart's LIVE typed designation and ground, proved against
		/// production's own canonical terminal binding: one exact cell of this zone, sole custody, the
		/// built mark in the int table and absent from the string table, and the plot and build key
		/// the terminal names. Deliberately does not read wear: whether the heart carries any is the
		/// separate proof <see cref="KingdomSubsidenceRungSaveHeartProof"/> owns.</summary>
		internal static void ProveHeartGround(GameObject Heart, Zone Ground, string Blueprint,
			string BuildKey, string PlotId, int X, int Y, string Failure)
		{
			Check(GameObject.Validate(Heart) && Heart.Blueprint == Blueprint
				&& Heart.CurrentZone == Ground && Heart.CurrentCell != null
				&& Heart.CurrentCell == Ground.GetCell(X, Y)
				&& Heart.Count == 1 && Heart.InInventory == null && Heart.Equipped == null
				&& Heart.HasIntProperty(KingdomAdopt.BuiltProperty)
				&& !Heart.HasStringProperty(KingdomAdopt.BuiltProperty)
				&& Heart.GetIntProperty(KingdomAdopt.BuiltProperty) == 1
				&& Raw(Heart, KingdomPlots.PlotIdProperty, PlotId == "" ? null : PlotId)
				&& Raw(Heart, KingdomUpgrade.BuildKeyProperty, BuildKey), Failure);
		}

		/// <summary>Length-framed digest of both telling registers, whole.</summary>
		internal static string Telling(KingdomSystem System)
		{
			return Digest(System, System.ChronicleEntries.Count, System.OutsiderEntries.Count);
		}

		/// <summary>Length-framed digest of the first Chronicle and Outsider lines only, so a saved
		/// prefix stays provable after a recovery has appended to both registers.</summary>
		internal static string Digest(KingdomSystem System, int ChronicleCount, int OutsiderCount)
		{
			StringBuilder text = new StringBuilder("taf-rung-save-telling-v1");
			Frame(text, System.ChronicleEntries, ChronicleCount);
			Frame(text, System.OutsiderEntries, OutsiderCount);
			return KingdomScenarioSaveFiles.HashText(text.ToString());
		}

		/// <summary>The exact dated lines this rung's recovery owes, composed by production's own
		/// report rules from the frozen parent bytes: the stage line, its named ruin, and the closing
		/// batch's departure summary.</summary>
		internal static List<string> Reports(string StepWire, KingdomSubsidenceRungPlan Plan)
		{
			KingdomSubsidenceStepBook book = null;
			KingdomSubsidenceBatch batch = null;
			Check(Plan != null && Plan.Works != null
				&& KingdomSubsidenceStepCodec.TryDecode(StepWire, out book) && book.Active != null
				&& KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out batch),
				"the saved parent step or its departure batch cannot be read for its dated reports");
			string name = batch.Name;
			List<string> lines = new List<string>();
			lines.Add(Dated(name + " ceased to be a " + Plan.From.ToString().ToLowerInvariant()
				+ " and became a " + Plan.To.ToString().ToLowerInvariant() + " again", Plan.DueTick));
			int ruined = 0, named = 0, deepest = 0;
			foreach (KingdomSubsidenceRungWork work in Plan.Works)
			{
				if (work.AfterWear <= work.BeforeWear) continue;
				if (KingdomSubsidenceRules.TellsRuin(ruined++))
				{
					lines.Add(Dated(KingdomSubsidenceRules.RuinedWorkLine(work.Name, name), Plan.DueTick));
					named++;
				}
				deepest = Math.Max(deepest, work.AfterWear);
			}
			string ruins = KingdomSubsidenceRules.RuinSummary(name, ruined, named, deepest);
			if (ruins != null) lines.Add(Dated(ruins, Plan.DueTick));
			Check(KingdomSubsidenceRungSaveForecast.TryClosingBatch(book, out KingdomSubsidenceBatch closing),
				"the saved active step does not forecast one exact closing departure batch");
			string departures = KingdomSubsidenceRules.SlideDepartureSummary(closing.Name, closing.Departed,
				KingdomSubsidenceBatchRules.Named(closing), KingdomSubsidenceRules.DepartureCause(closing.Binding));
			if (departures != null) lines.Add(Dated(departures, closing.ClosedTick));
			Check(lines.Count == ExpectedReports(Plan),
				"the saved rung does not owe exactly two dated reports and one per planned work");
			return lines;
		}

		/// <summary>The saved prefix of both registers stands untouched and exactly the expected dated
		/// reports were added, each once and each among the new lines.</summary>
		internal static void ProveTelling(KingdomSystem System, KingdomSubsidenceRungSaveSnapshot Snapshot,
			IReadOnlyList<string> Expected, int Reports)
		{
			Check(Expected != null && Reports >= 3 && Expected.Count == Reports
				&& System.ChronicleEntries.Count == Snapshot.ChronicleCount + Expected.Count
				&& System.OutsiderEntries.Count == Snapshot.OutsiderCount + Expected.Count
				&& Digest(System, Snapshot.ChronicleCount, Snapshot.OutsiderCount) == Snapshot.TellingDigest,
				"the recovered telling did not keep the saved prefix and add exactly its owed reports");
			for (int i = 0; i < Expected.Count; i++)
				Check(Count(System.ChronicleEntries, Expected[i], 0) == 1
					&& Count(System.ChronicleEntries, Expected[i], Snapshot.ChronicleCount) == 1,
					"a recovered dated report is missing, duplicated or told outside the new lines");
		}

		internal static bool ExactBody(KingdomSystem System, Zone Zone, GameObject Body, int Id, string ObjectId)
		{
			return GameObject.Validate(Body) && Body.IsAlive && Body.CurrentZone == Zone
				&& Body.IDIfAssigned == ObjectId && !Body.IsPlayerLed()
				&& KingdomCitizenship.BelongsTo(System, Body) && KingdomResidents.IdOf(Body) == Id
				&& System.City.TryResidentRow(Id, out _)
				&& System.Bindings.TryReadExact(out KingdomBindingTable bindings, out _)
				&& bindings.TryGet(Id, KingdomBindingKind.Resident, out KingdomBinding binding)
				&& binding.ObjectId == ObjectId && binding.ZoneId == Zone.ZoneID;
		}

		internal static bool SameLines(List<string> Current, string[] Before)
		{
			if (Current.Count != Before.Length) return false;
			for (int i = 0; i < Before.Length; i++) if (Current[i] != Before[i]) return false;
			return true;
		}

		private static bool Raw(GameObject Work, string Key, string Expected)
		{
			return !Work.HasIntProperty(Key) && Work.HasStringProperty(Key) == (Expected != null)
				&& (Expected == null || Work.GetStringProperty(Key) == Expected);
		}

		private static void Frame(StringBuilder Text, List<string> Lines, int Count)
		{
			Check(Lines != null && Count >= 0 && Count <= Lines.Count && Lines.Count <= MaxTellingLines,
				"a telling register is absent or exceeds its bound");
			Append(Text, ":" + Count.ToString(CultureInfo.InvariantCulture));
			for (int i = 0; i < Count; i++)
			{
				string line = Lines[i];
				Check(line != null && line.Length <= MaxTellingLineChars,
					"a telling line is absent or exceeds its bound");
				Append(Text, ":" + line.Length.ToString(CultureInfo.InvariantCulture) + ":");
				Append(Text, line);
			}
		}

		private static void Append(StringBuilder Text, string Part)
		{
			Check(Text.Length + Part.Length <= MaxTellingChars,
				"the framed telling text exceeds its aggregate bound");
			Text.Append(Part);
		}

		private static string Dated(string Text, long Tick)
		{
			// Qualified: System.Globalization is in scope here and also declares a Calendar.
			return "On the " + XRL.World.Calendar.GetDay(Tick) + " of " + XRL.World.Calendar.GetMonth(Tick)
				+ ", " + XRL.World.Calendar.GetYear(Tick) + " AR, " + Text + ".";
		}

		private static int Count(List<string> Lines, string Text, int From)
		{
			int count = 0;
			for (int i = From; i < Lines.Count; i++) if (Lines[i] == Text) count++;
			return count;
		}

		private static void Check(bool Condition, string Failure)
		{
			KingdomScenarioSaveFiles.Require(Condition, Failure ?? "native rung live proof refused");
		}
	}
}
