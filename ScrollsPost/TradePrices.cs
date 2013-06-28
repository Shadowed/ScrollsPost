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
        private List<Card>[] allCards;
        private Dictionary<int, String> origNames = new Dictionary<int, String>();

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
            foreach( List<Card> cards in allCards ) {
                foreach( Card card in cards ) {
                    CardType type = (CardType)typeof(Card).GetField("type", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(card);
                    if( origNames.ContainsKey(type.id) ) {
                        type.name = origNames[type.id];
                    }
                }
            }
        }

        // Overlay hooks
        public void PreOverlayRender(Card card) {
            CardType type = (CardType) typeof(Card).GetField("type", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(card);
            if( origNames.ContainsKey(type.id) ) {
                type.name = origNames[type.id];
            }
        }

        public void PostOverlayRender(Card card) {
            CardType type = (CardType) typeof(Card).GetField("type", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(card);
            SetupCard(type);
        }

        // Helpers for all
        private void SetupCard(CardType type) {
            if( !origNames.ContainsKey(type.id) ) {
                origNames.Add(type.id, type.name);
            }

            APIPriceCheckResult scroll = mod.scrollPrices.GetScroll(type.id);
            if( scroll != null ) {
                type.name = String.Format("[{0}g] {1}", scroll.price.suggested, origNames[type.id]);
            }
        }

        // Cards have been loaded in
        private void Loaded(List<Card> allCards1, List<Card> allCards2) {
            allCards = new List<Card>[] { allCards1, allCards2 };

            foreach( List<Card> cards in allCards ) {
                foreach( Card card in cards ) {
                    CardType type = (CardType) typeof(Card).GetField("type", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(card);
                    SetupCard(type);
                }
            }

            ptsType.GetMethod("SetCards").Invoke(this.player1, new object[] { allCards1 });
            ptsType.GetMethod("SetCards").Invoke(this.player2, new object[] { allCards2 });
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

                Thread.Sleep(5);
           }
        }
	}
}

