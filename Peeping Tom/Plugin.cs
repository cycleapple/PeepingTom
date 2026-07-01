using System;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using PeepingTom.Localization;

namespace PeepingTom
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PeepingTomPlugin : IDalamudPlugin
    {
        public static string Name => "Peeping Tom";

        internal IDalamudPluginInterface Interface { get; }
        internal IClientState ClientState { get; }
        private ICommandManager CommandManager { get; }
        internal IDataManager DataManager { get; }
        internal IFramework Framework { get; }
        internal IGameGui GameGui { get; }
        internal IObjectTable ObjectTable { get; }
        internal IPluginLog Log { get; }

        internal Configuration Config { get; }
        internal PluginUi Ui { get; }
        internal TargetWatcher Watcher { get; }

        internal bool InPvp { get; private set; }

        public PeepingTomPlugin(
            IDalamudPluginInterface pluginInterface,
            IClientState clientState,
            ICommandManager commandManager,
            IDataManager dataManager,
            IFramework framework,
            IGameGui gameGui,
            IObjectTable objectTable,
            IPluginLog log
        )
        {
            Interface = pluginInterface;
            ClientState = clientState;
            CommandManager = commandManager;
            DataManager = dataManager;
            Framework = framework;
            GameGui = gameGui;
            ObjectTable = objectTable;
            Log = log;

            Config = Interface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(Interface);
            Watcher = new TargetWatcher(this);
            Ui = new PluginUi(this);

            Loc.Load(Interface);
            Interface.LanguageChanged += OnLanguageChange;

            CommandManager.AddHandler("/ppeepingtom", new CommandInfo(OnCommand) { HelpMessage = "Opens the marker settings." });
            CommandManager.AddHandler("/ptom", new CommandInfo(OnCommand) { HelpMessage = "Alias for /ppeepingtom" });
            CommandManager.AddHandler("/ppeep", new CommandInfo(OnCommand) { HelpMessage = "Alias for /ppeepingtom" });

            ClientState.TerritoryChanged += OnTerritoryChange;
            Interface.UiBuilder.Draw += DrawUi;
            Interface.UiBuilder.OpenMainUi += MainUi;
            Interface.UiBuilder.OpenConfigUi += ConfigUi;

            UpdatePvpState(ClientState.TerritoryType);
        }

        public void Dispose()
        {
            Interface.UiBuilder.OpenConfigUi -= ConfigUi;
            Interface.UiBuilder.OpenMainUi -= MainUi;
            Interface.UiBuilder.Draw -= DrawUi;
            ClientState.TerritoryChanged -= OnTerritoryChange;
            CommandManager.RemoveHandler("/ppeep");
            CommandManager.RemoveHandler("/ptom");
            CommandManager.RemoveHandler("/ppeepingtom");
            Interface.LanguageChanged -= OnLanguageChange;
            Ui.Dispose();
            Watcher.Dispose();
        }

        private void OnLanguageChange(string langCode)
        {
            Loc.Load(Interface);
        }

        private void OnTerritoryChange(uint territoryId)
        {
            UpdatePvpState(territoryId);
        }

        private void UpdatePvpState(uint territoryId)
        {
            try
            {
                var territory = DataManager.GetExcelSheet<TerritoryType>().GetRow(territoryId);
                InPvp = territory.IsPvpZone;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not get territory for current zone");
            }
        }

        private void OnCommand(string command, string args)
        {
            Ui.SettingsOpen = true;
        }

        private void DrawUi()
        {
            Ui.Draw();
        }

        private void MainUi()
        {
            Ui.SettingsOpen = true;
        }

        private void ConfigUi()
        {
            Ui.SettingsOpen = true;
        }
    }
}
