using System;
using System.Globalization;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using PeepingTom.Resources;

namespace PeepingTom
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PeepingTomPlugin : IDalamudPlugin
    {
        public static string Name => "Peeping Tom";

        internal IDalamudPluginInterface Interface { get; }
        internal IChatGui ChatGui { get; }
        internal IClientState ClientState { get; }
        private ICommandManager CommandManager { get; }
        internal ICondition Condition { get; }
        internal IDataManager DataManager { get; }
        internal IFramework Framework { get; }
        internal IGameGui GameGui { get; }
        internal IObjectTable ObjectTable { get; }
        internal ITargetManager TargetManager { get; }
        internal IToastGui ToastGui { get; }
        internal IPluginLog Log { get; }

        internal Configuration Config { get; }
        internal PluginUi Ui { get; }
        internal TargetWatcher Watcher { get; }
        internal IpcManager IpcManager { get; }
        internal ExamineHelper ExamineHelper { get; }

        internal bool InPvp { get; private set; }

        public PeepingTomPlugin(
            IDalamudPluginInterface pluginInterface,
            IChatGui chatGui,
            IClientState clientState,
            ICommandManager commandManager,
            ICondition condition,
            IDataManager dataManager,
            IFramework framework,
            IGameGui gameGui,
            IObjectTable objectTable,
            ITargetManager targetManager,
            IToastGui toastGui,
            IPluginLog log
        )
        {
            Interface = pluginInterface;
            ChatGui = chatGui;
            ClientState = clientState;
            CommandManager = commandManager;
            Condition = condition;
            DataManager = dataManager;
            Framework = framework;
            GameGui = gameGui;
            ObjectTable = objectTable;
            TargetManager = targetManager;
            ToastGui = toastGui;
            Log = log;

            Config = Interface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Initialize(Interface);
            ExamineHelper = new ExamineHelper();
            Watcher = new TargetWatcher(this);
            Ui = new PluginUi(this);
            IpcManager = new IpcManager(this);

            OnLanguageChange(Interface.UiLanguage);
            Interface.LanguageChanged += OnLanguageChange;

            CommandManager.AddHandler(
                "/ppeepingtom",
                new CommandInfo(OnCommand)
                {
                    HelpMessage = "Use with no arguments to show the list. Use with \"c\" or \"config\" to show the config",
                }
            );
            CommandManager.AddHandler("/ptom", new CommandInfo(OnCommand) { HelpMessage = "Alias for /ppeepingtom" });
            CommandManager.AddHandler("/ppeep", new CommandInfo(OnCommand) { HelpMessage = "Alias for /ppeepingtom" });

            ClientState.Login += OnLogin;
            ClientState.Logout += OnLogout;
            ClientState.TerritoryChanged += OnTerritoryChange;
            Interface.UiBuilder.Draw += DrawUi;
            Interface.UiBuilder.OpenConfigUi += ConfigUi;

            UpdatePvpState(ClientState.TerritoryType);
        }

        public void Dispose()
        {
            Interface.UiBuilder.OpenConfigUi -= ConfigUi;
            Interface.UiBuilder.Draw -= DrawUi;
            ClientState.TerritoryChanged -= OnTerritoryChange;
            ClientState.Logout -= OnLogout;
            ClientState.Login -= OnLogin;
            CommandManager.RemoveHandler("/ppeep");
            CommandManager.RemoveHandler("/ptom");
            CommandManager.RemoveHandler("/ppeepingtom");
            Interface.LanguageChanged -= OnLanguageChange;
            IpcManager.Dispose();
            Ui.Dispose();
            Watcher.Dispose();
        }

        private static void OnLanguageChange(string langCode)
        {
            Language.Culture = new CultureInfo(langCode);
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
            if (args is "config" or "c")
            {
                Ui.SettingsOpen = true;
            }
            else
            {
                Ui.WantsOpen = true;
            }
        }

        private void OnLogin()
        {
            if (!Config.OpenOnLogin)
            {
                return;
            }

            Ui.WantsOpen = true;
        }

        private void OnLogout(int type, int code)
        {
            Ui.WantsOpen = false;
            Watcher.ClearPrevious();
        }

        private void DrawUi()
        {
            Ui.Draw();
        }

        private void ConfigUi()
        {
            Ui.SettingsOpen = true;
        }
    }
}
