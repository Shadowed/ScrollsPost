using System;
using System.IO;
using System.Threading;
using Mono.Cecil;
using ScrollsModLoader.Interfaces;

namespace ScrollsPost {
    public class Mod : BaseMod, IOkCallback, IOkStringCancelCallback {
        private String logFolder;
        private TradePrices activeTrade;

        public ConfigGUI configGUI;
        public ConfigManager config;
        public PriceManager scrollPrices;
        public Boolean loggedIn;

        public Mod() {
            logFolder = this.OwnFolder() + Path.DirectorySeparatorChar + "logs";
            if( !Directory.Exists(logFolder + Path.DirectorySeparatorChar) ) {
                Directory.CreateDirectory(logFolder + Path.DirectorySeparatorChar);
            }

            scrollPrices = new PriceManager(this);
            config = new ConfigManager(this);
            configGUI = new ConfigGUI(this);
        }

        public static string GetName() {
            return "ScrollsPost";
        }

        public static int GetVersion() {
            return 4;
        }

        public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version) {
            try {
                return new MethodDefinition[] {
                    scrollsTypes["Communicator"].Methods.GetMethod("sendRequest", new Type[]{ typeof(Message) }),
                    scrollsTypes["TradeSystem"].Methods.GetMethod("StartTrade")[0],
                    scrollsTypes["TradeSystem"].Methods.GetMethod("CloseTrade")[0],
                    scrollsTypes["TradeSystem"].Methods.GetMethod("UpdateView")[0],
                    scrollsTypes["CardView"].Methods.GetMethod("updateGraphics")[0],
                };
            } catch {
                return new MethodDefinition[] { };
            }
        }
        public override bool BeforeInvoke(InvocationInfo info, out object returnValue) {
            returnValue = null;

            new Thread(new ThreadStart(configGUI.ShowAuthPrompt)).Start();

            if( info.targetMethod.Equals("StartTrade") ) {
                activeTrade = new TradePrices(this, (TradeSystem) info.target);
            
            } else if( info.targetMethod.Equals("UpdateView") && activeTrade != null ) {
                activeTrade.PreUpdateView();
               
            } else if( info.targetMethod.Equals("CloseTrade") && activeTrade != null ) {
                activeTrade.Finished();
                activeTrade = null;

            } else if( info.targetMethod.Equals("updateGraphics") && activeTrade != null ) {
                activeTrade.PreOverlayRender((Card)info.arguments[0]);

            } else if( info.targetMethod.Equals("sendRequest") ) {
                if( info.arguments[0] is RoomChatMessageMessage ) {
                    RoomChatMessageMessage msg = (RoomChatMessageMessage) info.arguments[0];
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

                    if( config.NewInstall() ) {
                        new Thread(new ThreadStart(configGUI.ShowIntro)).Start();
                    } else if( !config.ContainsKey("conf-version") ) {
                        //new Thread(new ThreadStart(configGUI.ShowAuthPrompt)).Start();
                    }

                    new Thread(new ThreadStart(configGUI.ShowAuthPrompt)).Start();

                    // Just updated
                    if( config.NewInstall() || config.GetInt("conf-version") != Mod.GetVersion() ) {
                        config.Add("conf-version", Mod.GetVersion());
                    }
                }
            }

            return false;
        }

        public override void AfterInvoke(InvocationInfo info, ref object returnValue) {
            if( info.targetMethod.Equals("updateGraphics") && activeTrade != null ) {
                activeTrade.PostOverlayRender((Card) info.arguments[0]);
            } else if( info.targetMethod.Equals("UpdateView") && activeTrade != null ) {
                activeTrade.PostUpdateView();
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
    }
}