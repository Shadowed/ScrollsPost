using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using JsonFx.Json;

namespace ScrollsPost {
    public class ConfigManager {
        //private ScrollsPost.Mod mod;
        private String configPath;
        private Dictionary<String, object> config;
        private Boolean newInstall;
        private Thread writer;

        public ConfigManager(ScrollsPost.Mod mod) {
            //this.mod = mod;

            // Setup the directory to start with
            String path = mod.OwnFolder() + Path.DirectorySeparatorChar + "config";
            if( !Directory.Exists(path + Path.DirectorySeparatorChar) ) {
                Directory.CreateDirectory(path + Path.DirectorySeparatorChar);
            }

            configPath = String.Format("{0}{1}config{1}{2}.json", mod.OwnFolder(), Path.DirectorySeparatorChar, App.MyProfile.ProfileInfo.id);

            Load();
        }

        private void Load() {
            if( File.Exists(configPath) ) {
                String data = File.ReadAllText(configPath);
                config = new JsonReader().Read<Dictionary<String, object>>(data);

            // Fresh install
            } else {
                config = new Dictionary<String, object>();
                config["sync-notif"] = true;
                config["trade"] = true;
                config["replay"] = "ask";

                newInstall = true;
            }
        }

        private void Write() {
            String data = new JsonWriter().Write(config);
            File.WriteAllText(configPath, data);
        }

        // Allow multiple options to be queued up and then flushed out
        private void DelayedWrite() {
            Thread.Sleep(500);
            Write();
            writer = null;
        }

        // Setters & Getters
        public Boolean NewInstall() {
            return newInstall;
        }

        public Boolean VersionBelow(int version) {
            return config.ContainsKey("conf-version") ? (GetInt("conf-version") < version) : true;
        }

        public object Get(String key) {
            return config[key];
        }

        public int GetInt(String key) {
            return (int)config[key];
        }

        public String GetString(String key) {
            return (String)config[key];
        }

        public Boolean GetBoolean(String key) {
            return (Boolean)config[key];
        }

        public object GetWithDefault(String key, object defValue) {
            if( !config.ContainsKey(key) ) {
                config.Add(key, defValue);
            }

            return config[key];
        }

        public Boolean ContainsKey(String key) {
            return config.ContainsKey(key);
        }

        public void Remove(String key) {
            if( !config.ContainsKey(key) )
                return;

            config.Remove(key);

            if( writer == null ) {
                writer = new Thread(new ThreadStart(DelayedWrite));
                writer.Start();
            }
        }

        public void Add(String key, object value) {
            config[key] = value;

            if( writer == null ) {
                writer = new Thread(new ThreadStart(DelayedWrite));
                writer.Start();
            }
        }
    }
}

