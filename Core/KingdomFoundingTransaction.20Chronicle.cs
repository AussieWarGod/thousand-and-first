using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		internal static void RecordChronicleOnce(KingdomSystem System, string EventID,
			string Text, bool Accomplishment, string MuralText,
			Func<int> ReadStage, Action<int> WriteStage,
			Func<int?> ReadDisposition, Action<int> WriteDisposition,
			Func<bool> ValidateAuthority = null)
		{
			if (System == null || string.IsNullOrEmpty(EventID) || EventID.Length > 160 ||
				string.IsNullOrEmpty(Text) || ReadStage == null || WriteStage == null ||
				ReadDisposition == null || WriteDisposition == null)
			{
				throw new InvalidOperationException("The chronicle outbox identity is malformed.");
			}
			int stage = ReadStage();
			int existing = CountAccomplishments(EventID);
			int? rawDisposition = ReadDisposition();
			if (!KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(stage,
				rawDisposition.HasValue, rawDisposition.GetValueOrDefault(), existing,
				!Accomplishment || Options.GetOption("r_TAF_OptionChronicle") == "No",
				out var disposition, out var needsDispositionWrite))
			{
				throw new InvalidOperationException(
					"The chronicle outbox stage or journal disposition is malformed.");
			}
			if (needsDispositionWrite)
			{
				WriteDisposition((int)disposition);
				if (ReadDisposition() != (int)disposition)
				{
					throw new InvalidOperationException(
						"The migrated chronicle disposition was not retained.");
				}
			}
			if (stage == 0)
			{
				List<string> official = System.ChronicleEntries == null
					? null : new List<string>(System.ChronicleEntries);
				List<string> outsider = System.OutsiderEntries == null
					? null : new List<string>(System.OutsiderEntries);
				try
				{
					KingdomChronicle.Record(System, Text, Accomplishment: false,
						MuralText: null);
					WriteStage(1);
					if (ReadStage() != 1)
					{
						throw new InvalidOperationException(
							"The chronicle register outbox stage was not retained.");
					}
					stage = 1;
				}
				catch
				{
					RestoreList(System.ChronicleEntries, official);
					RestoreList(System.OutsiderEntries, outsider);
					throw;
				}
			}
			if (stage == 1)
			{
				existing = CountAccomplishments(EventID);
				rawDisposition = ReadDisposition();
				if (!KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(stage,
					rawDisposition.HasValue, rawDisposition.GetValueOrDefault(), existing,
					!Accomplishment || Options.GetOption("r_TAF_OptionChronicle") == "No",
					out disposition, out needsDispositionWrite))
				{
					throw new InvalidOperationException(
						"The chronicle outbox disposition changed incompatibly.");
				}
				if (needsDispositionWrite)
				{
					WriteDisposition((int)disposition);
				}
				if (disposition == KingdomChronicleDisposition.None)
				{
					disposition = Accomplishment &&
						Options.GetOption("r_TAF_OptionChronicle") != "No"
						? KingdomChronicleDisposition.Required
						: KingdomChronicleDisposition.Skipped;
					WriteDisposition((int)disposition);
					if (ReadDisposition() != (int)disposition)
					{
						throw new InvalidOperationException(
							"The chronicle journal decision was not retained before callback.");
					}
				}
				if (disposition == KingdomChronicleDisposition.Required)
				{
					existing = CountAccomplishments(EventID);
					if (existing > 1)
					{
						throw new InvalidOperationException(
							"The founding journal event id already appears more than once.");
					}
					if (existing == 0)
					{
						if (!KingdomChronicle.TryPrepareJournalProjection(EventID, MuralText,
							out string projectedMural, out string gospelText,
							out MuralWeight weight))
						{
							throw new InvalidOperationException(
								"The founding journal projection is malformed.");
						}
						int callbackStage = ReadStage();
						int? callbackDisposition = ReadDisposition();
						if (ValidateAuthority != null && !ValidateAuthority())
						{
							throw new InvalidOperationException(
								"The founding authority changed before the journal callback.");
						}
						try
						{
							JournalAPI.AddAccomplishment(Text.Capitalize() + ".",
								projectedMural, gospelText, null, "general",
								MuralCategory.CreatesSomething,
								weight,
								EventID, -1L);
						}
						catch
						{
							if ((ValidateAuthority != null && !ValidateAuthority()) ||
								CountAccomplishments(EventID) != 1)
							{
								throw;
							}
						}
						if (ReadStage() != callbackStage ||
							ReadDisposition() != callbackDisposition)
						{
							throw new InvalidOperationException(
								"The chronicle receipt changed during the journal callback.");
						}
						if (ValidateAuthority != null && !ValidateAuthority())
						{
							throw new InvalidOperationException(
								"The founding authority changed during the journal callback.");
						}
					}
					if (CountAccomplishments(EventID) != 1)
					{
						throw new InvalidOperationException(
							"The founding journal event was not retained exactly once.");
					}
					disposition = KingdomChronicleDisposition.Inserted;
					WriteDisposition((int)disposition);
					if (ReadDisposition() != (int)disposition)
					{
						throw new InvalidOperationException(
							"The inserted journal disposition was not retained.");
					}
				}
				else if (disposition == KingdomChronicleDisposition.Inserted)
				{
					if (CountAccomplishments(EventID) != 1)
					{
						throw new InvalidOperationException(
							"The inserted journal disposition lost its exact row.");
					}
				}
				else if (disposition == KingdomChronicleDisposition.Skipped)
				{
					if (CountAccomplishments(EventID) != 0)
					{
						throw new InvalidOperationException(
							"A skipped journal disposition unexpectedly has a row.");
					}
				}
				else
				{
					throw new InvalidOperationException(
						"The chronicle journal disposition is not terminal.");
				}
				if (ValidateAuthority != null && !ValidateAuthority())
				{
					throw new InvalidOperationException(
						"The founding authority changed before chronicle completion.");
				}
				WriteStage(2);
				if (ReadStage() != 2)
				{
					throw new InvalidOperationException(
						"The completed chronicle outbox stage was not retained.");
				}
			}
		}

		private static int CountAccomplishments(string EventID)
		{
			int count = 0;
			if (JournalAPI.Accomplishments == null)
			{
				return 0;
			}
			foreach (JournalAccomplishment accomplishment in JournalAPI.Accomplishments)
			{
				if (accomplishment != null && accomplishment.ID == EventID)
				{
					count++;
				}
			}
			return count;
		}

		private static bool ChronicleAccomplishmentObserved(string EventID,
			KingdomChronicleDisposition Disposition)
		{
			int count = CountAccomplishments(EventID);
			return KingdomFoundingTransactionRules.ChronicleDispositionValid(2,
				Disposition, count);
		}

		/// <summary>Atomic compatibility helper for non-transaction civic events.</summary>
		internal static void RecordChronicleAtomically(KingdomSystem System, string Text,
			bool Accomplishment = false, string MuralText = null)
		{
			if (System == null)
			{
				throw new InvalidOperationException("No kingdom chronicle exists for this founding.");
			}
			List<string> official = System.ChronicleEntries == null
				? null : new List<string>(System.ChronicleEntries);
			List<string> outsider = System.OutsiderEntries == null
				? null : new List<string>(System.OutsiderEntries);
			try
			{
				KingdomChronicle.Record(System, Text, Accomplishment, MuralText);
			}
			catch
			{
				RestoreList(System.ChronicleEntries, official);
				RestoreList(System.OutsiderEntries, outsider);
				throw;
			}
		}

		private static void RestoreList(List<string> Target, List<string> Snapshot)
		{
			if (Target == null || Snapshot == null)
			{
				return;
			}
			Target.Clear();
			Target.AddRange(Snapshot);
		}

	}
}
