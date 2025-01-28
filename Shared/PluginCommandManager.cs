using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Dalamud.Game.Command.CommandInfo;
using static Dalamud.Game.Command.IReadOnlyCommandInfo;

namespace Veda
{
    public class PluginCommandManager<THost> : IDisposable
    {
        private readonly ICommandManager commandManager;
        private readonly (string, CommandInfo)[] pluginCommands;
        private readonly THost host;

        public PluginCommandManager(THost host, ICommandManager commands)
        {
            this.commandManager = commands;
            this.host = host;

            this.pluginCommands = host.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
              .Where(method => method.GetCustomAttribute<CommandAttribute>() != null)
              .SelectMany(GetCommandInfoTuple)
              .ToArray();

            AddCommandHandlers();
        }

        private void AddCommandHandlers()
        {
            for (var i = 0; i < this.pluginCommands.Length; i++)
            {
                var (command, commandInfo) = this.pluginCommands[i];
                this.commandManager.AddHandler(command, commandInfo);
            }
        }

        private void RemoveCommandHandlers()
        {
            for (var i = 0; i < this.pluginCommands.Length; i++)
            {
                var (command, _) = this.pluginCommands[i];
                this.commandManager.RemoveHandler(command);
            }
        }

        private IEnumerable<(string, CommandInfo)> GetCommandInfoTuple(MethodInfo method)
        {
            var handlerDelegate = (HandlerDelegate)Delegate.CreateDelegate(typeof(HandlerDelegate), this.host, method);

            var command = handlerDelegate.Method.GetCustomAttribute<CommandAttribute>();
            var aliases = handlerDelegate.Method.GetCustomAttribute<AliasesAttribute>();
            var helpMessage = handlerDelegate.Method.GetCustomAttribute<HelpMessageAttribute>();

            var commandInfo = new CommandInfo(handlerDelegate)
            {
                HelpMessage = helpMessage?.HelpMessage ?? string.Empty,
                ShowInHelp = true,
            };

            var commandInfoTuples = new List<(string, CommandInfo)> { (command.Command, commandInfo) };
            if (aliases != null)
            {
                for (var i = 0; i < aliases.Aliases.Length; i++)
                {
                    commandInfoTuples.Add((aliases.Aliases[i], commandInfo));
                }
            }

            return commandInfoTuples;
        }

        public void Dispose()
        {
            RemoveCommandHandlers();
        }
    }
} 