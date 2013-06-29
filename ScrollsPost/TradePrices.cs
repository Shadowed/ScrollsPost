using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using JsonFx.Json;

namespace ScrollsPost {
    public class TradePrices {
        private ScrollsPost.Mod mod;
        private TradeSystem trade;
        private object player1;
        private object player2;
        private Type ptsType;
        private Dictionary<int, CardType> allCardTypes = new Dictionary<int, CardType>();
        private Dictionary<int, String> origNames = new Dictionary<int, String>();
        private Dictionary<int, String> pricedNames = new Dictionary<int, String>();

        public TradePrices(ScrollsPost.Mod mod, TradeSystem trade) {
            this.mod = mod;
            this.trade = trade;

            new Thread(new ThreadStart(CheckData)).Start();
        }


        // Strip out our price hook
        public void Finished() {
            new Thread(new ThreadStart(FinishAsync)).Start();
        }

        private void FinishAsync() {
            foreach( var pair in allCardTypes ) {
                if( origNames.ContainsKey(pair.Value.id) ) {
                    pair.Value.name = origNames[pair.Value.id];
                }
            }
        }

        // Overlay hooks
        public void PreOverlayRender(Card card) {
            CardType type = allCardTypes[card.typeId];
            if( origNames.ContainsKey(type.id) ) {
                type.name = origNames[type.id];
            }
        }

        public void PostOverlayRender(Card card) {
            allCardTypes[card.typeId].name = pricedNames[card.typeId];
        }

        // Restore card names before sorting
        public void PreUpdateView() {
            FinishAsync();
        }

        public void PostUpdateView() {
            foreach( var pair in allCardTypes ) {
                if( pricedNames.ContainsKey(pair.Value.id) ) {
                    pair.Value.name = pricedNames[pair.Value.id];
                }
            }
        }

        // Cards have been loaded in
        private void Loaded(List<Card> allCards1, List<Card> allCards2) {
            //ptsType.GetMethod("SortLists", BindingFlags.Public | BindingFlags.GetField | BindingFlags.Instance).Invoke(this.player1, null);
            //ptsType.GetMethod("SortLists", BindingFlags.Public | BindingFlags.GetField | BindingFlags.Instance).Invoke(this.player2, null);


            foreach( List<Card> cards in new List<Card>[] { allCards1, allCards2 } ) {
                foreach( Card card in cards ) {
                    CardType type = (CardType) typeof(Card).GetField("type", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(card);

                    // Cards seem to be shared, if between two trades you have all 138 cards, you actually only have 138 CardType instances
                    if( allCardTypes.ContainsKey(type.id) )
                        continue;

                    allCardTypes.Add(type.id, type);

                    // Store the original name for easy restoring of names for sorting/etc
                    origNames.Add(type.id, type.name);

                    // Grab the price and store the name if any
                    APIPriceCheckResult scroll = mod.scrollPrices.GetScroll(type.id);
                    if( scroll != null ) {
                        type.name = String.Format("[{0}g] {1}", scroll.price.suggested, origNames[type.id]);

                        pricedNames.Add(type.id, type.name);
                    } else {
                        pricedNames.Add(type.id, type.name);
                    }
                }
            }
        }

        private void CheckData() {
            ptsType = mod.asm.GetType("TradeSystem+PlayerTradeStatus");
            this.player1 = typeof(TradeSystem).GetField("p1", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(this.trade);
            this.player2 = typeof(TradeSystem).GetField("p2", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(this.trade);

            // Might as well start freshening the data while we wait a few ms for cards to load
            mod.scrollPrices.LoadData();

            // This is a total hack, but the cards are not loaded immediately and I can't find a good efficient hook.
            // So instead will just poll until it appears.
            List<Card> allCards1;
            List<Card> allCards2;

            while( true ) {
                allCards1 = (List<Card>) ptsType.GetField("allCards", BindingFlags.Public | BindingFlags.GetField | BindingFlags.Instance).GetValue(this.player1);
                allCards2 = (List<Card>) ptsType.GetField("allCards", BindingFlags.Public | BindingFlags.GetField | BindingFlags.Instance).GetValue(this.player2);

                if( allCards1.Count > 0 && allCards2.Count > 0 ) {
                    Loaded(allCards1, allCards2);
                    break;
                }

                Thread.Sleep(10);
            }
        }
    }
}

