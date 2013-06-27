using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Text;
using Mono.Cecil;
using ScrollsModLoader.Interfaces;
using UnityEngine;

namespace ScrollsPost {
    public class Mod : BaseMod, IOkCallback, IOkStringCancelCallback {
        private String configFolder;
        private String logFolder;
        public Dictionary<String, String> config;

        public Mod() {
            configFolder = this.OwnFolder() + Path.DirectorySeparatorChar + "config";
            if( !Directory.Exists(configFolder + Path.DirectorySeparatorChar) ) {
                Directory.CreateDirectory(configFolder + Path.DirectorySeparatorChar);
            }

            logFolder = this.OwnFolder() + Path.DirectorySeparatorChar + "logs";
            if( !Directory.Exists(logFolder + Path.DirectorySeparatorChar) ) {
                Directory.CreateDirectory(logFolder + Path.DirectorySeparatorChar);
            }
        }

        public static string GetName() {
            return "ScrollsPost";
        }

        public static int GetVersion() {
            return 3;
        }

        public String ConfigPath(String file) {
            return configFolder + Path.DirectorySeparatorChar + file;
        }

        public void LoadConfig() {
            config = new Dictionary<String, String>();

            String path = this.ConfigPath("config.ini");
            if( File.Exists(path) ) {
                String[] lines = path.Split(new char[] { '\n' });
                foreach( String line in lines ) {
                    String[] parts = line.Split(new char[] { '=' }, 2);
                    config.Add(parts[0], parts[1]);
                }
            }
        }

        public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version) {
            try {
                return new MethodDefinition[] {
                    scrollsTypes["Communicator"].Methods.GetMethod("sendRequest", new Type[]{ typeof(Message) })
                };
            } catch {
                return new MethodDefinition[] { };
            }
        }

        public override bool BeforeInvoke(InvocationInfo info, out object returnValue) {
            returnValue = null;
            /*
            if (info.targetMethod.Equals("sendRequest"))
            {
                if (info.arguments[0] is RoomChatMessageMessage)
                {
                    RoomChatMessageMessage msg = (RoomChatMessageMessage) info.arguments[0];
                    if (msg.text.ToLower().StartsWith("/sp-auth")) {
                        new Authenticator (this);
                    }
                }
            }
            */

            if( info.targetMethod.Equals("sendRequest") ) {
                if( info.arguments[0] is RoomChatMessageMessage ) {
                    RoomChatMessageMessage msg = (RoomChatMessageMessage)info.arguments[0];
                    if( msg.text.StartsWith("/pc-1h") ) {
                        new PriceCheck(this, "1-hour", msg.text.Split(new char[] { ' ' }, 2)[1]);
                        return true;

                    } else if( msg.text.StartsWith("/pc-3d") ) {
                        new PriceCheck(this, "3-days", msg.text.Split(new char[] { ' ' }, 2)[1]);
                        return true;

                    } else if( msg.text.StartsWith("/pc-7d") ) {
                        new PriceCheck(this, "7-days", msg.text.Split(new char[] { ' ' }, 2)[1]);
                        return true;

                    } else if( msg.text.StartsWith("/pc") || msg.text.StartsWith("/pc-1d") ) {
                        new PriceCheck(this, "1-day", msg.text.Split(new char[] { ' ' }, 2)[1]);
                        return true;
                    }
                }
            }

            return false;
        }

        public override void AfterInvoke(InvocationInfo info, ref object returnValue) {
          return;
        }

        public void PopupCancel(string popupType) {
          return;
        }

        public void PopupOk(string popupType) {
          return;
        }

        public void PopupOk(String popupType, String choice) {
          return;
        }

        public void SendMessage(String message) {
            RoomChatMessageMessage msg = new RoomChatMessageMessage();
            msg.from = "ScrollsPost";
            msg.text = message;
            msg.roomName = App.ArenaChat.ChatRooms.GetCurrentRoom();

            App.ChatUI.handleMessage(msg);
            App.ArenaChat.ChatRooms.ChatMessage(msg);
        }

        public void WriteLog(String txt, Exception e) {
            String name = "error-" + DateTime.Now.ToString ("yyyy-MM-dd-HH-mm") + ".log";
            File.WriteAllText (logFolder + Path.DirectorySeparatorChar + name, txt + "\n\n" + e);
        }


        internal class Authenticator : IOkStringCancelCallback {
            private Mod mod;

            public Authenticator(Mod mod) {
                this.mod = mod;

                if( mod.config.ContainsKey("user_id") ) {
                    App.Popups.ShowTextInput(this, "", "", "email", "ScrollsPost Login", "Enter your email:", "Go");
                } else {
                    App.Popups.ShowTextInput(this, "", "Card syncing requires a http://www.scrollspost.com account.\n\nIf you don't want the advance trade features and just price check, you can skip this.", "email", "ScrollsPost Login / Registration", "Enter your email to get started:", "Go");
                }
            }

            private void Start() {
                //App.Popups.ShowInfo("Authenticating", "We're authenticating you with ScrollsPost, please wait...");
                try {
                    String res = new WebClient().DownloadString("http://api.scrollspost.com/v1/authenticate");

                    if( res == "1" ) {
                        App.Popups.ShowOk(mod, "fail", "Authenticated!", "You can now view your data on ScrollsPost.com", "Ok");
                        File.WriteAllText(mod.ConfigPath("key.sp"), res);

                    } else if( res == "2" ) {
                        App.Popups.ShowOk(mod, "fail", "Invalid API Key", "That API key is invalid, please double check that you got the right one.", "Ok");
                    }
                } catch( System.Net.WebException we ) {
                    App.Popups.ShowOk(mod, "fail", "HTTP Error", "Contact support@scrollspost.com for help.\n\n" + we.Message, "Ok");
                    mod.WriteLog("Failed to authenticate", we);
                }
            }

            public void PopupCancel(string popupType) {
                return;
            }

            public void PopupOk(string popupType, String choice) {
                if( popupType == "email" ) {
                    if( mod.config.ContainsKey("user_id") ) {
                        App.Popups.ShowTextInput(this, "", "Do not use the same password as you do on Scrolls!", "password", "ScrollsPost Login", "Password:", "Save");
                    } else {
                        App.Popups.ShowTextInput(this, "", "Do not use the same password as you do on Scrolls!", "password", "ScrollsPost Login / Registration", "Now enter your password:", "Save");
                    }

                } else if( popupType == "password" ) {
                    App.Popups.ShowInfo("Authenticating...", "We're authenticating with ScrollsPost, give us a second.");
                }

                //Thread authThread = new Thread(new ThreadStart(Start));
                //authThread.Start();

                //mod.FinishedAuthentication();
                return;
            }
        }
    }
}