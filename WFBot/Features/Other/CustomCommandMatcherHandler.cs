﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using GammaLibrary.Extensions;
using WFBot.Features.Commands;
using WFBot.Features.Events;
using WFBot.Features.ImageRendering;
using WFBot.Orichalt;
using WFBot.TextCommandCore;
using WFBot.Utils;

namespace WFBot.Features.Other
{
    public class CustomCommandMatcherHandler : ICommandHandler<CustomCommandMatcherHandler>
    {
        internal static Lazy<CommandInfo[]> _commandInfos =
            new Lazy<CommandInfo[]>(CommandHandlerHelper.GetCommandInfos<CommandsHandler>());

        static Dictionary<string, List<string>> CustomCommandsRegistry => CustomCommandConfig.Instance.CustomCommands;

        static volatile bool _inited;

        public static void InitCustomCommandHandler()
        {
            if (_inited) return;
            _inited = true;

            CustomCommandConfig.Update();
            foreach (var info in _commandInfos.Value)
            {
                info.Matchers.Add(s =>
                {
                    var commandID = info.Method.Name;
                    if (ContainsCustomCommand(commandID))
                    {
                        return CustomCommandConfig.Instance.CustomCommands[commandID]
                            .Any(customMatcher => string.Equals(customMatcher, s, StringComparison.OrdinalIgnoreCase));
                    }
                    return false;
                });
            }
        }

        [MatchersIgnoreCase("PrintCustomCommandMatchers")]
        void PrintCustomCommands()
        {
            bool found = false;
            foreach (var info in _commandInfos.Value)
            {
                var commandID = info.Method.Name;
                Trace.WriteLine($"ID: {commandID}");
                if (ContainsCustomCommand(commandID))
                {
                    Trace.WriteLine($"    自定义匹配器: [{GetCustomCommandList(commandID).Connect()}]\n");
                    found = true;
                }
            }

            if (!found)
            {
                Trace.WriteLine("没有一个命令有自定义匹配器.");
            }
        }

        [MatchersIgnoreCase("PrintCommands")]
        void PrintCommands()
        {
            foreach (var info in _commandInfos.Value)
            {
                var commandID = info.Method.Name;
                Trace.WriteLine($"ID: {commandID}");
                if (info.Method.IsAttributeDefined<MatchersAttribute>())
                {
                    Trace.WriteLine($"    预定义匹配器: [{info.Method.GetCustomAttribute<MatchersAttribute>().Matchers.Connect()}]");
                }
                if (info.Method.IsAttributeDefined<MatchersIgnoreCaseAttribute>())
                {
                    Trace.WriteLine($"    预定义匹配器(不区分大小写): [{info.Method.GetCustomAttribute<MatchersIgnoreCaseAttribute>().Matchers.Connect()}]");
                }

                if (ContainsCustomCommand(commandID))
                {
                    Trace.WriteLine($"    自定义匹配器: [{GetCustomCommandList(commandID).Connect()}]\n");
                }
            }
        }

        static bool ContainsCustomCommand(string commandID)
        {
            return CustomCommandsRegistry.ContainsKey(commandID);
        }

        static List<string> GetCustomCommandList(string commandID)
        {
            if (!ContainsCustomCommand(commandID)) CustomCommandsRegistry[commandID] = new List<string>();
            return CustomCommandsRegistry[commandID];
        }

        [MatchersIgnoreCase("AddCustomCommandMatcher")]
        string AddCustomCommandMatcher(string commandID, string matcher)
        {
            if (_commandInfos.Value.All(info => info.Method.Name != commandID))
            {
                return "找不到这个命令 ID.";
            }
            var list = GetCustomCommandList(commandID);
            if (list.Contains(matcher)) return "请不要重复添加.";
            
            list.Add(matcher);
            SaveConfig();
            return "添加完成.";
        }

        [MatchersIgnoreCase("RemoveCustomCommandMatcher")]
        string RemoveCustomCommandMatcher(string commandID, string matcher)
        {
            if (_commandInfos.Value.All(info => info.Method.Name != commandID))
            {
                return "找不到这样的命令 ID.";
            }
            var list = GetCustomCommandList(commandID);
            if (!list.Contains(matcher))
            {
                return "找不到这样的 Matcher.";
            }

            list.Remove(matcher);
            if (list.IsEmpty()) CustomCommandsRegistry.Remove(commandID);
            SaveConfig();
            return "移除完成.";
        }

        void SaveConfig() => CustomCommandConfig.Save();

        public Action<Message> MessageSender { get; } = (s) =>
        {
            Trace.WriteLine(s);
        };

        public Action<RichMessages> RichMessageSender { get; }
        public Action<Message> ErrorMessageSender { get; } = s => Trace.WriteLine(s);
        public OrichaltContext O { get; }
        public string Message { get; }

        public CustomCommandMatcherHandler(string message)
        {
            Message = message;
        }
    }

    [Configuration("CustomCommands")]
    public class CustomCommandConfig : Configuration<CustomCommandConfig>
    {
        public Dictionary<string, List<string>> CustomCommands { get; set; } = new Dictionary<string, List<string>>();
    }
}
