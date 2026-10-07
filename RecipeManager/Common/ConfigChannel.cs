using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace RecipeManager.Common
{
    /// <summary>
    /// One kind of config file - recipes, pieces, conversions or items. Finds its files, reads and merges them,
    /// sends them to clients and re-applies them when they change. The typed work lives in
    /// <see cref="ConfigChannel{TCollection, TEntry}"/>, this is what the folder watcher dispatches to.
    /// </summary>
    internal abstract class ConfigChannel
    {
        // Saving a file in most editors fires several change events, this waits for them to stop
        private const float SettleSeconds = 0.5f;

        private float lastChange;
        private bool reloadPending;

        /// <summary>For log messages, e.g. "recipe".</summary>
        public abstract string Label { get; }

        /// <summary>The file name a default file is created with, which is also the suffix every file of this kind ends with.</summary>
        public abstract string Suffix { get; }

        /// <summary>Whether a file in the config folder belongs to this channel.</summary>
        public abstract bool Matches(string fileName);

        /// <summary>Reads every file of this kind from disk, without applying anything.</summary>
        public abstract void LoadFromDisk();

        /// <summary>Re-reads the files, re-applies them and sends them to connected clients.</summary>
        public abstract void ReloadFromDisk();

        /// <summary>Called by the folder watcher. Waits for the file to settle, then reloads.</summary>
        public void ScheduleReload()
        {
            lastChange = Time.realtimeSinceStartup;
            if (reloadPending) { return; }
            reloadPending = true;
            ThreadingHelper.Instance.StartCoroutine(ReloadWhenSettled());
        }

        private IEnumerator ReloadWhenSettled()
        {
            while (Time.realtimeSinceStartup - lastChange < SettleSeconds)
            {
                yield return new WaitForSecondsRealtime(SettleSeconds);
            }
            reloadPending = false;
            Logger.LogInfo($"A {Label} file changed, reloading the {Label} configuration.");
            ReloadFromDisk();
        }

        /// <summary>
        /// True for a dedicated server and for a player hosting from their own game. Checking for a headless
        /// instance missed the second case, so clients of a player hosted world never got config changes.
        /// </summary>
        internal static bool IsServerInstance()
        {
            return ZNet.instance != null && ZNet.instance.IsServer();
        }

        /// <summary>In a world hosted by someone else, whose configuration is the one that applies.</summary>
        internal static bool IsConnectedClient()
        {
            return ZNet.instance != null && !ZNet.instance.IsServer();
        }
    }

    internal class ConfigChannel<TCollection, TEntry> : ConfigChannel where TCollection : class
    {
        private readonly string label;
        private readonly string suffix;
        private readonly string[] excluded;
        private readonly string rpcName;
        private readonly Func<TCollection, Dictionary<string, TEntry>> entriesOf;
        private readonly Func<Dictionary<string, TEntry>, TCollection> wrap;
        private readonly Func<Dictionary<string, TEntry>> getEntries;
        private readonly Action<Dictionary<string, TEntry>> setEntries;
        private readonly Action reapply;
        private CustomRPC rpc;

        /// <param name="label">For log messages, e.g. "recipe".</param>
        /// <param name="suffix">Every file whose name ends with this is read, e.g. "Recipes.yaml".</param>
        /// <param name="excluded">Files that end with the suffix but are never read, the reference dumps.</param>
        /// <param name="rpcName">The name the configuration is sent to clients under.</param>
        /// <param name="entriesOf">The entries in a parsed file, which can be null for an empty one.</param>
        /// <param name="wrap">Builds a file's worth of data from entries, for sending and for writing.</param>
        /// <param name="getEntries">The entries currently in use.</param>
        /// <param name="setEntries">Replaces the entries in use, without applying them.</param>
        /// <param name="reapply">Undoes what the previous entries changed and applies the current ones.</param>
        public ConfigChannel(string label, string suffix, string[] excluded, string rpcName,
            Func<TCollection, Dictionary<string, TEntry>> entriesOf, Func<Dictionary<string, TEntry>, TCollection> wrap,
            Func<Dictionary<string, TEntry>> getEntries, Action<Dictionary<string, TEntry>> setEntries, Action reapply)
        {
            this.label = label;
            this.suffix = suffix;
            this.excluded = excluded ?? new string[0];
            this.rpcName = rpcName;
            this.entriesOf = entriesOf;
            this.wrap = wrap;
            this.getEntries = getEntries;
            this.setEntries = setEntries;
            this.reapply = reapply;
        }

        public override string Label => label;
        public override string Suffix => suffix;

        public override bool Matches(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) { return false; }
            fileName = Path.GetFileName(fileName);
            if (!fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { return false; }
            return !excluded.Any(name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Every file of this kind, in name order so entries always merge the same way.</summary>
        public List<string> FindFiles()
        {
            string folder = ValConfig.GetSecondaryConfigDirectoryPath();
            List<string> files = Directory.GetFiles(folder).Where(file => Matches(file)).ToList();
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        /// <summary>
        /// Reads and merges every file of this kind. A file that fails to parse is skipped with an error, an empty
        /// one or an empty entry is skipped, and an entry name used twice keeps its first definition.
        /// </summary>
        public Dictionary<string, TEntry> ReadAll()
        {
            Dictionary<string, TEntry> merged = new Dictionary<string, TEntry>();
            Dictionary<string, string> definedIn = new Dictionary<string, string>();
            foreach (string file in FindFiles())
            {
                if (ValConfig.EnableDebugMode.Value) { Logger.LogDebug($"Loading {label} entries from {file}."); }
                TCollection collection;
                try
                {
                    string text = File.ReadAllText(file);
                    WarnDuplicateNames(text, Path.GetFileName(file));
                    collection = ValConfig.yamldeserializer.Deserialize<TCollection>(text);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"There was an error reading {label} data from {file}, it will not be used. Error: {ex}");
                    continue;
                }
                Merge(collection, Path.GetFileName(file), merged, definedIn);
            }
            if (ValConfig.EnableDebugMode.Value) { CheckRoundTrip(merged); }
            return merged;
        }

        /// <summary>
        /// Within one file, the parser keeps the last of two entries with the same name and says nothing, which also
        /// happens to a whole file whose top level key is written twice. Says so instead.
        /// </summary>
        private void WarnDuplicateNames(string text, string source)
        {
            try
            {
                new YamlStream().Load(new StringReader(text));
            }
            catch (YamlException ex) when (ex.Message.StartsWith("Duplicate key", StringComparison.Ordinal))
            {
                Logger.LogWarning($"{source} uses the name {ex.Message.Substring("Duplicate key".Length).Trim()} twice (line {ex.Start.Line}), only the last one with that name is used. Rename one of them, or remove the extra line if it is the top level key.");
            }
            catch (Exception)
            {
                // Anything else is a syntax error, which reading the file reports properly
            }
        }

        private void Merge(TCollection collection, string source, Dictionary<string, TEntry> merged, Dictionary<string, string> definedIn)
        {
            // An empty file, or one whose entries are all commented out, parses to nothing
            Dictionary<string, TEntry> entries = collection == null ? null : entriesOf(collection);
            if (entries == null)
            {
                Logger.LogDebug($"{source} has no {label} entries.");
                return;
            }
            foreach (KeyValuePair<string, TEntry> entry in entries)
            {
                if (entry.Value == null)
                {
                    Logger.LogWarning($"The {label} entry '{entry.Key}' in {source} is empty, it will be skipped.");
                    continue;
                }
                if (definedIn.TryGetValue(entry.Key, out string firstSource))
                {
                    Logger.LogWarning($"The {label} entry '{entry.Key}' in {source} is already defined in {firstSource}, only the first definition is used. Entry names have to be unique across every file.");
                    continue;
                }
                merged.Add(entry.Key, entry.Value);
                definedIn.Add(entry.Key, source);
            }
        }

        /// <summary>
        /// Clients get the configuration the server serializes, so an entry has to come out of that the same as
        /// it went in. Logs any entry that does not, which means a config field with an initializer it should
        /// not have.
        /// </summary>
        private void CheckRoundTrip(Dictionary<string, TEntry> entries)
        {
            foreach (KeyValuePair<string, TEntry> entry in entries)
            {
                try
                {
                    string sent = ValConfig.syncSerializer.Serialize(entry.Value);
                    string received = ValConfig.syncSerializer.Serialize(ValConfig.yamldeserializer.Deserialize<TEntry>(sent));
                    if (sent != received)
                    {
                        Logger.LogWarning($"The {label} entry '{entry.Key}' would reach clients changed.\nSent:\n{sent}\nReceived:\n{received}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Could not check how the {label} entry '{entry.Key}' reaches clients. Error: {ex.Message}");
                }
            }
        }

        public override void LoadFromDisk()
        {
            setEntries(ReadAll());
            PatchManager.Refresh();
        }

        public override void ReloadFromDisk()
        {
            // The server's configuration is the one in use, and stays until leaving the world
            if (IsConnectedClient())
            {
                Logger.LogInfo($"Not reloading the local {label} files, this game is connected to a server and uses its configuration. Change the files on the server instead.");
                return;
            }
            LoadFromDisk();
            Reapply();
            SendToPeers();
            Logger.LogInfo($"Loaded {getEntries().Count} {label} entr{(getEntries().Count == 1 ? "y" : "ies")}.");
        }

        /// <summary>Undoes what was applied and applies the entries in use. Only possible once the object DB exists.</summary>
        public void Reapply()
        {
            if (ObjectDB.instance == null)
            {
                Logger.LogDebug($"The object DB does not exist yet, the {label} entries will be applied when it does.");
                return;
            }
            reapply();
        }

        public void RegisterRpc()
        {
            rpc = NetworkManager.Instance.AddRPC(rpcName, OnServerReceive, OnClientReceive);
            SynchronizationManager.Instance.AddInitialSynchronization(rpc, BuildPackage);
        }

        /// <summary>Sends the entries in use to every connected client. Does nothing anywhere but on the server.</summary>
        public void SendToPeers()
        {
            if (!IsServerInstance() || rpc == null) { return; }
            if (ZNet.instance.m_peers == null || ZNet.instance.m_peers.Count == 0) { return; }
            try
            {
                rpc.SendPackage(ZNet.instance.m_peers, BuildPackage());
                Logger.LogDebug($"Sent the {label} configuration to {ZNet.instance.m_peers.Count} client(s).");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error while sending the {label} configuration to clients: {ex}");
            }
        }

        private ZPackage BuildPackage()
        {
            ZPackage package = new ZPackage();
            package.Write(ValConfig.syncSerializer.Serialize(wrap(getEntries())));
            return package;
        }

        private IEnumerator OnServerReceive(long sender, ZPackage package)
        {
            if (ValConfig.EnableDebugMode.Value) { Logger.LogInfo($"Server received a {label} configuration from a client, rejecting it since this is the server."); }
            yield return null;
        }

        private IEnumerator OnClientReceive(long sender, ZPackage package)
        {
            Dictionary<string, TEntry> entries = new Dictionary<string, TEntry>();
            try
            {
                TCollection collection = ValConfig.yamldeserializer.Deserialize<TCollection>(package.ReadString());
                Merge(collection, "the server's configuration", entries, new Dictionary<string, string>());
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Received an invalid {label} configuration from the server, it was ignored. Error: {ex.Message}");
                yield break;
            }
            Logger.LogInfo($"Received {entries.Count} {label} entr{(entries.Count == 1 ? "y" : "ies")} from the server.");
            setEntries(entries);
            PatchManager.Refresh();
            Reapply();
            yield return null;
        }

        /// <summary>The entries in use, as the text of a config file.</summary>
        public string Serialize()
        {
            return ValConfig.yamlserializer.Serialize(wrap(getEntries()));
        }
    }
}
