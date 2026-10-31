using System;
using System.Collections;
using System.IO;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Arena.Tests
{
	/// <summary>
	/// The real arena with this machine as the host and a second player (a detached session in the same process) as the guest:
	/// the guest picks a class and gets a fighter, its pose is shown on the host, the host's bots reach it as entities, its
	/// hits are checked by the host, the host's damage, kills, respawns and period results reach it.
	/// </summary>
	public class OnlineArenaTests
	{
		NetSession _guest;
		string _saveFolder;

		static IEnumerator Until(Func<bool> condition, string what, float seconds = 10f)
		{
			var end = Time.realtimeSinceStartup + seconds;
			while (!condition())
			{
				if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
				yield return null;
			}
		}

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "arena-online-" + Guid.NewGuid());
			ArenaGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			ArenaGame.SaveRootOverride = null;
			ArenaHud.ForceTouch = false;
			Time.timeScale = 1f;
			if (_guest != null) { _guest.Leave(); UnityEngine.Object.Destroy(_guest.gameObject); }
			NetSession.Instance.Leave();
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		[UnityTest]
		[Timeout(120000)]
		public IEnumerator A_guest_picks_a_class_fights_on_the_hosts_arena_and_gets_the_hosts_verdicts()
		{
			var host = NetSession.Instance;
			host.Leave();
			host.GameId = "arena";
			host.GameVersion = Application.version;
			host.LocalName = "Hosty";
			host.MaxPlayers = 4;
			Assert.IsTrue(host.HostDirect(7806), host.LastError);

			_guest = NetSession.CreateDetached();
			_guest.GameId = "arena";
			_guest.GameVersion = Application.version;
			_guest.LocalName = "Guesty";
			_guest.LocalColour = 3;
			Assert.IsTrue(_guest.JoinDirect("127.0.0.1:7806"), _guest.LastError);
			yield return Until(() => host.Players.Count == 2 && _guest.Players.Count == 2 && !_guest.Connecting, "the guest to join");

			string assigned = null, damage = null, killed = null, respawn = null, period = null;
			_guest.On(ArenaNet.AssignEvent, (who, text) => assigned = text);
			_guest.On(ArenaNet.DamageEvent, (who, text) => damage = text);
			_guest.On(ArenaNet.KillEvent, (who, text) => killed = text);
			_guest.On(ArenaNet.RespawnEvent, (who, text) => respawn = text);
			_guest.On(ArenaNet.PeriodEvent, (who, text) => period = text);

			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;

			var game = ArenaGame.Instance;
			game.SaveEnabled = false;
			game.RespawnSeconds = 0.5f;
			Assert.IsNotNull(game.Net, "an online arena has the link to other players");
			Assert.IsTrue(game.Net.IsHost);
			Assert.IsNotNull(ShellChrome.Current, "the shell's bars frame the arena");
			Assert.AreEqual(ArenaPhase.ClassSelect, game.Phase);
			foreach (var fighter in game.Fighters) Assert.AreEqual(FighterControl.Bot, fighter.Control, "the host runs every fighter nobody plays");

			// The host picks the Mage: the match starts, the host plays the first Mage.
			game.Choose(HeroClass.Mage);
			Assert.AreEqual(ArenaPhase.Playing, game.Phase);
			Assert.AreSame(game.Fighters[4], game.Player);
			Assert.IsTrue(game.Player.IsPlayer);

			// The guest joins in progress by picking the Archer; the host gives it the first Archer.
			_guest.Send(ArenaNet.PickEvent, "1", true);
			yield return Until(() => game.Fighters[2].Control == FighterControl.Remote, "the guest to get a fighter");
			Assert.AreEqual(_guest.LocalId, game.Fighters[2].OwnerId);
			Assert.AreEqual("Guesty", game.Fighters[2].DisplayName);
			Assert.AreEqual(HeroClass.Archer, game.Fighters[2].Class);
			yield return Until(() => assigned != null, "the table to reach the guest");
			StringAssert.Contains("2:" + _guest.LocalId, assigned);
			foreach (var fighter in game.Fighters)
				if (fighter.Control == FighterControl.Bot) fighter.Held = true; // the bots stand still so only the test hurts anybody

			// The guest says where it is: the host shows its fighter there.
			var far = new Vector3(-15f, 0f, -15f);
			for (var i = 0; i < 8; i++)
			{
				_guest.PublishPose(new NetPose { Position = far, Yaw = 40f, Speed = 5f, State = 0, A = 170f, B = 2f });
				yield return new WaitForSeconds(0.1f);
			}
			yield return Until(() => (game.Fighters[2].transform.position - far).magnitude < 0.5f, "the guest's fighter to be shown");

			// The host's bots reach the guest as entities, in the places the guest's own scene has them. The guest's own fighter is not one.
			yield return Until(() => _guest.Entities.Count >= 4, "the bots' states");
			Assert.IsTrue(_guest.Entities.ContainsKey(0) && _guest.Entities.ContainsKey(1) && _guest.Entities.ContainsKey(3) && _guest.Entities.ContainsKey(5));
			Assert.AreEqual((int)HeroClass.Warrior, _guest.Entities[0].Kind);
			Assert.AreEqual(Classes.Warrior.Health, _guest.Entities[0].Value, 0.01f);

			// A hit that cannot reach is refused (the host's Mage is on the far side of the courtyard).
			game.Player.GetUp(new Vector3(15f, 0f, 15f), Quaternion.identity);
			var mageHealth = game.Player.Health.MaxHealth;
			_guest.Send(ArenaNet.HitEvent, "4|1", true);
			yield return new WaitForSeconds(0.6f);
			Assert.AreEqual(mageHealth, game.Player.Health.Current, 0.01f, "too far: refused");

			// Close enough: the host applies its own number for an arrow.
			var near = new Vector3(12f, 0f, 12f);
			for (var i = 0; i < 8; i++)
			{
				_guest.PublishPose(new NetPose { Position = near, Yaw = 40f, Speed = 0f, State = 0, A = 170f, B = 2f });
				yield return new WaitForSeconds(0.1f);
			}
			yield return Until(() => (game.Fighters[2].transform.position - near).magnitude < 0.5f, "the guest's fighter to come close");
			_guest.Send(ArenaNet.HitEvent, "4|1", true);
			yield return Until(() => game.Player.Health.Current < mageHealth, "the host to apply the guest's hit");
			Assert.AreEqual(mageHealth - Classes.Archer.Damage, game.Player.Health.Current, 0.01f, "the damage is the host's own number for an arrow");

			// A hit with the wrong kind of attack for that class is refused.
			var afterArrow = game.Player.Health.Current;
			yield return new WaitForSeconds(1f);
			_guest.Send(ArenaNet.HitEvent, "4|0", true);
			yield return new WaitForSeconds(0.6f);
			Assert.AreEqual(afterArrow, game.Player.Health.Current, 0.01f, "an Archer does not swing a sword");

			// A hit on the guest's fighter goes to the guest, who applies it to its own fighter.
			game.Fighters[2].Health.ApplyDamage(new DamageInfo(30f, game.Player.gameObject));
			yield return Until(() => damage != null, "the guest to be told it was hit");
			StringAssert.StartsWith("30", damage);

			// A kill: the score is the host's, the guest hears of it and is put back where the host says.
			game.Fighters[2].Health.ApplyDamage(new DamageInfo(9999f, game.Player.gameObject));
			Assert.IsTrue(game.Fighters[2].IsDown);
			Assert.AreEqual(1, game.Scoring.Fighters[4].Kills);
			Assert.AreEqual(1, game.Scoring.Fighters[2].Deaths);
			yield return Until(() => killed == "4|2", "the guest to hear who was defeated");
			yield return Until(() => respawn != null, "the guest to be told to stand up");
			Assert.AreEqual(4, respawn.Split('|').Length);
			Assert.IsFalse(game.Fighters[2].IsDown, "back up on the host's side too");

			// The period ends by itself: everybody gets the result, with the host's Mage as the Hero.
			game.PeriodSeconds = game.PeriodTime + 1f;
			yield return Until(() => game.Phase == ArenaPhase.PeriodEnd, "the period to end");
			yield return Until(() => period != null, "the guest to get the period result");
			var result = ArenaNet.DecodeResult(period, 6);
			Assert.IsNotNull(result);
			Assert.AreEqual(1, result.Number);
			Assert.AreEqual(4, result.HeroIndex[(int)HeroClass.Mage], "one kill makes the host's Mage its class's Hero");
			Assert.AreEqual(-1, result.HeroIndex[(int)HeroClass.Archer]);

			// The guest leaves: its fighter goes back to the bots and nobody is stuck.
			_guest.Leave();
			yield return Until(() => game.Fighters[2].Control == FighterControl.Bot, "the fighter to go back to the bots");
			Assert.AreEqual("Sylva", game.Fighters[2].DisplayName);
			Assert.IsFalse(game.Fighters[2].IsDown);
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator Offline_the_arena_has_no_link_and_bars_frame_it()
		{
			NetSession.Instance.Leave();
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
			var game = ArenaGame.Instance;
			game.SaveEnabled = false;

			Assert.IsNull(game.Net);
			Assert.AreEqual(0f, Time.timeScale, "the start screen holds time as before");
			Assert.IsNotNull(ShellChrome.Current);
			game.StartMatch(HeroClass.Warrior);
			Assert.AreEqual(1f, Time.timeScale);

			// Another place holds the game and gives it back.
			game.SetUiHold(true);
			Assert.AreEqual(0f, Time.timeScale);
			game.SetUiHold(false);
			Assert.AreEqual(1f, Time.timeScale);
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator A_phone_gets_a_stick_a_look_area_and_buttons()
		{
			ArenaHud.ForceTouch = true;
			NetSession.Instance.Leave();
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
			ArenaGame.Instance.SaveEnabled = false;
			ArenaGame.Instance.StartMatch(HeroClass.Warrior); // the touch controls show while fighting
			yield return null;
			yield return null;

			var hud = UnityEngine.Object.FindAnyObjectByType<ArenaHud>();
			Assert.IsTrue(hud.HasTouchControls);
			Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<OnScreenStick>().Length, "one move stick");
			Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<TouchLookArea>().Length, "one look area");
			Assert.AreEqual(3, UnityEngine.Object.FindObjectsByType<OnScreenButton>().Length, "attack, jump, run");

			var found = false;
			foreach (var device in InputSystem.devices) if (device is EngineTouchControls) found = true;
			Assert.IsTrue(found, "the touch controls feed the engine's virtual touch device");
		}
	}
}
