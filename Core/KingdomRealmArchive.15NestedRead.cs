#if !TAF_TESTS
using System.IO;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		/// <summary>Pairs with the historical Write(IComposite) frame, which includes a type token.
		/// Qud's generic ReadComposite&lt;T&gt; expects the token-free WriteComposite&lt;T&gt; frame.
		/// Nested engine recovery must never turn a failed read into accepted partial authority.</summary>
		private static T ReadArchiveComposite<T>(SerializationReader Reader) where T : class, IComposite
		{
			int errors = Reader.Errors;
			IComposite value = Reader.ReadComposite();
			if (Reader.Errors != errors || value == null || value.GetType() != typeof(T))
				throw new InvalidDataException("Archived nested composite type or read was rejected.");
			return (T)value;
		}
	}
}
#endif
