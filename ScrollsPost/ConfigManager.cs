using System;
using System.IO;
using System.Collections.Generic;
using JsonFx.Json;

namespace ScrollsPost {
    public class ConfigManager {
        private ScrollsPost.Mod mod;
        private Dictionary<String, object> config;

        public ConfigManager(ScrollsPost.Mod mod) {
            this.mod = mod;

            // Setup the directory to start with
            String path = mod.OwnFolder() + Path.DirectorySeparatorChar + "config";
            if( !Directory.Exists(path + Path.DirectorySeparatorChar) ) {
                Directory.CreateDirectory(path + Path.DirectorySeparatorChar);
            }

            Load();
        }

        public String path(String file) {
            return String.Format("{0}/config/{1}/{2}", mod.OwnFolder(), Path.DirectorySeparatorChar, file);
        }

        private void Load() {
            if( File.Exists(path("config.json")) ) {
                String data = File.ReadAllText(path("config.json"));
                config = new JsonReader().Read<Dictionary<String, object>>(data);
            } else {
                config = new Dictionary<String, object>();
            }
        }

        private void Write() {
            String data = new JsonWriter().Write(config);
            File.WriteAllText(path("config.json"), data);
        }

        // Setters & Getters
        public object Get(String key) {
            return config[key];
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

        public void Add(String key, object value) {
            if( config.ContainsKey(key) ) {
                config[key] = value;
            } else {
                config.Add(key, value);
            }

            // Should be buffered/async some point
            Write();
        }
    }
}

