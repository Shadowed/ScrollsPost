using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
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
            App.Popups.ShowOkCancel(this, "welcome", "Welcome to ScrollsPost", "* You can access config through /sp or /scrollspost.\n* To use collection syncing, setup an account through /sp -> ScrollsPost Account.\n* You can manage the replay upload settings through /sp -> Replay Uploading\n* Questions, issues or comments, let us know at support@scrollspost.com", "Replay Configuration", "Done");
        }

        public void ShowChanges(object ver) {
            int version = (int) ver;
            if( version == 4 ) {
                App.Popups.ShowOk(this, "done", "ScrollsPost v1.0.4", "Mod updated to v1.0.4\n\n1) You can now disable inline trade prices, handy if your computer is older and you experience lag issues.\n2) You can now be notified every time your collection is synced via a chat message.\n3)Initial syncs will always show a message to reduce confusion.", "Done");
            } else if( version == 5 ) {
                App.Popups.ShowOk(this, "show-replay", "ScrollsPost v1.0.5 - Replays!", "ScrollsPost now has full replay support with the ability to view both player hands (when available), fast forward to turns, and automatic upload (when enabled).\n\nClick Configure to setup your preferences for Replays.\nYou can view your replays by typing /sp.", "Configure");
            } else if( version == 6 ) {
                Thread.Sleep(1000);
                App.Popups.ShowScrollText(this, "done", "ScrollsPost v1.0.6 - Fixes", "* This popup will no longer keep showing up each time you login\n\n* Instant seeking! Replay seeking is now instant no matter what round you want to go to\n\n* Improved the replay list with sorting by date + increased size\n\n* Improved viewpoint handling, if we have both viewpoints available, [VP] shows up next to both names the replay shows up once\n\n* Improved replay searching for multi-hand replays, now always checks your replay folder for the other viewpoint even when playing from a file\n\n* Improved replay controls, uses slower/normal/faster for speed instead of confusing percentages\n\n* Real time replays are now disabled by default, capped at 1.1 seconds per turn. You can enable them through replay controls\n\n* Added colored player names to make the replay list easier to read\n\n* Added replay uploading through the replay list, with a tag to show replays you haven't uploaded yet\n\n* Added replay playing by URL, automatically downloads both viewpoints when available to view multi-hand replays\n\n* Fixed replay controls covering the resource #s under certain screen resolutions\n\n* Fixed disconnects causing the replay logger to break\n\n* Fixed the initial load in of a replay looking buggy\n\n* Fixed collection syncing being messed up by trading (when it's enabled)", "Done");
            } else if( version == 8 ) {
                App.Popups.ShowOk(this, "done", "ScrollsPost v1.0.7 - Fixes", "This is a quick release to get ScrollsPost working with the new Summoner update.\n\nInstant skip to turn is not back yet, but it will be soon. You might have issues playing old replays, which will also be fixed soon.\nIf you run into any new issues, let me know at shadow@scrollspost.com", "Done");
            } else if( version == 9 ) {
                App.Popups.ShowOk(this, "done", "ScrollsPost v1.0.9", "* Instant seeking and speed controls are back!\n* You can now play replays from older versions of Scrolls\n* You can now play ScrollsGuide replays (either by file or ScrollsGuide URL) including pre-0.96 replays", "Done");
            }
        }

        public void PopupCancel(String type) {
            if( type == "period" ) {
                mod.scrollPrices.Flush();
            }
        }

        public void PopupOk(String type) {
            if( type == "welcome" || type == "show-replay" ) {
                Init();
                BuildReplayMenu();
            } else if( type == "show-replay-list" ) {
                BuildReplayListMenu();
            } else if( type == "play-replay" ) {
                this.mod.StartReplayRunner(replayPath);
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
            } else if( type == "show-replay-list" ) {
                BuildReplayListMenu();
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
            } else if( type == "replay-url" ) {
                new Thread(new ParameterizedThreadStart(PlayReplayFromURL)).Start(choice);
            } else if( type == "pick-replay" ) {
                if( choice == "play" ) {
                    if( !String.IsNullOrEmpty(replayPath) ) {
                        this.mod.StartReplayRunner(replayPath);
                    } else {
                        App.Popups.ShowOk(this, "show-replay-list", "No Replay Chosen", "You must click a replay from the list before you can play one.", "Ok");
                    }

                } else if( choice == "upload" ) {
                    if( !String.IsNullOrEmpty(replayPath) ) {
                        new Thread(new ParameterizedThreadStart(UploadReplay)).Start(replayPath);
                    } else {
                        App.Popups.ShowOk(this, "show-replay-list", "No Replay Chosen", "You must click a replay from the list before you can upload it.", "Ok");
                    }
                } else if( choice == "play-file" ) {
                    PlayReplayFromFile();
                } else if( choice == "play-url" ) {
                    App.Popups.ShowTextInput(this, "", "Can be any ScrollsPost or ScrollsGuide URL. Both perspectives will be downloaded if available.", "replay-url", "Replay URL", "Enter an URL:", "View");
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

            // Figure out what games we have multi perspectives for
            Dictionary<String, Boolean> gameIDs = new Dictionary<String, Boolean>();
            foreach( String path in files ) {
                String name = Path.GetFileNameWithoutExtension(path);
                name = name.Split(new char[] { '-' }, 2)[0];
                if( gameIDs.ContainsKey(name) ) {
                    gameIDs[name] = true;
                } else {
                    gameIDs[name] = false;
                }
            }

            // Figure out what has been uploaded already
            Dictionary<String, Boolean> notUploaded = new Dictionary<String, Boolean>();
            if( File.Exists(mod.replayLogger.uploadCachePath) ) {
                using( StreamReader sr = new StreamReader(mod.replayLogger.uploadCachePath) ) {
                    while( sr.Peek() > 0 ) {
                        notUploaded[sr.ReadLine()] = true;
                    }
                }
            }

            foreach( String path in files ) {
                using( StreamReader sr = new StreamReader(path) ) {
                    String line = sr.ReadLine();
                    if( line.StartsWith("metadata") ) {
                        line = line.Split(new char[] { '|' }, 2)[1];
                        String filename = Path.GetFileName(path);

                        Dictionary<String, object> metadata = new JsonReader().Read<Dictionary<String, object>>(line);
                        // If we have a multi viewpoint replay, only show the white one as that's the person who plays first
                        Boolean status = gameIDs[(String)metadata["game-id"]];
                        if( status == true && !metadata["perspective"].Equals("white") ) {
                            continue;
                        }

                        DateTime played = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        played = played.AddSeconds(Convert.ToDouble(metadata["played-at"]));
                        played = TimeZone.CurrentTimeZone.ToLocalTime(played);

                        metadata["white-name"] = "<color=#fde50d>" + metadata["white-name"] + "</color>";
                        metadata["black-name"] = "<color=#fde50d>" + metadata["black-name"] + "</color>";

                        if( status == true ) {
                            metadata["white-name"] = "[VP] <color=#fde50d>" + metadata["white-name"] + "</color>";
                            metadata["black-name"] = "[VP] <color=#fde50d>" + metadata["black-name"] + "</color>";
                        } else {
                            metadata[metadata["perspective"] + "-name"] = "[VP] " + metadata[metadata["perspective"] + "-name"];
                        }

                        String deck;
                        if( notUploaded.ContainsKey(filename) ) {
                            deck = "<color=#D61320>Not Uploaded</color>";
                        } else {
                            deck = (String) metadata["deck"];
                        }

                        String label = String.Format("{0} {1} - {2}\n{3} vs {4}", played.ToShortDateString(), played.ToShortTimeString(), deck, metadata["white-name"], metadata["black-name"]);
                        OptionPopups.ConfigOption option = new OptionPopups.ConfigOption(label, path, path.Equals(replayPath));

                        options.Add(option);
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
        public void UploadReplay(object path) {
            String replayPath = (String)path;

            App.Popups.ShowInfo("ScrollsPost Replay", "We're uploading your replay, this will only take a minute.");
            Dictionary<String, object> response = mod.replayLogger.Upload(replayPath);

            if( response.ContainsKey("url") ) {
                App.Popups.ShowTextInput(this, (response["url"] as String).Replace("scrollspost/", "scrollspost.com/"), "", "show-replay-list", "Replay Uploaded!", "URL:", "Done");
            } else if( response["error"].Equals("game_too_short") ) {
                App.Popups.ShowOk(this, "show-replay-list", "Replay Failed", "We were unable to upload the replay as it was too short and didn't have enough rounds played.", "Ok");
            } else {
                App.Popups.ShowOk(this, "show-replay-list", "Replay Failed", String.Format("We were unable to upload the replay due to error {0}", response["error"]), "Ok");
            }
        }

        public void PlayReplayFromFile() {
            String path = this.mod.OpenFileDialog();
            if( !path.EndsWith(".sgr") && !path.EndsWith(".spr") ) {
                App.Popups.ShowOk(this, "show-replay-list", "Not A Replay", "A valid replay will end with .spr or .sgr.", "Ok");
            } else {
                this.mod.StartReplayRunner(path);
            }
        }

        private void PlayScrollsGuideFromURL(String url) {
            Match match = Regex.Match(url, "r/([0-9]+)", RegexOptions.IgnoreCase);
            if( !match.Success ) {
                App.Popups.ShowTextInput(this, url, "<color=red>Invalid Replay URL</color>", "reply-url", "ScrollsGuide Replay URL", "Enter an URL:", "View");
                return;
            }

            int gameID = Convert.ToInt32(match.Groups[1].Value);
            String path = String.Format("{0}{1}{2}.sgr", mod.replayLogger.replayFolder, Path.DirectorySeparatorChar, gameID);
            int found = 0;

            try {
                WebClient wc = new WebClient();
                wc.DownloadFile(new Uri(String.Format("http://a.scrollsguide.com/replay/download/{0}", gameID)), path);

                if( File.Exists(path) ) {
                    found += 1;
                    replayPath = path;
                }

            } catch ( WebException we ) {
                Console.WriteLine("***** WEBEXCEPTION {0}", we.ToString());
                if( File.Exists(path) ) {
                    File.Delete(path);
                }
            }

            if( found == 0 ) {
                App.Popups.ShowOk(this, "show-replay-list", "No Replay Found", "Sorry, we couldn't find any replays using the given URL.", "Ok");
            } else if( found == 1 ) {
                App.Popups.ShowOkCancel(this, "play-replay", "Found Replay", "We downloaded the replay, but ScrollsGuide replays don't support multi perspective. Click 'Play Replay' to view.", "Play Replay", "Cancel");
            }
        }

        public void PlayReplayFromURL(object arg) {
            String text = (String)arg;
            if( text.Contains("scrollsguide.com") ) {
                PlayScrollsGuideFromURL(text);
                return;
            }

            Match match = Regex.Match(text, "replay/([0-9]+)-(0|1)/|([0-9]+)-(0|1).spr|([0-9+])-(0|1)", RegexOptions.IgnoreCase);
            if( !match.Success ) {
                App.Popups.ShowTextInput(this, text, "<color=red>Invalid Replay URL</color>", "reply-url", "ScrollsPost Replay URL", "Enter an URL:", "View");
                return;
            }

            App.Popups.ShowInfo("ScrollsPost Replay", "Searching for replay, this may take a minute...");

            int gameID = Convert.ToInt32(match.Groups[1].Value);
            //int perspective = Convert.ToInt32(match.Groups[2].Value);

            var urls = new String[] {
                String.Format("http://www.scrollspost.com/replay/download/{0}-1.spr", gameID),
                String.Format("http://www.scrollspost.com/replay/download/{0}-0.spr", gameID)
            };
            int found = 0;

            foreach( String url in urls ) {
                String path = String.Format("{0}{1}{2}-{3}.spr", mod.replayLogger.replayFolder, Path.DirectorySeparatorChar, gameID, url.EndsWith("-0.spr") ? "white" : "black");

                try {
                    WebClient wc = new WebClient();
                    wc.DownloadFile(new Uri(url), path);

                    if( File.Exists(path) ) {
                        found += 1;
                        replayPath = path;
                    }

                } catch ( WebException we ) {
                    Console.WriteLine("***** WEBEXCEPTION {0}", we.ToString());
                    if( File.Exists(path) ) {
                        File.Delete(path);
                    }
                }
            }

            if( found == 0 ) {
                App.Popups.ShowOk(this, "show-replay-list", "No Replay Found", "Sorry, we couldn't find any replays using the given URL.", "Ok");
            } else if( found == 1 ) {
                App.Popups.ShowOkCancel(this, "play-replay", "Found Replay", "We downloaded the replay, but only one viewpoint is available  Click 'Play Replay' to view.", "Play Replay", "Cancel");
            } else if( found == 2 ) {
                App.Popups.ShowOkCancel(this, "play-replay", "Found Replay", "We downloaded the replay and both viewpoints were available! Click 'Play Replay' to view.", "Play Replay", "Cancel");
            }
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

