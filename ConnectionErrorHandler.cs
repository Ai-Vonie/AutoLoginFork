using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Game.ClientState.Conditions;
using System;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System.Threading.Tasks;
using System.Threading;

namespace AutoLogin
{
    internal unsafe class ConnectionErrorHandler : IDisposable
    {
        private Plugin plugin;
        private bool seenErrorWindow = false;
        private bool isReconnecting = false;
        private bool hasClickedOk = false;
        private int retryAttempts = 0;
        private const int MaxRetryAttempts = 3;
        private const int InitialDelayFrames = 180;
        private const int RetryDelayFrames = 120;
        private int currentInitialDelay = 0;
        private int currentRetryDelay = 0;

        protected bool ClickButtonIfEnabled(AtkComponentButton* button, AtkUnitBase* addon)
        {
            if(button->IsEnabled && button->AtkResNode->IsVisible())
            {
                var btnRes = button->AtkComponentBase.OwnerNode->AtkResNode;
                var evt = (AtkEvent*)btnRes.AtkEventManager.Event;
                addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, btnRes.AtkEventManager.Event);
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            Plugin.Framework.Update -= ConnectionErrorWatcher;
        }

        public ConnectionErrorHandler(Plugin plugin)
        {
            this.plugin = plugin;
            Plugin.Framework.Update += ConnectionErrorWatcher;
        }

        private void ConnectionErrorWatcher(object framework)
        {
            if (!Plugin.ClientState.IsLoggedIn)
            {
                var addonPtr = Plugin.GameGui.GetAddonByName("Dialogue", 1);
                if (addonPtr != IntPtr.Zero && ((AtkUnitBase*)addonPtr)->IsVisible)
                {
                    if (!seenErrorWindow && !isReconnecting && Plugin.PluginConfig.AutoReconnectEnabled)
                    {
                        seenErrorWindow = true;
                        isReconnecting = true;
                        hasClickedOk = false;
                        retryAttempts = 0;
                        currentInitialDelay = InitialDelayFrames;
                        currentRetryDelay = 0;
                        
                        Plugin.PluginLog.Information($"Connection error detected, waiting {InitialDelayFrames/60f:F1} seconds before first attempt...");
                        Plugin.NotifObject.Content = "Connection error detected. Starting auto-reconnect...";
                        Plugin.NotifObject.Type = Dalamud.Interface.ImGuiNotification.NotificationType.Warning;
                        Plugin.NotificationManager.AddNotification(Plugin.NotifObject);
                        return;
                    }

                    if (currentInitialDelay > 0)
                    {
                        currentInitialDelay--;
                        if (currentInitialDelay % 60 == 0)
                        {
                            Plugin.PluginLog.Debug($"Initial delay: {currentInitialDelay/60f:F1} seconds remaining...");
                        }
                        return;
                    }

                    if (currentRetryDelay > 0)
                    {
                        currentRetryDelay--;
                        if (currentRetryDelay % 60 == 0)
                        {
                            Plugin.PluginLog.Debug($"Retry delay: {currentRetryDelay/60f:F1} seconds remaining...");
                        }
                        return;
                    }

                    if (isReconnecting && !hasClickedOk && retryAttempts < MaxRetryAttempts)
                    {
                        var addon = (AtkUnitBase*)addonPtr;
                        var okButton = addon->GetButtonNodeById(4);
                        
                        if (okButton != null)
                        {
                            Plugin.PluginLog.Debug($"OK Button found. Enabled: {okButton->IsEnabled}, Visible: {okButton->AtkResNode->IsVisible()}");
                            
                            if (okButton->IsEnabled && okButton->AtkResNode->IsVisible())
                            {
                                hasClickedOk = true;
                                Plugin.PluginLog.Information($"Attempting to close error dialog (attempt {retryAttempts + 1}/{MaxRetryAttempts})...");

                                try
                                {
                                    if (ClickButtonIfEnabled(okButton, addon))
                                    {
                                        Plugin.PluginLog.Debug("Button click event sent");
                                        
                                        Thread.Sleep(100);

                                        if (!addon->IsVisible)
                                        {
                                            Plugin.PluginLog.Information("Dialog successfully closed");
                                            Plugin.Framework.Update += DelayedAutoLogin;
                                            ResetDelays();
                                        }
                                        else
                                        {
                                            Plugin.PluginLog.Warning("Dialog did not close after click, will retry after delay...");
                                            hasClickedOk = false;
                                            retryAttempts++;
                                            currentRetryDelay = RetryDelayFrames;
                                        }
                                    }
                                    else
                                    {
                                        Plugin.PluginLog.Warning("Failed to click button, will retry after delay...");
                                        hasClickedOk = false;
                                        retryAttempts++;
                                        currentRetryDelay = RetryDelayFrames;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Plugin.PluginLog.Error($"Error while trying to close dialog: {ex.Message}");
                                    retryAttempts++;
                                    hasClickedOk = false;
                                    currentRetryDelay = RetryDelayFrames;
                                }
                            }
                            else
                            {
                                retryAttempts++;
                                hasClickedOk = false;
                                currentRetryDelay = RetryDelayFrames;
                                Plugin.PluginLog.Warning($"OK Button not ready. Retry attempt {retryAttempts}/{MaxRetryAttempts} will start after delay");
                            }
                        }
                        else
                        {
                            Plugin.PluginLog.Error("Failed to find OK button in error dialog");
                        }
                    }
                }
                else
                {
                    ResetState();
                }
            }
            else
            {
                ResetState();
            }
        }

        private void ResetState()
        {
            seenErrorWindow = false;
            isReconnecting = false;
            hasClickedOk = false;
            retryAttempts = 0;
            ResetDelays();
        }

        private void ResetDelays()
        {
            currentInitialDelay = 0;
            currentRetryDelay = 0;
        }

        private int loginDelayFrames = 60;
        private void DelayedAutoLogin(object framework)
        {
            loginDelayFrames--;
            if (loginDelayFrames <= 0)
            {
                Plugin.Framework.Update -= DelayedAutoLogin;
                loginDelayFrames = 60;
                
                Plugin.PluginLog.Information("Starting auto-login process...");
                plugin.AutoLogin();
            }
        }

        internal static void Setup(bool enable, Plugin plugin)
        {
            if (enable)
            {
                if (plugin.ConnectionErrorHandler == null)
                {
                    plugin.ConnectionErrorHandler = new ConnectionErrorHandler(plugin);
                    Plugin.PluginLog.Information("Enabling connection error handler");
                }
            }
            else
            {
                if (plugin.ConnectionErrorHandler != null)
                {
                    plugin.ConnectionErrorHandler.Dispose();
                    plugin.ConnectionErrorHandler = null;
                    Plugin.PluginLog.Information("Disabling connection error handler");
                }
            }
        }
    }
} 
