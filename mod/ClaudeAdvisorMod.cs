using System;
using ICities;
using UnityEngine;

namespace ClaudeAdvisor
{
    public class ClaudeAdvisorMod : IUserMod
    {
        public string Name { get { return "Claude City Advisor MCP"; } }
        public string Description { get { return "AI companion for Cities Skylines — connects to Claude Code via MCP for real-time city management"; } }
    }

    public class ClaudeAdvisorLoading : LoadingExtensionBase
    {
        private static GameObject _gameObject;

        public override void OnLevelLoaded(LoadMode mode)
        {
            if (mode == LoadMode.LoadGame || mode == LoadMode.NewGame)
            {
                _gameObject = new GameObject("ClaudeAdvisorMCP");
                _gameObject.AddComponent<HttpCommandServer>();
                Debug.Log("[ClaudeAdvisor] MCP Advisor loaded! HTTP server starting on port 7828...");
            }
        }

        public override void OnLevelUnloading()
        {
            if (_gameObject != null)
            {
                var server = _gameObject.GetComponent<HttpCommandServer>();
                if (server != null) server.StopServer();
                UnityEngine.Object.Destroy(_gameObject);
                _gameObject = null;
                Debug.Log("[ClaudeAdvisor] MCP Advisor unloaded.");
            }
        }
    }
}
