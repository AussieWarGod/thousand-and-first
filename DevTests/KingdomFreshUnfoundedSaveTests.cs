#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>#271/#272: a game that has not founded a city must be saveable. KingdomSystem
	/// needs the engine, so this pin writes and checks, engine-free, the durable books a fresh
	/// KingdomSystem is constructed with, from the explicit list in
	/// FreshUnfoundedKingdomSystemEveryDurableBookIsWritable. Each is built exactly as its field
	/// initializer builds it and checked through its production codec and load validator (codec
	/// books), against an emulation of the engine's named-field writer (named-field books), or
	/// against its write gate (the empty settlement topologies). It is not a field census: a field
	/// added to KingdomSystem is not detected here, and no production source is read. The native
	/// unfounded-save persona performs a real engine save of every serialized field and is the
	/// complete census for a release build; docs/RELEASING.md requires that automated unfounded save
	/// and reload check for every release, and an in-game reflection census is #281. The other save
	/// systems a new game creates (KingdomSeal, KingdomCivicMemorySystem, and the optional succession
	/// and inheritance systems) and state written by play before the first save are not covered
	/// here (#275).</summary>
	public class KingdomFreshUnfoundedSaveTests
	{
		// How Walk treats each book type it can meet, from this fixture's own list. Named: written by
		// the engine's named-field writer alone, so its fields are walked. Guarded: its own writer runs
		// a load guard and then the named-field writer (the city book), so its fields are walked at
		// top level. Codec: its own writer, exercised at top level by the book's validator only.
		private enum BookWriter { Named, Guarded, Codec }

		private static readonly Dictionary<string, BookWriter> Writers = new Dictionary<string, BookWriter>
		{
			{ "KingdomLedger", BookWriter.Named }, { "KingdomLifecycleBook", BookWriter.Codec },
			{ "KingdomCityBook", BookWriter.Guarded }, { "KingdomBindingRegistry", BookWriter.Named },
			{ "KingdomJobRegistry", BookWriter.Named }, { "KingdomTradeBook", BookWriter.Codec },
			{ "KingdomPolityLedger", BookWriter.Codec }, { "KingdomFounderHistoryReceipt", BookWriter.Named },
			{ "KingdomPolityRealmTransition", BookWriter.Named }, { "KingdomExperienceLedger", BookWriter.Codec },
			{ "KingdomPolityDispatchState", BookWriter.Named }, { "KingdomSettlementTopology", BookWriter.Codec },
			{ "KingdomCarryBook", BookWriter.Codec }, { "KingdomResidentDepartureOperation", BookWriter.Named },
			{ "KingdomResidentAdmissionOperation", BookWriter.Named },
			{ "KingdomNamedCookReceipt", BookWriter.Named }, { "KingdomAssentingMootReceipt", BookWriter.Named }
		};

		[Test]
		public void FreshUnfoundedKingdomSystemEveryDurableBookIsWritable()
		{
			List<string> failures = new List<string>();
			Check(failures, "Ledger", new KingdomLedger(), b => Settled(b, x => ((KingdomLedger)x).Normalize()));
			Check(failures, "LifecycleBook", new KingdomLifecycleBook(), b => Lifecycle((KingdomLifecycleBook)b));
			Check(failures, "City", new KingdomCityBook(), b => City((KingdomCityBook)b));
			Check(failures, "Bindings", new KingdomBindingRegistry(),
				b => Settled(b, x => ((KingdomBindingRegistry)x).Normalize()));
#if !TAF_CONSTRUCTION_INPUT_PORTABLE
			Check(failures, "Jobs", new KingdomJobRegistry(), b => Settled(b, x => ((KingdomJobRegistry)x).Normalize()));
			Check(failures, "TradeBook", new KingdomTradeBook(), b => Trade((KingdomTradeBook)b));
#endif
			Check(failures, "PolityLedger", new KingdomPolityLedger(), b => Polity((KingdomPolityLedger)b));
			Check(failures, "FounderHistory", new KingdomFounderHistoryReceipt(), b => History((KingdomFounderHistoryReceipt)b));
			// Its Read normalizes after the named fields load, so the loaded transition must settle and validate.
			Check(failures, "PolityTransition", new KingdomPolityRealmTransition(), b =>
				Settled(b, x => ((KingdomPolityRealmTransition)x).Normalize())
				?? (KingdomPolityRules.TryValidateRealmTransition((KingdomPolityRealmTransition)b, out string f) ? null : f));
			Check(failures, "Experience", new KingdomExperienceLedger(), b => Experience((KingdomExperienceLedger)b));
			Check(failures, "PolityDispatch", new KingdomPolityDispatchState(), b =>
				KingdomPolityDispatchRules.ValidState((KingdomPolityDispatchState)b, out string f) ? null : f);
#if !TAF_CONSTRUCTION_INPUT_PORTABLE
			Check(failures, "SettlementTopology", new KingdomSettlementTopology(), b => Topology((KingdomSettlementTopology)b));
			Check(failures, "ExiledSettlementTopology", new KingdomSettlementTopology(),
				b => Topology((KingdomSettlementTopology)b));
#endif
			Check(failures, "CarryBook", new KingdomCarryBook(), b => Carry((KingdomCarryBook)b));
			Check(failures, "ResidentDeparture", new KingdomResidentDepartureOperation(), b =>
				KingdomResidentDepartureRules.IsEmpty((KingdomResidentDepartureOperation)b) ? null : "not empty");
#if !TAF_CONSTRUCTION_INPUT_PORTABLE
			Check(failures, "ResidentAdmission", new KingdomResidentAdmissionOperation(), b =>
				KingdomResidentAdmissionRules.Empty((KingdomResidentAdmissionOperation)b)
				&& KingdomResidentAdmissionRules.Valid((KingdomResidentAdmissionOperation)b) ? null : "not empty");
			// KingdomSystem.Write refreshes Away and ExiledAway from the topologies and Manifest
			// from the Trade manifest before writing; all three stay null in a fresh game.
			ClassicAssert.IsNull(new KingdomSettlementTopology().Get(0));
			ClassicAssert.IsNull(new KingdomTradeBook().Manifest);
#endif
			CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
		}

		private static void Check(List<string> failures, string field, object fresh, Func<object, string> validate)
		{
			string failure;
			try { failure = Walk(fresh, field, 0, new StringBuilder()) ?? validate(fresh); }
			catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; }
			if (failure != null) failures.Add(field + ": " + failure);
			Console.WriteLine("FRESH-BOOK " + field + " " + (failure == null ? "writable" : "REFUSED " + failure));
		}

		// Mirrors SerializationWriter.WriteObject (decompiled 2.0.211.56
		// XRL/World/SerializationWriter.cs:715-1236) for what a fresh book holds: engine-native
		// values, collections of them and named-field composites, recursively. Any other object
		// would reach the engine's BinaryFormatter fallback, so it fails here.
		private static string Walk(object value, string path, int depth, StringBuilder image)
		{
			if (value == null || value is string || value is Enum || value.GetType().IsPrimitive)
			{
				image.Append(path).Append('=').Append(value ?? "<null>").Append('\n');
				return null;
			}
			if (depth > 6) return path + " nests too deeply";
			if (value is IDictionary map)
			{
				foreach (DictionaryEntry entry in map)
					if ((Walk(entry.Key, path + "{}", depth + 1, image)
						?? Walk(entry.Value, path + "{" + entry.Key + "}", depth + 1, image)) is string failure)
						return failure;
				return null;
			}
			if (value is IList list)
			{
				for (int i = 0; i < list.Count; i++)
					if (Walk(list[i], path + "[" + i + "]", depth + 1, image) is string failure) return failure;
				return null;
			}
			if (!Writers.TryGetValue(value.GetType().Name, out BookWriter writer))
				return path + " (" + value.GetType().Name + ") has no engine-free writer";
			// A book with its own writer is exercised only at top level, by its validator. A top-level
			// writer that ends in the named-field writer (the city book, after its load guard) writes
			// every field through WriteObject, so its fields are walked too.
			if (writer == BookWriter.Codec || (writer == BookWriter.Guarded && depth > 0))
				return depth == 0 ? null : path + " nests a book with its own writer";
			// SerializationWriter.WriteNamedFields selects public instance fields that are not
			// static, literal or NotSerialized (decompiled XRL/World/SerializationWriter.cs:2981-3008).
			foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
				if (!field.IsLiteral && !Attribute.IsDefined(field, typeof(NonSerializedAttribute))
					&& Walk(field.GetValue(value), path + "." + field.Name, depth + 1, image) is string failure) return failure;
			return null;
		}

		/// <summary>Load normalization reaches a fixed point that is still writable.</summary>
		private static string Settled(object book, Action<object> normalize)
		{
			StringBuilder once = new StringBuilder(), twice = new StringBuilder();
			normalize(book);
			string failure = Walk(book, "book", 0, once);
			normalize(book);
			Walk(book, "book", 0, twice);
			return failure ?? (once.ToString() == twice.ToString() ? null : "load normalization does not settle");
		}

		private static string History(KingdomFounderHistoryReceipt receipt)
		{
			string failure = Settled(receipt, x => ((KingdomFounderHistoryReceipt)x).Normalize());
			return failure ?? (receipt.Phase == KingdomFounderHistoryPhase.None && receipt.Fault == ""
				? null : "fresh founder history does not load idle");
		}

		private static string Lifecycle(KingdomLifecycleBook book)
		{
			byte[] saved = Bytes(w => KingdomLifecycleWireCodec.WriteLifecycle(w, book));
			KingdomLifecycleBook loaded = new KingdomLifecycleBook();
			using (BinaryReader reader = new BinaryReader(new MemoryStream(saved, false)))
				KingdomLifecycleWireCodec.ReadLifecycle(reader, loaded);
			return KingdomLifecycleRules.DormantLifecycleWireExact(loaded)
				&& Same(saved, Bytes(w => KingdomLifecycleWireCodec.WriteLifecycle(w, loaded)))
				? null : "lifecycle book does not reload dormant and byte-stable";
		}

		private static string City(KingdomCityBook book)
		{
			if (book.SubsidenceReadFailed || !book.HasValidSubsidenceStorage())
				return "subsidence storage is not writable and readable";
			if (!KingdomNamedCookRules.Validate(book.NamedCook, out string failure)
				|| !KingdomAssentingMootRules.Validate(book.AssentingMoot, out failure)) return failure;
			// The engine reads the written fields into a new book through the production load path:
			// residence and subsidence storage migration, then validated load normalization.
			StringBuilder saved = new StringBuilder(), reloaded = new StringBuilder();
			KingdomCityBook loaded = new KingdomCityBook();
			Walk(book, "book", 0, saved);
			loaded.ReadNamedState(() => CopyNamedFields(book, loaded));
			if ((Walk(loaded, "book", 0, reloaded) ?? (loaded.HasValidSubsidenceStorage() ? null : "invalid"))
				!= null || saved.ToString() != reloaded.ToString()) return "city book does not reload exactly";
			return Settled(book, x => ((KingdomCityBook)x).Normalize())
				?? (book.HasValidSubsidenceStorage() ? null : "normalized subsidence storage is unreadable");
		}

#if !TAF_CONSTRUCTION_INPUT_PORTABLE
		private static string Trade(KingdomTradeBook book)
		{
			byte[] saved = KingdomTradeCodec.EncodeEnvelope(book);
			KingdomTradeBook loaded = KingdomTradeCodec.DecodeEnvelopeRaw(saved);
			KingdomTradeRules.Normalize(loaded);
			return loaded.SchemaState == KingdomTradeSchemaState.Compatible
				&& Same(saved, KingdomTradeCodec.EncodeEnvelope(loaded)) ? null : "trade book does not reload exactly";
		}

		private static string Topology(KingdomSettlementTopology topology)
		{
			// An empty topology writes only its marker, version and a zero count.
			return topology.Count == 0 && !topology.HasOpaqueEvidence
				&& topology.NormalizeCurrent(out string failure) ? null : "topology is not empty and canonical";
		}
#endif

		private static string Polity(KingdomPolityLedger ledger)
		{
			if (!KingdomPolityRules.TryValidate(ledger, out string failure)) return failure;
			byte[] saved = KingdomPolityCodec.EncodeEnvelope(ledger);
			KingdomPolityLedger loaded = KingdomPolityCodec.DecodeEnvelope(saved);
			return KingdomPolityRules.TryValidate(loaded, out failure)
				&& Same(saved, KingdomPolityCodec.EncodeEnvelope(loaded)) ? null : "polity ledger does not reload exactly";
		}

		private static string Experience(KingdomExperienceLedger ledger)
		{
			if (!KingdomExperienceRules.TryValidate(ledger, out string failure)) return failure;
			byte[] saved = KingdomExperienceCodec.EncodeEnvelope(ledger);
			KingdomExperienceLedger loaded = KingdomExperienceCodec.DecodeEnvelope(saved);
			return KingdomExperienceRules.TryValidate(loaded, out failure)
				&& Same(saved, KingdomExperienceCodec.EncodeEnvelope(loaded)) ? null : "experience does not reload exactly";
		}

		private static string Carry(KingdomCarryBook book)
		{
			byte[] saved = Bytes(w => KingdomLifecycleWireCodec.WriteCarry(w, book));
			KingdomCarryBook loaded = new KingdomCarryBook();
			using (BinaryReader reader = new BinaryReader(new MemoryStream(saved, false)))
				KingdomLifecycleWireCodec.ReadCarry(reader, loaded);
			KingdomLifecycleRules.Normalize(loaded);
			return !loaded.Quarantined && !loaded.WireRejected
				&& Same(saved, Bytes(w => KingdomLifecycleWireCodec.WriteCarry(w, loaded))) ? null : "carry does not reload exactly";
		}

		/// <summary>What SerializationReader.ReadNamedFields assigns (decompiled 2.0.211.56
		/// XRL/World/SerializationReader.cs:395-415): each field the named-field writer wrote, by
		/// name, read back into new objects.</summary>
		private static void CopyNamedFields(object source, object target)
		{
			foreach (FieldInfo field in source.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
				if (!field.IsLiteral && !Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
					field.SetValue(target, Clone(field.GetValue(source)));
		}

		private static object Clone(object value)
		{
			if (value == null || value is string || value is Enum || value.GetType().IsPrimitive) return value;
			object copy = Activator.CreateInstance(value.GetType());
			if (value is IDictionary map)
				foreach (DictionaryEntry entry in map) ((IDictionary)copy).Add(Clone(entry.Key), Clone(entry.Value));
			else if (value is IList list)
				foreach (object item in list) ((IList)copy).Add(Clone(item));
			else CopyNamedFields(value, copy);
			return copy;
		}

		private static byte[] Bytes(Action<BinaryWriter> write)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				write(new BinaryWriter(stream));
				return stream.ToArray();
			}
		}

		private static bool Same(byte[] left, byte[] right)
		{
			return Convert.ToBase64String(left) == Convert.ToBase64String(right);
		}
	}
}
#endif
