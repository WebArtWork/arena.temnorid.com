using System.Collections.Generic;
using NUnit.Framework;

namespace Temnorid.Arena.Tests
{
	/// <summary>The strings the host and the other players send each other (no Unity or network needed).</summary>
	public class ArenaNetCodecTests
	{
		static ArenaScoring NewArena()
		{
			return new ArenaScoring(new[]
			{
				new KeyValuePair<string, HeroClass>("Gorm", HeroClass.Warrior),
				new KeyValuePair<string, HeroClass>("Brann", HeroClass.Warrior),
				new KeyValuePair<string, HeroClass>("Sylva", HeroClass.Archer),
				new KeyValuePair<string, HeroClass>("Fenn", HeroClass.Archer),
				new KeyValuePair<string, HeroClass>("Ilyra", HeroClass.Mage),
				new KeyValuePair<string, HeroClass>("Morven", HeroClass.Mage),
			});
		}

		[Test]
		public void Who_plays_which_fighter_survives_the_trip()
		{
			var owners = new ulong[6];
			for (var i = 0; i < 6; i++) owners[i] = Fighter.NoOwner;
			owners[2] = 0UL;
			owners[5] = 3UL;

			var text = ArenaNet.EncodeOwners(owners);
			var back = ArenaNet.DecodeOwners(text, 6);

			Assert.AreEqual(owners, back);
			Assert.AreEqual(Fighter.NoOwner, ArenaNet.DecodeOwners("", 6)[0]);
			Assert.IsNull(ArenaNet.DecodeOwners("9:1", 6), "a fighter that does not exist is refused");
			Assert.IsNull(ArenaNet.DecodeOwners("x", 6));
		}

		[Test]
		public void A_period_result_survives_the_trip_and_a_client_ends_up_with_the_same_Heroes()
		{
			var host = NewArena();
			host.RecordKill(0, 3, 10f);
			host.RecordKill(0, 4, 20f);
			host.RecordKill(5, 1, 30f);
			var result = host.EndPeriod();

			var decoded = ArenaNet.DecodeResult(ArenaNet.EncodeResult(result), 6);

			Assert.IsNotNull(decoded);
			Assert.AreEqual(result.Number, decoded.Number);
			Assert.AreEqual(result.HeroIndex, decoded.HeroIndex);
			Assert.AreEqual(result.Kills, decoded.Kills);
			Assert.AreEqual(result.Deaths, decoded.Deaths);
			for (var c = 0; c < 3; c++) Assert.AreEqual(result.Rankings[c], decoded.Rankings[c]);

			var client = NewArena();
			client.ApplyResult(decoded);
			Assert.AreEqual(2, client.PeriodNumber, "the next period is number two, as on the host");
			for (var i = 0; i < 6; i++) Assert.AreEqual(host.Fighters[i].IsHero, client.Fighters[i].IsHero, "fighter " + i);
			Assert.AreEqual(0, client.Fighters[0].Kills, "the counts start again");
		}

		[Test]
		public void A_broken_result_is_refused()
		{
			Assert.IsNull(ArenaNet.DecodeResult("nonsense", 6));
			Assert.IsNull(ArenaNet.DecodeResult("1|0,1,2|0,1;2,3;4,5|0,0,0|0,0,0,0,0,0", 6), "too few kills");
			Assert.IsNull(ArenaNet.DecodeResult("1|0,1,9|0,1;2,3;4,5|0,0,0,0,0,0|0,0,0,0,0,0", 6), "a Hero that does not exist");
		}

		[Test]
		public void A_client_mirrors_the_hosts_score()
		{
			var client = NewArena();
			client.Mirror(3, 2, 4, 1, true);
			Assert.AreEqual(3, client.PeriodNumber);
			Assert.AreEqual(4, client.Fighters[2].Kills);
			Assert.AreEqual(1, client.Fighters[2].Deaths);
			Assert.IsTrue(client.Fighters[2].IsHero);
		}
	}
}
