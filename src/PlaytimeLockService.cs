using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlaytimeLock
{
    public class LockEntry
    {
        public string Name { get; set; }
        public ulong Playtime { get; set; }
        public ulong PlayCount { get; set; }
    }

    /// <summary>
    /// Locked games never lose playtime / play count.
    /// Increases (Playnite session tracking, higher store values, manual edits upward) are accepted and remembered;
    /// decreases (store sync resetting to 0) are reverted silently.
    /// </summary>
    internal class PlaytimeLockService
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly PlaytimeLockPlugin plugin;
        private readonly IPlayniteAPI api;
        private readonly string filePath;
        private readonly object sync = new object();
        private Dictionary<string, LockEntry> locks = new Dictionary<string, LockEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Guid> pendingRestore = new HashSet<Guid>();
        private bool restoreScheduled;

        public PlaytimeLockService(PlaytimeLockPlugin plugin)
        {
            this.plugin = plugin;
            api = plugin.PlayniteApi;
            filePath = Path.Combine(plugin.GetPluginUserDataPath(), "locks.json");
            Load();
        }

        #region Storage

        private void Load()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var loaded = Serialization.FromJson<Dictionary<string, LockEntry>>(File.ReadAllText(filePath));
                    if (loaded != null)
                    {
                        locks = new Dictionary<string, LockEntry>(loaded, StringComparer.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    ImportLegacyLocks();
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to load playtime locks.");
            }
        }

        private class LegacyLock
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public ulong Playtime { get; set; }
            public ulong PlayCount { get; set; }
        }

        /// <summary>Imports locks from the earlier PowerShell version of this extension, if present.</summary>
        private void ImportLegacyLocks()
        {
            try
            {
                var legacy = Path.Combine(api.Paths.ExtensionsDataPath, "SureKilidi_Ataberk_Script", "kilitler.json");
                if (!File.Exists(legacy)) return;
                var items = Serialization.FromJson<List<LegacyLock>>(File.ReadAllText(legacy));
                if (items == null) return;
                foreach (var item in items.Where(a => a != null && !string.IsNullOrEmpty(a.Id)))
                {
                    locks[item.Id] = new LockEntry { Name = item.Name, Playtime = item.Playtime, PlayCount = item.PlayCount };
                }
                logger.Info($"Imported {locks.Count} playtime locks from the PowerShell version.");
                Save();
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to import legacy playtime locks.");
            }
        }

        private void Save()
        {
            try
            {
                string json;
                lock (sync)
                {
                    json = Serialization.ToJson(locks, true);
                }
                var tmp = filePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(filePath))
                {
                    File.Replace(tmp, filePath, null, true);
                }
                else
                {
                    File.Move(tmp, filePath);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to save playtime locks.");
            }
        }

        #endregion

        public bool IsLocked(Guid gameId)
        {
            lock (sync) { return locks.ContainsKey(gameId.ToString()); }
        }

        public void Start()
        {
            api.Database.Games.ItemUpdated += Games_ItemUpdated;
            SyncAll();
        }

        public void Stop()
        {
            api.Database.Games.ItemUpdated -= Games_ItemUpdated;
        }

        /// <summary>
        /// Reacts to every game change in the database (library update, manual edit, Playnite session tracking).
        /// May be called from a background thread.
        /// </summary>
        private void Games_ItemUpdated(object sender, ItemUpdatedEventArgs<Game> e)
        {
            var dirty = false;
            var needsRestore = false;
            lock (sync)
            {
                foreach (var update in e.UpdatedItems)
                {
                    var game = update.NewData;
                    if (game == null) continue;

                    LockEntry entry;
                    if (!locks.TryGetValue(game.Id.ToString(), out entry)) continue;

                    if (game.Playtime > entry.Playtime) { entry.Playtime = game.Playtime; dirty = true; }
                    else if (game.Playtime < entry.Playtime) { pendingRestore.Add(game.Id); needsRestore = true; }

                    if (game.PlayCount > entry.PlayCount) { entry.PlayCount = game.PlayCount; dirty = true; }
                    else if (game.PlayCount < entry.PlayCount) { pendingRestore.Add(game.Id); needsRestore = true; }

                    if (entry.Name != game.Name) { entry.Name = game.Name; dirty = true; }
                }

                if (needsRestore && !restoreScheduled)
                {
                    restoreScheduled = true;
                }
                else
                {
                    needsRestore = false;
                }
            }

            if (dirty) Save();
            if (needsRestore)
            {
                // Do the actual write after the current database operation has finished.
                api.MainView.UIDispatcher.BeginInvoke(new Action(RestorePending));
            }
        }

        private void RestorePending()
        {
            List<Guid> ids;
            lock (sync)
            {
                ids = pendingRestore.ToList();
                pendingRestore.Clear();
                restoreScheduled = false;
            }
            RestoreGames(ids);
        }

        /// <summary>Raises playtime/play count of the given games back to their locked values.</summary>
        private void RestoreGames(IEnumerable<Guid> ids)
        {
            using (api.Database.BufferedUpdate())
            {
                foreach (var id in ids)
                {
                    LockEntry entry;
                    lock (sync)
                    {
                        if (!locks.TryGetValue(id.ToString(), out entry)) continue;
                    }

                    var game = api.Database.Games.Get(id);
                    if (game == null) continue;

                    var changed = false;
                    if (game.Playtime < entry.Playtime)
                    {
                        logger.Info($"Restoring playtime of '{game.Name}': {game.Playtime}s -> {entry.Playtime}s");
                        game.Playtime = entry.Playtime;
                        changed = true;
                    }
                    if (game.PlayCount < entry.PlayCount)
                    {
                        game.PlayCount = entry.PlayCount;
                        changed = true;
                    }
                    if (changed)
                    {
                        api.Database.Games.Update(game);
                    }
                }
            }
        }

        /// <summary>Full check of all locked games (on startup and after library updates).</summary>
        public void SyncAll()
        {
            var toRestore = new List<Guid>();
            var dirty = false;
            lock (sync)
            {
                foreach (var key in locks.Keys.ToList())
                {
                    Guid id;
                    if (!Guid.TryParse(key, out id)) { locks.Remove(key); dirty = true; continue; }

                    var game = api.Database.Games.Get(id);
                    if (game == null)
                    {
                        // Game was removed from the library
                        locks.Remove(key);
                        dirty = true;
                        continue;
                    }

                    var entry = locks[key];
                    if (game.Playtime > entry.Playtime) { entry.Playtime = game.Playtime; dirty = true; }
                    if (game.PlayCount > entry.PlayCount) { entry.PlayCount = game.PlayCount; dirty = true; }
                    if (entry.Name != game.Name) { entry.Name = game.Name; dirty = true; }
                    if (game.Playtime < entry.Playtime || game.PlayCount < entry.PlayCount) toRestore.Add(id);
                }
            }

            if (dirty) Save();
            RestoreGames(toRestore);
        }

        #region Actions

        /// <summary>
        /// Locks the game. With a value, the playtime is set to it (may be lower than before);
        /// without one, the current playtime is locked.
        /// </summary>
        public void Lock(Guid gameId, ulong? playtime)
        {
            var game = api.Database.Games.Get(gameId);
            if (game == null) return;
            var value = playtime ?? game.Playtime;

            // Update the lock first so our own database update is not treated as a reset.
            lock (sync)
            {
                locks[gameId.ToString()] = new LockEntry
                {
                    Name = game.Name,
                    Playtime = value,
                    PlayCount = game.PlayCount
                };
            }
            Save();

            var changed = game.Playtime != value;
            game.Playtime = value;
            if (plugin.Settings.AddLockTag && AddTag(game)) changed = true;
            if (changed) api.Database.Games.Update(game);
            logger.Info($"Locked playtime of '{game.Name}' at {value}s");
        }

        public void Unlock(Guid gameId)
        {
            bool removed;
            lock (sync) { removed = locks.Remove(gameId.ToString()); }
            if (!removed) return;
            Save();

            var game = api.Database.Games.Get(gameId);
            if (game != null && RemoveTag(game))
            {
                api.Database.Games.Update(game);
            }
            logger.Info($"Unlocked playtime of '{game?.Name}'");
        }

        public List<LockEntry> GetAllLocks()
        {
            lock (sync) { return locks.Values.OrderBy(a => a.Name).ToList(); }
        }

        #endregion

        #region Tag

        private Tag GetTag(bool create)
        {
            var name = Loc.Get("LockTagName");
            var tag = api.Database.Tags.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            if (tag == null && create)
            {
                tag = new Tag(name);
                api.Database.Tags.Add(tag);
            }
            return tag;
        }

        private bool AddTag(Game game)
        {
            var tag = GetTag(true);
            if (game.TagIds == null) game.TagIds = new List<Guid>();
            if (game.TagIds.Contains(tag.Id)) return false;
            game.TagIds.Add(tag.Id);
            return true;
        }

        private bool RemoveTag(Game game)
        {
            var tag = GetTag(false);
            return tag != null && game.TagIds != null && game.TagIds.Remove(tag.Id);
        }

        #endregion

        /// <summary>
        /// Parses "12" (hours), "12.5" / "12,5", "12:30" (h:mm), "45m" / "45dk" / "45min" (minutes).
        /// </summary>
        public static ulong? ParsePlaytime(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text.Trim().ToLowerInvariant();

            var m = Regex.Match(t, @"^(\d+)\s*(m|min|dk|dakika)$");
            if (m.Success) return ulong.Parse(m.Groups[1].Value) * 60;

            m = Regex.Match(t, @"^(\d+)\s*:\s*(\d{1,2})$");
            if (m.Success) return ulong.Parse(m.Groups[1].Value) * 3600 + ulong.Parse(m.Groups[2].Value) * 60;

            t = Regex.Replace(t, @"\s*(h|hr|hours?|sa|saat)$", "").Replace(',', '.');
            double hours;
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out hours) && hours >= 0 && hours < 1000000)
            {
                return (ulong)Math.Round(hours * 3600);
            }
            return null;
        }
    }
}
