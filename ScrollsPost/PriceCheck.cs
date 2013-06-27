using System;
using System.Net;
using JsonFx.Json;

namespace ScrollsPost {
	public class PriceCheck {
        private ScrollsPost.Mod mod;

		public PriceCheck (ScrollsPost.Mod mod, String period, String search) {
            this.mod = mod;

			WebClient wc = new WebClient();
            wc.DownloadStringCompleted += (sender, e) => {
                Loaded(search, e);
            };
    
            try {
    			wc.DownloadStringAsync(new Uri("http://api.scrollspost.com/v1/price/" + period + "/" + search + "?formatting=1"));
            } catch ( WebException we ) { // eeeeeeeeeeeeeeee
                mod.SendMessage("We had an error while loading price info, contact us at support@scrollspost.com for help.");
                mod.WriteLog("Failed to load price check", we);
            }
		}

		private void Loaded(String search, DownloadStringCompletedEventArgs e) {
            try  {
                APIPriceCheckResult res = (APIPriceCheckResult) new JsonReader().Read(e.Result, System.Type.GetType("APIPriceCheckResult"));
                if( res.error == "scroll_not_found" ) {
                    mod.SendMessage(String.Format("[<color=#fde50d>{0}</color>] No scrolls found, please check your spelling.", search));
                } else {
                    mod.SendMessage(String.Format("[<color=#fde50d>{0}</color>] Suggested price <color=#fde50d>{1}g</color>, Buy price <color=#fde50d>{2}g</color>, Sell price <color=#fde50d>{3}g</color>.", res.name, res.price.suggested, res.price.buy, res.price.sell));
                }
             
            } catch ( Exception ex ) {
                mod.SendMessage("We had an error while loading price info, contact us at support@scrollspost.com for help.");
                mod.WriteLog("Failed to load price check", ex);
            }
		}
	}
}

