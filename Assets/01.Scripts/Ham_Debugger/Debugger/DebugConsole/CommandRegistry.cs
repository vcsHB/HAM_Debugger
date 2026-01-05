using System;
using System.Collections.Generic;
using System.Linq;

namespace HAM_DeBugger.Core.Debugging.DebugConsole
{
    /// <summary>
    /// Command Registry 
    /// </summary>
    public class CommandRegistry
    {
        private Dictionary<string, Command> _top = new Dictionary<string, Command>();

        // public void Register(Command cmd)
        // {
        //     _top[cmd.Name] = cmd;
        // }


        public void RegisterByPath(string path, Command cmd)
        {
            if (string.IsNullOrEmpty(path) || cmd == null) return;

            var parts = path.Split(new[] { '/', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.ToLower()).ToArray();

            if (parts.Length == 0) return;

            // top-level
            var topName = parts[0];
            if (parts.Length == 1)
            {
                // 단일 토큰이면 그냥 등록
                _top[topName] = cmd;
                return;
            }

            // 다중 토큰이면 group을 확보하고 마지막에 sub 등록
            // 1) top group 존재 여부 확인
            if (!_top.TryGetValue(topName, out var topCmd) || !(topCmd is GroupCommand))
            {
                // top이 없거나 GroupCommand가 아니면 동적 그룹 생성
                var dynGroup = new DynamicGroupCommand(topName, topName + " group");
                _top[topName] = dynGroup;
                topCmd = dynGroup;
            }

            var currentGroup = (GroupCommand)topCmd;

            // 중간 파트 처리(두번째부터 끝-1까지 그룹으로 확보)
            for (int i = 1; i < parts.Length - 1; i++)
            {
                var subName = parts[i];
                // GroupCommand에서 subCommands 접근 용도의 helper 필요. 
                // 여기선 GroupCommand에 TryGetSub, EnsureGroupSub 같은 공개 함수가 있다고 가정하고 사용.
                var next = currentGroup.GetSubCommand(subName);
                if (next == null)
                {
                    var newGroup = new DynamicGroupCommand(subName, subName + " group");
                    currentGroup.RegisterSub(newGroup);
                    currentGroup = newGroup;
                }
                else if (next is GroupCommand nextGroup)
                {
                    currentGroup = nextGroup;
                }
                else
                {
                    // 이미 존재하는데 일반 커맨드면 덮어쓰지 않음(충돌)
                    // 대처: 덮어쓰기 하거나 에러 로그
                    // 여기선 덮어쓰지 않고 반환
                    return;
                }
            }

            // 마지막은 실제 커맨드 이름
            var lastName = parts[parts.Length - 1];
            currentGroup.RegisterSub(cmd);
        }

        public Dictionary<string, Command> GetAllCommands()
        {
            return _top;
        }

        public IEnumerable<string> GetAllNames()
        {
            // Return top-level names plus group subnames as suggestions
            var list = new List<string>();
            foreach (var kv in _top)
            {
                list.Add(kv.Key);
                list.AddRange(kv.Value.GetAllNames());
            }
            return list.Distinct();
        }

        public string[] GetSuggestionsForInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();
            var tokens = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var suggestions = new List<string>();

            if (tokens.Length == 1)
            {
                // top-level suggestions
                foreach (var name in _top.Keys)
                    if (name.StartsWith(tokens[0].ToLower()))
                        suggestions.Add(name);
            }
            else if (tokens.Length == 2)
            {
                // subcommands if top exists
                var topKey = tokens[0].ToLower();
                if (_top.TryGetValue(topKey, out var cmd))
                {
                    var subs = cmd.GetAutoCompleteCandidates(tokens, 1);
                    suggestions.AddRange(subs.Where(s => s.StartsWith(tokens[1].ToLower())));
                }
            }
            return suggestions.ToArray();
        }


        public string GetTopLevelMatch(string tokenPrefix)
        {
            tokenPrefix = (tokenPrefix ?? "").ToLower();
            return _top.Keys.FirstOrDefault(k => k.StartsWith(tokenPrefix));
        }

        public string GetSubMatch(string topToken, string subPrefix)
        {
            topToken = topToken?.ToLower();
            subPrefix = subPrefix?.ToLower();
            if (string.IsNullOrEmpty(topToken) || string.IsNullOrEmpty(subPrefix)) return null;
            if (!_top.TryGetValue(topToken, out var cmd)) return null;
            var subs = cmd.GetAutoCompleteCandidates(new string[] { topToken }, 1);
            return subs.FirstOrDefault(s => s.StartsWith(subPrefix));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tokens">Command text data</param>
        /// <param name="errorMessage">If there is an error, the return string corresponding to it</param>
        /// <returns></returns>
        public bool TryExecute(string[] tokens, out string errorMessage)
        {
            // tokens: ["set","timescale","1"]
            for (int i = tokens.Length; i > 0; i--)
            {
                var path = string.Join("/", tokens.Take(i));
                if (_top.TryGetValue(path, out var cmd))
                {
                    // args = tokens.Skip(i).ToArray();
                    return cmd.TryExecute(tokens, i - 1, out errorMessage); // 또는 cmd.TryExecute(args,...)
                }
            }
            errorMessage = "Unknown command";
            return false;
        }

    }
}