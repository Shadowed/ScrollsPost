using System;
using System.Net;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace ScrollsPost {
    public class DeckManager : IOkCallback, IOkStringCancelCallback{
        //private ScrollsPost.Mod mod;

        private String baseStart = "0123456789";
        private String base77 = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private DeckBuilder2 deckBuilder;
        private GUISkin buttonSkin;

        public DeckManager(ScrollsPost.Mod mod) {
            //this.mod = mod;
            buttonSkin = (GUISkin) Resources.Load("_GUISkins/Lobby");
        }
       
        private void ExportDeck() {
            List<DeckCard> deck = (List<DeckCard>)typeof(DeckBuilder2).GetField("tableCards", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(deckBuilder);

            var compiledDeck = new Dictionary<int, int>();
            foreach( DeckCard deckCard in deck ) {
                int id = deckCard.card.getCardInfo().typeId;
                if( !compiledDeck.ContainsKey(id) ) compiledDeck[id] = 0;
                compiledDeck[id] += 1;
            }

            if( compiledDeck.Count == 0 ) {
                App.Popups.ShowOk(this, "", "No Cards", "You can't export a deck without any cards in it.", "Ok");
                return;
            }

            var cards = new List<String>();
            // Version
            cards.Add("1");

            foreach( KeyValuePair<int, int> row in compiledDeck ) {
                cards.Add(ConvertBase(String.Format("{0}{1}", row.Value, row.Key), baseStart, base77));
            }

            var url = String.Format("http://www.scrollspost.com/deckbuilder#{0}", String.Join(";", cards.ToArray()));
            App.Popups.ShowTextInput(this, url, "Above URL can be shared or imported in-game", "export", "Deck Export", "Deck Builder URL:", "Ok");
        }

        private void ImportDeck(String url) {
            if( !url.Contains("scrollspost.com/deckbuilder#1") ) {
                App.Popups.ShowTextInput(this, "", "<color=red>Not a valid ScrollsPost Deck Builder URL</color>", "import", "Deck Import", "Enter an URL:", "Import");
                return;
            }

            var parts = url.Split(new char[] { '#' }, 2);
            parts = parts[1].Split(new char[] { ';' }, 2);

            var version = Convert.ToInt32(parts[0].ToString());
            if( version != 1 ) {
                App.Popups.ShowTextInput(this, "", String.Format("<color=red>Invalid Deck Builder URL, version {0} found, expected {1}</color>", version), "import", "Deck Import", "Enter an URL:", "Import");
                return;
            }

            App.Popups.ShowInfo("Import Deck", "We're importing the deck. This might take a second.\n\nContact us at support@scrollspost.com if you run into any issues or have any questions!");

            var totalCards = 0;

            var deckCards = new Dictionary<int, int>();
            try {
                foreach( var line in parts[1].Split(new char[] { ';' }) ) {
                    var parsed = ConvertBase(line, base77, baseStart);
                    var quantity = Convert.ToInt32(parsed[0].ToString());
                    var card_id = Convert.ToInt32(parsed.Substring(1).ToString());

                    deckCards[card_id] = quantity;
                    totalCards += 1;
                }
            } catch( Exception ex ) {
                Console.WriteLine("***** EXCEPTION {0}", ex.ToString());
                App.Popups.ShowTextInput(this, "", String.Format("<color=red>Invalid Deck Builder URL, error parsing cards form URL.</color>", version), "import", "Deck Import", "Enter an URL:", "Import");
                return;
            }

            if( deckCards.Count == 0 ) {
                App.Popups.ShowOk(this, "", "No Cards Found", "We were able to parse the URL, but it contained no cards inside.", "Ok");
                return;
            }

            // Load a bunch of our fun patches
            var loadDeckMethod = typeof(DeckBuilder2).GetMethod("loadDeck", BindingFlags.NonPublic | BindingFlags.Instance);
            var alignCardsMethod = typeof(DeckBuilder2).GetMethod("alignTableCards", BindingFlags.NonPublic | BindingFlags.Instance);
            var allCardsField = typeof(DeckBuilder2).GetField("allCards", BindingFlags.NonPublic | BindingFlags.Instance);
            var collSorterField = typeof(DeckBuilder2).GetField("collectionSorter", BindingFlags.NonPublic | BindingFlags.Instance);
            var deckSortType = collSorterField.FieldType;


            List<Card> cards = new List<Card>((allCardsField.GetValue(deckBuilder) as List<Card>));

            // Sort it so we get the higher level cards first
            var sorter = Convert.ChangeType(Activator.CreateInstance(deckSortType), deckSortType);
            sorter = deckSortType.GetMethod("byLevel", BindingFlags.Public | BindingFlags.Instance).Invoke(sorter, new object[0]);
            typeof(List<Card>).GetMethod("Sort", new Type[]{ deckSortType }).Invoke(cards, new object[] { sorter });

            // Convert card type ids -> card ids
            var cardIDs = new List<long>();
            foreach( Card card in cards ) {
                int cardID = card.getType();
                if( !deckCards.ContainsKey(cardID) ) continue;

                deckCards[cardID] -= 1;
                cardIDs.Add(card.getId());

                if( deckCards[cardID] <= 0 ) {
                    deckCards.Remove(cardID);
                }
            }

            // Load the deck
            loadDeckMethod.Invoke(deckBuilder, new object[] { "Imported Deck", cardIDs, null });

            // Setup a number based sorter to align it all
            sorter = Convert.ChangeType(Activator.CreateInstance(deckSortType), deckSortType);
            sorter = deckSortType.GetMethod("byColor", BindingFlags.Public | BindingFlags.Instance).Invoke(sorter, new object[0]);
            sorter = deckSortType.GetMethod("byResourceCount", BindingFlags.Public | BindingFlags.Instance).Invoke(sorter, new object[0]);
            sorter = deckSortType.GetMethod("byName", BindingFlags.Public | BindingFlags.Instance).Invoke(sorter, new object[0]);
            sorter = deckSortType.GetMethod("byLevelAscending", BindingFlags.Public | BindingFlags.Instance).Invoke(sorter, new object[0]);
            alignCardsMethod.Invoke(deckBuilder, new object[] { 0, sorter });

            List<String> missingCards = new List<String>();
            CardTypeManager types = CardTypeManager.getInstance();

            var totalMissing = 0;
            foreach( KeyValuePair<int, int> row in deckCards ) {
                missingCards.Add(String.Format("{0} x {1}", row.Value, types.get(row.Key).name));
                totalMissing += row.Value;
            }
            
            if( missingCards.Count == 0 ) {
                App.Popups.ShowOk(this, "", "Fully Imported!", String.Format("All {0} cards of the deck have been fully imported. You weren't missing any of the cards required to construct it. Enjoy!", totalCards), "Ok");
            } else if( missingCards.Count <= 6 ) {
                App.Popups.ShowOk(this, "", "Partially Imported", String.Format("Only {0} of {1} cards were imported. Missing:\n{2}", (totalCards - totalMissing), totalCards, String.Join("\n", missingCards.ToArray())), "Ok");   
            } else {
                App.Popups.ShowScrollText(this, "", "Partially Imported", String.Format("Only {0} of {1} cards were imported. The below are the cards and quanities that we were unable to find in your collection.\n\n{2}", (totalCards - totalMissing), totalCards, String.Join("\n", missingCards.ToArray())), "Ok");   
            }
        }

        public void OnGUI(DeckBuilder2 builder) {
            this.deckBuilder = builder;

            var skin = GUI.skin;
            GUI.skin = buttonSkin;

            GUIPositioner subMenuPositioner = App.LobbyMenu.getSubMenuPositioner(1f, 4);
            var rect = subMenuPositioner.getButtonRect(4f);

            rect.width *= 0.80f;
            rect.x -= (rect.width * 0.50f);
           
            if( LobbyMenu.drawButton(rect, "Import Deck") ) {
                App.Popups.ShowTextInput(this, "", "Imports a deck from ScrollsPost", "import", "Deck Import", "Enter an URL:", "Import");
            }

            rect.x += rect.width + 8f;
            if( LobbyMenu.drawButton(rect, "Export Deck") ) {
                ExportDeck();
            }

            GUI.skin = skin;
        }

        public void PopupOk(String type, String choice) {
            if( type == "import" ) {
                ImportDeck(choice);
            }
        }

        public void PopupOk(String type) {

        }

        public void PopupCancel(String type) {

        }

        // Base converters
        public String ConvertBase(String src, String srcAlpha, String dstAlpha) {
            var srcBase = srcAlpha.Length;
            var dstBase = dstAlpha.Length;

            var wet = src;
            var val = 0;
            var mlt = 1;

            while( wet.Length > 0 ) {
                var digit = wet[wet.Length - 1];
                val += mlt * srcAlpha.IndexOf(digit);
                wet = wet.Substring(0, wet.Length - 1);
                mlt *= srcBase;
            }

            var left = val;
            var ret = "";

            while( left >= dstBase ) {
                var digitVal = left % dstBase;
                var digit = dstAlpha[digitVal];
                ret = digit + ret;
                left /= dstBase;
            }

            ret = dstAlpha[left] + ret;

            return ret;
        }
    }
}

