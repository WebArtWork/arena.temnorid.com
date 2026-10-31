using System;
using System.Collections.Generic;

namespace Temnorid.Arena
{
	/// <summary>The three kinds of fighter.</summary>
	public enum HeroClass
	{
		Warrior,
		Archer,
		Mage,
	}

	/// <summary>One fighter's score in the current period.</summary>
	public sealed class FighterRecord
	{
		public readonly string Name;
		public readonly HeroClass Class;

		public int Kills;
		public int Deaths;

		/// <summary>When in the period the fighter got their last kill, in seconds (used to break ties).</summary>
		public float LastKillAt = float.MaxValue;

		/// <summary>True while the fighter holds the Hero title earned in the previous period.</summary>
		public bool IsHero;

		/// <summary>How many periods the fighter has been named Hero of their class.</summary>
		public int HeroPeriods;

		public FighterRecord(string name, HeroClass fighterClass)
		{
			Name = name;
			Class = fighterClass;
		}
	}

	/// <summary>How a finished period came out.</summary>
	public sealed class PeriodResult
	{
		public int Number;

		/// <summary>The Hero of each class, by class index; -1 when nobody of that class got a kill.</summary>
		public int[] HeroIndex = { -1, -1, -1 };

		/// <summary>Each class's fighters from first to last, by class index, as fighter indexes.</summary>
		public int[][] Rankings = new int[3][];

		/// <summary>What every fighter scored in the period, by fighter index.</summary>
		public int[] Kills;
		public int[] Deaths;
	}

	/// <summary>
	/// The scoring of the arena with no Unity in them. Everyone fights everyone; kills and deaths are counted for each
	/// fighter. When a period ends, the fighters of each class are ranked against their own class, and the best of each
	/// class becomes that class's Hero for the next period: up to three Heroes, one per class.
	///
	/// The ranking: more kills first; then fewer deaths; then the earlier last kill; then the lower index, so the order
	/// is always the same for the same scores. A class where nobody got a kill has no Hero that period.
	/// </summary>
	public sealed class ArenaScoring
	{
		readonly List<FighterRecord> _fighters = new List<FighterRecord>();
		readonly List<PeriodResult> _history = new List<PeriodResult>();

		public int PeriodNumber { get; private set; } = 1;
		public IReadOnlyList<FighterRecord> Fighters => _fighters;
		public IReadOnlyList<PeriodResult> History => _history;

		public ArenaScoring(IEnumerable<KeyValuePair<string, HeroClass>> roster)
		{
			foreach (var entry in roster) _fighters.Add(new FighterRecord(entry.Key, entry.Value));
			if (_fighters.Count == 0) throw new ArgumentException("the arena needs fighters", nameof(roster));
		}

		/// <summary>
		/// <paramref name="killer"/> defeated <paramref name="victim"/> at <paramref name="time"/> seconds into the
		/// period. A killer of -1 (a fall, a trap) only counts the death; killing yourself counts no kill.
		/// </summary>
		public void RecordKill(int killer, int victim, float time)
		{
			_fighters[victim].Deaths++;
			if (killer < 0 || killer == victim) return;
			_fighters[killer].Kills++;
			_fighters[killer].LastKillAt = time;
		}

		// ---- online: a client mirrors what the host decided ----

		/// <summary>Client: shows the host's current period and this fighter's score.</summary>
		public void Mirror(int periodNumber, int fighter, int kills, int deaths, bool hero)
		{
			if (fighter < 0 || fighter >= _fighters.Count) return;
			PeriodNumber = Math.Max(1, periodNumber);
			_fighters[fighter].Kills = kills;
			_fighters[fighter].Deaths = deaths;
			_fighters[fighter].IsHero = hero;
		}

		/// <summary>Client: the host ended a period; takes its result (Heroes, history) and starts the next period's counts.</summary>
		public void ApplyResult(PeriodResult result)
		{
			foreach (var fighter in _fighters) fighter.IsHero = false;
			for (var c = 0; c < result.HeroIndex.Length; c++)
			{
				var hero = result.HeroIndex[c];
				if (hero < 0 || hero >= _fighters.Count) continue;
				_fighters[hero].IsHero = true;
				_fighters[hero].HeroPeriods++;
			}
			_history.Add(result);
			PeriodNumber = result.Number + 1;
			foreach (var fighter in _fighters)
			{
				fighter.Kills = 0;
				fighter.Deaths = 0;
				fighter.LastKillAt = float.MaxValue;
			}
		}

		/// <summary>The fighters of one class from first to last, as fighter indexes.</summary>
		public int[] Ranking(HeroClass fighterClass)
		{
			var indexes = new List<int>();
			for (var i = 0; i < _fighters.Count; i++) if (_fighters[i].Class == fighterClass) indexes.Add(i);
			indexes.Sort(Compare);
			return indexes.ToArray();
		}

		int Compare(int a, int b)
		{
			var fa = _fighters[a];
			var fb = _fighters[b];
			if (fa.Kills != fb.Kills) return fb.Kills.CompareTo(fa.Kills);
			if (fa.Deaths != fb.Deaths) return fa.Deaths.CompareTo(fb.Deaths);
			if (fa.LastKillAt != fb.LastKillAt) return fa.LastKillAt.CompareTo(fb.LastKillAt);
			return a.CompareTo(b);
		}

		/// <summary>
		/// Ends the period: names a Hero for each class (taking the title from the old one), remembers the result, resets
		/// the scores and starts the next period. Heroes keep their title for the whole next period.
		/// </summary>
		public PeriodResult EndPeriod()
		{
			var result = new PeriodResult { Number = PeriodNumber, Kills = new int[_fighters.Count], Deaths = new int[_fighters.Count] };
			for (var i = 0; i < _fighters.Count; i++)
			{
				result.Kills[i] = _fighters[i].Kills;
				result.Deaths[i] = _fighters[i].Deaths;
			}
			foreach (var fighter in _fighters) fighter.IsHero = false;

			foreach (HeroClass fighterClass in Enum.GetValues(typeof(HeroClass)))
			{
				var ranking = Ranking(fighterClass);
				result.Rankings[(int)fighterClass] = ranking;
				if (ranking.Length == 0 || _fighters[ranking[0]].Kills < 1) continue;

				result.HeroIndex[(int)fighterClass] = ranking[0];
				_fighters[ranking[0]].IsHero = true;
				_fighters[ranking[0]].HeroPeriods++;
			}

			_history.Add(result);
			PeriodNumber++;
			foreach (var fighter in _fighters)
			{
				fighter.Kills = 0;
				fighter.Deaths = 0;
				fighter.LastKillAt = float.MaxValue;
			}
			return result;
		}
	}
}
