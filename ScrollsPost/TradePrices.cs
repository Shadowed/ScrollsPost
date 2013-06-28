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
        private String pricedName;

        public TradePrices(ScrollsPost.Mod mod, TradeSystem trade) {
            this.mod = mod;
            this.trade = trade;

            ptsType = mod.asm.GetType("TradeSystem+PlayerTradeStatus");
            this.player1 = typeof(TradeSystem).GetField("p1", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(this.trade);
            this.player2 = typeof(TradeSystem).GetField("p2", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(this.trade);

            new Thread(new ThreadStart(Check)).Start();
		}

        private void Loaded(List<Card> allCards1, List<Card> allCards2) {
            foreach( Card card in allCards1 ) {
                CardType type = (CardType) typeof(Card).GetField("type", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(card);
                if( !type.name.StartsWith("[") ) {
                    APIPriceCheckResult scroll = mod.scrollPrices.GetScroll(type.id);
                    if( scroll != null ) {
                        type.name = String.Format("[{0}g] {1}", scroll.price.suggested, type.name);
                    }
                }
            }
              
            ptsType.GetMethod("SetCards").Invoke(this.player1, new object[] { allCards1 });
        }

        public void PreOverlayRender(Card card) {
            CardType type = (CardType) typeof(Card).GetField("type", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(card);
            if( type.name.StartsWith("[") ) {
                pricedName = type.name;
                type.name = type.name.Split(new char[] { ' ' }, 2)[1];
            }
        }

        public void PostOverlayRender(Card card) {
            CardType type = (CardType) typeof(Card).GetField("type", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(card);
            type.name = pricedName;
            pricedName = null;
        }

        private void Check() {
            // Might as well start freshening the data while we wait a few ms for cards to load
            mod.scrollPrices.LoadData();

            // This is a total hack, but the cards are not loaded immediately and I can't find a good efficient hook.
            // So instead will just poll until it appears.
            while( true ) {
                Thread.Sleep(1);

                List<Card> allCards1 = (List<Card>) ptsType.GetField("allCards", BindingFlags.Public | BindingFlags.GetField | BindingFlags.Instance).GetValue(this.player1);
                List<Card> allCards2 = (List<Card>) ptsType.GetField("allCards", BindingFlags.Public | BindingFlags.GetField | BindingFlags.Instance).GetValue(this.player2);

                if( allCards1.Count > 0 && allCards2.Count > 0 ) {
                    Loaded(allCards1, allCards2);
                    break;
                }
            }
        }
	}
}

