using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Temnorid.Arena.Tests
{
	public class ArenaScoringTests
	{
		// Two of every class, in the order the game has them.
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

		const int Gorm = 0, Brann = 1, Sylva = 2, Fenn = 3, Ilyra = 4, Morven = 5;

		[Test]
		public void Kills_and_deaths_are_counted_for_the_right_fighters()
		{
			var arena = NewArena();
			arena.RecordKill(Gorm, Fenn, 10f);
			arena.RecordKill(Gorm, Morven, 20f);

			Assert.AreEqual(2, arena.Fighters[Gorm].Kills);
			Assert.AreEqual(0, arena.Fighters[Gorm].Deaths);
			Assert.AreEqual(1, arena.Fighters[Fenn].Deaths);
			Assert.AreEqual(1, arena.Fighters[Morven].Deaths);
		}

		[Test]
		public void A_fall_or_a_self_kill_counts_the_death_but_no_kill()
		{
			var arena = NewArena();
			arena.RecordKill(-1, Sylva, 5f);
			arena.RecordKill(Fenn, Fenn, 6f);

			Assert.AreEqual(1, arena.Fighters[Sylva].Deaths);
			Assert.AreEqual(1, arena.Fighters[Fenn].Deaths);
			Assert.AreEqual(0, arena.Fighters[Fenn].Kills);
		}

		[Test]
		public void Each_class_is_ranked_against_its_own_class_only()
		{
			var arena = NewArena();
			for (var i = 0; i < 5; i++) arena.RecordKill(Ilyra, Gorm, i); // a great mage must not outrank a warrior
			arena.RecordKill(Brann, Fenn, 30f);

			CollectionAssert.AreEqual(new[] { Brann, Gorm }, arena.Ranking(HeroClass.Warrior));
			CollectionAssert.AreEqual(new[] { Ilyra, Morven }, arena.Ranking(HeroClass.Mage));
			CollectionAssert.AreEqual(new[] { Sylva, Fenn }, arena.Ranking(HeroClass.Archer), "no kills: by deaths, then by order");
		}

		[Test]
		public void Ties_are_broken_by_deaths_then_by_the_earlier_last_kill_then_by_order()
		{
			var arena = NewArena();
			// Gorm and Brann both have one kill. Brann died once, Gorm never: Gorm is ahead.
			arena.RecordKill(Gorm, Sylva, 40f);
			arena.RecordKill(Brann, Fenn, 30f);
			arena.RecordKill(Ilyra, Brann, 35f);
			CollectionAssert.AreEqual(new[] { Gorm, Brann }, arena.Ranking(HeroClass.Warrior));

			// Same kills, same deaths: the one who got there first wins.
			arena = NewArena();
			arena.RecordKill(Gorm, Sylva, 50f);
			arena.RecordKill(Brann, Fenn, 30f);
			CollectionAssert.AreEqual(new[] { Brann, Gorm }, arena.Ranking(HeroClass.Warrior));
		}

		[Test]
		public void Ending_a_period_names_one_Hero_per_class_and_resets_the_scores()
		{
			var arena = NewArena();
			arena.RecordKill(Gorm, Sylva, 10f);
			arena.RecordKill(Gorm, Fenn, 20f);
			arena.RecordKill(Fenn, Gorm, 25f);
			arena.RecordKill(Sylva, Ilyra, 12f);

			var result = arena.EndPeriod();

			Assert.AreEqual(1, result.Number);
			Assert.AreEqual(Gorm, result.HeroIndex[(int)HeroClass.Warrior]);
			Assert.AreEqual(Sylva, result.HeroIndex[(int)HeroClass.Archer], "Sylva and Fenn tie on kills and deaths, and Sylva got her kill first");
			Assert.AreEqual(-1, result.HeroIndex[(int)HeroClass.Mage], "nobody of the mages got a kill");
			Assert.IsTrue(arena.Fighters[Gorm].IsHero);
			Assert.IsTrue(arena.Fighters[Sylva].IsHero);
			Assert.IsFalse(arena.Fighters[Ilyra].IsHero);
			Assert.AreEqual(2, result.Kills[Gorm], "the result keeps the period's scores");
			Assert.AreEqual(0, arena.Fighters[Gorm].Kills, "the next period starts from zero");
			Assert.AreEqual(2, arena.PeriodNumber);
		}

		[Test]
		public void The_Hero_title_keeps_through_the_next_period_and_moves_to_the_new_winner()
		{
			var arena = NewArena();
			arena.RecordKill(Gorm, Sylva, 10f);
			arena.EndPeriod();
			Assert.IsTrue(arena.Fighters[Gorm].IsHero);

			// During the next period Gorm is still the Hero, even before scoring anything.
			arena.RecordKill(Brann, Fenn, 15f);
			arena.RecordKill(Brann, Sylva, 20f);
			Assert.IsTrue(arena.Fighters[Gorm].IsHero);

			arena.EndPeriod();
			Assert.IsFalse(arena.Fighters[Gorm].IsHero);
			Assert.IsTrue(arena.Fighters[Brann].IsHero);
			Assert.AreEqual(1, arena.Fighters[Gorm].HeroPeriods);
			Assert.AreEqual(2, arena.History.Count);
		}

		[Test]
		public void A_Hero_can_defend_the_title_and_the_count_grows()
		{
			var arena = NewArena();
			for (var period = 0; period < 3; period++)
			{
				arena.RecordKill(Ilyra, Morven, 10f);
				arena.EndPeriod();
			}
			Assert.AreEqual(3, arena.Fighters[Ilyra].HeroPeriods);
			Assert.IsTrue(arena.Fighters[Ilyra].IsHero);
		}

		[Test]
		public void The_records_of_the_player_survive_a_restart_and_a_damaged_file_starts_fresh()
		{
			var session = new ArenaSession();
			session.RecordPeriod(5, true, "Period 1: Warrior You, Archer Fenn, Mage none");
			session.RecordPeriod(3, false, "Period 2: Warrior Gorm, Archer Fenn, Mage Ilyra");
			Assert.AreEqual(2, session.PeriodsPlayed);
			Assert.AreEqual(5, session.BestKills);
			Assert.AreEqual(1, session.TimesHero);

			var folder = Path.Combine(Path.GetTempPath(), "arena-test-" + System.Guid.NewGuid());
			try
			{
				var store = new ArenaSaveStore(folder);
				Assert.IsNull(store.Load(), "nothing saved yet");
				store.Save(session.ToSave());

				var restored = new ArenaSession();
				restored.Load(new ArenaSaveStore(folder).Load());
				Assert.AreEqual(2, restored.PeriodsPlayed);
				Assert.AreEqual(5, restored.BestKills);
				Assert.AreEqual("Period 2: Warrior Gorm, Archer Fenn, Mage Ilyra", restored.Recent[1]);

				File.WriteAllText(Path.Combine(folder, "arena", "default.json"), "{ broken");
				Assert.IsNull(new ArenaSaveStore(folder).Load(), "damaged file: start fresh");
				Assert.AreEqual(1, Directory.GetFiles(Path.Combine(folder, "arena"), "default.corrupt-*.json").Length, "the damaged file is kept");
			}
			finally
			{
				if (Directory.Exists(folder)) Directory.Delete(folder, true);
			}
		}

		[Test]
		public void Only_the_latest_periods_are_remembered()
		{
			var session = new ArenaSession();
			for (var i = 1; i <= 12; i++) session.RecordPeriod(i, false, "Period " + i);
			Assert.AreEqual(ArenaSession.RecentLimit, session.Recent.Count);
			Assert.AreEqual("Period 12", session.Recent[session.Recent.Count - 1]);
			Assert.AreEqual(12, session.BestKills);
		}
	}
}
