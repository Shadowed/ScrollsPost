using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using Mono.Cecil;
using ScrollsModLoader.Interfaces;
using System.Reflection;
using UnityEngine;

namespace ScrollsPost {
    public class Mod : BaseMod, IOkCallback, IOkStringCancelCallback {
        private String logFolder;
        private TradePrices activeTrade;

        public Boolean loggedIn;
        public ConfigGUI configGUI;
        public ConfigManager config;
        public PriceManager scrollPrices;
        public CollectionSync cardSync;
        public ReplayLogger replayLogger;
        public ReplayRunner replayRunner;

        //public String apiURL = "http://api.scrollspost.com/";
        public String apiURL = "http://localhost:5000/api/";


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

            // Config migration + upgrades
            config.MigratePath();

            // Old version migration + initial setup
            if( config.NewInstall() ) {
                new Thread(new ThreadStart(configGUI.ShowIntro)).Start();
            } else if( !config.ContainsKey("conf-version") ) {
                new Thread(new ThreadStart(configGUI.ShowAuthPrompt)).Start();
            }

            // Added trade/sync notification
            if( config.VersionBelow(5) ) {
                config.Add("trade", true);
                config.Add("sync-notif", false);
            }

            // Add replay support
            if( config.VersionBelow(6) ) {
                // Move to per account configuration
                config.Add("replay", "ask");
                new Thread(new ParameterizedThreadStart(configGUI.ShowChanges)).Start((object)5);
            }

            // Just updated
            if( config.NewInstall() || !config.ContainsKey("conf-version") || config.GetInt("conf-version") != Mod.GetVersion() ) {
                config.Add("conf-version", Mod.GetVersion());
            }

            // Check if we need to resync cards
            cardSync.PushIfStale();

            //replayRunner = new ReplayRunner(this, "/Applications/Scrolls.app/Contents/MacOS/game/MacScrolls.app/Contents/Data/Managed/ModLoader/mods/ScrollsPost/replays/4179766-black.spr");
        }

        public static string GetName() {
            return "ScrollsPost";
        }

        public static int GetVersion() {
            return 6;
        }

        public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version) {
            try {
                return new MethodDefinition[] {
                    scrollsTypes["Communicator"].Methods.GetMethod("sendRequest", new Type[]{ typeof(Message) }),
                    scrollsTypes["TradeSystem"].Methods.GetMethod("StartTrade")[0],
                    scrollsTypes["TradeSystem"].Methods.GetMethod("CloseTrade")[0],
                    scrollsTypes["TradeSystem"].Methods.GetMethod("UpdateView")[0],
                    scrollsTypes["CardView"].Methods.GetMethod("updateGraphics")[0],

                    scrollsTypes["Communicator"].Methods.GetMethod("handleNextMessage")[0],
                    scrollsTypes["BattleMode"].Methods.GetMethod("addDelay")[0],
                    scrollsTypes["iTween"].Methods.GetMethod("UpdatePercentage")[0],
                    scrollsTypes["BattleMode"].Methods.GetMethod("OnGUI")[0],
                    scrollsTypes["BattleModeUI"].Methods.GetMethod("Init")[0],
                    scrollsTypes["BattleModeUI"].Methods.GetMethod("ShowEndTurn")[0]
                };
            } catch( Exception ex ) {
                Console.WriteLine("****** HOOK FAILURE {0}", ex.ToString());
                return new MethodDefinition[] { };
            }
        }

        public override bool BeforeInvoke(InvocationInfo info, out object returnValue) {
            returnValue = null;

            if( info.targetMethod.Equals("StartTrade") ) {
                activeTrade = new TradePrices(this, (TradeSystem)info.target);
            
            } else if( info.targetMethod.Equals("UpdateView") && activeTrade != null ) {
                activeTrade.PreUpdateView();
               
            } else if( info.targetMethod.Equals("CloseTrade") && activeTrade != null ) {
                activeTrade.Finished();
                activeTrade = null;

            } else if( info.targetMethod.Equals("updateGraphics") && activeTrade != null ) {
                activeTrade.PreOverlayRender((Card)info.arguments[0]);

            } else if( info.targetMethod.Equals("sendRequest") ) {
                if( info.arguments[0] is RoomChatMessageMessage ) {
                    RoomChatMessageMessage msg = (RoomChatMessageMessage)info.arguments[0];
                    if( msg.text.Equals("/sp") || msg.text.Equals("/scrollspost") || msg.text.Equals("/scrollpost") ) {
                        new Thread(new ThreadStart(configGUI.Show)).Start();

                        SendMessage("Configuration opened");
                        return true;

                    } else if( msg.text.StartsWith("/pc-1h") ) {
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
               
                    // Do our initial login check
                } else if( !loggedIn && info.arguments[0] is RoomEnterFreeMessage ) {
                    loggedIn = true;
                    Init();
                }
                
            // Replay handler
            } else if( replayRunner != null ) {
                if( info.targetMethod.Equals("handleNextMessage") ) {
                    if( replayRunner.OnHandleNextMessage() ) {
                        returnValue = true;
                        return true;
                    }

                } else if( info.targetMethod.Equals("OnGUI") ) {
                    replayRunner.OnBattleGUI(info);
                
                } else if( info.targetMethod.Equals("addDelay") ) {
                    if( replayRunner.OnBattleDelay(info) ) {
                        returnValue = null;
                        return true;
                    }

                //} else if( info.targetMethod.Equals("Init") ) {
                //    replayRunner.OnBattleUIInit(info);

                } else if( info.targetMethod.Equals("ShowEndTurn") ) {
                    if( replayRunner.OnBattleUIShowEndTurn(info) ) {
                        returnValue = null;
                        return true;
                    }
                }
            }

            return false;
        }

        public override void AfterInvoke(InvocationInfo info, ref object returnValue) {
            if( activeTrade != null && info.targetMethod.Equals("updateGraphics") ) {
                activeTrade.PostOverlayRender((Card)info.arguments[0]);
            } else if( activeTrade != null && info.targetMethod.Equals("UpdateView") ) {
                activeTrade.PostUpdateView();
            } else if( replayRunner != null && info.targetMethod.Equals("UpdatePercentage") ) {
                replayRunner.OnTweenUpdatePercentage(info);
            }

            return;
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
            msg.roomName = App.ArenaChat.ChatRooms.GetCurrentRoom();

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