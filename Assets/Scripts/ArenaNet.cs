using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Net;
using UnityEngine;

namespace Temnorid.Arena
{
	/// <summary>
	/// Makes the arena a free-for-all for up to four people, the bots filling the other fighters. The host is the authority:
	/// it runs the bots, the periods, the score and the Heroes, checks every hit, and tells the others. A client plays its own
	/// fighter and shows what the host says.
	/// <list type="bullet">
	/// <item>Who plays which fighter: a person picks a class (<c>ar.pick</c>, to the host); the host gives them a free fighter of
	/// that class (or the next class with one) and announces the table (<c>ar.assign</c>, repeated every second or so, so a
	/// machine that loads late catches up). A person who joins after the match began picks the same way and takes over a bot.
	/// A person who leaves gives the fighter back to the bots.</item>
	/// <item>Each person's own fighter is simulated by its owner and published as a pose (position, yaw, state bits, health);
	/// the others show it with <see cref="RemoteBody"/>.</item>
	/// <item>The host's bots are entities (id = the fighter's place in the scene, kind = class).</item>
	/// <item>A hit by a person on another machine is a request (<c>ar.hit</c>: victim and kind of attack). The host checks the
	/// reach, the rate and the class, applies the class's own damage to its copy of the victim, and tells the victim's owner
	/// (<c>ar.dmg</c>, <c>SendTo</c>); the owner applies it to its real fighter. Bot hits on a person work the same way.</item>
	/// <item>Kills, score, period results and Heroes are the host's: <c>ar.kill</c>, <c>ar.state</c>, <c>ar.period</c>,
	/// <c>ar.next</c>. The fallen stay down until the host says (<c>ar.respawn</c> to the owner); if that message is lost the
	/// owner stands up by itself after a few seconds, so nobody is stuck.</item>
	/// </list>
	/// State bits of a pose or entity: 1 down, bits 1 to 3 the attack count (so others can show attacks), 16 Hero.
	/// </summary>
	public sealed class ArenaNet : MonoBehaviour
	{
		public const string PickEvent = "ar.pick";
		public const string AssignEvent = "ar.assign";
		public const string StateEvent = "ar.state";
		public const string PeriodEvent = "ar.period";
		public const string NextEvent = "ar.next";
		public const string HitEvent = "ar.hit";
		public const string DamageEvent = "ar.dmg";
		public const string KillEvent = "ar.kill";
		public const string RespawnEvent = "ar.respawn";

		const float PoseEvery = 1f / 15f;
		const float EntitiesEvery = 0.1f;
		const float StateEvery = 0.33f;
		const float AssignEvery = 1.5f;
		/// <summary>Slack on the reach of a hit for the delay between machines, in metres.</summary>
		const float ReachSlack = 3f;

		ArenaGame _game;
		NetSession _net;
		ulong[] _owner;
		int[] _seen;
		HeroClass? _wanted;
		float _pickAt;
		float _nextPose;
		float _nextEntities;
		float _nextState;
		float _nextAssign;
		bool _leaving;
		readonly Dictionary<int, float> _lastHit = new Dictionary<int, float>();
		readonly List<NetEntity> _entities = new List<NetEntity>();

		public bool IsHost => _net.IsHost;
		public bool IsAuthority => _net.IsAuthority;
		public ulong[] Owners => _owner;

		public ArenaNet Init(ArenaGame game)
		{
			_game = game;
			_net = NetSession.Instance;
			var count = game.Fighters.Length;
			_owner = new ulong[count];
			_seen = new int[count];
			for (var i = 0; i < count; i++)
			{
				_owner[i] = Fighter.NoOwner;
				_seen[i] = -1;
				var fighter = game.Fighters[i];
				fighter.Health.Damaged += (h, info) => OnHealthChanged(fighter, info);
				fighter.Health.Died += (h, info) => OnHealthChanged(fighter, info);
				fighter.HitRequested += OnLocalHit;
			}

			if (_net.IsHost) HoldBots(true);
			ApplyTable();

			_net.On(PickEvent, OnPick);
			_net.On(AssignEvent, OnAssign);
			_net.On(StateEvent, OnState);
			_net.On(PeriodEvent, OnPeriod);
			_net.On(NextEvent, OnNext);
			_net.On(HitEvent, OnHit);
			_net.On(DamageEvent, OnDamage);
			_net.On(KillEvent, OnKill);
			_net.On(RespawnEvent, OnRespawn);
			_net.Disconnected += OnDisconnected;
			_net.PlayerLeft += OnPlayerLeft;
			return this;
		}

		void OnDestroy()
		{
			if (_net == null) return;
			_net.Off(PickEvent, OnPick);
			_net.Off(AssignEvent, OnAssign);
			_net.Off(StateEvent, OnState);
			_net.Off(PeriodEvent, OnPeriod);
			_net.Off(NextEvent, OnNext);
			_net.Off(HitEvent, OnHit);
			_net.Off(DamageEvent, OnDamage);
			_net.Off(KillEvent, OnKill);
			_net.Off(RespawnEvent, OnRespawn);
			_net.Disconnected -= OnDisconnected;
			_net.PlayerLeft -= OnPlayerLeft;
		}

		void OnDisconnected(string reason)
		{
			// The host left or the connection dropped: nothing to fight in together any more.
			if (_leaving) return;
			_leaving = true;
			_game.QuitToMenu();
		}

		// ---- who plays which fighter ----

		/// <summary>The fighter this person plays, or -1.</summary>
		public int FighterOf(ulong player)
		{
			for (var i = 0; i < _owner.Length; i++) if (_owner[i] == player) return i;
			return -1;
		}

		/// <summary>The person chose a class. The host decides straight away (and starts the match); a client asks the host until it has a fighter.</summary>
		public void RequestClass(HeroClass fighterClass)
		{
			if (_game.Player != null) return;
			_wanted = fighterClass;
			if (_net.IsHost)
			{
				Assign(_net.LocalId, fighterClass);
				if (_game.Phase == ArenaPhase.ClassSelect)
				{
					_game.BeginMatch();
					SendState();
				}
				return;
			}
			SendPick();
		}

		void SendPick()
		{
			if (!_wanted.HasValue) return;
			_pickAt = Time.unscaledTime + 1f;
			_net.Send(PickEvent, ((int)_wanted.Value).ToString(CultureInfo.InvariantCulture), true);
		}

		/// <summary>Host: gives <paramref name="player"/> a free fighter of the class, or of the next class that has one. Returns its place, or -1.</summary>
		public int Assign(ulong player, HeroClass wanted)
		{
			var existing = FighterOf(player);
			if (existing >= 0) return existing;
			for (var k = 0; k < 3; k++)
			{
				var fighterClass = (HeroClass)(((int)wanted + k) % 3);
				for (var i = 0; i < _owner.Length; i++)
				{
					if (_game.Fighters[i].Class != fighterClass || _owner[i] != Fighter.NoOwner) continue;
					_owner[i] = player;
					ApplyTable();
					BroadcastAssign();
					return i;
				}
			}
			return -1;
		}

		void OnPick(ulong sender, string payload)
		{
			if (!_net.IsHost || _net.Find(sender) == null) return;
			if (!int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) || c < 0 || c > 2) return;
			Assign(sender, (HeroClass)c);
		}

		void BroadcastAssign()
		{
			_net.Send(AssignEvent, EncodeOwners(_owner));
			_nextAssign = Time.unscaledTime + AssignEvery;
		}

		void OnAssign(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL) return;
			var owners = DecodeOwners(payload, _owner.Length);
			if (owners == null) return;
			_owner = owners;
			ApplyTable();
		}

		/// <summary>Puts every fighter into the role the table says it has on this machine.</summary>
		void ApplyTable()
		{
			for (var i = 0; i < _owner.Length; i++)
			{
				var fighter = _game.Fighters[i];
				var owner = _owner[i];
				if (owner != Fighter.NoOwner && owner == _net.LocalId)
				{
					if (fighter.Control != FighterControl.Player) _game.AdoptLocal(fighter);
					continue;
				}

				if (owner != Fighter.NoOwner)
				{
					if (fighter.Control != FighterControl.Remote || fighter.OwnerId != owner)
					{
						fighter.ConfigureAsRemote(owner);
						_seen[i] = -1;
						if (_net.IsHost) _net.RemoveEntity(i);
					}
					var person = _net.Find(owner);
					fighter.DisplayName = person != null ? person.Name : fighter.BotName;
					continue;
				}

				fighter.DisplayName = fighter.Control == FighterControl.Player ? fighter.DisplayName : fighter.BotName;
				if (_net.IsHost)
				{
					if (fighter.Control == FighterControl.Remote) ToBot(fighter);
				}
				else if (fighter.Control != FighterControl.Remote || fighter.OwnerId != Fighter.NoOwner)
				{
					if (fighter.Control != FighterControl.Player) fighter.ConfigureAsRemote(Fighter.NoOwner);
					_seen[i] = -1;
				}
			}

			if (_wanted.HasValue && _game.Player != null)
			{
				if (_game.Player.Class != _wanted.Value)
					_game.ShowToast($"No {Classes.Get(_wanted.Value).Name} was free: you fight as the {Classes.Get(_game.Player.Class).Name}.");
				_wanted = null;
			}
		}

		void ToBot(Fighter fighter)
		{
			fighter.ConfigureAsBot();
			fighter.DisplayName = fighter.BotName;
			fighter.Held = _game.Phase != ArenaPhase.Playing;
			var spawn = _game.PickSpawn(fighter);
			fighter.GetUp(spawn.position, spawn.rotation);
		}

		void OnPlayerLeft(NetPlayer player)
		{
			if (!_net.IsHost) return;
			var index = FighterOf(player.Id);
			if (index < 0) return;
			_owner[index] = Fighter.NoOwner;
			ApplyTable();
			BroadcastAssign();
			_game.AddFeed(player.Name + " left");
		}

		/// <summary>Host: the bots wait (before the match, between periods) or fight.</summary>
		public void HoldBots(bool hold)
		{
			if (!_net.IsHost) return;
			foreach (var fighter in _game.Fighters)
				if (fighter.Control == FighterControl.Bot) fighter.Held = hold;
		}

		// ---- every machine, every frame ----

		void Update()
		{
			if (_net == null || !_net.IsOnline) return;
			if (_net.IsClient && _wanted.HasValue && _game.Player == null && Time.unscaledTime >= _pickAt) SendPick();
			ShowOthers();

			if (_game.Player != null && Time.unscaledTime >= _nextPose)
			{
				_nextPose = Time.unscaledTime + PoseEvery;
				_net.PublishPose(PoseOf(_game.Player));
			}

			if (!_net.IsHost) return;
			if (Time.unscaledTime >= _nextEntities)
			{
				_nextEntities = Time.unscaledTime + EntitiesEvery;
				PublishBots();
			}
			if (Time.unscaledTime >= _nextState)
			{
				_nextState = Time.unscaledTime + StateEvery;
				SendState();
			}
			if (Time.unscaledTime >= _nextAssign) BroadcastAssign();
		}

		int StateBits(Fighter fighter)
		{
			var hero = _game.Scoring.Fighters[fighter.Index].IsHero;
			return (fighter.IsDown ? 1 : 0) | ((fighter.AttackCount & 7) << 1) | (hero ? 16 : 0);
		}

		NetPose PoseOf(Fighter fighter)
		{
			return new NetPose
			{
				Position = fighter.transform.position,
				Yaw = fighter.transform.eulerAngles.y,
				Speed = fighter.Velocity.magnitude,
				State = StateBits(fighter),
				A = fighter.Health.Current,
				B = fighter.Index,
			};
		}

		void PublishBots()
		{
			_entities.Clear();
			for (var i = 0; i < _owner.Length; i++)
			{
				if (_owner[i] != Fighter.NoOwner) continue;
				var fighter = _game.Fighters[i];
				_entities.Add(new NetEntity
				{
					Id = i,
					Kind = (int)fighter.Class,
					Position = fighter.transform.position,
					Yaw = fighter.transform.eulerAngles.y,
					Value = fighter.Health.Current,
					State = StateBits(fighter),
				});
			}
			_net.PublishEntities(_entities);
		}

		void ShowOthers()
		{
			var follow = _net.IsClient;
			for (var i = 0; i < _owner.Length; i++)
			{
				var fighter = _game.Fighters[i];
				if (fighter.Control != FighterControl.Remote || fighter.Remote == null) continue;

				if (fighter.OwnerId != Fighter.NoOwner)
				{
					if (_net.TryGetPose(fighter.OwnerId, out var pose)) Show(fighter, pose.Position, pose.Yaw, pose.State, pose.A, follow);
				}
				else if (follow && _net.Entities.TryGetValue(i, out var entity))
				{
					Show(fighter, entity.Position, entity.Yaw, entity.State, entity.Value, true);
				}
			}
		}

		void Show(Fighter fighter, Vector3 position, float yaw, int state, float health, bool follow)
		{
			var i = fighter.Index;
			fighter.Remote.Apply(position, yaw, 0f, state, health, 0f);
			var attacks = (state >> 1) & 7;
			if (_seen[i] >= 0 && _seen[i] != attacks) fighter.ShowRemoteAttack();
			_seen[i] = attacks;
			if (!follow) return;

			// A client shows what the owner or the host says: down or up, and the health that is left.
			var down = (state & 1) != 0;
			if (down && !fighter.IsDown)
			{
				fighter.Health.ApplyDamage(new DamageInfo(fighter.Health.Current + 1f));
				if (!fighter.IsDown) fighter.GoDown();
			}
			else if (!down && fighter.IsDown)
			{
				fighter.GetUp(fighter.transform.position, fighter.transform.rotation);
			}
			if (!fighter.IsDown)
			{
				var current = fighter.Health.Current;
				if (health > 0f && health < current - 0.01f) fighter.Health.ApplyDamage(new DamageInfo(current - health));
			}
		}

		// ---- the match state ----

		void SendState()
		{
			if (!_net.IsHost) return;
			_net.Send(StateEvent, EncodeState(_game));
		}

		void OnState(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL) return;
			var parts = payload.Split('|');
			if (parts.Length != 4) return;
			if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var phase)) return;
			if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var period)) return;
			if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var time)) return;

			if (phase == (int)ArenaPhase.Playing && _game.Phase == ArenaPhase.ClassSelect) _game.HostStarted();
			else if (phase == (int)ArenaPhase.Playing && _game.Phase == ArenaPhase.PeriodEnd) _game.NextPeriod();
			if (phase != (int)ArenaPhase.Playing || _game.Phase != ArenaPhase.Playing) return;

			_game.PeriodTime = time;
			var rows = parts[3].Split(';');
			for (var i = 0; i < rows.Length && i < _game.Fighters.Length; i++)
			{
				var cells = rows[i].Split(',');
				if (cells.Length != 3) continue;
				if (!int.TryParse(cells[0], out var kills) || !int.TryParse(cells[1], out var deaths)) continue;
				var hero = cells[2] == "1";
				_game.Scoring.Mirror(period, i, kills, deaths, hero);
				_game.Fighters[i].SetHero(hero);
			}
		}

		/// <summary>Host: a period ended; everybody gets the result.</summary>
		public void AnnouncePeriod(PeriodResult result)
		{
			if (!_net.IsHost) return;
			_net.Send(PeriodEvent, EncodeResult(result));
		}

		void OnPeriod(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL) return;
			var result = DecodeResult(payload, _game.Fighters.Length);
			if (result != null) _game.ApplyPeriodResult(result);
		}

		/// <summary>The next period began here: the host tells the others.</summary>
		public void PeriodStarted()
		{
			if (!_net.IsHost) return;
			HoldBots(false);
			_net.Send(NextEvent, "");
			SendState();
		}

		void OnNext(ulong sender, string payload)
		{
			if (_net.IsClient && sender == 0UL) _game.NextPeriod();
		}

		// ---- kills and getting back up ----

		/// <summary>Host: a fighter fell. The others add it to their kill feed.</summary>
		public void AnnounceKill(int killer, int victim)
		{
			if (!_net.IsHost) return;
			_net.Send(KillEvent, killer.ToString(CultureInfo.InvariantCulture) + "|" + victim.ToString(CultureInfo.InvariantCulture));
		}

		void OnKill(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL) return;
			var parts = payload.Split('|');
			if (parts.Length != 2 || !int.TryParse(parts[0], out var killer) || !int.TryParse(parts[1], out var victim)) return;
			if (victim < 0 || victim >= _game.Fighters.Length) return;
			var fallen = _game.Fighters[victim];
			var by = killer >= 0 && killer < _game.Fighters.Length ? _game.Fighters[killer] : null;
			_game.AddFeed(by != null ? $"{by.DisplayName} defeated {fallen.DisplayName}" : $"{fallen.DisplayName} fell");
			if (fallen == _game.Player && !fallen.IsDown)
			{
				fallen.GoDown();
				fallen.RespawnAt = Time.time + _game.RespawnSeconds + 6f;
			}
		}

		/// <summary>Host: a fighter was put back. Its owner stands it up where the host says.</summary>
		public void Revived(Fighter fighter, Transform spawn)
		{
			if (!_net.IsHost || fighter.Control != FighterControl.Remote || fighter.OwnerId == Fighter.NoOwner) return;
			var p = spawn.position;
			_net.SendTo(fighter.OwnerId, RespawnEvent,
				string.Join("|", p.x.ToString("R", CultureInfo.InvariantCulture), p.y.ToString("R", CultureInfo.InvariantCulture), p.z.ToString("R", CultureInfo.InvariantCulture), spawn.eulerAngles.y.ToString("R", CultureInfo.InvariantCulture)));
		}

		void OnRespawn(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL || _game.Player == null) return;
			var v = payload.Split('|');
			if (v.Length != 4) return;
			if (!float.TryParse(v[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) return;
			if (!float.TryParse(v[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) return;
			if (!float.TryParse(v[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) return;
			if (!float.TryParse(v[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var yaw)) return;
			_game.Player.GetUp(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
		}

		// ---- hits ----

		/// <summary>Client: this person's attack touched somebody. The host decides what it does.</summary>
		void OnLocalHit(Fighter attacker, Fighter victim)
		{
			if (!_net.IsClient || attacker != _game.Player) return;
			_net.Send(HitEvent, victim.Index.ToString(CultureInfo.InvariantCulture) + "|" + ((int)attacker.Definition.Kind).ToString(CultureInfo.InvariantCulture), true);
		}

		void OnHit(ulong sender, string payload)
		{
			if (!_net.IsHost || _game.Phase != ArenaPhase.Playing) return;
			var parts = payload.Split('|');
			if (parts.Length != 2 || !int.TryParse(parts[0], out var victimIndex) || !int.TryParse(parts[1], out var kind)) return;
			var attackerIndex = FighterOf(sender);
			if (attackerIndex < 0 || victimIndex < 0 || victimIndex >= _owner.Length || victimIndex == attackerIndex) return;

			var attacker = _game.Fighters[attackerIndex];
			var victim = _game.Fighters[victimIndex];
			if (attacker.IsDown || victim.IsDown) return;
			var definition = attacker.Definition;
			if (kind != (int)definition.Kind) return;

			// Reachable from where the attacker is (with some room for the delay), and not faster than the weapon allows.
			var apart = victim.transform.position - attacker.transform.position;
			apart.y = 0f;
			if (apart.magnitude > definition.Range + ReachSlack) return;
			var key = attackerIndex * 16 + victimIndex;
			if (_lastHit.TryGetValue(key, out var last) && Time.time - last < definition.Cooldown * 0.65f) return;
			_lastHit[key] = Time.time;

			victim.Health.ApplyDamage(new DamageInfo(definition.Damage, attacker.gameObject, victim.Chest, definition.Kind.ToString()));
		}

		/// <summary>Host: a fighter that another person plays lost health. Their machine applies the same to the real fighter.</summary>
		void OnHealthChanged(Fighter fighter, DamageInfo info)
		{
			if (!_net.IsHost || fighter.Control != FighterControl.Remote || fighter.OwnerId == Fighter.NoOwner) return;
			var by = info.Source != null ? info.Source.GetComponent<Fighter>() : null;
			_net.SendTo(fighter.OwnerId, DamageEvent, info.Amount.ToString("R", CultureInfo.InvariantCulture) + "|" + (by != null ? by.Index : -1).ToString(CultureInfo.InvariantCulture));
		}

		void OnDamage(ulong sender, string payload)
		{
			if (!_net.IsClient || sender != 0UL) return;
			var player = _game.Player;
			if (player == null || player.IsDown) return;
			var parts = payload.Split('|');
			if (parts.Length != 2 || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)) return;
			int.TryParse(parts[1], out var by);
			var source = by >= 0 && by < _game.Fighters.Length ? _game.Fighters[by].gameObject : null;
			player.Health.ApplyDamage(new DamageInfo(amount, source, player.Chest));
		}

		// ---- wire formats (plain strings; no Unity needed, so they can be tested) ----

		public static string EncodeOwners(ulong[] owners)
		{
			var text = new StringBuilder();
			for (var i = 0; i < owners.Length; i++)
			{
				if (owners[i] == Fighter.NoOwner) continue;
				if (text.Length > 0) text.Append(';');
				text.Append(i.ToString(CultureInfo.InvariantCulture)).Append(':').Append(owners[i].ToString(CultureInfo.InvariantCulture));
			}
			return text.ToString();
		}

		/// <summary>The owner of each fighter from <see cref="EncodeOwners"/>, or null if the text is not valid.</summary>
		public static ulong[] DecodeOwners(string text, int count)
		{
			var owners = new ulong[count];
			for (var i = 0; i < count; i++) owners[i] = Fighter.NoOwner;
			if (string.IsNullOrEmpty(text)) return owners;
			foreach (var entry in text.Split(';'))
			{
				var pair = entry.Split(':');
				if (pair.Length != 2 || !int.TryParse(pair[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= count) return null;
				if (!ulong.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) return null;
				owners[index] = id;
			}
			return owners;
		}

		static string EncodeState(ArenaGame game)
		{
			var text = new StringBuilder();
			text.Append((int)game.Phase).Append('|').Append(game.Scoring.PeriodNumber).Append('|')
				.Append(game.PeriodTime.ToString("R", CultureInfo.InvariantCulture)).Append('|');
			for (var i = 0; i < game.Scoring.Fighters.Count; i++)
			{
				var record = game.Scoring.Fighters[i];
				if (i > 0) text.Append(';');
				text.Append(record.Kills).Append(',').Append(record.Deaths).Append(',').Append(record.IsHero ? '1' : '0');
			}
			return text.ToString();
		}

		public static string EncodeResult(PeriodResult result)
		{
			var text = new StringBuilder();
			text.Append(result.Number).Append('|').Append(string.Join(",", result.HeroIndex)).Append('|');
			for (var c = 0; c < result.Rankings.Length; c++)
			{
				if (c > 0) text.Append(';');
				text.Append(string.Join(",", result.Rankings[c] ?? new int[0]));
			}
			text.Append('|').Append(string.Join(",", result.Kills)).Append('|').Append(string.Join(",", result.Deaths));
			return text.ToString();
		}

		/// <summary>A period result from <see cref="EncodeResult"/> for <paramref name="fighters"/> fighters, or null if the text is not valid.</summary>
		public static PeriodResult DecodeResult(string text, int fighters)
		{
			var parts = text.Split('|');
			if (parts.Length != 5 || !int.TryParse(parts[0], out var number)) return null;
			var heroes = Ints(parts[1]);
			var kills = Ints(parts[3]);
			var deaths = Ints(parts[4]);
			if (heroes == null || heroes.Length != 3 || kills == null || kills.Length != fighters || deaths == null || deaths.Length != fighters) return null;
			var result = new PeriodResult { Number = number, HeroIndex = heroes, Kills = kills, Deaths = deaths };
			var classes = parts[2].Split(';');
			if (classes.Length != 3) return null;
			for (var c = 0; c < 3; c++)
			{
				var ranking = Ints(classes[c]);
				if (ranking == null) return null;
				foreach (var index in ranking) if (index < 0 || index >= fighters) return null;
				result.Rankings[c] = ranking;
			}
			foreach (var hero in heroes) if (hero >= fighters) return null;
			return result;
		}

		static int[] Ints(string text)
		{
			if (string.IsNullOrEmpty(text)) return new int[0];
			var cells = text.Split(',');
			var list = new int[cells.Length];
			for (var i = 0; i < cells.Length; i++)
				if (!int.TryParse(cells[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out list[i])) return null;
			return list;
		}
	}
}
