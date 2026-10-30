using System;
using System.Collections.Generic;
using ITKamianets.Engine.Save;

namespace Temnorid.Arena
{
	/// <summary>What is kept between sessions.</summary>
	[Serializable]
	public sealed class ArenaSave
	{
		public int periodsPlayed;
		public int bestKills;
		public int timesHero;
		/// <summary>The latest periods' Heroes, newest last, for example "Period 3: Gorm, Fenn, none".</summary>
		public List<string> recent = new List<string>();
	}

	/// <summary>
	/// The player's record across sessions with no Unity in them: how many periods they played, their best period,
	/// how often they were named Hero, and a short list of the latest Heroes.
	/// </summary>
	public sealed class ArenaSession
	{
		public const int RecentLimit = 8;

		readonly List<string> _recent = new List<string>();

		public int PeriodsPlayed { get; private set; }
		public int BestKills { get; private set; }
		public int TimesHero { get; private set; }
		public IReadOnlyList<string> Recent => _recent;

		/// <summary>
		/// A period ended. <paramref name="playerKills"/> are the player's kills in it, <paramref name="playerWasHero"/> whether
		/// the player was named Hero of their class, <paramref name="summary"/> the line to remember.
		/// </summary>
		public void RecordPeriod(int playerKills, bool playerWasHero, string summary)
		{
			PeriodsPlayed++;
			if (playerKills > BestKills) BestKills = playerKills;
			if (playerWasHero) TimesHero++;
			_recent.Add(summary);
			while (_recent.Count > RecentLimit) _recent.RemoveAt(0);
		}

		public ArenaSave ToSave() => new ArenaSave { periodsPlayed = PeriodsPlayed, bestKills = BestKills, timesHero = TimesHero, recent = new List<string>(_recent) };

		public void Load(ArenaSave save)
		{
			if (save == null) return;
			PeriodsPlayed = Math.Max(0, save.periodsPlayed);
			BestKills = Math.Max(0, save.bestKills);
			TimesHero = Math.Max(0, save.timesHero);
			_recent.Clear();
			if (save.recent != null) foreach (var line in save.recent) if (!string.IsNullOrEmpty(line)) _recent.Add(line);
			while (_recent.Count > RecentLimit) _recent.RemoveAt(0);
		}
	}

	/// <summary>Reads and writes the <see cref="ArenaSave"/> in the game's save slot.</summary>
	public sealed class ArenaSaveStore
	{
		const string Slot = "default";
		const int PayloadVersion = 1;

		readonly SaveStore _store;

		public ArenaSaveStore(string rootDirectory = null)
		{
			_store = new SaveStore("arena", rootDirectory);
		}

		/// <summary>The saved record, or null when there is none yet or it cannot be read.</summary>
		public ArenaSave Load()
		{
			var result = _store.Load<ArenaSave>(Slot, PayloadVersion);
			return result.Ok ? result.Value : null;
		}

		public void Save(ArenaSave save) => _store.Save(Slot, save, PayloadVersion);
	}
}
