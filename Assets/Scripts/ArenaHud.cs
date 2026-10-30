using System.Text;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Temnorid.Arena
{
	/// <summary>
	/// The screens: choosing a class, the fight (health, time, kills, kill feed), the scoreboard held with Tab, the end of
	/// a period with the rankings and the new Heroes, and the pause menu. Plain uGUI built in code; it only shows what
	/// <see cref="ArenaGame"/> says.
	/// </summary>
	public sealed class ArenaHud : MonoBehaviour
	{
		public ArenaGame Game;

		static readonly Color Panel = new Color(0.06f, 0.05f, 0.09f, 0.78f);
		static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

		UiBuilder _ui;
		InputAction _scoreboard;
		GameObject _select, _hud, _board, _periodEnd, _pause, _crosshair;
		Text _record, _classLabel, _timer, _score, _heroBanner, _respawn, _feed;
		Text _boardW, _boardA, _boardM, _endTitle, _endW, _endA, _endM, _endNote;
		Image _healthFill;
		ArenaPhase _shown = (ArenaPhase)(-1);
		readonly StringBuilder _text = new StringBuilder();

		void Start()
		{
			_ui = new UiBuilder();
			UiBuilder.EnsureEventSystem(transform);

			// The scoreboard key is an action of this game, added to the shared input so it can be rebound like the rest.
			var map = EngineInput.Default.Asset.FindActionMap("Arena");
			if (map == null)
			{
				map = EngineInput.Default.AddMap("Arena", m => m.AddAction("Scoreboard", InputActionType.Button, "<Keyboard>/tab", groups: EngineInputActions.Groups.KeyboardMouse));
			}
			_scoreboard = map.FindAction("Scoreboard");

			var root = UiBuilder.CreateCanvas(transform).transform;
			BuildSelect(root);
			BuildHud(root);
			BuildBoard(root);
			BuildPeriodEnd(root);
			BuildPause(root);
		}

		// ---- building the screens ----

		void BuildSelect(Transform root)
		{
			_select = UiBuilder.Box("ClassSelect", root, new Vector2(0.14f, 0.1f), new Vector2(0.86f, 0.92f), Panel);
			_ui.Label("Title", _select.transform, "TEMNORID ARENA", 72, TextAnchor.MiddleCenter, new Vector2(0f, 0.82f), new Vector2(1f, 0.98f), Gold);
			_ui.Label("Subtitle", _select.transform, "Choose your class. Fight everyone. The best of each class becomes its Hero.", 30, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.74f), new Vector2(0.97f, 0.82f), Color.white, wrap: true);

			var columns = new[] { (0.04f, 0.32f), (0.36f, 0.64f), (0.68f, 0.96f) };
			var classes = new[] { Classes.Warrior, Classes.Archer, Classes.Mage };
			for (var i = 0; i < 3; i++)
			{
				var definition = classes[i];
				var from = columns[i].Item1;
				var to = columns[i].Item2;
				var chosen = definition.Class;
				_ui.Button(definition.Name, _select.transform, new Vector2(from, 0.5f), new Vector2(to, 0.66f), () => Game.StartMatch(chosen));
				_ui.Label("Info", _select.transform, $"{definition.Description}\n\nHealth {definition.Health:0}   Damage {definition.Damage:0}", 26, TextAnchor.UpperCenter, new Vector2(from, 0.26f), new Vector2(to, 0.48f), Color.white, wrap: true);
			}
			_record = _ui.Label("Record", _select.transform, "", 28, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.22f), new Color(1f, 1f, 1f, 0.85f));
			_ui.Label("Hint", _select.transform, "WASD move  ·  Shift run  ·  Space jump  ·  left mouse attack  ·  Tab scoreboard  ·  Esc pause", 24, TextAnchor.MiddleCenter, new Vector2(0f, 0.02f), new Vector2(1f, 0.1f), new Color(1f, 1f, 1f, 0.65f));
		}

		void BuildHud(Transform root)
		{
			_hud = new GameObject("Hud", typeof(RectTransform));
			_hud.transform.SetParent(root, false);
			var rect = (RectTransform)_hud.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			var left = UiBuilder.Box("HealthPanel", _hud.transform, new Vector2(0.015f, 0.88f), new Vector2(0.25f, 0.985f), Panel);
			_classLabel = _ui.Label("Class", left.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.96f), Color.white);
			_healthFill = UiBuilder.Bar("Health", left.transform, new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.42f), new Color(0.85f, 0.15f, 0.12f));

			_timer = _ui.Label("Timer", _hud.transform, "", 40, TextAnchor.UpperCenter, new Vector2(0.3f, 0.92f), new Vector2(0.7f, 0.99f), Color.white);

			var right = UiBuilder.Box("ScorePanel", _hud.transform, new Vector2(0.78f, 0.88f), new Vector2(0.985f, 0.985f), Panel);
			_score = _ui.Label("Score", right.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.06f, 0.1f), new Vector2(0.95f, 0.92f), Color.white);
			_heroBanner = _ui.Label("HeroBanner", _hud.transform, "", 30, TextAnchor.UpperRight, new Vector2(0.55f, 0.82f), new Vector2(0.985f, 0.88f), Gold);

			_feed = _ui.Label("Feed", _hud.transform, "", 24, TextAnchor.LowerLeft, new Vector2(0.015f, 0.08f), new Vector2(0.4f, 0.28f), new Color(1f, 1f, 1f, 0.85f));
			_respawn = _ui.Label("Respawn", _hud.transform, "", 56, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.45f), new Vector2(0.75f, 0.58f), new Color(1f, 0.5f, 0.45f));
			_crosshair = UiBuilder.Box("Crosshair", _hud.transform, new Vector2(0.4985f, 0.4975f), new Vector2(0.5015f, 0.5025f), new Color(1f, 1f, 1f, 0.9f));
		}

		void BuildBoard(Transform root)
		{
			_board = UiBuilder.Box("Scoreboard", root, new Vector2(0.15f, 0.25f), new Vector2(0.85f, 0.8f), new Color(0.06f, 0.05f, 0.09f, 0.88f));
			_ui.Label("Title", _board.transform, "SCOREBOARD   (kills / deaths)", 36, TextAnchor.MiddleCenter, new Vector2(0f, 0.86f), new Vector2(1f, 1f), Gold);
			_boardW = _ui.Label("Warriors", _board.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.03f, 0.05f), new Vector2(0.34f, 0.84f), Color.white);
			_boardA = _ui.Label("Archers", _board.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.36f, 0.05f), new Vector2(0.67f, 0.84f), Color.white);
			_boardM = _ui.Label("Mages", _board.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.69f, 0.05f), new Vector2(0.99f, 0.84f), Color.white);
		}

		void BuildPeriodEnd(Transform root)
		{
			_periodEnd = UiBuilder.Box("PeriodEnd", root, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.9f), new Color(0.06f, 0.05f, 0.09f, 0.94f));
			_endTitle = _ui.Label("Title", _periodEnd.transform, "", 60, TextAnchor.MiddleCenter, new Vector2(0f, 0.84f), new Vector2(1f, 0.98f), Gold);
			_endNote = _ui.Label("Note", _periodEnd.transform, "", 30, TextAnchor.MiddleCenter, new Vector2(0f, 0.76f), new Vector2(1f, 0.84f), Color.white);
			_endW = _ui.Label("Warriors", _periodEnd.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.04f, 0.3f), new Vector2(0.34f, 0.75f), Color.white);
			_endA = _ui.Label("Archers", _periodEnd.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.36f, 0.3f), new Vector2(0.67f, 0.75f), Color.white);
			_endM = _ui.Label("Mages", _periodEnd.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.69f, 0.3f), new Vector2(0.99f, 0.75f), Color.white);
			_ui.Button("Next period", _periodEnd.transform, new Vector2(0.2f, 0.15f), new Vector2(0.8f, 0.27f), () => Game.NextPeriod());
			_ui.Button("Quit", _periodEnd.transform, new Vector2(0.2f, 0.03f), new Vector2(0.8f, 0.13f), () => Game.SaveAndQuit());
		}

		void BuildPause(Transform root)
		{
			_pause = UiBuilder.Box("PauseMenu", root, new Vector2(0.35f, 0.32f), new Vector2(0.65f, 0.68f), new Color(0.06f, 0.05f, 0.09f, 0.92f));
			_ui.Label("Title", _pause.transform, "PAUSED", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.68f), new Vector2(1f, 0.96f), Gold);
			_ui.Button("Resume", _pause.transform, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.62f), () => Game.SetPaused(false));
			_ui.Button("Save and quit", _pause.transform, new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.34f), () => Game.SaveAndQuit());
		}

		// ---- showing the game ----

		void Update()
		{
			if (Game == null || _select == null) return;
			var phase = Game.Phase;

			if (phase != _shown)
			{
				_shown = phase;
				_select.SetActive(phase == ArenaPhase.ClassSelect);
				_hud.SetActive(phase == ArenaPhase.Playing || phase == ArenaPhase.Paused);
				_pause.SetActive(phase == ArenaPhase.Paused);
				_periodEnd.SetActive(phase == ArenaPhase.PeriodEnd);
				if (phase == ArenaPhase.ClassSelect) UiBuilder.SetText(_record, RecordText());
				if (phase == ArenaPhase.PeriodEnd) FillPeriodEnd();
				UiBuilder.Select(phase == ArenaPhase.ClassSelect ? _select : phase == ArenaPhase.Paused ? _pause : phase == ArenaPhase.PeriodEnd ? _periodEnd : null);
			}

			var player = Game.Player;
			var playing = phase == ArenaPhase.Playing && player != null;
			_board.SetActive(playing && _scoreboard != null && _scoreboard.IsPressed());
			if (_board.activeSelf) FillBoard();
			if (player == null || !_hud.activeSelf) return;

			var record = Game.Scoring.Fighters[player.Index];
			UiBuilder.SetText(_classLabel, $"{player.Definition.Name.ToUpperInvariant()}   {Mathf.CeilToInt(player.Health.Current)} / {Mathf.CeilToInt(player.Health.MaxHealth)}");
			UiBuilder.SetFill(_healthFill, player.Health.Fraction);
			UiBuilder.SetText(_timer, $"Period {Game.Scoring.PeriodNumber}    {UiBuilder.FormatTime(Game.TimeLeft)}");
			UiBuilder.SetText(_score, $"Kills  {record.Kills}\nDeaths  {record.Deaths}");
			UiBuilder.SetText(_heroBanner, record.IsHero ? $"You are the Hero of the {player.Definition.Name}s" : "");
			UiBuilder.SetText(_respawn, player.IsDown ? $"Back in {Mathf.Max(0f, player.RespawnAt - Time.time):0.0}" : "");
			_crosshair.SetActive(!player.IsDown && player.Definition.Kind != AttackKind.Melee);

			_text.Clear();
			foreach (var line in Game.Feed) _text.Append(line).Append('\n');
			UiBuilder.SetText(_feed, _text.ToString());
		}

		string RecordText()
		{
			var session = Game.Session;
			if (session.PeriodsPlayed == 0) return "No periods played yet.";
			return $"Periods played  {session.PeriodsPlayed}     Best period  {session.BestKills} kills     Times Hero  {session.TimesHero}\n{session.Recent[session.Recent.Count - 1]}";
		}

		void FillBoard()
		{
			UiBuilder.SetText(_boardW, ClassColumn(HeroClass.Warrior, null));
			UiBuilder.SetText(_boardA, ClassColumn(HeroClass.Archer, null));
			UiBuilder.SetText(_boardM, ClassColumn(HeroClass.Mage, null));
		}

		void FillPeriodEnd()
		{
			var result = Game.LastResult;
			UiBuilder.SetText(_endTitle, $"PERIOD {result.Number} COMPLETE");
			var heroes = new StringBuilder("Heroes:  ");
			foreach (HeroClass fighterClass in System.Enum.GetValues(typeof(HeroClass)))
			{
				var hero = result.HeroIndex[(int)fighterClass];
				heroes.Append(Classes.Get(fighterClass).Name).Append(' ').Append(hero >= 0 ? Game.Fighters[hero].DisplayName : "none");
				if (fighterClass != HeroClass.Mage) heroes.Append("   ·   ");
			}
			UiBuilder.SetText(_endNote, heroes.ToString());
			UiBuilder.SetText(_endW, ClassColumn(HeroClass.Warrior, result));
			UiBuilder.SetText(_endA, ClassColumn(HeroClass.Archer, result));
			UiBuilder.SetText(_endM, ClassColumn(HeroClass.Mage, result));
		}

		/// <summary>One class's fighters, best first, with kills and deaths; the Hero is marked.</summary>
		string ClassColumn(HeroClass fighterClass, PeriodResult result)
		{
			_text.Clear();
			_text.Append(Classes.Get(fighterClass).Name.ToUpperInvariant()).Append("S\n");
			var ranking = result != null ? result.Rankings[(int)fighterClass] : Game.Scoring.Ranking(fighterClass);
			var place = 1;
			foreach (var index in ranking)
			{
				var kills = result != null ? result.Kills[index] : Game.Scoring.Fighters[index].Kills;
				var deaths = result != null ? result.Deaths[index] : Game.Scoring.Fighters[index].Deaths;
				var hero = result != null ? result.HeroIndex[(int)fighterClass] == index : Game.Scoring.Fighters[index].IsHero;
				_text.Append(place++).Append(".  ").Append(Game.Fighters[index].DisplayName).Append("   ").Append(kills).Append(" / ").Append(deaths);
				if (hero) _text.Append("   HERO");
				_text.Append('\n');
			}
			return _text.ToString();
		}
	}
}
