#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomCityBook));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomCityBook));
			Normalize();
		}
#endif

		/// <summary>How many zone rows the book holds after normalization.</summary>
		public int ZoneCount => ZoneIds.Count;

		public int WorkCount => WorkIds.Count;

		public int ResidentCount => ResidentIds.Count;

		public int ToldCount => ToldKinds.Count;
	}
}
