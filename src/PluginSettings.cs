using Playnite.SDK;
using System.Collections.Generic;

namespace PlaytimeLock
{
    public class PluginSettings : ObservableObject, ISettings
    {
        private readonly PlaytimeLockPlugin plugin;
        private PluginSettings editingClone;

        private bool addLockTag = true;

        public bool AddLockTag { get => addLockTag; set => SetValue(ref addLockTag, value); }

        // Needed by LoadPluginSettings<T>
        public PluginSettings() { }

        public PluginSettings(PlaytimeLockPlugin plugin)
        {
            this.plugin = plugin;
            var saved = plugin.LoadPluginSettings<PluginSettings>();
            if (saved != null)
            {
                CopyFrom(saved);
            }
        }

        private void CopyFrom(PluginSettings other)
        {
            AddLockTag = other.AddLockTag;
        }

        public void BeginEdit()
        {
            editingClone = new PluginSettings();
            editingClone.CopyFrom(this);
        }

        public void CancelEdit()
        {
            if (editingClone != null) CopyFrom(editingClone);
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
