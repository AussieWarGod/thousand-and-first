using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Synthetic pending-summary cuts after real departures; recovery uses production
	/// entry points. Retains the profile and receipts. No append-callback cut or save/load proof.</summary>
	internal static class KingdomSubsidenceNativeLossChecks
	{
		internal static int Run(KingdomSystem system, Zone zone, KingdomSurvey survey, long now,
			StringBuilder results)
		{
			Check(results != null && system != null && zone != null && survey != null, "loss fixture arguments missing");
			Frame frame = new Frame(system, zone, survey, now);
			ChronicleLost(frame);
			results.Append("\nsynthetic-terminal-chronicle-loss=PASS; sink-dispositions=seeded; append-callback-cut=untested");
			KingdomSubsidenceReportPlan reset = LedgerReset(frame);
			results.Append("\nsynthetic-empty-ledger-reset-aba=PASS; pending-intent-and-news-counter=seeded");
			HomecomingChanged(frame, reset);
			results.Append("\nsynthetic-unseen-homecoming-news=PASS; callback-news=seeded; rollback=none; save-load=untested");
			return 3;
		}

		private static void ChronicleLost(Frame frame)
		{
			KingdomSubsidenceReportPlan report = frame.SeedBatch("synthetic native Chronicle loss", "", false);
			string eventId = KingdomSubsidenceReportRules.EventId(report, 0);
			string priorRegistry = frame.Registry();
			Check(KingdomChronicleReceiptRules.TryParseRegistry(priorRegistry, out List<KingdomChronicleReceipt> rows,
				out bool migrated, out _) && !migrated
				&& KingdomChronicleReceiptRules.TryWriteRegistry(rows, out string canonical, out _)
				&& canonical == priorRegistry, "native Chronicle prefix is not canonical");
			foreach (KingdomChronicleReceipt row in rows)
				Check(row.EventId != eventId, "synthetic terminal receipt already exists");
			Check(KingdomChronicleReceiptRules.TryCanonicalHash("taf-chronicle-at-v1", new[] {
				frame.Realm, frame.Settlement, eventId, report.Entries[0].Text,
				frame.Now.ToString(CultureInfo.InvariantCulture) }, out string fingerprint), "dated fingerprint refused");
			rows.Add(new KingdomChronicleReceipt { EventId = eventId, Fingerprint = fingerprint,
				Compact = true, Updated = frame.Now, OfficialState = KingdomChronicleSinkDisposition.Lost,
				OutsiderState = KingdomChronicleSinkDisposition.Lost, JournalState = KingdomChronicleSinkDisposition.Skipped });
			Check(KingdomChronicleReceiptRules.TryWriteRegistry(rows, out string seeded, out _)
				&& seeded.StartsWith(priorRegistry + "\n", StringComparison.Ordinal)
				&& frame.Registry() == priorRegistry, "terminal seed would replace unrelated Chronicle evidence");
			frame.Game.StringGameState[Frame.RegistryKey] = seeded;
			Check(frame.Registry() == seeded, "synthetic terminal receipt did not persist exactly");
			string[] official = frame.System.ChronicleEntries.ToArray(), outsider = frame.System.OutsiderEntries.ToArray();
			int departures = frame.Ledger.Departures;
			frame.Resume();
			KingdomSubsidenceReportPlan lost = frame.Archived(report);
			Check(lost.Entries[0].ChronicleLost && !lost.Entries[0].ChronicleProved
				&& lost.Entries[0].LedgerPhase == ReportLedgerPhase.Skipped
				&& frame.Ledger.Departures == departures && frame.Registry() == seeded,
				"terminal Chronicle loss was reclassified or physical accounting replayed");
			Exact(frame.System.ChronicleEntries, official); Exact(frame.System.OutsiderEntries, outsider);
			string warning = KingdomSubsidenceReportArchive.Digest(frame.Read());
			string shown = frame.ReadHomecoming();
			Check(warning != null && warning.Contains("delivery is not claimed") && shown.Contains(warning)
				&& shown.Contains(report.Entries[0].Text) && shown.Contains(eventId)
				&& frame.Read().FailureModel == KingdomSubsidenceReportArchive.None && !frame.Ledger.Any
				&& frame.Ledger.Departures == 0 && frame.Notes.Count == 0
				&& KingdomChronicle.TryProveLostOnceAt(frame.System, eventId, report.Entries[0].Text, frame.Now),
				"homecoming did not acknowledge the exact warning without claiming Chronicle delivery");
		}

		private static KingdomSubsidenceReportPlan LedgerReset(Frame frame)
		{
			frame.ReadHomecoming();
			Check(frame.Read().FailureModel == KingdomSubsidenceReportArchive.None
				&& !frame.Ledger.Any && frame.Notes.Count == 0, "empty-ledger fixture is not fresh");
			// Synthetic news makes the real adapter take Reset even though Notes is empty.
			frame.Ledger.Fetched = 1;
			KingdomSubsidenceReportPlan report = frame.SeedBatch("synthetic native empty-ledger reset",
				"synthetic pending summary must never return after reset", true);
			KingdomSubsidenceReportEntry before = report.Entries[0];
			Check(before.LedgerPhase == ReportLedgerPhase.Intent && before.BeforeCount == 0
				&& KingdomSubsidenceReportRules.LedgerAction(report, 0, frame.Notes) == KingdomSubsidenceEffectAction.Apply,
				"empty-ledger intent does not authorize its frozen before");
			frame.ReadHomecoming();
			KingdomSubsidenceBatch batch = null;
			KingdomSubsidenceReportPlan reset = null;
			Check(frame.Ledger.Fetched == 0 && frame.Notes.Count == 0
				&& KingdomSubsidenceBatchCodec.TryDecode(frame.Read().BatchModel, out batch)
				&& KingdomSubsidenceReportCodec.TryDecode(batch.ReportModel, out reset),
				"real homecoming did not reset the empty note list and retain its pending report");
			KingdomSubsidenceReportEntry after = reset.Entries[0];
			Check(after.LedgerPhase == ReportLedgerPhase.Lost && after.LedgerLoss == LedgerLossKind.HomecomingReset
				&& after.BeforeCount == before.BeforeCount && after.BeforeHash == before.BeforeHash
				&& after.AfterCount == 0 && after.AfterHash == before.BeforeHash
				&& !after.ChronicleProved && !after.ChronicleLost, "reset ABA did not retain the exact lost before witness");
			frame.Resume();
			KingdomSubsidenceReportPlan archived = frame.Archived(report);
			Check(archived.Entries[0].LedgerPhase == ReportLedgerPhase.Lost
				&& archived.Entries[0].LedgerLoss == LedgerLossKind.HomecomingReset
				&& archived.Entries[0].BeforeHash == before.BeforeHash && archived.Entries[0].ChronicleProved
				&& !archived.Entries[0].ChronicleLost && frame.Notes.Count == 0 && frame.Ledger.Departures == 0,
				"driver reappended a reset summary or failed to preserve its ledger loss");
			string wire = frame.City.SubsidenceModel, registry = frame.Registry();
			frame.Resume();
			Check(frame.City.SubsidenceModel == wire && frame.Registry() == registry && frame.Notes.Count == 0,
				"same-tick loss recovery replayed a settled report");
			return archived;
		}

		private static void HomecomingChanged(Frame frame, KingdomSubsidenceReportPlan report)
		{
			KingdomSubsidenceStepBook before = frame.Read();
			string wire = frame.City.SubsidenceModel, warning = KingdomSubsidenceReportArchive.Digest(before);
			const string news = "synthetic unseen news written during the homecoming callback";
			Check(frame.Notes.Count == 0 && frame.Ledger.Fetched == 0 && frame.Ledger.Departures == 0
				&& before.FailureModel != KingdomSubsidenceReportArchive.None, "unseen-news fixture lacks its unread warning");
			string shown = null;
			int calls = 0, days = frame.System.HomecomingDays;
			bool read = KingdomSubsidenceStepRuntime.TryReadHomecoming(frame.System, text => {
				calls++; shown = text; frame.Ledger.Fetched++; frame.Notes.Add(news);
			}, out string refusal);
			Check(!read && !string.IsNullOrEmpty(refusal) && refusal.Contains("retained") && calls == 1
				&& shown != null && shown.Contains(warning) && !shown.Contains(news)
				&& frame.Read().FailureModel == before.FailureModel && frame.City.SubsidenceModel == wire
				&& frame.Ledger.Fetched == 1 && frame.Ledger.Departures == 0 && frame.System.HomecomingDays == days
				&& frame.Notes.Count == 1 && frame.Notes[0] == news,
				"callback-written unseen news was reset or its unseen failure archive acknowledged");
			// No restoration: the next real read must see both the retained news and warning.
			shown = frame.ReadHomecoming();
			Check(shown.Contains(news) && shown.Contains(warning)
				&& frame.Read().FailureModel == KingdomSubsidenceReportArchive.None
				&& !frame.Ledger.Any && frame.Notes.Count == 0 && frame.System.HomecomingDays == 0
				&& KingdomChronicle.TryProveOnceAt(frame.System, KingdomSubsidenceReportRules.EventId(report, 0),
					report.Entries[0].Text, frame.Now), "next homecoming did not acknowledge the retained exact news and loss");
		}

		private sealed class Frame
		{
			internal const string RegistryKey = "r_TAF_ChronicleEventRegistry_v1";
			internal readonly XRLGame Game;
			internal readonly KingdomSystem System;
			internal readonly KingdomCityBook City;
			internal readonly KingdomLedger Ledger;
			internal readonly List<string> Notes;
			internal readonly string Realm, Settlement, Binding;
			internal readonly long Now, Token;
			private readonly Zone Zone;
			private readonly KingdomSurvey Survey;
			private readonly GameObject Player;
			private readonly object[] Tables;
			internal Frame(KingdomSystem system, Zone zone, KingdomSurvey survey, long now)
			{
				Game = The.Game; Player = The.Player; System = system; City = system.City;
				Ledger = system.Ledger; Notes = Ledger?.Notes; Zone = zone; Survey = survey;
				Realm = system.CurrentRealmId; Settlement = system.CurrentSettlementId; Binding = system.SubsidenceBinding;
				Now = now; Token = system.MasterAppliedResumeToken;
				Check(Game != null && City != null && Ledger != null && Notes != null, "native loss owner missing");
				Tables = new object[] { Game.StringGameState, Game.IntGameState, Game.Int64GameState,
					Game.ObjectGameState, Game.BooleanGameState };
				KingdomSubsidenceNativeFixture fixture = KingdomSubsidenceNativeFixture.LastAttempt;
				Check(fixture != null && ReferenceEquals(fixture.Game, Game) && ReferenceEquals(fixture.System, system)
					&& ReferenceEquals(fixture.Zone, zone) && ReferenceEquals(fixture.Survey, survey), "not the retained native fixture");
				Check(Read().BatchModel == KingdomSubsidenceBatchRules.None
					&& Read().FailureModel == KingdomSubsidenceReportArchive.None, "native loss cuts require retired clean accounting");
			}
			internal KingdomSubsidenceStepBook Read()
			{
				Check(ReferenceEquals(The.Game, Game) && ReferenceEquals(The.Player, Player)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && System.Founded
					&& ReferenceEquals(System.City, City) && System.CurrentRealmId == Realm
					&& System.CurrentSettlementId == Settlement && City.SettlementId == Settlement
					&& System.MasterAppliedResumeToken == Token && Game.TimeTicks == Now
					&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone) && ReferenceEquals(Player?.CurrentZone, Zone)
					&& ReferenceEquals(KingdomSurvey.ActiveFor(Zone), Survey) && ReferenceEquals(Survey.Ground, Zone)
					&& ReferenceEquals(System.Ledger, Ledger) && ReferenceEquals(Ledger.Notes, Notes)
					&& System.LastSubsidenceTick == Now && System.Population == 45 && KingdomResidents.OnRollCount(System) == 45
					&& System.Stage == GrowthStage.City && System.SubsidenceBinding == Binding
					&& KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture),
					"native loss recovery changed the exact owner, world clock or completed resident accounting");
				KingdomSubsidenceStepBook book = null;
				Check(City.HasValidSubsidenceStorage() && KingdomSubsidenceStepCodec.TryDecode(City.SubsidenceModel,
					out book) && KingdomSubsidenceStepRules.Valid(book)
					&& book.Admission == KingdomSubsidenceAdmission.Admitted && book.RealmId == Realm
					&& book.SettlementId == Settlement && book.Sequence == 1 && book.LastRetiredTick == Now
					&& book.Active == null && book.OptionModel == KingdomSubsidenceStepRules.NoOption,
					"native loss recovery altered the retired step identity or clock receipt");
				return book;
			}
			internal KingdomSubsidenceReportPlan SeedBatch(string name, string ledgerText, bool arm)
			{
				KingdomSubsidenceStepBook book = Read(); string prior = City.SubsidenceModel;
				Check(book.BatchModel == KingdomSubsidenceBatchRules.None && book.FailureModel == KingdomSubsidenceReportArchive.None,
					"synthetic batch would replace a pending account");
				KingdomSubsidenceStepBook temporary = new KingdomSubsidenceStepBook(KingdomSubsidenceAdmission.Admitted, Realm, Settlement, 0, null);
				Check(KingdomSubsidenceBatchRules.TryBegin(temporary, Now - KingdomSubsidenceStepRules.StepTicks, Now,
					5, name, Binding, out KingdomSubsidenceBatch batch), "synthetic closed batch could not be prepared");
				KingdomSubsidenceReportPlan report = new KingdomSubsidenceReportPlan(batch.Id, Realm, Settlement,
					new[] { new KingdomSubsidenceReportEntry(name + " after five accounted departures", ledgerText, Now) });
				if (arm) { Check(KingdomSubsidenceReportRules.TryArmLedger(report, 0, Notes, out KingdomSubsidenceReportPlan armed),
					"synthetic ledger intent could not be armed"); report = armed; }
				string raw = Registry();
				Check(KingdomChronicleReceiptRules.TryParseRegistry(raw, out List<KingdomChronicleReceipt> receipts,
					out bool migrated, out _) && !migrated
					&& KingdomChronicleReceiptRules.TryWriteRegistry(receipts, out string canonical, out _) && canonical == raw,
					"synthetic batch requires an exact canonical Chronicle registry");
				foreach (KingdomChronicleReceipt receipt in receipts)
					Check(receipt.EventId != KingdomSubsidenceReportRules.EventId(report, 0), "synthetic report identity already exists");
				string batchWire = null, wire = null;
				Check(KingdomSubsidenceReportCodec.TryEncode(report, out string reportWire)
					&& KingdomSubsidenceBatchCodec.TryEncode(batch.Copy(departed: 5, closing: true, closedTick: Now,
						reportModel: reportWire), out batchWire), "synthetic report/batch encoding refused");
				KingdomSubsidenceStepBook next = book.WithBatch(batchWire);
				Check(KingdomSubsidenceStepRules.Valid(next) && KingdomSubsidenceStepCodec.TryEncode(next, out wire)
					&& Read().BatchModel == book.BatchModel && City.SubsidenceModel == prior, "synthetic book seed lost its exact prior");
				City.SubsidenceModel = wire;
				Check(Read().BatchModel == batchWire && City.SubsidenceModel == wire, "synthetic book seed did not persist exactly");
				return report;
			}
			internal void Resume()
			{
				Read(); bool ok = KingdomSubsidenceStepRuntime.TryBeforePass(System, Zone, Survey, out string refusal);
				Check(ok && refusal == null && Read().BatchModel == KingdomSubsidenceBatchRules.None,
					"real pre-pass recovery did not settle the synthetic summary cut: " + refusal);
			}
			internal KingdomSubsidenceReportPlan Archived(KingdomSubsidenceReportPlan expected)
			{
				KingdomSubsidenceReportPlan report = null;
				Check(KingdomSubsidenceReportArchive.TryRead(Read().FailureModel, out List<string> rows) && rows.Count == 1
					&& KingdomSubsidenceReportCodec.TryDecode(rows[0], out report)
					&& report.OwnerId == expected.OwnerId && report.Entries.Count == 1
					&& report.Entries[0].Text == expected.Entries[0].Text && report.Entries[0].AtTick == Now
					&& KingdomSubsidenceReportRules.Settled(report) && !KingdomSubsidenceReportRules.Complete(report),
					"failure archive did not retain one exact settled-but-undelivered report");
				return report;
			}
			internal string ReadHomecoming()
			{
				Read(); string shown = null; int calls = 0;
				bool ok = KingdomSubsidenceStepRuntime.TryReadHomecoming(System, text => { calls++; shown = text; }, out string refusal);
				Check(ok && refusal == null && calls == 1 && shown != null, "real homecoming read refused: " + refusal);
				Read(); return shown;
			}
			internal string Registry()
			{
				Read(); object[] current = { Game.StringGameState, Game.IntGameState, Game.Int64GameState, Game.ObjectGameState, Game.BooleanGameState };
				for (int i = 0; i < current.Length; i++) Check(current[i] != null && ReferenceEquals(current[i], Tables[i]), "native state table changed");
				string raw = null;
				Check(!Game.HasIntGameState(RegistryKey) && !Game.HasInt64GameState(RegistryKey)
					&& !Game.HasObjectGameState(RegistryKey) && !Game.HasBooleanGameState(RegistryKey)
					&& Game.StringGameState.TryGetValue(RegistryKey, out raw) && raw != null, "native Chronicle registry shape changed");
				return raw;
			}
		}
		private static void Exact(List<string> actual, string[] expected)
		{ Check(actual.Count == expected.Length, "native Chronicle list changed length"); for (int i = 0; i < expected.Length; i++) Check(actual[i] == expected[i], "native Chronicle prefix changed"); }
		private static void Check(bool condition, string detail)
		{ if (!condition) throw new InvalidOperationException(detail); }
	}
}
