using System.Collections;
using System.IO;
using ITKamianets.Engine.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Arena.Tests
{
	public class MatchTests
	{
		string _saveFolder;

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "arena-playmode-" + System.Guid.NewGuid());
			ArenaGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			ArenaGame.SaveRootOverride = null;
			Time.timeScale = 1f;
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		static IEnumerator Load()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
		}

		[UnityTest]
		public IEnumerator The_courtyard_has_six_fighters_two_of_each_class_all_on_the_baked_floor()
		{
			yield return Load();
			var game = ArenaGame.Instance;
			game.SaveEnabled = false;

			Assert.AreEqual(ArenaPhase.ClassSelect, game.Phase, "the game starts on the class screen");
			Assert.AreEqual(6, game.Fighters.Length);
			Assert.GreaterOrEqual(game.Spawns.Length, 6);
			foreach (HeroClass fighterClass in System.Enum.GetValues(typeof(HeroClass)))
			{
				var count = 0;
				foreach (var fighter in game.Fighters) if (fighter.Class == fighterClass) count++;
				Assert.AreEqual(2, count, "two " + fighterClass + "s");
			}
			foreach (var fighter in game.Fighters)
			{
				Assert.AreEqual(1, fighter.GetComponents<Fighter>().Length, fighter.name + " has exactly one Fighter");
				Assert.IsTrue(fighter.gameObject.activeInHierarchy, fighter.name + " is awake");
				Assert.IsTrue(fighter.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh, fighter.name + " stands on the NavMesh");
			}
		}

		[UnityTest]
		public IEnumerator Choosing_a_class_makes_the_first_fighter_of_that_class_the_player()
		{
			yield return Load();
			var game = ArenaGame.Instance;
			game.SaveEnabled = false;

			game.StartMatch(HeroClass.Archer);

			Assert.AreEqual(ArenaPhase.Playing, game.Phase);
			Assert.IsNotNull(game.Player);
			Assert.AreEqual(HeroClass.Archer, game.Player.Class);
			Assert.IsTrue(game.Player.IsPlayer);
			var players = 0;
			foreach (var fighter in game.Fighters) if (fighter.IsPlayer) players++;
			Assert.AreEqual(1, players, "exactly one fighter is the player");
		}

		[UnityTest]
		[Timeout(240000)]
		public IEnumerator A_whole_period_ends_with_Heroes_who_keep_their_badge_into_the_next_period()
		{
			yield return Load();
			var game = ArenaGame.Instance;
			game.SaveEnabled = false;
			game.PeriodSeconds = 60f;

			game.StartMatch(HeroClass.Mage, autopilot: true);
			Time.timeScale = 6f;
			var deadline = Time.realtimeSinceStartup + 120f;
			while (game.Phase == ArenaPhase.Playing && Time.realtimeSinceStartup < deadline) yield return null;
			Time.timeScale = 1f;

			Assert.AreEqual(ArenaPhase.PeriodEnd, game.Phase, "the period ended by itself");
			var result = game.LastResult;
			Assert.AreEqual(1, result.Number);
			var kills = 0;
			foreach (var k in result.Kills) kills += k;
			Assert.Greater(kills, 0, "the bots fought each other");
			var heroes = 0;
			for (var i = 0; i < 3; i++) if (result.HeroIndex[i] >= 0) heroes++;
			Assert.Greater(heroes, 0, "at least one class has a Hero");
			Assert.LessOrEqual(heroes, 3);
			Assert.AreEqual(1, game.Session.PeriodsPlayed);

			game.NextPeriod();
			Assert.AreEqual(ArenaPhase.Playing, game.Phase);
			Assert.AreEqual(0f, game.PeriodTime, 0.001f);
			for (var i = 0; i < 3; i++)
			{
				if (result.HeroIndex[i] < 0) continue;
				Assert.IsTrue(game.Scoring.Fighters[result.HeroIndex[i]].IsHero, "the Hero of class " + i + " still wears the title");
			}
			foreach (var fighter in game.Fighters) Assert.IsTrue(fighter.Health.IsAlive, fighter.name + " starts the period on their feet");
		}

		[UnityTest]
		public IEnumerator A_defeated_fighter_is_put_back_after_the_wait_and_the_kill_is_counted()
		{
			yield return Load();
			var game = ArenaGame.Instance;
			game.SaveEnabled = false;
			game.RespawnSeconds = 1f;
			game.StartMatch(HeroClass.Warrior, autopilot: true);

			var killer = game.Fighters[2];
			var victim = game.Fighters[4];
			victim.Health.ApplyDamage(new DamageInfo(9999f, killer.gameObject));

			Assert.IsTrue(victim.IsDown);
			Assert.AreEqual(1, game.Scoring.Fighters[killer.Index].Kills);
			Assert.AreEqual(1, game.Scoring.Fighters[victim.Index].Deaths);

			var deadline = Time.realtimeSinceStartup + 10f;
			while (victim.IsDown && Time.realtimeSinceStartup < deadline) yield return null;

			Assert.IsFalse(victim.IsDown, "back on their feet");
			Assert.IsTrue(victim.Health.IsAlive);
			Assert.AreEqual(victim.Health.MaxHealth, victim.Health.Current, 0.01f);
		}
	}
}
