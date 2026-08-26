namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		/// <summary>
		/// Standing policies: the founder sets intent once and the settlement acts on it.
		/// Every policy trades one good thing for another, so there is no correct answer.
		/// </summary>
		public enum GatePolicy
		{
			Open,
			Guarded
		}

		public enum StoresPolicy
		{
			Plenty,
			Thrift
		}

		public static readonly string[] GatePolicyNames = new string[2] { "open gates", "guarded gates" };

		public static readonly string[] GatePolicyBlurbs = new string[2]
		{
			"Word travels and strangers are welcome. Settlers come sooner; so does trouble.",
			"The watch turns away what it does not know. Fewer settlers and routine troubles; at a steading, denying passage may provoke one local snapjaw salt-road claim."
		};

		public static readonly string[] StoresPolicyNames = new string[2] { "open stores", "thrift" };

		public static readonly string[] StoresPolicyBlurbs = new string[2]
		{
			"Everyone drinks their fill. The settlement grows as fast as the water allows.",
			"The water-keepers ration. Upkeep falls by a quarter, and newcomers are made to wait."
		};

		/// <summary>Arrival interval after standing policy, in ticks.</summary>
		public static long PolicyInterval(long BaseInterval, GatePolicy Gate, StoresPolicy Stores)
		{
			long num = BaseInterval;
			if (Gate == GatePolicy.Guarded)
			{
				num = num * 140 / 100;
			}
			if (Stores == StoresPolicy.Thrift)
			{
				num = num * 130 / 100;
			}
			return num;
		}

		/// <summary>Daily upkeep after standing policy.</summary>
		public static int PolicyUpkeep(int BaseUpkeep, StoresPolicy Stores)
		{
			if (Stores != StoresPolicy.Thrift)
			{
				return BaseUpkeep;
			}
			return BaseUpkeep * 75 / 100;
		}

		/// <summary>Raid cooldown after standing policy; guarded gates buy quiet.</summary>
		public static long PolicyRaidCooldown(long BaseCooldown, GatePolicy Gate)
		{
			if (Gate != GatePolicy.Guarded)
			{
				return BaseCooldown;
			}
			return BaseCooldown * 160 / 100;
		}

		public const int TributeEscalationPercent = 50;

		/// <summary>
		/// What tribute costs now. A demand ignored once is a demand that has grown: deferring
		/// is a real choice with a real price, not a free delay.
		/// </summary>
		/// <param name="BaseDrams">The opening demand.</param>
		/// <param name="TimesDeferred">How many times this demand has been let pass.</param>
		public static int TributeDemand(int BaseDrams, int TimesDeferred)
		{
			int num = BaseDrams;
			for (int i = 0; i < TimesDeferred && i < 4; i++)
			{
				num = num * (100 + TributeEscalationPercent) / 100;
			}
			return num;
		}

		public const int DiplomacyStandingRequired = 250;

		/// <summary>
		/// Whether a standing offer of friendship can turn a raid aside without payment &mdash;
		/// the third exit. Kenshi's lesson: tribute that ignores earned goodwill feels wrong.
		/// </summary>
		public static bool CanTalkDown(int Standing, int TimesDeferred)
		{
			if (Standing >= DiplomacyStandingRequired)
			{
				return TimesDeferred == 0;
			}
			return false;
		}

		/// <summary>
		/// What a settler is asking the founder for. Every kind is generated from a condition
		/// the settlement is actually in, and every kind is met by a thing the player can see
		/// change &mdash; never a fetch quest invented from nothing.
		/// </summary>
		public enum PetitionKind
		{
			None,
			Thirst,
			Shelter,
			Craft,
			Peace,
			Memorial,

			/// <summary>
			/// The hall is spoken against (DIVERSITY &sect;3.6). Unlike the five above it, this one
			/// is never chosen by <see cref="ChoosePetition"/>: the settlement is not in a state
			/// that raises it, a founder DID something, and the lab pushes it at the moment they do.
			/// <para>
			/// Appended, never renumbered &mdash; these ordinals are carried in a save.
			/// </para>
			/// </summary>
			Flesh,

			/// <summary>
			/// The rolls are spoken about (END-STATE §2.4 &mdash; the Mechanimist chrome-debt).
			/// The annexe's twin of <see cref="Flesh"/>: never chosen by state, pushed by the
			/// first enrolment while a debt-minded minority lives in the city. Appended, never
			/// renumbered.
			/// </summary>
			Chrome
		}

		public const long PetitionCooldownTicks = 3600L;

		public const long PetitionLifetimeTicks = 24000L;

		/// <summary>
		/// Chooses the petition the settlement would actually raise, in order of how badly it
		/// wants it. Returns None when the settlement is content &mdash; silence is a valid
		/// answer, and the reason there is no petition board.
		/// </summary>
		/// <param name="StoredWater">Drams in the dedicated stores.</param>
		/// <param name="Population">Living settlers.</param>
		/// <param name="Beds">Beds built.</param>
		/// <param name="IdleWorks">Works standing unmanned.</param>
		/// <param name="WorstStanding">Lowest standing with any faction that knows the kingdom.</param>
		/// <param name="HasShrine">Whether a place of remembrance exists.</param>
		/// <param name="Dead">Settlers lost since the settlement was founded.</param>
		public static PetitionKind ChoosePetition(int StoredWater, int Population, int Beds, int IdleWorks, int WorstStanding, bool HasShrine, int Dead)
		{
			if (Population <= 0)
			{
				return PetitionKind.None;
			}
			if (StoredWater < UpkeepDrams(Population) * 3)
			{
				return PetitionKind.Thirst;
			}
			if (Beds <= Population)
			{
				return PetitionKind.Shelter;
			}
			if (Dead > 0 && !HasShrine)
			{
				return PetitionKind.Memorial;
			}
			if (WorstStanding <= -250)
			{
				return PetitionKind.Peace;
			}
			if (IdleWorks > 0)
			{
				return PetitionKind.Craft;
			}
			return PetitionKind.None;
		}

	}
}
