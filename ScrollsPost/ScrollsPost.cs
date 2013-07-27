using System;
using System.IO;
using System.Text;
using System.Threading;
using Mono.Cecil;
using ScrollsModLoader.Interfaces;
using System.Reflection;
using UnityEngine;

namespace ScrollsPost {
    public class Mod : BaseMod, IOkCallback, IOkStringCancelCallback, ICommListener {
        private String logFolder;
        private TradePrices activeTrade;

        public Boolean loggedIn;
        public ConfigGUI configGUI;
        public ConfigManager config;
        public PriceManager scrollPrices;
        public CollectionSync cardSync;
        public ReplayLogger replayLogger;
        public ReplayRunner replayRunner;
        //private ReplayGUI replayGUI;

        public String apiURL = "http://api.scrollspost.com/";
        //public String apiURL = "http://localhost:5000/api/";

        // WARNING: This is used for internal configs, please do not change it or it will cause bugs.
        // Change GetVersion() instead to not use the constant if it's needed.
        private static int CURRENT_VERSION = 9;


        public Mod() {
            logFolder = this.OwnFolder() + Path.DirectorySeparatorChar + "logs";
            if( !Directory.Exists(logFolder + Path.DirectorySeparatorChar) ) {
                Directory.CreateDirectory(logFolder + Path.DirectorySeparatorChar);
            }
        }

        private void Init() {
            scrollPrices = new PriceManager(this);
            config = new ConfigManager(this);
            configGUI = new ConfigGUI(this);
            cardSync = new CollectionSync(this);
            replayLogger = new ReplayLogger(this);
            //replayGUI = new ReplayGUI(this);

            // Added trade/sync notification
            if( config.VersionBelow(5) ) {
                config.Add("trade", true);
                config.Add("sync-notif", false);
            }

            if( !config.ContainsKey("replay") ) {
                config.Add("replay", "ask");
            }

            // Old version migration + initial setup
            if( config.NewInstall() ) {
                new Thread(new ThreadStart(configGUI.ShowIntro)).Start();
            } else if( !config.ContainsKey("conf-version") ) {
                new Thread(new ThreadStart(configGUI.ShowAuthPrompt)).Start();
            } else {
                // Replay updates
                if( config.VersionBelow(7) ) {
                    new Thread(new ParameterizedThreadStart(configGUI.ShowChanges)).Start((object)6);
                    // Add replay support
                } else if( config.VersionBelow(6) ) {
                    new Thread(new ParameterizedThreadStart(configGUI.ShowChanges)).Start((object)5);
                        
                } else if( config.VersionBelow(9) ) {
                    new Thread(new ParameterizedThreadStart(configGUI.ShowChanges)).Start((object)8);
                }
            }

            // Just updated
            if( config.NewInstall() || !config.ContainsKey("conf-version") || config.GetInt("conf-version") != CURRENT_VERSION ) {
                config.Add("conf-version", CURRENT_VERSION);
            }

            // Check if we need to resync cards
            cardSync.PushIfStale();
        }

        public static string GetName() {
            return "ScrollsPost";
        }

        public static int GetVersion() {
            return CURRENT_VERSION - 1;
        }

        public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version) {
            try {
                return new MethodDefinition[] {
                    scrollsTypes["Communicator"].Methods.GetMethod("sendRequest", new Type[]{ typeof(Message) }),
                    scrollsTypes["TradeSystem"].Methods.GetMethod("StartTrade")[0],
                    scrollsTypes["TradeSystem"].Methods.GetMethod("CloseTrade")[0],
                    scrollsTypes["TradeSystem"].Methods.GetMethod("UpdateView")[0],
                    scrollsTypes["CardView"].Methods.GetMethod("updateGraphics")[0],

                    scrollsTypes["AnimPlayer"].Methods.GetMethod("UpdateOnly")[0],
                    scrollsTypes["Communicator"].Methods.GetMethod("handleNextMessage")[0],
                    scrollsTypes["BattleMode"].Methods.GetMethod("addDelay")[0],
                    scrollsTypes["BattleMode"].Methods.GetMethod("effectDone")[0],
                    scrollsTypes["iTween"].Methods.GetMethod("Launch")[0],
                    scrollsTypes["BattleMode"].Methods.GetMethod("OnGUI")[0],
                    scrollsTypes["BattleModeUI"].Methods.GetMethod("ShowEndTurn")[0],
                    scrollsTypes["GUIBattleModeMenu"].Methods.GetMethod("toggleMenu")[0],

                    //scrollsTypes["ProfileMenu"].Methods.GetMethod("Start")[0],
                };
            } catch( Exception ex ) {
                Console.WriteLine("****** HOOK FAILURE {0}", ex.ToString());
                return new MethodDefinition[] { };
            }
        }

        public override bool WantsToReplace(InvocationInfo info) {
            if( info.targetMethod.Equals("sendRequest") && info.arguments[0] is RoomChatMessageMessage ) {
                RoomChatMessageMessage msg = (RoomChatMessageMessage)info.arguments[0];
                if( msg.text.Equals("/sp") || msg.text.Equals("/scrollspost") || msg.text.Equals("/scrollpost") ) {
                    new Thread(new ThreadStart(configGUI.Show)).Start();

                    SendMessage("Configuration opened");
                    return true;

                } else if( msg.text.StartsWith("/pc-1h ") ) {
                    new PriceCheck(this, "1-hour", msg.text.Split(new char[] { ' ' }, 2)[1]);
                    return true;

                } else if( msg.text.StartsWith("/pc-3d ") ) {
                    new PriceCheck(this, "3-days", msg.text.Split(new char[] { ' ' }, 2)[1]);
                    return true;

                } else if( msg.text.StartsWith("/pc-7d ") ) {
                    new PriceCheck(this, "7-days", msg.text.Split(new char[] { ' ' }, 2)[1]);
                    return true;

                } else if( msg.text.StartsWith("/pc ") || msg.text.StartsWith("/pc-1d ") ) {
                    new PriceCheck(this, "1-day", msg.text.Split(new char[] { ' ' }, 2)[1]);
                    return true;
                }
            } else if( replayRunner != null ) {
                if( info.targetMethod.Equals("handleNextMessage") ) {
                    if( replayRunner.OnHandleNextMessage() ) {
                        return true;
                    }

                } else if( info.targetMethod.Equals("UpdateOnly") ) {
                    replayRunner.OnAnimationUpdate(info);

                } else if( info.targetMethod.Equals("OnGUI") ) {
                    replayRunner.OnBattleGUI(info);

                } else if( info.targetMethod.Equals("addDelay") ) {
                    if( replayRunner.OnBattleDelay(info) ) {
                        return true;
                    }

                } else if( info.targetMethod.Equals("Launch") ) {
                    replayRunner.OnTweenLaunch(info);

                } else if( info.targetMethod.Equals("toggleMenu") ) {
                    StopReplayRunner();
                    return true;

                } else if( info.targetMethod.Equals("effectDone") ) {
                    replayRunner.OnBattleEffectDone(info);

                } else if( info.targetMethod.Equals("ShowEndTurn") ) {
                    if( replayRunner.OnBattleUIShowEndTurn(info) ) {
                        return true;
                    }
                }
            }

            return false;
        }

        public override void BeforeInvoke(InvocationInfo info) {
            if( info.targetMethod.Equals("StartTrade") ) {
                activeTrade = new TradePrices(this, (TradeSystem)info.target);
            
            } else if( info.targetMethod.Equals("UpdateView") && activeTrade != null ) {
                activeTrade.PreUpdateView();
               
            } else if( info.targetMethod.Equals("CloseTrade") && activeTrade != null ) {
                activeTrade.Finished();
                activeTrade = null;

            } else if( info.targetMethod.Equals("updateGraphics") && activeTrade != null ) {
                activeTrade.PreOverlayRender((Card)info.arguments[0]);

            // Do our initial login check
            } else if( !loggedIn && info.targetMethod.Equals("sendRequest") && info.arguments[0] is RoomEnterFreeMessage ) {
                loggedIn = true;
                Init();

            // Replay UI
            //} else if( info.targetMethod.Equals("Start") && App.SceneValues.profilePage.isMe() ) {
            //    replayGUI.Show();
           
            //} else if( info.targetMethod.Equals("drawEditButton") ) {
            //    replayGUI.Hide();
            }
        }

        public override void AfterInvoke(InvocationInfo info, ref object returnValue) {
            if( activeTrade != null && info.targetMethod.Equals("updateGraphics") ) {
                activeTrade.PostOverlayRender((Card)info.arguments[0]);
            } else if( activeTrade != null && info.targetMethod.Equals("UpdateView") ) {
                activeTrade.PostUpdateView();
            }

            return;
        }

        public void handleMessage(Message msg) {
            if( msg is ProfileInfoMessage && replayRunner != null ) {
                StopReplayRunner();
            }
        }

        public void onReconnect() {
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
            msg.from = GetName();
            msg.text = message;
            msg.roomName = App.ArenaChat.ChatRooms.GetCurrentRoom().name;

            App.ChatUI.handleMessage(msg);
            App.ArenaChat.ChatRooms.ChatMessage(msg);
        }

        public void WriteLog(String txt, Exception e) {
            String name = "error-" + DateTime.Now.ToString ("yyyy-MM-dd-HH-mm") + ".log";
            File.WriteAllText (logFolder + Path.DirectorySeparatorChar + name, txt + "\n\n" + e);
        }

        public double TimeSinceEpoch() {
            return (DateTime.UtcNow - (new DateTime(1970, 1, 1))).TotalSeconds;
        }

        public void StartReplayRunner(String path) {
            App.Communicator.addListener(this);
            replayRunner = new ReplayRunner(this, path);
        }

        public void StopReplayRunner() {
            if( replayRunner != null ) {
                App.Communicator.removeListener(this);
                replayRunner.Stop();
                replayRunner = null;
            }
        }

        public String OpenFileDialog() {
            return modAPI.FileOpenDialog();
        }

        public String CompressString(String text) {
            /*
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using( var output = new MemoryStream() ) {
                using( var gzs = new GZipStream(output, CompressionMode.Compress))
                using( var input = new MemoryStream(bytes) )
                    input.WriteTo(gzs);           

                return Convert.ToBase64String(output.ToArray());
            }
            */

            return text;
        }
    }
}
