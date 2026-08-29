#if !TAF_TESTS
using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The real wiring: each known section id to the frozen codec that owns it.
	/// <para>
	/// This is the only file in the authority that names the eight wire families, and it is
	/// compiled out of the pure test projects for the reason given on
	/// <see cref="KingdomCivicMemoryFamilyTable"/> &mdash; two of those families reach the engine
	/// through their own rules. Everything the envelope does with the answers lives on the pure
	/// side and is tested there; what lives here is the introduction, and a source-contract test
	/// checks that all nine introductions are still made.
	/// </para>
	/// </summary>
	public static class KingdomCivicMemoryFamilyBindings
	{
		/// <summary>Builds a table with every known family answering for itself.</summary>
		public static KingdomCivicMemoryFamilyTable Table()
		{
			return new KingdomCivicMemoryFamilyTable()
				.Add(KingdomCivicMemoryLimits.SectionCivicArtifacts, Artifacts)
				.Add(KingdomCivicMemoryLimits.SectionCivicPractice, Practice)
				.Add(KingdomCivicMemoryLimits.SectionBodyHistory, BodyHistory)
				.Add(KingdomCivicMemoryLimits.SectionCuriosity, Curiosity)
				.Add(KingdomCivicMemoryLimits.SectionCivicLeads, Leads)
				.Add(KingdomCivicMemoryLimits.SectionTreaty, Treaty)
				.Add(KingdomCivicMemoryLimits.SectionCommunalRite, CommunalRite)
				.Add(KingdomCivicMemoryLimits.SectionGuestFeast, GuestFeast)
				.Add(KingdomCivicMemoryLimits.SectionVillageCovenant, VillageCovenant);
		}

		private static KingdomCivicMemoryNested Artifacts(byte[] Payload, out string Fault)
		{
			try
			{
				KingdomCivicArtifactsEnvelope envelope = KingdomCivicArtifactsCodec.Decode(Payload);
				KingdomCivicMemoryNested framing = EnvelopeState(envelope == null,
					envelope != null && envelope.IsOpaqueFuture,
					envelope != null && envelope.Quarantined, "civic artifacts",
					envelope == null ? null : envelope.Fault, out Fault);
				if (framing != KingdomCivicMemoryNested.Current) return framing;
				string identityFault;
				if (KingdomCivicArtifactsStore.TryValidateIdentity(envelope, out identityFault))
				{
					Fault = "";
					return KingdomCivicMemoryNested.Current;
				}
				return InvalidIdentity("civic artifacts", identityFault, out Fault);
			}
			catch (Exception e) when (WireFault(e)) { return Threw("civic artifacts", e, out Fault); }
		}

		private static KingdomCivicMemoryNested Practice(byte[] Payload, out string Fault)
		{
			try
			{
				KingdomCivicPracticeEnvelope envelope = KingdomCivicPracticeCodec.Decode(Payload);
				KingdomCivicMemoryNested framing = EnvelopeState(envelope == null,
					envelope != null && envelope.IsOpaqueFuture,
					envelope != null && envelope.Quarantined, "civic practice",
					envelope == null ? null : envelope.Fault, out Fault);
				if (framing != KingdomCivicMemoryNested.Current) return framing;
				string identityFault;
				if (KingdomCivicPracticeStore.TryValidateIdentity(envelope, out identityFault))
				{
					Fault = "";
					return KingdomCivicMemoryNested.Current;
				}
				return InvalidIdentity("civic practice", identityFault, out Fault);
			}
			catch (Exception e) when (WireFault(e)) { return Threw("civic practice", e, out Fault); }
		}

		private static KingdomCivicMemoryNested BodyHistory(byte[] Payload, out string Fault)
		{
			try
			{
				KingdomBodyHistoryEnvelope envelope = KingdomBodyHistoryCodec.Decode(Payload);
				KingdomCivicMemoryNested framing = EnvelopeState(envelope == null,
					envelope != null && envelope.IsOpaqueFuture,
					envelope != null && envelope.Quarantined, "body history",
					envelope == null ? null : envelope.Fault, out Fault);
				if (framing != KingdomCivicMemoryNested.Current) return framing;
				string identityFault;
				if (KingdomBodyHistoryStore.TryValidateIdentity(envelope, out identityFault))
				{
					Fault = "";
					return KingdomCivicMemoryNested.Current;
				}
				return InvalidIdentity("body history", identityFault, out Fault);
			}
			catch (Exception e) when (WireFault(e)) { return Threw("body history", e, out Fault); }
		}

		/// <summary>O6 distinguishes compatible, future-opaque, and quarantined books exactly.</summary>
		private static KingdomCivicMemoryNested Curiosity(byte[] Payload, out string Fault)
		{
			try
			{
				KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(Payload);
				return CuriosityBook(book == null,
					book == null ? KingdomCuriosityBookState.Quarantined : book.State,
					"curiosity book", book == null ? null : book.Fault, out Fault);
			}
			catch (Exception e) when (WireFault(e)) { return Threw("curiosity book", e, out Fault); }
		}

		/// <summary>D7 uses the same explicit three-state disposition.</summary>
		private static KingdomCivicMemoryNested Leads(byte[] Payload, out string Fault)
		{
			try
			{
				KingdomCivicLeadBook book = KingdomCuriosityLeadCodec.DecodeLeads(Payload);
				return CuriosityBook(book == null,
					book == null ? KingdomCuriosityBookState.Quarantined : book.State,
					"civic-lead book", book == null ? null : book.Fault, out Fault);
			}
			catch (Exception e) when (WireFault(e)) { return Threw("civic-lead book", e, out Fault); }
		}

		/// <summary>
		/// Treaty. This family does distinguish the two, but not through <c>Quarantined</c>, which
		/// it sets for both. <c>StoreState</c> is the field that separates them
		/// (<c>Treaty/KingdomTreatyCodec.cs:67</c>), so that is the field read here.
		/// </summary>
		private static KingdomCivicMemoryNested Treaty(byte[] Payload, out string Fault)
		{
			try
			{
				ThousandAndFirst.Treaty.KingdomTreatyLedger ledger =
					ThousandAndFirst.Treaty.KingdomTreatyCodec.Decode(Payload);
				if (ledger == null) return Threw("treaty ledger",
					new InvalidDataException("the codec returned nothing at all"), out Fault);
				if (ledger.StoreState == ThousandAndFirst.Treaty.KingdomTreatyStoreState.FutureOpaque)
				{
					Fault = "";
					return KingdomCivicMemoryNested.Future;
				}
				return Book(false, ledger.StoreState ==
					ThousandAndFirst.Treaty.KingdomTreatyStoreState.Quarantined || ledger.Quarantined,
					"treaty ledger", ledger.Fault, out Fault);
			}
			catch (Exception e) when (WireFault(e)) { return Threw("treaty ledger", e, out Fault); }
		}

		private static KingdomCivicMemoryNested CommunalRite(byte[] Payload, out string Fault)
		{
			try
			{
				KingdomCommunalRiteBook book = KingdomCommunalRiteCodec.DecodeEnvelope(Payload);
				return ExperienceBook(book == null, book == null ? KingdomExperienceSchemaState.Quarantined
					: book.SchemaState, "communal-rite book",
					book == null ? null : book.SchemaFault, out Fault);
			}
			catch (Exception e) when (WireFault(e))
			{
				return Threw("communal-rite book", e, out Fault);
			}
		}

		private static KingdomCivicMemoryNested GuestFeast(byte[] Payload, out string Fault)
		{
			try
			{
				KingdomGuestFeastBook book = KingdomGuestFeastCodec.DecodeEnvelope(Payload);
				return ExperienceBook(book == null, book == null ? KingdomExperienceSchemaState.Quarantined
					: book.SchemaState, "guest-feast book",
					book == null ? null : book.SchemaFault, out Fault);
			}
			catch (Exception e) when (WireFault(e))
			{
				return Threw("guest-feast book", e, out Fault);
			}
		}

		/// <summary>
		/// D9's archive of completed village covenants, which unlike the other eight reaches no
		/// engine and can therefore keep its own disposition somewhere a test can run it. See
		/// <see cref="KingdomVillageCovenantInspection"/> for the three-state mapping itself.
		/// </summary>
		private static KingdomCivicMemoryNested VillageCovenant(byte[] Payload, out string Fault)
		{
			return KingdomVillageCovenantInspection.InspectGuarded(Payload, out Fault);
		}

		private static KingdomCivicMemoryNested ExperienceBook(bool Absent,
			KingdomExperienceSchemaState State, string Family, string FamilyFault,
			out string Fault)
		{
			if (!Absent && State == KingdomExperienceSchemaState.Unknown)
			{
				Fault = "";
				return KingdomCivicMemoryNested.Future;
			}
			return Book(Absent, State != KingdomExperienceSchemaState.Compatible,
				Family, FamilyFault, out Fault);
		}

		private static KingdomCivicMemoryNested CuriosityBook(bool Absent,
			KingdomCuriosityBookState State, string Family, string FamilyFault, out string Fault)
		{
			if (!Absent && State == KingdomCuriosityBookState.FutureOpaque)
			{
				Fault = "";
				return KingdomCivicMemoryNested.Future;
			}
			if (!Absent && State == KingdomCuriosityBookState.Compatible)
			{
				Fault = "";
				return KingdomCivicMemoryNested.Current;
			}
			if (!Absent && State != KingdomCuriosityBookState.Quarantined)
			{
				Fault = "the " + Family + " returned unsupported state " + (int)State;
				return KingdomCivicMemoryNested.Malformed;
			}
			return Book(Absent, true, Family, FamilyFault, out Fault);
		}

		private static KingdomCivicMemoryNested EnvelopeState(bool Absent, bool Future, bool Refused,
			string Family, string FamilyFault, out string Fault)
		{
			// Future is asked before refusal: these families set an opaque version rather than a
			// quarantine flag for a newer payload, and IsOpaqueFuture already excludes quarantine.
			if (!Absent && Future)
			{
				Fault = "";
				return KingdomCivicMemoryNested.Future;
			}
			return Book(Absent, Refused, Family, FamilyFault, out Fault);
		}

		private static KingdomCivicMemoryNested InvalidIdentity(string Family,
			string IdentityFault, out string Fault)
		{
			Fault = string.IsNullOrEmpty(IdentityFault)
				? "the " + Family + " identity is invalid" : IdentityFault;
			return KingdomCivicMemoryNested.Malformed;
		}

		private static KingdomCivicMemoryNested Book(bool Absent, bool Refused, string Family,
			string FamilyFault, out string Fault)
		{
			if (Absent)
			{
				Fault = "the " + Family + " codec returned nothing at all";
				return KingdomCivicMemoryNested.Malformed;
			}
			if (Refused)
			{
				Fault = "the " + Family + " was refused by its own codec ("
					+ (string.IsNullOrEmpty(FamilyFault) ? "no reason given" : FamilyFault) + ")";
				return KingdomCivicMemoryNested.Malformed;
			}
			Fault = "";
			return KingdomCivicMemoryNested.Current;
		}

		private static KingdomCivicMemoryNested Threw(string Family, Exception Thrown,
			out string Fault)
		{
			Fault = "the " + Family + " was refused by its own codec (" + Thrown.Message + ")";
			return KingdomCivicMemoryNested.Malformed;
		}

		/// <summary>
		/// The same fault set the frozen families use to decide a payload is a wire problem rather
		/// than a program problem. Anything outside it is a defect here and must not be caught.
		/// </summary>
		private static bool WireFault(Exception e)
		{
			return e is IOException || e is InvalidDataException || e is DecoderFallbackException
				|| e is EncoderFallbackException || e is ArgumentException;
		}
	}
}
#endif
