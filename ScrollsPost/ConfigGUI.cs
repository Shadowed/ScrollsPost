using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ScrollsPost {
    public class ConfigGUI : IOkCancelCallback, IOkStringCancelCallback {
        private ScrollsPost.Mod mod;
        private ConfigManager config;
        private OptionPopups popups;

        public ConfigGUI(ScrollsPost.Mod mod) {
            this.mod = mod;
            this.config = mod.config;
        }

        public void Show() {
            if( popups == null ) {
                popups = new GameObject("OptionPopups").AddComponent<OptionPopups>();
            }

            List<OptionPopups.ConfigOption> options = new List<OptionPopups.ConfigOption>();
            options.Add(new OptionPopups.ConfigOption("Default Period Price", "period"));

            popups.ShowMultiScrollPopup(this, "main", "ScrollsPost Configuration", "Pick an option to modify", options);
        }

        public void ShowIntro() {
            App.Popups.ShowOkCancel(this, "welcome", "Welcome to ScrollsPost!", "ScrollsPost mod has been installed, you can access the config by typing /sp or /scrollspost.\n\nPrice checking can be activated through /pc <name>.\n\nIf you have any issues, contact us at support@scrollspost.com", "View Config", "Cancel");
        }

        public void PopupCancel(String type) {
            if( type == "period" ) {
                mod.scrollPrices.Flush();
            }
        }

        public void PopupOk(String type) {
            if( type == "welcome" ) {
                Show();
            }
        }

        public void PopupOk(String type, String choice) {
            // Main page
            if( type == "main" ) {
                if( choice == "period" ) {
                    BuildPeriodMenu();
                }
            
            // Period config
            } else if( type == "period" ) {
                config.Add("data-period", choice);
            
            // Go back to the main back
            } else if( type == "back" && choice == "back" ) {
                Show();
            }
        }

        // Menu builders
        private void BuildPeriodMenu() {
            OptionPopups.ConfigOption[] options = new OptionPopups.ConfigOption[] {
                new OptionPopups.ConfigOption("Last Hour", "1-hour"),
                new OptionPopups.ConfigOption("Last 24 Hours", "1-day"),
                new OptionPopups.ConfigOption("Last 3 Days", "3-days"),
                new OptionPopups.ConfigOption("Last 7 Days", "7-days"),
                new OptionPopups.ConfigOption("Last 14 Days", "14-days"),
                new OptionPopups.ConfigOption("Last 30 Days", "30-days"),
            };

            popups.ShowMultiScrollPopup(this, "period", "Select Period Type", "What time period to use for prices on the trade window.", SetupOptions((String) config.GetWithDefault("data-period", (object) "1-day"), options));
        }

        // Helpers
        public List<OptionPopups.ConfigOption> SetupOptions(object selected, OptionPopups.ConfigOption[] options) {
            List<OptionPopups.ConfigOption> list = new List<OptionPopups.ConfigOption>();

            foreach( OptionPopups.ConfigOption option in options ) {
                option.enabled = option.key.Equals(selected);
                list.Add(option);
            }


            return list;
        }
    }
}

