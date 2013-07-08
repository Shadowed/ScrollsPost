using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Net;
using System.IO;
using JsonFx.Json;
using UnityEngine;

namespace ScrollsPost {
    public class ConfigGUI : IOkCancelCallback, IOkStringCancelCallback {
        private ScrollsPost.Mod mod;
        private ConfigManager config;
        private OptionPopups popups;
        private String replayPath;
        //private AccountVerifier verifier;

        public ConfigGUI(ScrollsPost.Mod mod) {
            this.mod = mod;
            this.config = mod.config;
        }

        public void Init() {
            if( popups == null ) {
                popups = new GameObject("OptionPopups").AddComponent<OptionPopups>();
            }
        }

        public void Show() {
            Init();

            List<OptionPopups.ConfigOption> options = new List<OptionPopups.ConfigOption>();
            options.Add(new OptionPopups.ConfigOption("Replay List", "replay-list"));
            options.Add(new OptionPopups.ConfigOption("Replay Uploading", "replay"));
            options.Add(new OptionPopups.ConfigOption("Default Period Price", "period"));
            options.Add(new OptionPopups.ConfigOption("Inline Trade Prices", "trade"));
            options.Add(new OptionPopups.ConfigOption("Collection Notifications", "sync-notif"));
            options.Add(new OptionPopups.ConfigOption("ScrollsPost Account", "account"));

            popups.ShowMultiScrollPopup(this, "main", "ScrollsPost Configuration", "Pick an option to modify", options);
        }

        public void ShowIntro() {
            App.Popups.ShowOkCancel(this, "welcome", "Welcome to ScrollsPost!", "ScrollsPost mod has been installed, you can access the config by typing /sp or /scrollspost.\n\nTo use collection syncing and store management features, you need a ScrollsPost account.\nYou can create one quickly without leaving the game, it only takes a minute.", "Setup Account or Login", "Cancel");
        }

        public void ShowChanges(object ver) {
            int version = (int) ver;
            if( version == 4 ) {
                App.Popups.ShowOk(this, "done", "ScrollsPost v1.0.4", "Mod updated to v1.0.4\n\n1) You can now disable inline trade prices, handy if your computer is older and you experience lag issues.\n2) You can now be notified every time your collection is synced via a chat message.\n3)Initial syncs will always show a message to reduce confusion.", "Done");
            } else if( version == 5 ) {
                App.Popups.ShowOk(this, "show-replay", "ScrollsPost v1.0.5 - Replays!", "ScrollsPost now has full replay support with the ability to view both player hands (when available), fast forward to turns, and automatic upload (when enabled).\n\nClick Configure to setup your preferences for Replays.\nYou can view your replays by typing /sp.", "Configure");
            }
        }

        public void PopupCancel(String type) {
            if( type == "period" ) {
                mod.scrollPrices.Flush();
            }
        }

        public void PopupOk(String type) {
            if( type == "welcome" ) {
                ShowAuthEmail();
            } else if( type == "show-replay" ) {
                Init();
                BuildReplayMenu();
            } else if( type == "show-replay-list" ) {
                BuildReplayListMenu();
            }
        }

        public void PopupOk(String type, String choice) {
            // Main page
            if( type == "main" ) {
                if( choice == "period" ) {
                    BuildPeriodMenu();
                } else if( choice == "account" ) {
                    BuildAccountPopup();
                } else if( choice == "sync-notif" ) {
                    BuildCollectionNotificationMenu();
                } else if( choice == "trade" ) {
                    BuildTradeMenu();
                } else if( choice == "replay" ) {
                    BuildReplayMenu();
                } else if( choice == "replay-list" ) {
                    BuildReplayListMenu();
                }
            } else if( type == "trade" ) {
                config.Add("trade", choice.Equals("True"));
            } else if( type == "sync-notif" ) {
                config.Add("sync-notif", choice.Equals("True"));
            } else if( type == "replay" ) {
                config.Add("replay", choice);
            } else if( type == "period" ) {
                config.Add("data-period", choice);
            } else if( type == "back" ) {
                Show();
            } else if( type == "pick-replay" ) {
                if( choice == "play" ) {
                    if( !String.IsNullOrEmpty(replayPath) ) {
                        this.mod.StartReplayRunner(replayPath);
                    } else {
                        App.Popups.ShowOk(this, "show-replay-list", "No Replay Chosen", "You must click a replay from the list before you can play one.", "Ok");
                    }

                } else if( choice == "upload" ) {
                    if( !String.IsNullOrEmpty(replayPath) ) {
                        UploadReplay(replayPath);
                    } else {
                        App.Popups.ShowOk(this, "show-replay-list", "No Replay Chosen", "You must click a replay from the list before you can upload it.", "Ok");
                    }
                } else if( choice == "play-file" ) {
                    PlayReplayFromFile();
                } else if( choice == "play-url" ) {
                    PlayReplayFromURL();
                } else {
                    replayPath = choice;
                }

            //} else if( type == "verifier" && verifier != null ) {
            //    verifier.ShowExplanation();
            // Registration & Logging in
            } else if( type == "email" ) {
                new Thread(new ParameterizedThreadStart(CheckAccount)).Start(choice);
            } else if( type == "password-new" ) {
                new Thread(new ParameterizedThreadStart(CreateAccount)).Start(choice);
            } else if( type == "password-login" ) {
                new Thread(new ParameterizedThreadStart(AccountLogin)).Start(choice);
            }
        }
        
        // Menu builders
        private void BuildReplayListMenu() {
            List<OptionPopups.ConfigOption> options = new List<OptionPopups.ConfigOption>();

            String[] files = Directory.GetFiles(this.mod.replayLogger.replayFolder, "*.spr");
            Array.Sort(files);
            Array.Reverse(files);

            foreach( String path in files ) {
                using( StreamReader sr = new StreamReader(path) ) {
                    String line = sr.ReadLine();
                    if( line.StartsWith("metadata") ) {
                        line = line.Split(new char[] { '|' }, 2)[1];
                        Dictionary<String, object> metadata = new JsonReader().Read<Dictionary<String, object>>(line);

                        DateTime played = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        played = played.AddSeconds(Convert.ToDouble(metadata["played-at"]));
                        played = TimeZone.CurrentTimeZone.ToLocalTime(played);

                        metadata[metadata["perspective"] + "-name"] = "[VP] " + metadata[metadata["perspective"] + "-name"];

                        String label = String.Format("{0} {1} - {2}\n{3} vs {4}", played.ToShortDateString(), played.ToShortTimeString(), metadata["deck"], metadata["white-name"], metadata["black-name"]);
                        options.Add(new OptionPopups.ConfigOption(label, path, path.Equals(replayPath)));
                    }
                }
            }

            popups.ShowReplayScrollPopup(this, "pick-replay", "Select Replay", "Pick a replay to watch, share or delete.", options);
        }

        private void BuildReplayMenu() {
            OptionPopups.ConfigOption[] options = new OptionPopups.ConfigOption[] {
                new OptionPopups.ConfigOption("Ask After Matches", "ask"),
                new OptionPopups.ConfigOption("Automatically Upload", "auto"),
                new OptionPopups.ConfigOption("Don't Upload", "never")
            };

            popups.ShowMultiScrollPopup(this, "replay", "Select Replay Mode", "Configures how ScrollsPost should handle your replays.", SetupOptions((String) config.GetWithDefault("replay", (object) "ask"), options));
        }

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

        private void BuildCollectionNotificationMenu() {
            OptionPopups.ConfigOption[] options = new OptionPopups.ConfigOption[] {
                new OptionPopups.ConfigOption("Enable", true),
                new OptionPopups.ConfigOption("Disable", false)
            };

            popups.ShowMultiScrollPopup(this, "sync-notif", "Collection Sync Notifications", "Whether you want to be notified every time your collection has been updated on ScrollsPost.com", SetupOptions(config.GetBoolean("sync-notif"), options));
        }

        private void BuildAccountPopup() {
            if( config.ContainsKey("user-id") && config.ContainsKey("api-key") ) {
                App.Popups.ShowOk(this, "back", "ScrollsPost Account", "You are already logged into ScrollsPost, you don't need to do anything else.\n\nYour collections will automatically sync to ScrollsPost now.", "Back");
            } else {
                ShowAuthEmail();   
            }
        }

        private void BuildTradeMenu() {
            OptionPopups.ConfigOption[] options = new OptionPopups.ConfigOption[] {
                new OptionPopups.ConfigOption("Enable", true),
                new OptionPopups.ConfigOption("Disable", false)
            };

            popups.ShowMultiScrollPopup(this, "trade", "Inline Trade Prices", "Whether you want to see item prices inline in the trade window.", SetupOptions(config.GetBoolean("trade"), options));
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

        // Replay management
        public void UploadReplay(String path) {

        }

        public void PlayReplayFromFile() {
            String path = this.mod.OpenFileDialog();
            if( path.EndsWith(".sgr") ) {
                App.Popups.ShowOk(this, "show-replay-list", "Not ScrollsPost Replay", "You cannot play an .sgr replay with this mod without converting it first, contact us at support@scrollspost.com and will help you out.", "Ok");
            } else if( !path.EndsWith(".spr") ) {
                App.Popups.ShowOk(this, "show-replay-list", "Not ScrollsPost Replay", "A valid ScrollsPost replay will end with .spr, this is not a valid replay.", "Ok");
            } else {
                this.mod.StartReplayRunner(path);
            }
        }

        public void PlayReplayFromURL() {

        }

        // Authentication flow
        // Create a new account
        public void CreateAccount(object password) {
            App.Popups.ShowInfo("ScrollsPost Registration", "Registering, this will only take a second...");

            WebClient wc = new WebClient();

            try {
                NameValueCollection form = new NameValueCollection();
                form["email"] = config.GetString("email");
                form["password"] = (String) password;
                form["id"] = App.MyProfile.ProfileInfo.id;
                form["uid"] = App.MyProfile.ProfileInfo.userUuid;
                form["name"] = App.MyProfile.ProfileInfo.name;

                byte[] bytes = wc.UploadValues(new Uri(mod.apiURL + "/v1/user"), "POST", form);
                Dictionary<String, object> result = new JsonReader().Read<Dictionary<String, object>>(Encoding.UTF8.GetString(bytes));

                // Failed to register
                if( result.ContainsKey("errors") ) {
                    Dictionary<String, object> errors = (Dictionary<String, object>) result["errors"];
                    if( errors.ContainsKey("password") ) {
                        ShowAuthPassword(true, "<color=red>Password " + ((String[]) errors["password"])[0] + "</color>");
                    } else if( errors.ContainsKey("email") ) {
                        ShowAuthEmail("<color=red>Email " + ((String[]) errors["email"])[0] + "</color>");
                    }

                 // Save our keys so we don't store passwords
                 } else {
                    App.Popups.ShowOk(this, "done", "Registered!", String.Format("You now have an account with the email {0} on ScrollsPost.com!\n\nYou can login at any time on ScrollsPost.com to manage your card collection as well as set cards for sale or find cards to buy.\n\nYour collection will automatically sync to ScrollsPost now, will let you know when the initial sync has finished in-game.", config.GetString("email")), "Ok");

                    config.Add("user-id", (String) result["user_id"]);
                    config.Add("api-key", (String) result["api_key"]);
                    config.Remove("email");

                    if( result.ContainsKey("verif_key") ) {
                        config.Add("verif-key", (String) result["verif_key"]);
                        //verifier = new AccountVerifier(config);
                    }

                    App.Communicator.sendRequest(new LibraryViewMessage());
                }

            } catch ( WebException we ) {
                App.Popups.ShowOk(this, "fail", "HTTP Error", "Unable to register due to an HTTP error.\nContact support@scrollspost.com for help.\n\n" + we.Message, "Ok");
                mod.WriteLog("Failed to register", we);
            }
        }

        // Logging an account in
        public void AccountLogin(object password) {
            App.Popups.ShowInfo("ScrollsPost Login", "Logging in, this will only take a second...");

            WebClient wc = new WebClient();

            try {
                NameValueCollection form = new NameValueCollection();
                form["email"] = config.GetString("email");
                form["password"] = (String) password;
                form["id"] = App.MyProfile.ProfileInfo.id;
                form["uid"] = App.MyProfile.ProfileInfo.userUuid;
                form["name"] = App.MyProfile.ProfileInfo.name;

                byte[] bytes = wc.UploadValues(new Uri(mod.apiURL + "/v1/user"), "PUT", form);
                Dictionary<String, object> result = new JsonReader().Read<Dictionary<String, object>>(Encoding.UTF8.GetString(bytes));

                // Failed to login
                if( result.ContainsKey("errors") ) {
                    Dictionary<String, object> errors = (Dictionary<String, object>) result["errors"];
                    if( errors.ContainsKey("password") ) {
                        ShowAuthPassword(false, "<color=red>Password " + ((String[]) errors["password"])[0] + "</color>");
                    } else if( errors.ContainsKey("email") ) {
                        ShowAuthEmail("<color=red>Email " + ((String[]) errors["email"])[0] + "</color>");
                    }

                    // Save our keys so we don't store passwords
                } else {
                    App.Popups.ShowOk(this, "done", "Logged In!", "You're now logged into your ScrollsPost.com account!\n\nYour collection will automatically sync to ScrollsPost now, will let you know when the initial sync has finished in-game.", "Ok");

                    config.Add("user-id", (String) result["user_id"]);
                    config.Add("api-key", (String) result["api_key"]);
                    config.Remove("email");
                    config.Remove("last-card-sync");

                    if( result.ContainsKey("verif_key") ) {
                        config.Add("verif-key", (String) result["verif_key"]);
                    }

                    //if( config.ContainsKey("verif-key") ) {
                    //    verifier = new AccountVerifier(config);
                    //}

                    App.Communicator.sendRequest(new LibraryViewMessage());
                }

            } catch ( WebException we ) {
                App.Popups.ShowOk(this, "fail", "HTTP Error", "Unable to login due to an HTTP error.\nContact support@scrollspost.com for help.\n\n" + we.Message, "Ok");
                mod.WriteLog("Failed to register", we);
            }
        }

        // Figure out if it's a login or registration
        public void CheckAccount(object email) {
            App.Popups.ShowInfo(config.ContainsKey("user-id") ? "ScrollsPost Login" : "ScrollsPost Login / Registration", "Checking if the account exists...");

            config.Add("email", (String) email);

            WebClient wc = new WebClient();

            try {
                NameValueCollection form = new NameValueCollection();
                form["email"] =(String)  email;

                byte[] bytes = wc.UploadValues(new Uri(mod.apiURL + "/v1/user/exists"), "POST", form);
                String result = Encoding.UTF8.GetString(bytes);

                if( result.Equals("2") ) {
                    ShowAuthEmail("<color=red>Invalid email entered. Please make sure it's valid.</color>");
                } else {
                    ShowAuthPassword(result.Equals("0"));
                }

            } catch ( WebException we ) {
                App.Popups.ShowOk(this, "fail", "HTTP Error", "Unable to check if the account exists due to an HTTP error.\nContact support@scrollspost.com for help.\n\n" + we.Message, "Ok");
                mod.WriteLog("Failed to load account data", we);
            }
        }

        // Prompts
        public void ShowAuthPrompt() {
            App.Popups.ShowOkCancel(this, "welcome", "ScrollsPost v1.0.3", "We've added collection syncing and store management features to ScrollsPost.com. Giving you the ability to quickly put up cards for sale, or find cards to buy.\n\nA ScrollsPost account is required to do this, and can be done without leaving the game. It will only take a minute.", "Setup Account or Login", "Cancel");
        }

        public void ShowAuthEmail(String error=null) {
            App.Popups.ShowTextInput(this, (String) config.GetWithDefault("email", ""), String.IsNullOrEmpty(error) ? "This can be different from your Scrolls login." : error, "email", "ScrollsPost Login / Registration", "Enter Your Email:", "Go");
        }

        public void ShowAuthPassword(Boolean newAccount, String error=null) {
            if( newAccount ) {
                App.Popups.ShowTextInput(this, "", String.IsNullOrEmpty(error) ? "<color=red>Pick a different password from your Scrolls login!</color>" : error, "password-new", "ScrollsPost Registration", "Enter a password:", "Register");
            } else {
                App.Popups.ShowTextInput(this, "", String.IsNullOrEmpty(error) ? "Enter your ScrollsPost.com account password" : error, "password-login", "ScrollsPost Login", "ScrollsPost Password:", "Login");
            }
        }
    }
}

