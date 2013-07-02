using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Net;
using JsonFx.Json;
using UnityEngine;

namespace ScrollsPost {
    public class ConfigGUI : IOkCancelCallback, IOkStringCancelCallback {
        private ScrollsPost.Mod mod;
        private ConfigManager config;
        private OptionPopups popups;
        private AccountVerifier verifier;

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
            options.Add(new OptionPopups.ConfigOption("ScrollsPost Account", "account"));

            popups.ShowMultiScrollPopup(this, "main", "ScrollsPost Configuration", "Pick an option to modify", options);
        }

        public void ShowIntro() {
            App.Popups.ShowOkCancel(this, "welcome", "Welcome to ScrollsPost!", "ScrollsPost mod has been installed, you can access the config by typing /sp or /scrollspost.\n\nTo use collection syncing and store management features, you need a ScrollsPost account.\nYou can create one quickly without leaving the game, it only takes a minute.", "Setup Account or Login", "Cancel");
        }

        public void PopupCancel(String type) {
            if( type == "period" ) {
                mod.scrollPrices.Flush();
            }
        }

        public void PopupOk(String type) {
            if( type == "welcome" ) {
                ShowAuthEmail();
            }
        }

        public void PopupOk(String type, String choice) {
            // Main page
            if( type == "main" ) {
                if( choice == "period" ) {
                    BuildPeriodMenu();
                } else if( choice == "account" ) {
                    BuildAccountPopup();
                }
            
                // Period config
            } else if( type == "period" ) {
                config.Add("data-period", choice);
            
                // Go back to the main menu
            } else if( type == "back" ) {
                Show();
            
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

        private void BuildAccountPopup() {
            if( config.ContainsKey("user-id") && config.ContainsKey("api-key") ) {
                App.Popups.ShowOk(this, "back", "ScrollsPost Account", "You are already logged into ScrollsPost, you don't need to do anything else.", "Back");
            } else {
                ShowAuthEmail();   
            }
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
                    App.Popups.ShowOk(this, "done", "Registered!", String.Format("You now have an account with the email {0} on ScrollsPost.com!\n\nYou can login at any time on ScrollsPost.com to manage your card collection as well as set cards for sale or find cards to buy.", config.GetString("email")), "Ok");

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
                    App.Popups.ShowOk(this, "done", "Logged In!", "You're now logged into your ScrollsPost.com account!\n\nYour cards will be synced automatically and can be viewed at any time", "Ok");

                    config.Add("user-id", (String) result["user_id"]);
                    config.Add("api-key", (String) result["api_key"]);
                    config.Remove("email");

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

