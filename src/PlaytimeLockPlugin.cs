using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace PlaytimeLock
{
    public class PlaytimeLockPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("35892ff4-9120-4f10-93a2-59d76b742244");

        internal PluginSettings Settings { get; private set; }
        internal PlaytimeLockService Locks { get; private set; }

        public PlaytimeLockPlugin(IPlayniteAPI api) : base(api)
        {
            Loc.Init(api);
            Settings = new PluginSettings(this);
            Properties = new GenericPluginProperties { HasSettings = true };
            Locks = new PlaytimeLockService(this);
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            Safe(() => Locks.Start());
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            Safe(() => Locks.Stop());
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            Safe(() => Locks.SyncAll());
        }

        #region Menus

        /// <summary>One top-level item: "Lock playtime..." or "Unlock playtime" depending on the selection.</summary>
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var games = args.Games ?? new List<Game>();
            if (games.Count == 0) yield break;

            if (games.Any(g => !Locks.IsLocked(g.Id)))
            {
                yield return new GameMenuItem
                {
                    Description = Loc.Get("MenuLock"),
                    Action = a => Safe(() => LockGames(a.Games))
                };
            }

            if (games.Any(g => Locks.IsLocked(g.Id)))
            {
                yield return new GameMenuItem
                {
                    Description = Loc.Get("MenuUnlock"),
                    Action = a => Safe(() => UnlockGames(a.Games))
                };
            }
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = Loc.Get("MenuListLocks"),
                MenuSection = "@" + Loc.Get("LockSection"),
                Action = a => Safe(ListLocks)
            };
        }

        #endregion

        #region Actions

        private void LockGames(List<Game> selected)
        {
            var games = selected.Where(g => !Locks.IsLocked(g.Id)).ToList();
            if (games.Count == 0) return;

            if (games.Count > 1)
            {
                // Several games: lock each at its current playtime.
                using (PlayniteApi.Database.BufferedUpdate())
                {
                    foreach (var game in games) Locks.Lock(game.Id, null);
                }
                return;
            }

            var single = PlayniteApi.Database.Games.Get(games[0].Id);
            if (single == null) return;
            var current = single.Playtime;
            var defaultInput = $"{current / 3600}:{(current % 3600) / 60:00}";

            var input = PlayniteApi.Dialogs.SelectString(Loc.Format("LockPrompt", single.Name), Loc.Get("LockSection"), defaultInput);
            if (!input.Result) return;

            var value = PlaytimeLockService.ParsePlaytime(input.SelectedString);
            if (value == null)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(Loc.Format("ParseError", input.SelectedString), Loc.Get("LockSection"));
                return;
            }

            // Keep the exact seconds if the user didn't change the suggested value.
            Locks.Lock(single.Id, input.SelectedString.Trim() == defaultInput ? current : value.Value);
        }

        private void UnlockGames(List<Game> selected)
        {
            using (PlayniteApi.Database.BufferedUpdate())
            {
                foreach (var game in selected.Where(g => Locks.IsLocked(g.Id)))
                {
                    Locks.Unlock(game.Id);
                }
            }
        }

        private void ListLocks()
        {
            var caption = Loc.Get("LockSection");
            var all = Locks.GetAllLocks();
            if (all.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage(Loc.Get("NoLocks"), caption);
                return;
            }
            var lines = all.Select(a => $"• {a.Name}: {Loc.Playtime(a.Playtime)}");
            PlayniteApi.Dialogs.ShowMessage(Loc.Format("LockList", all.Count, string.Join("\n", lines)), caption);
        }

        #endregion

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SettingsView();
        }

        private void Safe(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                logger.Error(e, "PlaytimeLock error");
            }
        }
    }
}
