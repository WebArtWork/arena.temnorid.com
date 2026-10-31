using System.Text;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Temnorid.Arena
{
	/// <summary>
	/// The screens: choosing a class, the fight (health, time, kills, kill feed), the scoreboard held with Tab, the end of
	/// a period with the rankings and the new Heroes, and the pause menu. Plain uGUI built in code; it only shows what
	/// <see cref="ArenaGame"/> says. The shell's bars (<see cref="ShellChrome"/>) frame it: the Arena place is this
	/// screen, the Scoreboard place a panel with the scores and the player's record. On a phone a stick, a look area and
	/// buttons stand in for the keyboard and mouse.
	/// </summary>
	public sealed class ArenaHud : MonoBehaviour
	{
		public ArenaGame Game;

		/// <summary>Tests set this to build the touch controls on a desktop.</summary>
		public static bool ForceTouch;

		static readonly Color Panel = new Color(0.06f, 0.05f, 0.09f, 0.78f);
		static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

		public bool HasTouchControls { get; private set; }

		ShellChrome _chrome;
		UiBuilder _ui;
		InputAction _scoreboard;
		GameObject _select, _hud, _board, _periodEnd, _pause, _crosshair, _placeBoard, _nextButton;
		Text _record, _classLabel, _timer, _score, _heroBanner, _respawn, _feed, _toast, _endWait;
		Text _boardW, _boardA, _boardM, _endTitle, _endW, _endA, _endM, _endNote;
		Text _placeW, _placeA, _placeM, _placeRecord;
		Image _healthFill;
		int _shown = -1;
		bool _touch;
		readonly StringBuilder _text = new StringBuilder();

		void Start()
		{
			_ui = new UiBuilder();
			UiBuilder.EnsureEventSystem(transform);
			_touch = TouchButton.IsNeeded || ForceTouch;

			// The scoreboard key is an action of this game, added to the shared input so it can be rebound like the rest.
			var map = EngineInput.Default.Asset.FindActionMap("Arena");
			if (map == null)
			{
				map = EngineInput.Default.AddMap("Arena", m => m.AddAction("Scoreboard", InputActionType.Button, "<Keyboard>/tab", groups: EngineInputActions.Groups.KeyboardMouse));
			}
			_scoreboard = map.FindAction("Scoreboard");

			// The shell's bars (the top bar, and the places at the bottom on a phone); the game's own screens live between them.
			var canvas = UiBuilder.CreateCanvas(transform);
			_chrome = ShellChrome.Create(transform);
			_chrome.PlaceSelected += OnPlace;

			var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaFitter));
			safe.transform.SetParent(canvas.transform, false);
			var content = new GameObject("Content", typeof(RectTransform));
			content.transform.SetParent(safe.transform, false);
			var rect = (RectTransform)content.transform;
			rect.anchorMin = new Vector2(0f, _chrome.BottomFraction);
			rect.anchorMax = new Vector2(1f, 1f - _chrome.TopFraction);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			var root = content.transform;

			if (_touch) BuildLookArea(root);
			BuildHud(root);
			BuildSelect(root);
			BuildBoard(root);
			BuildPeriodEnd(root);
			BuildPause(root);
			_toast = _ui.Label("Toast", root, "", 36, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.72f), Gold, wrap: true);
			BuildPlaceBoard(root);
		}

		// ---- building the screens ----

		void BuildSelect(Transform root)
		{
			_select = UiBuilder.Box("ClassSelect", root, new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.96f), Panel);
			_ui.Label("Subtitle", _select.transform, "Choose your class. Fight everyone. The best of each class becomes its Hero.", 36, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.82f), new Vector2(0.97f, 0.97f), Color.white, wrap: true);

			var columns = new[] { (0.04f, 0.32f), (0.36f, 0.64f), (0.68f, 0.96f) };
			var classes = new[] { Classes.Warrior, Classes.Archer, Classes.Mage };
			for (var i = 0; i < 3; i++)
			{
				var definition = classes[i];
				var from = columns[i].Item1;
				var to = columns[i].Item2;
				var chosen = definition.Class;
				_ui.Button(definition.Name, _select.transform, new Vector2(from, 0.56f), new Vector2(to, 0.78f), () => Game.Choose(chosen));
				_ui.Label("Info", _select.transform, $"{definition.Description}\n\nHealth {definition.Health:0}   Damage {definition.Damage:0}", 26, TextAnchor.UpperCenter, new Vector2(from, 0.26f), new Vector2(to, 0.54f), Color.white, wrap: true);
			}
			_record = _ui.Label("Record", _select.transform, "", 28, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.24f), new Color(1f, 1f, 1f, 0.85f), wrap: true);
			var hint = _touch
				? "Left thumb moves  ·  drag the right side to look  ·  Attack and Jump on the right"
				: "WASD move  ·  Shift run  ·  Space jump  ·  left mouse attack  ·  Tab scoreboard  ·  Esc pause and free the mouse for the top bar";
			_ui.Label("Hint", _select.transform, hint, 24, TextAnchor.MiddleCenter, new Vector2(0f, 0.01f), new Vector2(1f, 0.1f), new Color(1f, 1f, 1f, 0.65f), wrap: true);
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

			var left = UiBuilder.Box("HealthPanel", _hud.transform, new Vector2(0.015f, 0.86f), new Vector2(0.25f, 0.985f), Panel);
			left.GetComponent<Image>().raycastTarget = false;
			_classLabel = _ui.Label("Class", left.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.96f), Color.white);
			_healthFill = UiBuilder.Bar("Health", left.transform, new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.42f), new Color(0.85f, 0.15f, 0.12f));

			_timer = _ui.Label("Timer", _hud.transform, "", 40, TextAnchor.UpperCenter, new Vector2(0.3f, 0.92f), new Vector2(0.7f, 0.99f), Color.white);

			var right = UiBuilder.Box("ScorePanel", _hud.transform, new Vector2(0.78f, 0.86f), new Vector2(0.985f, 0.985f), Panel);
			right.GetComponent<Image>().raycastTarget = false;
			_score = _ui.Label("Score", right.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.06f, 0.1f), new Vector2(0.95f, 0.92f), Color.white);
			_heroBanner = _ui.Label("HeroBanner", _hud.transform, "", 30, TextAnchor.UpperRight, new Vector2(0.45f, 0.8f), new Vector2(0.985f, 0.86f), Gold);

			_feed = _touch
				? _ui.Label("Feed", _hud.transform, "", 26, TextAnchor.LowerLeft, new Vector2(0.22f, 0.08f), new Vector2(0.62f, 0.3f), new Color(1f, 1f, 1f, 0.85f))
				: _ui.Label("Feed", _hud.transform, "", 26, TextAnchor.LowerLeft, new Vector2(0.015f, 0.08f), new Vector2(0.4f, 0.28f), new Color(1f, 1f, 1f, 0.85f));
			_respawn = _ui.Label("Respawn", _hud.transform, "", 56, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.45f), new Vector2(0.75f, 0.58f), new Color(1f, 0.5f, 0.45f));
			_crosshair = UiBuilder.Box("Crosshair", _hud.transform, new Vector2(0.4985f, 0.4975f), new Vector2(0.5015f, 0.5025f), new Color(1f, 1f, 1f, 0.9f));
			_crosshair.GetComponent<Image>().raycastTarget = false;
			if (_touch) BuildTouch(_hud.transform);
		}

		// ---- phones: a stick on the left, buttons on the right, dragging anywhere else turns the view ----

		void BuildLookArea(Transform parent)
		{
			var area = UiBuilder.Box("LookArea", parent, Vector2.zero, Vector2.one, Color.clear);
			area.AddComponent<TouchLookArea>();
		}

		void BuildTouch(Transform parent)
		{
			HasTouchControls = true;
			var holder = UiBuilder.Box("Touch", parent, Vector2.zero, Vector2.one, Color.clear);
			holder.GetComponent<Image>().raycastTarget = false;
			var t = holder.transform;

			var stickBase = UiBuilder.Box("StickBase", t, new Vector2(0.03f, 0.08f), new Vector2(0.18f, 0.4f), new Color(1f, 1f, 1f, 0.1f));
			stickBase.GetComponent<Image>().raycastTarget = false;
			var knob = UiBuilder.Box("StickKnob", stickBase.transform, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), new Color(1f, 1f, 1f, 0.3f));
			knob.SetActive(false);
			var stick = knob.AddComponent<OnScreenStick>();
			stick.controlPath = EngineTouchControls.Path(EngineTouchControls.Move);
			stick.movementRange = 120f;
			knob.SetActive(true);

			TouchButtonFor(t, "Attack", EngineTouchControls.Primary, new Vector2(0.8f, 0.08f), new Vector2(0.97f, 0.34f), 36);
			TouchButtonFor(t, "Jump", EngineTouchControls.Jump, new Vector2(0.83f, 0.4f), new Vector2(0.96f, 0.58f), 32);
			TouchButtonFor(t, "Run", EngineTouchControls.Sprint, new Vector2(0.66f, 0.1f), new Vector2(0.77f, 0.26f), 30);

			var pause = _ui.Button("Pause", t, new Vector2(0.9f, 0.68f), new Vector2(0.985f, 0.78f), () => Game.SetPaused(true));
			pause.GetComponentInChildren<Text>().fontSize = 26;
		}

		void TouchButtonFor(Transform parent, string label, string control, Vector2 min, Vector2 max, int size)
		{
			var box = UiBuilder.Box("Touch " + label, parent, min, max, new Color(1f, 1f, 1f, 0.2f));
			_ui.Label("Text", box.transform, label, size, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.95f));
			box.SetActive(false);
			var button = box.AddComponent<OnScreenButton>();
			button.controlPath = EngineTouchControls.Path(control);
			box.SetActive(true);
		}

		void BuildBoard(Transform root)
		{
			_board = UiBuilder.Box("Scoreboard", root, new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.8f), new Color(0.06f, 0.05f, 0.09f, 0.88f));
			_board.GetComponent<Image>().raycastTarget = false;
			_ui.Label("Title", _board.transform, "SCOREBOARD   (kills / deaths)", 36, TextAnchor.MiddleCenter, new Vector2(0f, 0.86f), new Vector2(1f, 1f), Gold);
			_boardW = _ui.Label("Warriors", _board.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.03f, 0.05f), new Vector2(0.34f, 0.84f), Color.white);
			_boardA = _ui.Label("Archers", _board.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.36f, 0.05f), new Vector2(0.67f, 0.84f), Color.white);
			_boardM = _ui.Label("Mages", _board.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.69f, 0.05f), new Vector2(0.99f, 0.84f), Color.white);
		}

		// The Scoreboard place of the shell's bars: the same scores, held on screen, with the player's record and a way back.
		void BuildPlaceBoard(Transform root)
		{
			_placeBoard = UiBuilder.Box("ScoreboardPlace", root, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.97f), new Color(0.06f, 0.05f, 0.09f, 0.96f));
			_ui.Label("Title", _placeBoard.transform, "SCOREBOARD   (kills / deaths)", 44, TextAnchor.MiddleCenter, new Vector2(0f, 0.88f), new Vector2(1f, 0.99f), Gold);
			_placeW = _ui.Label("Warriors", _placeBoard.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.03f, 0.36f), new Vector2(0.34f, 0.87f), Color.white);
			_placeA = _ui.Label("Archers", _placeBoard.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.36f, 0.36f), new Vector2(0.67f, 0.87f), Color.white);
			_placeM = _ui.Label("Mages", _placeBoard.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.69f, 0.36f), new Vector2(0.99f, 0.87f), Color.white);
			_placeRecord = _ui.Label("Record", _placeBoard.transform, "", 28, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.2f), new Vector2(0.96f, 0.35f), new Color(1f, 1f, 1f, 0.85f), wrap: true);
			_ui.Button("Back to the arena", _placeBoard.transform, new Vector2(0.25f, 0.04f), new Vector2(0.75f, 0.17f), () => _chrome.Select("arena"));
			_placeBoard.SetActive(false);
		}

		void BuildPeriodEnd(Transform root)
		{
			_periodEnd = UiBuilder.Box("PeriodEnd", root, new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.96f), new Color(0.06f, 0.05f, 0.09f, 0.94f));
			_endTitle = _ui.Label("Title", _periodEnd.transform, "", 60, TextAnchor.MiddleCenter, new Vector2(0f, 0.84f), new Vector2(1f, 0.98f), Gold);
			_endNote = _ui.Label("Note", _periodEnd.transform, "", 30, TextAnchor.MiddleCenter, new Vector2(0f, 0.74f), new Vector2(1f, 0.84f), Color.white, wrap: true);
			_endW = _ui.Label("Warriors", _periodEnd.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.04f, 0.3f), new Vector2(0.34f, 0.73f), Color.white);
			_endA = _ui.Label("Archers", _periodEnd.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.36f, 0.3f), new Vector2(0.67f, 0.73f), Color.white);
			_endM = _ui.Label("Mages", _periodEnd.transform, "", 30, TextAnchor.UpperLeft, new Vector2(0.69f, 0.3f), new Vector2(0.99f, 0.73f), Color.white);
			_nextButton = _ui.Button("Next period", _periodEnd.transform, new Vector2(0.2f, 0.16f), new Vector2(0.8f, 0.28f), () => Game.NextPeriod()).gameObject;
			_endWait = _ui.Label("Wait", _periodEnd.transform, "The next period starts soon.", 30, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.16f), new Vector2(0.9f, 0.28f), new Color(1f, 1f, 1f, 0.8f));
			_ui.Button("Main menu", _periodEnd.transform, new Vector2(0.2f, 0.03f), new Vector2(0.8f, 0.14f), () => Game.QuitToMenu());
		}

		void BuildPause(Transform root)
		{
			_pause = UiBuilder.Box("PauseMenu", root, new Vector2(0.3f, 0.2f), new Vector2(0.7f, 0.8f), new Color(0.06f, 0.05f, 0.09f, 0.92f));
			_ui.Label("Title", _pause.transform, "PAUSED", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.72f), new Vector2(1f, 0.96f), Gold);
			_ui.Label("Note", _pause.transform, "", 26, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.72f), new Color(1f, 1f, 1f, 0.7f), wrap: true);
			_ui.Button("Resume", _pause.transform, new Vector2(0.2f, 0.32f), new Vector2(0.8f, 0.56f), () => Game.SetPaused(false));
			_ui.Button("Main menu", _pause.transform, new Vector2(0.2f, 0.06f), new Vector2(0.8f, 0.3f), () => Game.QuitToMenu());
		}

		void OnPlace(string id)
		{
			if (Game == null || _placeBoard == null) return;
			var board = id == "board";
			_placeBoard.SetActive(board);
			if (board) FillPlaceBoard();
			// Any place but the arena holds the game; the profile panel is the shell's own.
			Game.SetUiHold(id != "arena");
		}

		// ---- showing the game ----

		void Update()
		{
			if (Game == null || _select == null) return;
			var phase = Game.Phase;
			var player = Game.Player;

			var key = (int)phase | (Game.NeedsPick ? 8 : 0) | (Game.PauseMenuOpen ? 16 : 0) | (player != null ? 32 : 0);
			if (key != _shown)
			{
				_shown = key;
				var pick = Game.NeedsPick;
				_select.SetActive(pick);
				_hud.SetActive((phase == ArenaPhase.Playing || phase == ArenaPhase.Paused) && player != null);
				_pause.SetActive(Game.PauseMenuOpen);
				_periodEnd.SetActive(phase == ArenaPhase.PeriodEnd);
				if (phase == ArenaPhase.PeriodEnd) FillPeriodEnd();
				UiBuilder.Select(pick ? _select : Game.PauseMenuOpen ? _pause : phase == ArenaPhase.PeriodEnd ? _periodEnd : null);
			}
			if (_select.activeSelf) UiBuilder.SetText(_record, RecordText());
			UiBuilder.SetText(_toast, Time.unscaledTime < Game.ToastUntil ? Game.ToastText : "");

			var playing = phase == ArenaPhase.Playing && player != null;
			_board.SetActive(playing && !_touch && _scoreboard != null && _scoreboard.IsPressed());
			if (_board.activeSelf) FillBoard();
			if (_placeBoard.activeSelf) FillPlaceBoard();
			if (phase == ArenaPhase.PeriodEnd) _nextButton.SetActive(Game.Net == null || Game.Net.IsAuthority);
			if (phase == ArenaPhase.PeriodEnd) _endWait.gameObject.SetActive(!_nextButton.activeSelf);
			if (player == null || !_hud.activeSelf) return;

			var record = Game.Scoring.Fighters[player.Index];
			UiBuilder.SetText(_classLabel, $"{player.Definition.Name.ToUpperInvariant()}   {Mathf.CeilToInt(player.Health.Current)} / {Mathf.CeilToInt(player.Health.MaxHealth)}");
			UiBuilder.SetFill(_healthFill, player.Health.Fraction);
			UiBuilder.SetText(_timer, $"Period {Game.Scoring.PeriodNumber}    {UiBuilder.FormatTime(Game.TimeLeft)}");
			UiBuilder.SetText(_score, $"Kills  {record.Kills}\nDeaths  {record.Deaths}");
			UiBuilder.SetText(_heroBanner, record.IsHero ? $"You are the Hero of the {player.Definition.Name}s" : "");
			UiBuilder.SetText(_respawn, player.IsDown ? $"Back in {Mathf.Max(0f, player.RespawnAt - Time.time):0.0}" : "");
			_crosshair.SetActive(!player.IsDown && player.Definition.Kind != AttackKind.Melee);
			_chrome.SetStatus($"Kills {record.Kills}   Deaths {record.Deaths}");

			_text.Clear();
			foreach (var line in Game.Feed) _text.Append(line).Append('\n');
			UiBuilder.SetText(_feed, _text.ToString());
		}

		string RecordText()
		{
			var session = Game.Session;
			var online = "";
			if (Game.Net != null)
			{
				online = Game.Net.IsAuthority
					? "Online: the match starts when you choose a class. Others can join at any time.\n"
					: Game.Phase == ArenaPhase.ClassSelect
						? "Online: choose your class. The match starts when the host chooses.\n"
						: "Online: a match is running. Choose a class to join in.\n";
			}
			if (session.PeriodsPlayed == 0) return online + "No periods played yet.";
			return online + $"Periods played  {session.PeriodsPlayed}     Best period  {session.BestKills} kills     Times Hero  {session.TimesHero}\n{session.Recent[session.Recent.Count - 1]}";
		}

		void FillBoard()
		{
			UiBuilder.SetText(_boardW, ClassColumn(HeroClass.Warrior, null));
			UiBuilder.SetText(_boardA, ClassColumn(HeroClass.Archer, null));
			UiBuilder.SetText(_boardM, ClassColumn(HeroClass.Mage, null));
		}

		void FillPlaceBoard()
		{
			UiBuilder.SetText(_placeW, ClassColumn(HeroClass.Warrior, null));
			UiBuilder.SetText(_placeA, ClassColumn(HeroClass.Archer, null));
			UiBuilder.SetText(_placeM, ClassColumn(HeroClass.Mage, null));
			var session = Game.Session;
			UiBuilder.SetText(_placeRecord, session.PeriodsPlayed == 0
				? "No periods played yet."
				: $"Periods played  {session.PeriodsPlayed}     Best period  {session.BestKills} kills     Times Hero  {session.TimesHero}\n{session.Recent[session.Recent.Count - 1]}");
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
