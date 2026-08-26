using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomZoningRules
	{
		/// <summary>
		/// The whole verdict on one design, against one piece of ground, for one settlement.
		/// Checks in <see cref="ZoningVerdict"/> order and returns at the first refusal, so the
		/// founder is told one thing to fix rather than four.
		/// </summary>
		/// <param name="Gate">The design's parsed gate. <c>ZoneGate.Open</c> always permits.</param>
		/// <param name="TileDistrict">District key on the ground being built on, or null.</param>
		/// <param name="Category">The design's <c>Category</c>, for the open-ground clause.</param>
		/// <param name="ClaimedZones">Zones the realm holds (<c>ClaimedZones.Count</c>).</param>
		/// <param name="Roster">Knowledge keys the settlement holds; null reads as none known.</param>
		public static ZoningJudgement Judge(ZoneGate Gate, string TileDistrict, string Category, int ClaimedZones, IEnumerable<string> Roster)
		{
			return Judge(Gate, TileDistrict, Category, ClaimedZones, Roster, Underground: false, RequiresSky: false);
		}

		/// <summary>
		/// The same verdict, with the stratum the ground sits in folded in &mdash; which is what
		/// narrows the catalogue by depth at the moment the founder is choosing, rather than
		/// after they have chosen (the brief's "different catalogue subset by stratum", as far as
		/// the catalogue's own attributes can express it today).
		/// <para>
		/// The subset is derived, never authored: a design that declares <c>Sky</c> wants sun,
		/// wind, or rain, and there is none of any of them under the rock. That refusal already
		/// existed at <c>KingdomPlotRules.RefuseSky</c>, one step further down, where it fires
		/// only once the founder has picked the design and the commission is already running.
		/// Reading it here as a gate means the menu itself carries the tag, and the whole
		/// catalogue still shows &mdash; a list that silently shortens teaches nothing.
		/// </para>
		/// <para>
		/// That derived half is no longer the whole of it. Addendum 15's <c>Strata</c> attribute is
		/// the authored half this comment used to say was missing: a design names the set it lives
		/// in and the strata it shares into, and <see cref="StrataAdmits"/> is where "this design
		/// belongs to the deep" is finally sayable. This overload derives the stratum from the
		/// depth; the overload below takes it.
		/// </para>
		/// </summary>
		/// <param name="Underground">Whether the ground is below <c>KingdomRules.SurfaceZLevel</c>
		/// (<c>KingdomPlotRules.IsUnderground</c>).</param>
		/// <param name="RequiresSky">The design's <c>Sky</c> flag.</param>
		public static ZoningJudgement Judge(ZoneGate Gate, string TileDistrict, string Category, int ClaimedZones,
			IEnumerable<string> Roster, bool Underground, bool RequiresSky)
		{
			return Judge(Gate, TileDistrict, Category, ClaimedZones, Roster, Underground, RequiresSky, BuilderRoll.Unknown);
		}

		/// <summary>
		/// The same verdict with the city's own people folded in, which is what Addendum 16's
		/// creed stack is judged against.
		/// <para>
		/// The three creed gates are checked BEFORE all five of the older ones, in the order the
		/// addendum states them: alignment, then amount, then the hands. Alignment leads because it
		/// is the only one of the eight that a founder cannot answer by doing anything to the
		/// ground &mdash; a city where nobody has ever held the creed is not short of a disk or a
		/// parasang, and telling them about the disk first would send them the wrong way for a
		/// season.
		/// </para>
		/// <para>
		/// Against <see cref="BuilderRoll.Unknown"/> every creed gate permits, so this overload
		/// answers exactly as the one above it for a caller who has no roll to give.
		/// </para>
		/// </summary>
		/// <param name="Roll">Who lives here. <see cref="BuilderRoll.Unknown"/> permits.</param>
		public static ZoningJudgement Judge(ZoneGate Gate, string TileDistrict, string Category, int ClaimedZones,
			IEnumerable<string> Roster, bool Underground, bool RequiresSky, BuilderRoll Roll)
		{
			return Judge(Gate, TileDistrict, Category, ClaimedZones, Roster, Underground, RequiresSky, Roll,
				StratumOfGround(Underground));
		}

		/// <summary>
		/// The same verdict with the ground's own STRATUM named rather than derived, which is what
		/// Addendum 15's separate building sets are judged against.
		/// <para>
		/// Two depth questions are asked here and they are not the same one.
		/// <see cref="StratumAccepts"/> asks whether WEATHER reaches this ground, which is a fact
		/// about the rock and answers the design's <c>Sky</c> flag. <see cref="StrataAdmits"/> asks
		/// whether this design's SET stands here, which is a fact about the catalogue and answers
		/// its <c>Strata</c> list. A carved cell wants no weather and is still refused on open
		/// ground; an air-well wants weather and is still refused under it. Both refuse as
		/// <see cref="ZoningVerdict.RefusedStratum"/> because they are the same refusal at the same
		/// scale, and each names the stratum that would take the design instead.
		/// </para>
		/// <para>
		/// <paramref name="Underground"/> and <paramref name="Stratum"/> are both taken rather than
		/// one derived from the other, because they will stop agreeing: an arcology floor is not
		/// under the rock and has no weather over it either.
		/// </para>
		/// </summary>
		/// <param name="Stratum">The ground's stratum, from <see cref="StratumOfGround"/>. Null
		/// admits every design, so ground nobody could name never refuses one.</param>
		public static ZoningJudgement Judge(ZoneGate Gate, string TileDistrict, string Category, int ClaimedZones,
			IEnumerable<string> Roster, bool Underground, bool RequiresSky, BuilderRoll Roll, string Stratum)
		{
			return Judge(Gate, TileDistrict, Category, ClaimedZones, Roster, Underground, RequiresSky, Roll, Stratum,
				Key: null, CityKeeps: null);
		}

		/// <summary>
		/// The same verdict with Addendum 22 A1's cardinality folded in: what this city already
		/// keeps, and therefore whether it may be about a second thing.
		/// <para>
		/// <b>Asked LAST, after the district gate, and the position is the ruling.</b> Every gate
		/// above it is about whether the realm CAN raise the design &mdash; a lack the founder
		/// answers by teaching, claiming, growing or walking. This one is about whether the city
		/// SHOULD, and the answer is a thing they already chose. Told last, it is the only sentence
		/// left standing, so a founder who has not yet reached arclight hears about arclight rather
		/// than about a purpose they cannot get near.
		/// </para>
		/// <para>
		/// <b>Fails OPEN by construction.</b> A null or empty <paramref name="CityKeeps"/> permits,
		/// which is exactly what a derivation that could not read the city hands back
		/// (<c>KingdomZoning.KeptMegastructure</c>). A cardinality rule that could not see the city
		/// must let the founder build; the alternative is a realm bricked by a book it could not
		/// open.
		/// </para>
		/// </summary>
		/// <param name="Key">The design's own registry key, so re-raising the megastructure a city
		/// already keeps is not a second choice. Null reads as a design that is not the kept one.</param>
		/// <param name="CityKeeps">The registry key of the megastructure this city already keeps, or
		/// null when it keeps none and when nothing could tell.</param>
		public static ZoningJudgement Judge(ZoneGate Gate, string TileDistrict, string Category, int ClaimedZones,
			IEnumerable<string> Roster, bool Underground, bool RequiresSky, BuilderRoll Roll, string Stratum,
			string Key, string CityKeeps)
		{
			return Judge(Gate, TileDistrict, Category, ClaimedZones, Roster, Underground, RequiresSky, Roll, Stratum,
				Key, CityKeeps, Crowned: false, CapitalName: null,
				Satellite: KingdomSatelliteVerdict.Allowed, SatelliteDetail: null);
		}

		/// <summary>
		/// The same verdict with the capital's two lanes folded in: whether the crown is set down
		/// in this city, and whether this design is an outpost the realm and the city will carry.
		/// <para>
		/// <b>The satellite gate is asked with the territory gates and the crown gate is asked
		/// last</b>, and the split is the same one that already orders this method. "Nowhere in the
		/// realm does that great work stand" is a lack of the same kind as "the realm holds three
		/// zones and this wants four" &mdash; a fact about the realm the founder answers by
		/// building elsewhere &mdash; so it sits with them. "Only a capital raises this" is not a
		/// lack at all; it is a decision the founder already made about a different city, which is
		/// where the purpose gate lives and why both are told after everything a founder could go
		/// and fix today.
		/// </para>
		/// <para>
		/// <b>Both new answers are computed by the CALLER</b> and passed in already read, exactly
		/// as <paramref name="CityKeeps"/> and <paramref name="Roll"/> are. Reading a realm's books
		/// needs an engine, and this class does not have one and must not grow one.
		/// </para>
		/// <para>
		/// The overload above chains here with <paramref name="Crowned"/> false, which is the
		/// fail-CLOSED direction and is deliberate: the crown is a realm fact carried in one
		/// always-readable string, so "we could not tell" is not a state it has, and a caller that
		/// does not know about capitals must not hand an uncrowned realm the capital's catalogue.
		/// It changes nothing for any existing caller, because no design declared
		/// <c>Capital</c> before this landed.
		/// </para>
		/// </summary>
		/// <param name="Crowned">Whether the realm's crown is set down in the city holding this
		/// ground (<c>KingdomCrown.CrownedOnActiveGround</c>).</param>
		/// <param name="CapitalName">The city keeping the crown, for the refusal, or null when the
		/// realm has no capital.</param>
		/// <param name="Satellite">What the outpost lane answered
		/// (<c>KingdomSatellite.JudgeActiveGround</c>).
		/// <see cref="KingdomSatelliteVerdict.Allowed"/> for every design that is not one.</param>
		/// <param name="SatelliteDetail">The parent's key or the kept outpost's key, per the
		/// verdict.</param>
		public static ZoningJudgement Judge(ZoneGate Gate, string TileDistrict, string Category, int ClaimedZones,
			IEnumerable<string> Roster, bool Underground, bool RequiresSky, BuilderRoll Roll, string Stratum,
			string Key, string CityKeeps, bool Crowned, string CapitalName,
			KingdomSatelliteVerdict Satellite, string SatelliteDetail)
		{
			if (!Aligned(Roll, Gate.Creed))
			{
				return new ZoningJudgement(ZoningVerdict.RefusedUnaligned, Gate.Creed, "no one holds with it");
			}
			if (!string.IsNullOrEmpty(Gate.Creed) && Roll.Known
				&& !CreedShareMet(Roll.HoldingNow(Gate.Creed), Roll.People, Gate.EffectiveCreedShare))
			{
				return new ZoningJudgement(ZoningVerdict.RefusedCreedShare, Gate.Creed,
					"wants " + Gate.EffectiveCreedShare + "% of the city");
			}
			List<string> hands = MissingBuilders(Roll, Gate.Builders);
			if (hands.Count > 0)
			{
				return new ZoningJudgement(ZoningVerdict.RefusedBuilders, JoinAnd(DescribeBuilders(hands)), "nobody here can");
			}
			List<string> missing = MissingKnowledge(Roster, Gate.Knowledge);
			if (missing.Count > 0)
			{
				return new ZoningJudgement(ZoningVerdict.RefusedUnlearned, JoinAnd(DescribeKeys(missing)), "not known here");
			}
			TechLevel reached = LevelForPoints(TechPoints(Roster));
			if (Gate.MinTech > TechLevel.Hands && reached < Gate.MinTech)
			{
				return new ZoningJudgement(ZoningVerdict.RefusedTechLevel, TechName(Gate.MinTech), "wants " + TechName(Gate.MinTech));
			}
			if (Gate.MinZones > 0 && ClaimedZones < Gate.MinZones)
			{
				string zones = Gate.MinZones + ((Gate.MinZones == 1) ? " claimed zone" : " claimed zones");
				return new ZoningJudgement(ZoningVerdict.RefusedTerritory, zones, "wants " + Gate.MinZones + " zones");
			}
			// Beside the territory gate, because it is the same kind of lack at a different scale:
			// the realm does not hold enough ground, or the realm does not hold the great work this
			// is an outpost of. Both are answered by going and building somewhere, and neither is
			// answered by anything on this piece of ground.
			if (Satellite == KingdomSatelliteVerdict.RefusedNoParent)
			{
				return new ZoningJudgement(ZoningVerdict.RefusedSatellite, SatelliteDetail, "nothing to be an outpost of");
			}
			if (Satellite == KingdomSatelliteVerdict.RefusedCityKeeps)
			{
				return new ZoningJudgement(ZoningVerdict.RefusedSatelliteKept, SatelliteDetail, "one to a city");
			}
			if (!StratumAccepts(Underground, RequiresSky))
			{
				return new ZoningJudgement(ZoningVerdict.RefusedStratum, StratumName(Underground: false), "wants open sky");
			}
			// Asked second because the weather refusal is the one nothing can answer, and a founder
			// told "claim ground in the deep" for a design that would want sun when they got there
			// would have been sent a parasang the wrong way.
			if (!StrataAdmits(Gate.Strata, Stratum))
			{
				return new ZoningJudgement(ZoningVerdict.RefusedStratum, DescribeStrata(Gate.Strata),
					"wants " + StratumName(HomeStratum(Gate.Strata)));
			}
			if (!DistrictAccepts(TileDistrict, Gate.Districts, Category))
			{
				string where = DescribeDistricts(Gate.Districts);
				return new ZoningJudgement(ZoningVerdict.RefusedDistrict, where, where);
			}
			KingdomPurposeVerdict purpose = KingdomLabRules.JudgePurpose(Gate.Megastructure, Gate.Capital, Crowned, CityKeeps, Key);
			if (purpose == KingdomPurposeVerdict.RefusedUncrowned)
			{
				// The Detail is a CITY and not a key, which is the one Detail in this method that
				// is already prose: a city's name is the founder's own word for it and nothing
				// downstream should be looking it up in a catalogue.
				return new ZoningJudgement(ZoningVerdict.RefusedUncrowned, CapitalName, "only the capital");
			}
			if (purpose != KingdomPurposeVerdict.Allowed)
			{
				// The Detail is the KEY rather than the display name: the refusal is composed one
				// lane over, where the catalogue can be asked what a key is called, and a rules
				// class that reached for a display name would be a rules class that knew about the
				// catalogue.
				return new ZoningJudgement(ZoningVerdict.RefusedMegastructure, CityKeeps, "the city has its purpose");
			}
			return ZoningJudgement.Allowed;
		}

	}
}
