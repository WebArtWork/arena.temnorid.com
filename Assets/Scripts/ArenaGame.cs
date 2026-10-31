using System.Collections.Generic;
using System.Text;
using ITKamianets.Engine.AI;
using ITKamianets.Engine.CharacterView;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using Unity.AI.Navigation;
using UnityEngine;

namespace Temnorid.Arena
{
	public enum ArenaPhase
	{
		/// <summary>The start screen: the player picks a class.</summary>
		ClassSelect,
		Playing,
		Paused,
		/// <summary>A period is over: rankings and the new Heroes are on screen.</summary>
		PeriodEnd,
	}

	/// <summary>
	/// Runs the arena: bakes the NavMesh, puts six fighters (two of every class) in the courtyard, counts down each
	/// period, puts the fallen back after a short wait, keeps score, and at the end of a period names the Hero of each
	/// class, who wears the title through the next one.
	/// <para>
	/// Online (<see cref="Net"/> is not null) the host runs all of that and <see cref="ArenaNet"/> tells the others; a
	/// client's game only shows what the host says and plays its own fighter.
	/// </para>
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public sealed class ArenaGame : MonoBehaviour
	{
		/// <summary>Where saves go instead of the persistent data path. Tests set it before loading the scene.</summary>
		public static string SaveRootOverride;

		/// <summary>Online: seconds the rankings stay up before the host starts the next period by itself.</summary>
		public const float AutoNextSeconds = 15f;

		public static ArenaGame Instance { get; private set; }

		[Header("Scene")]
		public Fighter[] Fighters;
		public Transform[] Spawns;
		public NavMeshSurface Surface;
		public CharacterViewCamera ViewCamera;

		[Header("Rules")]
		public float PeriodSeconds = 90f;
		public float RespawnSeconds = 3f;

		public bool SaveEnabled = true;

		const string Log = "Arena";

		ArenaSaveStore _store;
		float _periodEndAt;
		readonly List<string> _feed = new List<string>();

		public ArenaScoring Scoring { get; private set; }
		public ArenaSession Session { get; private set; }
		public ArenaPhase Phase { get; private set; } = ArenaPhase.ClassSelect;
		public Fighter Player { get; private set; }

		/// <summary>The link to the other players when this is an online game, else null.</summary>
		public ArenaNet Net { get; private set; }

		/// <summary>Another place of the shell (scoreboard, profile) is open over the arena: the game waits.</summary>
		public bool UiHold { get; private set; }

		/// <summary>Online: the pause menu is open on this machine. The arena goes on without this person.</summary>
		public bool LocalPaused { get; private set; }

		/// <summary>The pause menu should show.</summary>
		public bool PauseMenuOpen => Phase == ArenaPhase.Paused || (Net != null && LocalPaused);

		/// <summary>The person has no fighter yet and has to choose a class.</summary>
		public bool NeedsPick => Player == null && (Net == null ? Phase == ArenaPhase.ClassSelect : Phase != ArenaPhase.PeriodEnd);

		/// <summary>Seconds into the current period.</summary>
		public float PeriodTime { get; set; }

		public float TimeLeft => Mathf.Max(0f, PeriodSeconds - PeriodTime);
		public PeriodResult LastResult { get; private set; }
		public IReadOnlyList<string> Feed => _feed;

		public string ToastText { get; private set; } = "";
		public float ToastUntil { get; private set; }

		void Awake()
		{
			Instance = this;
			var online = NetSession.Instance.IsOnline;
			Time.timeScale = online ? 1f : 0f; // the start screen: nothing moves yet (online the bots are held instead)
			EngineInput.Default.PlayerEnabled = false;

			Session = new ArenaSession();
			_store = new ArenaSaveStore(SaveRootOverride);
			Session.Load(_store.Load());

			var roster = new List<KeyValuePair<string, HeroClass>>();
			foreach (var fighter in Fighters) roster.Add(new KeyValuePair<string, HeroClass>(fighter.DisplayName, fighter.Class));
			Scoring = new ArenaScoring(roster);

			// The fighters wait, switched off, until the floor exists to walk on.
			Surface.BuildNavMesh();
			for (var i = 0; i < Fighters.Length; i++)
			{
				var fighter = Fighters[i];
				fighter.Index = i;
				fighter.gameObject.SetActive(true);
				fighter.Health.Died += (health, info) => OnFighterDied(fighter, info);
				fighter.ConfigureAsBot();
				var control = fighter.GetComponent<PlayerControl>();
				if (control != null) control.Game = this;
			}
			PlaceAll();
			if (online) Net = gameObject.AddComponent<ArenaNet>().Init(this);
		}

		void OnEnable() => ShellFlow.BeforeQuit += Save;

		void OnDisable() => ShellFlow.BeforeQuit -= Save;

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		// ---- the match ----

		/// <summary>The player picks a class and the first period starts. With <paramref name="autopilot"/> they stay a bot (for tests).</summary>
		public void StartMatch(HeroClass fighterClass, bool autopilot = false)
		{
			foreach (var fighter in Fighters)
			{
				if (!autopilot && fighter.Class == fighterClass && Player == null)
				{
					Player = fighter;
					fighter.ConfigureAsPlayer(ViewCamera);
					fighter.DisplayName = "You";
				}
			}
			if (Player == null) Player = System.Array.Find(Fighters, f => f.Class == fighterClass);

			PeriodTime = 0f;
			Phase = ArenaPhase.Playing;
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = !autopilot;
			PlaceAll();
			EngineLog.Info(Log, $"Match started as {fighterClass}.");
		}

		/// <summary>The person picks a class. Offline that starts the match; online the host decides who plays which fighter.</summary>
		public void Choose(HeroClass fighterClass)
		{
			if (Net == null) StartMatch(fighterClass);
			else Net.RequestClass(fighterClass);
		}

		/// <summary>Online, host: the first period starts (everybody who has chosen is already placed).</summary>
		public void BeginMatch()
		{
			if (Phase != ArenaPhase.ClassSelect) return;
			PeriodTime = 0f;
			Phase = ArenaPhase.Playing;
			PlaceAll();
			Net?.HoldBots(false);
			ApplyState();
			EngineLog.Info(Log, "Online match started.");
		}

		/// <summary>Online, client: the host says the first period has started.</summary>
		public void HostStarted()
		{
			if (Phase != ArenaPhase.ClassSelect) return;
			PeriodTime = 0f;
			Phase = ArenaPhase.Playing;
			ApplyState();
		}

		/// <summary>Online: this person now plays <paramref name="fighter"/>.</summary>
		public void AdoptLocal(Fighter fighter)
		{
			Player = fighter;
			fighter.ConfigureAsPlayer(ViewCamera);
			fighter.SetOwner(NetSession.Instance.LocalId);
			fighter.DisplayName = "You";
			fighter.HitsAreRequests = Net != null && !Net.IsAuthority;
			var spawn = PickSpawn(fighter);
			fighter.GetUp(spawn.position, spawn.rotation);
			ApplyState();
		}

		void Update()
		{
			if (Net != null)
			{
				OnlineUpdate();
				return;
			}

			if (Phase == ArenaPhase.Playing)
			{
				PeriodTime += Time.deltaTime;

				foreach (var fighter in Fighters)
				{
					if (fighter.IsDown && Time.time >= fighter.RespawnAt) Revive(fighter);
				}

				if (PeriodTime >= PeriodSeconds) EndPeriod();
				else if (!UiHold && EngineInput.Default.UI.Pause.WasPressedThisFrame()) SetPaused(true);
			}
			else if (Phase == ArenaPhase.Paused && EngineInput.Default.UI.Pause.WasPressedThisFrame())
			{
				SetPaused(false);
			}
		}

		void OnlineUpdate()
		{
			var authority = Net.IsAuthority;
			if (Phase == ArenaPhase.Playing)
			{
				PeriodTime += Time.deltaTime;
				if (authority)
				{
					foreach (var fighter in Fighters)
					{
						if (fighter.IsDown && Time.time >= fighter.RespawnAt) Revive(fighter);
					}
					if (PeriodTime >= PeriodSeconds) EndPeriod();
				}
				else if (Player != null && Player.IsDown && Time.time >= Player.RespawnAt)
				{
					// The host has not put this fighter back (a lost message): never stay down for good.
					var spawn = PickSpawn(Player);
					Player.GetUp(spawn.position, spawn.rotation);
				}

				if (!UiHold && EngineInput.Default.UI.Pause.WasPressedThisFrame()) SetPaused(!LocalPaused);
			}
			else if (Phase == ArenaPhase.PeriodEnd && authority && Time.time - _periodEndAt >= AutoNextSeconds)
			{
				NextPeriod();
			}
		}

		public void SetPaused(bool paused)
		{
			if (Net != null)
			{
				if (paused && Phase != ArenaPhase.Playing) return;
				LocalPaused = paused;
				ApplyState();
				return;
			}
			if (paused && Phase != ArenaPhase.Playing) return;
			if (!paused && Phase != ArenaPhase.Paused) return;
			Phase = paused ? ArenaPhase.Paused : ArenaPhase.Playing;
			Time.timeScale = paused ? 0f : 1f;
			EngineInput.Default.PlayerEnabled = !paused && Player != null && Player.IsPlayer;
		}

		/// <summary>Another place of the shell is open over the arena (or closed again).</summary>
		public void SetUiHold(bool hold)
		{
			if (UiHold == hold) return;
			UiHold = hold;
			ApplyState();
		}

		/// <summary>Time and input follow the phase: nothing moves on a screen, and online time never stops for one person.</summary>
		void ApplyState()
		{
			var live = Phase == ArenaPhase.Playing && !UiHold && !LocalPaused;
			if (Net == null) Time.timeScale = live ? 1f : 0f;
			EngineInput.Default.PlayerEnabled = live && Player != null && Player.IsPlayer;
		}

		void OnFighterDied(Fighter victim, DamageInfo info)
		{
			if (Net != null && !Net.IsAuthority)
			{
				// A client's fighters only fall when the host says so (its own damage message, or what it publishes).
				if (!victim.IsDown) victim.GoDown();
				if (victim == Player) victim.RespawnAt = Time.time + RespawnSeconds + 6f;
				return;
			}
			if (Phase != ArenaPhase.Playing) return;
			var killer = info.Source != null ? info.Source.GetComponent<Fighter>() : null;
			Scoring.RecordKill(killer != null ? killer.Index : -1, victim.Index, PeriodTime);

			victim.GoDown();
			victim.RespawnAt = Time.time + RespawnSeconds;
			AddToFeed(killer != null ? $"{killer.DisplayName} defeated {victim.DisplayName}" : $"{victim.DisplayName} fell");
			Net?.AnnounceKill(killer != null ? killer.Index : -1, victim.Index);
		}

		void Revive(Fighter fighter)
		{
			var spawn = PickSpawn(fighter);
			fighter.GetUp(spawn.position, spawn.rotation);
			Net?.Revived(fighter, spawn);
		}

		/// <summary>Everyone to a starting place, on their feet at full health.</summary>
		void PlaceAll()
		{
			for (var i = 0; i < Fighters.Length; i++)
			{
				var spawn = Spawns[(i * 3) % Spawns.Length]; // spread out round the ring
				Fighters[i].GetUp(spawn.position, spawn.rotation);
			}
		}

		/// <summary>The spawn point furthest from every fighter still on their feet.</summary>
		public Transform PickSpawn(Fighter fighter)
		{
			Transform best = Spawns[0];
			var bestDistance = -1f;
			foreach (var spawn in Spawns)
			{
				var nearest = float.MaxValue;
				foreach (var other in Fighters)
				{
					if (other == fighter || other.IsDown) continue;
					nearest = Mathf.Min(nearest, Vector3.Distance(spawn.position, other.transform.position));
				}
				if (nearest > bestDistance)
				{
					bestDistance = nearest;
					best = spawn;
				}
			}
			return best;
		}

		// ---- periods ----

		void EndPeriod()
		{
			var kills = Player != null ? Scoring.Fighters[Player.Index].Kills : 0;
			LastResult = Scoring.EndPeriod();
			ConcludePeriod(kills);
			Net?.AnnouncePeriod(LastResult);
		}

		/// <summary>Online, client: the host ended the period.</summary>
		public void ApplyPeriodResult(PeriodResult result)
		{
			if (Phase == ArenaPhase.PeriodEnd && LastResult != null && LastResult.Number == result.Number) return;
			var kills = Player != null ? result.Kills[Player.Index] : 0;
			LastResult = result;
			Scoring.ApplyResult(result);
			ConcludePeriod(kills);
		}

		void ConcludePeriod(int kills)
		{
			for (var i = 0; i < Fighters.Length; i++) Fighters[i].SetHero(Scoring.Fighters[i].IsHero);

			var summary = new StringBuilder($"Period {LastResult.Number}: ");
			foreach (HeroClass fighterClass in System.Enum.GetValues(typeof(HeroClass)))
			{
				var hero = LastResult.HeroIndex[(int)fighterClass];
				summary.Append(Classes.Get(fighterClass).Name).Append(' ').Append(hero >= 0 ? Fighters[hero].DisplayName : "none");
				if (fighterClass != HeroClass.Mage) summary.Append(", ");
			}
			if (Player != null) Session.RecordPeriod(kills, Scoring.Fighters[Player.Index].IsHero, summary.ToString());
			Save();

			Phase = ArenaPhase.PeriodEnd;
			_periodEndAt = Time.time;
			LocalPaused = false;
			Net?.HoldBots(true);
			ApplyState(); // offline: stops time; either way unlocks the cursor for the buttons
			EngineLog.Info(Log, summary.ToString());
		}

		/// <summary>The next period begins: everyone back at full health, the new Heroes wearing their badges.</summary>
		public void NextPeriod()
		{
			if (Phase != ArenaPhase.PeriodEnd) return;
			PeriodTime = 0f;
			Phase = ArenaPhase.Playing;
			_feed.Clear();
			PlaceAll();
			Net?.PeriodStarted();
			ApplyState();
		}

		void AddToFeed(string line)
		{
			_feed.Add(line);
			while (_feed.Count > 5) _feed.RemoveAt(0);
		}

		/// <summary>Online: a line for the kill feed from another machine.</summary>
		public void AddFeed(string line) => AddToFeed(line);

		public void ShowToast(string text, float seconds = 4f)
		{
			ToastText = text;
			ToastUntil = Time.unscaledTime + seconds;
		}

		public void Save()
		{
			if (SaveEnabled && Session != null) _store.Save(Session.ToSave());
		}

		void OnApplicationQuit() => Save();

		/// <summary>Saves, then leaves for the landing screen (closing any online game).</summary>
		public void QuitToMenu() => ShellFlow.QuitToMenu();

		public void SaveAndQuit()
		{
			Save();
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}
	}
}
