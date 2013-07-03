using System;
using System.Net;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Threading;
using JsonFx.Json;

namespace ScrollsPost {
    public class CollectionSync : ICommListener {
        private ScrollsPost.Mod mod;
        private ConfigManager config;
        private Dictionary<long, String> inventoryCards = new Dictionary<long, String>();
        //private Dictionary<String, Dictionary<int, > deckCards = new Dictionary<int, int>();
        private Thread dataPusher;

        public CollectionSync(ScrollsPost.Mod mod) {
            this.mod = mod;
            this.config = mod.config;

            App.Communicator.addListener(this);
        }

        // DeckList && LibraryView
        public void handleMessage(Message msg) {
            if( msg is LibraryViewMessage && config.ContainsKey("user-id") ) {
                LibraryViewMessage viewMsg = (LibraryViewMessage) msg;

                inventoryCards.Clear();
                foreach( Card card in viewMsg.cards ) {
                    inventoryCards[card.id] = String.Format("{0},{1}", card.typeId, card.isTradable() ? 1 : 0);
                }

                if( dataPusher == null ) {
                    if( config.ContainsKey("last-card-sync") ) {
                        dataPusher = new Thread(new ThreadStart(DelayedPush));
                    } else {
                        dataPusher = new Thread(new ThreadStart(Push));
                    }

                    dataPusher.Start();
                }

            //} else if( msg is DeckCardsMessage ) {
            //    DeckCardsMessage deckMsg = (DeckCardsMessage)msg;
            //} else if( msg is DeckSaveMessage ) {
            }

        }

        public void onReconnect() {

        }

        public void PushIfStale() {
            if( !config.ContainsKey("user-id") )
                return;

            double epoch = (DateTime.UtcNow - (new DateTime(1970, 1, 1))).TotalSeconds;
            if( !config.ContainsKey("last-card-sync") || (epoch - config.GetInt("last-card-sync")) >= 86400 ) {
                App.Communicator.sendRequest(new LibraryViewMessage());
            }
        }

        // Push collection
        public void DelayedPush() {
            Thread.Sleep(5000);
            Push();
        }

        public void Push() {
            // Turn it into something that's not crazy to send over the wire first
            Dictionary<int, Dictionary<String, int>> cards = new Dictionary<int, Dictionary<String, int>>();

            foreach( KeyValuePair<long, String> pair in inventoryCards ) {
                String[] data = pair.Value.Split(new char[] { ',' }, 2);

                int card_id = Convert.ToInt32(data[0]);

                if( cards.ContainsKey(card_id) ) { 
                    Dictionary<String, int> compiled = cards[card_id];
                    compiled["total"] += 1;
                    if( data[1].Equals("1") )
                        compiled["trade"] += 1;
                 
                } else {
                    Dictionary<String, int> compiled = new Dictionary<String, int>();
                    compiled["total"] = 1;
                    compiled["trade"] = data[1].Equals("1") ? 1 : 0;

                    cards[card_id] = compiled;
                }
            }

            String to_send = mod.CompressString(new JsonWriter().Write(cards));

            try {
                NameValueCollection form = new NameValueCollection();
                form["user_id"] = config.GetString("user-id");
                form["api_key"] = config.GetString("api-key");
                form["uid"] = App.MyProfile.ProfileInfo.userUuid;
                form["collection"] = to_send;

                WebClient wc = new WebClient();
                wc.UploadValues(new Uri(mod.apiURL + "/v1/cards"), "POST", form);

                if( !config.ContainsKey("last-card-sync") ) {
                    mod.SendMessage("Finished initial collection sync to ScrollsPost. From now on, your collection will auto sync whenever your cards change, you can force a resync by opening up the deck library and waiting about 10 seconds for it to show up on ScrollsPost.");
                } else if( config.GetBoolean("sync-notif") ) {
                    mod.SendMessage("Collection synced to ScrollsPost.com.");
                }

            } catch ( WebException we ) {
                Console.WriteLine("**** ERROR {0}", we.ToString());
                mod.SendMessage("We had an HTTP error while syncing your cards, contact us at support@scrollspost.com for help.");
                mod.WriteLog("Failed to sync collection", we);
            }

            config.Add("last-card-sync", (int) (DateTime.UtcNow - (new DateTime(1970, 1, 1))).TotalSeconds);
            dataPusher = null;
        }
    }
}

