using System;
using System.Linq;

public class APIPriceCheckResult {
    public String error;

	public String name;
    public int card_id;
    public APIPriceCheckPriceResult price;

    public class APIPriceCheckPriceResult {
        public String suggested;
        public String buy;
        public String sell;
    }
}
