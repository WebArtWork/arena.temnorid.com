using System.Collections.Generic;
using System.Text;
using ITKamianets.Engine.AI;
using ITKamianets.Engine.CharacterView;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Input;
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
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public sealed class ArenaGame : MonoBehaviour
	{
		/// <summary>Where saves go instead of the persistent data path. Tests set it before loading the scene.</summary>
		public static string SaveRootOverride;

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
		readonly List<string> _feed = new List<string>();

		public ArenaScoring Scoring { get; private set; }
		public ArenaSession Session { get; private set; }
		public ArenaPhase Phase { get; private set; } = ArenaPhase.ClassSelect;
		public Fighter Player { get; private set; }

		/// <summary>Seconds into the current period.</summary>
		public float PeriodTime { get; private set; }

		public float TimeLeft => Mathf.Max(0f, PeriodSeconds - PeriodTime);
		public PeriodResult LastResult { get; private set; }
		public IReadOnlyList<string> Feed => _feed;

		void Awake()
		{
			Instance = this;
			Time.timeScale = 0f; // the start screen: nothing moves yet
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
		}

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

		void Update()
		{
			if (Phase == ArenaPhase.Playing)
			{
				PeriodTime += Time.deltaTime;

				foreach (var fighter in Fighters)
				{
					if (fighter.IsDown && Time.time >= fighter.RespawnAt) Revive(fighter);
				}

				if (PeriodTime >= PeriodSeconds) EndPeriod();
				else if (EngineInput.Default.UI.Pause.WasPressedThisFrame()) SetPaused(true);
			}
			else if (Phase == ArenaPhase.Paused && EngineInput.Default.UI.Pause.WasPressedThisFrame())
			{
				SetPaused(false);
			}
		}

		public void SetPaused(bool paused)
		{
			if (paused && Phase != ArenaPhase.Playing) return;
			if (!paused && Phase != ArenaPhase.Paused) return;
			Phase = paused ? ArenaPhase.Paused : ArenaPhase.Playing;
			Time.timeScale = paused ? 0f : 1f;
			EngineInput.Default.PlayerEnabled = !paused && Player != null && Player.IsPlayer;
		}

		void OnFighterDied(Fighter victim, DamageInfo info)
		{
			if (Phase != ArenaPhase.Playing) return;
			var killer = info.Source != null ? info.Source.GetComponent<Fighter>() : null;
			Scoring.RecordKill(killer != null ? killer.Index : -1, victim.Index, PeriodTime);

			victim.GoDown();
			victim.RespawnAt = Time.time + RespawnSeconds;
			AddToFeed(killer != null ? $"{killer.DisplayName} defeated {victim.DisplayName}" : $"{victim.DisplayName} fell");
		}

		void Revive(Fighter fighter)
		{
			var spawn = PickSpawn(fighter);
			fighter.GetUp(spawn.position, spawn.rotation);
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
		Transform PickSpawn(Fighter fighter)
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
			var player = Scoring.Fighters[Player.Index];
			var kills = player.Kills;

			LastResult = Scoring.EndPeriod();
			for (var i = 0; i < Fighters.Length; i++) Fighters[i].SetHero(Scoring.Fighters[i].IsHero);

			var summary = new StringBuilder($"Period {LastResult.Number}: ");
			foreach (HeroClass fighterClass in System.Enum.GetValues(typeof(HeroClass)))
			{
				var hero = LastResult.HeroIndex[(int)fighterClass];
				summary.Append(Classes.Get(fighterClass).Name).Append(' ').Append(hero >= 0 ? Fighters[hero].DisplayName : "none");
				if (fighterClass != HeroClass.Mage) summary.Append(", ");
			}
			Session.RecordPeriod(kills, Scoring.Fighters[Player.Index].IsHero, summary.ToString());
			Save();

			Phase = ArenaPhase.PeriodEnd;
			Time.timeScale = 0f;
			EngineInput.Default.PlayerEnabled = false; // unlocks the cursor for the buttons
			EngineLog.Info(Log, summary.ToString());
		}

		/// <summary>The next period begins: everyone back at full health, the new Heroes wearing their badges.</summary>
		public void NextPeriod()
		{
			if (Phase != ArenaPhase.PeriodEnd) return;
			PeriodTime = 0f;
			Phase = ArenaPhase.Playing;
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = Player != null && Player.IsPlayer;
			_feed.Clear();
			PlaceAll();
		}

		void AddToFeed(string line)
		{
			_feed.Add(line);
			while (_feed.Count > 5) _feed.RemoveAt(0);
		}

		public void Save()
		{
			if (SaveEnabled && Session != null) _store.Save(Session.ToSave());
		}

		void OnApplicationQuit() => Save();

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
