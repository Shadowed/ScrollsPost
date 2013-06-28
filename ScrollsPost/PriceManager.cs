using System;
using System.Collections.Generic;
using System.Net;
using JsonFx.Json;


namespace ScrollsPost {
    public class PriceManager : IOkCallback {
        private DateTime nextUpdate = DateTime.UtcNow;
        private Dictionary<int, APIPriceCheckResult> scrolls = new Dictionary<int, APIPriceCheckResult>();
        private Mod mod;

        public PriceManager(Mod mod) {
            this.mod = mod;
        }

        public void LoadData() {
            // Make sure it's time to update
            if( DateTime.Compare(nextUpdate, DateTime.UtcNow) == 1 ) {
                return;
            }

            nextUpdate = DateTime.UtcNow.AddHours(1);

            // Grab new data
            WebClient wc = new WebClient();

            try {
                wc.QueryString.Add("formatting", "1");
                String result = wc.DownloadString(new Uri("http://api.scrollspost.com/v1/prices/1-day"));

                // Load it up
                List<APIPriceCheckResult> prices = new JsonReader().Read<List<APIPriceCheckResult>>(result);
                foreach( APIPriceCheckResult scroll in prices ) {
                    scrolls.Add(scroll.card_id, scroll); 
                }

            } catch ( WebException we ) { // eeeeeeeeeeeeeeee
                App.Popups.ShowOk(this, "fail", "HTTP Error", "Unable to load pricing data, contact support@scrollspost.com for help.\n\n" + we.Message, "Ok");
                mod.WriteLog("Failed to load pricing data", we);
            }
        }

        public APIPriceCheckResult GetScroll(int id) {
            LoadData();
            return scrolls.ContainsKey(id) ? scrolls[id] : null;
        }

        public void PopupOk(String res) {
            return;
        }
    }
}

